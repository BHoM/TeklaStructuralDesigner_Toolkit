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
using BH.oM.Base;

namespace BH.oM.Adapters.TeklaStructuralDesigner
{
    [Description("Settings controlling how the TeklaStructuralDesignerAdapter connects to Tekla Structural Designer. Supplied once when the adapter is created, unlike a pull configuration which is supplied per action.")]
    public class TeklaStructuralDesignerConfig : BHoMObject
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        [Description("How long to wait, in seconds, for Tekla Structural Designer to respond to a single API call before giving up. Zero or negative waits indefinitely. The default is generous because a first call against a large model can be slow.")]
        public virtual int TimeoutSeconds { get; set; } = 300;

        [Description("If true, and more than one instance of Tekla Structural Designer is running while no model path was supplied to disambiguate them, the adapter binds to the first instance it finds. If false it refuses to guess and reports an error naming every instance. Leave this false: binding to an arbitrary instance means results can silently come from the wrong model.")]
        public virtual bool AllowArbitraryInstanceSelection { get; set; } = false;

        /***************************************************/
    }
}
