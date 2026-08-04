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

        // Distinct Nodes at the start and end of every span in the model - identity only, built for
        // the same reason ReadBars exists: so a Bar's Start/End have somewhere real to point to, and so
        // a caller who wants to Pull geometry alongside results can.
        private List<Node> ReadNodes(IList ids)
        {
            List<TsdSpanIdentity> spans = BuildSpanIdentities(TeklaStructuralDesignerConfig.TimeoutSeconds);
            Dictionary<Guid, Node> nodeById = NodesByPointId(spans);

            if (ids == null || ids.Count == 0)
                return nodeById.Values.ToList();

            HashSet<string> requested = new HashSet<string>(ids.Cast<object>().Select(id => id?.ToString()), StringComparer.OrdinalIgnoreCase);
            return nodeById.Where(kvp => requested.Contains(kvp.Key.ToString())).Select(kvp => kvp.Value).ToList();
        }

        /***************************************************/

        private static Dictionary<Guid, Node> NodesByPointId(List<TsdSpanIdentity> spans)
        {
            Dictionary<Guid, Node> nodeById = new Dictionary<Guid, Node>();

            foreach (TsdSpanIdentity span in spans)
            {
                if (span.StartPointId != Guid.Empty && !nodeById.ContainsKey(span.StartPointId))
                    nodeById[span.StartPointId] = Convert.ToBHoM(span.StartPointId, span.StartPosition);

                if (span.EndPointId != Guid.Empty && !nodeById.ContainsKey(span.EndPointId))
                    nodeById[span.EndPointId] = Convert.ToBHoM(span.EndPointId, span.EndPosition);
            }

            return nodeById;
        }

        /***************************************************/
    }
}
