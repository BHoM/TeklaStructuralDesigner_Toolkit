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
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SectionProperties;
using TSD.API.Remoting.Sections;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // Section and material are read straight off the live IMemberSpan handle rather than off
        // TsdSpanIdentity's own SectionName/MaterialGrade strings: those two exist only for the
        // TeklaStructuralDesignerMemberProperties fragment, kept human readable because the numeric
        // properties on ExplicitSection/GenericIsotropicMaterial are already a lossy reconstruction.
        //
        // Orientation angle is assumed to be reported in degrees, matching common structural software
        // convention; this has not been checked against a live model and is worth confirming - see the
        // toolkit README.
        public static Bar ToBHoM(this TsdSpanIdentity span, Node start, Node end)
        {
            IMaterialFragment material = span.Span.Material.ValueOrDefault().ToBHoM();

            IMemberSection memberSection = span.Span.ElementSection.ValueOrDefault() as IMemberSection;
            ISection physicalSection = memberSection != null ? memberSection.PhysicalSection.ValueOrDefault() : null;
            ISectionProperty section = physicalSection.ToBHoM(material) ?? new ExplicitSection { Name = span.SectionName, Material = material };

            double rotationAngleDegrees = span.Span.RotationAngle.ValueOrDefault(0.0);

            Bar bar = new Bar
            {
                Name = span.ObjectId,
                Start = start,
                End = end,
                SectionProperty = section,
                OrientationAngle = rotationAngleDegrees * Math.PI / 180.0,
            };

            bar.Fragments.Add(new TeklaStructuralDesignerId { Id = span.ObjectId });
            bar.Fragments.Add(new TeklaStructuralDesignerMemberProperties
            {
                MemberName = span.MemberName,
                MemberIndex = span.MemberIndex,
                MemberId = span.MemberId,
                SpanIndex = span.SpanIndex,
                SpanId = span.SpanId,
                LevelName = span.LevelName,
                ElementGroupName = span.ElementGroupName,
                ParentElementGroupName = span.ParentElementGroupName,
                MaterialGrade = span.MaterialGrade,
                SectionName = span.SectionName,
                MemberConstruction = span.MemberConstruction,
            });

            return bar;
        }

        /***************************************************/
    }
}
