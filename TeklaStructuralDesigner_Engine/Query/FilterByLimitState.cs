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
using System.ComponentModel;
using System.Linq;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Base.Attributes;
using BH.oM.Structure.Loads;

namespace BH.Engine.Adapters.TeklaStructuralDesigner
{
    public static partial class Query
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        [Description("The LoadCombinations of one limit state, from a list pulled from Tekla Structural Designer - for example every Service combination, to request deflection results for.")]
        [Input("combinations", "LoadCombinations pulled from Tekla Structural Designer.")]
        [Input("limitState", "The limit state to keep.")]
        [Output("combinations", "The combinations of that limit state, in the order given.")]
        public static List<LoadCombination> FilterByLimitState(this List<LoadCombination> combinations, TeklaStructuralDesignerLimitState limitState)
        {
            if (combinations == null)
                return new List<LoadCombination>();

            return combinations.Where(c => c != null && c.LimitState() == limitState).ToList();
        }

        /***************************************************/
    }
}
