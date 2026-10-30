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
using BH.Engine.Adapters.TeklaStructuralDesigner;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Loads;
using NUnit.Framework;
using Shouldly;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    // Headless: these need no Tekla Structural Designer.
    public class EngineQueryTests
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        [TestCase(1048, 48)]
        [TestCase(2048, 48)]
        [TestCase(1000, 0)]
        [TestCase(48, -1)]
        [TestCase(3048, -1)]
        [TestCase(-1048, -1)]
        public void CombinationNumberReversesCaseNumber(int caseNumber, int expected)
        {
            Query.CombinationNumber(caseNumber).ShouldBe(expected);
        }

        /***************************************************/

        [Test]
        public void LimitStateComesFromTheFragmentFirst()
        {
            LoadCombination combination = new LoadCombination { Number = 1048 };
            combination.Fragments.Add(new TeklaStructuralDesignerCombinationProperties { LimitState = TeklaStructuralDesignerLimitState.Service });

            combination.LimitState().ShouldBe(TeklaStructuralDesignerLimitState.Service);
        }

        /***************************************************/

        [TestCase(1048, TeklaStructuralDesignerLimitState.Strength)]
        [TestCase(2048, TeklaStructuralDesignerLimitState.Service)]
        public void LimitStateFallsBackToTheNumber(int number, TeklaStructuralDesignerLimitState expected)
        {
            new LoadCombination { Number = number }.LimitState().ShouldBe(expected);
        }

        /***************************************************/

        [Test]
        public void FilterByLimitStateKeepsOrderAndSkipsNulls()
        {
            List<LoadCombination> combinations = new List<LoadCombination>
            {
                new LoadCombination { Name = "a", Number = 2001 },
                new LoadCombination { Name = "b", Number = 1001 },
                null,
                new LoadCombination { Name = "c", Number = 2002 },
            };

            combinations.FilterByLimitState(TeklaStructuralDesignerLimitState.Service).Select(c => c.Name).ShouldBe(new[] { "a", "c" });
            ((List<LoadCombination>)null).FilterByLimitState(TeklaStructuralDesignerLimitState.Service).ShouldBeEmpty();
        }

        /***************************************************/
    }
}
