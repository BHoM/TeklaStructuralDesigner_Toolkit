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
using System.Linq;
using BH.oM.Adapters.TeklaStructuralDesigner;
using TSD.API.Remoting.Solver;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Solver access)                ****/
        /***************************************************/

        // The solver model of the analysis type on the pull configuration, which every result read
        // through the solver starts from. Null, with the reason recorded, if there is none.
        private TSD.API.Remoting.Solver.IModel SolverModel(TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            AnalysisType analysisType = config.AnalysisType.ToTeklaStructuralDesigner();

            TSD.API.Remoting.Solver.IModel solverModel;
            try
            {
                solverModel = Async.RunSync(ct => m_Model.GetSolverModelsAsync(new[] { analysisType }, ct), timeoutSeconds, "reading the solver model").FirstOrDefault();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read the solver model for " + config.AnalysisType + " from Tekla Structural Designer. " + e.Message);
                return null;
            }

            if (solverModel == null)
            {
                Engine.Base.Compute.RecordError("No solver model is available for " + config.AnalysisType +
                    " in Tekla Structural Designer. Run the analysis for that type, or choose a different AnalysisType on the pull configuration.");
            }

            return solverModel;
        }

        /***************************************************/

        // The 3D analysis results of a solver model. Null, with the reason recorded, if there are none.
        private IAnalysis3DResults Analysis3D(TSD.API.Remoting.Solver.IModel solverModel, TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            IAnalysisResults analysisResults;
            try
            {
                analysisResults = Async.RunSync(ct => solverModel.GetResultsAsync(ct), timeoutSeconds, "reading analysis results");
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read analysis results for " + config.AnalysisType + " from Tekla Structural Designer. " + e.Message);
                return null;
            }

            if (analysisResults == null)
            {
                Engine.Base.Compute.RecordError("No analysis results are available for " + config.AnalysisType +
                    " in Tekla Structural Designer. Run the analysis, then pull again.");
                return null;
            }

            IAnalysis3DResults analysis3D;
            try
            {
                analysis3D = Async.RunSync(ct => analysisResults.GetAnalysis3DAsync(ct), timeoutSeconds, "reading 3D analysis results");
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read 3D analysis results for " + config.AnalysisType + " from Tekla Structural Designer. " + e.Message);
                return null;
            }

            if (analysis3D == null)
                Engine.Base.Compute.RecordError("No 3D analysis results are available for " + config.AnalysisType + " in Tekla Structural Designer.");

            return analysis3D;
        }

        /***************************************************/
    }
}
