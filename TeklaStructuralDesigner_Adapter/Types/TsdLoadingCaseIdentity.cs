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

using System;
using BH.oM.Adapters.TeklaStructuralDesigner;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // A loading case - a loadcase, or one limit state of a combination - reduced to what a bar force
    // read needs: the Guid TSD requires to ask for its results, and the Number and Name the matching
    // BHoM Loadcase or LoadCombination carries. See Engine Query.CaseNumber for the numbering.
    internal sealed class TsdLoadingCaseIdentity
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        // The loadingId argument every TSD results call needs. For a Strength combination this is the
        // combination's own Guid; for a Service combination it is the combination's SlsId - the hidden
        // SLS combination Tekla Structural Designer solves the service factors in. Checked against a
        // live model: results read with the SlsId equal the loadcase results recombined with the
        // service factors exactly. Kept internally and never surfaced as ResultCase.
        public Guid Id { get; }

        // The Number of the matching BHoM Loadcase or LoadCombination, and what ResultCase is set to.
        // Unique across loadcases, Strength and Service combinations.
        public int Number { get; }

        // The Name of the matching BHoM Loadcase or LoadCombination - for a combination, prefixed with
        // its limit state.
        public string Name { get; }

        // Null for a loadcase.
        public TeklaStructuralDesignerLimitState? LimitState { get; }

        /***************************************************/
        /****                Constructors               ****/
        /***************************************************/

        public TsdLoadingCaseIdentity(Guid id, int number, string name, TeklaStructuralDesignerLimitState? limitState)
        {
            Id = id;
            Number = number;
            Name = name;
            LimitState = limitState;
        }

        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // How the case is referred to in messages: its number and name, as in "1048 Strength 48 ...".
        public override string ToString()
        {
            return Number + " " + Name;
        }

        /***************************************************/
    }
}
