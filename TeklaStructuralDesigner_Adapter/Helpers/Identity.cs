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
using System.Security.Cryptography;
using System.Text;
using BH.Engine.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Base;
using BH.oM.Structure.Elements;

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Helpers
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Sets the TeklaStructuralDesignerId (id is the readable identifier, persistentId the Tekla
        // Structural Designer Guid) and a BHoM_Guid derived from the same Guid - see StableGuid. piece
        // tells apart the objects one element becomes, such as the Panels a slab item is cut into.
        public static T SetIdentity<T>(this T obj, object id, Guid persistentId, int piece = 0) where T : IBHoMObject
        {
            obj.BHoM_Guid = StableGuid(obj.GetType().Name, persistentId, piece);
            obj.SetAdapterId(new TeklaStructuralDesignerId { Id = id, PersistentId = persistentId });
            return obj;
        }

        /***************************************************/

        public static Panel SetIdentity(this Panel panel, string id, Guid persistentId, TeklaStructuralDesignerPanelProperties properties, int piece = 0)
        {
            panel.SetIdentity<Panel>(id, persistentId, piece);
            panel.Fragments.Add(properties);
            return panel;
        }

        /***************************************************/

        // A name based (SHA-1) Guid, the same every time the same object is pulled.
        // Elements are rebuilt inside every load pulled against them, and BHoM's push matches a load's
        // elements to the pushed ones by BHoM_Guid (ReplaceObjectsInLoadsModule). The adapter cache
        // only shares instances within one Pull, so separate Pulls of Panels and their loads need this.
        public static Guid StableGuid(string kind, Guid tsdId, int piece)
        {
            byte[] name = Encoding.UTF8.GetBytes("TeklaStructuralDesigner|" + kind + "|" + tsdId.ToString("D") + "|" + piece);

            byte[] hash;
            using (SHA1 sha1 = SHA1.Create())
                hash = sha1.ComputeHash(name);

            byte[] guid = new byte[16];
            Array.Copy(hash, guid, 16);
            guid[6] = (byte)((guid[6] & 0x0F) | 0x50);     // version 5
            guid[8] = (byte)((guid[8] & 0x3F) | 0x80);     // RFC 4122 variant
            return new Guid(guid);
        }

        /***************************************************/
    }
}
