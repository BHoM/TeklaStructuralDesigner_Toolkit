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
    [Description("Which route through the Tekla Structural Designer API is used to read bar forces. The two routes return the same force type but differ enormously in cost, so the choice is exposed for troubleshooting rather than for routine use.")]
    public enum TeklaStructuralDesignerBarForceSource
    {
        [Description("Use the batched solver route, and validate it against the per span end route for a single span before trusting it. Falls back to the per span end route if the two disagree. The default, and the right choice unless you are diagnosing a discrepancy.")]
        Auto,

        [Description("Batched solver route: one call per loading case returns the end forces of every solver element in the model. Orders of magnitude fewer calls than SpanEnds, and the only practical option on a large model, but it reports at solver element boundaries rather than at span ends.")]
        Solver,

        [Description("Per span end route: one call per span, per end, per loading case. Reports exactly at span ends and is the route proven in earlier tooling, but the number of calls is the product of those three counts and becomes unusable on a large model.")]
        SpanEnds,
    }
}
