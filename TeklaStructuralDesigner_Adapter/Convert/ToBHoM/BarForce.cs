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
        /****            Public Methods                 ****/
        /***************************************************/

        // The one place units, axes and signs are decided for every bar force this adapter returns,
        // whichever of the two read routes produced it - both the batched solver route
        // (IElementEndForces.StartForce/EndForce) and the per span end route
        // (IMemberSpan.GetEndForceAsync) return the same IForce3DLocal type, so one converter serves
        // both.
        //
        // Units: the API documents IForce3DLocal's forces in N and its moments in N.mm, so only the
        // moments are scaled - see Convert/Units.cs. Nothing is rounded.
        //
        // Axes: the API documents Fy as the minor axis shear, Fz the major axis shear, My the major
        // axis moment and Mz the minor, which is BH.oM.Structure.Results.BarForce's own convention.
        // TeklaStructuralDesignerPullConfig.SwapMajorMinorAxes remains as an escape hatch for a model
        // that demonstrates otherwise - see the toolkit README.
        public static BarForce ToBHoM(this IForce3DLocal force, string objectId, int resultCase, double position, int divisions, bool swapMajorMinorAxes)
        {
            double fx = force.Fx;
            double fy = swapMajorMinorAxes ? force.Fz : force.Fy;
            double fz = swapMajorMinorAxes ? force.Fy : force.Fz;

            double mx = force.Mx * MomentScale;
            double my = (swapMajorMinorAxes ? force.Mz : force.My) * MomentScale;
            double mz = (swapMajorMinorAxes ? force.My : force.Mz) * MomentScale;

            return new BarForce(objectId, resultCase, -1, 0.0, position, divisions, fx, fy, fz, mx, my, mz);
        }

        /***************************************************/
    }
}
