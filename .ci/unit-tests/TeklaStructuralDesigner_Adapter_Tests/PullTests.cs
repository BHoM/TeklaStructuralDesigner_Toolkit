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

using BH.Adapter.TeklaStructuralDesigner;
using BH.Engine.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Requests;
using BH.oM.Structure.Results;
using NUnit.Framework;
using Shouldly;
using System.Linq;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    // Every test in this fixture is [Explicit]: they need Tekla Structural Designer actually running,
    // with a model open and analysed, which no CI agent has - the Remoting API attaches to a live
    // instance and has no headless mode. Unlike Robot_Toolkit's equivalent fixture, there is no
    // committed test model here yet either: see ../Models/README.md for what is needed before these
    // can be un-explicited and pointed at a real file.
    //
    // Run explicitly with: dotnet test --filter "FullyQualifiedName~PullTests"
    [Explicit("Requires Tekla Structural Designer running with an analysed model open - see Models/README.md")]
    public class PullTests
    {
        /***************************************************/
        /**** Public methods - setup                    ****/
        /***************************************************/

        private TeklaStructuralDesignerAdapter m_Adapter = null!;

        [OneTimeSetUp]
        [Description("Attaches to the single running Tekla Structural Designer instance. Fails the fixture immediately, with a clear message, if that is not what is found - rather than let every test fail individually for the same reason.")]
        public void OneTimeSetup()
        {
            m_Adapter = new TeklaStructuralDesignerAdapter("", new TeklaStructuralDesignerConfig(), true);

            var errors = BH.Engine.Base.Query.CurrentEvents()
                .Where(e => e.Type == BH.oM.Base.Debugging.EventType.Error)
                .ToList();

            if (errors.Count > 0)
                Assert.Fail("Could not connect to Tekla Structural Designer: " + errors[0].Message);
        }

        /***************************************************/
        /**** Public Tests                              ****/
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

            // Major axis shear should be equal and opposite at the two ends; major axis moment should
            // be zero at both pinned ends.
            (start.FZ + end.FZ).ShouldBe(0, 1.0);
            start.MY.ShouldBe(0, 1.0);
            end.MY.ShouldBe(0, 1.0);
        }

        /***************************************************/

        [Test]
        [Description("The Auto and SpanEnds force sources must agree, within a generous tolerance, for the same span and case - if they do not, the batched solver route's assumptions (element ordering, axis mapping) are wrong for this model and SwapMajorMinorAxes or BarForceSource needs attention.")]
        public void SolverAndSpanEndRoutesAgree()
        {
            string memberObjectId = "B1:0";
            string combinationName = "1";

            // BarForceSource lives on the ActionConfig, not the request, so a fresh request is built
            // per call only because ObjectIds/Cases are mutable lists best not shared between calls.
            BarResultRequest NewRequest() => new BarResultRequest
            {
                ResultType = BarResultType.BarForce,
                ObjectIds = new System.Collections.Generic.List<object> { memberObjectId },
                Cases = new System.Collections.Generic.List<object> { combinationName },
                Divisions = 2,
            };

            var solverConfig = new TeklaStructuralDesignerPullConfig { BarForceSource = TeklaStructuralDesignerBarForceSource.Solver };
            var spanEndConfig = new TeklaStructuralDesignerPullConfig { BarForceSource = TeklaStructuralDesignerBarForceSource.SpanEnds };

            var solverResult = m_Adapter.ReadResults(NewRequest(), solverConfig).Cast<BarForce>().First(r => r.Position < 0.5);
            var spanEndResult = m_Adapter.ReadResults(NewRequest(), spanEndConfig).Cast<BarForce>().First(r => r.Position < 0.5);

            solverResult.FX.ShouldBe(spanEndResult.FX, 1.0);
            solverResult.FY.ShouldBe(spanEndResult.FY, 1.0);
            solverResult.FZ.ShouldBe(spanEndResult.FZ, 1.0);
            solverResult.MX.ShouldBe(spanEndResult.MX, 1.0);
            solverResult.MY.ShouldBe(spanEndResult.MY, 1.0);
            solverResult.MZ.ShouldBe(spanEndResult.MZ, 1.0);
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
