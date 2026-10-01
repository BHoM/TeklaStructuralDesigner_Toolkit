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

        [Description("If true, results are read for the Strength (ultimate limit state) version of every combination when a result request names no cases. True by default: a schedule of design forces is normally built from strength combinations. Cases named on the request are always read, whatever this is set to.")]
        public virtual bool IncludeStrengthCombinations { get; set; } = true;

        [Description("If true, results are read for the Service (serviceability limit state) version of every combination when a result request names no cases. False by default, because it doubles the number of combination results returned. Cases named on the request are always read, whatever this is set to.")]
        public virtual bool IncludeServiceCombinations { get; set; } = false;

        [Description("If true, results are read for every individual loadcase when a result request names no cases. False by default: on a real model this multiplies the number of results returned without usually being what was wanted. Cases named on the request are always read, whatever this is set to.")]
        public virtual bool IncludeLoadcases { get; set; } = false;

        [Description("Swaps the major and minor local axes when converting forces, so that the moment Tekla Structural Designer reports about one local axis is written to the other. Only set this true if a known model demonstrates that the default mapping is transposed; see the toolkit README. Shears are swapped together with moments, because a genuine axis transposition affects both.")]
        public virtual bool SwapMajorMinorAxes { get; set; } = false;

        [Description("If true, loads that Tekla Structural Designer generated rather than a user applied - decomposed slab and wind loads, solver loads, loads arriving from an incoming element - are pulled alongside the applied loads. False by default: a decomposed load is the same loading already described by the slab load it came from, so pulling both double counts it. Set this true when you want what actually acts on the members rather than what was drawn.")]
        public virtual bool IncludeDerivedLoads { get; set; } = false;

        [Description("If true, composite and precast slabs are pulled as the BHoM surface property nearest to them: a SlabOnDeck built from the deck profile, or a HollowCore or ConstantThickness plank wrapped in a ToppedSlab where the topping is structural. False by default, because the Robot and ETABS toolkits cannot push those types - Robot fails and ETABS creates nothing - so every slab is instead pulled as a ConstantThickness of its overall depth, with the deck or plank kept in the property name. Set this true only for a package or workflow that understands the detailed types.")]
        public virtual bool DetailedSurfaceProperties { get; set; } = false;

        /***************************************************/
    }
}
