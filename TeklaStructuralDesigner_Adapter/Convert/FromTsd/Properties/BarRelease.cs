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

using System.Collections.Generic;
using BH.Engine.Structure;
using BH.oM.Structure.Constraints;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // approximatedEnds counts ends whose fixity has no BHoM equivalent (nominal, partial or non
        // linear) and was approximated - see RotationalFixity.
        public static BarRelease FromTsdBarRelease(this ISpanReleases start, ISpanReleases end, out int approximatedEnds)
        {
            bool startApproximated, endApproximated, startFixed, endFixed;
            Constraint6DOF startRelease = start.FromTsdEndRelease(out startApproximated, out startFixed);
            Constraint6DOF endRelease = end.FromTsdEndRelease(out endApproximated, out endFixed);

            approximatedEnds = (startApproximated ? 1 : 0) + (endApproximated ? 1 : 0);

            if (startFixed && endFixed)
                return Engine.Structure.Create.BarReleaseFixFix();

            BarRelease release = new BarRelease { StartRelease = startRelease, EndRelease = endRelease };
            release.Name = release.Description();
            return release;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Each set DegreeOfFreedom flag is a fixed direction in the span's local axes, so Fx..Mz map onto
        // TranslationX..RotationZ. The named release properties are only a fallback for when the flags
        // are not reported. No release data at all means fully fixed, the Tekla Structural Designer default.
        private static Constraint6DOF FromTsdEndRelease(this ISpanReleases releases, out bool approximated, out bool isFixed)
        {
            approximated = false;
            isFixed = true;
            if (releases == null)
                return Engine.Structure.Create.FixConstraint6DOF();

            DegreeOfFreedom? flags = null;
            if (releases.DegreeOfFreedom != null && releases.DegreeOfFreedom.IsApplicable)
                flags = releases.DegreeOfFreedom.Value;

            IStiffnessData major = releases.MajorRotationalStiffness.ValueOrDefault();
            IStiffnessData minor = releases.MinorRotationalStiffness.ValueOrDefault();

            bool x = Fixity(flags, DegreeOfFreedom.Fx, !releases.AxialRelease.ValueOrDefault(false));
            bool y = Fixity(flags, DegreeOfFreedom.Fy, true);
            bool z = Fixity(flags, DegreeOfFreedom.Fz, true);
            bool xx = Fixity(flags, DegreeOfFreedom.Mx, !releases.TorsionalRelease.ValueOrDefault(false));
            bool yy = Fixity(flags, DegreeOfFreedom.My, TypeOf(major) != SpringStiffness.Release);
            bool zz = Fixity(flags, DegreeOfFreedom.Mz, TypeOf(minor) != SpringStiffness.Release);

            double majorSpring, minorSpring;
            yy = RotationalFixity(major, yy, out majorSpring, ref approximated);
            zz = RotationalFixity(minor, zz, out minorSpring, ref approximated);

            if (x && y && z && xx && yy && zz)
                return Engine.Structure.Create.FixConstraint6DOF();

            isFixed = false;

            Constraint6DOF constraint = Engine.Structure.Create.Constraint6DOF("",
                new List<bool> { x, y, z, xx, yy, zz },
                new List<double> { 0, 0, 0, 0, majorSpring, minorSpring });

            constraint.Name = constraint.Description();
            return constraint;
        }

        /***************************************************/

        private static bool Fixity(DegreeOfFreedom? flags, DegreeOfFreedom flag, bool fallback)
        {
            return flags.HasValue ? (flags.Value & flag) != 0 : fallback;
        }

        /***************************************************/

        private static SpringStiffness TypeOf(IStiffnessData stiffness)
        {
            return stiffness != null ? stiffness.Type.ValueOrDefault(SpringStiffness.Unknown) : SpringStiffness.Unknown;
        }

        /***************************************************/

        // A linear spring is kept exactly. Nominally pinned becomes free, nominally fixed becomes fixed,
        // and partial or non linear springs a linear spring of the reported stiffness - all flagged as
        // approximated. Anything else leaves the fixity unchanged.
        private static bool RotationalFixity(IStiffnessData stiffness, bool isFixed, out double spring, ref bool approximated)
        {
            spring = 0;
            double value = stiffness != null ? stiffness.Stiffness.ValueOrDefault(0.0) * MomentScale : 0.0;

            switch (TypeOf(stiffness))
            {
                case SpringStiffness.SpringLinear:
                    spring = value;
                    return false;

                case SpringStiffness.NominallyPinned:
                    approximated = true;
                    return false;

                case SpringStiffness.NominallyFixed:
                    approximated = true;
                    return true;

                case SpringStiffness.PartiallyFixed:
                case SpringStiffness.SpringNonLinear:
                    approximated = true;
                    spring = value;
                    return false;

                default:
                    return isFixed;
            }
        }

        /***************************************************/
    }
}
