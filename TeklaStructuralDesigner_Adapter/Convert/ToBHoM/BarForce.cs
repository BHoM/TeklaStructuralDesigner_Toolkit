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

using BH.Engine.Geometry;
using BH.oM.Geometry;
using BH.oM.Structure.Results;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // The one place units, axes and signs are decided for every bar force this adapter returns,
        // whichever of the two read routes produced it - both the batched solver route
        // (IElementEndForces.StartForce/EndForce) and the per span end route
        // (IMemberSpan.GetEndForceAsync) return the same IForce3DLocal type, so one converter serves
        // both.
        //
        // Units: the API documents IForce3DLocal's forces in N and its moments in N.mm, so only the
        // moments are scaled - see Convert/Units.cs. Nothing is rounded.
        //
        // Three things separate what Tekla Structural Designer reports from a BHoM BarForce, all
        // measured on a live Tekla Structural Designer 2026 SP1 model in October 2026:
        //
        //  - End forces, not section forces. IForce3DLocal is the force the node applies to the element
        //    ("forces acting on an element"): a column in compression reads +267.9 kN at its base and
        //    -251.3 kN at its top, and its base force equals the support reaction. A BarForce is the
        //    section force on the positive face of a cut - compression negative at both ends - which is
        //    the end force reversed at the start of an element and unchanged at its end. isStart says
        //    which end the force was reported for.
        //
        //  - Moments are named after the shear they go with. Tekla Structural Designer's Mz pairs with
        //    Fz - on a column, the end Mz is exactly Fz times the length - so it is the moment about
        //    local y, the major axis moment, and its My is the moment about local z. BHoM names a
        //    moment by the axis it acts about, so Mz goes to MY and My to MZ.
        //
        //  - On a non-vertical member Tekla Structural Designer's local z points down - both end shears
        //    of a beam under self weight are negative - where BHoM's points up, so its local y and z are
        //    BHoM's turned half way round local x, and every y and z component changes sign. On a
        //    vertical member the two agree: with the quarter turn Convert/ToBHoM/Bar.cs gives it, BHoM's
        //    local y runs along global X and local z along global Y, exactly as Tekla Structural
        //    Designer's do - checked against the support reactions of single columns under wind.
        //    isVertical must be decided the same way as that quarter turn; see IsVertical below.
        //
        // Checked on a fixed ended beam under self weight: FZ -6.70 kN at the start and +6.39 kN at the
        // end, MY +12.55 and +11.06 kNm - hogging positive - as BHoM's sign convention has it. Sloped
        // and rotated members are taken to follow the same rule as horizontal ones, which has not been
        // measured.
        //
        // TeklaStructuralDesignerPullConfig.SwapMajorMinorAxes swaps the major and minor results after
        // all of this, as an escape hatch for a model that demonstrates otherwise.
        public static BarForce ToBHoM(this IForce3DLocal force, string objectId, int resultCase, double position, int divisions, bool isStart, bool isVertical, bool swapMajorMinorAxes)
        {
            double end = isStart ? -1.0 : 1.0;
            double frame = isVertical ? 1.0 : -1.0;

            double fx = end * force.Fx;
            double fy = end * frame * force.Fy;
            double fz = end * frame * force.Fz;
            double mx = end * force.Mx * MomentScale;
            double my = end * frame * force.Mz * MomentScale;
            double mz = end * frame * force.My * MomentScale;

            if (swapMajorMinorAxes)
                return new BarForce(objectId, resultCase, -1, 0.0, position, divisions, fx, fz, fy, mx, mz, my);

            return new BarForce(objectId, resultCase, -1, 0.0, position, divisions, fx, fy, fz, mx, my, mz);
        }

        /***************************************************/

        // Whether a span is vertical by the same test Convert/ToBHoM/Bar.cs uses to give a vertical
        // member its quarter turn - BHoM's own Line.IsVertical - so that the force mapping above and the
        // Bar's orientation never disagree about which members are columns.
        public static bool IsVertical(this TsdSpanIdentity span)
        {
            if (span?.StartPosition == null || span.EndPosition == null)
                return false;

            return new Line { Start = span.StartPosition, End = span.EndPosition }.IsVertical();
        }

        /***************************************************/
    }
}
