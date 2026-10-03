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
using BH.oM.Structure.Elements;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // One Bar per span (see BarResults.cs for why span, not member, is the unit), with its section
        // matched to the BHoM library or built from its shape, and its end Nodes carrying their
        // supports.
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

        // The Bars of the given spans, shared by the read by id above and the read of the current
        // selection.
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

            bar = span.ToBHoM(start, end, cache);
            return true;
        }

        /***************************************************/
    }
}
