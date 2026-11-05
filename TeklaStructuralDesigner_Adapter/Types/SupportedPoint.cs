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

using BH.oM.Geometry;
using BH.oM.Structure.Constraints;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // A supported construction point: its position, fixity and the support's own axes, if any.
    internal sealed class SupportedPoint
    {
        /***************************************************/
        /**** Public Properties                         ****/
        /***************************************************/

        public Point Position { get; }

        public Constraint6DOF Support { get; }

        public Basis Orientation { get; }

        /***************************************************/
        /**** Constructors                              ****/
        /***************************************************/

        public SupportedPoint(Point position, Constraint6DOF support, Basis orientation)
        {
            Position = position;
            Support = support;
            Orientation = orientation;
        }

        /***************************************************/
    }
}
