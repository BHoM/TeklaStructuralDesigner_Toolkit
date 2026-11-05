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

using System.Collections.Generic;
using System.Linq;
using BH.Engine.Geometry;
using BH.oM.Geometry;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Helpers
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        // Consecutive vertices closer than this (m) are one vertex.
        private const double VertexTolerance = 1e-6;

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // A closed Polyline through the points without repeated vertices, or null if fewer than three
        // distinct vertices remain.
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

        // BHoM's Line.IsVertical, the same test the Bar orientation uses, so forces and orientation
        // always agree on which members are vertical.
        public static bool IsVertical(this TsdSpanIdentity span)
        {
            if (span?.StartPosition == null || span.EndPosition == null)
                return false;

            return new Line { Start = span.StartPosition, End = span.EndPosition }.IsVertical();
        }

        /***************************************************/
    }
}
