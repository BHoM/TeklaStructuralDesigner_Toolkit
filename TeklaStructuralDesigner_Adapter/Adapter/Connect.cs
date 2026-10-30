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
using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        private IModel m_Model;

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // NoInlining, so no Tekla Structural Designer metadata is touched before the resolver is registered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Connect(string modelPath)
        {
            int timeout = TeklaStructuralDesignerConfig.TimeoutSeconds;

            string error;
            if (!Async.TryRunSync(ct => ApplicationFactory.GetRunningApplicationsAsync(ct), timeout, "discovering running Tekla Structural Designer instances", out var running, out error))
            {
                Engine.Base.Compute.RecordError(ErrorMessages.DiscoveryFailed(error));
                return;
            }

            if (!running.Any())
            {
                Engine.Base.Compute.RecordError(ErrorMessages.NoRunningInstance());
                return;
            }

            // Every instance is described before one is chosen, so ambiguity can be reported.
            List<InstanceInfo> candidates = new List<InstanceInfo>();
            foreach (IApplication application in running)
            {
                IDocument document;
                string version;
                const string consequence = "that instance has been ignored";
                if (Async.TryRunSync(ct => application.GetDocumentAsync(ct), timeout, "reading the active document", consequence, out document) &&
                    Async.TryRunSync(ct => application.GetVersionStringAsync(ct), timeout, "reading the application version", consequence, out version))
                    candidates.Add(new InstanceInfo(application, document, document?.Path ?? "", version));
            }

            if (candidates.Count == 0)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.DiscoveryFailed("None of the running instances responded."));
                return;
            }

            InstanceInfo chosen = ChooseInstance(candidates, modelPath);
            if (chosen == null)
                return;         // ChooseInstance has already recorded why.

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
            if (!Async.TryRunSync(ct => chosen.Document.GetModelAsync(ct), timeout, "attaching to the model", out model, out error))
            {
                Engine.Base.Compute.RecordError(ErrorMessages.ModelAttachFailed(Describe(chosen), error));
                return;
            }

            if (model == null)
            {
                Engine.Base.Compute.RecordError(ErrorMessages.ModelAttachFailed(Describe(chosen), "The call succeeded but returned no model."));
                return;
            }

            m_Model = model;

            // Always say which instance was bound, so reading the wrong model is never silent.
            Engine.Base.Compute.RecordNote(
                "Connected to Tekla Structural Designer " + chosen.Version + ", model '" + chosen.Path +
                "' (ModelId " + chosen.Document.ModelId + ").");
        }

        /***************************************************/

        private InstanceInfo ChooseInstance(List<InstanceInfo> candidates, string modelPath)
        {
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                // Full path first, then file name.
                List<InstanceInfo> matches = candidates.Where(c => PathsEqual(c.Path, modelPath, Path.GetFullPath)).ToList();
                if (matches.Count == 0)
                    matches = candidates.Where(c => PathsEqual(c.Path, modelPath, Path.GetFileName)).ToList();

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

            // Refuse to guess, as results could then silently come from the wrong model.
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

        // Paths compared after normalising each, such as to its full path or its file name.
        private static bool PathsEqual(string a, string b, Func<string, string> normalise)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            try
            {
                return string.Equals(normalise(a), normalise(b), StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        /***************************************************/
    }
}
