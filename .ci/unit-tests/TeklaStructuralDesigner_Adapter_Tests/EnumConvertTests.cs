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
        [Description("A value newer than the toolkit is reported as not recognised and pulled as Undefined.")]
        public void UnknownFutureValuesAreNotRecognised()
        {
            // Arrange
            bool usageRecognised, slabRecognised, wallRecognised, roofRecognised, spanRecognised;

            // Act
            StructuralUsage1D usage = ((MemberConstruction)999).FromTsdStructuralUsage(out usageRecognised);
            TeklaStructuralDesignerSlabType slab = ((SlabType)999).FromTsd(out slabRecognised);
            TeklaStructuralDesignerWallType wall = ((StructuralWallType)999).FromTsd(out wallRecognised);
            TeklaStructuralDesignerRoofType roof = ((RoofType)999).FromTsd(out roofRecognised);
            TeklaStructuralDesignerSpanType span = ((DecompositionType)999).FromTsd(out spanRecognised);

            // Assert
            usageRecognised.ShouldBeFalse();
            slabRecognised.ShouldBeFalse();
            wallRecognised.ShouldBeFalse();
            roofRecognised.ShouldBeFalse();
            spanRecognised.ShouldBeFalse();
            usage.ShouldBe(StructuralUsage1D.Undefined);
            slab.ShouldBe(TeklaStructuralDesignerSlabType.Undefined);
            wall.ShouldBe(TeklaStructuralDesignerWallType.Undefined);
            roof.ShouldBe(TeklaStructuralDesignerRoofType.Undefined);
            span.ShouldBe(TeklaStructuralDesignerSpanType.Undefined);
        }

        /***************************************************/

        [Test]
        [Description("Every slab type in the API maps to the oM value of the same name, and Unknown to Undefined.")]
        public void EverySlabTypeMapsByName()
        {
            foreach (SlabType value in Enum.GetValues(typeof(SlabType)))
                ShouldMapByName(value, value.FromTsd(out bool recognised), recognised);
        }

        /***************************************************/

        [Test]
        [Description("Every wall type in the API maps to the oM value of the same name, and Unknown to Undefined.")]
        public void EveryWallTypeMapsByName()
        {
            foreach (StructuralWallType value in Enum.GetValues(typeof(StructuralWallType)))
                ShouldMapByName(value, value.FromTsd(out bool recognised), recognised);
        }

        /***************************************************/

        [Test]
        [Description("Every roof type in the API maps to the oM value of the same name, and Unknown to Undefined.")]
        public void EveryRoofTypeMapsByName()
        {
            foreach (RoofType value in Enum.GetValues(typeof(RoofType)))
                ShouldMapByName(value, value.FromTsd(out bool recognised), recognised);
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

        [Test]
        [Description("Every panel element type maps to the Tekla Structural Designer entity of the same name, and Undefined to Unknown.")]
        public void EveryPanelElementTypeMapsToItsEntity()
        {
            foreach (TeklaStructuralDesignerPanelElementType value in Enum.GetValues(typeof(TeklaStructuralDesignerPanelElementType)))
            {
                TsdEntityType entity = value.ToTsd();
                if (value == TeklaStructuralDesignerPanelElementType.Undefined)
                    entity.ShouldBe(TsdEntityType.Unknown);
                else
                    entity.ToString().ShouldBe(value.ToString());
            }
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
