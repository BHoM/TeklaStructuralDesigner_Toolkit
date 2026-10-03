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
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        // The order the limit states of every combination are pulled in: all Strength first, then all
        // Service, so a list of combinations reads in the same order as the case numbers.
        private static readonly TeklaStructuralDesignerLimitState[] m_LimitStates = { TeklaStructuralDesignerLimitState.Strength, TeklaStructuralDesignerLimitState.Service };

        /***************************************************/
        /****            Private Methods                ****/
        /****            (Loadcases and combinations)   ****/
        /***************************************************/

        // Note the difference from BuildLoadingCaseIdentities: that reduces every case to the Guid its
        // results are read with, its Number and its Name, and results are filtered by the pull
        // configuration's Include flags. This reads cases as objects in their own right, which those
        // flags do not affect - asking to Pull a Loadcase is already an unambiguous request for every
        // loadcase.
        private Dictionary<Guid, Loadcase> LoadcasesById(int timeoutSeconds)
        {
            return BHoMLoadcases(ReadTsdLoadcases(timeoutSeconds, null));
        }

        /***************************************************/

        // The BHoM Loadcase of each Tekla Structural Designer loadcase, keyed by its Guid, so that a
        // loadcase pulled on its own, as the case of a load and as the case of a combination is built
        // the same way.
        private static Dictionary<Guid, Loadcase> BHoMLoadcases(List<ILoadcase> loadcases)
        {
            Dictionary<Guid, Loadcase> result = new Dictionary<Guid, Loadcase>();
            Dictionary<Guid, int> numberById = LoadcaseNumbers(loadcases);

            foreach (ILoadcase loadcase in loadcases)
            {
                if (!result.ContainsKey(loadcase.Id))
                    result[loadcase.Id] = loadcase.ToBHoM(Identifier(loadcase.Name, loadcase.UserName, loadcase.Index), numberById[loadcase.Id]);
            }

            return result;
        }

        /***************************************************/

        // The loadcases that are pulled: every one except Tekla Structural Designer's own slab and roof
        // unit loadcases, which are not loading of the structure - see Convert.IsUnitLoadcase. Every
        // read of loadcases goes through this, so that Loadcase, load and result pulls agree on which
        // loadcases exist. Empty, with an error naming the consequence, if the read fails.
        private List<ILoadcase> ReadTsdLoadcases(int timeoutSeconds, string consequence)
        {
            try
            {
                return Async.RunSync(ct => m_Model.GetLoadcasesAsync(null, ct), timeoutSeconds, "reading loadcases")
                    .Where(l => !l.IsUnitLoadcase())
                    .ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read loadcases from Tekla Structural Designer" + (consequence != null ? ", " + consequence : "") + ". " + e.Message);
                return new List<ILoadcase>();
            }
        }

        /***************************************************/

        // Every combination in the model; empty, with an error, if the read fails.
        private List<ICombination> ReadTsdCombinations(int timeoutSeconds)
        {
            try
            {
                return Async.RunSync(ct => m_Model.GetCombinationsAsync(null, ct), timeoutSeconds, "reading load combinations").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read load combinations from Tekla Structural Designer. " + e.Message);
                return new List<ICombination>();
            }
        }

        /***************************************************/

        // The BHoM Number of each loadcase, keyed by its Guid. The number Tekla Structural Designer shows
        // is kept wherever it can be; a loadcase whose number is 0 or less, or repeats one already taken,
        // gets the next number above the highest in use instead, with a warning naming it, because a
        // BHoM case number must be positive and unique - an analysis package refuses a case without one.
        // Every read of loadcases goes through this, so the same loadcase has the same number whether it
        // is pulled as a Loadcase, as the case of a load, or as the case of a result.
        private static Dictionary<Guid, int> LoadcaseNumbers(List<ILoadcase> loadcases)
        {
            Dictionary<Guid, int> result = new Dictionary<Guid, int>();
            HashSet<int> taken = new HashSet<int>();
            List<ILoadcase> renumber = new List<ILoadcase>();

            foreach (ILoadcase loadcase in loadcases.OrderBy(l => l.Index))
            {
                int number = loadcase.Number();
                if (number > 0 && taken.Add(number))
                    result[loadcase.Id] = number;
                else
                    renumber.Add(loadcase);
            }

            int next = taken.Count == 0 ? 1 : taken.Max() + 1;
            foreach (ILoadcase loadcase in renumber)
                result[loadcase.Id] = next++;

            if (renumber.Count > 0)
            {
                Engine.Base.Compute.RecordWarning(renumber.Count + " loadcase(s) have a Tekla Structural Designer number of 0 or less, or one already used by another loadcase, " +
                    "so they have been numbered from " + (next - renumber.Count) + " instead: " +
                    string.Join(", ", renumber.Select(l => "'" + (l.Name ?? "").Trim() + "' -> " + result[l.Id])) + ".");
            }

            return result;
        }

        /***************************************************/

        private List<Loadcase> ReadLoadcases(IList ids)
        {
            return Filter(LoadcasesById(TeklaStructuralDesignerConfig.TimeoutSeconds).Values.ToList(), ids, c => c.Name, c => c.Number);
        }

        /***************************************************/

        private List<LoadCombination> ReadLoadCombinations(IList ids)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            // The loadcases have to be read first regardless of whether the caller wanted them: a
            // LoadCombination's LoadCases hold real Loadcase objects, not identifiers.
            Dictionary<Guid, Loadcase> loadcaseById = LoadcasesById(timeout);

            List<ICombination> combinations = ReadTsdCombinations(timeout);

            // Each combination becomes a Strength and a Service LoadCombination, for the limit states
            // Tekla Structural Designer assesses it at.
            List<LoadCombination> result = new List<LoadCombination>();
            int totalUnresolved = 0;
            int noSlsId = 0;
            int withNotionalLoads = 0;

            foreach (TeklaStructuralDesignerLimitState limitState in m_LimitStates)
            {
                foreach (ICombination combination in combinations)
                {
                    if (!combination.IsAssessedFor(limitState))
                        continue;

                    int unresolved;
                    LoadCombination converted = combination.ToBHoM(limitState, loadcaseById, out unresolved);
                    totalUnresolved += unresolved;

                    if (converted == null)
                    {
                        if (limitState == TeklaStructuralDesignerLimitState.Service)
                            noSlsId++;
                        continue;
                    }

                    result.Add(converted);

                    if (limitState == TeklaStructuralDesignerLimitState.Strength && HasNotionalLoads(combination))
                        withNotionalLoads++;
                }
            }

            result = Filter(result, ids, c => c.Name, c => c.Number);

            if (totalUnresolved > 0)
            {
                Engine.Base.Compute.RecordWarning(totalUnresolved + " loadcase reference(s) across the pulled combinations could not be matched to a loadcase and have been left out of those combinations. " +
                    "This normally means the model was edited between the two reads; pull again.");
            }

            if (noSlsId > 0)
            {
                Engine.Base.Compute.RecordWarning(noSlsId + " combination(s) are assessed for Service but Tekla Structural Designer reported no service combination for them, " +
                    "so no Service LoadCombination has been pulled for them. Re-analyse the model and pull again.");
            }

            // Checked against a live model: Tekla Structural Designer's Strength results include the
            // combination's notional horizontal loads, which are not a loadcase and so cannot be one of
            // the LoadCombination's factored cases. The Service results match their factors exactly.
            if (withNotionalLoads > 0)
            {
                Engine.Base.Compute.RecordNote(withNotionalLoads + " Strength combination(s) include notional horizontal loads in Tekla Structural Designer. " +
                    "They are not a loadcase, so the pulled LoadCombination does not carry them: its factored loadcases will not add up to the Strength results Tekla Structural Designer reports for it. " +
                    "Results pulled from Tekla Structural Designer do include them.");
            }

            return result;
        }

        /***************************************************/

        // Shared id filtering. BHoM passes ids through as objects, and this adapter's convention is
        // that a case is addressed by the same identifier it carries as its Name and as a result's
        // ResultCase - never by the Guid, which is meaningless to whoever is writing the request.
        // A case can also be asked for by its Number, given as a number or as text.
        private static List<T> Filter<T>(List<T> items, IList ids, Func<T, string> identifier, Func<T, int> number)
        {
            HashSet<string> requested = RequestedIds(ids);
            if (requested == null)
                return items;

            return items.Where(item => requested.Contains(identifier(item)) || requested.Contains(number(item).ToString())).ToList();
        }

        /***************************************************/

        // Whether any notional horizontal load is switched on for the combination, in either direction.
        private static bool HasNotionalLoads(ICombination combination)
        {
            Tristate x = combination.NhlX.ValueOrDefault(Tristate.None);
            Tristate y = combination.NhlY.ValueOrDefault(Tristate.None);
            return (x == Tristate.Positive || x == Tristate.Negative) || (y == Tristate.Positive || y == Tristate.Negative);
        }

        /***************************************************/
    }
}
