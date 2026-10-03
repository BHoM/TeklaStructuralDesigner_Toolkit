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
using TSD.API.Remoting.Loading;
using TsdQuery = BH.Engine.Adapters.TeklaStructuralDesigner.Query;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Loading case identity)        ****/
        /***************************************************/

        // Every loading case results can be read for: each combination at each limit state it is
        // assessed for, then each loadcase - numbered and named exactly as the Loadcase and
        // LoadCombination objects a Pull returns, so that either can be handed back in a result request.
        // Which of these a request with no cases reads is decided in FilterCases, from the pull
        // configuration; a case named on the request is always found here.
        private List<TsdLoadingCaseIdentity> BuildLoadingCaseIdentities(int timeoutSeconds)
        {
            List<TsdLoadingCaseIdentity> identities = new List<TsdLoadingCaseIdentity>();

            List<ICombination> combinations = ReadTsdCombinations(timeoutSeconds);

            foreach (TeklaStructuralDesignerLimitState limitState in m_LimitStates)
            {
                foreach (ICombination combination in combinations)
                {
                    if (!combination.IsAssessedFor(limitState))
                        continue;

                    Guid resultsId = combination.ResultsId(limitState);
                    int number = TsdQuery.CaseNumber(combination.Number(), limitState);
                    if (resultsId == Guid.Empty || number < 0)
                        continue;

                    identities.Add(new TsdLoadingCaseIdentity(resultsId, number, TsdQuery.CombinationName(combination.Name, limitState), limitState));
                }
            }

            List<ILoadcase> loadcases = ReadTsdLoadcases(timeoutSeconds, null);
            Dictionary<Guid, int> loadcaseNumbers = LoadcaseNumbers(loadcases);
            foreach (ILoadcase loadcase in loadcases)
                identities.Add(new TsdLoadingCaseIdentity(loadcase.Id, loadcaseNumbers[loadcase.Id], Identifier(loadcase.Name, loadcase.UserName, loadcase.Index), null));

            return identities;
        }

        /***************************************************/

        // ResultCase should read as "the name or number of the loadcase" per BarResult's own
        // description - never the Guid, which TSD requires internally but which means nothing to a
        // human reading a pulled schedule.
        private static string Identifier(string name, TSD.API.Remoting.Common.Properties.IProperty<string> userName, int index)
        {
            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();

            string unwrappedUserName = userName.ValueOrDefault("");
            if (!string.IsNullOrWhiteSpace(unwrappedUserName))
                return unwrappedUserName.Trim();

            return index.ToString();
        }

        /***************************************************/
    }
}
