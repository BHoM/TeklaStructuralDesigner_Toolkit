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

using System.Collections.Generic;
using System.Linq;
using BH.oM.Adapters.TeklaStructuralDesigner;
using TSD.API.Remoting.Solver;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // The solver model for the configured analysis type. Null, with the reason recorded, if none.
        private IModel SolverModel(TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            AnalysisType analysisType = config.AnalysisType.ToTsd();

            IEnumerable<IModel> solverModels;
            if (!Async.TryRunSync(ct => m_Model.GetSolverModelsAsync(new[] { analysisType }, ct), timeoutSeconds, "reading the solver model for " + config.AnalysisType, "no results have been read", out solverModels, true))
                return null;

            IModel solverModel = solverModels.FirstOrDefault();
            if (solverModel == null)
            {
                Engine.Base.Compute.RecordError("No solver model is available for " + config.AnalysisType +
                    " in Tekla Structural Designer. Run the analysis for that type, or choose a different AnalysisType on the pull configuration.");
            }

            return solverModel;
        }

        /***************************************************/

        // The 3D analysis results of a solver model. Null, with the reason recorded, if none.
        private IAnalysis3DResults Analysis3D(IModel solverModel, TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            IAnalysisResults analysisResults;
            if (!Async.TryRunSync(ct => solverModel.GetResultsAsync(ct), timeoutSeconds, "reading analysis results for " + config.AnalysisType, "no results have been read", out analysisResults, true))
                return null;

            if (analysisResults == null)
            {
                Engine.Base.Compute.RecordError("No analysis results are available for " + config.AnalysisType +
                    " in Tekla Structural Designer. Run the analysis, then pull again.");
                return null;
            }

            IAnalysis3DResults analysis3D;
            if (!Async.TryRunSync(ct => analysisResults.GetAnalysis3DAsync(ct), timeoutSeconds, "reading 3D analysis results for " + config.AnalysisType, "no results have been read", out analysis3D, true))
                return null;

            if (analysis3D == null)
                Engine.Base.Compute.RecordError("No 3D analysis results are available for " + config.AnalysisType + " in Tekla Structural Designer.");

            return analysis3D;
        }

        /***************************************************/
    }
}
