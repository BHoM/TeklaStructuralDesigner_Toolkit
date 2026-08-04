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
        public static TSD.API.Remoting.Solver.AnalysisType ToTeklaStructuralDesigner(this TeklaStructuralDesignerAnalysisType analysisType)
        {
            switch (analysisType)
            {
                case TeklaStructuralDesignerAnalysisType.FirstOrderLinear:
                    return TSD.API.Remoting.Solver.AnalysisType.FirstOrderLinear;
                case TeklaStructuralDesignerAnalysisType.FirstOrderNonLinear:
                    return TSD.API.Remoting.Solver.AnalysisType.FirstOrderNonLinear;
                case TeklaStructuralDesignerAnalysisType.SecondOrderLinear:
                    return TSD.API.Remoting.Solver.AnalysisType.SecondOrderLinear;
                case TeklaStructuralDesignerAnalysisType.SecondOrderNonLinear:
                    return TSD.API.Remoting.Solver.AnalysisType.SecondOrderNonLinear;
                case TeklaStructuralDesignerAnalysisType.GrillageChaseDown:
                    return TSD.API.Remoting.Solver.AnalysisType.GrillageChaseDown;
                case TeklaStructuralDesignerAnalysisType.FEChaseDown:
                    return TSD.API.Remoting.Solver.AnalysisType.FEChaseDown;
                case TeklaStructuralDesignerAnalysisType.FirstOrderVibration:
                    return TSD.API.Remoting.Solver.AnalysisType.FirstOrderVibration;
                case TeklaStructuralDesignerAnalysisType.SecondOrderBuckling:
                    return TSD.API.Remoting.Solver.AnalysisType.SecondOrderBuckling;
                case TeklaStructuralDesignerAnalysisType.SeismicVibration:
                    return TSD.API.Remoting.Solver.AnalysisType.SeismicVibration;
                case TeklaStructuralDesignerAnalysisType.FirstOrderRsaSeismic:
                    return TSD.API.Remoting.Solver.AnalysisType.FirstOrderRsaSeismic;
                case TeklaStructuralDesignerAnalysisType.SecondOrderRsaSeismic:
                    return TSD.API.Remoting.Solver.AnalysisType.SecondOrderRsaSeismic;
                case TeklaStructuralDesignerAnalysisType.SequentialLoading:
                    return TSD.API.Remoting.Solver.AnalysisType.SequentialLoading;
                case TeklaStructuralDesignerAnalysisType.FirstOrderLinearStagedConstruction:
                    return TSD.API.Remoting.Solver.AnalysisType.FirstOrderLinearStagedConstruction;
                case TeklaStructuralDesignerAnalysisType.FirstOrderNonLinearStagedConstruction:
                    return TSD.API.Remoting.Solver.AnalysisType.FirstOrderNonLinearStagedConstruction;
                case TeklaStructuralDesignerAnalysisType.SecondOrderLinearStagedConstruction:
                    return TSD.API.Remoting.Solver.AnalysisType.SecondOrderLinearStagedConstruction;
                case TeklaStructuralDesignerAnalysisType.SecondOrderNonLinearStagedConstruction:
                    return TSD.API.Remoting.Solver.AnalysisType.SecondOrderNonLinearStagedConstruction;
                default:
                    Engine.Base.Compute.RecordWarning("Unrecognised AnalysisType '" + analysisType + "'; defaulting to FirstOrderLinear.");
                    return TSD.API.Remoting.Solver.AnalysisType.FirstOrderLinear;
            }
        }

        /***************************************************/

        public static TSD.API.Remoting.Loading.LoadingResultType ToTeklaStructuralDesigner(this TeklaStructuralDesignerLoadingResultType loadingResultType)
        {
            switch (loadingResultType)
            {
                case TeklaStructuralDesignerLoadingResultType.Base:
                    return TSD.API.Remoting.Loading.LoadingResultType.Base;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection1Positive:
                    return TSD.API.Remoting.Loading.LoadingResultType.NotionalLoadsDirection1Positive;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection2Positive:
                    return TSD.API.Remoting.Loading.LoadingResultType.NotionalLoadsDirection2Positive;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection1Negative:
                    return TSD.API.Remoting.Loading.LoadingResultType.NotionalLoadsDirection1Negative;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection2Negative:
                    return TSD.API.Remoting.Loading.LoadingResultType.NotionalLoadsDirection2Negative;
                default:
                    Engine.Base.Compute.RecordWarning("Unrecognised LoadingResultType '" + loadingResultType + "'; defaulting to Base.");
                    return TSD.API.Remoting.Loading.LoadingResultType.Base;
            }
        }

        /***************************************************/
    }
}
