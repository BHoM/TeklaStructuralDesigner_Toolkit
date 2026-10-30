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
using BH.oM.Structure.Elements;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Structure;
using BHoMLoad = BH.oM.Structure.Loads.ILoad;
using EntityType = TSD.API.Remoting.Common.EntityType;
using TsdLoadType = TSD.API.Remoting.Loading.LoadType;
using TsdPlane = TSD.API.Remoting.Geometry.IPlane;

namespace BH.Adapter.TeklaStructuralDesigner
{
    public partial class TeklaStructuralDesignerAdapter
    {
        /***************************************************/
        /**** Private Fields                            ****/
        /***************************************************/

        // Every planar load type, all read so the unconvertible ones are reported rather than missing.
        private static readonly TsdLoadType[] m_PlanarLoadTypes =
        {
            TsdLoadType.Slab, TsdLoadType.UniformAreaElement, TsdLoadType.VariableAreaElement, TsdLoadType.ConstructionPlane,
            TsdLoadType.UniformRectangular, TsdLoadType.UniformPolygonal, TsdLoadType.VariablePolygonal,
            TsdLoadType.UniformLine, TsdLoadType.Point, TsdLoadType.Perimeter,
        };

        // Every load type a load pull reads: member, nodal and planar.
        private static readonly TsdLoadType[] m_PulledLoadTypes = new[] { TsdLoadType.Member, TsdLoadType.Nodal }.Concat(m_PlanarLoadTypes).ToArray();

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // Uniform loads on an element become AreaUniformlyDistributedLoads on its Panels, patch loads
        // ContourLoads and line loads GeometricalLineLoads. Point, varying, perimeter and whole plane
        // loads, and loads on vertical construction planes, are skipped with a counted reason.
        private List<BHoMLoad> ReadPlanarLoads(List<PendingLoad> planar, WarningTally warnings, int timeout)
        {
            List<BHoMLoad> result = new List<BHoMLoad>();
            if (planar.Count == 0)
                return result;

            // Only read the Panels and planes the convertible loads need.
            HashSet<EntityType> elementTypes = new HashSet<EntityType>(planar
                .Select(p => p.Load as IPlanarAreaElementLoad)
                .Where(l => l != null && !(l is IConstructionPlaneLoad) && UniformLoad(l, out _))
                .Select(l => l.AreaElementInfo.Type));

            Dictionary<Tuple<EntityType, int>, List<Panel>> panelsByElement = elementTypes.Count > 0
                ? PanelsByElement(elementTypes, timeout)
                : new Dictionary<Tuple<EntityType, int>, List<Panel>>();

            Dictionary<Tuple<EntityType, int>, TsdPlane> planeByInfo = planar.Any(p => (p.Load is IPolygonalLoad && UniformLoad((IPlanarLoad)p.Load, out _)) || p.Load is IUniformLineLoad)
                ? ConstructionPlanes(timeout)
                : new Dictionary<Tuple<EntityType, int>, TsdPlane>();

            foreach (PendingLoad p in planar)
            {
                IPlanarLoad load = (IPlanarLoad)p.Load;
                string skipReason = null;
                BHoMLoad converted = null;
                TsdPlane plane;

                double uniform;
                bool isUniform = UniformLoad(load, out uniform);

                if (load is IConstructionPlaneLoad)
                {
                    skipReason = "they are applied to a whole construction plane, which has no BHoM element to carry them";
                }
                else if (isUniform && load is IPlanarAreaElementLoad onElement)
                {
                    List<Panel> panels;
                    if (panelsByElement.TryGetValue(Tuple.Create(onElement.AreaElementInfo.Type, onElement.AreaElementInfo.Index), out panels) && panels.Count > 0)
                        converted = load.FromTsdAreaLoad(uniform, panels, p.Loadcase, out skipReason);
                    else
                        skipReason = "the " + onElement.AreaElementInfo.Type + " they are applied to could not be matched to a pulled Panel";
                }
                else if (isUniform && load is IPolygonalLoad patch)
                {
                    if (TryPlane(load, planeByInfo, out plane, out skipReason))
                        converted = patch.FromTsdContourLoad(uniform, plane, p.Loadcase, out skipReason);
                }
                else if (load is IUniformLineLoad line)
                {
                    if (TryPlane(load, planeByInfo, out plane, out skipReason))
                        converted = line.FromTsdLineLoad(plane, p.Loadcase, out skipReason);
                }
                else if (load is IPointLoad)
                {
                    skipReason = "they are point loads on a slab or other plane, which are not supported at the moment: BHoM has no point load type for a point on a Panel (PointLoad needs a Node, BarPointLoad a Bar), so these loads are missing from the pull - see the toolkit README";
                }
                else if (load is IVariableAreaElementLoad || load is IVariablePolygonalLoad)
                {
                    skipReason = "they vary across their area, which no BHoM area load can represent";
                }
                else if (load is IPerimeterLoad)
                {
                    skipReason = "they are perimeter loads, which are not yet converted";
                }
                else
                {
                    skipReason = "'" + load.Type + "' is not a planar load type this toolkit converts";
                }

                if (converted != null)
                    result.Add(converted);
                else
                    warnings.Skip("load(s)", skipReason);
            }

            return result;
        }

        /***************************************************/

        // The value (N/mm²) of a uniform planar load. These types share no interface for it.
        private static bool UniformLoad(IPlanarLoad load, out double value)
        {
            switch (load)
            {
                case ISlabLoad slab: value = slab.Load; return true;
                case IUniformAreaElementLoad element: value = element.Load; return true;
                case IConstructionPlaneLoad onPlane: value = onPlane.Load; return true;
                case IUniformRectangularLoad rectangle: value = rectangle.Load; return true;
                case IUniformPolygonalLoad polygon: value = polygon.Load; return true;
                default: value = 0; return false;
            }
        }

        /***************************************************/

        private static bool TryPlane(IPlanarLoad load, Dictionary<Tuple<EntityType, int>, TsdPlane> planeByInfo, out TsdPlane plane, out string skipReason)
        {
            skipReason = null;
            if (planeByInfo.TryGetValue(Tuple.Create(load.PlaneInfo.Type, load.PlaneInfo.Index), out plane) && plane != null)
                return true;

            skipReason = load.PlaneInfo.Type == EntityType.VerticalConstructionPlane
                ? "they are drawn on a vertical construction plane, whose geometry the API does not expose"
                : "the construction plane they are drawn on could not be read";
            return false;
        }

        /***************************************************/

        // The plane of every level and slope, keyed as planar loads refer to them.
        private Dictionary<Tuple<EntityType, int>, TsdPlane> ConstructionPlanes(int timeout)
        {
            Dictionary<Tuple<EntityType, int>, TsdPlane> result = new Dictionary<Tuple<EntityType, int>, TsdPlane>();

            foreach (IHorizontalConstructionPlane level in ReadTsdLevels(timeout, "patch and line loads on levels cannot be placed"))
                result[Tuple.Create(EntityType.HorizontalConstructionPlane, level.Index)] = level.Plane.ValueOrDefault();

            IEnumerable<ISlopedConstructionPlane> slopes;
            if (Async.TryRunSync(ct => m_Model.GetSlopesAsync(null, ct), timeout, "reading sloped planes", "patch and line loads on them cannot be placed", out slopes))
            {
                foreach (ISlopedConstructionPlane slope in slopes)
                    result[Tuple.Create(EntityType.SlopedConstructionPlane, slope.Index)] = slope.Plane.ValueOrDefault();
            }

            return result;
        }

        /***************************************************/
    }
}
