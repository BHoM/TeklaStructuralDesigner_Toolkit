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
using BH.Engine.Base;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Empty ObjectIds means every span; otherwise each is an ObjectId or a pulled Bar.
        private List<TsdSpanIdentity> FilterSpans(List<TsdSpanIdentity> spans, List<object> objectIds)
        {
            if (objectIds == null || objectIds.Count == 0)
                return spans;

            List<IGrouping<string, TsdSpanIdentity>> groups = spans.GroupBy(s => s.ObjectId, StringComparer.OrdinalIgnoreCase).ToList();
            Dictionary<string, TsdSpanIdentity> byObjectId = groups.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            List<string> duplicated = groups.Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicated.Count > 0)
            {
                Engine.Base.Compute.RecordWarning(duplicated.Count + " span identifier(s) are shared by more than one member, and only the first span of each has been read: " +
                    Helpers.Summary(duplicated) + ".");
            }

            List<TsdSpanIdentity> matched = new List<TsdSpanIdentity>();
            List<string> unmatched = new List<string>();

            foreach (object requested in objectIds)
            {
                string key = ObjectKey(requested);
                TsdSpanIdentity found;

                if (key != null && byObjectId.TryGetValue(key, out found))
                    matched.Add(found);
                else
                    unmatched.Add(key ?? requested?.ToString() ?? "<null>");
            }

            ReportUnmatched(unmatched, objectIds.Count, matched.Count, "object(s) to a span", "Accepted forms are the ObjectId string (e.g. 'B12:0') or a Bar previously pulled from this adapter. ");

            return matched;
        }

        /***************************************************/

        // Empty ObjectIds means every Node; otherwise each is a Node id (its construction point Guid)
        // or a pulled Node.
        private List<KeyValuePair<Guid, Node>> FilterNodes(Dictionary<Guid, Node> nodeById, List<object> objectIds)
        {
            if (objectIds == null || objectIds.Count == 0)
                return nodeById.ToList();

            List<KeyValuePair<Guid, Node>> matched = new List<KeyValuePair<Guid, Node>>();
            HashSet<Guid> seen = new HashSet<Guid>();
            List<string> unmatched = new List<string>();

            foreach (object requested in objectIds)
            {
                string key = ObjectKey(requested);
                Guid id;
                Node found;

                if (key == null || !Guid.TryParse(key, out id) || !nodeById.TryGetValue(id, out found))
                    unmatched.Add(key ?? "<null>");
                else if (seen.Add(id))
                    matched.Add(new KeyValuePair<Guid, Node>(id, found));
            }

            ReportUnmatched(unmatched, objectIds.Count, matched.Count, "object(s) to a Node", "Accepted forms are the Node's id (the Guid of its construction point) or a Node previously pulled from this adapter. ");

            return matched;
        }

        /***************************************************/

        // The text a requested object is matched on: itself, or its TeklaStructuralDesignerId.
        private static string ObjectKey(object requested)
        {
            if (requested == null)
                return null;

            string asString = requested as string;
            if (asString != null)
                return asString;

            BH.oM.Base.IBHoMObject asBHoM = requested as BH.oM.Base.IBHoMObject;
            if (asBHoM != null)
            {
                object id = Engine.Adapter.Query.AdapterId<object>(asBHoM, typeof(TeklaStructuralDesignerId));
                if (id != null)
                    return id.ToString();
            }

            return requested.ToString();
        }

        /***************************************************/

        // With no cases named, the pull configuration's Include flags decide. Named cases are always
        // read, and can be a pulled Loadcase or LoadCombination, a case Number (1048 Strength and 2048
        // Service for combination 48), a Name or a Guid. Numbers are unique across all of them.
        private List<TsdLoadingCaseIdentity> FilterCases(List<TsdLoadingCaseIdentity> cases, List<object> requestedCases, TeklaStructuralDesignerPullConfig config)
        {
            if (requestedCases == null || requestedCases.Count == 0)
            {
                List<TsdLoadingCaseIdentity> byConfig = cases.Where(c =>
                    c.LimitState == TeklaStructuralDesignerLimitState.Strength ? config.IncludeStrengthCombinations :
                    c.LimitState == TeklaStructuralDesignerLimitState.Service ? config.IncludeServiceCombinations :
                    config.IncludeLoadcases).ToList();

                Engine.Base.Compute.RecordNote("No specific loading cases were requested, so results have been read for " + IncludedDescription(config) +
                    ". Name cases on the request, or set the Include flags on the pull configuration, to read others.");

                if (byConfig.Count == 0)
                    Engine.Base.Compute.RecordError("The pull configuration excludes every loading case: set at least one of IncludeStrengthCombinations, IncludeServiceCombinations or IncludeLoadcases.");

                return byConfig;
            }

            Dictionary<Guid, TsdLoadingCaseIdentity> byId = cases
                .Where(c => c.Id != Guid.Empty)
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First());

            Dictionary<int, TsdLoadingCaseIdentity> byNumber = cases
                .GroupBy(c => c.Number)
                .ToDictionary(g => g.Key, g => g.First());

            Dictionary<string, TsdLoadingCaseIdentity> byName = cases
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            List<TsdLoadingCaseIdentity> matched = new List<TsdLoadingCaseIdentity>();
            HashSet<TsdLoadingCaseIdentity> seen = new HashSet<TsdLoadingCaseIdentity>();
            List<string> unmatched = new List<string>();

            foreach (object requested in requestedCases)
            {
                TsdLoadingCaseIdentity found = MatchCase(requested, byId, byNumber, byName);
                if (found == null)
                    unmatched.Add(CaseDescription(requested));
                else if (seen.Add(found))
                    matched.Add(found);
            }

            ReportUnmatched(unmatched, requestedCases.Count, matched.Count, "loading case(s)", "Accepted forms are a Loadcase or LoadCombination pulled from this adapter, its Number (1000 + n for Strength and 2000 + n for Service combination n), its Name, or its Guid. ");

            return matched;
        }

        /***************************************************/

        private static string IncludedDescription(TeklaStructuralDesignerPullConfig config)
        {
            List<string> included = new List<string>();
            if (config.IncludeStrengthCombinations)
                included.Add("every Strength combination");
            if (config.IncludeServiceCombinations)
                included.Add("every Service combination");
            if (config.IncludeLoadcases)
                included.Add("every loadcase");

            return included.Count == 0 ? "no cases" : string.Join(" and ", included);
        }

        /***************************************************/

        private static TsdLoadingCaseIdentity MatchCase(object requested, Dictionary<Guid, TsdLoadingCaseIdentity> byId, Dictionary<int, TsdLoadingCaseIdentity> byNumber, Dictionary<string, TsdLoadingCaseIdentity> byName)
        {
            if (requested == null)
                return null;

            TsdLoadingCaseIdentity found;

            if (requested is Guid)
                return byId.TryGetValue((Guid)requested, out found) ? found : null;

            ICase asCase = requested as ICase;
            if (asCase != null)
            {
                // The Guid its results are read with is the most exact match.
                TeklaStructuralDesignerId id = ((BH.oM.Base.IBHoMObject)asCase).FindFragment<TeklaStructuralDesignerId>();
                if (id != null && id.PersistentId is Guid && byId.TryGetValue((Guid)id.PersistentId, out found))
                    return found;

                if (byNumber.TryGetValue(asCase.Number, out found))
                    return found;

                return !string.IsNullOrWhiteSpace(asCase.Name) && byName.TryGetValue(asCase.Name.Trim(), out found) ? found : null;
            }

            Guid parsed;
            if (Guid.TryParse(requested.ToString(), out parsed))
                return byId.TryGetValue(parsed, out found) ? found : null;

            int? number = AsCaseNumber(requested);
            if (number.HasValue && byNumber.TryGetValue(number.Value, out found))
                return found;

            return byName.TryGetValue(requested.ToString().Trim(), out found) ? found : null;
        }

        /***************************************************/

        // A whole number of any numeric type, or as text.
        private static int? AsCaseNumber(object requested)
        {
            if (requested is int)
                return (int)requested;

            if (requested is long || requested is short || requested is double || requested is float || requested is decimal)
            {
                double value = System.Convert.ToDouble(requested);
                return Math.Abs(value - Math.Round(value)) < 1e-9 ? (int?)(int)Math.Round(value) : null;
            }

            int parsed;
            return int.TryParse(requested.ToString().Trim(), out parsed) ? parsed : (int?)null;
        }

        /***************************************************/

        private static string CaseDescription(object requested)
        {
            if (requested == null)
                return "<null>";

            LoadCombination asCombination = requested as LoadCombination;
            if (asCombination != null)
                return "LoadCombination '" + asCombination.Name + "'";

            Loadcase asLoadcase = requested as Loadcase;
            if (asLoadcase != null)
                return "Loadcase '" + asLoadcase.Name + "'";

            return requested.ToString();
        }

        /***************************************************/

        // Records the requested items that matched nothing: an error if none matched, a warning if some did.
        private static void ReportUnmatched(List<string> unmatched, int requested, int matched, string what, string acceptedForms)
        {
            if (unmatched.Count == 0)
                return;

            string message = "Could not match " + unmatched.Count + " of " + requested + " requested " + what + ". " + acceptedForms + "Unmatched: " + Helpers.Summary(unmatched);

            if (matched == 0)
                Engine.Base.Compute.RecordError(message);
            else
                Engine.Base.Compute.RecordWarning(message);
        }

        /***************************************************/
    }
}
