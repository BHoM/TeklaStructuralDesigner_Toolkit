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
using BH.Adapter.TeklaStructuralDesigner;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;
using NUnit.Framework;
using Shouldly;
using TSD.API.Remoting.Structure;
using TsdEntityType = TSD.API.Remoting.Common.EntityType;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    // Headless: the enum switches are pure, so these need no Tekla Structural Designer.
    public class EnumConvertTests
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        [Test]
        [Description("Every member construction type in the API maps to a StructuralUsage1D, and only Unknown maps to Undefined.")]
        public void EveryMemberConstructionIsRecognised()
        {
            foreach (MemberConstruction construction in Enum.GetValues(typeof(MemberConstruction)))
            {
                // Act
                bool recognised;
                StructuralUsage1D usage = construction.FromTsdStructuralUsage(out recognised);

                // Assert
                recognised.ShouldBeTrue(construction.ToString());
                (usage == StructuralUsage1D.Undefined).ShouldBe(construction == MemberConstruction.Unknown, construction.ToString());
            }
        }

        /***************************************************/

        [TestCase(MemberConstruction.SteelBrace)]
        [TestCase(MemberConstruction.TimberBrace)]
        [TestCase(MemberConstruction.ColdFormedBrace)]
        [TestCase(MemberConstruction.GeneralMaterialBrace)]
        [TestCase(MemberConstruction.SteelTie)]
        [TestCase(MemberConstruction.SteelTrussInternal)]
        [TestCase(MemberConstruction.TimberTrussInternal)]
        [TestCase(MemberConstruction.ColdFormedTrussInternal)]
        [Description("Braces, ties and truss internal members are braces.")]
        public void BracesTiesAndTrussInternalsAreBraces(MemberConstruction construction)
        {
            construction.FromTsdStructuralUsage(out _).ShouldBe(StructuralUsage1D.Brace);
        }

        /***************************************************/

        [TestCase(MemberConstruction.SteelTrussMemberTop)]
        [TestCase(MemberConstruction.TimberTrussMemberBottom)]
        [TestCase(MemberConstruction.ColdFormedTrussMemberSide)]
        [TestCase(MemberConstruction.Purlin)]
        [TestCase(MemberConstruction.ConcreteWallBeamElement)]
        [Description("Truss chords, purlins and wall beam elements are beams.")]
        public void ChordsPurlinsAndWallBeamElementsAreBeams(MemberConstruction construction)
        {
            construction.FromTsdStructuralUsage(out _).ShouldBe(StructuralUsage1D.Beam);
        }

        /***************************************************/

        [TestCase(MemberConstruction.SteelGablePost)]
        [TestCase(MemberConstruction.ColdFormedParapetPost)]
        [TestCase(MemberConstruction.ConcreteWallColumnElement)]
        [Description("Posts and wall column elements are columns.")]
        public void PostsAndWallColumnElementsAreColumns(MemberConstruction construction)
        {
            construction.FromTsdStructuralUsage(out _).ShouldBe(StructuralUsage1D.Column);
        }

        /***************************************************/

        [Test]
        [Description("Every span type in the API maps to the oM value of the same name, and Unknown to Undefined.")]
        public void EverySpanTypeMapsByName()
        {
            foreach (DecompositionType value in Enum.GetValues(typeof(DecompositionType)))
                ShouldMapByName(value, value.FromTsd(out bool recognised), recognised);
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        private static void ShouldMapByName(Enum tsdValue, Enum bhomValue, bool recognised)
        {
            recognised.ShouldBeTrue(tsdValue.ToString());

            if (tsdValue.ToString() == "Unknown")
                bhomValue.ToString().ShouldBe("Undefined");
            else
                bhomValue.ToString().ShouldBe(tsdValue.ToString());
        }

        /***************************************************/
    }
}
