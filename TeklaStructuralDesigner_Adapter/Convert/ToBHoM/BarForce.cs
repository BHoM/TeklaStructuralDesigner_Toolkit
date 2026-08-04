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
        // Units: force components are already newtons - the WPF tool this was ported from applied a
        // 1e-3 scale to reach kN, which is only explicable if the raw values are in N. Moments are
        // therefore in N.mm, so only the moments need the 1e-3 factor to reach BHoM's SI N.m. Nothing
        // is rounded: rounding to whole numbers, which the ported tool did, is presentation logic and
        // has no place in an adapter.
        //
        // Axes: BH.oM.Structure.Results.BarForce's own descriptions are the authority here - FY is
        // "generally minor axis shear", FZ "generally major axis shear", MY "generally major axis
        // bending", MZ "generally minor axis bending". Whether Tekla Structural Designer's local y/z
        // agree with that or are transposed for a given model is exactly what
        // TeklaStructuralDesignerPullConfig.SwapMajorMinorAxes exists to let a user correct, once
        // checked against a model with a known answer - see the toolkit README.
        public static BarForce ToBHoM(this IForce3DLocal force, string objectId, string resultCase, double position, int divisions, bool swapMajorMinorAxes)
        {
            const double MomentScale = 0.001; // Tekla Structural Designer reports moments in N.mm; BHoM wants N.m.

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
