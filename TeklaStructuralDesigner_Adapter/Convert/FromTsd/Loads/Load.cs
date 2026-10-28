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
using BH.oM.Base;
using BH.oM.Geometry;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;
using TSD.API.Remoting.Loading;
using BHoMLoad = BH.oM.Structure.Loads.ILoad;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // The force's Z is gravity positive, as for global Z member loads, so it is reversed.
        public static PointLoad FromTsdPointLoad(this INodalLoad load, Node node, Loadcase loadcase)
        {
            Vector force = load.ForceAsVector.FromTsdVector() * ForceScale;
            force.Z = -force.Z;

            return new PointLoad
            {
                Loadcase = loadcase,
                Objects = new BHoMGroup<Node> { Elements = new List<Node> { node } },
                Axis = LoadAxis.Global,
                Force = force,
                Moment = load.MomentAsVector.FromTsdVector() * MomentScale,
            };
        }

        /***************************************************/

        // The BHoM load for a member load, or null with the reason there is none. Reasons are returned,
        // not recorded, so the caller can report each one once.
        public static BHoMLoad FromTsdBarLoad(this IMemberLoad load, Bar bar, Loadcase loadcase, out string skipReason)
        {
            skipReason = null;

            Vector direction;
            LoadAxis axis;
            if (!TryDirection(load.Direction, out direction, out axis))
            {
                skipReason = "their direction is '" + load.Direction + "', which has no unambiguous BHoM equivalent";
                return null;
            }

            // Any named projection plane is a projected load.
            bool projected = load.Projection != Projection.NoProjection && load.Projection != Projection.Unknown;

            switch (load.MemberLoadType)
            {
                case MemberLoadType.FullUdl:
                    {
                        IMemberFullUniformlyDistributedLoad udl = load as IMemberFullUniformlyDistributedLoad;
                        if (udl == null)
                            break;

                        return Uniform(bar, loadcase, axis, projected, direction * (udl.Load * ForcePerLengthScale), null);
                    }

                case MemberLoadType.Udl:
                    {
                        IMemberUniformlyDistributedLoad udl = load as IMemberUniformlyDistributedLoad;
                        if (udl == null)
                            break;

                        // A partial UDL is a varying load with equal ends.
                        Vector value = direction * (udl.Load * ForcePerLengthScale);
                        return Varying(bar, loadcase, axis, projected, udl.Distance, udl.Length, value, value, false);
                    }

                case MemberLoadType.Vdl:
                    {
                        IMemberVariablyDistributedLoad vdl = load as IMemberVariablyDistributedLoad;
                        if (vdl == null)
                            break;

                        return Varying(bar, loadcase, axis, projected, vdl.Distance, vdl.Length,
                            direction * (vdl.StartLoad * ForcePerLengthScale),
                            direction * (vdl.EndLoad * ForcePerLengthScale), false);
                    }

                case MemberLoadType.Force:
                    {
                        IMemberForceLoad force = load as IMemberForceLoad;
                        if (force == null)
                            break;

                        return new BarPointLoad
                        {
                            Loadcase = loadcase,
                            Objects = Group(bar),
                            Axis = axis,
                            Projected = projected,
                            DistanceFromA = force.Distance * LengthScale,
                            Force = direction * (force.Load * ForceScale),
                        };
                    }

                case MemberLoadType.Moment:
                    {
                        IMemberMomentLoad moment = load as IMemberMomentLoad;
                        if (moment == null)
                            break;

                        return new BarPointLoad
                        {
                            Loadcase = loadcase,
                            Objects = Group(bar),
                            Axis = axis,
                            Projected = projected,
                            DistanceFromA = moment.Distance * LengthScale,
                            Moment = direction * (moment.Load * MomentScale),
                        };
                    }

                // Torsion always acts about local x, whatever the Direction.
                case MemberLoadType.FullUdTorsionMoment:
                    {
                        IMemberFullUniformlyDistributedTorsionMomentLoad torsion = load as IMemberFullUniformlyDistributedTorsionMomentLoad;
                        if (torsion == null)
                            break;

                        return Uniform(bar, loadcase, LoadAxis.Local, projected, null, Torsion(torsion.Load));
                    }

                case MemberLoadType.UdTorsionMoment:
                    {
                        IMemberUniformlyDistributedTorsionMomentLoad torsion = load as IMemberUniformlyDistributedTorsionMomentLoad;
                        if (torsion == null)
                            break;

                        Vector value = Torsion(torsion.Load);
                        return Varying(bar, loadcase, LoadAxis.Local, projected, torsion.Distance, torsion.Length, value, value, true);
                    }

                case MemberLoadType.VdTorsionMoment:
                    {
                        IMemberVariablyDistributedTorsionMomentLoad torsion = load as IMemberVariablyDistributedTorsionMomentLoad;
                        if (torsion == null)
                            break;

                        return Varying(bar, loadcase, LoadAxis.Local, projected, torsion.Distance, torsion.Length,
                            Torsion(torsion.StartLoad), Torsion(torsion.EndLoad), true);
                    }

                case MemberLoadType.Trapezoidal:
                    skipReason = "a trapezoidal load's shape cannot be reconstructed from the API, which exposes only one magnitude and one distance for it";
                    return null;

                case MemberLoadType.EccMoment:
                    skipReason = "an eccentricity moment is a consequence of how a load is attached rather than an applied load, so pulling it would double count";
                    return null;
            }

            skipReason = "'" + load.MemberLoadType + "' is not a member load type this toolkit converts";
            return null;
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Global Z is gravity positive in Tekla Structural Designer, so Zg maps to -Z. X and Y are
        // positive along the axis. U and V are undocumented and refused.
        private static bool TryDirection(LoadDirection direction, out Vector unit, out LoadAxis axis)
        {
            axis = direction == LoadDirection.X || direction == LoadDirection.Y || direction == LoadDirection.Z ? LoadAxis.Local : LoadAxis.Global;

            switch (direction)
            {
                case LoadDirection.X:
                case LoadDirection.Xg:
                    unit = new Vector { X = 1 };
                    return true;
                case LoadDirection.Y:
                case LoadDirection.Yg:
                    unit = new Vector { Y = 1 };
                    return true;
                case LoadDirection.Z:
                    unit = new Vector { Z = 1 };
                    return true;
                case LoadDirection.Zg:
                    unit = new Vector { Z = -1 };
                    return true;
                default:
                    unit = null;
                    return false;
            }
        }

        /***************************************************/

        private static Vector Torsion(double load)
        {
            return new Vector { X = load * MomentPerLengthScale };
        }

        /***************************************************/

        private static BHoMGroup<Bar> Group(Bar bar)
        {
            return new BHoMGroup<Bar> { Elements = new List<Bar> { bar } };
        }

        /***************************************************/

        private static BarUniformlyDistributedLoad Uniform(Bar bar, Loadcase loadcase, LoadAxis axis, bool projected, Vector force, Vector moment)
        {
            return new BarUniformlyDistributedLoad
            {
                Loadcase = loadcase,
                Objects = Group(bar),
                Axis = axis,
                Projected = projected,
                Force = force ?? new Vector(),
                Moment = moment ?? new Vector(),
            };
        }

        /***************************************************/

        // Positions are absolute, as Tekla Structural Designer gives them.
        private static BarVaryingDistributedLoad Varying(
            Bar bar, Loadcase loadcase, LoadAxis axis, bool projected,
            double distance, double length, Vector start, Vector end, bool isMoment)
        {
            double startPosition = distance * LengthScale;
            double endPosition = (distance + length) * LengthScale;

            return new BarVaryingDistributedLoad
            {
                Loadcase = loadcase,
                Objects = Group(bar),
                Axis = axis,
                Projected = projected,
                RelativePositions = false,
                StartPosition = startPosition,
                EndPosition = endPosition,
                ForceAtStart = isMoment ? new Vector() : start,
                ForceAtEnd = isMoment ? new Vector() : end,
                MomentAtStart = isMoment ? start : new Vector(),
                MomentAtEnd = isMoment ? end : new Vector(),
            };
        }

        /***************************************************/
    }
}
