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
    // Message text in one place, as in Robot_Adapter. Each says what went wrong and what to do.
    internal static class ErrorMessages
    {
        /***************************************************/
        /**** Public Methods - Connection               ****/
        /***************************************************/

        internal static string NotConnected()
        {
            return "The link to Tekla Structural Designer is not established. Create the TeklaStructuralDesignerAdapter with 'active' set to true, and make sure Tekla Structural Designer is running with the model open.";
        }

        /***************************************************/

        internal static string NoRunningInstance()
        {
            return "No running instance of Tekla Structural Designer was found. Start Tekla Structural Designer and open the model before activating the adapter: the Remoting API attaches to a running instance and this adapter will not open a model for you.";
        }

        /***************************************************/

        internal static string DiscoveryFailed(string detail)
        {
            return "Failed to look for running instances of Tekla Structural Designer. " + detail +
                   " If this mentions a missing native library, the toolkit's dependency folder is incomplete: rebuild TeklaStructuralDesigner_Toolkit or reinstall BHoM.";
        }

        /***************************************************/

        internal static string MultipleInstances(IEnumerable<string> descriptions)
        {
            return "More than one instance of Tekla Structural Designer is running, so it is ambiguous which one to read. " +
                   "Pass the model path, or its file name, as 'modelPath' to choose one. Running instances are:" + Bullets(descriptions);
        }

        /***************************************************/

        internal static string NoMatchForPath(string modelPath, IEnumerable<string> descriptions)
        {
            return "No running instance of Tekla Structural Designer has '" + modelPath + "' open. Running instances are:" + Bullets(descriptions);
        }

        /***************************************************/

        internal static string AmbiguousMatchForPath(string modelPath, IEnumerable<string> descriptions)
        {
            return "More than one running instance of Tekla Structural Designer matches '" + modelPath +
                   "'. Pass the full model path to choose one. Matching instances are:" + Bullets(descriptions);
        }

        /***************************************************/

        internal static string NoDocument(string description)
        {
            return "Tekla Structural Designer is running (" + description + ") but no document could be accessed. Open the model and try again.";
        }

        /***************************************************/

        internal static string NoModelOpen(string description)
        {
            return "Tekla Structural Designer is running (" + description + ") but no model is open. Open the model and try again.";
        }

        /***************************************************/

        internal static string ModelAttachFailed(string description, string detail)
        {
            return "Failed to attach to the model open in Tekla Structural Designer (" + description + "). " + detail;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        private static string Bullets(IEnumerable<string> items)
        {
            List<string> list = items?.ToList() ?? new List<string>();
            if (list.Count == 0)
                return " (none could be described).";

            return "\n  - " + string.Join("\n  - ", list);
        }

        /***************************************************/
    }
}
