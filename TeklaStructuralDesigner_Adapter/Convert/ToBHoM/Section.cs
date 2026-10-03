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
using System.Linq;
using BH.Engine.Base;
using BH.oM.Spatial.ShapeProfiles;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SectionProperties;
using TSD.API.Remoting.Sections;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        // Every steel section dataset BHoM ships (EU, UK and US). Library_Engine resolves a folder path
        // to all datasets beneath it.
        private const string SteelSectionLibrary = "Structure\\SectionProperties";

        // How far a library section's area may differ from the one Tekla Structural Designer reports
        // for the section of the same name before the match is refused. Guards against two catalogues
        // sharing a designation for slightly different sections, not a precision check.
        private const double LibraryAreaTolerance = 0.05;

        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // Looks a catalogue steel section up in the BHoM section library by name, through
        // Library.Query.Match, which already ignores case and white space. Tekla Structural Designer's
        // own names ("UB 305x165x40", "IPE 300", "HE 300 B", "W14X90") mostly follow the same
        // designations as the BHoM datasets, so both the long and short name are tried as they are.
        //
        // The library section is cloned rather than returned, because Library_Engine hands out shared
        // cached instances, and its material is replaced with the grade the model actually uses - the
        // datasets carry a default S355/A992 on every section.
        public static SteelSection MatchLibrarySection(this ISection section, Steel material, out string rejectedReason)
        {
            rejectedReason = null;

            if (section == null || !(section is INonParametricSection))
                return null;

            double tsdArea = section.CrossSectionalArea * AreaScale;

            foreach (string candidate in new[] { section.LongName, section.ShortName }.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
            {
                SteelSection match = Engine.Library.Query.Match(SteelSectionLibrary, candidate, true, true) as SteelSection;
                if (match == null)
                    continue;

                if (tsdArea > 0 && Math.Abs(match.Area - tsdArea) > LibraryAreaTolerance * tsdArea)
                {
                    rejectedReason = "'" + candidate + "' matched the BHoM library by name, but its area differs from the one Tekla Structural Designer reports by more than " +
                        (LibraryAreaTolerance * 100) + "%";
                    continue;
                }

                SteelSection result = match.ShallowClone(true);
                result.Material = material;
                return result;
            }

            return null;
        }

        /***************************************************/

        // The BHoM shape profile equivalent to a Tekla Structural Designer section, or null with the
        // reason there is none. Dimensions are read from the typed section interfaces - the API exposes
        // each shape family as its own interface - and converted from millimetres. Only shapes with an
        // unambiguous BHoM profile are mapped; the rest fall back to explicit constants in the caller
        // rather than being approximated by a shape they are not.
        //
        // Corner and root radii are carried where the API reports them. Hollow section corner radii are
        // not reported, so boxes are built square cornered, which slightly overstates their properties -
        // catalogue hollow sections that match the library by name are unaffected.
        //
        // The flip flags on parametric (concrete) sections are mapped onto the BHoM profile's mirror
        // flags one to one. That correspondence has not been checked against a live model.
        public static IProfile ToBHoMProfile(this ISection section, out string unsupportedReason)
        {
            unsupportedReason = null;
            if (section == null)
            {
                unsupportedReason = "no section is assigned";
                return null;
            }

            const double s = LengthScale;

            // Catalogue (steel and timber) sections

            ISymmetricISection iSection = section as ISymmetricISection;
            if (iSection != null)
                return Engine.Spatial.Create.ISectionProfile(iSection.Depth * s, iSection.Breadth * s, iSection.WebThickness * s, iSection.FlangeThickness * s, iSection.RootRadius * s, 0);

            IAsymmetricBeamSection asymmetric = section as IAsymmetricBeamSection;
            if (asymmetric != null)
            {
                return Engine.Spatial.Create.FabricatedISectionProfile(asymmetric.Depth * s, asymmetric.TopFlangeBreadth * s, asymmetric.BottomFlangeBreadth * s,
                    asymmetric.WebThickness * s, asymmetric.FlangeThickness * s, asymmetric.FlangeThickness * s, 0);
            }

            // Plated (fabricated) I sections, such as "PB 1000x500/35x457.3". Flanges can differ top to
            // bottom, which is exactly what FabricatedISectionProfile takes. No weld size is reported.
            IPlatedISectionV2 plated = section as IPlatedISectionV2;
            if (plated != null)
            {
                return Engine.Spatial.Create.FabricatedISectionProfile(plated.Depth * s, plated.TopFlangeBreadth * s, plated.BottomFlangeBreadth * s,
                    plated.WebThickness * s, plated.TopFlangeThickness * s, plated.BottomFlangeThickness * s, 0);
            }

            IChannel channel = section as IChannel;
            if (channel != null)
                return Engine.Spatial.Create.ChannelProfile(channel.Depth * s, channel.Breadth * s, channel.WebThickness * s, channel.FlangeThickness * s, channel.RootRadius * s, 0);

            ITee tee = section as ITee;
            if (tee != null)
                return Engine.Spatial.Create.TSectionProfile(tee.Depth * s, tee.Breadth * s, tee.WebThickness * s, tee.FlangeThickness * s, tee.RootRadius * s, 0);

            ISingleAngleSection angle = section as ISingleAngleSection;
            if (angle != null)
            {
                return Engine.Spatial.Create.AngleProfile(angle.LongLegLength * s, angle.ShortLegLength * s, angle.Thickness * s, angle.Thickness * s,
                    angle.RootRadius * s, angle.ToeRadius * s);
            }

            ISquareHollowSection square = section as ISquareHollowSection;
            if (square != null)
                return Engine.Spatial.Create.BoxProfile(square.Depth * s, square.Breadth * s, square.WallThickness * s);

            IRectangularHollowSection rectangularHollow = section as IRectangularHollowSection;
            if (rectangularHollow != null)
                return Engine.Spatial.Create.BoxProfile(rectangularHollow.Depth * s, rectangularHollow.Breadth * s, rectangularHollow.WallThickness * s);

            ICircularHollowSection circularHollow = section as ICircularHollowSection;
            if (circularHollow != null)
                return Engine.Spatial.Create.TubeProfile(circularHollow.OuterDiameter * s, circularHollow.WallThickness * s);

            IRod rod = section as IRod;
            if (rod != null)
                return Engine.Spatial.Create.CircleProfile(rod.OuterDiameter * s);

            IBar flat = section as IBar;
            if (flat != null)
                return Engine.Spatial.Create.RectangleProfile(flat.Depth * s, flat.Thickness * s);

            ITimberBeamSection timber = section as ITimberBeamSection;
            if (timber != null)
                return Engine.Spatial.Create.RectangleProfile(timber.Depth * s, timber.Breadth * s);

            // Parametric (concrete) sections

            IParametricRectangularSection rectangle = section as IParametricRectangularSection;
            if (rectangle != null)
                return Engine.Spatial.Create.RectangleProfile(rectangle.Depth * s, rectangle.Breadth * s);

            IParametricCircularSection circle = section as IParametricCircularSection;
            if (circle != null)
                return Engine.Spatial.Create.CircleProfile(circle.OuterDiameter * s);

            IParametricTSection tSection = section as IParametricTSection;
            if (tSection != null)
                return ParametricTProfile(tSection);

            IParametricLSection lSection = section as IParametricLSection;
            if (lSection != null)
            {
                return Engine.Spatial.Create.AngleProfile(lSection.Depth * s, lSection.Breadth * s, lSection.WebThickness * s, lSection.FlangeThickness * s, 0, 0,
                    lSection.IsFlippedHorizontally, lSection.IsFlippedVertically);
            }

            IParametricISection parametricI = section as IParametricISection;
            if (parametricI != null)
                return ParametricIProfile(parametricI);

            IParametricCSection cSection = section as IParametricCSection;
            if (cSection != null)
            {
                // A channel flipped vertically is the same channel, so only the horizontal flip matters.
                if (Math.Abs(cSection.TopFlangeThickness - cSection.BottomFlangeThickness) < 1e-6)
                {
                    return Engine.Spatial.Create.ChannelProfile(cSection.Depth * s, cSection.Breadth * s, cSection.WebThickness * s, cSection.TopFlangeThickness * s, 0, 0,
                        cSection.IsFlippedHorizontally);
                }

                unsupportedReason = "a C section with unequal flange thicknesses has no BHoM profile equivalent";
                return null;
            }

            unsupportedReason = "its shape (" + section.SectionGeometry + ") has no BHoM profile equivalent";
            return null;
        }

        /***************************************************/

        // The section constants Tekla Structural Designer reports, without a shape. The fallback for any
        // section ToBHoMProfile cannot represent. Deliberately lossy: the API reports no section moduli
        // or radii of gyration on the base ISection, and those are left at zero rather than derived.
        //
        // Axes: Iy is the major axis and Iz the minor, consistent with BarForce's convention that
        // MY/FZ are the major axis components - see Convert/ToBHoM/BarForce.cs.
        public static ExplicitSection ToBHoMExplicit(this ISection section, IMaterialFragment material)
        {
            if (section == null)
                return null;

            return new ExplicitSection
            {
                Name = SectionName(section),
                Material = material,
                Area = section.CrossSectionalArea * AreaScale,
                Iy = section.MajorAxisSecondMomentOfArea * InertiaScale,
                Iz = section.MinorAxisSecondMomentOfArea * InertiaScale,
                J = section.TorsionConstant * InertiaScale,
                Asy = section.ShearAreaLoadedParallelToMinorAxis * AreaScale,
                Asz = section.ShearAreaLoadedParallelToMajorAxis * AreaScale,
            };
        }

        /***************************************************/

        public static string SectionName(this ISection section)
        {
            return section == null ? "" : (section.LongName ?? section.ShortName ?? "");
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // A symmetric T maps onto TSectionProfile. One whose stem is off centre needs the generalised
        // profile, which takes the flange outstand either side of the stem separately.
        private static IProfile ParametricTProfile(IParametricTSection t)
        {
            const double s = LengthScale;

            double breadth = t.Breadth;
            double web = t.WebThickness;
            double stem = t.DistanceOfStemCentre;

            if (Math.Abs(stem - breadth / 2) < 0.5)      // mm
            {
                return Engine.Spatial.Create.TSectionProfile(t.Depth * s, breadth * s, web * s, t.FlangeThickness * s, 0, 0,
                    t.IsFlippedVertically);
            }

            double left = stem - web / 2;
            double right = breadth - stem - web / 2;
            if (t.IsFlippedHorizontally)
            {
                double swap = left;
                left = right;
                right = swap;
            }

            return Engine.Spatial.Create.GeneralisedTSectionProfile(t.Depth * s, web * s, left * s, t.FlangeThickness * s, right * s, t.FlangeThickness * s,
                t.IsFlippedVertically);
        }

        /***************************************************/

        private static IProfile ParametricIProfile(IParametricISection i)
        {
            const double s = LengthScale;

            bool flipped = i.IsFlippedVertically;
            double topBreadth = flipped ? i.BottomFlangeBreadth : i.TopFlangeBreadth;
            double topThickness = flipped ? i.BottomFlangeThickness : i.TopFlangeThickness;
            double bottomBreadth = flipped ? i.TopFlangeBreadth : i.BottomFlangeBreadth;
            double bottomThickness = flipped ? i.TopFlangeThickness : i.BottomFlangeThickness;

            if (Math.Abs(topBreadth - bottomBreadth) < 1e-6 && Math.Abs(topThickness - bottomThickness) < 1e-6)
                return Engine.Spatial.Create.ISectionProfile(i.Depth * s, topBreadth * s, i.WebThickness * s, topThickness * s, 0, 0);

            return Engine.Spatial.Create.FabricatedISectionProfile(i.Depth * s, topBreadth * s, bottomBreadth * s, i.WebThickness * s,
                topThickness * s, bottomThickness * s, 0);
        }

        /***************************************************/
    }
}
