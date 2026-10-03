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

using System.Globalization;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SurfaceProperties;
using TSD.API.Remoting.Decking;
using TSD.API.Remoting.Structure;
using BHoMPanelType = BH.oM.Structure.SurfaceProperties.PanelType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // The BHoM surface property of a slab item. depth is the item's own depth where it overrides the
        // slab's, in mm, as Tekla Structural Designer reports it. It is the overall depth of the slab:
        // for a precast slab, plank plus structural topping (checked on a live model, where a slab of
        // 150mm planks with a 75mm structural topping reported a depth of 225mm); for a composite slab,
        // the depth including the deck. Every directional property is built running along local x, which
        // the Panel's local x is set to the span direction of - see ToBHoMPanel.
        //
        // detailed chooses between two representations of composite and precast slabs:
        //
        //   - false (the default): a ConstantThickness of the overall depth, whatever the slab type. That
        //     is the only slab representation the Robot and ETABS toolkits can push: Robot's surface
        //     property converter handles ConstantThickness, Ribbed and Waffle and throws a null reference
        //     on anything else, and ETABS' silently creates nothing for anything else. The deck or plank
        //     is kept in the property's name and the slab type on the Panel's
        //     TeklaStructuralDesignerPanelProperties, so the slab can still be recognised later.
        //   - true: the BHoM type nearest to the slab - a SlabOnDeck built from the deck profile for a
        //     composite slab, a HollowCore or ConstantThickness plank for a precast slab, wrapped in a
        //     ToppedSlab where the topping is structural - for packages and workflows that understand
        //     those types.
        //
        // Where the slab has a shape BHoM could represent but the API does not expose the dimensions of
        // it, the property falls back to a solid slab of the same overall depth and approximation says
        // why, for the caller to report once rather than per slab item:
        //   - Ribbed and waffle slabs: the API reports only the overall depth, not the rib or waffle
        //     geometry, so they come through as solid slabs, overstating weight and stiffness.
        //   - Hollow core planks: the void geometry is not exposed, so the HollowCore property carries no
        //     Openings and is read by most packages as solid.
        //
        // Assumptions not yet checked against a live model with those slab types:
        //   - For a composite slab, Depth is the overall depth including the deck, so the concrete above
        //     the flutes is Depth less the deck's DepthOfSheet.
        //   - The deck's Breadth1 is the trough (bottom) width and Breadth2 the crest (top) width.
        //   - The API exposes no material for the deck, so SlabOnDeck.DeckMaterial is left null.
        public static ISurfaceProperty ToBHoMSurfaceProperty(this ISlabData data, double depth, IMaterialFragment material, BHoMPanelType panelType, bool detailed, out string approximation)
        {
            approximation = null;

            SlabType slabType = data != null ? data.SlabType.ValueOrDefault(SlabType.Unknown) : SlabType.Unknown;
            IDeck deck = data != null ? data.Deck.ValueOrDefault() : null;
            IProfile profile = deck != null ? deck.Profile.ValueOrDefault() : null;
            IGauge gauge = deck != null ? deck.Gauge.ValueOrDefault() : null;

            switch (slabType)
            {
                case SlabType.Composite:
                    string deckName = ((profile != null ? profile.ProfileName : "") + " " + (gauge != null ? gauge.Name : "")).Trim();

                    if (!detailed)
                    {
                        approximation = "they are composite slabs, pulled as solid slabs of their overall depth including the deck. Set DetailedSurfaceProperties on the pull configuration to pull them as SlabOnDeck instead, for a package that supports it";
                        return Solid(depth, material, panelType, ("Composite on " + deckName).Trim());
                    }

                    IMetalProfile metal = profile as IMetalProfile;
                    if (metal != null && metal.DepthOfSheet > 0 && metal.DepthOfSheet < depth)
                    {
                        IMetalGauge metalGauge = gauge as IMetalGauge;

                        return new SlabOnDeck
                        {
                            Name = "Composite " + Millimetres(depth) + " " + deckName + " " + MaterialName(material),
                            Material = material,
                            SlabThickness = (depth - metal.DepthOfSheet) * LengthScale,
                            Direction = PanelDirection.X,
                            DeckName = deckName,
                            DeckHeight = metal.DepthOfSheet * LengthScale,
                            DeckSpacing = metal.DistanceOfRibCenters * LengthScale,
                            DeckBottomWidth = metal.Breadth1 * LengthScale,
                            DeckTopWidth = metal.Breadth2 * LengthScale,
                            DeckThickness = metalGauge != null ? metalGauge.Size * LengthScale : 0,
                            DeckVolumeFactor = 1,
                            PanelType = panelType,
                        };
                    }

                    approximation = "they are composite slabs whose deck profile could not be read, so they have been pulled as solid slabs of their overall depth";
                    break;

                case SlabType.Precast:
                    ToppingOption topping = data.ToppingOption.ValueOrDefault(ToppingOption.None);
                    double toppingDepth = topping == ToppingOption.Structural ? data.ToppingDepth.ValueOrDefault(0) : 0;
                    double plankDepth = gauge != null && gauge.Depth > 0 ? gauge.Depth : depth - toppingDepth;
                    string plankName = ((profile != null ? profile.ProfileName : "Precast") + " " + (gauge != null ? gauge.Name : Millimetres(plankDepth))).Trim();

                    if (!detailed)
                    {
                        approximation = "they are precast slabs, pulled as solid slabs of their overall depth (plank plus any structural topping). Set DetailedSurfaceProperties on the pull configuration to pull the plank and topping separately, for a package that supports it";
                        return Solid(depth, material, panelType, toppingDepth > 0 ? plankName + " + " + Millimetres(toppingDepth) + " topping" : plankName);
                    }

                    IPrecastConcreteProfile precast = profile as IPrecastConcreteProfile;
                    ISurfaceProperty plank;
                    if (precast != null && precast.PrecastConcreteGeometry == PrecastConcreteGeometry.HollowCore)
                    {
                        plank = new HollowCore
                        {
                            Name = plankName + " " + MaterialName(material),
                            Thickness = plankDepth * LengthScale,
                            Material = material,
                            Direction = PanelDirection.X,
                            PanelType = panelType,
                        };
                        approximation = "they are hollow core planks, whose void geometry the API does not expose, so they have been pulled without their voids";
                    }
                    else
                    {
                        plank = Solid(plankDepth, material, panelType, plankName);
                    }

                    if (toppingDepth > 0)
                    {
                        return new ToppedSlab
                        {
                            Name = plank.Name + " + " + Millimetres(toppingDepth) + " topping",
                            BaseProperty = plank,
                            ToppingThickness = toppingDepth * LengthScale,
                            Material = material,
                            PanelType = panelType,
                        };
                    }

                    return plank;

                case SlabType.Rib:
                case SlabType.Waffle:
                    approximation = "they are ribbed or waffle slabs, whose rib geometry the API does not expose, so they have been pulled as solid slabs of their overall depth";
                    break;
            }

            return Solid(depth, material, panelType, slabType == SlabType.Unknown ? "Slab" : slabType.ToString());
        }

        /***************************************************/

        // A wall panel is a solid wall of the panel's thickness, whatever the wall type.
        public static ISurfaceProperty ToBHoMSurfaceProperty(this IStructuralWallPanelData data, IMaterialFragment material)
        {
            double thickness = data != null ? data.Thickness.ValueOrDefault(0) : 0;
            return Solid(thickness, material, BHoMPanelType.Wall, "Wall");
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        private static ConstantThickness Solid(double depth, IMaterialFragment material, BHoMPanelType panelType, string prefix)
        {
            return new ConstantThickness
            {
                Name = prefix + " " + Millimetres(depth) + " " + MaterialName(material),
                Thickness = depth * LengthScale,
                Material = material,
                PanelType = panelType,
            };
        }

        /***************************************************/

        private static string Millimetres(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture) + "mm";
        }

        /***************************************************/

        private static string MaterialName(IMaterialFragment material)
        {
            return material != null && !string.IsNullOrWhiteSpace(material.Name) ? material.Name : "";
        }

        /***************************************************/
    }
}
