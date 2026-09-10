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
    // A handful of BCL shims from the Tekla Structural Designer dependency chain are deployed to a
    // private subfolder of the BHoM assemblies folder rather than alongside the BHoM assemblies,
    // because they collide by version with assemblies other toolkits bind. See the deployment
    // comment in TeklaStructuralDesigner_Adapter.csproj for the full reasoning.
    //
    // Note the narrow scope: this resolver serves ONLY those shims. TSD.API.Remoting itself and the
    // gRPC stack deploy top level and are found by ordinary probing, which they have to be - BHoM
    // enumerates adapter types at startup, before any constructor has run and therefore before this
    // resolver is registered, and an assembly whose GetTypes() throws there is discarded entirely.
    // Moving the API back into the private folder makes the adapter vanish from the UI.
    //
    // Because the shims are not next to this assembly, normal directory probing will not find them,
    // so this resolver is required rather than defensive. It is safe to the extent that it only ever
    // answers for files physically present in our own folder, and returns null for everything else,
    // so it cannot intercept another toolkit's binding.
    //
    // BHoM's own resolver cannot serve this: BH.Engine.Base.Global.ResolveBHoMAssembly gates on
    // Assembly.GetCallingAssembly().Location, which inside a CLR invoked AssemblyResolve handler is
    // mscorlib rather than the requesting assembly.
    //
    // Registration is an explicit call rather than a module initialiser because [ModuleInitializer]
    // is C# 9 and this project is pinned to LangVersion 7.3.
    internal static class AssemblyResolver
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        private static readonly object m_Lock = new object();
        private static readonly Dictionary<string, Assembly> m_Resolved = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private static bool m_Registered = false;

        /***************************************************/
        /****            Internal Methods               ****/
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

        // Must run before the JIT resolves any Tekla Structural Designer metadata token. Guaranteed by
        // calling it as the first statement of the adapter constructor and keeping every TSD type out
        // of that constructor's body; the methods that do touch TSD types are marked NoInlining so
        // their metadata is not resolved while the constructor is being compiled.
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
        /****            Private Methods                ****/
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
                    return null;                    // Not one of ours. Let other handlers try.

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
