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
    [Description("Fragment storing identifier information of the object in Tekla Structural Designer: a readable identifier, and the Guid Tekla Structural Designer itself uses for the object.")]
    public class TeklaStructuralDesignerId : IAdapterId, IPersistentAdapterId
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        [Description("The identifier of the object in Tekla Structural Designer, as used to request it and its results. For a Bar this is a string of the form 'MemberName:SpanIndex', matching the member name shown in the Tekla Structural Designer interface. For a Loadcase or LoadCombination it is the case identifier, the same as its Name. For a Node it is the Guid of its construction point, which has no readable name.")]
        public virtual object Id { get; set; }

        [Description("The Guid Tekla Structural Designer uses for the object - the span for a Bar, the construction point for a Node, the case for a Loadcase or LoadCombination. Unlike the readable Id, it is unaffected by renaming the object.")]
        public virtual object PersistentId { get; set; }

        /***************************************************/
    }
}
