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
using BH.oM.Geometry;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // Ties one TSD span to the identity it is given as a BHoM Bar. Holds a live IMemberSpan handle so
    // the per-span-end fallback route (Types/TsdSpanIdentity -> IMemberSpan.GetEndForceAsync) can be
    // used without looking the span back up. Kept internal to the Adapter: it is not serialisable and
    // is never returned to a caller, only consumed while building results within a single Pull.
    internal sealed class TsdSpanIdentity
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        public string MemberName { get; }
        public int MemberIndex { get; }
        public Guid MemberId { get; }
        public int SpanIndex { get; }
        public Guid SpanId { get; }
        public IMemberSpan Span { get; }
        public string LevelName { get; }
        public string ElementGroupName { get; }
        public string ParentElementGroupName { get; }
        public string MaterialGrade { get; }
        public string SectionName { get; }
        public string MemberConstruction { get; }

        // Identity and position of the two construction points the span runs between, needed to build
        // the Nodes a Bar requires. Guid.Empty / null if the span's end could not be resolved to a
        // construction point - ReadBars skips a span in that state and reports the count once.
        public Guid StartPointId { get; }
        public Guid EndPointId { get; }
        public Point StartPosition { get; }
        public Point EndPosition { get; }

        // The identifier a BHoM Bar built from this span carries as its ObjectId: the member's own
        // name plus the span's index within that member, so a single span member and a spliced,
        // multi span member are both addressable and neither collides with the other's spans.
        public string ObjectId
        {
            get { return MemberName + ":" + SpanIndex; }
        }

        /***************************************************/
        /****                Constructors               ****/
        /***************************************************/

        public TsdSpanIdentity(
            string memberName,
            int memberIndex,
            Guid memberId,
            int spanIndex,
            Guid spanId,
            IMemberSpan span,
            string levelName,
            string elementGroupName,
            string parentElementGroupName,
            string materialGrade,
            string sectionName,
            string memberConstruction,
            Guid startPointId,
            Guid endPointId,
            Point startPosition,
            Point endPosition)
        {
            MemberName = memberName;
            MemberIndex = memberIndex;
            MemberId = memberId;
            SpanIndex = spanIndex;
            SpanId = spanId;
            Span = span;
            LevelName = levelName;
            ElementGroupName = elementGroupName;
            ParentElementGroupName = parentElementGroupName;
            MaterialGrade = materialGrade;
            SectionName = sectionName;
            MemberConstruction = memberConstruction;
            StartPointId = startPointId;
            EndPointId = endPointId;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }

        /***************************************************/
    }
}
