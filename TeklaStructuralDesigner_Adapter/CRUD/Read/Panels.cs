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
using BH.Engine.Geometry;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Geometry;
using BH.oM.Structure.Constraints;
using BH.oM.Structure.Elements;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SurfaceProperties;
using TSD.API.Remoting.Geometry;
using TSD.API.Remoting.Structure;
using BHoMPanelType = BH.oM.Structure.SurfaceProperties.PanelType;
using BHoMPoint = BH.oM.Geometry.Point;
using BHoMVector = BH.oM.Geometry.Vector;
using EntityType = TSD.API.Remoting.Common.EntityType;
using TsdAreaElement = TSD.API.Remoting.Structure.IAreaElement;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        // Angles (in radians) and unit vector lengths below this are zero when deciding which way an
        // element spans.
        private const double SpanAngleTolerance = 1e-4;

        // Distance (in metres) within which a wall panel's bottom edge is at the base of its wall, and a
        // Panel edge is the bottom edge it was built from - the millimetre precision used elsewhere.
        private const double WallBaseTolerance = 1e-3;

        /***************************************************/
        /****            Private Methods                ****/
        /****            (Panels)                       ****/
        /***************************************************/

        // Every 2D element in the model as a BHoM Panel: slab items, structural wall panels, roofs and
        // wind walls. Push is not supported.
        //
        // Slab items and wall panels carry their structural property. Roofs and wind walls exist in
        // Tekla Structural Designer only to collect load and pass it to the frame, so they carry a
        // LoadingPanelProperty - no material, no thickness, no stiffness - and their span direction.
        //
        // The span direction of every Panel is its local x; see Convert.ToBHoMPanel.
        //
        // The ids of the request are matched against the readable identifier on each Panel's
        // TeklaStructuralDesignerId - the slab item, roof or wind wall name, or 'WallName:PanelIndex' for
        // a wall panel - or against the element's name, so a whole wall can be asked for by name.
        private List<Panel> ReadPanels(IList ids, TeklaStructuralDesignerPullConfig config)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            PanelReadState state = new PanelReadState { Detailed = config != null && config.DetailedSurfaceProperties };
            List<Panel> panels = ReadAllPanels(timeout, state, null);
            state.Report();

            if (ids == null || ids.Count == 0)
                return panels;

            HashSet<string> requested = new HashSet<string>(ids.Cast<object>().Where(id => id != null).Select(id => id.ToString()), StringComparer.OrdinalIgnoreCase);
            return panels.Where(p => IsRequested(p, requested)).ToList();
        }

        /***************************************************/

        // The Panels an area load on a Tekla Structural Designer element lands on, keyed by that
        // element: a slab item, a whole slab (all of its items), a structural wall (all of its panels),
        // a roof or a wind wall. Read from each Panel's TeklaStructuralDesignerPanelProperties fragment,
        // so that which element a Panel belongs to is recorded in one place. Only the kinds of element
        // the loads are on are read. These Panels only identify what a load acts on, so, as for the Bars
        // loads are pulled against, nothing about how they were built is reported.
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

        // Every Panel of the given kinds of element, or of every kind if elementTypes is null. The
        // ElementType on each Panel's fragment is the name of the EntityType it came from.
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

        // One Panel per slab item. A slab in Tekla Structural Designer is a group of slab items sharing
        // one set of slab data (type, deck, material); the slab item is the panel that is drawn and
        // analysed, and may override the slab's depth.
        //
        // The outline comes from the item's Complete contour: its definition with column drops cut out
        // of it and slab openings applied as holes, which become the Panel's Openings. The contour is in
        // the local coordinates of the item's element plane.
        private List<Panel> ReadSlabPanels(int timeout, PanelReadState state)
        {
            List<Panel> result = new List<Panel>();

            List<ISlabItem> items;
            Dictionary<int, ISlab> slabByIndex;
            try
            {
                slabByIndex = Async.RunSync(ct => m_Model.GetSlabsAsync(null, ct), timeout, "reading slabs").ToDictionary(s => s.Index);
                items = Async.RunSync(ct => m_Model.GetSlabItemsAsync(null, ct), timeout, "reading slab items").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read slabs from Tekla Structural Designer, so no slab Panels have been pulled. " + e.Message);
                return result;
            }

            foreach (ISlabItem item in items)
            {
                TsdPlane plane = item.ElementPlane.ValueOrDefault();
                if (plane == null)
                {
                    state.Skip("slab item(s)", "Tekla Structural Designer reported no plane for them");
                    continue;
                }

                List<IPolygonWithHoles2D> contours;
                try
                {
                    contours = Async.RunSync(ct => item.GetContoursAsync(SlabContourType.Complete, ct), timeout, "reading the outline of slab item '" + item.Name + "'").ToList();
                }
                catch (Exception e)
                {
                    state.Skip("slab item(s)", "their outline could not be read (" + e.Message + ")");
                    continue;
                }

                ISlab slab;
                slabByIndex.TryGetValue(item.SlabIndex.ValueOrDefault(-1), out slab);
                ISlabData slabData = slab != null ? slab.SlabData.ValueOrDefault() : null;
                ISlabItemData itemData = item.SlabItemData.ValueOrDefault();

                double depth = itemData != null && itemData.OverrideSlabDepth.ValueOrDefault(false)
                    ? itemData.Depth.ValueOrDefault(0)
                    : (slabData != null ? slabData.Depth.ValueOrDefault(0) : 0);
                if (depth <= 0 && itemData != null)
                    depth = itemData.Depth.ValueOrDefault(0);

                BHoMPanelType panelType = item.IsColumnDrop.ValueOrDefault(false) ? BHoMPanelType.DropPanel : BHoMPanelType.Slab;
                IMaterialFragment material = state.Materials.Material(slabData != null ? slabData.Material.ValueOrDefault() : null);

                string approximation;
                ISurfaceProperty property = state.Shared(slabData.ToBHoMSurfaceProperty(depth, material, panelType, state.Detailed, out approximation));
                if (approximation != null)
                    state.Approximate(approximation);

                DecompositionType decomposition = slabData != null ? slabData.DecompositionType.ValueOrDefault(DecompositionType.Unknown) : DecompositionType.Unknown;

                TeklaStructuralDesignerPanelProperties fragment = new TeklaStructuralDesignerPanelProperties
                {
                    ElementType = "SlabItem",
                    ElementName = item.Name,
                    ElementIndex = item.Index,
                    ElementId = item.Id,
                    SlabName = slab != null ? slab.Name : "",
                    SlabIndex = slab != null ? slab.Index : -1,
                    ElementSubType = slabData != null ? slabData.SlabType.ValueOrDefault(SlabType.Unknown).ToString() : "",
                    SpanType = decomposition == DecompositionType.Unknown ? "" : decomposition.ToString(),
                    ElementGroupName = item.ElementGroupName ?? "",
                };

                BHoMVector localX = SpanDirection(item, plane, state);
                BHoMVector normal = Normal(plane);

                int built = 0;
                foreach (IPolygonWithHoles2D contour in contours)
                {
                    IPolygon2D outer = contour.Contour.ValueOrDefault();
                    Polyline outline = outer != null ? outer.Vertices.ValuesOrEmpty().ToBHoMPolyline(plane) : null;
                    List<Polyline> holes = contour.Holes.ValuesOrEmpty().Select(h => h.Vertices.ValuesOrEmpty().ToBHoMPolyline(plane)).Where(h => h != null).ToList();

                    Panel panel = Convert.ToBHoMPanel(outline, holes, property, localX, normal, item.Name);
                    if (panel == null)
                        continue;

                    result.Add(panel.SetIdentity(item.Name, item.Id, built == 0 ? fragment : fragment.ShallowClone(), built));
                    built++;
                }

                if (built == 0)
                    state.Skip("slab item(s)", "their outline did not enclose an area");
                else if (built > 1)
                    state.Approximate("they are cut into separate pieces by openings or column drops, and each piece has been pulled as a Panel of its own sharing the slab item's identifier");
            }

            return result;
        }

        /***************************************************/

        // One Panel per wall panel. A structural wall in Tekla Structural Designer is split into panels
        // between levels, each with its own thickness and material; the wall panel is what is analysed.
        //
        // The outline is the panel's bottom and top edges as Tekla Structural Designer reports them, and
        // local x runs horizontally along the wall. Wall openings become Openings of every wall panel
        // they lie wholly inside; one that straddles two panels cannot be split between them and is left
        // out, with a warning.
        private List<Panel> ReadWallPanels(int timeout, PanelReadState state)
        {
            List<Panel> result = new List<Panel>();

            List<IStructuralWall> walls;
            try
            {
                walls = Async.RunSync(ct => m_Model.GetStructuralWallsAsync(null, ct), timeout, "reading structural walls").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read structural walls from Tekla Structural Designer, so no wall Panels have been pulled. " + e.Message);
                return result;
            }

            foreach (IStructuralWall wall in walls)
            {
                int count = wall.SpanCount.ValueOrDefault(0);
                if (count <= 0)
                {
                    state.Skip("wall(s)", "they have no panels");
                    continue;
                }

                List<IStructuralWallPanel> wallPanels;
                try
                {
                    wallPanels = Async.RunSync(ct => wall.GetSpanAsync(Enumerable.Range(0, count), ct), timeout, "reading the panels of wall '" + wall.Name + "'").ToList();
                }
                catch (Exception e)
                {
                    state.Skip("wall(s)", "their panels could not be read (" + e.Message + ")");
                    continue;
                }

                IStructuralWallData wallData = wall.StructuralWallData.ValueOrDefault();
                TsdPlane wallPlane = wall.ElementPlane.ValueOrDefault();
                BHoMVector wallNormal = wallPlane != null ? Normal(wallPlane) : null;

                // The support the wall generates along its base, carried by the bottom edge of every wall
                // panel that sits at the base.
                Constraint6DOF baseSupport = WallBaseSupport(wallData, state);
                double baseZ = wallPanels
                    .Select(p => p.BottomSegment.ValueOrDefault())
                    .Where(s => s != null)
                    .SelectMany(s => new[] { s.GetPoint(Location.Start).ToBHoMPoint().Z, s.GetPoint(Location.End).ToBHoMPoint().Z })
                    .DefaultIfEmpty(double.NaN)
                    .Min();
                bool baseSupported = false;

                foreach (IStructuralWallPanel wallPanel in wallPanels)
                {
                    ISegment3D bottom = wallPanel.BottomSegment.ValueOrDefault();
                    ISegment3D top = wallPanel.TopSegment.ValueOrDefault();
                    if (bottom == null || top == null)
                    {
                        state.Skip("wall panel(s)", "Tekla Structural Designer reported no top or bottom edge for them");
                        continue;
                    }

                    BHoMPoint b0 = bottom.GetPoint(Location.Start).ToBHoMPoint();
                    BHoMPoint b1 = bottom.GetPoint(Location.End).ToBHoMPoint();
                    BHoMPoint t0 = top.GetPoint(Location.Start).ToBHoMPoint();
                    BHoMPoint t1 = top.GetPoint(Location.End).ToBHoMPoint();

                    // The top edge is expected to run the same way as the bottom one; if it runs the other
                    // way, walking bottom then top back would cross the outline over itself.
                    if (b1.Distance(t1) > b1.Distance(t0))
                    {
                        BHoMPoint swap = t0;
                        t0 = t1;
                        t1 = swap;
                    }

                    Polyline outline = Convert.ClosedPolyline(new[] { b0, b1, t1, t0 });

                    IStructuralWallPanelData panelData = wallPanel.WallPanelData.ValueOrDefault();
                    IMaterialFragment material = state.Materials.Material(panelData != null ? panelData.Material.ValueOrDefault() : null);
                    ISurfaceProperty property = state.Shared(panelData.ToBHoMSurfaceProperty(material));

                    List<Polyline> openings = WallOpenings(wallPanel, outline, timeout, state);

                    string id = wall.Name + ":" + wallPanel.Index;
                    Panel panel = Convert.ToBHoMPanel(outline, openings, property, b1 - b0, wallNormal, id);
                    if (panel == null)
                    {
                        state.Skip("wall panel(s)", "their outline did not enclose an area");
                        continue;
                    }

                    if (baseSupport != null && Math.Max(b0.Z, b1.Z) - baseZ < WallBaseTolerance && SupportEdge(panel, b0, b1, baseSupport))
                        baseSupported = true;

                    result.Add(panel.SetIdentity(id, wallPanel.Id, new TeklaStructuralDesignerPanelProperties
                    {
                        ElementType = "StructuralWall",
                        ElementName = wall.Name,
                        ElementIndex = wall.Index,
                        ElementId = wall.Id,
                        PanelIndex = wallPanel.Index,
                        ElementSubType = wallData != null ? wallData.StructuralWallType.ValueOrDefault(StructuralWallType.Unknown).ToString() : "",
                        ElementGroupName = wall.ElementGroupName ?? "",
                    }));
                }

                if (baseSupport != null && !baseSupported)
                    state.Warn("wall(s) generate a support along their base, but no wall panel edge was found there to carry it, so they have been pulled unsupported");
            }

            return result;
        }

        /***************************************************/

        // The support a structural wall generates along its base, or null if it generates none.
        //
        // Tekla Structural Designer holds this on the wall itself - its GenerateSupport switch and
        // SupportData - not as a support on a construction point, so it is not among the supports the
        // Nodes are pulled with, and walls used to be pulled free at their base. A BHoM Panel carries a
        // line support on the Support of an external edge, which Robot applies to that edge of the
        // panel. The SupportData is the same type as a point support's, so it is converted the same way,
        // with springs and partial fixity reported rather than converted.
        //
        // AutomaticGenerateSupport leaves Tekla Structural Designer to decide whether the wall is
        // supported. Where it is switched on and GenerateSupport reports no support, that is taken as
        // Tekla Structural Designer's decision, but it has not been checked against a live model, so it
        // is warned about.
        private static Constraint6DOF WallBaseSupport(IStructuralWallData wallData, PanelReadState state)
        {
            if (wallData == null)
                return null;

            if (!wallData.GenerateSupport.ValueOrDefault(false))
            {
                if (wallData.AutomaticGenerateSupport.ValueOrDefault(false))
                    state.Warn("wall(s) leave Tekla Structural Designer to decide whether they generate a base support, and it reported none, so they have been pulled unsupported at the base. Check this against the model");

                return null;
            }

            bool isPartial;
            Constraint6DOF support = wallData.SupportData.ValueOrDefault().ToBHoM(out isPartial);
            if (support == null)
            {
                state.Warn("wall(s) generate a base support but Tekla Structural Designer reported no fixity for it, so they have been pulled unsupported at the base");
                return null;
            }

            if (isPartial)
                state.Warn("wall base support(s) have spring or partial fixity in at least one direction. Their fixed and free directions have been pulled, but the springs have not");

            return support;
        }

        /***************************************************/

        // Puts the support on the external edge of the Panel running between start and end, in either
        // direction - found by its end points, because Convert.ToBHoMPanel may have reversed the outline
        // or started it elsewhere. False if no edge runs between them.
        private static bool SupportEdge(Panel panel, BHoMPoint start, BHoMPoint end, Constraint6DOF support)
        {
            foreach (Edge edge in panel.ExternalEdges ?? new List<Edge>())
            {
                if (edge?.Curve == null)
                    continue;

                BHoMPoint edgeStart = edge.Curve.IStartPoint();
                BHoMPoint edgeEnd = edge.Curve.IEndPoint();

                if ((edgeStart.Distance(start) < WallBaseTolerance && edgeEnd.Distance(end) < WallBaseTolerance) ||
                    (edgeStart.Distance(end) < WallBaseTolerance && edgeEnd.Distance(start) < WallBaseTolerance))
                {
                    edge.Support = support;
                    return true;
                }
            }

            return false;
        }

        /***************************************************/

        // A wall opening is a rectangle Width wide and Height high, in the coordinates of its reference
        // plane, whose corner is Offset from its reference point - not from the origin of the plane, which
        // can be far from the wall. Checked on a live model: an opening with Offset (-1990, 1175), 655 by
        // 600, referenced from the wall's start point, lies exactly where the solver mesh has its four
        // corner nodes. Width and Height run along the plane's own positive axes, whichever way the wall
        // itself runs - which is why the Offset along the wall can be negative.
        private static List<Polyline> WallOpenings(IStructuralWallPanel wallPanel, Polyline outline, int timeout, PanelReadState state)
        {
            List<Polyline> result = new List<Polyline>();
            if (outline == null)
                return result;

            List<IWallOpening> openings;
            try
            {
                openings = Async.RunSync(ct => wallPanel.GetOpeningsAsync(false, ct), timeout, "reading wall openings").ToList();
            }
            catch (Exception e)
            {
                state.Skip("wall opening(s)", "they could not be read (" + e.Message + ")");
                return result;
            }

            foreach (IWallOpening opening in openings)
            {
                TsdPlane plane = opening.ReferencePlane.ValueOrDefault();
                double width = opening.Width.ValueOrDefault(0);
                double height = opening.Height.ValueOrDefault(0);
                if (plane == null || width <= 0 || height <= 0)
                {
                    state.Skip("wall opening(s)", "they have no reference plane or no size");
                    continue;
                }

                if (opening.ReferenceCoordinates == null || !opening.ReferenceCoordinates.IsApplicable)
                {
                    state.Skip("wall opening(s)", "they have no reference point to be placed from");
                    continue;
                }

                Vector3D reference = opening.ReferenceCoordinates.Value;
                Point2D origin = plane.Global2Local(new Point3D(reference.X, reference.Y, reference.Z));
                Vector2D offset = opening.Offset.ValueOrDefault();

                double x = origin.X + offset.X;
                double y = origin.Y + offset.Y;

                Point2D[] corners =
                {
                    new Point2D(x, y),
                    new Point2D(x + width, y),
                    new Point2D(x + width, y + height),
                    new Point2D(x, y + height),
                };

                Polyline rectangle = corners.ToBHoMPolyline(plane);
                if (rectangle == null)
                    continue;

                if (!outline.IsContaining(rectangle.ControlPoints, true, 1e-3))
                {
                    state.Skip("wall opening(s)", "they straddle more than one wall panel, or fall outside the panel they were reported against");
                    continue;
                }

                result.Add(rectangle);
            }

            return result;
        }

        /***************************************************/

        // Roofs and wind walls, as loading panels. Their outline is the construction points they are
        // drawn between, and they span one way along the span direction set in Tekla Structural Designer.
        private List<Panel> ReadLoadingPanels(int timeout, PanelReadState state)
        {
            List<Panel> result = new List<Panel>();

            List<IRoof> roofs = new List<IRoof>();
            List<IWindWall> windWalls = new List<IWindWall>();
            try
            {
                roofs = Async.RunSync(ct => m_Model.GetRoofsAsync(null, ct), timeout, "reading roofs").ToList();
                windWalls = Async.RunSync(ct => m_Model.GetWindWallsAsync(null, ct), timeout, "reading wind walls").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read roofs and wind walls from Tekla Structural Designer, so no loading Panels have been pulled. " + e.Message);
                return result;
            }

            List<TsdAreaElement> elements = roofs.Cast<TsdAreaElement>().Concat(windWalls).ToList();
            if (elements.Count == 0)
                return result;

            // One batched call for every construction point the elements are drawn between.
            HashSet<int> pointIndices = new HashSet<int>(elements.SelectMany(e => e.ConstructionPointIndices ?? new List<int>()));
            Dictionary<int, ConstructionPointInfo> pointByIndex = BuildConstructionPointInfoByIndex(pointIndices, timeout);

            foreach (TsdAreaElement element in elements)
            {
                IRoof roof = element as IRoof;
                string kind = roof != null ? "roof(s)" : "wind wall(s)";

                IReadOnlyList<int> indices = element.ConstructionPointIndices ?? new List<int>();
                List<BHoMPoint> points = new List<BHoMPoint>();
                foreach (int index in indices)
                {
                    ConstructionPointInfo point;
                    if (pointByIndex.TryGetValue(index, out point) && point.Position != null)
                        points.Add(point.Position);
                }

                if (points.Count != indices.Count)
                {
                    state.Skip(kind, "one or more of the construction points they are drawn between could not be read");
                    continue;
                }

                TsdPlane plane = element.ElementPlane.ValueOrDefault();
                BHoMVector normal = plane != null ? Normal(plane) : null;
                BHoMVector span = null;
                if (plane != null)
                    span = roof != null ? SpanDirection(element, plane, state) : PlaneX(plane);

                // Wind wall span direction is not supported: the API does not report a wind wall's
                // rotation angle, so a wind wall keeps the x axis of its element plane, and every one
                // pulled is warned about.
                if (roof == null)
                    state.Warn("wind wall(s) have been pulled spanning along the x axis of their element plane, which is horizontal along the wall. Wind wall span direction is not supported at the moment: Tekla Structural Designer's API does not report a wind wall's rotation angle, so a wind wall rotated in Tekla Structural Designer - 90 degrees to span vertically between floors - will span the wrong way. Check the cladding direction of every wind wall in the receiving model");

                Panel panel = Convert.ToBHoMPanel(Convert.ClosedPolyline(points), null, null, span, normal, element.Name);
                if (panel == null)
                {
                    state.Skip(kind, "their outline did not enclose an area");
                    continue;
                }

                panel.Property = state.LoadingPanelProperty();

                IRoofData roofData = roof != null ? roof.RoofData.ValueOrDefault() : null;
                result.Add(panel.SetIdentity(element.Name, element.Id, new TeklaStructuralDesignerPanelProperties
                {
                    ElementType = roof != null ? "Roof" : "WindWall",
                    ElementName = element.Name,
                    ElementIndex = element.Index,
                    ElementId = element.Id,
                    ElementSubType = roofData != null ? roofData.RoofType.ValueOrDefault(RoofType.Unknown).ToString() : "",
                    SpanType = "OneWay",
                    ElementGroupName = element.ElementGroupName ?? "",
                }));
            }

            return result;
        }

        /***************************************************/

        // The direction a slab item or roof spans in: the x axis of its element plane, turned through the
        // element's RotationAngle where the plane has not already been.
        //
        // The API documents the element plane as respecting the element's orientation, but project
        // models tested in October 2026 showed rotated slab items whose element plane x had not been
        // turned: slab items that all spanned the same way, and terrace slabs a quarter turn out. So the
        // plane's x is compared with the element's unrotated direction, global X laid onto its plane:
        //  - if they agree and the element has a RotationAngle, the plane has not been turned, and the
        //    rotation is applied here, anticlockwise about the element's normal;
        //  - if the plane's x is already turned by the RotationAngle, it is used as it is, so the rotation
        //    is never applied twice;
        //  - if it is neither, which can happen on a sloped plane whose x is not global X laid onto the
        //    slope, the plane is taken as it is, with a warning.
        // Only a quarter turn has been seen on a live model, which reads the same whichever way the
        // rotation is measured; other angles assume anticlockwise about the normal.
        //
        // Wind walls do not come through here. Checked on a live Tekla Structural Designer 2026 SP1
        // model, every wind wall reports its RotationAngle as not applicable, with no value behind it -
        // including one shown in Tekla Structural Designer at 90 degrees - so a wind wall simply keeps
        // the x axis of its element plane; see ReadLoadingPanels.
        private static BHoMVector SpanDirection(TsdAreaElement element, TsdPlane plane, PanelReadState state)
        {
            BHoMVector x = PlaneX(plane);
            if (x == null)
                return null;

            BHoMVector normal = Normal(plane);
            BHoMVector unrotated = normal == null ? null : GlobalXOnPlane(normal);
            if (unrotated == null || element.RotationAngle == null || !element.RotationAngle.IsApplicable)
                return x;

            double rotationAngle = element.RotationAngle.Value;
            if (Math.Abs(rotationAngle) < SpanAngleTolerance)
                return x;

            double offset = AxisAngle(x, unrotated);
            if (offset < SpanAngleTolerance)
                return x.Rotate(rotationAngle, normal).Normalise();

            double turned = Math.Abs(rotationAngle) % Math.PI;
            turned = Math.Min(turned, Math.PI - turned);
            if (Math.Abs(offset - turned) > SpanAngleTolerance)
                state.Warn("rotated slab item(s) or roof(s) have an element plane that is neither in their unrotated direction nor turned through their rotation angle, so their span direction has been taken from the plane as Tekla Structural Designer reports it");

            return x;
        }

        /***************************************************/

        // The unrotated span direction of a slab item or roof: global X laid onto its plane. Null for a
        // plane square to global X, which has no such direction.
        private static BHoMVector GlobalXOnPlane(BHoMVector normal)
        {
            BHoMVector globalX = new BHoMVector { X = 1 };
            BHoMVector onPlane = globalX - normal * globalX.DotProduct(normal);
            return onPlane.Length() < SpanAngleTolerance ? null : onPlane.Normalise();
        }

        /***************************************************/

        // The x axis of an element plane as Tekla Structural Designer reports it, or null if it reports
        // none.
        private static BHoMVector PlaneX(TsdPlane plane)
        {
            BHoMVector x = plane.LCS.ValueOrDefault().Axes.XAxis.ToBHoMVector();
            return x.Length() > 0 ? x.Normalise() : null;
        }

        /***************************************************/

        // The angle between two directions as axes, ignoring which way along them each points: 0 to a
        // quarter turn.
        private static double AxisAngle(BHoMVector a, BHoMVector b)
        {
            double angle = a.Angle(b);
            return Math.Min(angle, Math.PI - angle);
        }

        /***************************************************/

        private static BHoMVector Normal(TsdPlane plane)
        {
            BHoMVector normal = plane.Normal.ValueOrDefault().ToBHoMVector();
            return normal.Length() > 0 ? normal.Normalise() : null;
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
        /****            Private Nested Types           ****/
        /***************************************************/

        // What one Panel pull shares across slabs, walls and loading panels: the material cache, one
        // surface property object per distinct property, and the reasons anything was skipped or
        // approximated, reported once each with a count rather than once per element.
        private sealed class PanelReadState
        {
            public BarPropertyCache Materials { get; } = new BarPropertyCache();

            // Whether composite and precast slabs get their detailed BHoM types rather than a solid slab
            // of their overall depth; see Convert.ToBHoMSurfaceProperty and the pull configuration.
            public bool Detailed { get; set; }

            public ISurfaceProperty Shared(ISurfaceProperty property)
            {
                if (property == null)
                    return null;

                string key = property.GetType().Name + "|" + ((BH.oM.Base.IBHoMObject)property).Name;
                ISurfaceProperty existing;
                if (m_Properties.TryGetValue(key, out existing))
                    return existing;

                m_Properties[key] = property;
                return property;
            }

            // The one way LoadingPanelProperty every roof and wind wall shares. TwoSides is BHoM's one way
            // load distribution: the load goes to the two sides the span runs between. ReferenceEdge 1 is
            // the Panel's first external edge, which Convert.ToBHoMPanel makes the edge along the span.
            public LoadingPanelProperty LoadingPanelProperty()
            {
                if (m_LoadingProperty == null)
                {
                    m_LoadingProperty = new LoadingPanelProperty
                    {
                        Name = "One way loading panel",
                        LoadApplication = LoadPanelSupportConditions.TwoSides,
                        ReferenceEdge = 1,
                    };
                }

                return m_LoadingProperty;
            }

            public void Skip(string what, string reason)
            {
                Count(m_Skipped, what + " were skipped because " + reason);
            }

            public void Approximate(string reason)
            {
                Count(m_Approximated, reason);
            }

            // Anything else worth a counted warning, given as a sentence that follows its count.
            public void Warn(string message)
            {
                Count(m_Warned, message);
            }

            public void Report()
            {
                foreach (KeyValuePair<string, int> skipped in m_Skipped)
                    Engine.Base.Compute.RecordWarning(skipped.Value + " " + skipped.Key + ".");

                foreach (KeyValuePair<string, int> approximated in m_Approximated)
                    Engine.Base.Compute.RecordWarning(approximated.Value + " slab item(s) have been approximated: " + approximated.Key + ".");

                foreach (KeyValuePair<string, int> warned in m_Warned)
                    Engine.Base.Compute.RecordWarning(warned.Value + " " + warned.Key + ".");

                Materials.Report();
            }

            private readonly Dictionary<string, ISurfaceProperty> m_Properties = new Dictionary<string, ISurfaceProperty>();
            private LoadingPanelProperty m_LoadingProperty;
            private readonly Dictionary<string, int> m_Skipped = new Dictionary<string, int>();
            private readonly Dictionary<string, int> m_Approximated = new Dictionary<string, int>();
            private readonly Dictionary<string, int> m_Warned = new Dictionary<string, int>();
        }

        /***************************************************/
    }
}
