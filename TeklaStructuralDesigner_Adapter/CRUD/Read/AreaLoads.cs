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
        /****            Private Fields                 ****/
        /***************************************************/

        // Every load Tekla Structural Designer draws on a plane or an area element. All are read, so
        // that the ones with no BHoM equivalent are reported rather than silently missing.
        private static readonly TsdLoadType[] m_PlanarLoadTypes =
        {
            TsdLoadType.Slab, TsdLoadType.UniformAreaElement, TsdLoadType.VariableAreaElement, TsdLoadType.ConstructionPlane,
            TsdLoadType.UniformRectangular, TsdLoadType.UniformPolygonal, TsdLoadType.VariablePolygonal,
            TsdLoadType.UniformLine, TsdLoadType.Point, TsdLoadType.Perimeter,
        };

        // Every load type a load pull reads: member, nodal and planar.
        private static readonly TsdLoadType[] m_PulledLoadTypes = new[] { TsdLoadType.Member, TsdLoadType.Nodal }.Concat(m_PlanarLoadTypes).ToArray();

        /***************************************************/
        /****            Private Methods                ****/
        /****            (Area loads)                   ****/
        /***************************************************/

        // Converts the planar loads of a load pull:
        //
        //   - a uniform load on a whole element (slab item, slab, wall, roof, wind wall) becomes an
        //     AreaUniformlyDistributedLoad on the Panels pulled for that element;
        //   - a rectangular or polygonal patch load becomes a ContourLoad over its outline;
        //   - a line load drawn on a plane becomes a GeometricalLineLoad.
        //
        // Refused, each with a counted reason: point loads on a plane (BHoM has no point load that is not
        // on a Node), varying area and polygonal loads (no BHoM equivalent), perimeter loads (a line load
        // along a slab's edges, not yet mapped), loads on a whole construction plane (no BHoM element to
        // carry them), and loads on a vertical construction plane, which the API offers no way to read
        // the plane of.
        private List<BHoMLoad> ReadPlanarLoads(List<PendingLoad> planar, Dictionary<string, int> skipReasons, int timeout)
        {
            List<BHoMLoad> result = new List<BHoMLoad>();
            if (planar.Count == 0)
                return result;

            // Only read what the convertible loads present actually need: the Panels of the kinds of
            // element uniform loads are on, and the construction planes only if a patch or line load
            // is there to be placed on one.
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
                        converted = load.ToBHoM(uniform, panels, p.Loadcase, out skipReason);
                    else
                        skipReason = "the " + onElement.AreaElementInfo.Type + " they are applied to could not be matched to a pulled Panel";
                }
                else if (isUniform && load is IPolygonalLoad patch)
                {
                    if (TryPlane(load, planeByInfo, out plane, out skipReason))
                        converted = patch.ToBHoMContour(uniform, plane, p.Loadcase, out skipReason);
                }
                else if (load is IUniformLineLoad line)
                {
                    if (TryPlane(load, planeByInfo, out plane, out skipReason))
                        converted = line.ToBHoM(plane, p.Loadcase, out skipReason);
                }
                else if (load is IPointLoad)
                {
                    skipReason = "they are point loads on a plane, and BHoM has no point load that is not applied to a Node";
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
                    Count(skipReasons, skipReason);
            }

            return result;
        }

        /***************************************************/

        // The single load value of the uniform planar load types, in N/mm².
        // The API gives the five uniform planar load types no shared interface for their value, so each
        // is asked in turn.
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

        // The plane of every level and sloped construction plane, keyed the way a planar load refers to
        // its plane. A patch or line load's coordinates are in the local coordinates of that plane.
        private Dictionary<Tuple<EntityType, int>, TsdPlane> ConstructionPlanes(int timeout)
        {
            Dictionary<Tuple<EntityType, int>, TsdPlane> result = new Dictionary<Tuple<EntityType, int>, TsdPlane>();

            foreach (IHorizontalConstructionPlane level in ReadTsdLevels(timeout, "patch and line loads on levels cannot be placed"))
                result[Tuple.Create(EntityType.HorizontalConstructionPlane, level.Index)] = level.Plane.ValueOrDefault();

            try
            {
                foreach (ISlopedConstructionPlane slope in Async.RunSync(ct => m_Model.GetSlopesAsync(null, ct), timeout, "reading sloped planes"))
                    result[Tuple.Create(EntityType.SlopedConstructionPlane, slope.Index)] = slope.Plane.ValueOrDefault();
            }
            catch (Exception e)
            {
                Engine.Base.Compute.RecordWarning("Failed to read sloped construction planes from Tekla Structural Designer, so patch and line loads on them cannot be placed. " + e.Message);
            }

            return result;
        }

        /***************************************************/
    }
}
