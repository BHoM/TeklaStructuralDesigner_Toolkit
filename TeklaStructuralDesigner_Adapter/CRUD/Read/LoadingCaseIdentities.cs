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
using System.Linq;
using BH.oM.Adapters.TeklaStructuralDesigner;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Loading case identity)        ****/
        /***************************************************/

        // Builds the list of loading cases results can be requested for, according to the pull
        // configuration. Only combinations are read by default: on a real model, individual loadcases
        // multiply the number of results returned without usually being what was wanted, and a bar
        // force schedule is normally built from combinations in any case.
        private List<TsdLoadingCaseIdentity> BuildLoadingCaseIdentities(TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            List<TsdLoadingCaseIdentity> identities = new List<TsdLoadingCaseIdentity>();

            if (config.IncludeCombinations)
            {
                List<ICombination> combinations;
                try
                {
                    combinations = Async.RunSync(ct => m_Model.GetCombinationsAsync(null, ct), timeoutSeconds, "reading load combinations").ToList();
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordError("Failed to read load combinations from Tekla Structural Designer. " + e.Message);
                    combinations = new List<ICombination>();
                }

                foreach (ICombination combination in combinations)
                    identities.Add(new TsdLoadingCaseIdentity(combination.Id, Identifier(combination.Name, combination.UserName, combination.Index), true));
            }

            if (config.IncludeLoadcases)
            {
                List<ILoadcase> loadcases;
                try
                {
                    loadcases = Async.RunSync(ct => m_Model.GetLoadcasesAsync(null, ct), timeoutSeconds, "reading loadcases").ToList();
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordError("Failed to read loadcases from Tekla Structural Designer. " + e.Message);
                    loadcases = new List<ILoadcase>();
                }

                foreach (ILoadcase loadcase in loadcases)
                    identities.Add(new TsdLoadingCaseIdentity(loadcase.Id, Identifier(loadcase.Name, loadcase.UserName, loadcase.Index), false));
            }

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
