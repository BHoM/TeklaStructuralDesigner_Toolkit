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

using BH.oM.Structure.Elements;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // What a member does in the structure. Truss chords and wall beam elements are beams; posts and
        // wall column elements are columns; ties and truss internals are braces. recognised is false for
        // a value newer than this toolkit, which is pulled as Undefined for the caller to report once.
        public static StructuralUsage1D FromTsdStructuralUsage(this MemberConstruction construction, out bool recognised)
        {
            recognised = true;

            switch (construction)
            {
                case MemberConstruction.SteelBeam:
                case MemberConstruction.CompositeBeam:
                case MemberConstruction.SteelJoist:
                case MemberConstruction.SteelTrussMemberTop:
                case MemberConstruction.SteelTrussMemberBottom:
                case MemberConstruction.SteelTrussMemberSide:
                case MemberConstruction.ConcreteBeam:
                case MemberConstruction.TimberBeam:
                case MemberConstruction.TimberTrussMemberTop:
                case MemberConstruction.TimberTrussMemberBottom:
                case MemberConstruction.TimberTrussMemberSide:
                case MemberConstruction.ColdFormedBeam:
                case MemberConstruction.ColdFormedTrussMemberTop:
                case MemberConstruction.ColdFormedTrussMemberBottom:
                case MemberConstruction.ColdFormedTrussMemberSide:
                case MemberConstruction.Purlin:
                case MemberConstruction.Rail:
                case MemberConstruction.EavesBeam:
                case MemberConstruction.ConcreteWallBeamElement:
                case MemberConstruction.ConcreteWallMeshBeamElement:
                case MemberConstruction.GeneralMaterialBeam:
                case MemberConstruction.BearingWallBeam:
                case MemberConstruction.ColdRolledBeam:
                case MemberConstruction.GroundBeam:
                case MemberConstruction.StiffeningBeam:
                case MemberConstruction.CouplingBeam:
                case MemberConstruction.GeneralWallMeshBeamElement:
                case MemberConstruction.MasonryWallMeshBeamElement:
                    return StructuralUsage1D.Beam;

                case MemberConstruction.SteelColumn:
                case MemberConstruction.CompositeColumn:
                case MemberConstruction.SteelGablePost:
                case MemberConstruction.SteelParapetPost:
                case MemberConstruction.ConcreteColumn:
                case MemberConstruction.TimberColumn:
                case MemberConstruction.TimberGablePost:
                case MemberConstruction.ColdFormedColumn:
                case MemberConstruction.ColdFormedGablePost:
                case MemberConstruction.ColdFormedParapetPost:
                case MemberConstruction.ConcreteWallColumnElement:
                case MemberConstruction.GeneralMaterialColumn:
                case MemberConstruction.BearingWallColumn:
                case MemberConstruction.ColdRolledColumn:
                    return StructuralUsage1D.Column;

                case MemberConstruction.SteelBrace:
                case MemberConstruction.SteelTie:
                case MemberConstruction.SteelTrussInternal:
                case MemberConstruction.TimberBrace:
                case MemberConstruction.TimberTrussInternal:
                case MemberConstruction.ColdFormedBrace:
                case MemberConstruction.ColdFormedTrussInternal:
                case MemberConstruction.GeneralMaterialBrace:
                    return StructuralUsage1D.Brace;

                case MemberConstruction.Unknown:
                    return StructuralUsage1D.Undefined;

                default:
                    recognised = false;
                    return StructuralUsage1D.Undefined;
            }
        }

        /***************************************************/
    }
}
