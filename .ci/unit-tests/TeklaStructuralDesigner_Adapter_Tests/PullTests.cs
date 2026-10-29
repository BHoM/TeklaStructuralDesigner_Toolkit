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

using System.Linq;
using BH.Adapter.TeklaStructuralDesigner;
using BH.Engine.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;
using BH.oM.Structure.Requests;
using BH.oM.Structure.Results;
using NUnit.Framework;
using Shouldly;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    // Explicit: these need Tekla Structural Designer running with an analysed model open, which CI
    // does not have. See ../Models/README.md for the test model they need.
    // Run explicitly with: dotnet test --filter "FullyQualifiedName~PullTests"
    [Explicit("Requires Tekla Structural Designer running with an analysed model open - see Models/README.md")]
    public class PullTests
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        private TeklaStructuralDesignerAdapter m_Adapter = null!;

        [OneTimeSetUp]
        [Description("Attaches to the single running Tekla Structural Designer instance. Fails the fixture immediately, with a clear message, if that is not what is found - rather than let every test fail individually for the same reason.")]
        public void OneTimeSetup()
        {
            BH.Engine.Base.Compute.ClearCurrentEvents();
            m_Adapter = new TeklaStructuralDesignerAdapter("", new TeklaStructuralDesignerConfig(), true);

            var errors = BH.Engine.Base.Query.CurrentEvents()
                .Where(e => e.Type == BH.oM.Base.Debugging.EventType.Error)
                .ToList();

            if (errors.Count > 0)
                Assert.Fail("Could not connect to Tekla Structural Designer: " + errors[0].Message);
        }

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        [Test]
        [Description("Pulling Bars must return at least one Bar, each carrying a TeklaStructuralDesignerId fragment.")]
        public void PullBars()
        {
            var bars = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(Bar) }).Cast<Bar>().ToList();

            bars.ShouldNotBeEmpty();
            bars.All(b => b.HasAdapterIdFragment(typeof(TeklaStructuralDesignerId))).ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("Pulling a SelectionRequest must return what is selected in Tekla Structural Designer, each object carrying a TeklaStructuralDesignerId fragment, and every Bar among them must be one a pull of all Bars also returns. Select at least one member, slab or wall in Tekla Structural Designer before running.")]
        public void PullSelection()
        {
            var selected = m_Adapter.Pull(new BH.oM.Data.Requests.SelectionRequest()).Cast<BH.oM.Base.IBHoMObject>().ToList();

            selected.ShouldNotBeEmpty("select at least one member, slab or wall in Tekla Structural Designer before running this test");
            selected.All(o => o.HasAdapterIdFragment(typeof(TeklaStructuralDesignerId))).ShouldBeTrue();

            var allBarIds = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(Bar) }).Cast<Bar>()
                .Select(b => b.AdapterId<object>(typeof(TeklaStructuralDesignerId)).ToString()).ToList();

            selected.OfType<Bar>().Select(b => b.AdapterId<object>(typeof(TeklaStructuralDesignerId)).ToString()).ShouldBeSubsetOf(allBarIds);
        }

        /***************************************************/

        [Test]
        [Description("Pulling bar forces for a simply supported beam under a single gravity combination must be symmetric: equal and opposite major axis shear at the two ends, and zero major axis moment at both ends.")]
        public void SimplySupportedBeamShearAndMomentAreSymmetric()
        {
            // This test names a specific member and combination that exist only in the (not yet
            // committed) test model - update MemberObjectId and CombinationName once that model exists.
            string memberObjectId = "B1:0";
            string combinationName = "1";

            var request = new BarResultRequest
            {
                ResultType = BarResultType.BarForce,
                ObjectIds = new System.Collections.Generic.List<object> { memberObjectId },
                Cases = new System.Collections.Generic.List<object> { combinationName },
                Divisions = 2,
            };

            var results = m_Adapter.ReadResults(request).Cast<BarForce>().OrderBy(r => r.Position).ToList();

            results.Count.ShouldBe(2);

            BarForce start = results[0];
            BarForce end = results[1];

            // Major axis shear should be equal and opposite at the two ends - negative at the start and
            // positive at the end under gravity, as BHoM's sign convention has it; major axis moment
            // should be zero at both pinned ends.
            (start.FZ + end.FZ).ShouldBe(0, 1.0);
            start.FZ.ShouldBeLessThan(0, "under gravity, BHoM's major axis shear is negative at the start of a span");
            end.FZ.ShouldBeGreaterThan(0, "under gravity, BHoM's major axis shear is positive at the end of a span");
            start.MY.ShouldBe(0, 1.0);
            end.MY.ShouldBe(0, 1.0);
        }

        /***************************************************/

        [Test]
        [Description("Gravity load on a horizontal beam must come back as major axis shear (FZ), not minor (FY). Guards the axis mapping in Convert/FromTsd/Results/BarForce.cs: if it fails with FY carrying the shear, the major and minor axes have been transposed.")]
        public void GravityBendingIsMajorAxis()
        {
            // Same placeholder member and combination as SimplySupportedBeamShearAndMomentAreSymmetric.
            string memberObjectId = "B1:0";
            string combinationName = "1";

            var request = new BarResultRequest
            {
                ResultType = BarResultType.BarForce,
                ObjectIds = new System.Collections.Generic.List<object> { memberObjectId },
                Cases = new System.Collections.Generic.List<object> { combinationName },
                Divisions = 2,
            };

            var results = m_Adapter.ReadResults(request).Cast<BarForce>().ToList();
            results.Count.ShouldBe(2);

            foreach (BarForce end in results)
            {
                System.Math.Abs(end.FZ).ShouldBeGreaterThan(1.0, "the end shear of a gravity loaded beam should be major axis shear, FZ");
                System.Math.Abs(end.FY).ShouldBeLessThan(System.Math.Abs(end.FZ) / 100, "a gravity loaded beam should carry next to no minor axis shear, FY");
            }
        }

        /***************************************************/

        [Test]
        [Description("Every roof and wind wall must be a one way loading panel whose ReferenceEdge is 1 and whose first external edge runs along its local x, the span. Robot reads a LoadingPanelProperty's direction only from whether ReferenceEdge is odd (local x) or even (local y), so a span edge anywhere else in the outline turned the span a quarter turn in Robot.")]
        public void LoadingPanelsSpanAlongTheirFirstEdge()
        {
            var panels = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(Panel) }).Cast<Panel>()
                .Where(p => p.Property is BH.oM.Structure.SurfaceProperties.LoadingPanelProperty)
                .ToList();

            panels.ShouldNotBeEmpty("the test model needs at least one roof or wind wall");

            foreach (Panel panel in panels)
            {
                ((BH.oM.Structure.SurfaceProperties.LoadingPanelProperty)panel.Property).ReferenceEdge.ShouldBe(1);

                BH.oM.Geometry.Vector localX = BH.Engine.Structure.Query.LocalOrientation(panel).X;
                // The outline is a polyline, so each external edge is one straight Line.
                BH.oM.Geometry.Line edge = (BH.oM.Geometry.Line)panel.ExternalEdges[0].Curve;
                BH.oM.Geometry.Point start = edge.Start;
                BH.oM.Geometry.Point end = edge.End;
                double length = System.Math.Sqrt(System.Math.Pow(end.X - start.X, 2) + System.Math.Pow(end.Y - start.Y, 2) + System.Math.Pow(end.Z - start.Z, 2));
                double alignment = System.Math.Abs((end.X - start.X) * localX.X + (end.Y - start.Y) * localX.Y + (end.Z - start.Z) * localX.Z) / length;

                alignment.ShouldBeGreaterThan(0.7, "the first edge of '" + panel.Name + "' should run along its span, local x");
            }
        }

        /***************************************************/

        [Test]
        [Description("The batched solver route users get and the per span end route it is checked against must agree, within a generous tolerance, for the same span and case - if they do not, the solver route's assumptions (element ordering, axis mapping) are wrong for this model.")]
        public void SolverAndSpanEndRoutesAgree()
        {
            string memberObjectId = "B1:0";
            string combinationName = "1";

            // A fresh request per call only because ObjectIds/Cases are mutable lists best not shared
            // between calls.
            BarResultRequest NewRequest() => new BarResultRequest
            {
                ResultType = BarResultType.BarForce,
                ObjectIds = new System.Collections.Generic.List<object> { memberObjectId },
                Cases = new System.Collections.Generic.List<object> { combinationName },
                Divisions = 2,
            };

            var solverResult = m_Adapter.ReadBarForces(NewRequest(), null, false).Cast<BarForce>().First(r => r.Position < 0.5);
            var spanEndResult = m_Adapter.ReadBarForces(NewRequest(), null, true).Cast<BarForce>().First(r => r.Position < 0.5);

            solverResult.FX.ShouldBe(spanEndResult.FX, 1.0);
            solverResult.FY.ShouldBe(spanEndResult.FY, 1.0);
            solverResult.FZ.ShouldBe(spanEndResult.FZ, 1.0);
            solverResult.MX.ShouldBe(spanEndResult.MX, 1.0);
            solverResult.MY.ShouldBe(spanEndResult.MY, 1.0);
            solverResult.MZ.ShouldBe(spanEndResult.MZ, 1.0);
        }

        /***************************************************/

        [Test]
        [Description("Pulling Levels must return at least one, named, lowest first, each carrying a TeklaStructuralDesignerId fragment.")]
        public void PullLevels()
        {
            var levels = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(BH.oM.Spatial.SettingOut.Level) }).Cast<BH.oM.Spatial.SettingOut.Level>().ToList();

            levels.ShouldNotBeEmpty();
            levels.All(l => !string.IsNullOrWhiteSpace(l.Name)).ShouldBeTrue();
            levels.All(l => l.HasAdapterIdFragment(typeof(TeklaStructuralDesignerId))).ShouldBeTrue();
            levels.Select(l => l.Elevation).ShouldBe(levels.Select(l => l.Elevation).OrderBy(e => e));
        }

        /***************************************************/

        [Test]
        [Description("Pulling Grids must return one named Grid per grid line, each with a line of some length as its Curve.")]
        public void PullGridsReturnNamedLines()
        {
            var grids = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(BH.oM.Spatial.SettingOut.Grid) }).Cast<BH.oM.Spatial.SettingOut.Grid>().ToList();

            grids.ShouldNotBeEmpty("the test model needs at least one architectural grid");
            grids.All(g => !string.IsNullOrWhiteSpace(g.Name)).ShouldBeTrue();
            grids.All(g => g.HasAdapterIdFragment(typeof(TeklaStructuralDesignerId))).ShouldBeTrue();
            grids.All(g => g.Curve is BH.oM.Geometry.Line line && line.Start != null && line.End != null &&
                System.Math.Sqrt(System.Math.Pow(line.End.X - line.Start.X, 2) + System.Math.Pow(line.End.Y - line.Start.Y, 2) + System.Math.Pow(line.End.Z - line.Start.Z, 2)) > 0).ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("Node reactions must come back against pulled Nodes that carry a support, once per Node and case - the two things that make a pulled reaction usable downstream.")]
        public void NodeReactionsLandOnSupportedNodes()
        {
            var nodes = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(Node) }).Cast<Node>().ToList();
            var nodeById = nodes.ToDictionary(n => n.AdapterId<object>(typeof(TeklaStructuralDesignerId)).ToString());

            var config = new TeklaStructuralDesignerPullConfig { IncludeLoadcases = true, IncludeStrengthCombinations = false };
            var results = m_Adapter.ReadResults(new NodeResultRequest { ResultType = NodeResultType.NodeReaction }, config).Cast<NodeReaction>().ToList();

            results.ShouldNotBeEmpty("the test model needs at least one support under a member end, and to have been analysed");
            results.All(r => nodeById.ContainsKey(r.ObjectId.ToString())).ShouldBeTrue("every reaction should be reported against a pulled Node");
            results.All(r => nodeById[r.ObjectId.ToString()].Support != null).ShouldBeTrue("a reaction should only be reported at a supported Node");
            results.GroupBy(r => new { Id = r.ObjectId.ToString(), Case = r.ResultCase.ToString() }).All(g => g.Count() == 1).ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("Node displacements requested for specific pulled Nodes must return one result per Node and case, in metres - a displacement of a metre or more would mean the millimetres were not converted.")]
        public void NodeDisplacementsReturnedForRequestedNodes()
        {
            var nodes = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(Node) }).Cast<Node>().Take(5).ToList();
            nodes.ShouldNotBeEmpty();

            var config = new TeklaStructuralDesignerPullConfig { IncludeLoadcases = true, IncludeStrengthCombinations = false };
            var request = new NodeResultRequest
            {
                ResultType = NodeResultType.NodeDisplacement,
                ObjectIds = nodes.Cast<object>().ToList(),
            };

            var results = m_Adapter.ReadResults(request, config).Cast<NodeDisplacement>().ToList();

            results.ShouldNotBeEmpty();
            results.Select(r => r.ObjectId.ToString()).Distinct().Count().ShouldBe(nodes.Count);
            results.All(r => System.Math.Abs(r.UX) < 1 && System.Math.Abs(r.UY) < 1 && System.Math.Abs(r.UZ) < 1).ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("Pulling Loadcases must return at least one, with a name rather than a Guid, and a Number matching the loadcase's index in Tekla Structural Designer.")]
        public void PullLoadcases()
        {
            var loadcases = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(Loadcase) }).Cast<Loadcase>().ToList();

            loadcases.ShouldNotBeEmpty();
            loadcases.All(c => !string.IsNullOrWhiteSpace(c.Name)).ShouldBeTrue();

            // A Guid in the Name would mean Identifier() fell all the way through, and would break
            // matching a pulled case against a result's ResultCase.
            loadcases.Any(c => System.Guid.TryParse(c.Name, out _)).ShouldBeFalse();
        }

        /***************************************************/

        [Test]
        [Description("Pulling LoadCombinations must return combinations whose LoadCases reference real Loadcase objects, so that a combination can be expanded without a second pull.")]
        public void PullLoadCombinationsResolveTheirLoadcases()
        {
            var combinations = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(LoadCombination) }).Cast<LoadCombination>().ToList();

            combinations.ShouldNotBeEmpty();

            var withCases = combinations.Where(c => c.LoadCases != null && c.LoadCases.Count > 0).ToList();
            withCases.ShouldNotBeEmpty("at least one combination should reference a loadcase");
            withCases.All(c => c.LoadCases.All(t => t.Item2 is Loadcase)).ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("Each Tekla Structural Designer combination must come through as a Strength and a Service LoadCombination, numbered 1000 + n and 2000 + n, named with their limit state, and carrying their own factors - otherwise a Service request would silently return Strength factors.")]
        public void CombinationsSplitIntoStrengthAndService()
        {
            var combinations = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(LoadCombination) }).Cast<LoadCombination>().ToList();

            var strength = combinations.Where(c => c.Name.StartsWith("Strength ")).ToList();
            var service = combinations.Where(c => c.Name.StartsWith("Service ")).ToList();

            strength.ShouldNotBeEmpty();
            service.ShouldNotBeEmpty();
            (strength.Count + service.Count).ShouldBe(combinations.Count, "every combination should carry a limit state prefix");

            // Numbers are unique across both limit states, and each band holds only its own limit state.
            combinations.Select(c => c.Number).Distinct().Count().ShouldBe(combinations.Count);
            strength.All(c => c.Number / 1000 == 1).ShouldBeTrue();
            service.All(c => c.Number / 1000 == 2).ShouldBeTrue();

            // The Strength and Service versions of one combination hold different factors. If this fails
            // on a model whose factors are genuinely identical throughout, it is the model that is unusual.
            var pairs = strength.Join(service, s => s.Number % 1000, v => v.Number % 1000, (s, v) => new { s, v }).ToList();
            pairs.ShouldNotBeEmpty();
            pairs.Any(p => !p.s.LoadCases.Select(t => t.Item1).SequenceEqual(p.v.LoadCases.Select(t => t.Item1))).ShouldBeTrue("Service should not carry the Strength factors");
        }

        /***************************************************/

        [Test]
        [Description("Pulled bar loads must attach to Bars that were themselves pulled, and carry the Loadcase they belong to - the two things that make a pulled load usable downstream.")]
        public void PullBarLoadsAttachToBarsAndCases()
        {
            var loads = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(BH.oM.Structure.Loads.ILoad) })
                .Cast<BH.oM.Structure.Loads.ILoad>().ToList();

            loads.ShouldNotBeEmpty("the test model needs at least one applied bar or nodal load");

            var barLoads = loads.OfType<BH.oM.Structure.Loads.BarUniformlyDistributedLoad>().ToList();
            barLoads.ShouldNotBeEmpty("the test model needs at least one full-length UDL on a bar");

            barLoads.All(l => l.Loadcase != null).ShouldBeTrue();
            barLoads.All(l => l.Objects != null && l.Objects.Elements.Count > 0).ShouldBeTrue();
            barLoads.All(l => l.Objects.Elements.All(b => b.HasAdapterIdFragment(typeof(TeklaStructuralDesignerId)))).ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("A known UDL must come back at the magnitude the model applies, in BHoM's N/m. This is the test that settles whether Tekla Structural Designer reports applied loads in the same N/mm system as its results - see Convert/Units.cs.")]
        public void UdlMagnitudeMatchesTheModel()
        {
            // Update these two to a UDL that exists in the test model, and the magnitude it is applied
            // at, expressed in newtons per metre.
            string memberObjectId = "B1:0";
            double expectedForcePerMetre = -10000.0;

            var loads = m_Adapter.Pull(new BH.oM.Data.Requests.FilterRequest { Type = typeof(BH.oM.Structure.Loads.BarUniformlyDistributedLoad) })
                .Cast<BH.oM.Structure.Loads.BarUniformlyDistributedLoad>()
                .Where(l => l.Objects.Elements.Any(b => b.Name == memberObjectId))
                .ToList();

            loads.ShouldNotBeEmpty();

            // A thousand-fold error is the failure mode this is here to catch, so the tolerance is
            // deliberately loose enough to ignore rounding and tight enough to catch that.
            loads.Any(l => System.Math.Abs(l.Force.Z - expectedForcePerMetre) < System.Math.Abs(expectedForcePerMetre) * 0.01).ShouldBeTrue(
                "the UDL came back as " + string.Join(", ", loads.Select(l => l.Force.Z)) + " N/m, expected about " + expectedForcePerMetre);
        }

        /***************************************************/

        [Test]
        [Description("Requesting results for an unsolved analysis type must name the unsolved cases, not throw or return an empty list unexplained.")]
        public void UnsolvedAnalysisTypeNamesTheUnsolvedCases()
        {
            var config = new TeklaStructuralDesignerPullConfig { AnalysisType = TeklaStructuralDesignerAnalysisType.SecondOrderNonLinear };
            BH.Engine.Base.Compute.ClearCurrentEvents();

            var request = new BarResultRequest { ResultType = BarResultType.BarForce };
            m_Adapter.ReadResults(request, config).ToList();

            var warningsAndErrors = BH.Engine.Base.Query.CurrentEvents()
                .Where(e => e.Type == BH.oM.Base.Debugging.EventType.Warning || e.Type == BH.oM.Base.Debugging.EventType.Error)
                .ToList();

            warningsAndErrors.ShouldNotBeEmpty("an unsolved analysis type should be reported, not silently return nothing");
        }

        /***************************************************/
    }
}
