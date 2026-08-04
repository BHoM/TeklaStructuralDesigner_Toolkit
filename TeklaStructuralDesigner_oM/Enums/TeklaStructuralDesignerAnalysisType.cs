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

namespace BH.oM.Adapters.TeklaStructuralDesigner
{
    [Description("Analysis type to extract results for, mirroring TSD.API.Remoting.Solver.AnalysisType. Mirrored rather than referenced directly because a BHoM _oM assembly must not depend on an external software API.")]
    public enum TeklaStructuralDesignerAnalysisType
    {
        [Description("First order linear analysis. The default, and the type the Tekla Structural Designer interface reports first.")]
        FirstOrderLinear,

        [Description("First order non-linear analysis.")]
        FirstOrderNonLinear,

        [Description("Second order linear analysis.")]
        SecondOrderLinear,

        [Description("Second order non-linear analysis.")]
        SecondOrderNonLinear,

        [Description("Grillage chase down analysis.")]
        GrillageChaseDown,

        [Description("Finite element chase down analysis.")]
        FEChaseDown,

        [Description("First order vibration analysis.")]
        FirstOrderVibration,

        [Description("Second order buckling analysis.")]
        SecondOrderBuckling,

        [Description("Seismic vibration analysis.")]
        SeismicVibration,

        [Description("First order response spectrum seismic analysis.")]
        FirstOrderRsaSeismic,

        [Description("Second order response spectrum seismic analysis.")]
        SecondOrderRsaSeismic,

        [Description("Sequential loading analysis.")]
        SequentialLoading,

        [Description("First order linear staged construction analysis.")]
        FirstOrderLinearStagedConstruction,

        [Description("First order non-linear staged construction analysis.")]
        FirstOrderNonLinearStagedConstruction,

        [Description("Second order linear staged construction analysis.")]
        SecondOrderLinearStagedConstruction,

        [Description("Second order non-linear staged construction analysis.")]
        SecondOrderNonLinearStagedConstruction,
    }
}
