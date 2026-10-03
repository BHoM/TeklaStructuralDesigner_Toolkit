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
    [Description("Fragment carrying the Tekla Structural Designer information that has no equivalent on a BHoM Panel. Attached to Panels pulled from Tekla Structural Designer so that the slab, wall, roof or wind wall a Panel came from remains traceable.")]
    public class TeklaStructuralDesignerPanelProperties : IFragment
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        [Description("The kind of Tekla Structural Designer element the Panel was pulled from: 'SlabItem', 'StructuralWall', 'Roof' or 'WindWall'.")]
        public virtual string ElementType { get; set; } = "";

        [Description("Name of the element in Tekla Structural Designer, as shown in its interface. For a wall this is the name of the whole wall; each of its panels becomes a separate BHoM Panel.")]
        public virtual string ElementName { get; set; } = "";

        [Description("Index of the element in Tekla Structural Designer. Negative if unknown.")]
        public virtual int ElementIndex { get; set; } = -1;

        [Description("The Guid Tekla Structural Designer uses for the element.")]
        public virtual Guid ElementId { get; set; } = Guid.Empty;

        [Description("Index of the wall panel within its wall, for a Panel pulled from a structural wall. Negative for every other element.")]
        public virtual int PanelIndex { get; set; } = -1;

        [Description("Name of the slab a slab item belongs to. Empty for every other element.")]
        public virtual string SlabName { get; set; } = "";

        [Description("Index of the slab a slab item belongs to in Tekla Structural Designer. Negative for every other element.")]
        public virtual int SlabIndex { get; set; } = -1;

        [Description("The Tekla Structural Designer type of the element, for example 'Flat' or 'Composite' for a slab, 'MeshedShearWall' for a wall, 'DuoPitch' for a roof.")]
        public virtual string ElementSubType { get; set; } = "";

        [Description("How the element spans in Tekla Structural Designer: 'OneWay' or 'TwoWay'. Where it spans one way, it spans along the local x of the Panel.")]
        public virtual string SpanType { get; set; } = "";

        [Description("Name of the element group the element belongs to in Tekla Structural Designer.")]
        public virtual string ElementGroupName { get; set; } = "";

        /***************************************************/
    }
}
