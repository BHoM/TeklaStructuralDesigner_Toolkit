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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Loads;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Loadcases and combinations)   ****/
        /***************************************************/

        // Note the difference from BuildLoadingCaseIdentities: that reads whichever of loadcases and
        // combinations the pull configuration says results are wanted for, and reduces each to a Guid
        // and a name. This reads them as objects in their own right, so it is not filtered by those
        // flags - asking to Pull a Loadcase is already an unambiguous request for every loadcase.
        private Dictionary<Guid, Loadcase> LoadcasesById(int timeoutSeconds)
        {
            Dictionary<Guid, Loadcase> result = new Dictionary<Guid, Loadcase>();

            List<ILoadcase> loadcases;
            try
            {
                loadcases = Async.RunSync(ct => m_Model.GetLoadcasesAsync(null, ct), timeoutSeconds, "reading loadcases").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read loadcases from Tekla Structural Designer. " + e.Message);
                return result;
            }

            foreach (ILoadcase loadcase in loadcases)
            {
                if (result.ContainsKey(loadcase.Id))
                    continue;

                result[loadcase.Id] = loadcase.ToBHoM(Identifier(loadcase.Name, loadcase.UserName, loadcase.Index));
            }

            return result;
        }

        /***************************************************/

        private List<Loadcase> ReadLoadcases(IList ids)
        {
            return Filter(LoadcasesById(TeklaStructuralDesignerConfig.TimeoutSeconds).Values.ToList(), ids, c => c.Name);
        }

        /***************************************************/

        private List<LoadCombination> ReadLoadCombinations(IList ids, TeklaStructuralDesignerPullConfig config)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            // The loadcases have to be read first regardless of whether the caller wanted them: a
            // LoadCombination's LoadCases hold real Loadcase objects, not identifiers.
            Dictionary<Guid, Loadcase> loadcaseById = LoadcasesById(timeout);

            List<ICombination> combinations;
            try
            {
                combinations = Async.RunSync(ct => m_Model.GetCombinationsAsync(null, ct), timeout, "reading load combinations").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read load combinations from Tekla Structural Designer. " + e.Message);
                return new List<LoadCombination>();
            }

            TeklaStructuralDesignerCombinationFactor factorType = config.CombinationFactor;

            List<LoadCombination> result = new List<LoadCombination>();
            int totalUnresolved = 0;

            foreach (ICombination combination in combinations)
            {
                int unresolved;
                result.Add(combination.ToBHoM(
                    Identifier(combination.Name, combination.UserName, combination.Index),
                    loadcaseById, factorType, out unresolved));

                totalUnresolved += unresolved;
            }

            if (totalUnresolved > 0)
            {
                Engine.Base.Compute.RecordWarning(totalUnresolved + " loadcase reference(s) across the pulled combinations could not be matched to a loadcase and have been left out of those combinations. " +
                    "This normally means the model was edited between the two reads; pull again.");
            }

            Engine.Base.Compute.RecordNote("Load combination factors were taken from Tekla Structural Designer's " + factorType +
                " factor. Set CombinationFactor on the pull configuration to use a different one.");

            return result;
        }

        /***************************************************/

        // Shared id filtering. BHoM passes ids through as objects, and this adapter's convention is
        // that a case is addressed by the same identifier it carries as its Name and as a result's
        // ResultCase - never by the Guid, which is meaningless to whoever is writing the request.
        private static List<T> Filter<T>(List<T> items, IList ids, Func<T, string> identifier)
        {
            if (ids == null || ids.Count == 0)
                return items;

            HashSet<string> requested = new HashSet<string>(
                ids.Cast<object>().Where(id => id != null).Select(id => id.ToString()), StringComparer.OrdinalIgnoreCase);

            return items.Where(item => requested.Contains(identifier(item))).ToList();
        }

        /***************************************************/
    }
}
