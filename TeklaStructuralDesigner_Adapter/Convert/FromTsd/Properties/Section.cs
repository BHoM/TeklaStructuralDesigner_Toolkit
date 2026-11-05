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
using BH.oM.Spatial.ShapeProfiles;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SectionProperties;
using TSD.API.Remoting.Sections;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // The equivalent BHoM profile, or null with the reason there is none. Only unambiguous shapes are
        // mapped; the caller falls back to FromTsdExplicitSection for the rest. Hollow section corner
        // radii are not reported, so boxes are square cornered. Parametric flip flags map one to one onto
        // the profile's mirror flags (not yet verified on a live model).
        public static IProfile FromTsdProfile(this ISection section, out string unsupportedReason)
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
                // A vertically flipped channel is the same channel.
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

        // The reported section constants without a shape, for sections FromTsdProfile cannot represent.
        // Section moduli and radii of gyration are not reported and stay zero. Iy is the major axis.
        public static ExplicitSection FromTsdExplicitSection(this ISection section, IMaterialFragment material)
        {
            if (section == null)
                return null;

            return new ExplicitSection
            {
                Name = section.SectionName(),
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
        /**** Private Methods                           ****/
        /***************************************************/

        // An off centre stem needs the generalised T profile, which takes each outstand separately.
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
