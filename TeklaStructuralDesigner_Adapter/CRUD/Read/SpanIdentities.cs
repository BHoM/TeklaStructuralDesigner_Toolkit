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
using System.Collections.Generic;
using System.Linq;
using BH.oM.Geometry;
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Materials;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Span identity)                ****/
        /***************************************************/

        // Builds one TsdSpanIdentity per span in the whole model. Every member and span is included:
        // this adapter does not filter by material or construction type, unlike the tool it was ported
        // from - that kind of filtering is application logic for whatever consumes the pull, not
        // something the adapter should decide on the caller's behalf.
        //
        // Rebuilt on every call rather than cached across Pulls, so a model edited between pulls is
        // never read against stale identities. The cost is a handful of extra calls (GetMembersAsync,
        // one GetSpanAsync per member, one GetElementGroupsAsync, one GetLevelsAsync, one
        // GetConstructionPointsAsync) which is negligible next to the results calls that follow.
        private List<TsdSpanIdentity> BuildSpanIdentities(int timeoutSeconds)
        {
            List<TsdSpanIdentity> result = new List<TsdSpanIdentity>();

            List<IMember> members;
            try
            {
                members = Async.RunSync(ct => m_Model.GetMembersAsync(null, ct), timeoutSeconds, "reading members").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError("Failed to read members from Tekla Structural Designer. " + e.Message);
                return result;
            }

            Dictionary<Guid, ElementGroupInfo> groupLookup = BuildElementGroupLookup(timeoutSeconds);

            // First pass: read every span and note which construction point indices its ends refer to,
            // without resolving level names yet - that is done once, for the whole set, below.
            List<PendingSpan> pending = new List<PendingSpan>();
            HashSet<int> pointIndices = new HashSet<int>();
            int membersWithNoSpans = 0;

            foreach (IMember member in members)
            {
                List<IMemberSpan> spans;
                try
                {
                    spans = Async.RunSync(ct => member.GetSpanAsync(null, ct), timeoutSeconds, "reading spans for member '" + member.Name + "'").ToList();
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning("Failed to read spans for member '" + member.Name + "'; it has been skipped. " + e.Message);
                    continue;
                }

                if (spans.Count == 0)
                {
                    membersWithNoSpans++;
                    continue;
                }

                IMemberData data = member.Data.ValueOrDefault();
                string construction = data != null
                    ? data.Construction.ValueOrDefault(default(MemberConstruction)).ToString()
                    : "";

                foreach (IMemberSpan span in spans)
                {
                    int startPointIndex = span.StartMemberNode != null ? span.StartMemberNode.ConstructionPointIndex.ValueOrDefault(-1) : -1;
                    int endPointIndex = span.EndMemberNode != null ? span.EndMemberNode.ConstructionPointIndex.ValueOrDefault(-1) : -1;

                    if (startPointIndex >= 0)
                        pointIndices.Add(startPointIndex);
                    if (endPointIndex >= 0)
                        pointIndices.Add(endPointIndex);

                    pending.Add(new PendingSpan(member, span, construction, startPointIndex, endPointIndex));
                }
            }

            if (membersWithNoSpans > 0)
                Engine.Base.Compute.RecordNote(membersWithNoSpans + " member(s) had no spans and were skipped.");

            Dictionary<int, ConstructionPointInfo> pointInfoByIndex = BuildConstructionPointInfoByIndex(pointIndices, timeoutSeconds);

            // Second pass: now that construction point positions and levels are resolvable, build the
            // identities.
            int spansWithNoLevel = 0;
            int spansWithNoGeometry = 0;

            foreach (PendingSpan p in pending)
            {
                ConstructionPointInfo startInfo = null;
                ConstructionPointInfo endInfo = null;
                if (p.StartPointIndex >= 0)
                    pointInfoByIndex.TryGetValue(p.StartPointIndex, out startInfo);
                if (p.EndPointIndex >= 0)
                    pointInfoByIndex.TryGetValue(p.EndPointIndex, out endInfo);

                string levelName = startInfo?.LevelName ?? endInfo?.LevelName;
                if (levelName == null)
                {
                    levelName = "Unassigned";
                    spansWithNoLevel++;
                }

                if (startInfo == null || endInfo == null)
                    spansWithNoGeometry++;

                IMemberSection memberSection = p.Span.ElementSection.ValueOrDefault() as IMemberSection;
                ISection physicalSection = memberSection != null ? memberSection.PhysicalSection.ValueOrDefault() : null;
                string sectionName = physicalSection != null ? (physicalSection.LongName ?? physicalSection.ShortName ?? "") : "";

                IMaterial material = p.Span.Material.ValueOrDefault();
                string materialGrade = material != null ? (material.Name ?? "") : "";

                double lengthMetres = p.Span.Length.ValueOrDefault(0.0) * 0.001;

                ElementGroupInfo groupInfo;
                groupLookup.TryGetValue(p.Member.Id, out groupInfo);

                result.Add(new TsdSpanIdentity(
                    memberName: p.Member.Name,
                    memberIndex: p.Member.Index,
                    memberId: p.Member.Id,
                    spanIndex: p.Span.Index,
                    spanId: p.Span.Id,
                    span: p.Span,
                    lengthMetres: lengthMetres,
                    levelName: levelName,
                    elementGroupName: groupInfo != null ? groupInfo.Name : "",
                    parentElementGroupName: groupInfo != null ? groupInfo.ParentName : "",
                    materialGrade: materialGrade,
                    sectionName: sectionName,
                    memberConstruction: p.Construction,
                    startPointId: startInfo?.Id ?? Guid.Empty,
                    endPointId: endInfo?.Id ?? Guid.Empty,
                    startPosition: startInfo?.Position,
                    endPosition: endInfo?.Position));
            }

            if (spansWithNoLevel > 0)
                Engine.Base.Compute.RecordWarning(spansWithNoLevel + " span(s) could not be matched to a level and have been reported as 'Unassigned'.");

            if (spansWithNoGeometry > 0)
                Engine.Base.Compute.RecordWarning(spansWithNoGeometry + " span(s) could not be resolved to construction point coordinates at one or both ends; they have been skipped when building Bars.");

            return result;
        }

        /***************************************************/

        // Builds member Id -> (group name, parent group name), from the innermost group containing
        // each member. GetElementGroupsAsync only returns top level groups, so sub groups are reached
        // by walking IElementGroup.SubGroups recursively. A member's direct Elements list (not
        // AllElements, which also includes every sub group's members) is therefore always its most
        // specific group.
        private Dictionary<Guid, ElementGroupInfo> BuildElementGroupLookup(int timeoutSeconds)
        {
            Dictionary<Guid, ElementGroupInfo> lookup = new Dictionary<Guid, ElementGroupInfo>();

            List<IElementGroup> topLevelGroups;
            try
            {
                topLevelGroups = Async.RunSync(ct => m_Model.GetElementGroupsAsync(null, EntityType.Member, ct), timeoutSeconds, "reading element groups").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordWarning("Failed to read element groups from Tekla Structural Designer; element group names will be left empty. " + e.Message);
                return lookup;
            }

            foreach (IElementGroup group in FlattenGroups(topLevelGroups))
            {
                string parentName = group.ParentGroupName.ValueOrDefault("");

                foreach (EntityInfo entity in group.Elements.ValuesOrEmpty())
                {
                    if (entity.Type != EntityType.Member || entity.Id == Guid.Empty)
                        continue;

                    lookup[entity.Id] = new ElementGroupInfo(group.Name, parentName);
                }
            }

            return lookup;
        }

        /***************************************************/

        private static IEnumerable<IElementGroup> FlattenGroups(IEnumerable<IElementGroup> groups)
        {
            foreach (IElementGroup group in groups)
            {
                yield return group;

                foreach (IElementGroup subGroup in FlattenGroups(group.SubGroups.ValuesOrEmpty()))
                    yield return subGroup;
            }
        }

        /***************************************************/

        // One GetConstructionPointsAsync call for every point index actually referenced by a span end
        // in the model, rather than one call per span - the same batching principle as the results
        // read itself. Returns each point's own Guid (the Node identity a Bar's ends are built from),
        // its position in metres, and the level name resolved from its construction plane.
        private Dictionary<int, ConstructionPointInfo> BuildConstructionPointInfoByIndex(HashSet<int> pointIndices, int timeoutSeconds)
        {
            Dictionary<int, ConstructionPointInfo> result = new Dictionary<int, ConstructionPointInfo>();
            if (pointIndices.Count == 0)
                return result;

            Dictionary<Guid, string> levelNameByPlaneId = BuildLevelNameByPlaneId(timeoutSeconds);

            List<IConstructionPoint> points;
            try
            {
                points = Async.RunSync(ct => m_Model.GetConstructionPointsAsync(pointIndices, ct), timeoutSeconds, "reading construction points").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordWarning("Failed to read construction points from Tekla Structural Designer; Bar geometry and level names will be left empty. " + e.Message);
                return result;
            }

            foreach (IConstructionPoint point in points)
            {
                TSD.API.Remoting.Geometry.Point3D coordinates = point.Coordinates.ValueOrDefault(default(TSD.API.Remoting.Geometry.Point3D));
                // Tekla Structural Designer reports coordinates in millimetres, consistent with the
                // mm/N unit system observed on forces and section properties elsewhere in this toolkit.
                Point position = new Point { X = coordinates.X * 0.001, Y = coordinates.Y * 0.001, Z = coordinates.Z * 0.001 };

                string levelName = null;
                EntityInfo planeInfo = point.PlaneInfo.ValueOrDefault(default(EntityInfo));
                if (planeInfo.Id != Guid.Empty)
                    levelNameByPlaneId.TryGetValue(planeInfo.Id, out levelName);

                result[point.Index] = new ConstructionPointInfo(point.Id, position, levelName);
            }

            return result;
        }

        /***************************************************/

        private Dictionary<Guid, string> BuildLevelNameByPlaneId(int timeoutSeconds)
        {
            Dictionary<Guid, string> lookup = new Dictionary<Guid, string>();

            List<IHorizontalConstructionPlane> planes;
            try
            {
                planes = Async.RunSync(ct => m_Model.GetLevelsAsync(null, ct), timeoutSeconds, "reading levels").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordWarning("Failed to read levels from Tekla Structural Designer; level names will be left empty. " + e.Message);
                return lookup;
            }

            foreach (IHorizontalConstructionPlane plane in planes)
            {
                string name = plane.LongReference.ValueOrDefault("");
                if (string.IsNullOrWhiteSpace(name))
                    name = plane.ShortReference.ValueOrDefault("");

                lookup[plane.Id] = string.IsNullOrWhiteSpace(name) ? "Unassigned" : name.Trim();
            }

            return lookup;
        }

        /***************************************************/
        /****            Private Nested Types           ****/
        /***************************************************/

        private sealed class PendingSpan
        {
            public IMember Member { get; }
            public IMemberSpan Span { get; }
            public string Construction { get; }
            public int StartPointIndex { get; }
            public int EndPointIndex { get; }

            public PendingSpan(IMember member, IMemberSpan span, string construction, int startPointIndex, int endPointIndex)
            {
                Member = member;
                Span = span;
                Construction = construction;
                StartPointIndex = startPointIndex;
                EndPointIndex = endPointIndex;
            }
        }

        /***************************************************/

        private sealed class ElementGroupInfo
        {
            public string Name { get; }
            public string ParentName { get; }

            public ElementGroupInfo(string name, string parentName)
            {
                Name = name;
                ParentName = parentName;
            }
        }

        /***************************************************/

        private sealed class ConstructionPointInfo
        {
            public Guid Id { get; }
            public Point Position { get; }
            public string LevelName { get; }

            public ConstructionPointInfo(Guid id, Point position, string levelName)
            {
                Id = id;
                Position = position;
                LevelName = levelName;
            }
        }

        /***************************************************/
    }
}
