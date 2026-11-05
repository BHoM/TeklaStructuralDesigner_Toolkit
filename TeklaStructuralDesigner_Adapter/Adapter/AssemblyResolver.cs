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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // Loads the BCL shims deployed to the private TeklaStructuralDesigner subfolder (see "Deployment" in
    // the README). It only answers for files in that folder, so cannot intercept another toolkit's
    // binding. BHoM's own resolver cannot serve them, as it checks the calling assembly, which is
    // mscorlib inside an AssemblyResolve handler.
    internal static class AssemblyResolver
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        private static readonly object m_Lock = new object();
        private static readonly Dictionary<string, Assembly> m_Resolved = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private static bool m_Registered = false;

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        internal static string PrivateFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "BHoM", "Assemblies", "TeklaStructuralDesigner");
            }
        }

        /***************************************************/

        // Must run before any Tekla Structural Designer type is touched: it is the adapter constructor's
        // first statement, and the methods using those types are NoInlining.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void EnsureRegistered()
        {
            lock (m_Lock)
            {
                if (m_Registered)
                    return;

                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                m_Registered = true;

                if (!Directory.Exists(PrivateFolder))
                {
                    Engine.Base.Compute.RecordWarning(
                        "The Tekla Structural Designer dependency folder was not found at " + PrivateFolder +
                        ". Rebuild TeklaStructuralDesigner_Toolkit, or reinstall BHoM, before activating the adapter.");
                }
            }
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(args?.Name))
                return null;

            string simpleName;
            try
            {
                simpleName = new AssemblyName(args.Name).Name;
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrEmpty(simpleName) || simpleName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;

            lock (m_Lock)
            {
                Assembly cached;
                if (m_Resolved.TryGetValue(simpleName, out cached))
                    return cached;

                string candidate = Path.Combine(PrivateFolder, simpleName + ".dll");
                if (!File.Exists(candidate))
                    return null;                    // Not one of ours.

                try
                {
                    Assembly loaded = Assembly.LoadFrom(candidate);
                    m_Resolved[simpleName] = loaded;
                    return loaded;
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordError(
                        "Failed to load the Tekla Structural Designer dependency '" + simpleName + "' from " +
                        candidate + ". " + e.Message);
                    return null;
                }
            }
        }

        /***************************************************/
    }
}
