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

using BH.oM.Spatial.SettingOut;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // A level is a horizontal construction plane. Its Name is the reference LevelName gives, which
        // is also what a Bar's TeklaStructuralDesignerMemberProperties.LevelName holds, so a pulled Bar
        // can be tied to its Level by name.
        public static Level ToBHoM(this IHorizontalConstructionPlane plane)
        {
            Level level = new Level
            {
                Name = plane.LevelName(),
                Elevation = plane.Level.ValueOrDefault(0.0) * LengthScale,
            };

            return level.SetIdentity(level.Name, plane.Id);
        }

        /***************************************************/

        // The reference a level is known by: its long reference, or its short one if it has no long
        // one.
        public static string LevelName(this IHorizontalConstructionPlane plane)
        {
            string name = plane.LongReference.ValueOrDefault("");
            if (string.IsNullOrWhiteSpace(name))
                name = plane.ShortReference.ValueOrDefault("");

            return string.IsNullOrWhiteSpace(name) ? "Unassigned" : name.Trim();
        }

        /***************************************************/
    }
}
