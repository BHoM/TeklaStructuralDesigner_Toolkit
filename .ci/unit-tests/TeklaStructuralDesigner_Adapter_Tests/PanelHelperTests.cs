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
using BH.Adapter.TeklaStructuralDesigner;
using BH.oM.Geometry;
using NUnit.Framework;
using Shouldly;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    // Headless: StartAlong is pure geometry, so these need no Tekla Structural Designer.
    public class PanelHelperTests
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        [Test]
        [Description("An outline whose first edge already runs along the direction is returned as it is.")]
        public void StartAlongKeepsAnOutlineAlreadyStartingAlongTheDirection()
        {
            // Arrange
            Polyline outline = Rectangle();

            // Act
            Polyline result = outline.StartAlong(new Vector { X = 1 });

            // Assert
            result.ShouldBeSameAs(outline);
        }

        /***************************************************/

        [Test]
        [Description("An outline is restarted on the edge most nearly along the direction, keeping its vertices, their order and its closure.")]
        public void StartAlongRestartsOnTheEdgeMostNearlyAlongTheDirection()
        {
            // Arrange
            Polyline outline = Rectangle();

            // Act
            Polyline result = outline.StartAlong(new Vector { Y = 1 });

            // Assert
            result.ControlPoints.Count.ShouldBe(5);
            ShouldBeAt(result.ControlPoints[0], 4, 0);
            ShouldBeAt(result.ControlPoints[1], 4, 1);
            ShouldBeAt(result.ControlPoints[2], 0, 1);
            ShouldBeAt(result.ControlPoints[3], 0, 0);
            ShouldBeAt(result.ControlPoints[4], 4, 0);
        }

        /***************************************************/

        [Test]
        [Description("The edge direction counts regardless of sense, so a direction against the first edge keeps the outline as it is.")]
        public void StartAlongIgnoresTheSenseOfTheDirection()
        {
            // Arrange
            Polyline outline = Rectangle();

            // Act
            Polyline result = outline.StartAlong(new Vector { X = -1 });

            // Assert
            result.ShouldBeSameAs(outline);
        }

        /***************************************************/
        /**** Private Methods                           ****/
        /***************************************************/

        // A closed 4 by 1 rectangle in the XY plane, starting along global X.
        private static Polyline Rectangle()
        {
            return new Polyline
            {
                ControlPoints = new List<Point>
                {
                    new Point { X = 0, Y = 0 },
                    new Point { X = 4, Y = 0 },
                    new Point { X = 4, Y = 1 },
                    new Point { X = 0, Y = 1 },
                    new Point { X = 0, Y = 0 },
                },
            };
        }

        /***************************************************/

        private static void ShouldBeAt(Point point, double x, double y)
        {
            point.X.ShouldBe(x, 1e-12);
            point.Y.ShouldBe(y, 1e-12);
            point.Z.ShouldBe(0, 1e-12);
        }

        /***************************************************/
    }
}
