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
        // Axes: the API documents IForce3DLocal only as forces "with respect to element local coordinate
        // system", without saying which local axis is major. The mapping here - Fy minor axis shear, Fz
        // major, My major axis moment, Mz minor, BH.oM.Structure.Results.BarForce's own convention - is
        // the one the column check in Convert/ToBHoM/Bar.cs and the section orientation both point to.
        // It is still unconfirmed for beams: one tester (on Tekla Structural Designer 2024, against a
        // 2026 API) found MZ matching Tekla Structural Designer's major axis moment. The
        // GravityBendingIsMajorAxis test settles it on a live model; until it has,
        // TeklaStructuralDesignerPullConfig.SwapMajorMinorAxes is the escape hatch - see the README.
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
