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
                EntityType type;
                if (properties == null || !Enum.TryParse(properties.ElementType, out type))
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
            if (wanted(EntityType.StructuralWall))
                panels.AddRange(ReadWallPanels(timeout, state));
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

                string spanWarning, skipReason;
                List<Panel> panels = item.FromTsdSlabItem(slab, contours, property, out spanWarning, out skipReason, out approximation);

                if (spanWarning != null)
                    state.Warnings.Add(spanWarning);
                if (skipReason != null)
                    state.Warnings.Skip("slab item(s)", skipReason);
                if (approximation != null)
                    state.Approximate(approximation);

                result.AddRange(panels);
            }

            return result;
        }

        /***************************************************/

        // One Panel per wall panel (see Convert.FromTsdWallPanel). Openings wholly inside a wall panel
        // become its Openings; ones straddling two are left out.
        private List<Panel> ReadWallPanels(int timeout, PanelReadState state)
        {
            List<Panel> result = new List<Panel>();

            IEnumerable<IStructuralWall> walls;
            if (!Async.TryRunSync(ct => m_Model.GetStructuralWallsAsync(null, ct), timeout, "reading structural walls", "no wall Panels have been pulled", out walls, true))
                return result;

            foreach (IStructuralWall wall in walls)
            {
                int count = wall.SpanCount.ValueOrDefault(0);
                if (count <= 0)
                {
                    state.Warnings.Skip("wall(s)", "they have no panels");
                    continue;
                }

                IEnumerable<IStructuralWallPanel> panelsRead;
                string error;
                if (!Async.TryRunSync(ct => wall.GetSpanAsync(Enumerable.Range(0, count), ct), timeout, "reading the panels of wall '" + wall.Name + "'", out panelsRead, out error))
                {
                    state.Warnings.Skip("wall(s)", "their panels could not be read (" + error + ")");
                    continue;
                }

                List<IStructuralWallPanel> wallPanels = panelsRead.ToList();

                // The wall's base support goes on the bottom edge of every wall panel at the base.
                string supportWarning;
                Constraint6DOF baseSupport = wall.StructuralWallData.ValueOrDefault().FromTsdWallBaseSupport(out supportWarning);
                if (supportWarning != null)
                    state.Warnings.Add(supportWarning);

                double baseZ = wallPanels.FromTsdBaseLevel();
                bool baseSupported = false;

                foreach (IStructuralWallPanel wallPanel in wallPanels)
                {
                    string skipReason;
                    Polyline outline = wallPanel.FromTsdWallOutline(out skipReason);
                    if (skipReason != null)
                    {
                        state.Warnings.Skip("wall panel(s)", skipReason);
                        continue;
                    }

                    IStructuralWallPanelData panelData = wallPanel.WallPanelData.ValueOrDefault();
                    IMaterialFragment material = state.Materials.Material(panelData != null ? panelData.Material.ValueOrDefault() : null);
                    ISurfaceProperty property = state.Shared(panelData.FromTsdSurfaceProperty(material));

                    IEnumerable<IWallOpening> openings = outline != null ? ReadWallOpenings(wallPanel, timeout, state) : Enumerable.Empty<IWallOpening>();

                    bool onBase;
                    List<string> openingSkipReasons;
                    Panel panel = wallPanel.FromTsdWallPanel(wall, outline, openings, property, baseSupport, baseZ, out onBase, out skipReason, out openingSkipReasons);

                    foreach (string reason in openingSkipReasons)
                        state.Warnings.Skip("wall opening(s)", reason);

                    if (panel == null)
                    {
                        state.Warnings.Skip("wall panel(s)", skipReason);
                        continue;
                    }

                    baseSupported |= onBase;
                    result.Add(panel);
                }

                if (baseSupport != null && !baseSupported)
                    state.Warnings.Add("wall(s) generate a support along their base, but no wall panel edge was found there to carry it, so they have been pulled unsupported");
            }

            return result;
        }

        /***************************************************/

        // A wall panel's openings, or none, counted as skipped, if they cannot be read.
        private static IEnumerable<IWallOpening> ReadWallOpenings(IStructuralWallPanel wallPanel, int timeout, PanelReadState state)
        {
            IEnumerable<IWallOpening> openings;
            string error;
            if (!Async.TryRunSync(ct => wallPanel.GetOpeningsAsync(false, ct), timeout, "reading wall openings", out openings, out error))
            {
                state.Warnings.Skip("wall opening(s)", "they could not be read (" + error + ")");
                return Enumerable.Empty<IWallOpening>();
            }

            return openings;
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
                string spanWarning, skipReason;
                Panel panel = element.FromTsdLoadingPanel(pointByIndex, state.LoadingPanelProperty(), out spanWarning, out skipReason);

                if (spanWarning != null)
                    state.Warnings.Add(spanWarning);

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
