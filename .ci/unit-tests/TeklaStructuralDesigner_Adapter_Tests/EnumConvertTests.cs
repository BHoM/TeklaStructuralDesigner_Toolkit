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
