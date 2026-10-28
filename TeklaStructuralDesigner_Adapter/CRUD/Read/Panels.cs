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
        /**** Private Fields                            ****/
        /***************************************************/

        // Angles (rad) and vector lengths below this are zero when deciding a span direction.
        private const double SpanAngleTolerance = 1e-4;

        // Distance (m) within which a wall panel edge is at the base of its wall.
        private const double WallBaseTolerance = 1e-3;

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

        // One Panel per slab item, from its Complete contour (column drops cut out, openings as holes).
        // The slab supplies the type, deck and material; the item may override the depth.
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
                TsdPlane plane = item.ElementPlane.ValueOrDefault();
                if (plane == null)
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
                ISlabItemData itemData = item.SlabItemData.ValueOrDefault();

                double depth = itemData != null && itemData.OverrideSlabDepth.ValueOrDefault(false)
                    ? itemData.Depth.ValueOrDefault(0)
                    : (slabData != null ? slabData.Depth.ValueOrDefault(0) : 0);
                if (depth <= 0 && itemData != null)
                    depth = itemData.Depth.ValueOrDefault(0);

                BHoMPanelType panelType = item.IsColumnDrop.ValueOrDefault(false) ? BHoMPanelType.DropPanel : BHoMPanelType.Slab;
                IMaterialFragment material = state.Materials.Material(slabData != null ? slabData.Material.ValueOrDefault() : null);

                string approximation;
                ISurfaceProperty property = state.Shared(slabData.FromTsdSurfaceProperty(depth, material, panelType, state.Detailed, out approximation));
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
                    Polyline outline = outer != null ? outer.Vertices.ValuesOrEmpty().FromTsdPolyline(plane) : null;
                    List<Polyline> holes = contour.Holes.ValuesOrEmpty().Select(h => h.Vertices.ValuesOrEmpty().FromTsdPolyline(plane)).Where(h => h != null).ToList();

                    Panel panel = Convert.FromTsdPanel(outline, holes, property, localX, normal, item.Name);
                    if (panel == null)
                        continue;

                    result.Add(panel.SetIdentity(item.Name, item.Id, built == 0 ? fragment : fragment.ShallowClone(), built));
                    built++;
                }

                if (built == 0)
                    state.Warnings.Skip("slab item(s)", "their outline did not enclose an area");
                else if (built > 1)
                    state.Approximate("they are cut into separate pieces by openings or column drops, and each piece has been pulled as a Panel of its own sharing the slab item's identifier");
            }

            return result;
        }

        /***************************************************/

        // One Panel per wall panel, between its bottom and top edges, with local x along the wall.
        // Openings wholly inside a wall panel become its Openings; ones straddling two are left out.
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
                IStructuralWallData wallData = wall.StructuralWallData.ValueOrDefault();
                TsdPlane wallPlane = wall.ElementPlane.ValueOrDefault();
                BHoMVector wallNormal = wallPlane != null ? Normal(wallPlane) : null;

                // The wall's base support goes on the bottom edge of every wall panel at the base.
                Constraint6DOF baseSupport = WallBaseSupport(wallData, state);
                double baseZ = wallPanels
                    .Select(p => p.BottomSegment.ValueOrDefault())
                    .Where(s => s != null)
                    .SelectMany(s => new[] { s.GetPoint(Location.Start).FromTsdPoint().Z, s.GetPoint(Location.End).FromTsdPoint().Z })
                    .DefaultIfEmpty(double.NaN)
                    .Min();
                bool baseSupported = false;

                foreach (IStructuralWallPanel wallPanel in wallPanels)
                {
                    ISegment3D bottom = wallPanel.BottomSegment.ValueOrDefault();
                    ISegment3D top = wallPanel.TopSegment.ValueOrDefault();
                    if (bottom == null || top == null)
                    {
                        state.Warnings.Skip("wall panel(s)", "Tekla Structural Designer reported no top or bottom edge for them");
                        continue;
                    }

                    BHoMPoint b0 = bottom.GetPoint(Location.Start).FromTsdPoint();
                    BHoMPoint b1 = bottom.GetPoint(Location.End).FromTsdPoint();
                    BHoMPoint t0 = top.GetPoint(Location.Start).FromTsdPoint();
                    BHoMPoint t1 = top.GetPoint(Location.End).FromTsdPoint();

                    // A top edge running the other way would cross the outline over itself.
                    if (b1.Distance(t1) > b1.Distance(t0))
                    {
                        BHoMPoint swap = t0;
                        t0 = t1;
                        t1 = swap;
                    }

                    Polyline outline = Helpers.ClosedPolyline(new[] { b0, b1, t1, t0 });

                    IStructuralWallPanelData panelData = wallPanel.WallPanelData.ValueOrDefault();
                    IMaterialFragment material = state.Materials.Material(panelData != null ? panelData.Material.ValueOrDefault() : null);
                    ISurfaceProperty property = state.Shared(panelData.FromTsdSurfaceProperty(material));

                    List<Polyline> openings = WallOpenings(wallPanel, outline, timeout, state);

                    string id = wall.Name + ":" + wallPanel.Index;
                    Panel panel = Convert.FromTsdPanel(outline, openings, property, b1 - b0, wallNormal, id);
                    if (panel == null)
                    {
                        state.Warnings.Skip("wall panel(s)", "their outline did not enclose an area");
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
                    state.Warnings.Add("wall(s) generate a support along their base, but no wall panel edge was found there to carry it, so they have been pulled unsupported");
            }

            return result;
        }

        /***************************************************/

        // The support a wall generates along its base (GenerateSupport and SupportData on the wall, not a
        // point support), or null. Springs and partial fixity are reported rather than converted.
        private static Constraint6DOF WallBaseSupport(IStructuralWallData wallData, PanelReadState state)
        {
            if (wallData == null)
                return null;

            if (!wallData.GenerateSupport.ValueOrDefault(false))
            {
                if (wallData.AutomaticGenerateSupport.ValueOrDefault(false))
                    state.Warnings.Add("wall(s) leave Tekla Structural Designer to decide whether they generate a base support, and it reported none, so they have been pulled unsupported at the base. Check this against the model");

                return null;
            }

            bool isPartial;
            Constraint6DOF support = wallData.SupportData.ValueOrDefault().FromTsdConstraint(out isPartial);
            if (support == null)
            {
                state.Warnings.Add("wall(s) generate a base support but Tekla Structural Designer reported no fixity for it, so they have been pulled unsupported at the base");
                return null;
            }

            if (isPartial)
                state.Warnings.Add("wall base support(s) have spring or partial fixity in at least one direction. Their fixed and free directions have been pulled, but the springs have not");

            return support;
        }

        /***************************************************/

        // Puts the support on the external edge between start and end, in either direction. False if
        // there is none.
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

        // A wall opening is a Width by Height rectangle in its reference plane, with its corner at
        // Offset from its reference point (not the plane origin). Verified against the solver mesh.
        private static List<Polyline> WallOpenings(IStructuralWallPanel wallPanel, Polyline outline, int timeout, PanelReadState state)
        {
            List<Polyline> result = new List<Polyline>();
            if (outline == null)
                return result;

            IEnumerable<IWallOpening> openings;
            string error;
            if (!Async.TryRunSync(ct => wallPanel.GetOpeningsAsync(false, ct), timeout, "reading wall openings", out openings, out error))
            {
                state.Warnings.Skip("wall opening(s)", "they could not be read (" + error + ")");
                return result;
            }

            foreach (IWallOpening opening in openings)
            {
                TsdPlane plane = opening.ReferencePlane.ValueOrDefault();
                double width = opening.Width.ValueOrDefault(0);
                double height = opening.Height.ValueOrDefault(0);
                if (plane == null || width <= 0 || height <= 0)
                {
                    state.Warnings.Skip("wall opening(s)", "they have no reference plane or no size");
                    continue;
                }

                if (opening.ReferenceCoordinates == null || !opening.ReferenceCoordinates.IsApplicable)
                {
                    state.Warnings.Skip("wall opening(s)", "they have no reference point to be placed from");
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

                Polyline rectangle = corners.FromTsdPolyline(plane);
                if (rectangle == null)
                    continue;

                if (!outline.IsContaining(rectangle.ControlPoints, true, 1e-3))
                {
                    state.Warnings.Skip("wall opening(s)", "they straddle more than one wall panel, or fall outside the panel they were reported against");
                    continue;
                }

                result.Add(rectangle);
            }

            return result;
        }

        /***************************************************/

        // Roofs and wind walls, outlined by their construction points and spanning one way.
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
                    state.Warnings.Skip(kind, "one or more of the construction points they are drawn between could not be read");
                    continue;
                }

                TsdPlane plane = element.ElementPlane.ValueOrDefault();
                BHoMVector normal = plane != null ? Normal(plane) : null;
                BHoMVector span = null;
                if (plane != null)
                    span = roof != null ? SpanDirection(element, plane, state) : PlaneX(plane);

                // The API reports no rotation angle for wind walls, so they keep their plane's x axis.
                if (roof == null)
                    state.Warnings.Add("wind wall(s) have been pulled spanning along the x axis of their element plane, which is horizontal along the wall. Wind wall span direction is not supported at the moment: Tekla Structural Designer's API does not report a wind wall's rotation angle, so a wind wall rotated in Tekla Structural Designer - 90 degrees to span vertically between floors - will span the wrong way. Check the cladding direction of every wind wall in the receiving model");

                Panel panel = Convert.FromTsdPanel(Helpers.ClosedPolyline(points), null, null, span, normal, element.Name);
                if (panel == null)
                {
                    state.Warnings.Skip(kind, "their outline did not enclose an area");
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

        // The element plane's x axis, turned through the element's RotationAngle unless the plane is
        // already turned. Project models showed rotated slab items whose plane had not been, so the
        // plane's x is compared with global X laid onto the plane:
        //  - in line with it: the rotation is applied here, anticlockwise about the normal;
        //  - already turned by the rotation: used as it is;
        //  - neither (possible on a sloped plane): used as it is, with a warning.
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
                state.Warnings.Add("rotated slab item(s) or roof(s) have an element plane that is neither in their unrotated direction nor turned through their rotation angle, so their span direction has been taken from the plane as Tekla Structural Designer reports it");

            return x;
        }

        /***************************************************/

        // Global X projected onto the plane, or null for a plane square to global X.
        private static BHoMVector GlobalXOnPlane(BHoMVector normal)
        {
            BHoMVector globalX = new BHoMVector { X = 1 };
            BHoMVector onPlane = globalX - normal * globalX.DotProduct(normal);
            return onPlane.Length() < SpanAngleTolerance ? null : onPlane.Normalise();
        }

        /***************************************************/

        private static BHoMVector PlaneX(TsdPlane plane)
        {
            BHoMVector x = plane.LCS.ValueOrDefault().Axes.XAxis.FromTsdVector();
            return x.Length() > 0 ? x.Normalise() : null;
        }

        /***************************************************/

        // The angle between two axes regardless of sense: 0 to a quarter turn.
        private static double AxisAngle(BHoMVector a, BHoMVector b)
        {
            double angle = a.Angle(b);
            return Math.Min(angle, Math.PI - angle);
        }

        /***************************************************/

        private static BHoMVector Normal(TsdPlane plane)
        {
            BHoMVector normal = plane.Normal.ValueOrDefault().FromTsdVector();
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
    }
}
