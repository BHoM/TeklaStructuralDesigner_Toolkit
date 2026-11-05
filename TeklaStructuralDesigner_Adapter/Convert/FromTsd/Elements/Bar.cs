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
using BH.Engine.Geometry;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Geometry;
using BH.oM.Structure.Elements;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SectionProperties;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Section and material come from the live span, through the cache so Bars with the same
        // section share one section object (see BarPropertyCache).
        public static Bar FromTsdBar(this TsdSpanIdentity span, Node start, Node end, BarPropertyCache cache)
        {
            IMaterialFragment material = cache.Material(span.Span.Material.ValueOrDefault());

            IMemberSection memberSection = span.Span.ElementSection.ValueOrDefault() as IMemberSection;
            ISection physicalSection = memberSection != null ? memberSection.PhysicalSection.ValueOrDefault() : null;
            ISectionProperty section = cache.Section(physicalSection, material) ?? new ExplicitSection { Name = span.SectionName, Material = material };

            Bar bar = new Bar
            {
                Name = span.ObjectId,
                Start = start,
                End = end,
                SectionProperty = section,
                Release = cache.Release(span.Span),
                OrientationAngle = BarOrientationAngle(span.Span, start, end),
            };

            bool recognised;
            StructuralUsage1D usage = span.Construction.FromTsdStructuralUsage(out recognised);
            if (!recognised)
                cache.Unrecognised(WarningTally.UnrecognisedMessage("bar(s)", "construction type", span.Construction));

            bar.SetIdentity(span.ObjectId, span.SpanId);
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
                StructuralUsage = usage,
            });

            return bar;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // GlobalRotationAngle, not RotationAngle: the latter is measured from the member's construction
        // plane, which differs from global on sloped planes. Vertical members also need a quarter turn,
        // because Tekla Structural Designer's zero rotation puts local y along global X where BHoM puts
        // it along global Y; the turn reverses for members modelled top down.
        private static double BarOrientationAngle(IMemberSpan span, Node start, Node end)
        {
            double rotation = span.GlobalRotationAngle.ValueOrDefault(span.RotationAngle.ValueOrDefault(0.0));

            if (start?.Position == null || end?.Position == null)
                return rotation;

            Line centreline = new Line { Start = start.Position, End = end.Position };
            if (!centreline.IsVertical())
                return rotation;

            return rotation + (end.Position.Z >= start.Position.Z ? -Math.PI / 2 : Math.PI / 2);
        }

        /***************************************************/
    }
}
