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

using System.Linq;
using BH.Adapter.TeklaStructuralDesigner;
using BH.oM.Adapters.TeklaStructuralDesigner;
using BH.oM.Structure.Elements;
using BH.oM.Structure.Requests;
using BH.oM.Structure.Results;
using NUnit.Framework;
using Shouldly;

namespace BH.Tests.Adapter.TeklaStructuralDesigner
{
    public class ConnectionTests
    {
        /***************************************************/
        /**** Public Tests - these run without Tekla    ****/
        /**** Structural Designer being installed or    ****/
        /**** running, and are exercised by CI.         ****/
        /***************************************************/

        [Test]
        [Description("An adapter created with active = false must not attempt to connect, must keep the configuration it was given, and must still identify its objects with a TeklaStructuralDesignerId.")]
        public void InactiveAdapterConstructsWithoutConnecting()
        {
            TeklaStructuralDesignerConfig config = new TeklaStructuralDesignerConfig { TimeoutSeconds = 42 };
            BH.Engine.Base.Compute.ClearCurrentEvents();

            TeklaStructuralDesignerAdapter adapter = new TeklaStructuralDesignerAdapter("", config, false);

            adapter.AdapterIdFragmentType.ShouldBe(typeof(TeklaStructuralDesignerId));
            adapter.TeklaStructuralDesignerConfig.ShouldBeSameAs(config);
            BH.Engine.Base.Query.CurrentEvents()
                .Any(e => e.Type == BH.oM.Base.Debugging.EventType.Error)
                .ShouldBeFalse();
        }

        /***************************************************/

        [Test]
        [Description("Requesting active = true with no Tekla Structural Designer instance running must record exactly one clear error and must not throw.")]
        public void ActiveAdapterWithNoRunningInstanceRecordsOneError()
        {
            // On a developer machine with Tekla Structural Designer open the adapter connects, so there
            // is nothing to test; report that rather than fail.
            bool running = TSD.API.Remoting.ApplicationFactory.GetRunningApplicationsAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult().Any();
            Assume.That(running, Is.False, "Tekla Structural Designer is running, so the no-instance path cannot be exercised.");

            BH.Engine.Base.Compute.ClearCurrentEvents();

            TeklaStructuralDesignerAdapter adapter = null;
            Should.NotThrow(() => adapter = new TeklaStructuralDesignerAdapter("", null, true));

            var errors = BH.Engine.Base.Query.CurrentEvents()
                .Where(e => e.Type == BH.oM.Base.Debugging.EventType.Error)
                .ToList();

            errors.Count.ShouldBe(1);
            errors[0].Message.ShouldContain("No running instance");
        }

        /***************************************************/

        [Test]
        [Description("Pull on an adapter that never connected must return an empty list with a warning, not throw.")]
        public void PullWithNoConnectionReturnsEmptyWithWarning()
        {
            TeklaStructuralDesignerAdapter adapter = new TeklaStructuralDesignerAdapter();
            BH.Engine.Base.Compute.ClearCurrentEvents();

            var request = new BarResultRequest { ResultType = BarResultType.BarForce };
            var pulled = adapter.Pull(request).ToList();

            pulled.Count.ShouldBe(0);
            BH.Engine.Base.Query.CurrentEvents()
                .Any(e => e.Type == BH.oM.Base.Debugging.EventType.Warning)
                .ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("ReadResults has its own connection guard, independent of Pull's, in case it is ever called directly.")]
        public void ReadResultsWithNoConnectionReturnsEmptyWithWarning()
        {
            TeklaStructuralDesignerAdapter adapter = new TeklaStructuralDesignerAdapter();
            BH.Engine.Base.Compute.ClearCurrentEvents();

            var results = adapter.ReadResults(new BarResultRequest { ResultType = BarResultType.BarForce }).ToList();

            results.Count.ShouldBe(0);
            BH.Engine.Base.Query.CurrentEvents()
                .Any(e => e.Type == BH.oM.Base.Debugging.EventType.Warning)
                .ShouldBeTrue();
        }

        /***************************************************/

        [Test]
        [Description("Push is not supported by this adapter and must say so clearly rather than silently succeed.")]
        public void PushRecordsAnErrorAndReturnsEmpty()
        {
            TeklaStructuralDesignerAdapter adapter = new TeklaStructuralDesignerAdapter();
            BH.Engine.Base.Compute.ClearCurrentEvents();

            var pushed = adapter.Push(new System.Collections.Generic.List<object> { new Bar() });

            pushed.Count.ShouldBe(0);
            BH.Engine.Base.Query.CurrentEvents()
                .Any(e => e.Type == BH.oM.Base.Debugging.EventType.Error)
                .ShouldBeTrue();
        }

        /***************************************************/
    }
}
