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

using BH.oM.Geometry;
using BH.oM.Spatial.SettingOut;
using TSD.API.Remoting.Structure;
using TsdLocation = TSD.API.Remoting.Geometry.Location;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;
using TsdSegment2D = TSD.API.Remoting.Geometry.ISegment2D;
using TsdSegmentType = TSD.API.Remoting.Geometry.SegmentType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // One BHoM Grid per grid line: a BHoM Grid is a single named curve, where a Tekla Structural
        // Designer architectural grid is a set of them. A grid line is a construction helper of its
        // architectural grid, drawn in the local coordinates of that grid's plane, so it is placed in 3D
        // through the plane.
        //
        // Arc grid lines are refused rather than guessed at: the API gives an arc's centre, radius and
        // orientation, but no model with one was available to check how they are to be read.
        public static Grid ToBHoM(this IConstructionHelper line, TsdPlane plane, out string skipReason)
        {
            skipReason = null;

            TsdSegment2D segment = line.Segment.ValueOrDefault();
            if (segment == null || plane == null)
            {
                skipReason = "Tekla Structural Designer reported no geometry for them";
                return null;
            }

            if (segment.Type.ValueOrDefault(TsdSegmentType.Unknown) != TsdSegmentType.Line)
            {
                skipReason = "they are not straight, and only straight grid lines are converted";
                return null;
            }

            Grid grid = new Grid
            {
                Name = line.Name,
                Curve = new Line
                {
                    Start = plane.Local2Global(segment.GetPoint(TsdLocation.Start)).ToBHoMPoint(),
                    End = plane.Local2Global(segment.GetPoint(TsdLocation.End)).ToBHoMPoint(),
                },
            };

            return grid.SetIdentity(line.Name, line.Id);
        }

        /***************************************************/
    }
}
