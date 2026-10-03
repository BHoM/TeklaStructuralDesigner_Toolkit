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
using EntityInfo = TSD.API.Remoting.Common.EntityInfo;
using EntityType = TSD.API.Remoting.Common.EntityType;
using SelectionType = TSD.API.Remoting.Common.SelectionType;
using SubEntityType = TSD.API.Remoting.Common.SubEntityType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // Whatever is selected in Tekla Structural Designer at the moment of the pull, as the objects a
        // pull by type returns for those elements:
        //
        //   a member                              every Bar of the member, one per span
        //   a span of a member                    that Bar
        //   a slab item, roof or wind wall        its Panel
        //   a slab                                the Panels of all its slab items
        //   a structural wall                     the Panels of all its wall panels
        //   a panel of a structural wall          that Panel
        //   a construction point or a support     the Node there
        //   a level                               its Level
        //   a grid line                           its Grid
        //
        // Tekla Structural Designer reports a selected element together with what it belongs to and what
        // it is made of: a member comes with each of its spans, a slab item with its slab. Checked on a
        // live model, where one selected slab item came back as that item and its slab. So the more
        // specific wins: where spans of a member, items of a slab or panels of a wall are in the
        // selection, only those are pulled, and the whole member, slab or wall only where none are.
        //
        // Anything else that is selected - a load, an opening, a foundation - is left out and counted in
        // a warning.
        //
        // Public, unlike the other reads, because the Pull method on the base adapter finds the read for
        // a request by its public signature.
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

            List<ISelectionItem> items;
            try
            {
                items = Async.RunSync(ct => m_Model.GetSelectedEntitiesAsync(ct), timeout, "reading the current selection").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read the current selection from Tekla Structural Designer, so nothing has been pulled. " + e.Message);
                return result;
            }

            if (items.Count == 0)
            {
                Engine.Base.Compute.RecordWarning("Nothing is selected in Tekla Structural Designer, so nothing has been pulled. Select the elements to pull there first.");
                return result;
            }

            Selection selection = new Selection();
            foreach (ISelectionItem item in items)
                selection.Add(item);

            if (selection.NotSelectedByUser.Count > 0)
                Engine.Base.Compute.RecordNote("Tekla Structural Designer reported some elements as highlighted or flagged rather than as selected by the user, and they have been left out: " + Describe(selection.NotSelectedByUser) + ".");

            if (selection.NotPulled.Count > 0)
            {
                Engine.Base.Compute.RecordWarning("The selection includes elements that are not pulled by selection, which have been left out: " + Describe(selection.NotPulled) +
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
        /****            Private Methods                ****/
        /****            (Selection)                    ****/
        /***************************************************/

        // Only the members the selection touches are read, so that picking a few beams out of a large
        // model does not cost a read of every member in it.
        private List<Bar> ReadSelectedBars(Selection selection, int timeout)
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

        // Every element of the kinds selected is read, and the selected ones kept: which element a Panel
        // came from is recorded on its TeklaStructuralDesignerPanelProperties fragment, as it is for the
        // Panels loads are pulled against.
        private List<Panel> ReadSelectedPanels(Selection selection, TeklaStructuralDesignerPullConfig config, int timeout)
        {
            if (selection.AreaElementTypes.Count == 0)
                return new List<Panel>();

            PanelReadState state = new PanelReadState { Detailed = config.DetailedSurfaceProperties };
            List<Panel> panels = ReadAllPanels(timeout, state, selection.AreaElementTypes);
            state.Report();

            // The slabs and walls that have items or panels of their own in the selection, of which only
            // those are pulled.
            HashSet<int> slabsWithItems = new HashSet<int>(panels
                .Select(p => p.FindFragment<TeklaStructuralDesignerPanelProperties>())
                .Where(p => p != null && p.ElementType == EntityType.SlabItem.ToString() && selection.AreaElements.Contains(Tuple.Create(EntityType.SlabItem, p.ElementIndex)))
                .Select(p => p.SlabIndex));
            HashSet<int> wallsWithPanels = new HashSet<int>(selection.WallPanels.Select(w => w.Item1));

            return panels.Where(p => IsSelected(p, selection, slabsWithItems, wallsWithPanels)).ToList();
        }

        /***************************************************/

        private static bool IsSelected(Panel panel, Selection selection, HashSet<int> slabsWithItems, HashSet<int> wallsWithPanels)
        {
            TeklaStructuralDesignerPanelProperties properties = panel.FindFragment<TeklaStructuralDesignerPanelProperties>();
            EntityType type;
            if (properties == null || !Enum.TryParse(properties.ElementType, out type))
                return false;

            bool selected = selection.AreaElements.Contains(Tuple.Create(type, properties.ElementIndex));

            if (type == EntityType.SlabItem)
                return selected || (selection.AreaElements.Contains(Tuple.Create(EntityType.Slab, properties.SlabIndex)) && !slabsWithItems.Contains(properties.SlabIndex));

            if (type == EntityType.StructuralWall)
                return selection.WallPanels.Contains(Tuple.Create(properties.ElementIndex, properties.PanelIndex)) || (selected && !wallsWithPanels.Contains(properties.ElementIndex));

            return selected;
        }

        /***************************************************/

        // The Nodes at the selected construction points, and at the points the selected supports sit on.
        // They are taken from the same set a pull of every Node returns, so a Node pulled by selection is
        // the Node pulled by type, support included.
        private List<Node> ReadSelectedNodes(Selection selection, int timeout)
        {
            List<Node> result = new List<Node>();
            HashSet<int> pointIndices = new HashSet<int>(selection.PointIndices);

            if (selection.SupportIndices.Count > 0)
            {
                try
                {
                    foreach (ISupport support in Async.RunSync(ct => m_Model.GetSupportsAsync(selection.SupportIndices, ct), timeout, "reading the selected supports"))
                    {
                        int pointIndex = support.ConstructionPointIndex.ValueOrDefault(-1);
                        if (pointIndex >= 0)
                            pointIndices.Add(pointIndex);
                    }
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning("Failed to read the selected supports from Tekla Structural Designer, so no Nodes have been pulled for them. " + e.Message);
                }
            }

            if (pointIndices.Count == 0)
                return result;

            Dictionary<Guid, Node> nodeById = NodesById(timeout, true);
            double tolerance = Math.Pow(10, -SupportMatchDecimals);
            int withoutNode = 0;

            foreach (ConstructionPointInfo point in BuildConstructionPointInfoByIndex(pointIndices, timeout).Values)
            {
                // The Node on the point itself or, failing that, the Nodes at its position: a support and
                // the member end it holds up are often on two coincident points - see ApplySupports.
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

        private List<Level> ReadSelectedLevels(Selection selection, int timeout)
        {
            if (selection.LevelIds.Count == 0)
                return new List<Level>();

            return ReadTsdLevels(timeout, "no Levels have been pulled")
                .Where(plane => selection.LevelIds.Contains(plane.Id))
                .Select(plane => plane.ToBHoM())
                .OrderBy(level => level.Elevation)
                .ToList();
        }

        /***************************************************/

        private List<Grid> ReadSelectedGrids(Selection selection)
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

        private static string Describe(Dictionary<string, int> counts)
        {
            return string.Join(", ", counts.Select(c => c.Value + " x " + c.Key));
        }

        /***************************************************/
        /****            Private Nested Types           ****/
        /***************************************************/

        // What is selected, sorted into what each read needs to find it. Elements are keyed by their
        // index, the key the API itself reads them by and the one recorded on a pulled Panel's fragment;
        // levels and grid lines by their Guid, which is what the pulled Level and Grid carry.
        private sealed class Selection
        {
            // The members to read: those selected whole, and those a selected span belongs to.
            public HashSet<int> MemberIndices { get; } = new HashSet<int>();

            public HashSet<int> Members { get; } = new HashSet<int>();

            // Member index and span index.
            public HashSet<Tuple<int, int>> Spans { get; } = new HashSet<Tuple<int, int>>();

            // The kinds of area element to read.
            public HashSet<EntityType> AreaElementTypes { get; } = new HashSet<EntityType>();

            public HashSet<Tuple<EntityType, int>> AreaElements { get; } = new HashSet<Tuple<EntityType, int>>();

            // Wall index and panel index.
            public HashSet<Tuple<int, int>> WallPanels { get; } = new HashSet<Tuple<int, int>>();

            public HashSet<int> PointIndices { get; } = new HashSet<int>();

            public HashSet<int> SupportIndices { get; } = new HashSet<int>();

            public HashSet<Guid> LevelIds { get; } = new HashSet<Guid>();

            public HashSet<Guid> GridLineIds { get; } = new HashSet<Guid>();

            // Counts, by selection type and by kind of element, of what was left out.
            public Dictionary<string, int> NotSelectedByUser { get; } = new Dictionary<string, int>();

            public Dictionary<string, int> NotPulled { get; } = new Dictionary<string, int>();

            public void Add(ISelectionItem item)
            {
                // Tekla Structural Designer keeps the element under the cursor, and the elements it flags
                // with an error or warning status, in selections of their own. Only what the user selected
                // is pulled - reported as Local on the live model this was checked against.
                if (item.Type != SelectionType.GlobalUser && item.Type != SelectionType.Local && item.Type != SelectionType.LocalActive)
                {
                    Count(NotSelectedByUser, item.Type.ToString());
                    return;
                }

                ISelectedSubEntity selectedSubEntity = item as ISelectedSubEntity;
                if (selectedSubEntity != null)
                {
                    SubEntityInfo subEntity = selectedSubEntity.SubEntity;

                    if (subEntity.Type == SubEntityType.Span && subEntity.EntityType == EntityType.Member)
                    {
                        MemberIndices.Add(subEntity.EntityIndex);
                        Spans.Add(Tuple.Create(subEntity.EntityIndex, subEntity.Index));
                    }
                    else if (subEntity.Type == SubEntityType.Span && subEntity.EntityType == EntityType.StructuralWall)
                    {
                        AreaElementTypes.Add(EntityType.StructuralWall);
                        WallPanels.Add(Tuple.Create(subEntity.EntityIndex, subEntity.Index));
                    }
                    else
                        Count(NotPulled, subEntity.Type + " of a " + subEntity.EntityType);

                    return;
                }

                ISelectedEntity selectedEntity = item as ISelectedEntity;
                if (selectedEntity == null)
                {
                    Count(NotPulled, "Unknown");
                    return;
                }

                EntityInfo entity = selectedEntity.Entity;
                switch (entity.Type)
                {
                    case EntityType.Member:
                        MemberIndices.Add(entity.Index);
                        Members.Add(entity.Index);
                        break;
                    case EntityType.SlabItem:
                    case EntityType.Slab:
                    case EntityType.StructuralWall:
                    case EntityType.Roof:
                    case EntityType.WindWall:
                        AreaElementTypes.Add(entity.Type);
                        AreaElements.Add(Tuple.Create(entity.Type, entity.Index));
                        break;
                    case EntityType.ConstructionPoint:
                        PointIndices.Add(entity.Index);
                        break;
                    case EntityType.Support:
                        SupportIndices.Add(entity.Index);
                        break;
                    case EntityType.HorizontalConstructionPlane:
                        LevelIds.Add(entity.Id);
                        break;
                    case EntityType.ConstructionHelper:
                        GridLineIds.Add(entity.Id);
                        break;
                    default:
                        Count(NotPulled, entity.Type.ToString());
                        break;
                }
            }
        }

        /***************************************************/
    }
}
