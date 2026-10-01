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
using BH.oM.Base.Attributes;

namespace BH.Engine.Adapters.TeklaStructuralDesigner
{
    public static partial class Query
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        [Description("The number a Tekla Structural Designer loadcase or combination name starts with - the reference number Tekla Structural Designer shows for it, as in '48 1.35Gk + 1.5LL' or '30 LL CONCOURSE'. Returns -1 if the name does not start with a number.")]
        [Input("name", "The loadcase or combination name as shown in Tekla Structural Designer.")]
        [Output("number", "The reference number, or -1.")]
        public static int ReferenceNumber(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return -1;

            string trimmed = name.TrimStart();
            int end = 0;
            while (end < trimmed.Length && char.IsDigit(trimmed[end]))
                end++;

            int number;
            return end > 0 && int.TryParse(trimmed.Substring(0, end), out number) ? number : -1;
        }

        /***************************************************/
    }
}
