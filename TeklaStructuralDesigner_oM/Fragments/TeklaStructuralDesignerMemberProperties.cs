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
    [Description("Fragment carrying the Tekla Structural Designer member and span information that has no equivalent on a BHoM Bar. Attached to Bars pulled from Tekla Structural Designer so that the physical member a Bar came from, and the level and element group it belongs to, remain traceable.")]
    public class TeklaStructuralDesignerMemberProperties : IFragment
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        [Description("Name of the physical member in Tekla Structural Designer, as shown in its interface. A member may contain several spans, each of which becomes a separate BHoM Bar.")]
        public virtual string MemberName { get; set; } = "";

        [Description("Index of the physical member in Tekla Structural Designer. Negative if unknown.")]
        public virtual int MemberIndex { get; set; } = -1;

        [Description("Unique identifier of the physical member in Tekla Structural Designer. Stable within a modelling session.")]
        public virtual Guid MemberId { get; set; } = Guid.Empty;

        [Description("Index of the span within its parent member, counted from zero. A single span member has index 0. Negative if unknown.")]
        public virtual int SpanIndex { get; set; } = -1;

        [Description("Unique identifier of the span in Tekla Structural Designer. Stable within a modelling session.")]
        public virtual Guid SpanId { get; set; } = Guid.Empty;

        [Description("Name of the level the span is associated with, taken from the construction plane of its end points. Empty if no level could be resolved.")]
        public virtual string LevelName { get; set; } = "";

        [Description("Name of the innermost Tekla Structural Designer element group containing the member. Empty if the member belongs to no group.")]
        public virtual string ElementGroupName { get; set; } = "";

        [Description("Name of the element group containing the group named by ElementGroupName. Empty if that group has no parent.")]
        public virtual string ParentElementGroupName { get; set; } = "";

        [Description("Material grade of the span as named in Tekla Structural Designer, for example 'S355'. Retained verbatim, since the Bar's material may be a BHoM library material whose name differs slightly.")]
        public virtual string MaterialGrade { get; set; } = "";

        [Description("Section name of the span as named in Tekla Structural Designer, for example 'UB 457x191x67'. Retained verbatim, since the Bar's section may be a BHoM library section whose name differs slightly.")]
        public virtual string SectionName { get; set; } = "";

        [Description("Construction type of the member in Tekla Structural Designer, for example 'SteelBeam' or 'SteelBrace'.")]
        public virtual string MemberConstruction { get; set; } = "";

        /***************************************************/
    }
}
