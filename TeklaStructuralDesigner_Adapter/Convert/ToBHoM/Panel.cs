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
using BH.Engine.Geometry;
using BH.Engine.Structure;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Geometry;
using BH.oM.Structure.Elements;
using BH.oM.Structure.SurfaceProperties;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;
using TsdPoint2D = TSD.API.Remoting.Geometry.Point2D;
using TsdPoint3D = TSD.API.Remoting.Geometry.Point3D;
using TsdVector3D = TSD.API.Remoting.Geometry.Vector3D;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        // Consecutive outline vertices closer than this (in metres) are one vertex. Tekla Structural
        // Designer contours can repeat a vertex where two edges of a slab item meet at a construction
        // point, and a zero length edge would become a zero length BHoM Edge.
        private const double VertexTolerance = 1e-6;

        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // One BHoM Panel from an outline and its holes, in metres.
        //
        // Span direction is carried by the Panel's local x: localX is the direction the element spans in
        // Tekla Structural Designer, turned into the OrientationAngle by BHoM's own
        // OrientationAngleAreaElement. Every directional BHoM surface property this adapter creates runs
        // along local x, so a one way slab, deck or loading panel spans along local x too.
        //
        // The outline is reversed where needed so that the Panel's normal - which BHoM derives from the
        // order of its edges - points the same way as the element's in Tekla Structural Designer. That
        // matters most for wind walls, whose normal is the direction the wind pressure acts in.
        //
        // The Panel is built by Structure.Create.Panel without a localX: given one, Create.Panel finds
        // the Panel's normal through BHoM's run time extension method lookup, which throws when that
        // lookup cannot see Structure_Engine - as happened when the adapter was driven from outside a
        // BHoM UI host. The orientation is set here instead, from the outline's own normal.
        public static Panel ToBHoMPanel(Polyline outline, IEnumerable<Polyline> holes, ISurfaceProperty property, Vector localX, Vector normal, string name)
        {
            if (outline == null || outline.ControlPoints.Count < 4)
                return null;

            Vector outlineNormal = outline.Normal();
            if (outlineNormal == null || outlineNormal.Length() == 0)
                return null;

            if (normal != null && normal.Length() > 0 && outlineNormal.DotProduct(normal) < 0)
            {
                outline = outline.Flip();
                outlineNormal = outlineNormal.Reverse();
            }

            List<ICurve> openings = (holes ?? Enumerable.Empty<Polyline>())
                .Where(h => h != null && h.ControlPoints.Count >= 4)
                .Cast<ICurve>()
                .ToList();

            Panel panel = Engine.Structure.Create.Panel(outline, openings, property, null, name);
            if (panel == null)
                return null;

            // A local x along the normal has no in-plane direction to give; the Panel keeps BHoM's default.
            if (localX != null && localX.Length() > 0 && Math.Abs(localX.Normalise().DotProduct(outlineNormal.Normalise())) < 1 - 1e-6)
                panel.OrientationAngle = outlineNormal.OrientationAngleAreaElement(localX);

            return panel;
        }

        /***************************************************/

        // A pulled Panel's identity (see SetIdentity) and the fragment saying which Tekla Structural
        // Designer element it came from. id is the readable identifier the Panel is requested by,
        // persistentId the Guid of the element (or, for a wall, the wall panel), and piece tells apart
        // the Panels one slab item is cut into by openings or column drops.
        public static Panel SetIdentity(this Panel panel, string id, Guid persistentId, TeklaStructuralDesignerPanelProperties properties, int piece = 0)
        {
            panel.SetIdentity(id, persistentId, piece);
            panel.Fragments.Add(properties);
            return panel;
        }

        /***************************************************/

        public static Point ToBHoMPoint(this TsdPoint3D point)
        {
            return new Point { X = point.X * LengthScale, Y = point.Y * LengthScale, Z = point.Z * LengthScale };
        }

        /***************************************************/

        // Directions are unitless, so unscaled.
        public static Vector ToBHoMVector(this TsdVector3D vector)
        {
            return new Vector { X = vector.X, Y = vector.Y, Z = vector.Z };
        }

        /***************************************************/

        // A 2D polygon in the local coordinates of a Tekla Structural Designer plane, as a closed 3D
        // Polyline in global coordinates. IPlane.Local2Global is evaluated locally by the API, not a
        // call to Tekla Structural Designer, so this costs nothing per vertex.
        public static Polyline ToBHoMPolyline(this IEnumerable<TsdPoint2D> vertices, TsdPlane plane)
        {
            if (vertices == null || plane == null)
                return null;

            return ClosedPolyline(vertices.Select(v => plane.Local2Global(v).ToBHoMPoint()));
        }

        /***************************************************/

        // A closed Polyline through the points, with repeated consecutive vertices removed. Null if
        // fewer than three distinct vertices remain - no area to make a Panel from.
        public static Polyline ClosedPolyline(IEnumerable<Point> points)
        {
            List<Point> vertices = (points ?? Enumerable.Empty<Point>()).Where(p => p != null).ToList();
            if (vertices.Count < 3)
                return null;

            Polyline polyline = Engine.Geometry.Create.Polyline(vertices)
                .Close(VertexTolerance)
                .RemoveShortSegments(VertexTolerance, VertexTolerance);

            return polyline.ControlPoints.Count < 4 ? null : polyline;
        }

        /***************************************************/

        // Index of the external edge running most nearly along the span. A LoadingPanelProperty spanning
        // one way takes its direction from one of the Panel's edges rather than from its local axes, so
        // the edge has to be found on the Panel as built - after any reversal of its outline.
        public static int SpanReferenceEdge(this Panel panel, Vector spanDirection)
        {
            if (panel == null || panel.ExternalEdges == null || spanDirection == null || spanDirection.Length() == 0)
                return 0;

            Vector span = spanDirection.Normalise();
            int best = 0;
            double bestAlignment = -1;

            for (int i = 0; i < panel.ExternalEdges.Count; i++)
            {
                ICurve curve = panel.ExternalEdges[i].Curve;
                if (curve == null)
                    continue;

                Vector along = curve.IEndPoint() - curve.IStartPoint();
                if (along.Length() == 0)
                    continue;

                double alignment = Math.Abs(along.Normalise().DotProduct(span));
                if (alignment > bestAlignment)
                {
                    bestAlignment = alignment;
                    best = i;
                }
            }

            return best;
        }

        /***************************************************/
    }
}
