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
using IDisplacement = TSD.API.Remoting.Loading.IDisplacement;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Node results are in global axes with +Z up, as BHoM's, so no signs change.

        // Summed over every solver node at the Node's position.
        public static NodeReaction FromTsdNodeReaction(this IEnumerable<IForce3DGlobal> forces, string objectId, int resultCase)
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

        public static NodeDisplacement FromTsdNodeDisplacement(this IDisplacement displacement, string objectId, int resultCase)
        {
            return new NodeDisplacement(objectId, resultCase, -1, 0.0, Basis.XY,
                displacement.Mx * LengthScale, displacement.My * LengthScale, displacement.Mz * LengthScale,
                displacement.Rx, displacement.Ry, displacement.Rz);
        }

        /***************************************************/
    }
}
