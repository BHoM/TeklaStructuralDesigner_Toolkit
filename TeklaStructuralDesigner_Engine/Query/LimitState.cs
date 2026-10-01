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

using System.ComponentModel;
using BH.Engine.Base;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Base.Attributes;
using BH.oM.Structure.Loads;

namespace BH.Engine.Adapters.TeklaStructuralDesigner
{
    public static partial class Query
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        [Description("The limit state (Strength or Service) of a LoadCombination pulled from Tekla Structural Designer, read from its TeklaStructuralDesignerCombinationProperties fragment, or from its Number if the fragment has been lost.")]
        [Input("combination", "A LoadCombination pulled from Tekla Structural Designer.")]
        [Output("limitState", "The limit state the combination represents.")]
        public static TeklaStructuralDesignerLimitState LimitState(this LoadCombination combination)
        {
            if (combination == null)
            {
                Base.Compute.RecordError("Cannot query the limit state of a null LoadCombination.");
                return TeklaStructuralDesignerLimitState.Strength;
            }

            TeklaStructuralDesignerCombinationProperties properties = combination.FindFragment<TeklaStructuralDesignerCombinationProperties>();
            if (properties != null)
                return properties.LimitState;

            TeklaStructuralDesignerLimitState fromNumber;
            if (LimitState(combination.Number, out fromNumber))
                return fromNumber;

            Base.Compute.RecordError("LoadCombination '" + combination.Name + "' carries no Tekla Structural Designer combination data and its Number (" + combination.Number +
                ") is not a Tekla Structural Designer combination number, so its limit state is unknown.");
            return TeklaStructuralDesignerLimitState.Strength;
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // The band decoding is CombinationNumber's; this only adds which limit state the band means.
        private static bool LimitState(int caseNumber, out TeklaStructuralDesignerLimitState limitState)
        {
            limitState = caseNumber / NumberBand == ServiceBand ? TeklaStructuralDesignerLimitState.Service : TeklaStructuralDesignerLimitState.Strength;
            return CombinationNumber(caseNumber) >= 0;
        }

        /***************************************************/
    }
}
