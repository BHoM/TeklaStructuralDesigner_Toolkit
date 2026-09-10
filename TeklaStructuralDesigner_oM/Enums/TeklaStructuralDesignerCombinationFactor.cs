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

using System.ComponentModel;

namespace BH.oM.Adapters.TeklaStructuralDesigner
{
    [Description("Which of Tekla Structural Designer's per loadcase factors is used when building a BHoM LoadCombination. Tekla Structural Designer holds three factors against every loadcase in a combination; BHoM's LoadCombination holds one factor per case, so exactly one of the three has to be chosen.")]
    public enum TeklaStructuralDesignerCombinationFactor
    {
        [Description("The ultimate limit state factor. The default, because a schedule of design forces is normally built from strength combinations.")]
        Strength,

        [Description("The serviceability limit state factor, for deflection and similar checks.")]
        Service,

        [Description("The quasi permanent serviceability factor, for long term effects such as creep.")]
        ServiceQuasi,
    }
}
