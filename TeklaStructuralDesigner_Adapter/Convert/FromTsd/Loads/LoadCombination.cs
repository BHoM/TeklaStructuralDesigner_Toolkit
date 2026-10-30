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
using System.Collections.Generic;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Loads;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // One limit state of a combination, named and numbered as in Helpers.CaseIdentity. Loadcases
        // missing from loadcaseById (the model changed between reads) are counted in unresolvedCases.
        public static LoadCombination FromTsdLoadCombination(
            this ICombination combination,
            TeklaStructuralDesignerLimitState limitState,
            Dictionary<Guid, Loadcase> loadcaseById,
            out int unresolvedCases)
        {
            unresolvedCases = 0;

            TsdLoadingCaseIdentity identity = combination.CaseIdentity(limitState);
            if (identity == null)
                return null;

            // Zero factors are left out, as BHoM's Create.LoadCombination does.
            List<Tuple<double, ICase>> cases = new List<Tuple<double, ICase>>();

            foreach (ILoadcaseFactors factors in combination.LoadcaseFactors.ValuesOrEmpty())
            {
                if (factors == null)
                    continue;

                Guid loadcaseId = factors.LoadcaseId.ValueOrDefault(Guid.Empty);

                Loadcase loadcase;
                if (loadcaseId == Guid.Empty || !loadcaseById.TryGetValue(loadcaseId, out loadcase))
                {
                    unresolvedCases++;
                    continue;
                }

                double factor = limitState == TeklaStructuralDesignerLimitState.Service
                    ? factors.ServiceFactor.ValueOrDefault(0.0)
                    : factors.StrengthFactor.ValueOrDefault(0.0);

                if (factor != 0)
                    cases.Add(new Tuple<double, ICase>(factor, loadcase));
            }

            LoadCombination result = new LoadCombination
            {
                Name = identity.Name,
                Number = identity.Number,
                LoadCases = cases,
            }.SetIdentity(identity.Name, identity.Id);

            result.Fragments.Add(new TeklaStructuralDesignerCombinationProperties
            {
                LimitState = limitState,
                CombinationNumber = combination.Number(),
                CombinationName = combination.Name ?? "",
                CombinationId = combination.Id,
            });

            return result;
        }

        /***************************************************/
    }
}
