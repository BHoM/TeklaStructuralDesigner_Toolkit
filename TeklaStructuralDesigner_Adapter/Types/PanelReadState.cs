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

using System.Collections.Generic;
using BH.oM.Base;
using BH.oM.Structure.SurfaceProperties;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // What one Panel pull shares across slabs, walls and loading panels: the material cache, one surface
    // property object per distinct property, and its warnings.
    internal sealed class PanelReadState
    {
        /***************************************************/
        /**** Public Properties                         ****/
        /***************************************************/

        public BarPropertyCache Materials { get; } = new BarPropertyCache();

        public WarningTally Warnings { get; } = new WarningTally();

        // Whether composite and precast slabs get their detailed BHoM types; see TeklaStructuralDesignerPullConfig.
        public bool Detailed { get; set; }

        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        private readonly Dictionary<string, ISurfaceProperty> m_Properties = new Dictionary<string, ISurfaceProperty>();
        private LoadingPanelProperty m_LoadingProperty;

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        public ISurfaceProperty Shared(ISurfaceProperty property)
        {
            if (property == null)
                return null;

            string key = property.GetType().Name + "|" + ((IBHoMObject)property).Name;
            ISurfaceProperty existing;
            if (m_Properties.TryGetValue(key, out existing))
                return existing;

            m_Properties[key] = property;
            return property;
        }

        /***************************************************/

        // The one way property every roof and wind wall shares: load goes to the two sides the span runs
        // between, referenced from edge 1, which Convert.FromTsdPanel makes the edge along the span.
        public LoadingPanelProperty LoadingPanelProperty()
        {
            if (m_LoadingProperty == null)
            {
                m_LoadingProperty = new LoadingPanelProperty
                {
                    Name = "One way loading panel",
                    LoadApplication = LoadPanelSupportConditions.TwoSides,
                    ReferenceEdge = 1,
                };
            }

            return m_LoadingProperty;
        }

        /***************************************************/

        public void Approximate(string reason)
        {
            Warnings.Add("slab item(s) have been approximated: " + reason);
        }

        /***************************************************/

        public void Report()
        {
            Warnings.Report();
            Materials.Report();
        }

        /***************************************************/
    }
}
