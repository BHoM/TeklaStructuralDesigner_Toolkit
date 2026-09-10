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
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Adapter Methods                ****/
        /***************************************************/

        // Called by the Pull method on the base adapter, once per requested Type.
        protected override IEnumerable<IBHoMObject> IRead(Type type, IList ids, ActionConfig actionConfig = null)
        {
            if (type == null)
            {
                Engine.Base.Compute.RecordError("No type was provided to read, so nothing was pulled from Tekla Structural Designer.");
                return new List<IBHoMObject>();
            }

            if (type == typeof(Bar))
                return ReadBars(ids).Cast<IBHoMObject>();

            if (type == typeof(Node))
                return ReadNodes(ids).Cast<IBHoMObject>();

            if (typeof(IResult).IsAssignableFrom(type))
            {
                // Results are requested through a result request rather than by type, and are served by the
                // ReadResults overloads. This is the standard BHoM message telling the caller to do that.
                Modules.Structure.ErrorMessages.ReadResultsError(type);
                return new List<IBHoMObject>();
            }

            TeklaStructuralDesignerPullConfig config = PullConfigOrDefault(actionConfig);

            if (type == typeof(Loadcase))
                return ReadLoadcases(ids).Cast<IBHoMObject>();

            if (type == typeof(LoadCombination))
                return ReadLoadCombinations(ids, config).Cast<IBHoMObject>();

            // ICase asked for on its own means both kinds of case, which is what a caller filtering on
            // the interface rather than on a concrete type is asking for.
            if (type == typeof(ICase))
                return ReadLoadcases(ids).Cast<IBHoMObject>().Concat(ReadLoadCombinations(ids, config).Cast<IBHoMObject>());

            // Loads are matched on assignability rather than equality, so that ILoad returns every
            // supported load and a concrete type returns just that one. The ids of a load request are
            // not meaningful - a load has no identity of its own in Tekla Structural Designer - so they
            // are not consulted here, unlike for Bars, Nodes and cases.
            if (typeof(ILoad).IsAssignableFrom(type))
            {
                if (ids != null && ids.Count > 0)
                    Engine.Base.Compute.RecordWarning("Loads have no identifier of their own in Tekla Structural Designer, so the ids on this request have been ignored. Filter the pulled loads by their Loadcase instead.");

                return ReadLoads(type, config).Cast<IBHoMObject>();
            }

            Engine.Base.Compute.RecordWarning($"Pulling objects of type {type.Name} is not supported by the Tekla Structural Designer adapter. This adapter reads Bars, Nodes, Loadcases, LoadCombinations, bar and nodal loads, and bar forces; use a BarResultRequest to pull bar forces.");
            return new List<IBHoMObject>();
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // Shared by every read that takes settings, so that a caller who passes the wrong kind of
        // ActionConfig is told once, in the same words, wherever it happened.
        private static TeklaStructuralDesignerPullConfig PullConfigOrDefault(ActionConfig actionConfig)
        {
            TeklaStructuralDesignerPullConfig config = actionConfig as TeklaStructuralDesignerPullConfig;
            if (config != null)
                return config;

            if (actionConfig != null)
                Engine.Base.Compute.RecordWarning("The supplied ActionConfig is not a TeklaStructuralDesignerPullConfig; default pull settings have been used instead.");

            return new TeklaStructuralDesignerPullConfig();
        }

        /***************************************************/
    }
}
