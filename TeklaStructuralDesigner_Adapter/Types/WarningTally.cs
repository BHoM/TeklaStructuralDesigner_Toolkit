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
    // Counts identical messages so each is reported once with its count, rather than once per object
    // on a model with thousands of them.
    internal sealed class WarningTally
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        private readonly Dictionary<string, int> m_Counts = new Dictionary<string, int>();

        /***************************************************/
        /**** Public Properties                         ****/
        /***************************************************/

        public int Total
        {
            get { return m_Counts.Values.Sum(); }
        }

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // message reads on from its count, as in "3 " + "load(s) were skipped because ...".
        public void Add(string message, int count = 1)
        {
            int existing;
            m_Counts.TryGetValue(message, out existing);
            m_Counts[message] = existing + count;
        }

        /***************************************************/

        public void Skip(string what, string reason)
        {
            Add(what + " were skipped because " + (string.IsNullOrWhiteSpace(reason) ? "they could not be converted" : reason));
        }

        /***************************************************/

        // One warning per message.
        public void Report()
        {
            foreach (KeyValuePair<string, int> message in m_Counts)
                Engine.Base.Compute.RecordWarning(message.Value + " " + message.Key + ".");
        }

        /***************************************************/

        // The counts as a list, as in "3 x Opening, 2 x Load".
        public string Describe()
        {
            return string.Join(", ", m_Counts.Select(c => c.Value + " x " + c.Key));
        }

        /***************************************************/
    }
}
