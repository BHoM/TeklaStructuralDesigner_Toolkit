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

namespace BH.Adapter.TeklaStructuralDesigner
{
    // A loading case (combination or loadcase) reduced to what a bar force read needs: the Guid TSD
    // requires to ask for results, and the human readable identifier BHoM's ResultCase should carry
    // instead of that Guid. See BH.oM.Structure.Results.BarResult.ResultCase: "generally name or
    // number of the loadcase" - a Guid in that slot would break every downstream grouping workflow.
    internal sealed class TsdLoadingCaseIdentity
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        // The loadingId argument every TSD results call needs. Kept internally and never surfaced as
        // ResultCase.
        public Guid Id { get; }

        // What ResultCase is actually set to: the case's name, its user name, or its index, in that
        // order of preference.
        public string Identifier { get; }

        public bool IsCombination { get; }

        /***************************************************/
        /****                Constructors               ****/
        /***************************************************/

        public TsdLoadingCaseIdentity(Guid id, string identifier, bool isCombination)
        {
            Id = id;
            Identifier = identifier;
            IsCombination = isCombination;
        }

        /***************************************************/
    }
}
