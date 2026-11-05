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
using System.Collections.Generic;
using System.Linq;
using BH.oM.Geometry;
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Materials;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Structure;
using TsdPoint3D = TSD.API.Remoting.Geometry.Point3D;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Every level in the model, or none, with a warning naming the consequence.
        private List<IHorizontalConstructionPlane> ReadTsdLevels(int timeoutSeconds, string consequence)
        {
            IEnumerable<IHorizontalConstructionPlane> levels;
            if (!Async.TryRunSync(ct => m_Model.GetLevelsAsync(null, ct), timeoutSeconds, "reading levels", consequence, out levels))
                return new List<IHorizontalConstructionPlane>();

            return levels.ToList();
        }

        /***************************************************/
    }
}
