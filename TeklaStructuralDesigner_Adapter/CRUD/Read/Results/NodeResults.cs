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
using BH.Engine.Data;
using BH.Engine.Geometry;
using BH.oM.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Data.Collections;
using BH.oM.Structure.Requests;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using IResult = BH.oM.Analytical.Results.IResult;
using Node = BH.oM.Structure.Elements.Node;
using Point = BH.oM.Geometry.Point;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        // Distance (m) within which a solver node is at a Node's position, as SupportMatchDecimals.
        private const double SolverNodeTolerance = 1e-3;

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Reactions and displacements, read from the solver model once per case. The solver has no
        // construction points, so each pulled Node is matched to the solver node at its position, and
        // solver nodes no pulled Node sits on are left out.
        public IEnumerable<IResult> ReadResults(NodeResultRequest request, ActionConfig actionConfig = null)
        {
            List<IResult> results = new List<IResult>();

            if (m_Model == null)
            {
                Engine.Base.Compute.RecordWarning(ErrorMessages.NotConnected());
                return results;
            }

            if (request == null)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoRequest());
                return results;
            }

            bool reactions = request.ResultType == NodeResultType.NodeReaction;
            if (!reactions && request.ResultType != NodeResultType.NodeDisplacement)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.UnsupportedResultType(request.ResultType.ToString(), "A NodeResultRequest reads node reactions and node displacements only."));
                return results;
            }

            if (request.Axis != BH.oM.Structure.Loads.LoadAxis.Global)
                Engine.Base.Compute.RecordWarning("Tekla Structural Designer reports node results in global axes; the Axis on the request has been ignored.");

            if (request.Modes != null && request.Modes.Count > 0)
                Engine.Base.Compute.RecordWarning("Modal results are not supported by this adapter; the requested Modes have been ignored.");

            TeklaStructuralDesignerPullConfig config = PullConfigOrDefault(actionConfig);

            int timeoutSeconds = TeklaStructuralDesignerConfig.TimeoutSeconds;

            Dictionary<Guid, Node> nodeById = NodesById(timeoutSeconds, false);
            if (nodeById.Count == 0)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoNodes());
                return results;
            }

            List<TsdLoadingCaseIdentity> allCases = BuildLoadingCaseIdentities(timeoutSeconds);
            if (allCases.Count == 0)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoCases());
                return results;
            }

            bool everyNode = request.ObjectIds == null || request.ObjectIds.Count == 0;
            List<KeyValuePair<Guid, Node>> nodes = FilterNodes(nodeById, request.ObjectIds);
            if (nodes.Count == 0)
                return results;                    // FilterNodes has already recorded why.

            List<TsdLoadingCaseIdentity> cases = FilterCases(allCases, request.Cases, config);
            if (cases.Count == 0)
                return results;                    // FilterCases has already recorded why.

            IModel solverModel = SolverModel(config, timeoutSeconds);
            if (solverModel == null)
                return results;

            IEnumerable<INode> solverNodesRead;
            if (!Async.TryRunSync(ct => solverModel.GetNodesAsync(null, ct), timeoutSeconds, "reading solver nodes", "no node results have been read", out solverNodesRead, true))
                return results;

            List<INode> solverNodes = solverNodesRead.ToList();
            List<NodeTarget> targets = reactions ? ReactionTargets(nodes, solverNodes, everyNode) : DisplacementTargets(nodes, solverNodes);
            if (targets.Count == 0)
            {
                Engine.Base.Compute.RecordWarning("None of the requested Nodes is at a " + (reactions ? "support" : "node") +
                    " of the solver model for " + config.AnalysisType + ", so no results were read.");
                return results;
            }

            IAnalysis3DResults analysis3D = Analysis3D(solverModel, config, timeoutSeconds);
            if (analysis3D == null)
                return results;

            List<TsdLoadingCaseIdentity> solvedCases = SolvedCasesOnly(analysis3D, cases, config.AnalysisType.ToString(), timeoutSeconds);
            if (solvedCases.Count == 0)
                return results;

            LoadingResultType loadingResultType = config.LoadingResultType.ToTsd();
            List<int> solverNodeIndices = targets.SelectMany(t => t.SolverNodeIndices).Distinct().ToList();

            foreach (TsdLoadingCaseIdentity loadingCase in solvedCases)
            {
                if (reactions)
                {
                    if (!Async.TryRunSync(ct => analysis3D.GetNodalReactionsAsync(loadingCase.Id, loadingResultType, solverNodeIndices, ct),
                        timeoutSeconds, "reading reactions for case '" + loadingCase + "'", "it has been skipped", out var reactionsRead))
                        continue;

                    Dictionary<int, IForce3DGlobal> forceByNode = reactionsRead
                        .Where(f => f.Force != null)
                        .GroupBy(f => f.NodeIndex)
                        .ToDictionary(g => g.Key, g => g.First().Force);

                    foreach (NodeTarget target in targets)
                    {
                        List<IForce3DGlobal> forces = target.SolverNodeIndices.Where(forceByNode.ContainsKey).Select(i => forceByNode[i]).ToList();
                        if (forces.Count > 0)
                            results.Add(forces.FromTsdNodeReaction(target.ObjectId, loadingCase.Number));
                    }
                }
                else
                {
                    if (!Async.TryRunSync(ct => analysis3D.GetNodalDisplacementsAsync(loadingCase.Id, loadingResultType, solverNodeIndices, ct),
                        timeoutSeconds, "reading displacements for case '" + loadingCase + "'", "it has been skipped", out var displacementsRead))
                        continue;

                    Dictionary<int, IDisplacement> displacementByNode = displacementsRead
                        .Where(d => d.Displacement != null)
                        .GroupBy(d => d.NodeIndex)
                        .ToDictionary(g => g.Key, g => g.First().Displacement);

                    foreach (NodeTarget target in targets)
                    {
                        IDisplacement displacement;
                        if (displacementByNode.TryGetValue(target.SolverNodeIndices[0], out displacement))
                            results.Add(displacement.FromTsdNodeDisplacement(target.ObjectId, loadingCase.Number));
                    }
                }
            }

            if (results.Count == 0)
            {
                Engine.Base.Compute.RecordWarning("No node results were returned for the requested Nodes and cases. Check that the model has been analysed for " +
                    config.AnalysisType + ".");
            }

            return results;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Every supporting solver node at each Node's position. Coincident Nodes all carry the support
        // (see ApplySupports), so its reaction is reported once, against the first, to keep the sum right.
        private static List<NodeTarget> ReactionTargets(List<KeyValuePair<Guid, Node>> nodes, List<INode> solverNodes, bool everyNode)
        {
            List<INode> supports = solverNodes.Where(IsSupport).ToList();
            PointMatrix<int> matrix = SolverNodeMatrix(supports);

            List<NodeTarget> targets = new List<NodeTarget>();
            HashSet<int> claimed = new HashSet<int>();
            int shared = 0, unsupported = 0;

            foreach (KeyValuePair<Guid, Node> node in nodes)
            {
                List<int> atPosition = SolverNodesAt(matrix, node.Value.Position);
                if (atPosition.Count == 0)
                {
                    unsupported++;
                    continue;
                }

                List<int> unclaimed = atPosition.Where(claimed.Add).ToList();
                if (unclaimed.Count == 0)
                    shared++;
                else
                    targets.Add(new NodeTarget(node.Key.ToString(), unclaimed));
            }

            if (shared > 0)
            {
                Engine.Base.Compute.RecordNote(shared + " Node(s) share their position with another Node that the reaction there has been reported against. " +
                    "A reaction is reported once per position, so that the reactions pulled sum correctly.");
            }

            // Only worth a warning for Nodes asked for by name.
            if (!everyNode && unsupported > 0)
                Engine.Base.Compute.RecordWarning(unsupported + " requested Node(s) are not at a support of the solver model, so have no reaction.");

            if (everyNode && supports.Count > claimed.Count)
            {
                Engine.Base.Compute.RecordWarning((supports.Count - claimed.Count) + " supported solver node(s) do not coincide with a pulled Node - along the base of a wall, for example - " +
                    "so their reactions have not been read. The reactions pulled do not sum to the total reaction on the structure.");
            }

            return targets;
        }

        /***************************************************/

        // IsSupporting alone misses supported column bases, so explicit supports are checked too.
        private static bool IsSupport(INode node)
        {
            return node.IsSupporting || node.HasSupport(SupportType.Structure3D);
        }

        /***************************************************/

        // The solver node nearest each Node's position.
        private static List<NodeTarget> DisplacementTargets(List<KeyValuePair<Guid, Node>> nodes, List<INode> solverNodes)
        {
            PointMatrix<int> matrix = SolverNodeMatrix(solverNodes);

            List<NodeTarget> targets = new List<NodeTarget>();
            int unmatched = 0;

            foreach (KeyValuePair<Guid, Node> node in nodes)
            {
                List<int> atPosition = SolverNodesAt(matrix, node.Value.Position);
                if (atPosition.Count == 0)
                    unmatched++;
                else
                    targets.Add(new NodeTarget(node.Key.ToString(), new List<int> { atPosition[0] }));
            }

            if (unmatched > 0)
                Engine.Base.Compute.RecordWarning(unmatched + " Node(s) have no solver node at their position, so have no displacement.");

            return targets;
        }

        /***************************************************/

        private static PointMatrix<int> SolverNodeMatrix(IEnumerable<INode> solverNodes)
        {
            List<LocalData<int>> data = solverNodes.Select(n => Engine.Data.Create.LocalData(n.Coordinates.FromTsdPoint(), n.Index)).ToList();
            return Engine.Data.Create.PointMatrix(data, SolverNodeTolerance);
        }

        /***************************************************/

        // The solver node indices within tolerance of a position, nearest first.
        private static List<int> SolverNodesAt(PointMatrix<int> matrix, Point position)
        {
            if (position == null)
                return new List<int>();

            return matrix.CloseToPoint(position, SolverNodeTolerance)
                .OrderBy(d => d.Position.Distance(position))
                .ThenBy(d => d.Data)
                .Select(d => d.Data)
                .ToList();
        }

        /***************************************************/
    }
}
