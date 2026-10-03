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

// BHoM and Tekla Structural Designer both call their load base interface ILoad, and this file needs
// both. The BHoM one is aliased rather than the Tekla one, so that every unqualified type in the body
// reads as the Tekla API type it is.
using BHoMLoad = BH.oM.Structure.Loads.ILoad;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // Nodal loads are given as global force and moment vectors against a construction point, so
        // there is no direction enum to interpret and no projection to worry about.
        //
        // The Z of the force vector follows the same gravity positive convention as global Z member
        // loads, despite being presented as a vector: on a live model all 50 slab self weight nodal
        // loads had a positive Z. It is reversed to give BHoM's -Z for a downward load. The moment
        // vector is passed through unchanged - no nodal moments were in the model to check it against.
        public static PointLoad ToBHoM(this INodalLoad load, Node node, Loadcase loadcase)
        {
            Vector force = load.ForceAsVector.ToBHoMVector() * ForceScale;
            force.Z = -force.Z;

            return new PointLoad
            {
                Loadcase = loadcase,
                Objects = new BHoMGroup<Node> { Elements = new List<Node> { node } },
                Axis = LoadAxis.Global,
                Force = force,
                Moment = load.MomentAsVector.ToBHoMVector() * MomentScale,
            };
        }

        // Member loads

        // Dispatches one Tekla Structural Designer member load onto the BHoM load that represents it,
        // or returns null with a reason if there is no honest representation. The reason is returned
        // rather than recorded here so the caller can count identical reasons and report them once,
        // instead of emitting one warning per load on a model with thousands.
        public static BHoMLoad ToBHoM(this IMemberLoad load, Bar bar, Loadcase loadcase, out string skipReason)
        {
            skipReason = null;

            Vector direction;
            LoadAxis axis;
            if (!TryDirection(load.Direction, out direction, out axis))
            {
                skipReason = "their direction is '" + load.Direction + "', which has no unambiguous BHoM equivalent";
                return null;
            }

            // BHoM's Projected means the load is projected onto the plane normal to its direction.
            // Tekla Structural Designer's Projection names the plane explicitly, so any named plane is a
            // projected load; NoProjection and Unknown are not. Its separate Measuring property
            // describes how the load's own distances are measured, which is handled by the caller.
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

                        // A partial UDL. BHoM has no "uniform over part of a bar" load, so it is
                        // expressed as a varying load whose two ends carry the same value - exact, not
                        // an approximation.
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

                // Torsion moments act about the member's own longitudinal axis by definition, so the
                // Direction enum is not consulted for them: the moment goes on local x, which is what
                // BHoM reads as torsion. Axis is forced Local for the same reason - a torsion moment
                // resolved into global axes would be meaningless.
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

                // Deliberately unsupported, with the reason stated rather than a guess made:
                //
                // Trapezoidal exposes only a Distance and a single Load. A trapezoid needs two
                // magnitudes and two positions, so the shape cannot be reconstructed from the API
                // surface - emitting a UDL from it would silently change the loading.
                //
                // EccMoment is the moment arising from a connection eccentricity. It is a consequence
                // of how a load is attached rather than an applied load, and pulling it alongside the
                // load that causes it would double count.
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
        /****            Private Methods                ****/
        /***************************************************/

        private static bool TryDirection(LoadDirection direction, out Vector unit, out LoadAxis axis)
        {
            // Tekla Structural Designer's global Z loads are positive downwards - gravity positive -
            // while BHoM's Force is the direction the load actually acts in, so a downward load is -Z.
            // Checked on a live model: every self weight, dead, imposed and snow member load in Zg
            // carried a positive value, while global X and Y loads were positive along +X and +Y (wind
            // from the north negative in Y, from the south positive).
            //
            // U and V are undocumented in the API and do not correspond to either of BHoM's two load
            // axes. Refused rather than guessed: a load applied along the wrong axis is worse than a
            // load reported as unconvertible.
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

        // RelativePositions is false throughout: Tekla Structural Designer gives the offset and loaded
        // length as absolute distances in millimetres, and converting them to fractions would need the
        // span length and would lose precision for nothing.
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
