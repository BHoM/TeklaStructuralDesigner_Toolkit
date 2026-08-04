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
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Base.Attributes;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter : BHoMAdapter
    {
        /***************************************************/
        /****            Public Properties              ****/
        /***************************************************/

        [Description("Settings controlling how this adapter talks to Tekla Structural Designer.")]
        public virtual TeklaStructuralDesignerConfig TeklaStructuralDesignerConfig { get; set; } = new TeklaStructuralDesignerConfig();

        /***************************************************/
        /****                Constructors               ****/
        /***************************************************/

        [Description("Creates an adapter for reading analysis results from Tekla Structural Designer. Tekla Structural Designer must already be running with the model open - its Remoting API attaches to a live instance and has no means of opening a file itself.")]
        [Input("modelPath", "Selects which running instance of Tekla Structural Designer to attach to, by the path of the model that instance has open. A file name on its own is enough. Leave empty when only one instance is running. This does not open a file: the API cannot do that.")]
        [Input("teklaStructuralDesignerConfig", "Settings controlling timeouts and instance selection. Defaults are used if left empty.")]
        [Input("active", "Set to true to attach to Tekla Structural Designer. While false the adapter is created but no connection is attempted, so it can sit in a script without requiring Tekla Structural Designer to be running.")]
        [Output("adapter", "Adapter for reading analysis results from Tekla Structural Designer.")]
        public TeklaStructuralDesignerAdapter(string modelPath = "", TeklaStructuralDesignerConfig teklaStructuralDesignerConfig = null, bool active = false)
        {
            // Must be the first statement: registers the resolver for the privately deployed Tekla
            // Structural Designer dependencies before any of their metadata could be touched.
            AssemblyResolver.EnsureRegistered();

            AdapterIdFragmentType = typeof(TeklaStructuralDesignerId);
            SetupComparers();
            SetupDependencies();

            BH.Adapter.Modules.Structure.ModuleLoader.LoadModules(this);

            if (!active)
                return;

            TeklaStructuralDesignerConfig = teklaStructuralDesignerConfig ?? new TeklaStructuralDesignerConfig();
            Connect(modelPath);
        }

        /***************************************************/
    }
}
