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

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        // Unit scaling, in one place for the whole Convert class. Tekla Structural Designer works in
        // newtons and millimetres, as the Remoting API documents on each property (forces "[N]",
        // moments "[Nmm]", distributed loads "[N/mm]", area loads "[N/mm²]", stresses "[N/mm²]",
        // densities "[kg/mm³]", lengths "[mm]", angles "[rad]"); BHoM is SI.
        //
        //     length            mm            -> m              1e-3
        //     force             N             -> N              1
        //     moment            N.mm          -> N.m            1e-3
        //     force / length    N/mm          -> N/m            1e3
        //     moment / length   (N.mm)/mm = N -> (N.m)/m = N    1
        //     pressure, stress  N/mm²         -> Pa             1e6
        //     density           kg/mm³        -> kg/m³          1e9
        //     area              mm²           -> m²             1e-6
        //     second moment     mm⁴           -> m⁴             1e-12
        //
        // The moment per length one is not a typo: a moment per unit length has the dimensions of a
        // force, and the millimetres cancel, so a distributed torsion moment needs no scaling at all.
        internal const double LengthScale = 1e-3;
        internal const double ForceScale = 1.0;
        internal const double MomentScale = 1e-3;
        internal const double ForcePerLengthScale = 1e3;
        internal const double MomentPerLengthScale = 1.0;
        internal const double PressureScale = 1e6;
        internal const double DensityScale = 1e9;
        internal const double AreaScale = 1e-6;
        internal const double InertiaScale = 1e-12;

        /***************************************************/
    }
}
