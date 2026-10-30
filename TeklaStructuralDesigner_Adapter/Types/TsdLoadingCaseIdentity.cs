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
    // A loadcase, or one limit state of a combination, reduced to what a results read needs.
    internal sealed class TsdLoadingCaseIdentity
    {
        /***************************************************/
        /**** Public Properties                         ****/
        /***************************************************/

        // The Guid results are read with: a Strength combination's own Id, a Service combination's
        // SlsId (verified to give the loadcase results recombined with the service factors).
        public Guid Id { get; }

        // The Number of the matching Loadcase or LoadCombination, used as ResultCase.
        public int Number { get; }

        public string Name { get; }

        // Null for a loadcase.
        public TeklaStructuralDesignerLimitState? LimitState { get; }

        /***************************************************/
        /**** Constructors                              ****/
        /***************************************************/

        public TsdLoadingCaseIdentity(Guid id, int number, string name, TeklaStructuralDesignerLimitState? limitState)
        {
            Id = id;
            Number = number;
            Name = name;
            LimitState = limitState;
        }

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        public override string ToString()
        {
            return Number + " " + Name;
        }

        /***************************************************/
    }
}
