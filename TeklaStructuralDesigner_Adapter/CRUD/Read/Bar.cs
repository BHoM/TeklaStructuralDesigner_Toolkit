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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BH.Engine.Base;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // One Bar per span, with its section matched to the BHoM library or built from its shape.
        private List<Bar> ReadBars(IList ids)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            List<TsdSpanIdentity> spans = BuildSpanIdentities(timeout);

            HashSet<string> requested = RequestedIds(ids);
            if (requested != null)
                spans = spans.Where(s => requested.Contains(s.ObjectId)).ToList();

            return BarsOfSpans(spans, timeout);
        }

        /***************************************************/

        // Shared by the read by id and the read of the selection.
        private List<Bar> BarsOfSpans(List<TsdSpanIdentity> spans, int timeout)
        {
            Dictionary<Guid, Node> nodeById = NodesByPointId(spans);
            ApplySupports(nodeById, ReadSupports(timeout));
            BarPropertyCache cache = new BarPropertyCache();

            List<Bar> bars = new List<Bar>();
            int skipped = 0;

            foreach (TsdSpanIdentity span in spans)
            {
                Bar bar;
                if (TryBuildBar(span, nodeById, cache, out bar))
                    bars.Add(bar);
                else
                    skipped++;
            }

            if (skipped > 0)
                Engine.Base.Compute.RecordWarning(skipped + " span(s) could not be resolved to Bar geometry because one or both end points were missing, and have been skipped.");

            // Tekla Structural Designer never loads braces from slabs, roofs or wind walls, but BHoM
            // cannot exclude a Bar from a loading panel, so the receiving package may.
            int braces = bars.Count(b => b.FindFragment<TeklaStructuralDesignerMemberProperties>()?.MemberConstruction?.EndsWith("Brace") == true);
            if (braces > 0)
            {
                Engine.Base.Compute.RecordWarning(braces + " Bar(s) are braces. Tekla Structural Designer leaves braces out of the distribution of slab, roof and wind wall load, " +
                    "but BHoM does not support excluding a Bar from load distribution, so a package that distributes loading panel load, such as Robot through its claddings, may load braces lying in a loading panel's plane. " +
                    "Check the braces in the receiving model, and exclude them from cladding load distribution there.");
            }

            cache.Report();

            return bars;
        }

        /***************************************************/

        // The Bar of a span, or false if either of the span's ends could not be resolved to a Node.
        private static bool TryBuildBar(TsdSpanIdentity span, Dictionary<Guid, Node> nodeById, BarPropertyCache cache, out Bar bar)
        {
            bar = null;

            Node start, end;
            if (span.StartPointId == Guid.Empty || span.EndPointId == Guid.Empty ||
                !nodeById.TryGetValue(span.StartPointId, out start) || !nodeById.TryGetValue(span.EndPointId, out end))
            {
                return false;
            }

            bar = span.FromTsdBar(start, end, cache);
            return true;
        }

        /***************************************************/
    }
}
