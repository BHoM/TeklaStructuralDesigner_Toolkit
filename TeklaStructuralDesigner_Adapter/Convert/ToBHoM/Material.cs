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

using System;
using BH.Engine.Base;
using BH.oM.Base;
using BH.oM.Structure.MaterialFragments;
using TSD.API.Remoting.Materials;

// Both BHoM and Tekla Structural Designer declare an ITimber; only the Tekla one is used here.
using TsdTimber = TSD.API.Remoting.Materials.ITimber;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        // Every structural material dataset BHoM ships (Europe and USA).
        private const string MaterialLibrary = "Structure\\Materials";

        // How far a library material's Young's modulus may differ from the model's before a name match
        // is refused - the same grade name can mean a different material under another standard.
        private const double LibraryModulusTolerance = 0.05;

        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // The BHoM library material with the same grade name ("S355", "C30/37"), found through
        // Library.Query.Match, or null if there is none. Only accepted if it is the same kind of material
        // as the one converted from the model, and its Young's modulus agrees; otherwise the model's
        // own values are used and the reason is returned. The library object is cloned, because
        // Library_Engine hands out shared cached instances.
        public static IMaterialFragment MatchLibraryMaterial(this IMaterialFragment fromModel, out string rejectedReason)
        {
            rejectedReason = null;

            if (fromModel == null || string.IsNullOrWhiteSpace(fromModel.Name) || !(fromModel is Steel || fromModel is Concrete))
                return null;

            IMaterialFragment match = Engine.Library.Query.Match(MaterialLibrary, fromModel.Name, true, true) as IMaterialFragment;
            if (match == null)
                return null;

            if (match.GetType() != fromModel.GetType())
            {
                rejectedReason = "'" + fromModel.Name + "' matched a " + match.GetType().Name + " in the BHoM material library, but it is a " + fromModel.GetType().Name + " in the model";
                return null;
            }

            double modelModulus = ((IIsotropic)fromModel).YoungsModulus;
            double libraryModulus = ((IIsotropic)match).YoungsModulus;
            if (modelModulus > 0 && Math.Abs(libraryModulus - modelModulus) > LibraryModulusTolerance * modelModulus)
            {
                rejectedReason = "'" + fromModel.Name + "' matched the BHoM material library by name, but its Young's modulus (" + (libraryModulus / 1e9).ToString("G4") +
                    " GPa) differs from the model's (" + (modelModulus / 1e9).ToString("G4") + " GPa) by more than " + (LibraryModulusTolerance * 100) + "%";
                return null;
            }

            return ((IBHoMObject)match).ShallowClone(true) as IMaterialFragment;
        }

        /***************************************************/

        // Steel and concrete become BHoM Steel and Concrete, built through BHoM's own Create methods, so
        // that Structure.Create.SectionPropertyFromProfile - which picks the section type from the
        // material type - produces a SteelSection or ConcreteSection from them. This is the model's own
        // material; BarPropertyCache prefers the library match above when there is one.
        //
        // Timber and general materials have no BHoM equivalent this converter can fill honestly (BHoM
        // timber is orthotropic, the API reports a single analysis modulus), so they, and anything
        // unrecognised, stay GenericIsotropicMaterial.
        public static IMaterialFragment ToBHoM(this IMaterial material)
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

            // Wet density, because that is the one that is load bearing in an analysis: it is what the
            // self weight of the member is calculated from.
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

            // Nothing more specific is exposed: derive E from G and nu, and leave density unknown.
            double youngsModulus = 2.0 * material.ShearModulus * PressureScale * (1.0 + poissonsRatio);
            return Generic(name, youngsModulus, poissonsRatio, thermalExpansion, 0);
        }

        /***************************************************/
        /****            Private Methods                ****/
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
