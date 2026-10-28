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

using BH.oM.Adapters.TeklaStructuralDesigner;

namespace BH.Engine.Adapters.TeklaStructuralDesigner
{
    public static partial class Query
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        // The case numbering shared by CaseNumber, CombinationNumber and LimitState. Loadcases keep their
        // own number; a combination's limit state is the thousands, so combination 48 is 1048 for
        // Strength and 2048 for Service. Nothing collides while every number is below 1000.
        private const int NumberBand = 1000;
        private const int StrengthBand = 1;
        private const int ServiceBand = 2;

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        private static int Band(TeklaStructuralDesignerLimitState limitState)
        {
            return limitState == TeklaStructuralDesignerLimitState.Service ? ServiceBand : StrengthBand;
        }

        /***************************************************/

        // The limit state a case number is in, or false if it is not a combination case number.
        private static bool LimitState(int caseNumber, out TeklaStructuralDesignerLimitState limitState)
        {
            int band = caseNumber / NumberBand;
            limitState = band == ServiceBand ? TeklaStructuralDesignerLimitState.Service : TeklaStructuralDesignerLimitState.Strength;
            return caseNumber >= 0 && (band == StrengthBand || band == ServiceBand);
        }

        /***************************************************/
    }
}
