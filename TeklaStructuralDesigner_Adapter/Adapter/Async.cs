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
using System.Threading;
using System.Threading.Tasks;

namespace BH.Adapter.TeklaStructuralDesigner
{
    // The whole Tekla Structural Designer API is Task based; the BHoM adapter surface is synchronous.
    // Every Tekla Structural Designer call in this toolkit goes through here, so there is exactly one
    // place where that mismatch is handled.
    //
    // This is internal to the Adapter rather than a public Engine method on purpose. A public method
    // taking Func<Task<T>> has no meaningful form as a Grasshopper or Excel component, so exposing it
    // would create a broken component and demand documentation for a signature no user can supply.
    // The corollary rule for the rest of the toolkit: every await lives in the Adapter, and Engine
    // methods take already materialised values and are fully synchronous.
    internal static class Async
    {
        /***************************************************/
        /****            Internal Methods               ****/
        /***************************************************/

        // Why this cannot deadlock.
        //
        // The classic failure is calling .Result or .Wait() on a Task that was STARTED on a thread
        // carrying a SynchronizationContext - Rhino/Grasshopper's UI thread, Excel's main thread. Each
        // await inside such a task posts its continuation back to that context, the context is pumped
        // by the very thread now blocked waiting, and nothing completes.
        //
        // Task.Run marshals taskFactory onto a thread pool thread, where SynchronizationContext.Current
        // is null. Every await inside the Tekla Structural Designer call chain therefore resumes on the
        // thread pool and never needs the UI thread. The UI thread blocks only on the outer Task, whose
        // completion has no dependency on it, so the cycle is broken.
        //
        // GetAwaiter().GetResult() rather than .Result, so the original exception propagates instead of
        // being wrapped in an AggregateException - BHoM error reporting wants the real message.
        //
        // This relies on the API being an out of process gRPC client with no UI thread affinity, which
        // is true. Do not call RunSync from inside a Parallel.For or from a thread pool thread that is
        // itself blocked: that risks pool starvation, a different failure this does not defend against.
        internal static T RunSync<T>(Func<CancellationToken, Task<T>> taskFactory, int timeoutSeconds, string description)
        {
            if (taskFactory == null)
                throw new ArgumentNullException(nameof(taskFactory));

            bool bounded = timeoutSeconds > 0;

            // The token is passed into the API rather than only used to stop waiting, so that on timeout
            // the remote call is actually abandoned instead of being left running against the model.
            using (CancellationTokenSource source = bounded
                ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))
                : new CancellationTokenSource())
            {
                CancellationToken token = source.Token;
                Task<T> task = Task.Run(() => taskFactory(token), token);

                try
                {
                    return task.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (bounded && token.IsCancellationRequested)
                {
                    throw new TimeoutException(TimeoutMessage(timeoutSeconds, description));
                }
                catch (AggregateException e) when (bounded && token.IsCancellationRequested)
                {
                    throw new TimeoutException(TimeoutMessage(timeoutSeconds, description), e);
                }
            }
        }

        /***************************************************/
        /****            Private Methods                ****/
        /***************************************************/

        private static string TimeoutMessage(int timeoutSeconds, string description)
        {
            string what = string.IsNullOrWhiteSpace(description) ? "communicating with the model" : description;
            return "Tekla Structural Designer did not respond within " + timeoutSeconds + " seconds while " + what +
                   ". Check that Tekla Structural Designer is responsive and is not waiting on a dialogue.";
        }

        /***************************************************/
    }
}
