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
using BH.oM.Structure.SectionProperties;
using TSD.API.Remoting.Sections;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // Deliberately lossy: ISection exposes only the section constants, not profile geometry, so an
        // ExplicitSection is the only honest representation - there is no radius of gyration, section
        // modulus or warping constant available, and those properties are left at zero rather than
        // derived, to avoid presenting a computed guess as data Tekla Structural Designer reported.
        //
        // Axes: Iy is taken from MajorAxisSecondMomentOfArea and Iz from MinorAxisSecondMomentOfArea,
        // and Asy/Asz the same way round, consistent with BH.oM.Structure.Results.BarForce's own
        // convention that MY/FZ are the major axis components and MZ/FY the minor axis ones - see
        // Convert/ToBHoM/BarForce.cs.
        public static ISectionProperty ToBHoM(this ISection section, IMaterialFragment material)
        {
            if (section == null)
                return null;

            const double AreaScale = 1e-6;     // mm^2 -> m^2
            const double InertiaScale = 1e-12;  // mm^4 -> m^4

            return new ExplicitSection
            {
                Name = section.LongName ?? section.ShortName ?? "",
                Material = material,
                Area = section.CrossSectionalArea * AreaScale,
                Iy = section.MajorAxisSecondMomentOfArea * InertiaScale,
                Iz = section.MinorAxisSecondMomentOfArea * InertiaScale,
                J = section.TorsionConstant * InertiaScale,
                Asy = section.ShearAreaLoadedParallelToMinorAxis * AreaScale,
                Asz = section.ShearAreaLoadedParallelToMajorAxis * AreaScale,
            };
        }

        /***************************************************/
    }
}
