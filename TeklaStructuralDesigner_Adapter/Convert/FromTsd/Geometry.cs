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
using BH.oM.Geometry;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;
using TsdPoint2D = TSD.API.Remoting.Geometry.Point2D;
using TsdPoint3D = TSD.API.Remoting.Geometry.Point3D;
using TsdVector3D = TSD.API.Remoting.Geometry.Vector3D;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        public static Point FromTsdPoint(this TsdPoint3D point)
        {
            return new Point { X = point.X * LengthScale, Y = point.Y * LengthScale, Z = point.Z * LengthScale };
        }

        /***************************************************/

        // Directions are unitless, so unscaled.
        public static Vector FromTsdVector(this TsdVector3D vector)
        {
            return new Vector { X = vector.X, Y = vector.Y, Z = vector.Z };
        }

        /***************************************************/

        // A polygon in a plane's local coordinates as a closed global Polyline. Local2Global is evaluated
        // locally by the API, so this makes no call to Tekla Structural Designer.
        public static Polyline FromTsdPolyline(this IEnumerable<TsdPoint2D> vertices, TsdPlane plane)
        {
            if (vertices == null || plane == null)
                return null;

            return Helpers.ClosedPolyline(vertices.Select(v => plane.Local2Global(v).FromTsdPoint()));
        }

        /***************************************************/
    }
}
