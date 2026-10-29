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
    // One span with the identity its Bar is given, and the live span handle, for use within one Pull.
    internal sealed class TsdSpanIdentity
    {
        /***************************************************/
        /**** Public Properties                         ****/
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
        public MemberConstruction Construction { get; }

        // The construction points at the span ends; Guid.Empty and null if unresolved.
        public Guid StartPointId { get; }
        public Guid EndPointId { get; }
        public Point StartPosition { get; }
        public Point EndPosition { get; }

        // The Bar's identifier: member name and span index, as "B12:0".
        public string ObjectId
        {
            get { return MemberName + ":" + SpanIndex; }
        }

        /***************************************************/
        /**** Constructors                              ****/
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
            MemberConstruction construction,
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
            Construction = construction;
            StartPointId = startPointId;
            EndPointId = endPointId;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }

        /***************************************************/
    }
}
