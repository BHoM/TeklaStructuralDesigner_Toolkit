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
    [Description("Which set of results to extract for a loading case, mirroring TSD.API.Remoting.Loading.LoadingResultType. Mirrored rather than referenced directly because a BHoM _oM assembly must not depend on an external software API.")]
    public enum TeklaStructuralDesignerLoadingResultType
    {
        [Description("The base results for the loading, without notional load effects. The default.")]
        Base,

        [Description("Results including positive notional loads in building direction 1.")]
        NotionalLoadsDirection1Positive,

        [Description("Results including positive notional loads in building direction 2.")]
        NotionalLoadsDirection2Positive,

        [Description("Results including negative notional loads in building direction 1.")]
        NotionalLoadsDirection1Negative,

        [Description("Results including negative notional loads in building direction 2.")]
        NotionalLoadsDirection2Negative,
    }
}
