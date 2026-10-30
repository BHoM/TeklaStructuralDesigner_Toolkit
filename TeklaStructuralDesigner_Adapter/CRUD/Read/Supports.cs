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
using BH.oM.Structure.Constraints;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Every support, keyed by its construction point Guid (as Nodes are), with its position for
        // supports no span ends at. Identical fixities share one Constraint6DOF. report false skips the
        // summary of what was skipped or approximated.
        private Dictionary<Guid, SupportedPoint> ReadSupports(int timeoutSeconds, bool report = true)
        {
            Dictionary<Guid, SupportedPoint> result = new Dictionary<Guid, SupportedPoint>();

            IEnumerable<ISupport> supportsRead;
            if (!Async.TryRunSync(ct => m_Model.GetSupportsAsync(null, ct), timeoutSeconds, "reading supports", "Nodes have been pulled without them", out supportsRead))
                return result;

            List<ISupport> supports = supportsRead.ToList();

            if (supports.Count == 0)
                return result;

            HashSet<int> pointIndices = new HashSet<int>(supports.Select(s => s.ConstructionPointIndex.ValueOrDefault(-1)).Where(i => i >= 0));
            Dictionary<int, ConstructionPointInfo> pointInfoByIndex = BuildConstructionPointInfoByIndex(pointIndices, timeoutSeconds);

            Dictionary<string, Constraint6DOF> constraintByName = new Dictionary<string, Constraint6DOF>();
            int unresolved = 0, partialFixity = 0, oriented = 0, orientationUnresolved = 0, subModel = 0;

            foreach (ISupport support in supports)
            {
                ConstructionPointInfo point;
                if (!pointInfoByIndex.TryGetValue(support.ConstructionPointIndex.ValueOrDefault(-1), out point))
                {
                    unresolved++;
                    continue;
                }

                ISupportData data = support.Data.ValueOrDefault();

                // Sub model supports are boundary conditions of a sub model, not of the structure.
                SupportType type = data != null ? data.Type.ValueOrDefault(SupportType.Unknown) : SupportType.Unknown;
                if (type == SupportType.SubModelTop || type == SupportType.SubModelBottom)
                {
                    subModel++;
                    continue;
                }

                bool isPartial;
                Constraint6DOF constraint = data.FromTsdConstraint(out isPartial);
                if (constraint == null)
                {
                    unresolved++;
                    continue;
                }

                if (isPartial)
                    partialFixity++;

                bool axesReported;
                Basis orientation = data.Ucs.ValueOrDefault().FromTsdOrientation(out axesReported);
                if (orientation != null)
                    oriented++;
                else if (!axesReported && IsOriented(support, data))
                    orientationUnresolved++;

                Constraint6DOF shared;
                if (constraintByName.TryGetValue(constraint.Name, out shared))
                    constraint = shared;
                else
                    constraintByName[constraint.Name] = constraint;

                result[point.Id] = new SupportedPoint(point.Position, constraint, orientation);
            }

            if (!report)
                return result;

            if (unresolved > 0)
                Engine.Base.Compute.RecordWarning(unresolved + " support(s) could not be matched to a construction point or had no fixity data, and have been skipped.");

            if (subModel > 0)
                Engine.Base.Compute.RecordNote(subModel + " sub model support(s) were skipped: they are boundary conditions of a sub model, not supports of the structure.");

            if (partialFixity > 0)
            {
                Engine.Base.Compute.RecordWarning(partialFixity + " support(s) have spring or partial fixity in at least one direction. Their fixed and free directions have been pulled, " +
                    "but the spring stiffnesses have not, because the mapping of Tekla Structural Designer's Axial/Major/Minor stiffness directions onto X/Y/Z has not been verified. Those directions are pulled as free.");
            }

            if (oriented > 0)
                Engine.Base.Compute.RecordNote(oriented + " support(s) have their own axis system, which has been pulled as the Orientation of the Node they sit on.");

            if (orientationUnresolved > 0)
            {
                Engine.Base.Compute.RecordWarning(orientationUnresolved + " support(s) are inclined, rotated or use a user defined axis system, but Tekla Structural Designer reported no usable axes for them. " +
                    "Their fixity has been pulled without the rotation, so it is applied in the Node's default (global) axes.");
            }

            return result;
        }

        /***************************************************/

        private static bool IsOriented(ISupport support, ISupportData data)
        {
            const double tolerance = 1e-6;     // radians

            if (Math.Abs(support.Inclination.ValueOrDefault(0.0)) > tolerance || Math.Abs(support.Rotation.ValueOrDefault(0.0)) > tolerance)
                return true;

            ISupportUcs ucs = data != null ? data.Ucs.ValueOrDefault() : null;
            return ucs != null && ucs.Definition.ValueOrDefault(SupportUcsDefinition.Unknown) == SupportUcsDefinition.UserDefined;
        }

        /***************************************************/
    }
}
