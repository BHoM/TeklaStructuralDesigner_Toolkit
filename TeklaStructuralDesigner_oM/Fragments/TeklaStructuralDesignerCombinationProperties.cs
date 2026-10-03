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

using System;
using System.ComponentModel;
using BH.oM.Base;

namespace BH.oM.Adapters.TeklaStructuralDesigner
{
    [Description("Fragment carrying the Tekla Structural Designer information behind a pulled LoadCombination. Each Tekla Structural Designer combination is pulled twice, once per limit state, and this is what ties the two back to the one combination they came from.")]
    public class TeklaStructuralDesignerCombinationProperties : IFragment
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        [Description("Which limit state this LoadCombination represents: Strength (factors and results Tekla Structural Designer designs with) or Service (factors and results it checks deflection with).")]
        public virtual TeklaStructuralDesignerLimitState LimitState { get; set; } = TeklaStructuralDesignerLimitState.Strength;

        [Description("The combination's number as shown in Tekla Structural Designer - the number its name starts with. The LoadCombination's own Number is this plus 1000 for Strength or 2000 for Service.")]
        public virtual int CombinationNumber { get; set; } = -1;

        [Description("The combination's name as shown in Tekla Structural Designer, without the limit state prefix the LoadCombination's Name carries.")]
        public virtual string CombinationName { get; set; } = "";

        [Description("The Guid Tekla Structural Designer uses for the combination. The same for the Strength and the Service LoadCombination pulled from it; the Guid results are read with is on the TeklaStructuralDesignerId fragment, and differs between the two.")]
        public virtual Guid CombinationId { get; set; } = Guid.Empty;

        /***************************************************/
    }
}
