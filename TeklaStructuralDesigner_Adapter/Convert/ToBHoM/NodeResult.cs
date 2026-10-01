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
using BH.oM.Geometry;
using BH.oM.Structure.Results;
using TSD.API.Remoting.Loading;

// BHoM and Tekla Structural Designer both declare an IDisplacement; the one converted here is Tekla's.
using IDisplacement = TSD.API.Remoting.Loading.IDisplacement;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // The one place units, axes and signs are decided for the node results this adapter returns.
        //
        // Units: the API documents IForce3d's forces in N and its moments in N.mm, and IDisplacement's
        // movements in mm and its rotations in rad - see Convert/Units.cs. Nothing is rounded.
        //
        // Axes: both are reported in the global coordinate system, so the Orientation of every result
        // is the global basis.
        //
        // Signs: nothing is reversed. Unlike applied loads, which Tekla Structural Designer holds gravity
        // positive, results have +Z upwards as BHoM does. Checked on a live model: under self weight the
        // vertical reactions were positive and the horizontal ones summed to zero, and the vertical
        // displacements were negative.

        // The reaction at a Node. More than one solver node can sit at a Node's position, so the forces
        // of every one of them are added up.
        public static NodeReaction ToBHoM(this IEnumerable<IForce3DGlobal> forces, string objectId, int resultCase)
        {
            double fx = 0, fy = 0, fz = 0, mx = 0, my = 0, mz = 0;

            foreach (IForce3DGlobal force in forces)
            {
                fx += force.Fx;
                fy += force.Fy;
                fz += force.Fz;
                mx += force.Mx;
                my += force.My;
                mz += force.Mz;
            }

            return new NodeReaction(objectId, resultCase, -1, 0.0, Basis.XY,
                fx * ForceScale, fy * ForceScale, fz * ForceScale,
                mx * MomentScale, my * MomentScale, mz * MomentScale);
        }

        /***************************************************/

        public static NodeDisplacement ToBHoM(this IDisplacement displacement, string objectId, int resultCase)
        {
            return new NodeDisplacement(objectId, resultCase, -1, 0.0, Basis.XY,
                displacement.Mx * LengthScale, displacement.My * LengthScale, displacement.Mz * LengthScale,
                displacement.Rx, displacement.Ry, displacement.Rz);
        }

        /***************************************************/
    }
}
