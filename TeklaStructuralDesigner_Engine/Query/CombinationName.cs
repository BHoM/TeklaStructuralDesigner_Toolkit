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
        /****            Public Methods                 ****/
        /***************************************************/

        [Description("The name a LoadCombination pulled from Tekla Structural Designer is given: the limit state followed by the combination's own name, for example 'Strength 48 1.35Gk + 1.5LL' and 'Service 48 1.35Gk + 1.5LL'.")]
        [Input("combinationName", "The combination's name as shown in Tekla Structural Designer.")]
        [Input("limitState", "The limit state the LoadCombination represents.")]
        [Output("name", "The LoadCombination's Name.")]
        public static string CombinationName(string combinationName, TeklaStructuralDesignerLimitState limitState)
        {
            return limitState + " " + (combinationName ?? "").Trim();
        }

        /***************************************************/
    }
}
