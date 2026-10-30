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
using BH.oM.Adapters.TeklaStructuralDesigner;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Helpers
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        // Loadcases keep their own number; a combination's limit state is the thousands, so combination
        // 48 is 1048 for Strength and 2048 for Service. Nothing collides while every number is below
        // 1000. The Engine's LimitState and CombinationNumber read these same bands back.
        private const int NumberBand = 1000;
        private const int StrengthBand = 1;
        private const int ServiceBand = 2;

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // The number a loadcase or combination's name starts with, as shown in Tekla Structural
        // Designer, or its Index if the name has none. Index alone does not match what users see.
        public static int Number(this ILoadingCase loadingCase)
        {
            int number = ReferenceNumber(loadingCase.Name);
            return number >= 0 ? number : loadingCase.Index;
        }

        /***************************************************/

        // The number a loadcase or combination name starts with, as in '48 1.35Gk + 1.5LL' or
        // '30 LL CONCOURSE', or -1 if it does not start with one.
        public static int ReferenceNumber(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return -1;

            string trimmed = name.TrimStart();
            int end = 0;
            while (end < trimmed.Length && char.IsDigit(trimmed[end]))
                end++;

            int number;
            return end > 0 && int.TryParse(trimmed.Substring(0, end), out number) ? number : -1;
        }

        /***************************************************/

        // The BHoM case number of one limit state of a combination: 1000 + the combination number for
        // Strength, 2000 + it for Service. -1, with an error, for a number outside 0-999.
        public static int CaseNumber(int combinationNumber, TeklaStructuralDesignerLimitState limitState)
        {
            if (combinationNumber < 0 || combinationNumber >= NumberBand)
            {
                Engine.Base.Compute.RecordError("Tekla Structural Designer combination number " + combinationNumber + " is outside 0-" + (NumberBand - 1) +
                    ", so it cannot be given a case number without colliding with another case.");
                return -1;
            }

            int band = limitState == TeklaStructuralDesignerLimitState.Service ? ServiceBand : StrengthBand;
            return band * NumberBand + combinationNumber;
        }

        /***************************************************/

        // The name a pulled LoadCombination is given: the limit state then the combination's own name,
        // as in 'Strength 48 1.35Gk + 1.5LL' and 'Service 48 1.35Gk + 1.5LL'.
        public static string CombinationName(string combinationName, TeklaStructuralDesignerLimitState limitState)
        {
            return limitState + " " + (combinationName ?? "").Trim();
        }

        /***************************************************/

        // The slab and roof unit loadcases Tekla Structural Designer generates for load decomposition.
        // They are not loading of the structure, and are numbered 0.
        public static bool IsUnitLoadcase(this ILoadcase loadcase)
        {
            LoadcaseType type = loadcase.Type.ValueOrDefault(LoadcaseType.Unknown);
            return type == LoadcaseType.SlabUnitLoad || type == LoadcaseType.RoofUnitLoad;
        }

        /***************************************************/

        // Whether the combination is switched on for design (Strength) or deflection (Service).
        public static bool IsAssessedFor(this ICombination combination, TeklaStructuralDesignerLimitState limitState)
        {
            return limitState == TeklaStructuralDesignerLimitState.Service
                ? combination.IsService.ValueOrDefault(false)
                : combination.IsStrength.ValueOrDefault(false);
        }

        /***************************************************/

        // One limit state of a combination, as both its LoadCombination and its results are identified:
        // results are read with the combination's own Guid for Strength and its SlsId for Service, and
        // it is named and numbered by CombinationName and CaseNumber. Null if there is no Guid or number
        // for that limit state.
        public static TsdLoadingCaseIdentity CaseIdentity(this ICombination combination, TeklaStructuralDesignerLimitState limitState)
        {
            Guid resultsId = limitState == TeklaStructuralDesignerLimitState.Service ? combination.SlsId.ValueOrDefault(Guid.Empty) : combination.Id;
            int number = CaseNumber(combination.Number(), limitState);
            if (resultsId == Guid.Empty || number < 0)
                return null;

            return new TsdLoadingCaseIdentity(resultsId, number, CombinationName(combination.Name, limitState), limitState);
        }

        /***************************************************/
    }
}
