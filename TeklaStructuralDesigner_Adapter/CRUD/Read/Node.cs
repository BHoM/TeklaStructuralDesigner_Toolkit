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
using BH.Engine.Structure;
using BH.oM.Structure.Elements;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        // Decimal places a Node's position is rounded to when a support is matched to it by position -
        // millimetre precision, the same as the BHoM structural adapters merge coincident nodes at.
        private const int SupportMatchDecimals = 3;

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // Distinct Nodes at the start and end of every span in the model, plus a Node at every supported
        // point that no span ends at. Supports are attached to the Node the member actually ends at, so
        // a Node pulled on its own and the same Node reached through a Bar's Start/End are one object
        // carrying one support.
        private List<Node> ReadNodes(IList ids)
        {
            Dictionary<Guid, Node> nodeById = NodesById(TeklaStructuralDesignerConfig.TimeoutSeconds, true);

            HashSet<string> requested = RequestedIds(ids);
            if (requested == null)
                return nodeById.Values.ToList();

            return nodeById.Where(kvp => requested.Contains(kvp.Key.ToString())).Select(kvp => kvp.Value).ToList();
        }

        /***************************************************/

        // Every Node this adapter pulls, keyed by the Guid of its construction point. Node results are
        // read against the same set, with reportSupports false so that a result pull does not repeat
        // what pulling the Nodes says about their supports.
        private Dictionary<Guid, Node> NodesById(int timeout, bool reportSupports)
        {
            List<TsdSpanIdentity> spans = BuildSpanIdentities(timeout);
            Dictionary<Guid, SupportedPoint> supports = ReadSupports(timeout, reportSupports);

            Dictionary<Guid, Node> nodeById = NodesByPointId(spans);
            HashSet<Guid> attached = ApplySupports(nodeById, supports);

            // Whatever is left holds up something this adapter does not read - a wall, a slab - so it
            // becomes a Node of its own rather than being dropped.
            foreach (KeyValuePair<Guid, SupportedPoint> supported in supports)
            {
                if (attached.Contains(supported.Key) || nodeById.ContainsKey(supported.Key))
                    continue;

                nodeById[supported.Key] = Convert.ToBHoM(supported.Key, supported.Value.Position, supported.Value.Support, supported.Value.Orientation);
            }

            return nodeById;
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

        // Attaches each support to its Node, and reports which supports found one.
        //
        // Matching on the construction point alone is not enough: Tekla Structural Designer does not
        // always put a support and the member end it holds up on the same point. On the model this was
        // written against, half the supports sat on a point of their own, coincident with the point the
        // member ended at - the raker bases and some perimeter columns among them. Keyed only by point,
        // those supports would land on a Node of their own while the Bar's end Node stayed free, and the
        // support would then disappear as soon as the model reached a package that merges coincident
        // nodes, as Robot does.
        //
        // So a support is attached to every Node at its position, found through BHoM's own
        // NodeDistanceComparer, as well as to the one carrying its construction point. Tekla Structural
        // Designer models coincident construction points freely - 124 positions carry more than one on
        // that same model - and a support has to survive whichever of them a downstream package keeps.
        private static HashSet<Guid> ApplySupports(Dictionary<Guid, Node> nodeById, Dictionary<Guid, SupportedPoint> supports)
        {
            HashSet<Guid> attached = new HashSet<Guid>();
            if (supports == null || supports.Count == 0)
                return attached;

            Dictionary<Node, List<Node>> nodesByPosition = new Dictionary<Node, List<Node>>(new NodeDistanceComparer(SupportMatchDecimals));
            foreach (Node node in nodeById.Values)
            {
                if (node.Position == null)
                    continue;

                List<Node> coincident;
                if (!nodesByPosition.TryGetValue(node, out coincident))
                    nodesByPosition[node] = coincident = new List<Node>();

                coincident.Add(node);
            }

            foreach (KeyValuePair<Guid, SupportedPoint> supported in supports)
            {
                List<Node> targets;
                if (!nodesByPosition.TryGetValue(new Node { Position = supported.Value.Position }, out targets))
                {
                    Node byPoint;
                    if (!nodeById.TryGetValue(supported.Key, out byPoint))
                        continue;

                    targets = new List<Node> { byPoint };
                }

                foreach (Node node in targets)
                {
                    node.Support = supported.Value.Support;
                    if (supported.Value.Orientation != null)
                        node.Orientation = supported.Value.Orientation;
                }

                attached.Add(supported.Key);
            }

            return attached;
        }

        /***************************************************/
    }
}
