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

        // One Bar per span (see BarResults.cs for why span, not member, is the unit). This exists so
        // that ObjectIds in a BarResultRequest can be matched against real Bar objects, and so a
        // caller who wants geometry alongside results can Pull it - not to be a complete geometry
        // adapter: Push is not supported, and the section/material conversions are deliberately lossy.
        private List<Bar> ReadBars(IList ids)
        {
            List<TsdSpanIdentity> spans = BuildSpanIdentities(TeklaStructuralDesignerConfig.TimeoutSeconds);

            if (ids != null && ids.Count > 0)
            {
                HashSet<string> requested = new HashSet<string>(ids.Cast<object>().Select(id => id?.ToString()), StringComparer.OrdinalIgnoreCase);
                spans = spans.Where(s => requested.Contains(s.ObjectId)).ToList();
            }

            Dictionary<Guid, Node> nodeById = NodesByPointId(spans);

            List<Bar> bars = new List<Bar>();
            int skipped = 0;

            foreach (TsdSpanIdentity span in spans)
            {
                Node start, end;
                if (span.StartPointId == Guid.Empty || span.EndPointId == Guid.Empty ||
                    !nodeById.TryGetValue(span.StartPointId, out start) || !nodeById.TryGetValue(span.EndPointId, out end))
                {
                    skipped++;
                    continue;
                }

                bars.Add(span.ToBHoM(start, end));
            }

            if (skipped > 0)
                Engine.Base.Compute.RecordWarning(skipped + " span(s) could not be resolved to Bar geometry because one or both end points were missing, and have been skipped.");

            return bars;
        }

        /***************************************************/
    }
}
