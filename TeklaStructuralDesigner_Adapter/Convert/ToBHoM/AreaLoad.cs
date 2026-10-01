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
        /****            Public Methods                 ****/
        /***************************************************/

        // Area loads come in N/mm² and line loads on a plane in N/mm - the same N and mm premise as
        // the rest of Load.cs, whose unit table holds PressureScale and ForcePerLengthScale.

        // A uniform load over the whole of one or more Panels - a load Tekla Structural Designer applies
        // to a slab item, a whole slab, a wall, a roof or a wind wall.
        public static AreaUniformlyDistributedLoad ToBHoM(this IPlanarLoad load, double pressure, List<Panel> panels, Loadcase loadcase, out string skipReason)
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

        // A uniform load over part of a plane - a rectangular or polygonal patch load. Its outline is
        // given in the coordinates of the construction plane it was drawn on, so it is placed in 3D
        // through that plane. BHoM's ContourLoad is the load over a closed region independent of the
        // elements under it, which is exactly what a patch load is.
        public static ContourLoad ToBHoMContour(this IPolygonalLoad load, double pressure, TsdPlane plane, Loadcase loadcase, out string skipReason)
        {
            Vector direction;
            if (!PlanarDirection(load, out direction, out skipReason))
                return null;

            Polyline contour = load.Shape != null ? load.Shape.Vertices.ValuesOrEmpty().ToBHoMPolyline(plane) : null;
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

        // A uniform line load drawn on a plane, placed in 3D through that plane.
        public static GeometricalLineLoad ToBHoM(this IUniformLineLoad load, TsdPlane plane, Loadcase loadcase, out string skipReason)
        {
            Vector direction;
            if (!PlanarDirection(load, out direction, out skipReason))
                return null;

            Vector force = direction * (load.Load * ForcePerLengthScale);

            return new GeometricalLineLoad
            {
                Loadcase = loadcase,
                Location = new Line { Start = plane.Local2Global(load.StartCoordinate).ToBHoMPoint(), End = plane.Local2Global(load.EndCoordinate).ToBHoMPoint() },
                Axis = LoadAxis.Global,
                Projected = IsProjected(load),
                ForceA = force,
                ForceB = force,
            };
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // The same direction convention as member loads - see TryDirection: global Z positive downwards,
        // global X and Y positive along the axis; checked on a live model, where every gravity area load
        // in Zg was positive and wind area loads on wind walls carried the same signs as the member wind
        // loads of the same cases. Only global directions are accepted: what a local direction means for
        // a load drawn on a plane rather than on an element has not been checked, and a load along the
        // wrong axis is worse than a load reported as unconvertible.
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

        // Tekla Structural Designer's Measuring says how a load on a sloped surface is measured: along
        // the element (per unit of its true area) or in projection (per unit of its plan area). BHoM's
        // Projected is the second of those.
        private static bool IsProjected(IPlanarLoad load)
        {
            return load.Measuring == DistanceMeasuring.InProjection;
        }

        /***************************************************/
    }
}
