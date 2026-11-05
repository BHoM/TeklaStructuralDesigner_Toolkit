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
using BH.oM.Base;
using BH.oM.Geometry;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;
using TSD.API.Remoting.Loading;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // A uniform load over the whole of one or more Panels.
        public static AreaUniformlyDistributedLoad FromTsdAreaLoad(this IPlanarLoad load, double pressure, List<Panel> panels, Loadcase loadcase, out string skipReason)
        {
            Vector direction;
            if (!PlanarDirection(load, out direction, out skipReason))
                return null;

            return new AreaUniformlyDistributedLoad
            {
                Loadcase = loadcase,
                Objects = new BHoMGroup<IAreaElement> { Elements = panels.Cast<IAreaElement>().ToList() },
                Axis = LoadAxis.Global,
                Projected = IsProjected(load),
                Pressure = direction * (pressure * PressureScale),
            };
        }

        /***************************************************/

        // A patch load, with its outline placed in 3D through the plane it was drawn on.
        public static ContourLoad FromTsdContourLoad(this IPolygonalLoad load, double pressure, TsdPlane plane, Loadcase loadcase, out string skipReason)
        {
            Vector direction;
            if (!PlanarDirection(load, out direction, out skipReason))
                return null;

            Polyline contour = load.Shape != null ? load.Shape.Vertices.ValuesOrEmpty().FromTsdPolyline(plane) : null;
            if (contour == null)
            {
                skipReason = "their outline did not enclose an area";
                return null;
            }

            return new ContourLoad
            {
                Loadcase = loadcase,
                Contour = contour,
                Axis = LoadAxis.Global,
                Projected = IsProjected(load),
                Force = direction * (pressure * PressureScale),
            };
        }

        /***************************************************/

        // A uniform line load, placed in 3D through the plane it was drawn on.
        public static GeometricalLineLoad FromTsdLineLoad(this IUniformLineLoad load, TsdPlane plane, Loadcase loadcase, out string skipReason)
        {
            Vector direction;
            if (!PlanarDirection(load, out direction, out skipReason))
                return null;

            Vector force = direction * (load.Load * ForcePerLengthScale);

            return new GeometricalLineLoad
            {
                Loadcase = loadcase,
                Location = new Line { Start = plane.Local2Global(load.StartCoordinate).FromTsdPoint(), End = plane.Local2Global(load.EndCoordinate).FromTsdPoint() },
                Axis = LoadAxis.Global,
                Projected = IsProjected(load),
                ForceA = force,
                ForceB = force,
            };
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Same sign convention as member loads. Only global directions are accepted, as local ones on
        // a load drawn on a plane have not been verified.
        private static bool PlanarDirection(IPlanarLoad load, out Vector direction, out string skipReason)
        {
            skipReason = null;

            LoadAxis axis;
            if (!TryDirection(load.Direction, out direction, out axis) || axis != LoadAxis.Global)
            {
                skipReason = "their direction is '" + load.Direction + "', and only global directions are converted for area loads";
                direction = null;
                return false;
            }

            return true;
        }

        /***************************************************/

        private static bool IsProjected(IPlanarLoad load)
        {
            return load.Measuring == DistanceMeasuring.InProjection;
        }

        /***************************************************/
    }
}
