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
        /**** Private Fields                            ****/
        /***************************************************/

        // All Strength, then all Service, so combinations are listed in case number order.
        private static readonly TeklaStructuralDesignerLimitState[] m_LimitStates = { TeklaStructuralDesignerLimitState.Strength, TeklaStructuralDesignerLimitState.Service };

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Every loadcase as a Loadcase. Unlike results, not filtered by the pull configuration.
        private Dictionary<Guid, Loadcase> LoadcasesById(int timeoutSeconds)
        {
            return BHoMLoadcases(ReadTsdLoadcases(timeoutSeconds, null));
        }

        /***************************************************/

        // Keyed by Guid, so a loadcase is built the same way on its own, in a load or in a combination.
        private static Dictionary<Guid, Loadcase> BHoMLoadcases(List<ILoadcase> loadcases)
        {
            Dictionary<Guid, Loadcase> result = new Dictionary<Guid, Loadcase>();
            Dictionary<Guid, int> numberById = LoadcaseNumbers(loadcases);

            foreach (ILoadcase loadcase in loadcases)
            {
                if (!result.ContainsKey(loadcase.Id))
                    result[loadcase.Id] = loadcase.FromTsdLoadcase(Identifier(loadcase.Name, loadcase.UserName, loadcase.Index), numberById[loadcase.Id]);
            }

            return result;
        }

        /***************************************************/

        // Every loadcase except the unit loadcases (see Helpers.IsUnitLoadcase), so Loadcase, load and
        // result pulls agree on which exist. Empty, with an error, if the read fails.
        private List<ILoadcase> ReadTsdLoadcases(int timeoutSeconds, string consequence)
        {
            IEnumerable<ILoadcase> loadcases;
            if (!Async.TryRunSync(ct => m_Model.GetLoadcasesAsync(null, ct), timeoutSeconds, "reading loadcases", consequence ?? "no loadcases have been read", out loadcases, true))
                return new List<ILoadcase>();

            return loadcases.Where(l => !l.IsUnitLoadcase()).ToList();
        }

        /***************************************************/

        // Every combination; empty, with an error, if the read fails.
        private List<ICombination> ReadTsdCombinations(int timeoutSeconds)
        {
            IEnumerable<ICombination> combinations;
            if (!Async.TryRunSync(ct => m_Model.GetCombinationsAsync(null, ct), timeoutSeconds, "reading load combinations", "no load combinations have been read", out combinations, true))
                return new List<ICombination>();

            return combinations.ToList();
        }

        /***************************************************/

        // The number shown in Tekla Structural Designer where it is positive and unique, as BHoM needs;
        // otherwise the next free number, with a warning. Shared by every loadcase read.
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

            // A LoadCombination holds Loadcase objects, so these are read first.
            Dictionary<Guid, Loadcase> loadcaseById = LoadcasesById(timeout);

            List<ICombination> combinations = ReadTsdCombinations(timeout);

            // One LoadCombination per limit state the combination is assessed at.
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
                    LoadCombination converted = combination.FromTsdLoadCombination(limitState, loadcaseById, out unresolved);
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

            // Strength results include notional horizontal loads, which are not a loadcase.
            if (withNotionalLoads > 0)
            {
                Engine.Base.Compute.RecordNote(withNotionalLoads + " Strength combination(s) include notional horizontal loads in Tekla Structural Designer. " +
                    "They are not a loadcase, so the pulled LoadCombination does not carry them: its factored loadcases will not add up to the Strength results Tekla Structural Designer reports for it. " +
                    "Results pulled from Tekla Structural Designer do include them.");
            }

            return result;
        }

        /***************************************************/

        // Cases are requested by Name or Number, never by Guid.
        private static List<T> Filter<T>(List<T> items, IList ids, Func<T, string> identifier, Func<T, int> number)
        {
            HashSet<string> requested = RequestedIds(ids);
            if (requested == null)
                return items;

            return items.Where(item => requested.Contains(identifier(item)) || requested.Contains(number(item).ToString())).ToList();
        }

        /***************************************************/

        // Whether notional horizontal loads are on in either direction.
        private static bool HasNotionalLoads(ICombination combination)
        {
            Tristate x = combination.NhlX.ValueOrDefault(Tristate.None);
            Tristate y = combination.NhlY.ValueOrDefault(Tristate.None);
            return (x == Tristate.Positive || x == Tristate.Negative) || (y == Tristate.Positive || y == Tristate.Negative);
        }

        /***************************************************/
    }
}
