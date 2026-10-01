/*
 * This file is part of the Buildings and Habitats object Model (BHoM)
 * Copyright (c) 2015 - 2026, the respective contributors. All rights reserved.
 *
 * Each contributor holds copyright over their respective contributions.
 * The project versioning (Git) records all such contribution source information.
 *                                           
 *                                                                              
 * The BHoM is free software: you can redistribute it and/or modify         
 * it under the terms of the GNU Lesser General Public License as published by  
 * the Free Software Foundation, either version 3.0 of the License, or          
 * (at your option) any later version.                                          
 *                                                                              
 * The BHoM is distributed in the hope that it will be useful,              
 * but WITHOUT ANY WARRANTY; without even the implied warranty of               
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the                 
 * GNU Lesser General Public License for more details.                          
 *                                                                            
 * You should have received a copy of the GNU Lesser General Public License     
 * along with this code. If not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.      
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BH.Engine.Base;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;
using TSD.API.Remoting.Loading;

// BHoM and Tekla Structural Designer both declare an ILoad and a LoadType, so those names are
// ambiguous in this file and every one of them is aliased. Aliasing only one side would not help: an
// alias adds a name, it does not remove the ambiguity of the one already there.
using BHoMLoad = BH.oM.Structure.Loads.ILoad;
using TsdLoad = TSD.API.Remoting.Loading.ILoad;
using TsdLoadType = TSD.API.Remoting.Loading.LoadType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Loads)                        ****/
        /***************************************************/

        // Loads hang off loadcases in Tekla Structural Designer - there is no model wide load list -
        // so this is one GetLoadsAsync call per loadcase, asking for member, nodal and planar loads
        // together. Planar loads - area, patch and line loads on slabs, walls, roofs, wind walls and
        // construction planes - are converted in AreaLoads.cs. Snow, diaphragm, temperature and
        // settlement loads are not read: snow loads are generated from roof geometry and reach the
        // frame as derived member loads, and the others have no BHoM element to attach to here.
        //
        // Read in one pass and converted in a second, rather than converted as they arrive, so that
        // every construction point a nodal load references can be resolved in a single batched call
        // once the full set is known - the same batching principle as BuildSpanIdentities. The whole
        // set is then filtered by the requested type, so pulling BarPointLoad and
        // BarUniformlyDistributedLoad separately does not walk every loadcase twice.
        private List<BHoMLoad> ReadLoads(Type type, TeklaStructuralDesignerPullConfig config)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            List<ILoadcase> tsdLoadcases = ReadTsdLoadcases(timeout, "so no loads could be read");
            Dictionary<Guid, Loadcase> loadcaseById = BHoMLoadcases(tsdLoadcases);

            // Pass one: every load, paired with the BHoM Loadcase it belongs to.
            List<PendingLoad> pending = new List<PendingLoad>();
            int derivedSkipped = 0;

            foreach (ILoadcase tsdLoadcase in tsdLoadcases)
            {
                Loadcase loadcase = loadcaseById[tsdLoadcase.Id];

                List<TsdLoad> tsdLoads;
                try
                {
                    tsdLoads = Async.RunSync(
                        ct => tsdLoadcase.GetLoadsAsync(m_PulledLoadTypes, ct),
                        timeout, "reading loads for case '" + loadcase.Name + "'").ToList();
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning("Failed to read loads for case '" + loadcase.Name + "'; it has been skipped. " + e.Message);
                    continue;
                }

                foreach (TsdLoad tsdLoad in tsdLoads)
                {
                    if (!config.IncludeDerivedLoads && tsdLoad.Source != LoadSourceType.User)
                    {
                        derivedSkipped++;
                        continue;
                    }

                    pending.Add(new PendingLoad(tsdLoad, loadcase));
                }
            }

            if (derivedSkipped > 0)
            {
                Engine.Base.Compute.RecordNote(derivedSkipped + " load(s) generated by Tekla Structural Designer rather than applied by a user were skipped. " +
                    "Set IncludeDerivedLoads on the pull configuration to include them.");
            }

            if (pending.Count == 0)
                return new List<BHoMLoad>();

            // Pass two: resolve what the loads point at, then convert.
            Dictionary<Guid, Bar> barBySpanId = pending.Any(p => p.Load is IMemberLoad) ? BarsBySpanId() : new Dictionary<Guid, Bar>();

            HashSet<int> pointIndices = new HashSet<int>(
                pending.Select(p => p.Load).OfType<INodalLoad>().Select(n => n.PointIndex));

            Dictionary<int, Node> nodeByPointIndex = new Dictionary<int, Node>();
            if (pointIndices.Count > 0)
            {
                foreach (KeyValuePair<int, ConstructionPointInfo> point in BuildConstructionPointInfoByIndex(pointIndices, timeout))
                    nodeByPointIndex[point.Key] = Convert.ToBHoM(point.Value.Id, point.Value.Position);
            }

            List<BHoMLoad> loads = new List<BHoMLoad>();
            Dictionary<string, int> skipReasons = new Dictionary<string, int>();

            foreach (PendingLoad p in pending)
            {
                IMemberLoad memberLoad = p.Load as IMemberLoad;
                if (memberLoad != null)
                {
                    Bar bar;
                    if (memberLoad.SpanId == Guid.Empty || !barBySpanId.TryGetValue(memberLoad.SpanId, out bar))
                    {
                        Count(skipReasons, "the span they are applied to could not be matched to a Bar");
                        continue;
                    }

                    // BHoM positions a load along a bar by absolute distance from end A. Tekla
                    // Structural Designer's InProjection measuring means its own distances are measured
                    // on a projection of the bar instead, which cannot be re-expressed without knowing
                    // the plane - so these are refused rather than silently mispositioned.
                    if (memberLoad.Measuring == DistanceMeasuring.InProjection)
                    {
                        Count(skipReasons, "their positions along the bar are measured in projection, which BHoM cannot express");
                        continue;
                    }

                    string skipReason;
                    BHoMLoad converted = memberLoad.ToBHoM(bar, p.Loadcase, out skipReason);
                    if (converted == null)
                        Count(skipReasons, skipReason);
                    else
                        loads.Add(converted);

                    continue;
                }

                INodalLoad nodalLoad = p.Load as INodalLoad;
                if (nodalLoad != null)
                {
                    Node node;
                    if (!nodeByPointIndex.TryGetValue(nodalLoad.PointIndex, out node))
                    {
                        Count(skipReasons, "the construction point they are applied to could not be read");
                        continue;
                    }

                    loads.Add(nodalLoad.ToBHoM(node, p.Loadcase));
                }
            }

            loads.AddRange(ReadPlanarLoads(pending.Where(p => p.Load is IPlanarLoad).ToList(), skipReasons, timeout));

            foreach (KeyValuePair<string, int> reason in skipReasons)
                Engine.Base.Compute.RecordWarning(reason.Value + " load(s) were skipped because " + reason.Key + ".");

            if (loads.Count > 0)
            {
                Engine.Base.Compute.RecordNote("Load magnitudes were converted assuming Tekla Structural Designer reports them in newtons and millimetres, " +
                    "consistent with the units its results use. If pulled loads are out by a factor of a thousand against the model, that assumption does not hold for applied loads - see Convert/ToBHoM/Load.cs.");
            }

            // typeof(ILoad), or any other base the loads share, means "everything you have".
            return type == null || type == typeof(BHoMLoad) ? loads : loads.FilterByType(type).Cast<BHoMLoad>().ToList();
        }

        /***************************************************/


        // Identical reasons are counted and reported once. A model can carry thousands of loads, and
        // one warning per skipped load would bury the rest of the pull's output.
        private static void Count(Dictionary<string, int> reasons, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = "they could not be converted";

            int existing;
            reasons.TryGetValue(reason, out existing);
            reasons[reason] = existing + 1;
        }

        /***************************************************/

        // Bars keyed by the span Guid a member load refers to. Spans whose ends do not resolve to
        // construction points are left out, exactly as ReadBars leaves them out - a load against one of
        // those is then reported as unmatched rather than attached to a Bar with no geometry.
        //
        // These Bars only identify what a load acts on, so supports are not read for them and the
        // section summary is not reported: pulling loads should not repeat what pulling Bars says.
        private Dictionary<Guid, Bar> BarsBySpanId()
        {
            List<TsdSpanIdentity> spans = BuildSpanIdentities(TeklaStructuralDesignerConfig.TimeoutSeconds);
            Dictionary<Guid, Node> nodeById = NodesByPointId(spans);
            BarPropertyCache cache = new BarPropertyCache();

            Dictionary<Guid, Bar> result = new Dictionary<Guid, Bar>();

            foreach (TsdSpanIdentity span in spans)
            {
                Bar bar;
                if (span.SpanId == Guid.Empty || result.ContainsKey(span.SpanId) || !TryBuildBar(span, nodeById, cache, out bar))
                    continue;

                result[span.SpanId] = bar;
            }

            return result;
        }

        /***************************************************/
        /****            Private Nested Types           ****/
        /***************************************************/

        private sealed class PendingLoad
        {
            public TsdLoad Load { get; }
            public Loadcase Loadcase { get; }

            public PendingLoad(TsdLoad load, Loadcase loadcase)
            {
                Load = load;
                Loadcase = loadcase;
            }
        }

        /***************************************************/
    }
}
