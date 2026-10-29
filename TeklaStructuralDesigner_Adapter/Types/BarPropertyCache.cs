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
using System.Globalization;
using System.Linq;
using BH.oM.Spatial.ShapeProfiles;
using BH.oM.Structure.Constraints;
using BH.oM.Structure.MaterialFragments;
using BH.oM.Structure.SectionProperties;
using TSD.API.Remoting.Materials;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Structure;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // Converts each distinct material, section and release in a pull once, so Bars share them and
    // section integration is not repeated per span, and summarises how each was obtained.
    internal sealed class BarPropertyCache
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        private readonly Dictionary<Guid, IMaterialFragment> m_Materials = new Dictionary<Guid, IMaterialFragment>();
        private readonly Dictionary<string, ISectionProperty> m_Sections = new Dictionary<string, ISectionProperty>();
        private readonly Dictionary<string, BarRelease> m_Releases = new Dictionary<string, BarRelease>();

        private readonly HashSet<string> m_MaterialsMatched = new HashSet<string>();
        private readonly HashSet<string> m_MaterialsFromModel = new HashSet<string>();
        private readonly HashSet<string> m_MaterialRejections = new HashSet<string>();
        private int m_ApproximatedReleaseEnds = 0;

        private readonly HashSet<string> m_LibraryMatched = new HashSet<string>();
        private readonly HashSet<string> m_CatalogueNotInLibrary = new HashSet<string>();
        private readonly HashSet<string> m_LibraryRejections = new HashSet<string>();
        private readonly HashSet<string> m_FromProfile = new HashSet<string>();
        private readonly HashSet<string> m_HolesIgnored = new HashSet<string>();
        private readonly Dictionary<string, HashSet<string>> m_Explicit = new Dictionary<string, HashSet<string>>();
        private readonly WarningTally m_Unrecognised = new WarningTally();

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Counts a value Convert did not recognise, so it is reported once with its count.
        public void Unrecognised(string message)
        {
            m_Unrecognised.Add(message);
        }

        /***************************************************/


        // The matching BHoM library material if there is one, otherwise the model's own.
        public IMaterialFragment Material(IMaterial material)
        {
            if (material == null)
                return null;

            IMaterialFragment result;
            if (m_Materials.TryGetValue(material.Id, out result))
                return result;

            IMaterialFragment fromModel = material.FromTsdMaterial();

            string rejectedReason;
            result = fromModel.MatchLibraryMaterial(out rejectedReason);

            if (rejectedReason != null)
                m_MaterialRejections.Add(rejectedReason);

            if (result != null)
                m_MaterialsMatched.Add(result.Name);
            else
            {
                result = fromModel;
                m_MaterialsFromModel.Add(result.Name);
            }

            m_Materials[material.Id] = result;
            return result;
        }

        /***************************************************/

        // Spans with the same releases share one BarRelease, as sections share one section.
        public BarRelease Release(IMemberSpan span)
        {
            if (span == null)
                return null;

            int approximated;
            BarRelease release = span.StartReleases.ValueOrDefault().FromTsdBarRelease(span.EndReleases.ValueOrDefault(), out approximated);
            m_ApproximatedReleaseEnds += approximated;

            BarRelease shared;
            if (m_Releases.TryGetValue(release.Name, out shared))
                return shared;

            m_Releases[release.Name] = release;
            return release;
        }

        /***************************************************/

        // In order of preference: the BHoM steel section library, a section built from the matching
        // profile, and the reported section constants. The material picks the section type.
        public ISectionProperty Section(ISection section, IMaterialFragment material)
        {
            if (section == null)
                return null;

            string key = Key(section, material);

            ISectionProperty result;
            if (m_Sections.TryGetValue(key, out result))
                return result;

            string name = section.SectionName();

            Steel steel = material as Steel;
            if (steel != null && section is INonParametricSection)
            {
                string rejectedReason;
                result = section.MatchLibrarySection(steel, out rejectedReason);

                if (rejectedReason != null)
                    m_LibraryRejections.Add(rejectedReason);

                if (result != null)
                {
                    m_LibraryMatched.Add(name);
                    m_Sections[key] = result;
                    return result;
                }

                m_CatalogueNotInLibrary.Add(name);
            }

            string unsupportedReason;
            IProfile profile = section.FromTsdProfile(out unsupportedReason);
            if (profile != null)
                result = Engine.Structure.Create.SectionPropertyFromProfile(profile, material, name);

            if (result != null)
            {
                m_FromProfile.Add(name);

                IParametricSection parametric = section as IParametricSection;
                if (parametric != null && parametric.Holes != null && parametric.Holes.Count > 0)
                    m_HolesIgnored.Add(name);
            }
            else
            {
                result = section.FromTsdExplicitSection(material);
                Add(m_Explicit, unsupportedReason ?? "the BHoM profile could not be created from its dimensions", name);
            }

            m_Sections[key] = result;
            return result;
        }

        /***************************************************/

        // One summary for the whole pull. Silent when nothing was converted.
        public void Report()
        {
            if (m_MaterialsMatched.Count + m_MaterialsFromModel.Count > 0)
            {
                Engine.Base.Compute.RecordNote("Materials: " +
                    (m_MaterialsMatched.Count > 0 ? List(m_MaterialsMatched) + " matched to the BHoM material library" : "none matched to the BHoM material library") +
                    (m_MaterialsFromModel.Count > 0 ? "; " + List(m_MaterialsFromModel) + " built from the values in the model" : "") + ".");
            }

            foreach (string rejection in m_MaterialRejections)
                Engine.Base.Compute.RecordWarning(rejection + ", so the material was built from the values in the model instead.");

            m_Unrecognised.Report();

            if (m_ApproximatedReleaseEnds > 0)
            {
                Engine.Base.Compute.RecordWarning(m_ApproximatedReleaseEnds + " bar end(s) have a nominally pinned, nominally fixed, partially fixed or non linear spring connection, which BHoM has no equivalent for. " +
                    "Nominally pinned ends have been pulled as free, nominally fixed as fixed, and the others as a linear spring of the stiffness Tekla Structural Designer reports.");
            }

            int total = m_LibraryMatched.Count + m_FromProfile.Count + m_Explicit.Values.Sum(v => v.Count);
            if (total == 0)
                return;

            Engine.Base.Compute.RecordNote(total + " distinct section(s) pulled: " +
                m_LibraryMatched.Count + " matched to the BHoM steel section library, " +
                m_FromProfile.Count + " built from their Tekla Structural Designer dimensions, " +
                m_Explicit.Values.Sum(v => v.Count) + " pulled as explicit section constants.");

            if (m_CatalogueNotInLibrary.Count > 0)
            {
                Engine.Base.Compute.RecordNote(m_CatalogueNotInLibrary.Count + " steel section(s) were not found in the BHoM section library and were built from their dimensions instead: " +
                    List(m_CatalogueNotInLibrary) + ".");
            }

            foreach (string rejection in m_LibraryRejections)
                Engine.Base.Compute.RecordWarning(rejection + ", so the section was built from its own dimensions instead.");

            foreach (KeyValuePair<string, HashSet<string>> reason in m_Explicit)
            {
                Engine.Base.Compute.RecordWarning(reason.Value.Count + " section(s) were pulled as an ExplicitSection with no shape, because " + reason.Key + ": " +
                    List(reason.Value) + ".");
            }

            if (m_HolesIgnored.Count > 0)
            {
                Engine.Base.Compute.RecordWarning(m_HolesIgnored.Count + " section(s) contain holes, which BHoM profiles cannot represent. They were built without them, so their section properties are overstated: " +
                    List(m_HolesIgnored) + ".");
            }
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Name alone is not unique across parametric sizes or grades, so constants and material are added.
        private static string Key(ISection section, IMaterialFragment material)
        {
            return string.Join("|",
                section.GetType().FullName,
                section.SectionName(),
                section.CrossSectionalArea.ToString("R", CultureInfo.InvariantCulture),
                section.MajorAxisSecondMomentOfArea.ToString("R", CultureInfo.InvariantCulture),
                section.MinorAxisSecondMomentOfArea.ToString("R", CultureInfo.InvariantCulture),
                material == null ? "" : material.GetType().Name + ":" + material.Name);
        }

        /***************************************************/

        private static void Add(Dictionary<string, HashSet<string>> reasons, string reason, string name)
        {
            HashSet<string> names;
            if (!reasons.TryGetValue(reason, out names))
            {
                names = new HashSet<string>();
                reasons[reason] = names;
            }

            names.Add(name);
        }

        /***************************************************/

        private static string List(IEnumerable<string> names)
        {
            return Helpers.Summary(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        }

        /***************************************************/
    }
}
