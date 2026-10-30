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
    [Description("The type of a slab in Tekla Structural Designer, mirroring TSD.API.Remoting.Structure.SlabType. Mirrored rather than referenced directly because a BHoM _oM assembly must not depend on an external software API.")]
    public enum TeklaStructuralDesignerSlabType
    {
        [Description("Not known, or the Panel was not pulled from a slab item.")]
        Undefined,

        [Description("A precast slab: planks, with or without a topping.")]
        Precast,

        [Description("A composite slab on a metal deck.")]
        Composite,

        [Description("A flat concrete slab.")]
        Flat,

        [Description("A concrete slab supported on beams.")]
        OnBeams,

        [Description("A ribbed concrete slab.")]
        Rib,

        [Description("A waffle concrete slab.")]
        Waffle,

        [Description("A foundation mat.")]
        FoundationMat,

        [Description("A general slab.")]
        General,
    }
}
