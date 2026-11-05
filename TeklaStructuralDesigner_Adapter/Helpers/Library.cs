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
using System.Linq;
using BH.Engine.Base;
using BH.oM.Base;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SectionProperties;
using TSD.API.Remoting.Sections;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Helpers
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        private const string MaterialLibrary = "Structure\\Materials";
        private const string SteelSectionLibrary = "Structure\\SectionProperties";

        // Guards against the same name meaning a different material or section in another catalogue.
        private const double LibraryModulusTolerance = 0.05;
        private const double LibraryAreaTolerance = 0.05;

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // The BHoM library material with the same grade name, if it is the same kind of material and its
        // Young's modulus agrees; otherwise null, with the reason. Cloned, as the library is cached.
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

        // The BHoM library steel section with the section's long or short name, if its area agrees.
        // Cloned, and given the model's grade in place of the dataset's default material.
        public static SteelSection MatchLibrarySection(this ISection section, Steel material, out string rejectedReason)
        {
            rejectedReason = null;

            if (section == null || !(section is INonParametricSection))
                return null;

            double tsdArea = section.CrossSectionalArea * Convert.AreaScale;

            foreach (string candidate in new[] { section.LongName, section.ShortName }.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
            {
                SteelSection match = Engine.Library.Query.Match(SteelSectionLibrary, candidate, true, true) as SteelSection;
                if (match == null)
                    continue;

                if (tsdArea > 0 && Math.Abs(match.Area - tsdArea) > LibraryAreaTolerance * tsdArea)
                {
                    rejectedReason = "'" + candidate + "' matched the BHoM library by name, but its area differs from the one Tekla Structural Designer reports by more than " +
                        (LibraryAreaTolerance * 100) + "%";
                    continue;
                }

                SteelSection result = match.ShallowClone(true);
                result.Material = material;
                return result;
            }

            return null;
        }

        /***************************************************/
    }
}
