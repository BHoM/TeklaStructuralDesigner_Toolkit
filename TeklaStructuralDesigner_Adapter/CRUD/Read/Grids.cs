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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BH.oM.Spatial.SettingOut;
using TSD.API.Remoting.Structure;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Grids)                        ****/
        /***************************************************/

        // Every grid line of every architectural grid in the model, each as a BHoM Grid.
        //
        // The lines are read from the architectural grid itself rather than from the levels it is shown
        // on: a level repeats every line of every grid that applies to it, so reading them there would
        // return each line once per level.
        //
        // The ids of the request are matched against the Grid's Name, the grid line's label.
        private List<Grid> ReadGrids(IList ids)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;
            List<Grid> result = new List<Grid>();

            List<IArchitecturalGrid> grids;
            try
            {
                grids = Async.RunSync(ct => m_Model.GetArchitecturalGridsAsync(null, ct), timeout, "reading architectural grids").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read architectural grids from Tekla Structural Designer, so no Grids have been pulled. " + e.Message);
                return result;
            }

            Dictionary<string, int> skipReasons = new Dictionary<string, int>();

            foreach (IArchitecturalGrid grid in grids)
            {
                List<IConstructionHelper> lines;
                try
                {
                    lines = Async.RunSync(ct => grid.GetConstructionHelpersAsync(null, ct), timeout, "reading the lines of grid '" + grid.Name + "'").ToList();
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning("Failed to read the lines of grid '" + grid.Name + "'; it has been skipped. " + e.Message);
                    continue;
                }

                TsdPlane plane = grid.Plane.ValueOrDefault();

                foreach (IConstructionHelper line in lines)
                {
                    string skipReason;
                    Grid converted = line.ToBHoM(plane, out skipReason);
                    if (converted == null)
                        Count(skipReasons, skipReason);
                    else
                        result.Add(converted);
                }
            }

            foreach (KeyValuePair<string, int> reason in skipReasons)
                Engine.Base.Compute.RecordWarning(reason.Value + " grid line(s) were skipped because " + reason.Key + ".");

            // A BHoM Grid has nowhere to say which architectural grid it belongs to, so two architectural
            // grids that both have a line 'A' come through as two Grids named 'A'.
            int repeated = result.GroupBy(g => g.Name).Count(g => g.Count() > 1);
            if (repeated > 0)
                Engine.Base.Compute.RecordWarning(repeated + " grid line name(s) are used by more than one architectural grid, so more than one pulled Grid carries each of those names.");

            HashSet<string> requested = RequestedIds(ids);
            if (requested == null)
                return result;

            return result.Where(g => g.Name != null && requested.Contains(g.Name)).ToList();
        }

        /***************************************************/
    }
}
