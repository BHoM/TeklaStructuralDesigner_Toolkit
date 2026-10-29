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
using BH.oM.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Analytical.Results;
using BH.oM.Structure.Requests;
using BH.oM.Structure.Results;
using TSD.API.Remoting.Loading;
using TsdAnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        public IEnumerable<IResult> ReadResults(BarResultRequest request, ActionConfig actionConfig = null)
        {
            if (m_Model == null)
            {
                Engine.Base.Compute.RecordWarning(ErrorMessages.NotConnected());
                return new List<BarForce>();
            }

            if (request == null)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoRequest());
                return new List<BarForce>();
            }

            if (request.ResultType != BarResultType.BarForce)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.UnsupportedResultType(request.ResultType.ToString(), "A BarResultRequest reads bar forces only."));
                return new List<BarForce>();
            }

            if (request.DivisionType == DivisionType.ExtremeValues)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.ExtremeValuesUnsupported());
                return new List<BarForce>();
            }

            if (request.Modes != null && request.Modes.Count > 0)
                Engine.Base.Compute.RecordWarning("Modal results are not supported by this adapter; the requested Modes have been ignored.");

            TeklaStructuralDesignerPullConfig config = PullConfigOrDefault(actionConfig);

            int timeoutSeconds = TeklaStructuralDesignerConfig.TimeoutSeconds;

            List<TsdSpanIdentity> allSpans = BuildSpanIdentities(timeoutSeconds);
            if (allSpans.Count == 0)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoSpans());
                return new List<BarForce>();
            }

            List<TsdLoadingCaseIdentity> allCases = BuildLoadingCaseIdentities(timeoutSeconds);
            if (allCases.Count == 0)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoCases());
                return new List<BarForce>();
            }

            List<TsdSpanIdentity> spans = FilterSpans(allSpans, request.ObjectIds);
            if (spans.Count == 0)
                return new List<BarForce>();       // FilterSpans has already recorded why.

            List<TsdLoadingCaseIdentity> cases = FilterCases(allCases, request.Cases, config);
            if (cases.Count == 0)
                return new List<BarForce>();       // FilterCases has already recorded why.

            bool endsOnly = request.Divisions <= 2;
            if (!endsOnly)
            {
                if (config.BarForceSource == TeklaStructuralDesignerBarForceSource.SpanEnds)
                    Engine.Base.Compute.RecordWarning(ErrorMessages.DivisionsIgnoredBySpanEnds(request.Divisions));
                else
                    Engine.Base.Compute.RecordWarning(ErrorMessages.DivisionsNotEvenlyDistributed(request.Divisions));
            }

            List<BarForce> results;
            switch (config.BarForceSource)
            {
                case TeklaStructuralDesignerBarForceSource.SpanEnds:
                    results = BarForcesFromSpanEnds(spans, cases, config, timeoutSeconds);
                    break;
                case TeklaStructuralDesignerBarForceSource.Solver:
                    results = BarForcesFromSolver(spans, cases, config, endsOnly, timeoutSeconds);
                    break;
                default:
                    results = BarForcesAuto(spans, cases, config, endsOnly, timeoutSeconds);
                    break;
            }

            if (results.Count == 0)
            {
                Engine.Base.Compute.RecordWarning("No bar forces were returned for the requested spans and cases. Check that the model has been analysed for " +
                    config.AnalysisType + ".");
            }

            return results;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // The batched solver route, spot checked against one span end force from the per span end
        // route; if they disagree, the per span end route is used for the whole pull.
        private List<BarForce> BarForcesAuto(List<TsdSpanIdentity> spans, List<TsdLoadingCaseIdentity> cases, TeklaStructuralDesignerPullConfig config, bool endsOnly, int timeoutSeconds)
        {
            List<BarForce> solverResults = BarForcesFromSolver(spans, cases, config, endsOnly, timeoutSeconds);

            if (!SolverResultsAreTrustworthy(solverResults, spans, cases, config, timeoutSeconds))
            {
                Engine.Base.Compute.RecordWarning(
                    "The batched solver results did not match a per span end spot check; falling back to the slower but proven per span end route for this pull." +
                    (endsOnly ? "" : " That route reads end forces only, so only the two end forces of each bar have been returned."));
                return BarForcesFromSpanEnds(spans, cases, config, timeoutSeconds);
            }

            return solverResults;
        }

        /***************************************************/

        private bool SolverResultsAreTrustworthy(List<BarForce> solverResults, List<TsdSpanIdentity> spans, List<TsdLoadingCaseIdentity> cases, TeklaStructuralDesignerPullConfig config, int timeoutSeconds)
        {
            // No results usually means an unsolved case, already reported; the slow route would not help.
            if (solverResults.Count == 0)
                return true;

            TsdSpanIdentity checkSpan = spans.FirstOrDefault(s => s.Span != null);
            TsdLoadingCaseIdentity checkCase = cases.FirstOrDefault();
            if (checkSpan == null || checkCase == null)
                return true;

            BarForce solverStart = solverResults.FirstOrDefault(r =>
                Equals(r.ObjectId, checkSpan.ObjectId) && Equals(r.ResultCase, checkCase.Number) && Math.Abs(r.Position) < 1e-6);
            if (solverStart == null)
                return true;

            string failure;
            IForce3DLocal spanEndForce = ReadSpanEndForce(checkSpan, 0, checkCase, config.AnalysisType.ToTsd(), config.LoadingResultType.ToTsd(), timeoutSeconds, out failure);

            if (failure != null)
            {
                Engine.Base.Compute.RecordWarning("The bar forces from the solver route could not be cross-checked against a span end force, so they have been trusted unverified. " + failure);
                return true;
            }

            if (spanEndForce == null)
                return true;

            BarForce reference = spanEndForce.FromTsdBarForce(checkSpan.ObjectId, checkCase.Number, 0.0, 2, true, checkSpan.IsVertical(), config.SwapMajorMinorAxes);

            const double tolerance = 1.0;       // N, N.m: a sanity check, not a precision comparison.
            return Close(solverStart.FX, reference.FX, tolerance) && Close(solverStart.FY, reference.FY, tolerance) &&
                   Close(solverStart.FZ, reference.FZ, tolerance) && Close(solverStart.MX, reference.MX, tolerance) &&
                   Close(solverStart.MY, reference.MY, tolerance) && Close(solverStart.MZ, reference.MZ, tolerance);
        }

        /***************************************************/

        private static bool Close(double a, double b, double tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }

        /***************************************************/
    }
}
