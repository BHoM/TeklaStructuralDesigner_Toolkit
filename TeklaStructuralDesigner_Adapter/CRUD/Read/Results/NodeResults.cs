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
using BH.oM.Structure.Requests;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;

// Aliased rather than imported by namespace: the Tekla Structural Designer solver namespace this file
// also needs declares node and result types of its own.
using IResult = BH.oM.Analytical.Results.IResult;
using Node = BH.oM.Structure.Elements.Node;
using Point = BH.oM.Geometry.Point;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // Node reactions and node displacements, read from the solver model in one call per loading
        // case - the same route, cases and pull configuration as the batched bar forces.
        //
        // Results are reported against the Nodes this adapter pulls, with the Node's id as their
        // ObjectId. The solver knows nothing of construction points - a solver node has an index and
        // coordinates only - so a Node is matched to the solver node at its position. Solver nodes that
        // no pulled Node sits on, such as those along a span or in the mesh of a slab or wall, have no
        // Node to be reported against and are left out.
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

            TSD.API.Remoting.Solver.IModel solverModel = SolverModel(config, timeoutSeconds);
            if (solverModel == null)
                return results;

            List<INode> solverNodes;
            try
            {
                solverNodes = Async.RunSync(ct => solverModel.GetNodesAsync(null, ct), timeoutSeconds, "reading solver nodes").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read solver nodes from Tekla Structural Designer. " + e.Message);
                return results;
            }

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

            LoadingResultType loadingResultType = config.LoadingResultType.ToTeklaStructuralDesigner();
            List<int> solverNodeIndices = targets.SelectMany(t => t.SolverNodeIndices).Distinct().ToList();

            foreach (TsdLoadingCaseIdentity loadingCase in solvedCases)
            {
                try
                {
                    if (reactions)
                    {
                        Dictionary<int, IForce3DGlobal> forceByNode = Async.RunSync(
                            ct => analysis3D.GetNodalReactionsAsync(loadingCase.Id, loadingResultType, solverNodeIndices, ct),
                            timeoutSeconds, "reading reactions for case '" + loadingCase + "'")
                            .Where(f => f.Force != null)
                            .GroupBy(f => f.NodeIndex)
                            .ToDictionary(g => g.Key, g => g.First().Force);

                        foreach (NodeTarget target in targets)
                        {
                            List<IForce3DGlobal> forces = target.SolverNodeIndices.Where(forceByNode.ContainsKey).Select(i => forceByNode[i]).ToList();
                            if (forces.Count > 0)
                                results.Add(forces.ToBHoM(target.ObjectId, loadingCase.Number));
                        }
                    }
                    else
                    {
                        Dictionary<int, IDisplacement> displacementByNode = Async.RunSync(
                            ct => analysis3D.GetNodalDisplacementsAsync(loadingCase.Id, loadingResultType, solverNodeIndices, ct),
                            timeoutSeconds, "reading displacements for case '" + loadingCase + "'")
                            .Where(d => d.Displacement != null)
                            .GroupBy(d => d.NodeIndex)
                            .ToDictionary(g => g.Key, g => g.First().Displacement);

                        foreach (NodeTarget target in targets)
                        {
                            IDisplacement displacement;
                            if (displacementByNode.TryGetValue(target.SolverNodeIndices[0], out displacement))
                                results.Add(displacement.ToBHoM(target.ObjectId, loadingCase.Number));
                        }
                    }
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning("Failed to read node results for case '" + loadingCase + "'; it has been skipped. " + e.Message);
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
        /****            Private Methods                ****/
        /****            (Node result targets)          ****/
        /***************************************************/

        // Which solver nodes each Node's reaction is read from: every supporting solver node at its
        // position.
        //
        // Tekla Structural Designer models coincident construction points freely, and this adapter
        // attaches a support to every Node at its position (see ApplySupports), so several pulled Nodes
        // can sit on one support. Its reaction is reported once, against the first of them: reported
        // against each, the reactions would no longer sum to the load on the structure.
        private static List<NodeTarget> ReactionTargets(List<KeyValuePair<Guid, Node>> nodes, List<INode> solverNodes, bool everyNode)
        {
            SolverNodeLookup lookup = new SolverNodeLookup(solverNodes.Where(IsSupport));

            List<NodeTarget> targets = new List<NodeTarget>();
            HashSet<int> claimed = new HashSet<int>();
            int shared = 0, unsupported = 0;

            foreach (KeyValuePair<Guid, Node> node in nodes)
            {
                List<int> atPosition = lookup.At(node.Value.Position);
                if (atPosition.Count == 0)
                {
                    unsupported++;
                    continue;
                }

                List<int> unclaimed = new List<int>();
                foreach (int index in atPosition)
                {
                    if (claimed.Add(index))
                        unclaimed.Add(index);
                }

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

            // Asked for by name, a Node that is not a support is worth a warning. With every Node read,
            // it is simply most of the model.
            if (!everyNode && unsupported > 0)
                Engine.Base.Compute.RecordWarning(unsupported + " requested Node(s) are not at a support of the solver model, so have no reaction.");

            if (everyNode && lookup.Count > claimed.Count)
            {
                Engine.Base.Compute.RecordWarning((lookup.Count - claimed.Count) + " supported solver node(s) do not coincide with a pulled Node - along the base of a wall, for example - " +
                    "so their reactions have not been read. The reactions pulled do not sum to the total reaction on the structure.");
            }

            return targets;
        }

        /***************************************************/

        // Whether the solver reports a reaction at a node: it carries a support of the structure, or is
        // one the solver supports implicitly, such as the mesh nodes along the base of a wall.
        // IsSupporting alone is not enough. Checked on a live model, it was true only for the 8 wall
        // base nodes and false for all 35 column bases carrying a support, while the solver returned
        // reactions at all 43.
        private static bool IsSupport(INode node)
        {
            return node.IsSupporting || node.HasSupport(SupportType.Structure3D);
        }

        /***************************************************/

        // Which solver node each Node's displacement is read from: the one nearest its position.
        private static List<NodeTarget> DisplacementTargets(List<KeyValuePair<Guid, Node>> nodes, List<INode> solverNodes)
        {
            SolverNodeLookup lookup = new SolverNodeLookup(solverNodes);

            List<NodeTarget> targets = new List<NodeTarget>();
            int unmatched = 0;

            foreach (KeyValuePair<Guid, Node> node in nodes)
            {
                List<int> atPosition = lookup.At(node.Value.Position);
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
        /****            Private Nested Types           ****/
        /***************************************************/

        // A Node results are reported against, and the solver nodes they are read from.
        private sealed class NodeTarget
        {
            public string ObjectId { get; }
            public List<int> SolverNodeIndices { get; }

            public NodeTarget(string objectId, List<int> solverNodeIndices)
            {
                ObjectId = objectId;
                SolverNodeIndices = solverNodeIndices;
            }
        }

        /***************************************************/

        // Solver nodes by position, in metres. A grid of cells one tolerance wide, searched together
        // with its neighbours, rather than a dictionary keyed on rounded coordinates: a coordinate that
        // falls on a rounding boundary rounds either way depending on floating point noise, and the
        // match would then be missed.
        private sealed class SolverNodeLookup
        {
            // The same millimetre a support is matched to a Node at - see SupportMatchDecimals.
            private const double Tolerance = 1e-3;

            private readonly Dictionary<Tuple<long, long, long>, List<KeyValuePair<Point, int>>> m_Cells = new Dictionary<Tuple<long, long, long>, List<KeyValuePair<Point, int>>>();

            public int Count { get; private set; }

            public SolverNodeLookup(IEnumerable<INode> nodes)
            {
                foreach (INode node in nodes)
                {
                    Point position = node.Coordinates.ToBHoMPoint();
                    Tuple<long, long, long> cell = Tuple.Create(Cell(position.X), Cell(position.Y), Cell(position.Z));

                    List<KeyValuePair<Point, int>> inCell;
                    if (!m_Cells.TryGetValue(cell, out inCell))
                        m_Cells[cell] = inCell = new List<KeyValuePair<Point, int>>();

                    inCell.Add(new KeyValuePair<Point, int>(position, node.Index));
                    Count++;
                }
            }

            // The indices of the solver nodes within tolerance of a position, nearest first.
            public List<int> At(Point position)
            {
                List<KeyValuePair<double, int>> found = new List<KeyValuePair<double, int>>();
                if (position == null)
                    return new List<int>();

                long x = Cell(position.X), y = Cell(position.Y), z = Cell(position.Z);

                for (long i = x - 1; i <= x + 1; i++)
                {
                    for (long j = y - 1; j <= y + 1; j++)
                    {
                        for (long k = z - 1; k <= z + 1; k++)
                        {
                            List<KeyValuePair<Point, int>> inCell;
                            if (!m_Cells.TryGetValue(Tuple.Create(i, j, k), out inCell))
                                continue;

                            foreach (KeyValuePair<Point, int> candidate in inCell)
                            {
                                double dx = candidate.Key.X - position.X, dy = candidate.Key.Y - position.Y, dz = candidate.Key.Z - position.Z;
                                double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                if (distance <= Tolerance)
                                    found.Add(new KeyValuePair<double, int>(distance, candidate.Value));
                            }
                        }
                    }
                }

                return found.OrderBy(f => f.Key).ThenBy(f => f.Value).Select(f => f.Value).ToList();
            }

            private static long Cell(double coordinate)
            {
                return (long)Math.Floor(coordinate / Tolerance);
            }
        }

        /***************************************************/
    }
}
