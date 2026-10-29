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
        /**** Public Methods                            ****/
        /***************************************************/

        // A slab item takes its slab's property, at its own depth where it overrides the slab's, and is
        // a DropPanel where it is a column drop. See the ISlabData overload for the rest.
        public static ISurfaceProperty FromTsdSurfaceProperty(this ISlabItem item, ISlabData slabData, IMaterialFragment material, bool detailed, out string approximation)
        {
            ISlabItemData itemData = item != null ? item.SlabItemData.ValueOrDefault() : null;

            double depth = itemData != null && itemData.OverrideSlabDepth.ValueOrDefault(false)
                ? itemData.Depth.ValueOrDefault(0)
                : (slabData != null ? slabData.Depth.ValueOrDefault(0) : 0);
            if (depth <= 0 && itemData != null)
                depth = itemData.Depth.ValueOrDefault(0);

            BHoMPanelType panelType = item != null && item.IsColumnDrop.ValueOrDefault(false) ? BHoMPanelType.DropPanel : BHoMPanelType.Slab;

            return slabData.FromTsdSurfaceProperty(depth, material, panelType, detailed, out approximation);
        }

        /***************************************************/

        // depth is the slab item's overall depth in mm (plank plus structural topping, or including the
        // deck). Directional properties run along local x, the Panel's span direction.
        //  - detailed false: a ConstantThickness of the overall depth, the only slab type Robot and ETABS
        //    can push. The deck or plank stays in the name.
        //  - detailed true: SlabOnDeck, HollowCore or a ToppedSlab, for packages that support them.
        // Where the API does not expose the geometry (ribs, waffles, hollow core voids) the slab falls
        // back to solid and approximation says why, for the caller to report once.
        public static ISurfaceProperty FromTsdSurfaceProperty(this ISlabData data, double depth, IMaterialFragment material, BHoMPanelType panelType, bool detailed, out string approximation)
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

                    // Assumes Depth includes the deck, Breadth1 is the trough and Breadth2 the crest.
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

        // A wall panel is a solid wall of its thickness, whatever the wall type.
        public static ISurfaceProperty FromTsdSurfaceProperty(this IStructuralWallPanelData data, IMaterialFragment material)
        {
            double thickness = data != null ? data.Thickness.ValueOrDefault(0) : 0;
            return Solid(thickness, material, BHoMPanelType.Wall, "Wall");
        }

        /***************************************************/
        /**** Private Methods                           ****/
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
