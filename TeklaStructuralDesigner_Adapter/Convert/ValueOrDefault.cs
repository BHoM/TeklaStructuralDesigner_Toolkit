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
using System.Linq;
using TSD.API.Remoting.Common.Properties;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // Nearly every property returned by Tekla Structural Designer is wrapped in IReadOnlyProperty<T>,
    // which carries an IsApplicable flag: many properties are only meaningful for some element types
    // (a concrete option on a steel span, for instance), and reading .Value when IsApplicable is false
    // is not something to rely on. Every unwrap in this toolkit goes through here, so that rule is
    // enforced in exactly one place. IPropertyWithValidValues<T> and IProperty<T> both derive from
    // IReadOnlyProperty<T>, so this single method also covers them.
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        public static TValue ValueOrDefault<TValue>(this IReadOnlyProperty<TValue> property, TValue defaultValue = default(TValue))
        {
            return property != null && property.IsApplicable ? property.Value : defaultValue;
        }

        /***************************************************/

        // IListOfReadOnlyProperties<TItem> is not a plain list of TItem: it is a list of
        // IReadOnlyProperty<TItem>, one wrapper per item, each with its own IsApplicable flag. This
        // unwraps such a list, silently dropping items that are not applicable.
        public static IEnumerable<TItem> ValuesOrEmpty<TItem>(this IEnumerable<IReadOnlyProperty<TItem>> items)
        {
            if (items == null)
                return Enumerable.Empty<TItem>();

            return items.Where(item => item != null && item.IsApplicable).Select(item => item.Value);
        }

        /***************************************************/
    }
}
