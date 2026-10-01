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
        /****            Public Methods                 ****/
        /***************************************************/

        // A span's end releases, as a BHoM BarRelease in the Bar's local axes. Fully fixed at both ends
        // is BHoM's own BarReleaseFixFix; anything else is named by BHoM's BarRelease Description.
        //
        // approximatedEnds counts ends whose fixity Tekla Structural Designer describes in terms BHoM has
        // no equivalent for (nominally pinned/fixed, partially fixed, non linear springs). Each is
        // resolved to the nearest thing BHoM has - see RotationalFixity - and the caller reports the count.
        public static BarRelease ToBHoM(this ISpanReleases start, ISpanReleases end, out int approximatedEnds)
        {
            bool startApproximated, endApproximated, startFixed, endFixed;
            Constraint6DOF startRelease = start.ToBHoMEnd(out startApproximated, out startFixed);
            Constraint6DOF endRelease = end.ToBHoMEnd(out endApproximated, out endFixed);

            approximatedEnds = (startApproximated ? 1 : 0) + (endApproximated ? 1 : 0);

            if (startFixed && endFixed)
                return Engine.Structure.Create.BarReleaseFixFix();

            BarRelease release = new BarRelease { StartRelease = startRelease, EndRelease = endRelease };
            release.Name = release.Description();
            return release;
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // The end's DegreeOfFreedom flags are its complete fixity, in the span's local axes, in exactly
        // the form of a Constraint6DOF: a set flag is a connected (fixed) direction. Read from a live
        // model, a pinned end reports Fx, Fy, Fz, Mx; a fixed or cantilever end all six; a torsion
        // release drops Mx and an axial release drops Fx. So Fx..Mz map one to one onto
        // TranslationX..RotationZ.
        //
        // Local axes: x along the span, with bending about y the major axis - the same correspondence as
        // Convert/ToBHoM/BarForce.cs (TSD My -> BHoM MY) and Section.cs (major second moment -> Iy).
        //
        // The named properties (axial/torsional release, major/minor rotational stiffness) are only
        // needed where the flags are not reported, and to turn a released rotation into a spring where
        // Tekla Structural Designer gives it one. A span end with no release data at all is fully
        // fixed, the default Tekla Structural Designer connection.
        private static Constraint6DOF ToBHoMEnd(this ISpanReleases releases, out bool approximated, out bool isFixed)
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

            // BHoM's own create: a direction that is not fixed is a spring if it has a stiffness, free if not.
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

        // Refines a rotational direction with the stiffness Tekla Structural Designer gives it. Fixed,
        // Release and unreported leave the fixity as it is. A linear spring is carried exactly, in
        // N.m/rad. The rest have no BHoM equivalent and are flagged as approximated: nominally pinned as
        // free, nominally fixed as fixed, and partially fixed or non linear springs as a linear spring of
        // the reported stiffness - or free if none is reported.
        private static bool RotationalFixity(IStiffnessData stiffness, bool isFixed, out double spring, ref bool approximated)
        {
            spring = 0;
            double value = stiffness != null ? stiffness.Stiffness.ValueOrDefault(0.0) * MomentScale : 0.0;     // N.mm/rad -> N.m/rad

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
