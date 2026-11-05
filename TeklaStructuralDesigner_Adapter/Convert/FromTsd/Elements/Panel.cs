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
        // warnings are complete messages for the caller to count.
        public static List<Panel> FromTsdSlabItem(this ISlabItem item, ISlab slab, IEnumerable<IPolygonWithHoles2D> contours, ISurfaceProperty property, out List<string> warnings, out string skipReason, out string approximation)
        {
            warnings = new List<string>();
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
            SlabType slabType = slabData != null ? slabData.SlabType.ValueOrDefault(SlabType.Unknown) : SlabType.Unknown;
            DecompositionType decomposition = slabData != null ? slabData.DecompositionType.ValueOrDefault(DecompositionType.Unknown) : DecompositionType.Unknown;

            bool slabTypeRecognised, spanTypeRecognised;
            TeklaStructuralDesignerPanelProperties fragment = new TeklaStructuralDesignerPanelProperties
            {
                ElementType = TeklaStructuralDesignerPanelElementType.SlabItem,
                ElementName = item.Name,
                ElementIndex = item.Index,
                ElementId = item.Id,
                SlabName = slab != null ? slab.Name : "",
                SlabIndex = slab != null ? slab.Index : -1,
                SlabType = slabType.FromTsd(out slabTypeRecognised),
                SpanType = decomposition.FromTsd(out spanTypeRecognised),
                ElementGroupName = item.ElementGroupName ?? "",
            };

            if (!slabTypeRecognised)
                warnings.Add(WarningTally.UnrecognisedMessage("slab item(s)", "slab type", slabType));
            if (!spanTypeRecognised)
                warnings.Add(WarningTally.UnrecognisedMessage("slab item(s)", "span type", decomposition));

            string spanWarning;
            BHoMVector localX = SpanDirection(item, plane, out spanWarning);
            if (spanWarning != null)
                warnings.Add(spanWarning);

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

        // A roof or wind wall, outlined by its construction points and spanning one way: a roof along
        // SpanDirection, a wind wall along its plane's x axis, with a warning saying why. Null, with
        // skipReason, if a construction point is missing or the outline encloses no area. warnings
        // are complete messages for the caller to count.
        public static Panel FromTsdLoadingPanel(this TsdAreaElement element, Dictionary<int, ConstructionPointInfo> pointByIndex, ISurfaceProperty property, out List<string> warnings, out string skipReason)
        {
            warnings = new List<string>();
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
            string spanWarning = null;
            if (plane != null)
                span = roof != null ? SpanDirection(element, plane, out spanWarning) : PlaneX(plane);

            if (spanWarning != null)
                warnings.Add(spanWarning);

            // The API reports no rotation angle for wind walls, so they keep their plane's x axis.
            if (roof == null)
                warnings.Add("wind wall(s) have been pulled spanning along the x axis of their element plane, which is horizontal along the wall. Wind wall span direction is not supported at the moment: Tekla Structural Designer's API does not report a wind wall's rotation angle, so a wind wall rotated in Tekla Structural Designer - 90 degrees to span vertically between floors - will span the wrong way. Check the cladding direction of every wind wall in the receiving model");

            Panel panel = Helpers.Panel(Helpers.ClosedPolyline(points), null, null, span, normal, element.Name);
            if (panel == null)
            {
                skipReason = "their outline did not enclose an area";
                return null;
            }

            panel.Property = property;

            IRoofData roofData = roof != null ? roof.RoofData.ValueOrDefault() : null;
            RoofType roofType = roofData != null ? roofData.RoofType.ValueOrDefault(RoofType.Unknown) : RoofType.Unknown;

            bool roofTypeRecognised;
            TeklaStructuralDesignerRoofType convertedRoofType = roofType.FromTsd(out roofTypeRecognised);
            if (!roofTypeRecognised)
                warnings.Add(WarningTally.UnrecognisedMessage("roof(s)", "roof type", roofType));

            return panel.SetIdentity(element.Name, element.Id, new TeklaStructuralDesignerPanelProperties
            {
                ElementType = roof != null ? TeklaStructuralDesignerPanelElementType.Roof : TeklaStructuralDesignerPanelElementType.WindWall,
                ElementName = element.Name,
                ElementIndex = element.Index,
                ElementId = element.Id,
                RoofType = convertedRoofType,
                SpanType = TeklaStructuralDesignerSpanType.OneWay,
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
    }
}
