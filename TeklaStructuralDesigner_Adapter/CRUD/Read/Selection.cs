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
using BH.Engine.Geometry;
using BH.oM.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Base;
using BH.oM.Data.Requests;
using BH.oM.Spatial.SettingOut;
using BH.oM.Structure.Elements;
using TSD.API.Remoting.Common.Interfaces;
using TSD.API.Remoting.Structure;
using EntityType = TSD.API.Remoting.Common.EntityType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // The current Tekla Structural Designer selection, as the objects a pull by type returns:
        // members and spans as Bars, slabs, slab items, walls, wall panels, roofs and wind walls as
        // Panels, construction points and supports as Nodes, levels as Levels and grid lines as Grids.
        // A member is reported with its spans and a slab item with its slab, so where both are selected
        // only the more specific is pulled. Public, as the base Pull finds it by signature.
        public IEnumerable<IBHoMObject> Read(SelectionRequest request, ActionConfig actionConfig = null)
        {
            List<IBHoMObject> result = new List<IBHoMObject>();

            if (m_Model == null)
            {
                Engine.Base.Compute.RecordWarning(ErrorMessages.NotConnected());
                return result;
            }

            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;
            TeklaStructuralDesignerPullConfig config = PullConfigOrDefault(actionConfig);

            IEnumerable<ISelectionItem> items;
            if (!Async.TryRunSync(ct => m_Model.GetSelectedEntitiesAsync(ct), timeout, "reading the current selection", "nothing has been pulled", out items, true))
                return result;

            if (!items.Any())
            {
                Engine.Base.Compute.RecordWarning("Nothing is selected in Tekla Structural Designer, so nothing has been pulled. Select the elements to pull there first.");
                return result;
            }

            TsdSelection selection = new TsdSelection();
            foreach (ISelectionItem item in items)
                selection.Add(item);

            if (selection.NotSelectedByUser.Total > 0)
                Engine.Base.Compute.RecordNote("Tekla Structural Designer reported some elements as highlighted or flagged rather than as selected by the user, and they have been left out: " + selection.NotSelectedByUser.Describe() + ".");

            if (selection.NotPulled.Total > 0)
            {
                Engine.Base.Compute.RecordWarning("The selection includes elements that are not pulled by selection, which have been left out: " + selection.NotPulled.Describe() +
                    ". A selection is pulled as Bars for members and spans, Panels for slabs, slab items, structural walls, roofs and wind walls, Nodes for construction points and supports, Levels for levels and Grids for grid lines.");
            }

            result.AddRange(ReadSelectedBars(selection, timeout));
            result.AddRange(ReadSelectedPanels(selection, config, timeout));
            result.AddRange(ReadSelectedNodes(selection, timeout));
            result.AddRange(ReadSelectedLevels(selection, timeout));
            result.AddRange(ReadSelectedGrids(selection));

            return result;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Only the members the selection touches are read.
        private List<Bar> ReadSelectedBars(TsdSelection selection, int timeout)
        {
            if (selection.MemberIndices.Count == 0)
                return new List<Bar>();

            HashSet<int> membersWithSpans = new HashSet<int>(selection.Spans.Select(s => s.Item1));

            List<TsdSpanIdentity> spans = BuildSpanIdentities(timeout, selection.MemberIndices)
                .Where(s => selection.Spans.Contains(Tuple.Create(s.MemberIndex, s.SpanIndex))
                    || (selection.Members.Contains(s.MemberIndex) && !membersWithSpans.Contains(s.MemberIndex)))
                .ToList();

            return BarsOfSpans(spans, timeout);
        }

        /***************************************************/

        // Every element of the selected kinds is read, and the selected ones kept by their Panel fragment.
        private List<Panel> ReadSelectedPanels(TsdSelection selection, TeklaStructuralDesignerPullConfig config, int timeout)
        {
            if (selection.AreaElementTypes.Count == 0)
                return new List<Panel>();

            PanelReadState state = new PanelReadState { Detailed = config.DetailedSurfaceProperties };
            List<Panel> panels = ReadAllPanels(timeout, state, selection.AreaElementTypes);
            state.Report();

            // Slabs and walls with items or panels of their own selected, of which only those are pulled.
            HashSet<int> slabsWithItems = new HashSet<int>(panels
                .Select(p => p.FindFragment<TeklaStructuralDesignerPanelProperties>())
                .Where(p => p != null && p.ElementType == TeklaStructuralDesignerPanelElementType.SlabItem && selection.AreaElements.Contains(Tuple.Create(EntityType.SlabItem, p.ElementIndex)))
                .Select(p => p.SlabIndex));
            HashSet<int> wallsWithPanels = new HashSet<int>(selection.WallPanels.Select(w => w.Item1));

            return panels.Where(p => IsSelected(p, selection, slabsWithItems, wallsWithPanels)).ToList();
        }

        /***************************************************/

        private static bool IsSelected(Panel panel, TsdSelection selection, HashSet<int> slabsWithItems, HashSet<int> wallsWithPanels)
        {
            TeklaStructuralDesignerPanelProperties properties = panel.FindFragment<TeklaStructuralDesignerPanelProperties>();
            if (properties == null)
                return false;

            EntityType type = properties.ElementType.ToTsd();
            if (type == EntityType.Unknown)
                return false;

            bool selected = selection.AreaElements.Contains(Tuple.Create(type, properties.ElementIndex));

            if (type == EntityType.SlabItem)
                return selected || (selection.AreaElements.Contains(Tuple.Create(EntityType.Slab, properties.SlabIndex)) && !slabsWithItems.Contains(properties.SlabIndex));

            if (type == EntityType.StructuralWall)
                return selection.WallPanels.Contains(Tuple.Create(properties.ElementIndex, properties.PanelIndex)) || (selected && !wallsWithPanels.Contains(properties.ElementIndex));

            return selected;
        }

        /***************************************************/

        // The Nodes at the selected points and supports, taken from the same set a Node pull returns.
        private List<Node> ReadSelectedNodes(TsdSelection selection, int timeout)
        {
            List<Node> result = new List<Node>();
            HashSet<int> pointIndices = new HashSet<int>(selection.PointIndices);

            IEnumerable<ISupport> supports;
            if (selection.SupportIndices.Count > 0 &&
                Async.TryRunSync(ct => m_Model.GetSupportsAsync(selection.SupportIndices, ct), timeout, "reading the selected supports", "no Nodes have been pulled for them", out supports))
            {
                foreach (ISupport support in supports)
                {
                    int pointIndex = support.ConstructionPointIndex.ValueOrDefault(-1);
                    if (pointIndex >= 0)
                        pointIndices.Add(pointIndex);
                }
            }

            if (pointIndices.Count == 0)
                return result;

            Dictionary<Guid, Node> nodeById = NodesById(timeout, true);
            double tolerance = Math.Pow(10, -SupportMatchDecimals);
            int withoutNode = 0;

            foreach (ConstructionPointInfo point in BuildConstructionPointInfoByIndex(pointIndices, timeout).Values)
            {
                // A support and the member end it holds are often on coincident points, so fall back to position.
                Node onPoint;
                List<Node> found = nodeById.TryGetValue(point.Id, out onPoint)
                    ? new List<Node> { onPoint }
                    : nodeById.Values.Where(n => n.Position != null && point.Position != null && n.Position.Distance(point.Position) < tolerance).ToList();

                if (found.Count == 0)
                    withoutNode++;

                result.AddRange(found);
            }

            if (withoutNode > 0)
                Engine.Base.Compute.RecordWarning(withoutNode + " selected construction point(s) or support(s) are not at the end of a member or at a support of the structure, so there is no Node to pull for them.");

            return result.Distinct().ToList();
        }

        /***************************************************/

        private List<Level> ReadSelectedLevels(TsdSelection selection, int timeout)
        {
            if (selection.LevelIds.Count == 0)
                return new List<Level>();

            return ReadTsdLevels(timeout, "no Levels have been pulled")
                .Where(plane => selection.LevelIds.Contains(plane.Id))
                .Select(plane => plane.FromTsdLevel())
                .OrderBy(level => level.Elevation)
                .ToList();
        }

        /***************************************************/

        private List<Grid> ReadSelectedGrids(TsdSelection selection)
        {
            if (selection.GridLineIds.Count == 0)
                return new List<Grid>();

            return ReadGrids(null).Where(grid =>
            {
                TeklaStructuralDesignerId id = grid.FindFragment<TeklaStructuralDesignerId>();
                return id != null && id.PersistentId is Guid && selection.GridLineIds.Contains((Guid)id.PersistentId);
            }).ToList();
        }

        /***************************************************/
    }
}
