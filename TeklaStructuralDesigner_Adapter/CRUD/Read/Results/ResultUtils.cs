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
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Loads;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /****            Private Methods                ****/
        /****            (Result request filtering)      ****/
        /***************************************************/

        // An empty or null ObjectIds means "every span". Otherwise, each requested item can be the
        // ObjectId string itself (matching a Bar previously pulled from this adapter), or a Bar object
        // carrying a TeklaStructuralDesignerId fragment, in which case that fragment's Id is what is
        // matched against.
        private List<TsdSpanIdentity> FilterSpans(List<TsdSpanIdentity> spans, List<object> objectIds)
        {
            if (objectIds == null || objectIds.Count == 0)
                return spans;

            Dictionary<string, TsdSpanIdentity> byObjectId = spans.ToDictionary(s => s.ObjectId, StringComparer.OrdinalIgnoreCase);

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

        // An empty or null Cases means "every requested loading case type" (combinations by default).
        // Each requested item can be the Guid TSD uses internally, the identifier string ResultCase
        // would carry (name, user name, or index), or a BHoM Loadcase/LoadCombination object.
        private List<TsdLoadingCaseIdentity> FilterCases(List<TsdLoadingCaseIdentity> cases, List<object> requestedCases)
        {
            if (requestedCases == null || requestedCases.Count == 0)
            {
                Engine.Base.Compute.RecordNote("No specific loading cases were requested; every combination (or loadcase, per the pull configuration) has been read.");
                return cases;
            }

            Dictionary<Guid, TsdLoadingCaseIdentity> byId = cases
                .Where(c => c.Id != Guid.Empty)
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First());

            Dictionary<string, TsdLoadingCaseIdentity> byIdentifier = cases
                .GroupBy(c => c.Identifier, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            List<TsdLoadingCaseIdentity> matched = new List<TsdLoadingCaseIdentity>();
            List<string> unmatched = new List<string>();

            foreach (object requested in requestedCases)
            {
                TsdLoadingCaseIdentity found = MatchCase(requested, byId, byIdentifier);
                if (found != null)
                    matched.Add(found);
                else
                    unmatched.Add(CaseDescription(requested));
            }

            if (unmatched.Count > 0)
            {
                string message = "Could not match " + unmatched.Count + " of " + requestedCases.Count +
                    " requested loading case(s). Accepted forms are the case's Guid, its name or number, or a Loadcase/LoadCombination object. Unmatched: " +
                    string.Join(", ", unmatched.Take(10)) + (unmatched.Count > 10 ? ", ..." : "");

                if (matched.Count == 0)
                    Engine.Base.Compute.RecordError(message);
                else
                    Engine.Base.Compute.RecordWarning(message);
            }

            return matched;
        }

        /***************************************************/

        private static TsdLoadingCaseIdentity MatchCase(object requested, Dictionary<Guid, TsdLoadingCaseIdentity> byId, Dictionary<string, TsdLoadingCaseIdentity> byIdentifier)
        {
            if (requested == null)
                return null;

            if (requested is Guid)
            {
                TsdLoadingCaseIdentity found;
                return byId.TryGetValue((Guid)requested, out found) ? found : null;
            }

            LoadCombination asCombination = requested as LoadCombination;
            if (asCombination != null)
                return MatchByNameOrNumber(asCombination.Name, asCombination.Number, byIdentifier);

            Loadcase asLoadcase = requested as Loadcase;
            if (asLoadcase != null)
                return MatchByNameOrNumber(asLoadcase.Name, asLoadcase.Number, byIdentifier);

            string asString = requested.ToString();
            TsdLoadingCaseIdentity byString;
            return byIdentifier.TryGetValue(asString, out byString) ? byString : null;
        }

        /***************************************************/

        private static TsdLoadingCaseIdentity MatchByNameOrNumber(string name, int number, Dictionary<string, TsdLoadingCaseIdentity> byIdentifier)
        {
            TsdLoadingCaseIdentity found;
            if (!string.IsNullOrWhiteSpace(name) && byIdentifier.TryGetValue(name, out found))
                return found;

            return byIdentifier.TryGetValue(number.ToString(), out found) ? found : null;
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
