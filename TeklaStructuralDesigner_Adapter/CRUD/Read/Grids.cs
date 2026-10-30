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
using TSD.API.Remoting.Structure;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // One Grid per grid line, read from each architectural grid rather than each level, which would
        // repeat every line once per level. Ids match the Grid's Name.
        private List<Grid> ReadGrids(IList ids)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;
            List<Grid> result = new List<Grid>();

            IEnumerable<IArchitecturalGrid> grids;
            if (!Async.TryRunSync(ct => m_Model.GetArchitecturalGridsAsync(null, ct), timeout, "reading architectural grids", "no Grids have been pulled", out grids, true))
                return result;

            WarningTally warnings = new WarningTally();

            foreach (IArchitecturalGrid grid in grids)
            {
                IEnumerable<IConstructionHelper> lines;
                if (!Async.TryRunSync(ct => grid.GetConstructionHelpersAsync(null, ct), timeout, "reading the lines of grid '" + grid.Name + "'", "it has been skipped", out lines))
                    continue;

                TsdPlane plane = grid.Plane.ValueOrDefault();

                foreach (IConstructionHelper line in lines)
                {
                    string skipReason;
                    Grid converted = line.FromTsdGrid(plane, out skipReason);
                    if (converted == null)
                        warnings.Skip("grid line(s)", skipReason);
                    else
                        result.Add(converted);
                }
            }

            warnings.Report();

            // A Grid has nowhere to record its architectural grid, so line names can repeat.
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
