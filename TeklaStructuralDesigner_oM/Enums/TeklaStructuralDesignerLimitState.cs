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

namespace BH.oM.Adapters.TeklaStructuralDesigner
{
    [Description("The limit state a load combination pulled from Tekla Structural Designer represents. Tekla Structural Designer holds a strength, a service and a quasi permanent service factor against every loadcase of one combination, and solves the strength and service sets separately. BHoM's LoadCombination holds one factor per loadcase, so each Tekla Structural Designer combination is pulled as one LoadCombination per limit state.")]
    public enum TeklaStructuralDesignerLimitState
    {
        [Description("The ultimate limit state: the combination's strength factors, and the results Tekla Structural Designer designs with. Numbered 1000 + the Tekla Structural Designer combination number.")]
        Strength,

        [Description("The serviceability limit state: the combination's service factors, and the results Tekla Structural Designer checks deflection with. Numbered 2000 + the Tekla Structural Designer combination number.")]
        Service,
    }
}
