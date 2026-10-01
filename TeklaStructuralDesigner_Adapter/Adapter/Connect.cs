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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Fields                 ****/
        /***************************************************/

        private IModel m_Model;

        /***************************************************/
        /****            Private Methods                ****/
        /****            (Connection)                   ****/
        /***************************************************/

        // Kept out of the constructor's own body and marked NoInlining so that no Tekla Structural
        // Designer metadata token is touched while the constructor itself is being JIT compiled - the
        // assembly resolver must already be registered by the time any of that metadata is resolved.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Connect(string modelPath)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            List<IApplication> running;
            try
            {
                running = Async.RunSync(
                    ct => ApplicationFactory.GetRunningApplicationsAsync(ct),
                    timeout, "discovering running Tekla Structural Designer instances").ToList();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.DiscoveryFailed(e.Message));
                return;
            }

            if (running.Count == 0)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoRunningInstance());
                return;
            }

            // Describe every instance BEFORE choosing one, so any ambiguity can be reported usefully
            // rather than discovered only after the wrong one has already been picked.
            List<InstanceInfo> candidates = new List<InstanceInfo>();
            foreach (IApplication application in running)
            {
                try
                {
                    IDocument document = Async.RunSync(ct => application.GetDocumentAsync(ct), timeout, "reading the active document");
                    string version = Async.RunSync(ct => application.GetVersionStringAsync(ct), timeout, "reading the application version");
                    candidates.Add(new InstanceInfo(application, document, document?.Path ?? "", version));
                }
                catch (Exception e)
                {
                    Engine.Base.Compute.RecordWarning(
                        "A running Tekla Structural Designer instance could not be interrogated and has been ignored. " + e.Message);
                }
            }

            if (candidates.Count == 0)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.DiscoveryFailed("None of the running instances responded."));
                return;
            }

            InstanceInfo chosen = ChooseInstance(candidates, modelPath);
            if (chosen == null)
                return;         // The relevant error has already been recorded by ChooseInstance.

            if (chosen.Document == null)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoDocument(Describe(chosen)));
                return;
            }

            if (chosen.Document.ModelId == Guid.Empty)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoModelOpen(Describe(chosen)));
                return;
            }

            IModel model;
            try
            {
                model = Async.RunSync(ct => chosen.Document.GetModelAsync(ct), timeout, "attaching to the model");
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.ModelAttachFailed(Describe(chosen), e.Message));
                return;
            }

            if (model == null)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.ModelAttachFailed(Describe(chosen), "The call succeeded but returned no model."));
                return;
            }

            m_Model = model;

            // Always report which instance was actually bound. Cheap, and it is what makes "the adapter
            // read from the wrong model" a self-diagnosing class of bug instead of a silent one.
            Engine.Base.Compute.RecordNote(
                "Connected to Tekla Structural Designer " + chosen.Version + ", model '" + chosen.Path +
                "' (ModelId " + chosen.Document.ModelId + ").");
        }

        /***************************************************/

        private InstanceInfo ChooseInstance(List<InstanceInfo> candidates, string modelPath)
        {
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                // Full path match first, then file name match, so a bare file name still disambiguates
                // between instances open on files of the same name in different folders where possible.
                List<InstanceInfo> matches = candidates.Where(c => PathsEqual(c.Path, modelPath)).ToList();
                if (matches.Count == 0)
                    matches = candidates.Where(c => FileNamesEqual(c.Path, modelPath)).ToList();

                if (matches.Count == 0)
                {
                    Engine.Base.Compute.RecordError(ErrorMessages.NoMatchForPath(modelPath, candidates.Select(Describe)));
                    return null;
                }

                if (matches.Count > 1)
                {
                    Engine.Base.Compute.RecordError(ErrorMessages.AmbiguousMatchForPath(modelPath, matches.Select(Describe)));
                    return null;
                }

                return matches[0];
            }

            if (candidates.Count == 1)
                return candidates[0];

            if (TeklaStructuralDesignerConfig.AllowArbitraryInstanceSelection)
            {
                InstanceInfo first = candidates[0];
                Engine.Base.Compute.RecordWarning(
                    "Multiple running instances of Tekla Structural Designer were found and AllowArbitraryInstanceSelection is set, so the first was chosen: " +
                    Describe(first) + ". Set 'modelPath' to choose deliberately instead.");
                return first;
            }

            // Refuse to guess. Binding to an arbitrary running instance means results can silently come
            // from the wrong model with nothing to indicate that anything went wrong.
            Engine.Base.Compute.RecordError(ErrorMessages.MultipleInstances(candidates.Select(Describe)));
            return null;
        }

        /***************************************************/

        private static string Describe(InstanceInfo instance)
        {
            string path = string.IsNullOrWhiteSpace(instance.Path) ? "<no document>" : instance.Path;
            return path + " (Tekla Structural Designer " + instance.Version + ")";
        }

        /***************************************************/

        private static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        /***************************************************/

        private static bool FileNamesEqual(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            try
            {
                return string.Equals(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        /***************************************************/
        /****            Private Nested Types           ****/
        /***************************************************/

        // A plain data holder rather than a `record`: records are C# 9, and this project is pinned to
        // LangVersion 7.3.
        private sealed class InstanceInfo
        {
            public IApplication Application { get; }
            public IDocument Document { get; }
            public string Path { get; }
            public string Version { get; }

            public InstanceInfo(IApplication application, IDocument document, string path, string version)
            {
                Application = application;
                Document = document;
                Path = path;
                Version = version;
            }
        }

        /***************************************************/
    }
}
