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
using BH.oM.Structure.Results;
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Batched solver bar forces)    ****/
        /***************************************************/

        // One call per requested loading case returns the end forces of every solver element in the
        // model at once (Analysis3DResults.GetEndForcesAsync with indices = null). On a model with
        // thousands of spans and dozens of combinations that is the difference between a few dozen
        // calls and hundreds of thousands, which is why this is the default route
        // (TeklaStructuralDesignerBarForceSource.Solver / Auto).
        //
        // The cost is granularity: results land at solver element boundaries, which can be finer than
        // a span (a span may be more than one solver element) but are not guaranteed to align with
        // BHoM's own station numbering for anything except the two ends. Interior stations from this
        // route are therefore reported at the position the solver actually computed, not at evenly
        // spaced fractions.
        private List<BarForce> BarForcesFromSolver(List<TsdSpanIdentity> spans, List<TsdLoadingCaseIdentity> cases, TeklaStructuralDesignerPullConfig config, bool endsOnly, int timeoutSeconds)
        {
            List<BarForce> results = new List<BarForce>();

            TSD.API.Remoting.Solver.AnalysisType analysisType = config.AnalysisType.ToTeklaStructuralDesigner();
            LoadingResultType loadingResultType = config.LoadingResultType.ToTeklaStructuralDesigner();

            List<TSD.API.Remoting.Solver.IModel> solverModels;
            try
            {
                solverModels = Async.RunSync(ct => m_Model.GetSolverModelsAsync(new[] { analysisType }, ct), timeoutSeconds, "reading the solver model").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read the solver model for " + config.AnalysisType + " from Tekla Structural Designer. " + e.Message);
                return results;
            }

            TSD.API.Remoting.Solver.IModel solverModel = solverModels.FirstOrDefault();
            if (solverModel == null)
            {
                Engine.Base.Compute.RecordError("No solver model is available for " + config.AnalysisType +
                    " in Tekla Structural Designer. Run the analysis for that type, or choose a different AnalysisType on the pull configuration.");
                return results;
            }

            List<IElement1D> elements;
            try
            {
                elements = Async.RunSync(ct => solverModel.GetElements1DAsync(null, ct), timeoutSeconds, "reading solver elements").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read solver elements from Tekla Structural Designer. " + e.Message);
                return results;
            }

            // Group solver elements by the span they belong to, in along-span order. Ascending solver
            // Index is assumed to follow the span from start to end; the Auto cross check in
            // BarResults.cs verifies this against the proven per span end route before it is trusted.
            Dictionary<Guid, List<IElement1D>> elementsBySpanId = new Dictionary<Guid, List<IElement1D>>();
            foreach (IElement1D element in elements)
            {
                SubEntityInfo source = element.SourceSubEntityInfo;
                if (source.Type != SubEntityType.Span || source.Id == Guid.Empty)
                    continue;

                List<IElement1D> list;
                if (!elementsBySpanId.TryGetValue(source.Id, out list))
                {
                    list = new List<IElement1D>();
                    elementsBySpanId[source.Id] = list;
                }
                list.Add(element);
            }

            foreach (List<IElement1D> list in elementsBySpanId.Values)
                list.Sort((a, b) => a.Index.CompareTo(b.Index));

            Dictionary<Guid, TsdSpanIdentity> spanBySpanId = new Dictionary<Guid, TsdSpanIdentity>();
            foreach (TsdSpanIdentity span in spans)
            {
                if (!spanBySpanId.ContainsKey(span.SpanId))
                    spanBySpanId[span.SpanId] = span;
            }

            IAnalysisResults analysisResults;
            try
            {
                analysisResults = Async.RunSync(ct => solverModel.GetResultsAsync(ct), timeoutSeconds, "reading analysis results");
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read analysis results for " + config.AnalysisType + " from Tekla Structural Designer. " + e.Message);
                return results;
            }

            if (analysisResults == null)
            {
                Engine.Base.Compute.RecordError("No analysis results are available for " + config.AnalysisType +
                    " in Tekla Structural Designer. Run the analysis, then pull again.");
                return results;
            }

            IAnalysis3DResults analysis3D;
            try
            {
                analysis3D = Async.RunSync(ct => analysisResults.GetAnalysis3DAsync(ct), timeoutSeconds, "reading 3D analysis results");
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read 3D analysis results for " + config.AnalysisType + " from Tekla Structural Designer. " + e.Message);
                return results;
            }

            if (analysis3D == null)
            {
                Engine.Base.Compute.RecordError("No 3D analysis results are available for " + config.AnalysisType + " in Tekla Structural Designer.");
                return results;
            }

            List<TsdLoadingCaseIdentity> solvedCases = SolvedCasesOnly(analysis3D, cases, config.AnalysisType.ToString(), timeoutSeconds);
            if (solvedCases.Count == 0)
                return results;

            foreach (TsdLoadingCaseIdentity loadingCase in solvedCases)
            {
                IEnumerable<IElementEndForces> endForces;
                try
                {
                    endForces = Async.RunSync(
                        ct => analysis3D.GetEndForcesAsync(loadingCase.Id, loadingResultType, null, ct),
                        timeoutSeconds, "reading end forces for case '" + loadingCase.Identifier + "'");
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning("Failed to read end forces for case '" + loadingCase.Identifier + "'; it has been skipped. " + e.Message);
                    continue;
                }

                Dictionary<int, IElementEndForces> forceByElementIndex = endForces.ToDictionary(f => f.ElementIndex);

                foreach (KeyValuePair<Guid, List<IElement1D>> spanElements in elementsBySpanId)
                {
                    TsdSpanIdentity span;
                    if (!spanBySpanId.TryGetValue(spanElements.Key, out span))
                        continue; // Not one of the spans this read is scoped to.

                    AddSpanResults(results, spanElements.Value, forceByElementIndex, span, loadingCase.Identifier, config.SwapMajorMinorAxes, endsOnly);
                }
            }

            return results;
        }

        /***************************************************/

        private static void AddSpanResults(
            List<BarForce> results,
            List<IElement1D> spanElements,
            Dictionary<int, IElementEndForces> forceByElementIndex,
            TsdSpanIdentity span,
            string resultCase,
            bool swapMajorMinorAxes,
            bool endsOnly)
        {
            if (spanElements.Count == 0)
                return;

            if (endsOnly)
            {
                IElementEndForces firstForces, lastForces;
                if (forceByElementIndex.TryGetValue(spanElements[0].Index, out firstForces))
                    results.Add(firstForces.StartForce.ToBHoM(span.ObjectId, resultCase, 0.0, 2, swapMajorMinorAxes));
                if (forceByElementIndex.TryGetValue(spanElements[spanElements.Count - 1].Index, out lastForces))
                    results.Add(lastForces.EndForce.ToBHoM(span.ObjectId, resultCase, 1.0, 2, swapMajorMinorAxes));
                return;
            }

            // n solver elements along a span produce n+1 stations: each element's start, plus the last
            // element's end.
            int stationCount = spanElements.Count + 1;

            for (int i = 0; i < spanElements.Count; i++)
            {
                IElementEndForces forces;
                if (!forceByElementIndex.TryGetValue(spanElements[i].Index, out forces))
                    continue;

                double position = (double)i / (stationCount - 1);
                results.Add(forces.StartForce.ToBHoM(span.ObjectId, resultCase, position, stationCount, swapMajorMinorAxes));

                if (i == spanElements.Count - 1)
                    results.Add(forces.EndForce.ToBHoM(span.ObjectId, resultCase, 1.0, stationCount, swapMajorMinorAxes));
            }
        }

        /***************************************************/

        // Pre-flight validation: names which requested cases have not been solved for this analysis
        // type, rather than the ported tool's behaviour of throwing a bare exception when none have.
        private List<TsdLoadingCaseIdentity> SolvedCasesOnly(IAnalysis3DResults analysis3D, List<TsdLoadingCaseIdentity> cases, string analysisTypeDescription, int timeoutSeconds)
        {
            HashSet<Guid> solvedLoadingIds;
            try
            {
                solvedLoadingIds = new HashSet<Guid>(Async.RunSync(ct => analysis3D.GetSolvedLoadingIdsAsync(ct), timeoutSeconds, "reading solved loading cases"));
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordWarning("Failed to read which loading cases have been solved for " + analysisTypeDescription + "; proceeding without that check. " + e.Message);
                return cases;
            }

            List<TsdLoadingCaseIdentity> solved = new List<TsdLoadingCaseIdentity>();
            List<TsdLoadingCaseIdentity> unsolved = new List<TsdLoadingCaseIdentity>();

            foreach (TsdLoadingCaseIdentity c in cases)
            {
                if (solvedLoadingIds.Contains(c.Id))
                    solved.Add(c);
                else
                    unsolved.Add(c);
            }

            if (unsolved.Count > 0)
            {
                string names = string.Join(", ", unsolved.Take(10).Select(c => c.Identifier));
                if (unsolved.Count > 10)
                    names += ", ...";

                Engine.Base.Compute.RecordWarning(unsolved.Count + " requested loading case(s) have not been solved for " +
                    analysisTypeDescription + " and have been skipped: " + names);
            }

            if (solved.Count == 0)
                Engine.Base.Compute.RecordError(ErrorMessages.NoSolvedCases(analysisTypeDescription));

            return solved;
        }

        /***************************************************/
    }
}
