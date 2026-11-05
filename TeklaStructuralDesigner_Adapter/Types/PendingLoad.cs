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

using BH.oM.Structure.Loads;
using TsdLoad = TSD.API.Remoting.Loading.ILoad;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // A load read from Tekla Structural Designer, waiting to be converted, with the Loadcase it is in.
    internal sealed class PendingLoad
    {
        /***************************************************/
        /**** Public Properties                         ****/
        /***************************************************/

        public TsdLoad Load { get; }

        public Loadcase Loadcase { get; }

        /***************************************************/
        /**** Constructors                              ****/
        /***************************************************/

        public PendingLoad(TsdLoad load, Loadcase loadcase)
        {
            Load = load;
            Loadcase = loadcase;
        }

        /***************************************************/
    }
}
