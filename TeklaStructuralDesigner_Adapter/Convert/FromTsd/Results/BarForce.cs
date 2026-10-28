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

using BH.oM.Structure.Results;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Used by both bar force routes. See "Bar force sign convention" in the README:
        //  - Tekla Structural Designer reports end forces, so the start end is reversed (isStart).
        //  - Its moments are named after the shear they go with, so its Mz is BHoM's MY and vice versa.
        //  - Its local y and z are BHoM's turned half way round x on non-vertical members, so their
        //    components change sign. isVertical must come from Helpers.IsVertical, which matches the
        //    quarter turn Bar orientation gives vertical members.
        public static BarForce FromTsdBarForce(this IForce3DLocal force, string objectId, int resultCase, double position, int divisions, bool isStart, bool isVertical, bool swapMajorMinorAxes)
        {
            double end = isStart ? -1.0 : 1.0;
            double frame = isVertical ? 1.0 : -1.0;

            double fx = end * force.Fx;
            double fy = end * frame * force.Fy;
            double fz = end * frame * force.Fz;
            double mx = end * force.Mx * MomentScale;
            double my = end * frame * force.Mz * MomentScale;
            double mz = end * frame * force.My * MomentScale;

            if (swapMajorMinorAxes)
                return new BarForce(objectId, resultCase, -1, 0.0, position, divisions, fx, fz, fy, mx, mz, my);

            return new BarForce(objectId, resultCase, -1, 0.0, position, divisions, fx, fy, fz, mx, my, mz);
        }

        /***************************************************/
    }
}
