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
using TsdQuery = BH.Engine.Adapters.TeklaStructuralDesigner.Query;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Helpers
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // The number a loadcase or combination's name starts with, as shown in Tekla Structural
        // Designer, or its Index if the name has none. Index alone does not match what users see.
        public static int Number(this ILoadingCase loadingCase)
        {
            int number = TsdQuery.ReferenceNumber(loadingCase.Name);
            return number >= 0 ? number : loadingCase.Index;
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
        // it is named and numbered by the Engine's CombinationName and CaseNumber. Null if there is no
        // Guid or number for that limit state.
        public static TsdLoadingCaseIdentity CaseIdentity(this ICombination combination, TeklaStructuralDesignerLimitState limitState)
        {
            Guid resultsId = limitState == TeklaStructuralDesignerLimitState.Service ? combination.SlsId.ValueOrDefault(Guid.Empty) : combination.Id;
            int number = TsdQuery.CaseNumber(combination.Number(), limitState);
            if (resultsId == Guid.Empty || number < 0)
                return null;

            return new TsdLoadingCaseIdentity(resultsId, number, TsdQuery.CombinationName(combination.Name, limitState), limitState);
        }

        /***************************************************/
    }
}
