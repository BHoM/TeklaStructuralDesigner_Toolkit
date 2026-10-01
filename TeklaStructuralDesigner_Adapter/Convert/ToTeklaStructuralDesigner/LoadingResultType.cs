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
using TsdLoadingResultType = TSD.API.Remoting.Loading.LoadingResultType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // An explicit switch rather than a numeric cast, for the reason given on AnalysisType.
        public static TsdLoadingResultType ToTeklaStructuralDesigner(this TeklaStructuralDesignerLoadingResultType loadingResultType)
        {
            switch (loadingResultType)
            {
                case TeklaStructuralDesignerLoadingResultType.Base:
                    return TsdLoadingResultType.Base;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection1Positive:
                    return TsdLoadingResultType.NotionalLoadsDirection1Positive;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection2Positive:
                    return TsdLoadingResultType.NotionalLoadsDirection2Positive;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection1Negative:
                    return TsdLoadingResultType.NotionalLoadsDirection1Negative;
                case TeklaStructuralDesignerLoadingResultType.NotionalLoadsDirection2Negative:
                    return TsdLoadingResultType.NotionalLoadsDirection2Negative;
                default:
                    Engine.Base.Compute.RecordWarning("Unrecognised LoadingResultType '" + loadingResultType + "'; defaulting to Base.");
                    return TsdLoadingResultType.Base;
            }
        }

        /***************************************************/
    }
}
