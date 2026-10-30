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
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Base.Attributes;

namespace BH.Engine.Adapters.TeklaStructuralDesigner
{
    public static partial class Query
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        [Description("The Tekla Structural Designer combination number a BHoM case number was made from: 48 for 1048 (its Strength case) or 2048 (its Service case). Returns -1 for a number that is not a combination case number, such as a loadcase's.")]
        [Input("caseNumber", "A LoadCombination Number or a result's ResultCase, as pulled from Tekla Structural Designer.")]
        [Output("combinationNumber", "The combination's number as shown in Tekla Structural Designer.")]
        public static int CombinationNumber(int caseNumber)
        {
            TeklaStructuralDesignerLimitState limitState;
            return LimitStateFromNumber(caseNumber, out limitState) ? caseNumber % NumberBand : -1;
        }

        /***************************************************/
    }
}
