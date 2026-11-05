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

using BH.oM.Structure.Loads;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        public static Loadcase FromTsdLoadcase(this ILoadcase loadcase, string identifier, int number)
        {
            return new Loadcase
            {
                Name = identifier,
                Number = number,
                Nature = loadcase.Type.ValueOrDefault(LoadcaseType.Unknown).FromTsdLoadNature(),
            }.SetIdentity(identifier, loadcase.Id);
        }

        /***************************************************/

        // A deliberate narrowing of a much finer taxonomy. Ancillary and equipment dead loads are
        // SuperDead, ice is grouped with snow and settlement is Other. Unknown future values warn.
        public static LoadNature FromTsdLoadNature(this LoadcaseType loadcaseType)
        {
            switch (loadcaseType)
            {
                case LoadcaseType.Dead:
                case LoadcaseType.SelfWeight:
                case LoadcaseType.SlabDry:
                case LoadcaseType.SlabWet:
                case LoadcaseType.SlabUnitLoad:
                case LoadcaseType.RoofUnitLoad:
                    return LoadNature.Dead;

                case LoadcaseType.AncillaryDead:
                case LoadcaseType.PipeworkEmpty:
                case LoadcaseType.PipeworkContentDead:
                case LoadcaseType.PipeworkTestingDead:
                case LoadcaseType.CableTrayEmpty:
                case LoadcaseType.EquipmentEmpty:
                case LoadcaseType.EquipmentContentDead:
                case LoadcaseType.EquipmentTestingDead:
                    return LoadNature.SuperDead;

                case LoadcaseType.Imposed:
                case LoadcaseType.RoofImposed:
                case LoadcaseType.ImposedOther:
                case LoadcaseType.AncillaryLive:
                case LoadcaseType.PipeworkContent:
                case LoadcaseType.PipeworkTesting:
                case LoadcaseType.CableTrayContent:
                case LoadcaseType.EquipmentContent:
                case LoadcaseType.EquipmentTesting:
                case LoadcaseType.Crane:
                    return LoadNature.Live;

                case LoadcaseType.Wind:
                    return LoadNature.Wind;

                case LoadcaseType.Snow:
                case LoadcaseType.SnowDrift:
                case LoadcaseType.SnowVolume2:
                case LoadcaseType.SnowVolume3:
                case LoadcaseType.Ice:
                    return LoadNature.Snow;

                case LoadcaseType.SeismicElf:
                case LoadcaseType.SeismicRsa:
                    return LoadNature.Seismic;

                case LoadcaseType.Temperature:
                    return LoadNature.Temperature;

                case LoadcaseType.MinimumLateralLoad:
                    return LoadNature.Notional;

                case LoadcaseType.Settlement:
                case LoadcaseType.Unknown:
                    return LoadNature.Other;

                default:
                    Engine.Base.Compute.RecordWarning("Unrecognised Tekla Structural Designer loadcase type '" + loadcaseType +
                        "'; its LoadNature has been set to Other. This usually means the toolkit is older than the connected version of Tekla Structural Designer.");
                    return LoadNature.Other;
            }
        }

        /***************************************************/
    }
}
