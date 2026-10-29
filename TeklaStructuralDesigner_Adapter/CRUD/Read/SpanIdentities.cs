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
using TsdPoint3D = TSD.API.Remoting.Geometry.Point3D;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // One TsdSpanIdentity per span of every member, or of the given members only. Rebuilt on every
        // call, so a model edited between pulls is never read against stale identities.
        private List<TsdSpanIdentity> BuildSpanIdentities(int timeoutSeconds, ICollection<int> memberIndices = null)
        {
            List<TsdSpanIdentity> result = new List<TsdSpanIdentity>();

            IEnumerable<IMember> members;
            if (!Async.TryRunSync(ct => m_Model.GetMembersAsync(memberIndices, ct), timeoutSeconds, "reading members", "no members have been read", out members, true))
                return result;

            Dictionary<Guid, ElementGroupInfo> groupLookup = BuildElementGroupLookup(timeoutSeconds);

            // Read every span first, so the construction points at their ends can be read in one batch.
            List<PendingSpan> pending = new List<PendingSpan>();
            HashSet<int> pointIndices = new HashSet<int>();
            int membersWithNoSpans = 0;

            foreach (IMember member in members)
            {
                IEnumerable<IMemberSpan> spansRead;
                if (!Async.TryRunSync(ct => member.GetSpanAsync(null, ct), timeoutSeconds, "reading spans for member '" + member.Name + "'", "it has been skipped", out spansRead))
                    continue;

                List<IMemberSpan> spans = spansRead.ToList();
                if (spans.Count == 0)
                {
                    membersWithNoSpans++;
                    continue;
                }

                IMemberData data = member.Data.ValueOrDefault();
                MemberConstruction construction = data != null
                    ? data.Construction.ValueOrDefault(MemberConstruction.Unknown)
                    : MemberConstruction.Unknown;

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

                IMaterial material = p.Span.Material.ValueOrDefault();
                string materialGrade = material != null ? (material.Name ?? "") : "";

                ElementGroupInfo groupInfo;
                groupLookup.TryGetValue(p.Member.Id, out groupInfo);

                result.Add(new TsdSpanIdentity(
                    memberName: p.Member.Name,
                    memberIndex: p.Member.Index,
                    memberId: p.Member.Id,
                    spanIndex: p.Span.Index,
                    spanId: p.Span.Id,
                    span: p.Span,
                    levelName: levelName,
                    elementGroupName: groupInfo != null ? groupInfo.Name : "",
                    parentElementGroupName: groupInfo != null ? groupInfo.ParentName : "",
                    materialGrade: materialGrade,
                    sectionName: physicalSection.SectionName(),
                    construction: p.Construction,
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

        // Member Id to its innermost group and that group's parent. Only top level groups are returned
        // by the API, so sub groups are walked, and each group's direct Elements used.
        private Dictionary<Guid, ElementGroupInfo> BuildElementGroupLookup(int timeoutSeconds)
        {
            Dictionary<Guid, ElementGroupInfo> lookup = new Dictionary<Guid, ElementGroupInfo>();

            IEnumerable<IElementGroup> topLevelGroups;
            if (!Async.TryRunSync(ct => m_Model.GetElementGroupsAsync(null, EntityType.Member, ct), timeoutSeconds, "reading element groups", "element group names will be left empty", out topLevelGroups))
                return lookup;

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

        // The Guid, position and level of every given construction point, in one call.
        private Dictionary<int, ConstructionPointInfo> BuildConstructionPointInfoByIndex(HashSet<int> pointIndices, int timeoutSeconds)
        {
            Dictionary<int, ConstructionPointInfo> result = new Dictionary<int, ConstructionPointInfo>();
            if (pointIndices.Count == 0)
                return result;

            Dictionary<Guid, string> levelNameByPlaneId = BuildLevelNameByPlaneId(timeoutSeconds);

            IEnumerable<IConstructionPoint> points;
            if (!Async.TryRunSync(ct => m_Model.GetConstructionPointsAsync(pointIndices, ct), timeoutSeconds, "reading construction points", "Bar geometry and level names will be left empty", out points))
                return result;

            foreach (IConstructionPoint point in points)
            {
                TsdPoint3D coordinates = point.Coordinates.ValueOrDefault(default(TsdPoint3D));
                Point position = coordinates.FromTsdPoint();

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

            foreach (IHorizontalConstructionPlane plane in ReadTsdLevels(timeoutSeconds, "level names will be left empty"))
                lookup[plane.Id] = plane.LevelName();

            return lookup;
        }

        /***************************************************/

        // Every level in the model, or none, with a warning naming the consequence.
        private List<IHorizontalConstructionPlane> ReadTsdLevels(int timeoutSeconds, string consequence)
        {
            IEnumerable<IHorizontalConstructionPlane> levels;
            if (!Async.TryRunSync(ct => m_Model.GetLevelsAsync(null, ct), timeoutSeconds, "reading levels", consequence, out levels))
                return new List<IHorizontalConstructionPlane>();

            return levels.ToList();
        }

        /***************************************************/
    }
}
