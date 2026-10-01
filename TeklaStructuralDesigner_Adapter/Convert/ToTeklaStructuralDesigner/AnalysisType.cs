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
using TsdAnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // An explicit switch rather than a numeric cast, even though the two enums are declared in the
        // same order: a cast would silently go wrong the day either enum gains, loses, or reorders a
        // value, and this way that mistake is instead an unhandled case caught by the default branch.
        public static TsdAnalysisType ToTeklaStructuralDesigner(this TeklaStructuralDesignerAnalysisType analysisType)
        {
            switch (analysisType)
            {
                case TeklaStructuralDesignerAnalysisType.FirstOrderLinear:
                    return TsdAnalysisType.FirstOrderLinear;
                case TeklaStructuralDesignerAnalysisType.FirstOrderNonLinear:
                    return TsdAnalysisType.FirstOrderNonLinear;
                case TeklaStructuralDesignerAnalysisType.SecondOrderLinear:
                    return TsdAnalysisType.SecondOrderLinear;
                case TeklaStructuralDesignerAnalysisType.SecondOrderNonLinear:
                    return TsdAnalysisType.SecondOrderNonLinear;
                case TeklaStructuralDesignerAnalysisType.GrillageChaseDown:
                    return TsdAnalysisType.GrillageChaseDown;
                case TeklaStructuralDesignerAnalysisType.FEChaseDown:
                    return TsdAnalysisType.FEChaseDown;
                case TeklaStructuralDesignerAnalysisType.FirstOrderVibration:
                    return TsdAnalysisType.FirstOrderVibration;
                case TeklaStructuralDesignerAnalysisType.SecondOrderBuckling:
                    return TsdAnalysisType.SecondOrderBuckling;
                case TeklaStructuralDesignerAnalysisType.SeismicVibration:
                    return TsdAnalysisType.SeismicVibration;
                case TeklaStructuralDesignerAnalysisType.FirstOrderRsaSeismic:
                    return TsdAnalysisType.FirstOrderRsaSeismic;
                case TeklaStructuralDesignerAnalysisType.SecondOrderRsaSeismic:
                    return TsdAnalysisType.SecondOrderRsaSeismic;
                case TeklaStructuralDesignerAnalysisType.SequentialLoading:
                    return TsdAnalysisType.SequentialLoading;
                case TeklaStructuralDesignerAnalysisType.FirstOrderLinearStagedConstruction:
                    return TsdAnalysisType.FirstOrderLinearStagedConstruction;
                case TeklaStructuralDesignerAnalysisType.FirstOrderNonLinearStagedConstruction:
                    return TsdAnalysisType.FirstOrderNonLinearStagedConstruction;
                case TeklaStructuralDesignerAnalysisType.SecondOrderLinearStagedConstruction:
                    return TsdAnalysisType.SecondOrderLinearStagedConstruction;
                case TeklaStructuralDesignerAnalysisType.SecondOrderNonLinearStagedConstruction:
                    return TsdAnalysisType.SecondOrderNonLinearStagedConstruction;
                default:
                    Engine.Base.Compute.RecordWarning("Unrecognised AnalysisType '" + analysisType + "'; defaulting to FirstOrderLinear.");
                    return TsdAnalysisType.FirstOrderLinear;
            }
        }

        /***************************************************/
    }
}
