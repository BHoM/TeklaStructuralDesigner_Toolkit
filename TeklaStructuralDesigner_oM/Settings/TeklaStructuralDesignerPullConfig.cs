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
using BH.oM.Adapter;

namespace BH.oM.Adapters.TeklaStructuralDesigner
{
    [Description("Configuration for a Pull from Tekla Structural Designer. Supply this as the ActionConfig of a Pull to choose which analysis results are read. If omitted, every default below applies.")]
    public class TeklaStructuralDesignerPullConfig : ActionConfig
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        [Description("Which analysis to read results from. Tekla Structural Designer holds a separate set of results per analysis type, and a model may be solved for some and not others. Results are only returned for loading cases that have actually been solved for the type chosen here.")]
        public virtual TeklaStructuralDesignerAnalysisType AnalysisType { get; set; } = TeklaStructuralDesignerAnalysisType.FirstOrderLinear;

        [Description("Whether to read base results or results including notional load effects.")]
        public virtual TeklaStructuralDesignerLoadingResultType LoadingResultType { get; set; } = TeklaStructuralDesignerLoadingResultType.Base;

        [Description("Which route through the API is used to read bar forces. Leave as Auto unless diagnosing a discrepancy.")]
        public virtual TeklaStructuralDesignerBarForceSource BarForceSource { get; set; } = TeklaStructuralDesignerBarForceSource.Auto;

        [Description("If true, load combinations are read. Tekla Structural Designer distinguishes combinations from the individual loadcases that make them up, and a schedule of design forces is normally built from combinations, so this is true by default.")]
        public virtual bool IncludeCombinations { get; set; } = true;

        [Description("If true, individual loadcases are read in addition to combinations. False by default: on a real model this multiplies the number of results returned without usually being what was wanted.")]
        public virtual bool IncludeLoadcases { get; set; } = false;

        [Description("Swaps the major and minor local axes when converting forces, so that the moment Tekla Structural Designer reports about one local axis is written to the other. Only set this true if a known model demonstrates that the default mapping is transposed; see the toolkit README. Shears are swapped together with moments, because a genuine axis transposition affects both.")]
        public virtual bool SwapMajorMinorAxes { get; set; } = false;

        [Description("Which of Tekla Structural Designer's three per loadcase factors is written into a pulled LoadCombination. Only affects pulling LoadCombination objects; it has no bearing on results, which Tekla Structural Designer has already combined internally.")]
        public virtual TeklaStructuralDesignerCombinationFactor CombinationFactor { get; set; } = TeklaStructuralDesignerCombinationFactor.Strength;

        [Description("If true, loads that Tekla Structural Designer generated rather than a user applied - decomposed slab and wind loads, solver loads, loads arriving from an incoming element - are pulled alongside the applied loads. False by default: a decomposed load is the same loading already described by the slab load it came from, so pulling both double counts it. Set this true when you want what actually acts on the members rather than what was drawn.")]
        public virtual bool IncludeDerivedLoads { get; set; } = false;

        /***************************************************/
    }
}
