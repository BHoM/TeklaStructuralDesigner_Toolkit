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
using BH.oM.Geometry;
using BH.oM.Structure.Constraints;
using BH.oM.Structure.Elements;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SurfaceProperties;
using TSD.API.Remoting.Geometry;
using TSD.API.Remoting.Structure;
using EntityType = TSD.API.Remoting.Common.EntityType;
using TsdAreaElement = TSD.API.Remoting.Structure.IAreaElement;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Slab items and wall panels carry their structural property; roofs and wind walls only collect
        // load, so carry a LoadingPanelProperty. Ids match the TeklaStructuralDesignerId (slab item,
        // roof or wind wall name, or 'WallName:PanelIndex') or the element name.
        private List<Panel> ReadPanels(IList ids, TeklaStructuralDesignerPullConfig config)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            PanelReadState state = new PanelReadState { Detailed = config != null && config.DetailedSurfaceProperties };
            List<Panel> panels = ReadAllPanels(timeout, state, null);
            state.Report();

            HashSet<string> requested = RequestedIds(ids);
            if (requested == null)
                return panels;

            return panels.Where(p => IsRequested(p, requested)).ToList();
        }

        /***************************************************/

        // The Panels an area load on an element lands on, keyed by that element: a slab item, a whole
        // slab, a wall, a roof or a wind wall. Nothing about how they were built is reported.
        private Dictionary<Tuple<EntityType, int>, List<Panel>> PanelsByElement(ICollection<EntityType> elementTypes, int timeout)
        {
            Dictionary<Tuple<EntityType, int>, List<Panel>> result = new Dictionary<Tuple<EntityType, int>, List<Panel>>();

            foreach (Panel panel in ReadAllPanels(timeout, new PanelReadState(), elementTypes))
            {
                TeklaStructuralDesignerPanelProperties properties = panel.FindFragment<TeklaStructuralDesignerPanelProperties>();
                if (properties == null)
                    continue;

                EntityType type = properties.ElementType.ToTsd();
                if (type == EntityType.Unknown)
                    continue;

                AddToGroup(result, Tuple.Create(type, properties.ElementIndex), panel);
                if (type == EntityType.SlabItem && properties.SlabIndex >= 0)
                    AddToGroup(result, Tuple.Create(EntityType.Slab, properties.SlabIndex), panel);
            }

            return result;
        }

        /***************************************************/

        // Every Panel of the given kinds of element, or of every kind if elementTypes is null.
        private List<Panel> ReadAllPanels(int timeout, PanelReadState state, ICollection<EntityType> elementTypes)
        {
            Func<EntityType, bool> wanted = t => elementTypes == null || elementTypes.Contains(t);

            List<Panel> panels = new List<Panel>();
            if (wanted(EntityType.SlabItem) || wanted(EntityType.Slab))
                panels.AddRange(ReadSlabPanels(timeout, state));
            if (wanted(EntityType.Roof) || wanted(EntityType.WindWall))
                panels.AddRange(ReadLoadingPanels(timeout, state));

            return panels;
        }

        /***************************************************/

        private static void AddToGroup(Dictionary<Tuple<EntityType, int>, List<Panel>> groups, Tuple<EntityType, int> key, Panel panel)
        {
            List<Panel> group;
            if (!groups.TryGetValue(key, out group))
                groups[key] = group = new List<Panel>();

            group.Add(panel);
        }

        /***************************************************/

        // One Panel per slab item, from its Complete contour (see Convert.FromTsdSlabItem). The slab
        // supplies the type, deck and material; the item may override the depth.
        private List<Panel> ReadSlabPanels(int timeout, PanelReadState state)
        {
            List<Panel> result = new List<Panel>();

            const string consequence = "no slab Panels have been pulled";
            IEnumerable<ISlab> slabs;
            IEnumerable<ISlabItem> items;
            if (!Async.TryRunSync(ct => m_Model.GetSlabsAsync(null, ct), timeout, "reading slabs", consequence, out slabs, true) ||
                !Async.TryRunSync(ct => m_Model.GetSlabItemsAsync(null, ct), timeout, "reading slab items", consequence, out items, true))
                return result;

            Dictionary<int, ISlab> slabByIndex = slabs.ToDictionary(s => s.Index);

            foreach (ISlabItem item in items)
            {
                // Checked before the outline is read, as there is no plane to place it in.
                if (item.ElementPlane.ValueOrDefault() == null)
                {
                    state.Warnings.Skip("slab item(s)", "Tekla Structural Designer reported no plane for them");
                    continue;
                }

                IEnumerable<IPolygonWithHoles2D> contours;
                string error;
                if (!Async.TryRunSync(ct => item.GetContoursAsync(SlabContourType.Complete, ct), timeout, "reading the outline of slab item '" + item.Name + "'", out contours, out error))
                {
                    state.Warnings.Skip("slab item(s)", "their outline could not be read (" + error + ")");
                    continue;
                }

                ISlab slab;
                slabByIndex.TryGetValue(item.SlabIndex.ValueOrDefault(-1), out slab);
                ISlabData slabData = slab != null ? slab.SlabData.ValueOrDefault() : null;
                IMaterialFragment material = state.Materials.Material(slabData != null ? slabData.Material.ValueOrDefault() : null);

                string approximation;
                ISurfaceProperty property = state.Shared(item.FromTsdSurfaceProperty(slabData, material, state.Detailed, out approximation));
                if (approximation != null)
                    state.Approximate(approximation);

                List<string> warnings;
                string skipReason;
                List<Panel> panels = item.FromTsdSlabItem(slab, contours, property, out warnings, out skipReason, out approximation);

                foreach (string warning in warnings)
                    state.Warnings.Add(warning);
                if (skipReason != null)
                    state.Warnings.Skip("slab item(s)", skipReason);
                if (approximation != null)
                    state.Approximate(approximation);

                result.AddRange(panels);
            }

            return result;
        }

        /***************************************************/

        // Roofs and wind walls, outlined by their construction points (see Convert.FromTsdLoadingPanel).
        private List<Panel> ReadLoadingPanels(int timeout, PanelReadState state)
        {
            List<Panel> result = new List<Panel>();

            const string consequence = "no loading Panels have been pulled";
            IEnumerable<IRoof> roofs;
            IEnumerable<IWindWall> windWalls;
            if (!Async.TryRunSync(ct => m_Model.GetRoofsAsync(null, ct), timeout, "reading roofs", consequence, out roofs, true) ||
                !Async.TryRunSync(ct => m_Model.GetWindWallsAsync(null, ct), timeout, "reading wind walls", consequence, out windWalls, true))
                return result;

            List<TsdAreaElement> elements = roofs.Cast<TsdAreaElement>().Concat(windWalls).ToList();
            if (elements.Count == 0)
                return result;

            HashSet<int> pointIndices = new HashSet<int>(elements.SelectMany(e => e.ConstructionPointIndices ?? new List<int>()));
            Dictionary<int, ConstructionPointInfo> pointByIndex = BuildConstructionPointInfoByIndex(pointIndices, timeout);

            foreach (TsdAreaElement element in elements)
            {
                List<string> warnings;
                string skipReason;
                Panel panel = element.FromTsdLoadingPanel(pointByIndex, state.LoadingPanelProperty(), out warnings, out skipReason);

                foreach (string warning in warnings)
                    state.Warnings.Add(warning);

                if (panel == null)
                {
                    state.Warnings.Skip(element is IRoof ? "roof(s)" : "wind wall(s)", skipReason);
                    continue;
                }

                result.Add(panel);
            }

            return result;
        }

        /***************************************************/

        private static bool IsRequested(Panel panel, HashSet<string> requested)
        {
            TeklaStructuralDesignerId id = panel.FindFragment<TeklaStructuralDesignerId>();
            if (id != null && ((id.Id != null && requested.Contains(id.Id.ToString())) || (id.PersistentId != null && requested.Contains(id.PersistentId.ToString()))))
                return true;

            TeklaStructuralDesignerPanelProperties properties = panel.FindFragment<TeklaStructuralDesignerPanelProperties>();
            return properties != null && requested.Contains(properties.ElementName);
        }

        /***************************************************/
    }
}
