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
using BH.Adapter.TeklaStructuralDesigner;
using BH.Engine.Adapter;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Geometry;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Loads;
using BH.oM.Structure.Results;
using NUnit.Framework;
using Shouldly;
using TSD.API.Remoting.Loading;
using TsdAnalysisType = TSD.API.Remoting.Solver.AnalysisType;
using TsdLoadingResultType = TSD.API.Remoting.Loading.LoadingResultType;
using TsdPoint3D = TSD.API.Remoting.Geometry.Point3D;
using TsdVector3D = TSD.API.Remoting.Geometry.Vector3D;
using IDisplacement = TSD.API.Remoting.Loading.IDisplacement;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    // Headless: the conversions and helpers are pure, so these need no Tekla Structural Designer.
    public class ConvertTests
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        [Test]
        public void PointsAreScaledAndVectorsAreNot()
        {
            Point point = new TsdPoint3D(1000, 2000, -500).FromTsdPoint();
            Vector vector = new TsdVector3D(0, 0, 1).FromTsdVector();

            point.X.ShouldBe(1, 1e-12);
            point.Y.ShouldBe(2, 1e-12);
            point.Z.ShouldBe(-0.5, 1e-12);
            vector.Z.ShouldBe(1);
        }

        /***************************************************/

        [Test]
        public void StableGuidIsTheSameForTheSameObjectOnly()
        {
            Guid tsdId = Guid.NewGuid();
            Guid guid = Helpers.StableGuid("Panel", tsdId, 0);

            Helpers.StableGuid("Panel", tsdId, 0).ShouldBe(guid);
            Helpers.StableGuid("Panel", tsdId, 1).ShouldNotBe(guid);
            Helpers.StableGuid("Bar", tsdId, 0).ShouldNotBe(guid);
        }

        /***************************************************/

        [Test]
        public void SummaryShowsTheFirstNamesAndCountsTheRest()
        {
            Helpers.Summary(new[] { "A", "", "C" }).ShouldBe("A, <unnamed>, C");
            Helpers.Summary(Enumerable.Range(1, 12).Select(i => i.ToString())).ShouldBe("1, 2, 3, 4, 5, 6, 7, 8, 9, 10 and 2 more");
        }

        /***************************************************/

        [Test]
        public void WarningTallyReportsEachMessageOnceWithItsCount()
        {
            WarningTally tally = new WarningTally();
            tally.Skip("load(s)", "x");
            tally.Skip("load(s)", "x");
            tally.Skip("load(s)", null);

            BH.Engine.Base.Compute.ClearCurrentEvents();
            tally.Report();

            tally.Total.ShouldBe(3);
            BH.Engine.Base.Query.CurrentEvents().Select(e => e.Message).ShouldBe(new[]
            {
                "2 load(s) were skipped because x.",
                "1 load(s) were skipped because they could not be converted.",
            });
        }

        /***************************************************/
    }
}
