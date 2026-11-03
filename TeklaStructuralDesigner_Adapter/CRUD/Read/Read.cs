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
using BH.oM.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Analytical.Results;
using BH.oM.Base;
using BH.oM.Spatial.SettingOut;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Adapter Methods                           ****/
        /***************************************************/

        // Called by the base Pull once per requested type.
        protected override IEnumerable<IBHoMObject> IRead(Type type, IList ids, ActionConfig actionConfig = null)
        {
            if (type == null)
            {
                Engine.Base.Compute.RecordError("No type was provided to read, so nothing was pulled from Tekla Structural Designer.");
                return new List<IBHoMObject>();
            }

            if (m_Model == null)
            {
                Engine.Base.Compute.RecordWarning(ErrorMessages.NotConnected());
                return new List<IBHoMObject>();
            }

            TeklaStructuralDesignerPullConfig config = PullConfigOrDefault(actionConfig);

            if (typeof(IResult).IsAssignableFrom(type))
            {
                // Results are read through a result request.
                Modules.Structure.ErrorMessages.ReadResultsError(type);
                return new List<IBHoMObject>();
            }

            Engine.Base.Compute.RecordWarning($"Pulling objects of type {type.Name} is not supported by the Tekla Structural Designer adapter. This adapter reads Bars, Nodes, Panels, Levels, Grids, Loadcases, LoadCombinations, bar, nodal, area, contour and line loads, bar forces, node reactions and node displacements; use a BarResultRequest or a NodeResultRequest to pull results.");
            return new List<IBHoMObject>();
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // A plain ActionConfig, as the BHoM UIs pass by default, silently means defaults.
        private static TeklaStructuralDesignerPullConfig PullConfigOrDefault(ActionConfig actionConfig)
        {
            TeklaStructuralDesignerPullConfig config = actionConfig as TeklaStructuralDesignerPullConfig;
            if (config != null)
                return config;

            if (actionConfig != null && actionConfig.GetType() != typeof(ActionConfig))
                Engine.Base.Compute.RecordWarning("The supplied " + actionConfig.GetType().Name + " is not a TeklaStructuralDesignerPullConfig; default pull settings have been used instead.");

            return new TeklaStructuralDesignerPullConfig();
        }

        /***************************************************/

        // The ids as a case insensitive set of trimmed strings, or null, meaning everything, if none.
        private static HashSet<string> RequestedIds(IList ids)
        {
            if (ids == null || ids.Count == 0)
                return null;

            return new HashSet<string>(ids.Cast<object>().Where(id => id != null).Select(id => id.ToString().Trim()), StringComparer.OrdinalIgnoreCase);
        }

        /***************************************************/
    }
}
