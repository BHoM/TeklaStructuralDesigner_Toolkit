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
using BH.Engine.Adapters.TeklaStructuralDesigner;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Loads;
using NUnit.Framework;
using Shouldly;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    // Headless: these need no Tekla Structural Designer.
    public class AdapterHelperTests
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        [TestCase(48, TeklaStructuralDesignerLimitState.Strength, 1048)]
        [TestCase(48, TeklaStructuralDesignerLimitState.Service, 2048)]
        [TestCase(0, TeklaStructuralDesignerLimitState.Strength, 1000)]
        [TestCase(999, TeklaStructuralDesignerLimitState.Service, 2999)]
        public void CaseNumberEncodesLimitStateInTheThousands(int combinationNumber, TeklaStructuralDesignerLimitState limitState, int expected)
        {
            Helpers.CaseNumber(combinationNumber, limitState).ShouldBe(expected);
        }

        /***************************************************/

        [TestCase(-1)]
        [TestCase(1000)]
        public void CaseNumberRefusesNumbersOutsideTheBand(int combinationNumber)
        {
            BH.Engine.Base.Compute.ClearCurrentEvents();

            Helpers.CaseNumber(combinationNumber, TeklaStructuralDesignerLimitState.Strength).ShouldBe(-1);
            BH.Engine.Base.Query.CurrentEvents().Any(e => e.Type == BH.oM.Base.Debugging.EventType.Error).ShouldBeTrue();
        }

        /***************************************************/

        [TestCase(0, TeklaStructuralDesignerLimitState.Strength)]
        [TestCase(48, TeklaStructuralDesignerLimitState.Strength)]
        [TestCase(48, TeklaStructuralDesignerLimitState.Service)]
        [TestCase(999, TeklaStructuralDesignerLimitState.Service)]
        [Description("The adapter writes the case number bands and the Engine reads them back, so the two must agree.")]
        public void EngineReadsBackTheCaseNumbersTheAdapterWrites(int combinationNumber, TeklaStructuralDesignerLimitState limitState)
        {
            // Act
            int caseNumber = Helpers.CaseNumber(combinationNumber, limitState);

            // Assert
            Query.CombinationNumber(caseNumber).ShouldBe(combinationNumber);
            new LoadCombination { Number = caseNumber }.LimitState().ShouldBe(limitState);
        }

        /***************************************************/

        [Test]
        public void CombinationNamePrefixesTheLimitState()
        {
            Helpers.CombinationName(" 48 1.35Gk + 1.5LL ", TeklaStructuralDesignerLimitState.Service).ShouldBe("Service 48 1.35Gk + 1.5LL");
            Helpers.CombinationName(null, TeklaStructuralDesignerLimitState.Strength).ShouldBe("Strength ");
        }

        /***************************************************/

        [TestCase("48 1.35Gk + 1.5LL", 48)]
        [TestCase("  30 LL CONCOURSE", 30)]
        [TestCase("7", 7)]
        [TestCase("LL 30", -1)]
        [TestCase("", -1)]
        [TestCase(null, -1)]
        public void ReferenceNumberReadsTheLeadingNumber(string name, int expected)
        {
            Helpers.ReferenceNumber(name).ShouldBe(expected);
        }

        /***************************************************/
    }
}
