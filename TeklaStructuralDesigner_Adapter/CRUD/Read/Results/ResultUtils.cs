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
using BH.oM.Structure.Loads;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Result request filtering)     ****/
        /***************************************************/

        // An empty or null ObjectIds means "every span". Otherwise, each requested item can be the
        // ObjectId string itself (matching a Bar previously pulled from this adapter), or a Bar object
        // carrying a TeklaStructuralDesignerId fragment, in which case that fragment's Id is what is
        // matched against.
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
                    string.Join(", ", duplicated.Take(10)) + (duplicated.Count > 10 ? ", ..." : "") + ".");
            }

            List<TsdSpanIdentity> matched = new List<TsdSpanIdentity>();
            List<string> unmatched = new List<string>();

            foreach (object requested in objectIds)
            {
                string key = SpanKey(requested);
                TsdSpanIdentity found;

                if (key != null && byObjectId.TryGetValue(key, out found))
                    matched.Add(found);
                else
                    unmatched.Add(key ?? requested?.ToString() ?? "<null>");
            }

            if (unmatched.Count > 0)
            {
                string message = "Could not match " + unmatched.Count + " of " + objectIds.Count +
                    " requested object(s) to a span. Accepted forms are the ObjectId string (e.g. 'B12:0') or a Bar previously pulled from this adapter. Unmatched: " +
                    string.Join(", ", unmatched.Take(10)) + (unmatched.Count > 10 ? ", ..." : "");

                if (matched.Count == 0)
                    Engine.Base.Compute.RecordError(message);
                else
                    Engine.Base.Compute.RecordWarning(message);
            }

            return matched;
        }

        /***************************************************/

        private static string SpanKey(object requested)
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

        // Which cases a result request reads.
        //
        // With no cases named, the pull configuration decides: the Strength combinations by default,
        // plus the Service combinations and the loadcases if their Include flags are set.
        //
        // Named cases are always read, whatever the flags say, and each can be given as:
        //  - a Loadcase or LoadCombination pulled from this adapter - matched on the Guid its results
        //    are read with (TeklaStructuralDesignerId.PersistentId), then on its Number, then its Name;
        //  - a case number, as a number or as text - 1048 is the Strength and 2048 the Service version
        //    of Tekla Structural Designer combination 48, and 48 alone is loadcase 48;
        //  - a case name, exactly as pulled - 'Strength 48 ...', 'Service 48 ...' or a loadcase name;
        //  - the Guid Tekla Structural Designer uses internally.
        // Numbers are unique across loadcases, Strength and Service combinations (see Engine
        // Query.CaseNumber), so no request can land on the wrong limit state or on a loadcase by mistake.
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

            if (unmatched.Count > 0)
            {
                string message = "Could not match " + unmatched.Count + " of " + requestedCases.Count +
                    " requested loading case(s). Accepted forms are a Loadcase or LoadCombination pulled from this adapter, its Number (1000 + n for Strength and 2000 + n for Service combination n), its Name, or its Guid. Unmatched: " +
                    string.Join(", ", unmatched.Take(10)) + (unmatched.Count > 10 ? ", ..." : "");

                if (matched.Count == 0)
                    Engine.Base.Compute.RecordError(message);
                else
                    Engine.Base.Compute.RecordWarning(message);
            }

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
                // The Guid on the case's own id fragment is the one its results are read with, so it
                // is the most exact match there is.
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

        // A requested case given as a number: any whole number type, or text that is a whole number.
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
    }
}
