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
        /****            Private Methods                ****/
        /****            (Per span end bar forces)      ****/
        /***************************************************/

        // One call per span, per end, per requested loading case - the route proven correct against a
        // real model before this toolkit existed, and the fallback used when the batched solver route
        // disagrees with it, or is chosen explicitly. On a model of any size this is many more calls
        // than BarForcesFromSolver: a 4,000 span, 60 combination model is 480,000 calls here against
        // roughly a few dozen there. Warn loudly rather than let a caller discover that by waiting.
        private List<BarForce> BarForcesFromSpanEnds(List<TsdSpanIdentity> spans, List<TsdLoadingCaseIdentity> cases, TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            List<BarForce> results = new List<BarForce>();

            TsdAnalysisType analysisType = config.AnalysisType.ToTeklaStructuralDesigner();
            LoadingResultType loadingResultType = config.LoadingResultType.ToTeklaStructuralDesigner();

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
                            results.Add(force.ToBHoM(span.ObjectId, loadingCase.Number, end, 2, config.SwapMajorMinorAxes));
                        }
                    }
                }
            }

            if (failures > 0)
                Engine.Base.Compute.RecordWarning(failures + " span/case end force read(s) failed and have been skipped. The first failure was: " + firstFailure);

            return results;
        }

        /***************************************************/

        // The force at one end of a span (0 for its start, 1 for its end) under one loading case, read
        // straight off the span. Null, with failure set to the reason, if the read threw.
        private IForce3DLocal ReadSpanEndForce(TsdSpanIdentity span, int end, TsdLoadingCaseIdentity loadingCase, TsdAnalysisType analysisType, LoadingResultType loadingResultType, int timeoutSeconds, out string failure)
        {
            failure = null;

            try
            {
                return Async.RunSync(
                    ct => span.Span.GetEndForceAsync(end, analysisType, loadingCase.Id, loadingResultType, ct),
                    timeoutSeconds, "reading the " + (end == 0 ? "start" : "end") + " force of span '" + span.ObjectId + "'");
            }
            catch (Exception e)
            {
                failure = e.Message;
                return null;
            }
        }

        /***************************************************/
    }
}
