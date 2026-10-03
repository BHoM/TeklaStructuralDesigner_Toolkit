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
        /****            Public Methods                 ****/
        /***************************************************/

        // Section and material are read straight off the live IMemberSpan handle rather than off
        // TsdSpanIdentity's own SectionName/MaterialGrade strings, which exist only for the
        // TeklaStructuralDesignerMemberProperties fragment. Both go through the cache so that every Bar
        // in a pull with the same section shares one section object - see Types/BarPropertyCache.cs for
        // how a section is matched to the library or built from its shape.
        //
        // The orientation angle comes from GlobalRotationAngle, not RotationAngle. Tekla Structural
        // Designer reports both: RotationAngle is measured from the member's own construction plane,
        // which on a sloped plane is not the global reference BHoM measures from, while
        // GlobalRotationAngle is measured from global axes. Checked against a live model, the two differ
        // by up to 25 degrees on sloped plane members, and GlobalRotationAngle is the one that matches
        // the solver's own gamma angle for every member. Both are in radians, as BHoM's is.
        public static Bar ToBHoM(this TsdSpanIdentity span, Node start, Node end, BarPropertyCache cache)
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
                MemberConstruction = span.MemberConstruction,
            });

            return bar;
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // Vertical members need a quarter turn on top of the rotation Tekla Structural Designer reports.
        // Both packages define the zero rotation of a vertical member by a horizontal reference axis, but
        // not the same one: Tekla Structural Designer's local y runs along global X, while BHoM's runs
        // along global Y (see BH.Engine.Geometry.Query.ElementNormal). The Tekla Structural Designer side
        // was confirmed on a live model by comparing a column's local end forces with the global support
        // reactions at the same end: its local y force tracked the global X reaction and its local z force
        // the global Y one.
        //
        // The turn goes the other way for a member modelled top down, because BHoM keeps local y on global
        // Y whichever way a vertical member runs, so the rotation that lands the section in the same place
        // reverses with it. BHoM's own IsVertical decides what counts as vertical, so this agrees with the
        // convention it is correcting for.
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
