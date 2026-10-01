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

namespace BH.Adapter.TeklaStructuralDesigner
{
    internal static partial class Convert
    {
        /***************************************************/
        /****            Public Methods                 ****/
        /***************************************************/

        // The identity every pulled element carries, set in one place: a TeklaStructuralDesignerId
        // fragment (id is the readable identifier the object is requested by, persistentId the Guid
        // Tekla Structural Designer uses for it), and a BHoM_Guid derived from that same Guid - see
        // StableGuid. piece tells apart the objects one Tekla Structural Designer element becomes, such
        // as the Panels a slab item is cut into by openings.
        public static T SetIdentity<T>(this T obj, object id, Guid persistentId, int piece = 0) where T : IBHoMObject
        {
            obj.BHoM_Guid = StableGuid(obj.GetType().Name, persistentId, piece);
            obj.SetAdapterId(new TeklaStructuralDesignerId { Id = id, PersistentId = persistentId });
            return obj;
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        // A BHoM_Guid derived from the Tekla Structural Designer object an object was pulled from,
        // instead of a fresh random one. Pulled Bars, Nodes and Panels are built more than once: once
        // when they are pulled for themselves, and again inside every load pulled against them. With
        // random Guids those are strangers to each other, and a push cannot tell that the Panel inside
        // an AreaUniformlyDistributedLoad is the Panel being pushed alongside it - so the load's Panel
        // never receives the analysis package's id, and Robot refuses the load. BHoM's push pre-process
        // (ReplaceObjectsInLoadsModule) swaps each element of a load for the pushed object with the same
        // BHoM_Guid, so a Guid that is the same every time the same object is pulled is what ties them.
        //
        // Name based (RFC 4122 version 5, SHA-1), from the kind of object, the Tekla Structural Designer
        // Guid and, where one element becomes several objects, which piece it is.
        private static Guid StableGuid(string kind, Guid tsdId, int piece)
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
