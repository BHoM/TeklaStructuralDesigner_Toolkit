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
using TsdTimber = TSD.API.Remoting.Materials.ITimber;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Steel and concrete become BHoM Steel and Concrete, so Create.SectionPropertyFromProfile makes
        // the matching section type. Timber and general materials stay GenericIsotropicMaterial, as BHoM
        // timber is orthotropic and the API reports only one modulus.
        public static IMaterialFragment FromTsdMaterial(this IMaterial material)
        {
            if (material == null)
                return null;

            string name = material.Name ?? "";
            double poissonsRatio = material.PoissonsRatio;
            double thermalExpansion = material.ThermalExpansionCoefficient;

            ISteel steel = material as ISteel;
            if (steel != null)
            {
                return Engine.Structure.Create.Steel(name,
                    steel.ElasticModulus * PressureScale, poissonsRatio, thermalExpansion, steel.Density * DensityScale, 0,
                    steel.MinimumYieldStrength * PressureScale, steel.MinimumTensileStrength * PressureScale);
            }

            IColdRolled coldRolled = material as IColdRolled;
            if (coldRolled != null)
            {
                return Engine.Structure.Create.Steel(name,
                    coldRolled.ElasticModulus * PressureScale, poissonsRatio, thermalExpansion, coldRolled.Density * DensityScale, 0,
                    coldRolled.YieldStrength * PressureScale, coldRolled.TensileStrength * PressureScale);
            }

            // Wet density, as self weight is calculated from it.
            IConcrete concrete = material as IConcrete;
            if (concrete != null)
            {
                return Engine.Structure.Create.Concrete(name,
                    concrete.BaseElasticModulus * PressureScale, poissonsRatio, thermalExpansion, concrete.WetDensity * DensityScale, 0,
                    concrete.CubeStrength * PressureScale, concrete.CylinderStrength * PressureScale);
            }

            TsdTimber timber = material as TsdTimber;
            if (timber != null)
                return Generic(name, timber.AnalysisElasticModulus * PressureScale, poissonsRatio, thermalExpansion, timber.Density * DensityScale);

            IGeneralMaterial general = material as IGeneralMaterial;
            if (general != null)
                return Generic(name, general.AnalysisElasticModulus * PressureScale, poissonsRatio, thermalExpansion, general.Density * DensityScale);

            // Nothing more specific is exposed: E from G and nu, density unknown.
            double youngsModulus = 2.0 * material.ShearModulus * PressureScale * (1.0 + poissonsRatio);
            return Generic(name, youngsModulus, poissonsRatio, thermalExpansion, 0);
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        private static GenericIsotropicMaterial Generic(string name, double youngsModulus, double poissonsRatio, double thermalExpansion, double density)
        {
            return new GenericIsotropicMaterial
            {
                Name = name,
                YoungsModulus = youngsModulus,
                PoissonsRatio = poissonsRatio,
                ThermalExpansionCoeff = thermalExpansion,
                Density = density,
            };
        }

        /***************************************************/
    }
}
