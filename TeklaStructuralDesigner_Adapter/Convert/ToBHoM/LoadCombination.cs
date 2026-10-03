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
using TsdQuery = BH.Engine.Adapters.TeklaStructuralDesigner.Query;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // One limit state of a Tekla Structural Designer combination, as a BHoM LoadCombination.
        //
        // Tekla Structural Designer holds a strength, a service and a quasi permanent service factor
        // against every loadcase of a combination; BHoM's LoadCombination holds one. So each
        // combination is pulled once per limit state - Strength and Service; quasi permanent service is
        // not pulled - named and numbered by the Engine's convention (Query.CombinationName and
        // Query.CaseNumber): Tekla Structural Designer combination 48 becomes 'Strength 48 ...', number
        // 1048, and 'Service 48 ...', number 2048.
        //
        // The TeklaStructuralDesignerId carries the Guid results for this limit state are read with:
        // the combination's own for Strength, its SlsId for Service. Null if the combination has no
        // SlsId, since a Service combination with no results behind it would be a trap.
        //
        // A loadcase referenced by the combination but absent from loadcaseById is skipped rather than
        // silently dropped: that only happens if the model changed between the two reads, and a
        // combination quietly missing a case is exactly the kind of error that survives review.
        public static LoadCombination ToBHoM(
            this ICombination combination,
            TeklaStructuralDesignerLimitState limitState,
            Dictionary<Guid, Loadcase> loadcaseById,
            out int unresolvedCases)
        {
            unresolvedCases = 0;

            Guid resultsId = combination.ResultsId(limitState);
            if (resultsId == Guid.Empty)
                return null;

            int combinationNumber = combination.Number();
            int caseNumber = TsdQuery.CaseNumber(combinationNumber, limitState);
            if (caseNumber < 0)
                return null;

            // A loadcase with a factor of zero contributes nothing, and Tekla Structural Designer lists
            // every loadcase against every combination, so those are left out - as BHoM's own
            // Create.LoadCombination does by default.
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

            string name = TsdQuery.CombinationName(combination.Name, limitState);

            LoadCombination result = new LoadCombination
            {
                Name = name,
                Number = caseNumber,
                LoadCases = cases,
            }.SetIdentity(name, resultsId);

            result.Fragments.Add(new TeklaStructuralDesignerCombinationProperties
            {
                LimitState = limitState,
                CombinationNumber = combinationNumber,
                CombinationName = combination.Name ?? "",
                CombinationId = combination.Id,
            });

            return result;
        }

        /***************************************************/

        // The Guid results for one limit state of the combination are read with: the combination's own
        // for Strength, its SlsId for Service. Guid.Empty if the combination has no SlsId, since a
        // Service combination with no results behind it would be a trap.
        public static Guid ResultsId(this ICombination combination, TeklaStructuralDesignerLimitState limitState)
        {
            return limitState == TeklaStructuralDesignerLimitState.Service ? combination.SlsId.ValueOrDefault(Guid.Empty) : combination.Id;
        }

        /***************************************************/

        // Whether Tekla Structural Designer assesses the combination at the limit state - its Strength
        // ("assessed for design") and Service ("assessed for deflection") switches. A combination
        // switched off for a limit state has no results worth reading at it, so none is pulled.
        public static bool IsAssessedFor(this ICombination combination, TeklaStructuralDesignerLimitState limitState)
        {
            return limitState == TeklaStructuralDesignerLimitState.Service
                ? combination.IsService.ValueOrDefault(false)
                : combination.IsStrength.ValueOrDefault(false);
        }

        /***************************************************/
    }
}
