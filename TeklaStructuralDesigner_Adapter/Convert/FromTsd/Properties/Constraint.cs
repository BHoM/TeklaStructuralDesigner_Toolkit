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
using BH.Engine.Structure;
using BH.oM.Geometry;
using BH.oM.Structure.Constraints;
using TSD.API.Remoting.Geometry;
using TSD.API.Remoting.Solver;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Each set DegreeOfFreedom flag is a restrained direction. Springs and partial fixities are not
        // converted, only reported through hasPartialFixity: their Axial/Major/Minor to X/Y/Z mapping
        // has not been verified.
        public static Constraint6DOF FromTsdConstraint(this ISupportData data, out bool hasPartialFixity)
        {
            hasPartialFixity = false;
            if (data == null)
                return null;

            DegreeOfFreedom fixity = data.DegreeOfFreedom.ValueOrDefault(DegreeOfFreedom.Free);

            bool x = (fixity & DegreeOfFreedom.Fx) != 0;
            bool y = (fixity & DegreeOfFreedom.Fy) != 0;
            bool z = (fixity & DegreeOfFreedom.Fz) != 0;
            bool xx = (fixity & DegreeOfFreedom.Mx) != 0;
            bool yy = (fixity & DegreeOfFreedom.My) != 0;
            bool zz = (fixity & DegreeOfFreedom.Mz) != 0;

            hasPartialFixity = HasPartialFixity(data);

            if (x && y && z && xx && yy && zz)
                return Engine.Structure.Create.FixConstraint6DOF();

            if (x && y && z && !xx && !yy && !zz)
                return Engine.Structure.Create.PinConstraint6DOF();

            Constraint6DOF constraint = Engine.Structure.Create.Constraint6DOF(x, y, z, xx, yy, zz);
            constraint.Name = constraint.Description();
            return constraint;
        }

        /***************************************************/

        // The support's axes, used as the Node's Orientation so a rotated support's fixity lands in the
        // right directions. Null for global or unreported axes; axesReported tells those apart.
        public static Basis FromTsdOrientation(this ISupportUcs ucs, out bool axesReported)
        {
            axesReported = false;
            if (ucs == null || ucs.Axes == null || !ucs.Axes.IsApplicable)
                return null;

            AxisTriad axes = ucs.Axes.Value;
            Vector x = axes.XAxis.FromTsdVector();
            Vector y = axes.YAxis.FromTsdVector();

            const double tolerance = 1e-6;
            if (x.Length() < tolerance || y.Length() < tolerance)
                return null;

            x = x.Normalise();
            y = y.Normalise();

            if (Math.Abs(x.X - 1) < tolerance && Math.Abs(y.Y - 1) < tolerance)
            {
                axesReported = true;
                return null;
            }

            try
            {
                Basis basis = Engine.Geometry.Create.Basis(x, y);
                axesReported = true;
                return basis;
            }
            catch (ArgumentException)
            {
                return null;        // Parallel axes.
            }
        }

        /***************************************************/

        // The support a wall generates along its base (GenerateSupport and SupportData on the wall, not a
        // point support), or null. Springs and partial fixity are reported through warning rather than
        // converted, as is a base support the wall should have but does not.
        public static Constraint6DOF FromTsdWallBaseSupport(this TSD.API.Remoting.Structure.IStructuralWallData wallData, out string warning)
        {
            warning = null;
            if (wallData == null)
                return null;

            if (!wallData.GenerateSupport.ValueOrDefault(false))
            {
                if (wallData.AutomaticGenerateSupport.ValueOrDefault(false))
                    warning = "wall(s) leave Tekla Structural Designer to decide whether they generate a base support, and it reported none, so they have been pulled unsupported at the base. Check this against the model";

                return null;
            }

            bool isPartial;
            Constraint6DOF support = wallData.SupportData.ValueOrDefault().FromTsdConstraint(out isPartial);
            if (support == null)
            {
                warning = "wall(s) generate a base support but Tekla Structural Designer reported no fixity for it, so they have been pulled unsupported at the base";
                return null;
            }

            if (isPartial)
                warning = "wall base support(s) have spring or partial fixity in at least one direction. Their fixed and free directions have been pulled, but the springs have not";

            return support;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        private static bool HasPartialFixity(ISupportData data)
        {
            foreach (Direction direction in new[] { Direction.Axial, Direction.Major, Direction.Minor })
            {
                if (IsPartial(() => data.GetStiffness(direction).ValueOrDefault()) ||
                    IsPartial(() => data.GetRotationalStiffness(direction).ValueOrDefault()))
                {
                    return true;
                }
            }

            return false;
        }

        /***************************************************/

        private static bool IsPartial(Func<IStiffnessData> read)
        {
            IStiffnessData stiffness;
            try
            {
                stiffness = read();
            }
            catch (Exception)
            {
                return false;       // Not applicable to this support.
            }

            if (stiffness == null)
                return false;

            switch (stiffness.Type.ValueOrDefault(SpringStiffness.Unknown))
            {
                case SpringStiffness.SpringLinear:
                case SpringStiffness.SpringNonLinear:
                case SpringStiffness.NominallyPinned:
                case SpringStiffness.NominallyFixed:
                case SpringStiffness.PartiallyFixed:
                    return true;
                default:
                    return false;
            }
        }

        /***************************************************/
    }
}
