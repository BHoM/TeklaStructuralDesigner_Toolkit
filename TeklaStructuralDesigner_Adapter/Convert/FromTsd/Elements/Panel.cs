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
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Geometry;
using BH.oM.Structure.Constraints;
using BH.oM.Structure.Elements;
using BH.oM.Structure.SurfaceProperties;
using TSD.API.Remoting.Geometry;
using TSD.API.Remoting.Structure;
using BHoMPoint = BH.oM.Geometry.Point;
using BHoMVector = BH.oM.Geometry.Vector;
using TsdAreaElement = TSD.API.Remoting.Structure.IAreaElement;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        // Angles (rad) and vector lengths below this are zero when deciding a span direction.
        private const double SpanAngleTolerance = 1e-4;

        // Distance (m) within which a wall panel edge is at the base of its wall.
        private const double WallBaseTolerance = 1e-3;

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // One Panel per piece of a slab item's Complete contour (column drops cut out, openings as
        // holes), spanning along SpanDirection. Every piece shares the item's identifier, and
        // approximation says so when there is more than one. Empty, with skipReason, if none.
        public static List<Panel> FromTsdSlabItem(this ISlabItem item, ISlab slab, IEnumerable<IPolygonWithHoles2D> contours, ISurfaceProperty property, out string spanWarning, out string skipReason, out string approximation)
        {
            spanWarning = null;
            skipReason = null;
            approximation = null;

            List<Panel> result = new List<Panel>();

            TsdPlane plane = item.ElementPlane.ValueOrDefault();
            if (plane == null)
            {
                skipReason = "Tekla Structural Designer reported no plane for them";
                return result;
            }

            ISlabData slabData = slab != null ? slab.SlabData.ValueOrDefault() : null;
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

            BHoMVector localX = SpanDirection(item, plane, out spanWarning);
            BHoMVector normal = Normal(plane);

            int built = 0;
            foreach (IPolygonWithHoles2D contour in contours ?? Enumerable.Empty<IPolygonWithHoles2D>())
            {
                IPolygon2D outer = contour.Contour.ValueOrDefault();
                Polyline outline = outer != null ? outer.Vertices.ValuesOrEmpty().FromTsdPolyline(plane) : null;
                List<Polyline> holes = contour.Holes.ValuesOrEmpty().Select(h => h.Vertices.ValuesOrEmpty().FromTsdPolyline(plane)).Where(h => h != null).ToList();

                Panel panel = Helpers.Panel(outline, holes, property, localX, normal, item.Name);
                if (panel == null)
                    continue;

                result.Add(panel.SetIdentity(item.Name, item.Id, built == 0 ? fragment : fragment.ShallowClone(), built));
                built++;
            }

            if (built == 0)
                skipReason = "their outline did not enclose an area";
            else if (built > 1)
                approximation = "they are cut into separate pieces by openings or column drops, and each piece has been pulled as a Panel of its own sharing the slab item's identifier";

            return result;
        }

        /***************************************************/

        // A wall panel's outline: its bottom edge, then its top edge back the other way. Null, with
        // skipReason, if Tekla Structural Designer reported no top or bottom edge; null without one
        // if the edges enclose no area, which FromTsdWallPanel then reports.
        public static Polyline FromTsdWallOutline(this IStructuralWallPanel wallPanel, out string skipReason)
        {
            skipReason = null;

            ISegment3D bottom = wallPanel.BottomSegment.ValueOrDefault();
            ISegment3D top = wallPanel.TopSegment.ValueOrDefault();
            if (bottom == null || top == null)
            {
                skipReason = "Tekla Structural Designer reported no top or bottom edge for them";
                return null;
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

            return Helpers.ClosedPolyline(new[] { b0, b1, t1, t0 });
        }

        /***************************************************/

        // One wall panel, with local x along the wall and its openings as Openings. Where its bottom
        // edge is at the base of the wall (baseZ), the wall's base support goes on that edge and
        // onBase is true. openingSkipReasons holds one reason per opening left out.
        public static Panel FromTsdWallPanel(this IStructuralWallPanel wallPanel, IStructuralWall wall, Polyline outline, IEnumerable<IWallOpening> openings, ISurfaceProperty property, Constraint6DOF baseSupport, double baseZ, out bool onBase, out string skipReason, out List<string> openingSkipReasons)
        {
            onBase = false;
            skipReason = null;
            openingSkipReasons = new List<string>();

            ISegment3D bottom = wallPanel.BottomSegment.ValueOrDefault();
            if (bottom == null)
            {
                skipReason = "Tekla Structural Designer reported no top or bottom edge for them";
                return null;
            }

            List<Polyline> holes = new List<Polyline>();
            if (outline != null)
            {
                foreach (IWallOpening opening in openings ?? Enumerable.Empty<IWallOpening>())
                {
                    string openingSkipReason;
                    Polyline rectangle = opening.FromTsdWallOpening(outline, out openingSkipReason);
                    if (rectangle != null)
                        holes.Add(rectangle);
                    else if (openingSkipReason != null)
                        openingSkipReasons.Add(openingSkipReason);
                }
            }

            BHoMPoint b0 = bottom.GetPoint(Location.Start).FromTsdPoint();
            BHoMPoint b1 = bottom.GetPoint(Location.End).FromTsdPoint();

            TsdPlane wallPlane = wall.ElementPlane.ValueOrDefault();
            BHoMVector wallNormal = wallPlane != null ? Normal(wallPlane) : null;

            string id = wall.Name + ":" + wallPanel.Index;
            Panel panel = Helpers.Panel(outline, holes, property, b1 - b0, wallNormal, id);
            if (panel == null)
            {
                skipReason = "their outline did not enclose an area";
                return null;
            }

            if (baseSupport != null && Math.Max(b0.Z, b1.Z) - baseZ < WallBaseTolerance && SupportEdge(panel, b0, b1, baseSupport))
                onBase = true;

            IStructuralWallData wallData = wall.StructuralWallData.ValueOrDefault();

            return panel.SetIdentity(id, wallPanel.Id, new TeklaStructuralDesignerPanelProperties
            {
                ElementType = "StructuralWall",
                ElementName = wall.Name,
                ElementIndex = wall.Index,
                ElementId = wall.Id,
                PanelIndex = wallPanel.Index,
                ElementSubType = wallData != null ? wallData.StructuralWallType.ValueOrDefault(StructuralWallType.Unknown).ToString() : "",
                ElementGroupName = wall.ElementGroupName ?? "",
            });
        }

        /***************************************************/

        // A wall opening is a Width by Height rectangle in its reference plane, with its corner at
        // Offset from its reference point (not the plane origin). Verified against the solver mesh.
        // Null, with skipReason, if it has no size or position, or is not wholly inside panelOutline.
        public static Polyline FromTsdWallOpening(this IWallOpening opening, Polyline panelOutline, out string skipReason)
        {
            skipReason = null;

            TsdPlane plane = opening.ReferencePlane.ValueOrDefault();
            double width = opening.Width.ValueOrDefault(0);
            double height = opening.Height.ValueOrDefault(0);
            if (plane == null || width <= 0 || height <= 0)
            {
                skipReason = "they have no reference plane or no size";
                return null;
            }

            if (opening.ReferenceCoordinates == null || !opening.ReferenceCoordinates.IsApplicable)
            {
                skipReason = "they have no reference point to be placed from";
                return null;
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
                return null;

            if (!panelOutline.IsContaining(rectangle.ControlPoints, true, 1e-3))
            {
                skipReason = "they straddle more than one wall panel, or fall outside the panel they were reported against";
                return null;
            }

            return rectangle;
        }

        /***************************************************/

        // The lowest point of the bottom edges of a wall's panels: the base of the wall. NaN if no
        // panel reports a bottom edge.
        public static double FromTsdBaseLevel(this IEnumerable<IStructuralWallPanel> wallPanels)
        {
            return wallPanels
                .Select(p => p.BottomSegment.ValueOrDefault())
                .Where(s => s != null)
                .SelectMany(s => new[] { s.GetPoint(Location.Start).FromTsdPoint().Z, s.GetPoint(Location.End).FromTsdPoint().Z })
                .DefaultIfEmpty(double.NaN)
                .Min();
        }

        /***************************************************/

        // A roof or wind wall, outlined by its construction points and spanning one way: a roof along
        // SpanDirection, a wind wall along its plane's x axis, with spanWarning saying why. Null, with
        // skipReason, if a construction point is missing or the outline encloses no area.
        public static Panel FromTsdLoadingPanel(this TsdAreaElement element, Dictionary<int, ConstructionPointInfo> pointByIndex, ISurfaceProperty property, out string spanWarning, out string skipReason)
        {
            spanWarning = null;
            skipReason = null;

            IRoof roof = element as IRoof;

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
                skipReason = "one or more of the construction points they are drawn between could not be read";
                return null;
            }

            TsdPlane plane = element.ElementPlane.ValueOrDefault();
            BHoMVector normal = plane != null ? Normal(plane) : null;
            BHoMVector span = null;
            if (plane != null)
                span = roof != null ? SpanDirection(element, plane, out spanWarning) : PlaneX(plane);

            // The API reports no rotation angle for wind walls, so they keep their plane's x axis.
            if (roof == null)
                spanWarning = "wind wall(s) have been pulled spanning along the x axis of their element plane, which is horizontal along the wall. Wind wall span direction is not supported at the moment: Tekla Structural Designer's API does not report a wind wall's rotation angle, so a wind wall rotated in Tekla Structural Designer - 90 degrees to span vertically between floors - will span the wrong way. Check the cladding direction of every wind wall in the receiving model";

            Panel panel = Helpers.Panel(Helpers.ClosedPolyline(points), null, null, span, normal, element.Name);
            if (panel == null)
            {
                skipReason = "their outline did not enclose an area";
                return null;
            }

            panel.Property = property;

            IRoofData roofData = roof != null ? roof.RoofData.ValueOrDefault() : null;
            return panel.SetIdentity(element.Name, element.Id, new TeklaStructuralDesignerPanelProperties
            {
                ElementType = roof != null ? "Roof" : "WindWall",
                ElementName = element.Name,
                ElementIndex = element.Index,
                ElementId = element.Id,
                ElementSubType = roofData != null ? roofData.RoofType.ValueOrDefault(RoofType.Unknown).ToString() : "",
                SpanType = "OneWay",
                ElementGroupName = element.ElementGroupName ?? "",
            });
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // The element plane's x axis, turned through the element's RotationAngle unless the plane is
        // already turned. Project models showed rotated slab items whose plane had not been, so the
        // plane's x is compared with global X laid onto the plane:
        //  - in line with it: the rotation is applied here, anticlockwise about the normal;
        //  - already turned by the rotation: used as it is;
        //  - neither (possible on a sloped plane): used as it is, with warning set.
        private static BHoMVector SpanDirection(TsdAreaElement element, TsdPlane plane, out string warning)
        {
            warning = null;

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
                warning = "rotated slab item(s) or roof(s) have an element plane that is neither in their unrotated direction nor turned through their rotation angle, so their span direction has been taken from the plane as Tekla Structural Designer reports it";

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
    }
}
