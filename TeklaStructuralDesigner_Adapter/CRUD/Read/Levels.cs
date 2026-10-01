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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BH.oM.Spatial.SettingOut;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Levels)                       ****/
        /***************************************************/

        // Every level in the model as a BHoM Level, lowest first. The ids of the request are matched
        // against the Level's Name - see Convert.LevelName.
        private List<Level> ReadLevels(IList ids)
        {
            List<Level> levels = ReadTsdLevels(TeklaStructuralDesignerConfig.TimeoutSeconds, "no Levels have been pulled")
                .Select(plane => plane.ToBHoM())
                .OrderBy(level => level.Elevation)
                .ToList();

            HashSet<string> requested = RequestedIds(ids);
            if (requested == null)
                return levels;

            return levels.Where(level => requested.Contains(level.Name)).ToList();
        }

        /***************************************************/
    }
}
