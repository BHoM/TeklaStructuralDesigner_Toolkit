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
    [Description("The shape of a roof in Tekla Structural Designer, mirroring TSD.API.Remoting.Structure.RoofType. Mirrored rather than referenced directly because a BHoM _oM assembly must not depend on an external software API.")]
    public enum TeklaStructuralDesignerRoofType
    {
        [Description("Not known, or the Panel was not pulled from a roof.")]
        Undefined,

        [Description("A roof whose shape Tekla Structural Designer works out automatically.")]
        Auto,

        [Description("A flat roof.")]
        Flat,

        [Description("A mono-pitch roof.")]
        MonoPitch,

        [Description("A duo-pitch roof.")]
        DuoPitch,

        [Description("The gable end of a hipped roof.")]
        HipGable,

        [Description("The main slope of a hipped roof.")]
        HipMain,

        [Description("A mansard roof.")]
        Mansard,
    }
}
