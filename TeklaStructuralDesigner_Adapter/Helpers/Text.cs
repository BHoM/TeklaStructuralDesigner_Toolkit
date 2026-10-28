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

using System.Collections.Generic;
using System.Linq;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Helpers
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // The first few names of a list for a message, as in "A, B, C and 4 more".
        public static string Summary(IEnumerable<string> names, int shown = 10)
        {
            List<string> all = names.Select(n => string.IsNullOrWhiteSpace(n) ? "<unnamed>" : n).ToList();
            string list = string.Join(", ", all.Take(shown));
            return all.Count > shown ? list + " and " + (all.Count - shown) + " more" : list;
        }

        /***************************************************/
    }
}
