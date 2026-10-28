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
using BH.oM.Structure.Results;
using TSD.API.Remoting.Loading;
using TsdAnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // One call per span end per case: the proven route, but far slower than the solver route on a
        // large model, so a large request is warned about.
        private List<BarForce> BarForcesFromSpanEnds(List<TsdSpanIdentity> spans, List<TsdLoadingCaseIdentity> cases, TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            List<BarForce> results = new List<BarForce>();

            TsdAnalysisType analysisType = config.AnalysisType.ToTsd();
            LoadingResultType loadingResultType = config.LoadingResultType.ToTsd();

            long callCount = (long)spans.Count * 2 * cases.Count;
            if (callCount > 20000)
            {
                Engine.Base.Compute.RecordWarning("The per span end route requires " + callCount +
                    " individual calls to Tekla Structural Designer for this request, which will be slow. Consider TeklaStructuralDesignerBarForceSource.Solver or Auto instead.");
            }

            int failures = 0;
            string firstFailure = null;

            foreach (TsdSpanIdentity span in spans)
            {
                foreach (TsdLoadingCaseIdentity loadingCase in cases)
                {
                    foreach (int end in new[] { 0, 1 })
                    {
                        string failure;
                        IForce3DLocal force = ReadSpanEndForce(span, end, loadingCase, analysisType, loadingResultType, timeoutSeconds, out failure);

                        if (failure != null)
                        {
                            failures++;
                            firstFailure = firstFailure ?? failure;
                        }
                        else if (force != null)
                        {
                            results.Add(force.FromTsdBarForce(span.ObjectId, loadingCase.Number, end, 2, end == 0, span.IsVertical(), config.SwapMajorMinorAxes));
                        }
                    }
                }
            }

            if (failures > 0)
                Engine.Base.Compute.RecordWarning(failures + " span/case end force read(s) failed and have been skipped. The first failure was: " + firstFailure);

            return results;
        }

        /***************************************************/

        // The force at the start (0) or end (1) of a span, or null with the reason in failure.
        private IForce3DLocal ReadSpanEndForce(TsdSpanIdentity span, int end, TsdLoadingCaseIdentity loadingCase, TsdAnalysisType analysisType, LoadingResultType loadingResultType, int timeoutSeconds, out string failure)
        {
            IForce3DLocal force;
            Async.TryRunSync(
                    ct => span.Span.GetEndForceAsync(end, analysisType, loadingCase.Id, loadingResultType, ct),
                    timeoutSeconds, "reading the " + (end == 0 ? "start" : "end") + " force of span '" + span.ObjectId + "'", out force, out failure);
            return force;
        }

        /***************************************************/
    }
}
