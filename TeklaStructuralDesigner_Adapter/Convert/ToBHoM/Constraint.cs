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
        /****            Public Methods                 ****/
        /***************************************************/

        // A support's fixity is a DegreeOfFreedom flag set in which each set flag is a restrained
        // direction - Free is the empty set - so it maps directly onto Constraint6DOF's fixed/free. The
        // two common cases are built with BHoM's own FixConstraint6DOF and PinConstraint6DOF, so that
        // they carry the names BHoM and the other structural adapters use for them; anything else is
        // named by its fixity code, from BHoM's Constraint6DOF Description.
        //
        // Spring and partial fixities (linear and non linear springs, nominally pinned, nominally fixed,
        // partially fixed) are reported through hasPartialFixity rather than converted. The API gives
        // their stiffness per Axial/Major/Minor direction, and how those map onto X/Y/Z has not been
        // checked against a live model; a spring on the wrong axis would be worse than a warning.
        public static Constraint6DOF ToBHoM(this ISupportData data, out bool hasPartialFixity)
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

        // A support's own axis system, as the orientation of the Node it sits on. BHoM reads a Node's
        // Support in the Node's Orientation, so this is what makes the fixity of a rotated or inclined
        // support land in the right directions. Null - leaving the Node in global axes, BHoM's default -
        // when the support's axes are global or not reported; axesReported tells the two apart. Built
        // through Geometry.Create.Basis, which orthonormalises the reported axes.
        public static Basis ToBHoMOrientation(this ISupportUcs ucs, out bool axesReported)
        {
            axesReported = false;
            if (ucs == null || ucs.Axes == null || !ucs.Axes.IsApplicable)
                return null;

            AxisTriad axes = ucs.Axes.Value;
            Vector x = axes.XAxis.ToBHoMVector();
            Vector y = axes.YAxis.ToBHoMVector();

            const double tolerance = 1e-6;
            if (x.Length() < tolerance || y.Length() < tolerance)
                return null;

            x = x.Normalise();
            y = y.Normalise();

            if (Math.Abs(x.X - 1) < tolerance && Math.Abs(y.Y - 1) < tolerance)
            {
                axesReported = true;
                return null;        // Global axes: BHoM's default orientation.
            }

            try
            {
                Basis basis = Engine.Geometry.Create.Basis(x, y);
                axesReported = true;
                return basis;
            }
            catch (ArgumentException)
            {
                return null;        // Parallel axes: nothing meaningful to orient the Node by.
            }
        }

        /***************************************************/
        /****            Private Methods                ****/
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
                return false;       // Not applicable to this support; nothing to report.
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
