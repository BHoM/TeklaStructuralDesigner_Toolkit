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

using BH.oM.Structure.MaterialFragments;
using TSD.API.Remoting.Materials;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // Deliberately lossy: the Remoting API's IMaterial exposes only PoissonsRatio, ShearModulus
        // and ThermalExpansionCoefficient, so this is as much of a material as can be reconstructed -
        // there is no yield strength, no design code grade data, and no density, so Density is left at
        // zero. Anyone running a self-weight or design check against a Bar pulled from this adapter
        // needs to know that, which is why BuildSpanIdentities issues a RecordNote about it once.
        //
        // Units: ShearModulus is assumed to be in N/mm^2 (MPa), consistent with the mm/N unit system
        // observed elsewhere in this API (forces in N, moments in N.mm, lengths in mm) - this specific
        // value has not been checked against a live model; see the toolkit README.
        public static IMaterialFragment ToBHoM(this IMaterial material)
        {
            if (material == null)
                return null;

            double poissonsRatio = material.PoissonsRatio;
            double shearModulusPascals = material.ShearModulus * 1e6;
            double youngsModulusPascals = 2.0 * shearModulusPascals * (1.0 + poissonsRatio);

            return new GenericIsotropicMaterial
            {
                Name = material.Name ?? "",
                YoungsModulus = youngsModulusPascals,
                PoissonsRatio = poissonsRatio,
                ThermalExpansionCoeff = material.ThermalExpansionCoefficient,
                Density = 0,
            };
        }

        /***************************************************/
    }
}
