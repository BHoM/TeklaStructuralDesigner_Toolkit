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

using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Loads;
using TSD.API.Remoting.Loading;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        public static Loadcase ToBHoM(this ILoadcase loadcase, string identifier)
        {
            Loadcase result = new Loadcase
            {
                Name = identifier,
                Number = loadcase.Index,
                Nature = loadcase.Type.ValueOrDefault(LoadcaseType.Unknown).ToBHoM(),
            };

            result.Fragments.Add(new TeklaStructuralDesignerId { Id = loadcase.Id });
            return result;
        }

        /***************************************************/

        // Tekla Structural Designer's loadcase taxonomy is far finer than BHoM's LoadNature, so this is
        // a deliberate narrowing rather than a one to one map. Written as an explicit switch, like
        // Convert/ToTeklaStructuralDesigner/AnalysisType.cs, so that a value added by a future version
        // surfaces as a warning instead of being silently folded into whatever it happens to sit next
        // to numerically.
        //
        // The groupings worth knowing about:
        //  - Everything permanent maps to Dead, except the ancillary and equipment "dead" variants,
        //    which are superimposed on the structure rather than part of it, and so map to SuperDead.
        //  - Snow, drift and the snow volume cases all map to Snow; Ice has no BHoM equivalent and is
        //    grouped with it rather than lost, because it is an environmental accretion load and is
        //    factored alongside snow in the codes Tekla Structural Designer supports.
        //  - Settlement maps to Other: BHoM has no settlement nature, and Other is honest about that.
        //  - MinimumLateralLoad maps to Notional, which is exactly what it is.
        public static LoadNature ToBHoM(this LoadcaseType loadcaseType)
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
                    return LoadNature.Other;

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
