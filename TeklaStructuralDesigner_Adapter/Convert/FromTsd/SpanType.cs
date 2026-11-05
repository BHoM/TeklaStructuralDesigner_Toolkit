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

using BH.oM.Adapters.TeklaStructuralDesigner;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // A switch rather than a cast, so a value added to either enum is reported instead of mapping
        // wrongly. recognised is false for a value newer than this toolkit, which is pulled as Undefined.
        public static TeklaStructuralDesignerSpanType FromTsd(this DecompositionType decomposition, out bool recognised)
        {
            recognised = true;

            switch (decomposition)
            {
                case DecompositionType.OneWay:
                    return TeklaStructuralDesignerSpanType.OneWay;
                case DecompositionType.TwoWay:
                    return TeklaStructuralDesignerSpanType.TwoWay;
                case DecompositionType.Unknown:
                    return TeklaStructuralDesignerSpanType.Undefined;
                default:
                    recognised = false;
                    return TeklaStructuralDesignerSpanType.Undefined;
            }
        }

        /***************************************************/
    }
}
