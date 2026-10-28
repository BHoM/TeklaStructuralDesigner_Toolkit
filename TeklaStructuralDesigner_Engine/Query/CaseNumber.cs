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

        [Description("The BHoM case number of one limit state of a Tekla Structural Designer combination: 1000 + the combination number for Strength, 2000 + it for Service. Loadcases keep their own Tekla Structural Designer number, so the three never collide. Returns -1, with an error, for a combination number outside 0-999, which the convention has no room for.")]
        [Input("combinationNumber", "The combination's number as shown in Tekla Structural Designer - the number its name starts with.")]
        [Input("limitState", "The limit state to number.")]
        [Output("caseNumber", "The BHoM LoadCombination Number, and the ResultCase of results read for it.")]
        public static int CaseNumber(int combinationNumber, TeklaStructuralDesignerLimitState limitState)
        {
            if (combinationNumber < 0 || combinationNumber >= NumberBand)
            {
                Base.Compute.RecordError("Tekla Structural Designer combination number " + combinationNumber + " is outside 0-" + (NumberBand - 1) +
                    ", so it cannot be given a case number without colliding with another case.");
                return -1;
            }

            return Band(limitState) * NumberBand + combinationNumber;
        }

        /***************************************************/
    }
}
