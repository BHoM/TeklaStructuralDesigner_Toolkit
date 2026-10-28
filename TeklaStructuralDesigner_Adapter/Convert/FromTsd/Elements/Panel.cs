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
using BH.oM.Geometry;
using BH.oM.Structure.Elements;
using BH.oM.Structure.SurfaceProperties;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // A Panel from an outline and holes already converted to metres.
        //  - The outline is flipped where needed so the Panel's normal matches the element's (this sets
        //    the wind direction on wind walls).
        //  - localX is the span direction. The outline is started on the edge most nearly along it, as
        //    Robot reads a one way LoadingPanelProperty's span from whether its edge number is odd or even.
        //  - Create.Panel is called without localX because its normal lookup throws outside a BHoM UI
        //    host; the orientation angle is set here instead.
        public static Panel FromTsdPanel(Polyline outline, IEnumerable<Polyline> holes, ISurfaceProperty property, Vector localX, Vector normal, string name)
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

            if (localX != null && localX.Length() > 0)
                outline = outline.StartAlong(localX);

            List<ICurve> openings = (holes ?? Enumerable.Empty<Polyline>())
                .Where(h => h != null && h.ControlPoints.Count >= 4)
                .Cast<ICurve>()
                .ToList();

            Panel panel = Engine.Structure.Create.Panel(outline, openings, property, null, name);
            if (panel == null)
                return null;

            // A local x along the normal gives no in-plane direction, so BHoM's default is kept.
            if (localX != null && localX.Length() > 0 && Math.Abs(localX.Normalise().DotProduct(outlineNormal.Normalise())) < 1 - 1e-6)
                panel.OrientationAngle = outlineNormal.OrientationAngleAreaElement(localX);

            return panel;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // The same closed outline, started so its first edge is the one most nearly along direction.
        // Ties keep the earlier edge, so an outline already starting along it is returned unchanged.
        private static Polyline StartAlong(this Polyline outline, Vector direction)
        {
            List<Point> vertices = outline.ControlPoints.Take(outline.ControlPoints.Count - 1).ToList();
            Vector along = direction.Normalise();
            int best = 0;
            double bestAlignment = -1;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector edge = vertices[(i + 1) % vertices.Count] - vertices[i];
                if (edge.Length() == 0)
                    continue;

                double alignment = Math.Abs(edge.Normalise().DotProduct(along));
                if (alignment > bestAlignment + 1e-9)
                {
                    bestAlignment = alignment;
                    best = i;
                }
            }

            if (best == 0)
                return outline;

            List<Point> reordered = vertices.Skip(best).Concat(vertices.Take(best)).ToList();
            reordered.Add(reordered[0]);
            return new Polyline { ControlPoints = reordered };
        }

        /***************************************************/
    }
}
