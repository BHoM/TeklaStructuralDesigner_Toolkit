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
using BH.Engine.Geometry;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Results;
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using Point = BH.oM.Geometry.Point;
using Vector = BH.oM.Geometry.Vector;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Batched solver bar forces)    ****/
        /***************************************************/

        // One call per requested loading case returns the end forces of every solver element in the
        // model at once (Analysis3DResults.GetEndForcesAsync with indices = null). On a model with
        // thousands of spans and dozens of combinations that is the difference between a few dozen
        // calls and hundreds of thousands, which is why the default route (Auto) tries this first.
        //
        // The cost is granularity: results land at solver element boundaries, which can be finer than
        // a span (a span may be more than one solver element). The solver API exposes no geometry for
        // its elements, so where a span is more than one element the interior stations are reported at
        // evenly spaced fractions by element order - exact only where the elements are of equal
        // length. The two ends are always exact.
        private List<BarForce> BarForcesFromSolver(List<TsdSpanIdentity> spans, List<TsdLoadingCaseIdentity> cases, TeklaStructuralDesignerPullConfig config, bool endsOnly, int timeoutSeconds)
        {
            List<BarForce> results = new List<BarForce>();

            LoadingResultType loadingResultType = config.LoadingResultType.ToTeklaStructuralDesigner();

            TSD.API.Remoting.Solver.IModel solverModel = SolverModel(config, timeoutSeconds);
            if (solverModel == null)
                return results;

            List<IElement1D> elements;
            try
            {
                elements = Async.RunSync(ct => solverModel.GetElements1DAsync(null, ct), timeoutSeconds, "reading solver elements").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read solver elements from Tekla Structural Designer. " + e.Message);
                return results;
            }

            // Group solver elements by the span they belong to; OrderAlongSpans below puts each group in
            // order from the span's start to its end. A solver element is traced back to its span by the
            // member's index and the span's index
            // within it, not by the span's Guid: Tekla Structural Designer 2026 leaves both the span and
            // the member Guid on SourceSubEntityInfo empty (checked on a live model - 972 of 972 solver
            // elements had empty Guids, and every one of them had a valid member and span index). Keyed
            // by Guid, every element was skipped and this route silently returned nothing.
            Dictionary<Tuple<int, int>, List<IElement1D>> elementsBySpan = new Dictionary<Tuple<int, int>, List<IElement1D>>();
            foreach (IElement1D element in elements)
            {
                SubEntityInfo source = element.SourceSubEntityInfo;
                if (source.Type != SubEntityType.Span || source.EntityType != EntityType.Member)
                    continue;

                Tuple<int, int> key = Tuple.Create(source.EntityIndex, source.Index);
                List<IElement1D> list;
                if (!elementsBySpan.TryGetValue(key, out list))
                {
                    list = new List<IElement1D>();
                    elementsBySpan[key] = list;
                }
                list.Add(element);
            }

            Dictionary<Tuple<int, int>, TsdSpanIdentity> spanByKey = new Dictionary<Tuple<int, int>, TsdSpanIdentity>();
            foreach (TsdSpanIdentity span in spans)
            {
                Tuple<int, int> key = Tuple.Create(span.MemberIndex, span.SpanIndex);
                if (!spanByKey.ContainsKey(key))
                    spanByKey[key] = span;
            }

            OrderAlongSpans(solverModel, elementsBySpan, spanByKey, timeoutSeconds);

            IAnalysis3DResults analysis3D = Analysis3D(solverModel, config, timeoutSeconds);
            if (analysis3D == null)
                return results;

            List<TsdLoadingCaseIdentity> solvedCases = SolvedCasesOnly(analysis3D, cases, config.AnalysisType.ToString(), timeoutSeconds);
            if (solvedCases.Count == 0)
                return results;

            foreach (TsdLoadingCaseIdentity loadingCase in solvedCases)
            {
                IEnumerable<IElementEndForces> endForces;
                try
                {
                    endForces = Async.RunSync(
                        ct => analysis3D.GetEndForcesAsync(loadingCase.Id, loadingResultType, null, ct),
                        timeoutSeconds, "reading end forces for case '" + loadingCase + "'");
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning("Failed to read end forces for case '" + loadingCase + "'; it has been skipped. " + e.Message);
                    continue;
                }

                Dictionary<int, IElementEndForces> forceByElementIndex = endForces
                    .GroupBy(f => f.ElementIndex)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (KeyValuePair<Tuple<int, int>, List<IElement1D>> spanElements in elementsBySpan)
                {
                    TsdSpanIdentity span;
                    if (!spanByKey.TryGetValue(spanElements.Key, out span))
                        continue; // Not one of the spans this read is scoped to.

                    AddSpanResults(results, spanElements.Value, forceByElementIndex, span, loadingCase.Number, config.SwapMajorMinorAxes, endsOnly);
                }
            }

            return results;
        }

        /***************************************************/

        // Puts the solver elements of each span in order from the span's start to its end, by how far
        // along the span the middle of each element lies. Ascending solver Index does not do this:
        // checked on a live model, a column split into two solver elements had its upper element first,
        // so its base was reported at mid height and its mid height at both ends. The solver API gives
        // an element no geometry of its own, but each solver node lists the elements it joins, so an
        // element's middle is the mean of its nodes. Where the nodes cannot be read, or an element's
        // cannot be found, a span falls back to solver Index order, with a warning.
        private static void OrderAlongSpans(
            TSD.API.Remoting.Solver.IModel solverModel,
            Dictionary<Tuple<int, int>, List<IElement1D>> elementsBySpan,
            Dictionary<Tuple<int, int>, TsdSpanIdentity> spanByKey,
            int timeoutSeconds)
        {
            foreach (List<IElement1D> list in elementsBySpan.Values)
                list.Sort((a, b) => a.Index.CompareTo(b.Index));

            if (!elementsBySpan.Any(kvp => kvp.Value.Count > 1 && spanByKey.ContainsKey(kvp.Key)))
                return;

            List<INode> nodes;
            try
            {
                nodes = Async.RunSync(ct => solverModel.GetNodesAsync(null, ct), timeoutSeconds, "reading solver nodes").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordWarning("Failed to read solver nodes, so the stations inside spans of more than one solver element are in solver order, which may not run from the start of the span to its end. The two ends of each span are unaffected. " + e.Message);
                return;
            }

            Dictionary<int, List<Point>> pointsByElement = new Dictionary<int, List<Point>>();
            foreach (INode node in nodes)
            {
                if (node.Element1dIndices == null)
                    continue;

                Point position = node.Coordinates.ToBHoMPoint();
                foreach (int elementIndex in node.Element1dIndices)
                {
                    List<Point> points;
                    if (!pointsByElement.TryGetValue(elementIndex, out points))
                        pointsByElement[elementIndex] = points = new List<Point>();

                    points.Add(position);
                }
            }

            int unordered = 0;
            foreach (KeyValuePair<Tuple<int, int>, List<IElement1D>> spanElements in elementsBySpan)
            {
                TsdSpanIdentity span;
                if (spanElements.Value.Count < 2 || !spanByKey.TryGetValue(spanElements.Key, out span) || span.StartPosition == null || span.EndPosition == null)
                    continue;

                if (spanElements.Value.Any(e => !pointsByElement.ContainsKey(e.Index) || pointsByElement[e.Index].Count < 2))
                {
                    unordered++;
                    continue;
                }

                Vector along = span.EndPosition - span.StartPosition;
                spanElements.Value.Sort((a, b) => DistanceAlong(pointsByElement[a.Index], span.StartPosition, along).CompareTo(DistanceAlong(pointsByElement[b.Index], span.StartPosition, along)));
            }

            if (unordered > 0)
                Engine.Base.Compute.RecordWarning(unordered + " span(s) of more than one solver element could not be put in order along the span, so their inside stations are in solver order, which may not run from the start of the span to its end. The two ends of each span are unaffected.");
        }

        /***************************************************/

        // How far along a span, from its start, the middle of a solver element's nodes lies.
        private static double DistanceAlong(List<Point> elementPoints, Point spanStart, Vector along)
        {
            Point middle = new Point
            {
                X = elementPoints.Average(p => p.X),
                Y = elementPoints.Average(p => p.Y),
                Z = elementPoints.Average(p => p.Z),
            };

            return (middle - spanStart).DotProduct(along);
        }

        /***************************************************/

        private static void AddSpanResults(
            List<BarForce> results,
            List<IElement1D> spanElements,
            Dictionary<int, IElementEndForces> forceByElementIndex,
            TsdSpanIdentity span,
            int resultCase,
            bool swapMajorMinorAxes,
            bool endsOnly)
        {
            if (spanElements.Count == 0)
                return;

            bool isVertical = span.IsVertical();

            if (endsOnly)
            {
                IElementEndForces firstForces, lastForces;
                if (forceByElementIndex.TryGetValue(spanElements[0].Index, out firstForces))
                    results.Add(firstForces.StartForce.ToBHoM(span.ObjectId, resultCase, 0.0, 2, true, isVertical, swapMajorMinorAxes));
                if (forceByElementIndex.TryGetValue(spanElements[spanElements.Count - 1].Index, out lastForces))
                    results.Add(lastForces.EndForce.ToBHoM(span.ObjectId, resultCase, 1.0, 2, false, isVertical, swapMajorMinorAxes));
                return;
            }

            // n solver elements along a span produce n+1 stations: each element's start, plus the last
            // element's end. Each station inside the span is read as the start of the element after it,
            // so it is converted as a start force.
            int stationCount = spanElements.Count + 1;

            for (int i = 0; i < spanElements.Count; i++)
            {
                IElementEndForces forces;
                if (!forceByElementIndex.TryGetValue(spanElements[i].Index, out forces))
                    continue;

                double position = (double)i / (stationCount - 1);
                results.Add(forces.StartForce.ToBHoM(span.ObjectId, resultCase, position, stationCount, true, isVertical, swapMajorMinorAxes));

                if (i == spanElements.Count - 1)
                    results.Add(forces.EndForce.ToBHoM(span.ObjectId, resultCase, 1.0, stationCount, false, isVertical, swapMajorMinorAxes));
            }
        }

        /***************************************************/

        // Pre-flight validation: names which requested cases have not been solved for this analysis
        // type, rather than the ported tool's behaviour of throwing a bare exception when none have.
        private List<TsdLoadingCaseIdentity> SolvedCasesOnly(IAnalysis3DResults analysis3D, List<TsdLoadingCaseIdentity> cases, string analysisTypeDescription, int timeoutSeconds)
        {
            HashSet<Guid> solvedLoadingIds;
            try
            {
                solvedLoadingIds = new HashSet<Guid>(Async.RunSync(ct => analysis3D.GetSolvedLoadingIdsAsync(ct), timeoutSeconds, "reading solved loading cases"));
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordWarning("Failed to read which loading cases have been solved for " + analysisTypeDescription + "; proceeding without that check. " + e.Message);
                return cases;
            }

            List<TsdLoadingCaseIdentity> solved = new List<TsdLoadingCaseIdentity>();
            List<TsdLoadingCaseIdentity> unsolved = new List<TsdLoadingCaseIdentity>();

            foreach (TsdLoadingCaseIdentity c in cases)
            {
                if (solvedLoadingIds.Contains(c.Id))
                    solved.Add(c);
                else
                    unsolved.Add(c);
            }

            if (unsolved.Count > 0)
            {
                string names = string.Join(", ", unsolved.Take(10).Select(c => c.ToString()));
                if (unsolved.Count > 10)
                    names += ", ...";

                Engine.Base.Compute.RecordWarning(unsolved.Count + " requested loading case(s) have not been solved for " +
                    analysisTypeDescription + " and have been skipped: " + names);
            }

            if (solved.Count == 0)
                Engine.Base.Compute.RecordError(ErrorMessages.NoSolvedCases(analysisTypeDescription));

            return solved;
        }

        /***************************************************/
    }
}
