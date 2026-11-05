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
using BH.Engine.Geometry;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Results;
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using Point = BH.oM.Geometry.Point;
using Vector = BH.oM.Geometry.Vector;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // One call per case for the end forces of every solver element, far fewer calls than the per
        // span end route. Spans of several solver elements report their inside stations at evenly
        // spaced fractions, as the API gives elements no geometry; the ends are always exact.
        private List<BarForce> BarForcesFromSolver(List<TsdSpanIdentity> spans, List<TsdLoadingCaseIdentity> cases, TeklaStructuralDesignerPullConfig config, bool endsOnly, int timeoutSeconds)
        {
            List<BarForce> results = new List<BarForce>();

            LoadingResultType loadingResultType = config.LoadingResultType.ToTsd();

            TSD.API.Remoting.Solver.IModel solverModel = SolverModel(config, timeoutSeconds);
            if (solverModel == null)
                return results;

            IEnumerable<IElement1D> elements;
            if (!Async.TryRunSync(ct => solverModel.GetElements1DAsync(null, ct), timeoutSeconds, "reading solver elements", "no bar forces have been read", out elements, true))
                return results;

            // Solver elements are grouped by member and span index, as the span and member Guids on
            // SourceSubEntityInfo are empty in Tekla Structural Designer 2026.
            Dictionary<Tuple<int, int>, List<IElement1D>> elementsBySpan = new Dictionary<Tuple<int, int>, List<IElement1D>>();
            foreach (IElement1D element in elements)
            {
                SubEntityInfo source = element.SourceSubEntityInfo;
                if (source.Type != SubEntityType.Span || source.EntityType != EntityType.Member)
                    continue;

                Tuple<int, int> key = Tuple.Create(source.EntityIndex, source.Index);
                List<IElement1D> list;
                if (!elementsBySpan.TryGetValue(key, out list))
                {
                    list = new List<IElement1D>();
                    elementsBySpan[key] = list;
                }
                list.Add(element);
            }

            Dictionary<Tuple<int, int>, TsdSpanIdentity> spanByKey = new Dictionary<Tuple<int, int>, TsdSpanIdentity>();
            foreach (TsdSpanIdentity span in spans)
            {
                Tuple<int, int> key = Tuple.Create(span.MemberIndex, span.SpanIndex);
                if (!spanByKey.ContainsKey(key))
                    spanByKey[key] = span;
            }

            OrderAlongSpans(solverModel, elementsBySpan, spanByKey, timeoutSeconds);

            IAnalysis3DResults analysis3D = Analysis3D(solverModel, config, timeoutSeconds);
            if (analysis3D == null)
                return results;

            List<TsdLoadingCaseIdentity> solvedCases = SolvedCasesOnly(analysis3D, cases, config.AnalysisType.ToString(), timeoutSeconds);
            if (solvedCases.Count == 0)
                return results;

            foreach (TsdLoadingCaseIdentity loadingCase in solvedCases)
            {
                IEnumerable<IElementEndForces> endForces;
                if (!Async.TryRunSync(
                        ct => analysis3D.GetEndForcesAsync(loadingCase.Id, loadingResultType, null, ct),
                        timeoutSeconds, "reading end forces for case '" + loadingCase + "'", "it has been skipped", out endForces))
                    continue;

                Dictionary<int, IElementEndForces> forceByElementIndex = endForces
                    .GroupBy(f => f.ElementIndex)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (KeyValuePair<Tuple<int, int>, List<IElement1D>> spanElements in elementsBySpan)
                {
                    TsdSpanIdentity span;
                    if (!spanByKey.TryGetValue(spanElements.Key, out span))
                        continue;

                    AddSpanResults(results, spanElements.Value, forceByElementIndex, span, loadingCase.Number, endsOnly);
                }
            }

            return results;
        }

        /***************************************************/

        // Orders each span's solver elements from its start, by where the mean of each element's nodes
        // lies along the span: solver Index order is not along the span. Falls back to Index order,
        // with a warning, where the nodes cannot be read.
        private static void OrderAlongSpans(
            TSD.API.Remoting.Solver.IModel solverModel,
            Dictionary<Tuple<int, int>, List<IElement1D>> elementsBySpan,
            Dictionary<Tuple<int, int>, TsdSpanIdentity> spanByKey,
            int timeoutSeconds)
        {
            foreach (List<IElement1D> list in elementsBySpan.Values)
                list.Sort((a, b) => a.Index.CompareTo(b.Index));

            if (!elementsBySpan.Any(kvp => kvp.Value.Count > 1 && spanByKey.ContainsKey(kvp.Key)))
                return;

            IEnumerable<INode> nodes;
            if (!Async.TryRunSync(ct => solverModel.GetNodesAsync(null, ct), timeoutSeconds, "reading solver nodes", "the stations inside spans of more than one solver element are in solver order, which may not run from the start of the span to its end. The two ends of each span are unaffected", out nodes))
                return;

            Dictionary<int, List<Point>> pointsByElement = new Dictionary<int, List<Point>>();
            foreach (INode node in nodes)
            {
                if (node.Element1dIndices == null)
                    continue;

                Point position = node.Coordinates.FromTsdPoint();
                foreach (int elementIndex in node.Element1dIndices)
                {
                    List<Point> points;
                    if (!pointsByElement.TryGetValue(elementIndex, out points))
                        pointsByElement[elementIndex] = points = new List<Point>();

                    points.Add(position);
                }
            }

            int unordered = 0;
            foreach (KeyValuePair<Tuple<int, int>, List<IElement1D>> spanElements in elementsBySpan)
            {
                TsdSpanIdentity span;
                if (spanElements.Value.Count < 2 || !spanByKey.TryGetValue(spanElements.Key, out span) || span.StartPosition == null || span.EndPosition == null)
                    continue;

                if (spanElements.Value.Any(e => !pointsByElement.ContainsKey(e.Index) || pointsByElement[e.Index].Count < 2))
                {
                    unordered++;
                    continue;
                }

                Vector along = span.EndPosition - span.StartPosition;
                spanElements.Value.Sort((a, b) => DistanceAlong(pointsByElement[a.Index], span.StartPosition, along).CompareTo(DistanceAlong(pointsByElement[b.Index], span.StartPosition, along)));
            }

            if (unordered > 0)
                Engine.Base.Compute.RecordWarning(unordered + " span(s) of more than one solver element could not be put in order along the span, so their inside stations are in solver order, which may not run from the start of the span to its end. The two ends of each span are unaffected.");
        }

        /***************************************************/

        // How far along a span the mean of a solver element's nodes lies.
        private static double DistanceAlong(List<Point> elementPoints, Point spanStart, Vector along)
        {
            return (elementPoints.Average() - spanStart).DotProduct(along);
        }

        /***************************************************/

        private static void AddSpanResults(
            List<BarForce> results,
            List<IElement1D> spanElements,
            Dictionary<int, IElementEndForces> forceByElementIndex,
            TsdSpanIdentity span,
            int resultCase,
            bool endsOnly)
        {
            if (spanElements.Count == 0)
                return;

            bool isVertical = span.IsVertical();

            if (endsOnly)
            {
                IElementEndForces firstForces, lastForces;
                if (forceByElementIndex.TryGetValue(spanElements[0].Index, out firstForces))
                    results.Add(firstForces.StartForce.FromTsdBarForce(span.ObjectId, resultCase, 0.0, 2, true, isVertical));
                if (forceByElementIndex.TryGetValue(spanElements[spanElements.Count - 1].Index, out lastForces))
                    results.Add(lastForces.EndForce.FromTsdBarForce(span.ObjectId, resultCase, 1.0, 2, false, isVertical));
                return;
            }

            // n elements give n + 1 stations: each element's start force, and the last one's end force.
            int stationCount = spanElements.Count + 1;

            for (int i = 0; i < spanElements.Count; i++)
            {
                IElementEndForces forces;
                if (!forceByElementIndex.TryGetValue(spanElements[i].Index, out forces))
                    continue;

                double position = (double)i / (stationCount - 1);
                results.Add(forces.StartForce.FromTsdBarForce(span.ObjectId, resultCase, position, stationCount, true, isVertical));

                if (i == spanElements.Count - 1)
                    results.Add(forces.EndForce.FromTsdBarForce(span.ObjectId, resultCase, 1.0, stationCount, false, isVertical));
            }
        }

        /***************************************************/

        // The requested cases that have been solved, naming those that have not.
        private List<TsdLoadingCaseIdentity> SolvedCasesOnly(IAnalysis3DResults analysis3D, List<TsdLoadingCaseIdentity> cases, string analysisTypeDescription, int timeoutSeconds)
        {
            IEnumerable<Guid> solvedRead;
            if (!Async.TryRunSync(ct => analysis3D.GetSolvedLoadingIdsAsync(ct), timeoutSeconds, "reading which cases are solved for " + analysisTypeDescription, "they have not been checked", out solvedRead))
                return cases;

            HashSet<Guid> solvedLoadingIds = new HashSet<Guid>(solvedRead);

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
                string names = Helpers.Summary(unsolved.Select(c => c.ToString()));

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
