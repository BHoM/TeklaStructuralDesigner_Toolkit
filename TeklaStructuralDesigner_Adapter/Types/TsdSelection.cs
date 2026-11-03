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
using TSD.API.Remoting.Common.Interfaces;
using TSD.API.Remoting.Structure;
using EntityInfo = TSD.API.Remoting.Common.EntityInfo;
using EntityType = TSD.API.Remoting.Common.EntityType;
using SelectionType = TSD.API.Remoting.Common.SelectionType;
using SubEntityType = TSD.API.Remoting.Common.SubEntityType;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // The current selection, sorted into what each read needs. Elements are keyed by index, as the API
    // reads them and Panel fragments record them; levels and grid lines by Guid, as pulled.
    internal sealed class TsdSelection
    {
        /***************************************************/
        /**** Public Properties                         ****/
        /***************************************************/

        // Members selected whole, and those a selected span belongs to.
        public HashSet<int> MemberIndices { get; } = new HashSet<int>();

        public HashSet<int> Members { get; } = new HashSet<int>();

        // Member index and span index.
        public HashSet<Tuple<int, int>> Spans { get; } = new HashSet<Tuple<int, int>>();

        public HashSet<EntityType> AreaElementTypes { get; } = new HashSet<EntityType>();

        public HashSet<Tuple<EntityType, int>> AreaElements { get; } = new HashSet<Tuple<EntityType, int>>();

        // Wall index and panel index.
        public HashSet<Tuple<int, int>> WallPanels { get; } = new HashSet<Tuple<int, int>>();

        public HashSet<int> PointIndices { get; } = new HashSet<int>();

        public HashSet<int> SupportIndices { get; } = new HashSet<int>();

        public HashSet<Guid> LevelIds { get; } = new HashSet<Guid>();

        public HashSet<Guid> GridLineIds { get; } = new HashSet<Guid>();

        // What was left out, by selection type and by kind of element.
        public WarningTally NotSelectedByUser { get; } = new WarningTally();

        public WarningTally NotPulled { get; } = new WarningTally();

        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        public void Add(ISelectionItem item)
        {
            // Hovered and error flagged elements are kept in selections of their own; only the user's is pulled.
            if (item.Type != SelectionType.GlobalUser && item.Type != SelectionType.Local && item.Type != SelectionType.LocalActive)
            {
                NotSelectedByUser.Add(item.Type.ToString());
                return;
            }

            ISelectedSubEntity selectedSubEntity = item as ISelectedSubEntity;
            if (selectedSubEntity != null)
            {
                SubEntityInfo subEntity = selectedSubEntity.SubEntity;

                if (subEntity.Type == SubEntityType.Span && subEntity.EntityType == EntityType.Member)
                {
                    MemberIndices.Add(subEntity.EntityIndex);
                    Spans.Add(Tuple.Create(subEntity.EntityIndex, subEntity.Index));
                }
                else if (subEntity.Type == SubEntityType.Span && subEntity.EntityType == EntityType.StructuralWall)
                {
                    AreaElementTypes.Add(EntityType.StructuralWall);
                    WallPanels.Add(Tuple.Create(subEntity.EntityIndex, subEntity.Index));
                }
                else
                    NotPulled.Add(subEntity.Type + " of a " + subEntity.EntityType);

                return;
            }

            ISelectedEntity selectedEntity = item as ISelectedEntity;
            if (selectedEntity == null)
            {
                NotPulled.Add("Unknown");
                return;
            }

            EntityInfo entity = selectedEntity.Entity;
            switch (entity.Type)
            {
                case EntityType.Member:
                    MemberIndices.Add(entity.Index);
                    Members.Add(entity.Index);
                    break;
                case EntityType.SlabItem:
                case EntityType.Slab:
                case EntityType.StructuralWall:
                case EntityType.Roof:
                case EntityType.WindWall:
                    AreaElementTypes.Add(entity.Type);
                    AreaElements.Add(Tuple.Create(entity.Type, entity.Index));
                    break;
                case EntityType.ConstructionPoint:
                    PointIndices.Add(entity.Index);
                    break;
                case EntityType.Support:
                    SupportIndices.Add(entity.Index);
                    break;
                case EntityType.HorizontalConstructionPlane:
                    LevelIds.Add(entity.Id);
                    break;
                case EntityType.ConstructionHelper:
                    GridLineIds.Add(entity.Id);
                    break;
                default:
                    NotPulled.Add(entity.Type.ToString());
                    break;
            }
        }

        /***************************************************/
    }
}
