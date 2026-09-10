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
        /****            Public Methods                 ****/
        /***************************************************/

        // Tekla Structural Designer holds three factors against every loadcase in a combination -
        // strength, service and quasi permanent service - where BHoM's LoadCombination holds exactly
        // one factor per case. Which of the three is used is therefore a choice the caller has to
        // make, not something this converter can decide, hence the pull configuration setting.
        //
        // A loadcase referenced by the combination but absent from loadcaseById is skipped rather than
        // silently dropped: that only happens if the model changed between the two reads, and a
        // combination quietly missing a case is exactly the kind of error that survives review.
        public static LoadCombination ToBHoM(
            this ICombination combination,
            string identifier,
            Dictionary<Guid, Loadcase> loadcaseById,
            TeklaStructuralDesignerCombinationFactor factorType,
            out int unresolvedCases)
        {
            unresolvedCases = 0;

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

                cases.Add(new Tuple<double, ICase>(Factor(factors, factorType), loadcase));
            }

            LoadCombination result = new LoadCombination
            {
                Name = identifier,
                Number = combination.Index,
                LoadCases = cases,
            };

            result.Fragments.Add(new TeklaStructuralDesignerId { Id = combination.Id });
            return result;
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        private static double Factor(ILoadcaseFactors factors, TeklaStructuralDesignerCombinationFactor factorType)
        {
            switch (factorType)
            {
                case TeklaStructuralDesignerCombinationFactor.Service:
                    return factors.ServiceFactor.ValueOrDefault(0.0);
                case TeklaStructuralDesignerCombinationFactor.ServiceQuasi:
                    return factors.ServiceQuasiFactor.ValueOrDefault(0.0);
                case TeklaStructuralDesignerCombinationFactor.Strength:
                    return factors.StrengthFactor.ValueOrDefault(0.0);
                default:
                    Engine.Base.Compute.RecordWarning("Unrecognised CombinationFactor '" + factorType + "'; the strength factor has been used.");
                    return factors.StrengthFactor.ValueOrDefault(0.0);
            }
        }

        /***************************************************/
    }
}
