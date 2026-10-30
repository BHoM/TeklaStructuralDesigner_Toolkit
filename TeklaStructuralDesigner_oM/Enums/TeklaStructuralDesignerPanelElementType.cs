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
    [Description("The kind of Tekla Structural Designer element a Panel was pulled from.")]
    public enum TeklaStructuralDesignerPanelElementType
    {
        [Description("Not known.")]
        Undefined,

        [Description("A slab item: one piece of a slab, carrying the slab's structural property.")]
        SlabItem,

        [Description("A panel of a structural wall. Each panel of a wall is pulled as a Panel of its own.")]
        StructuralWall,

        [Description("A roof, which only collects load and carries a LoadingPanelProperty.")]
        Roof,

        [Description("A wind wall, which only collects wind load and carries a LoadingPanelProperty.")]
        WindWall,
    }
}
