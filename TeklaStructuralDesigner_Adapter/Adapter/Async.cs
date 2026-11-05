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
    // The Tekla Structural Designer API is Task based and the BHoM adapter is synchronous; every API
    // call goes through here. Internal, as a Func<Task<T>> has no meaningful UI component.
    internal static class Async
    {
        /***************************************************/
        /**** Public Methods                            ****/
        /***************************************************/

        // Task.Run moves the call onto the thread pool, where there is no SynchronizationContext, so
        // blocking a UI thread (Rhino, Excel) on it cannot deadlock. GetResult rethrows the original
        // exception rather than an AggregateException. The token is passed to the API so a timed out
        // call is abandoned rather than left running.
        public static T RunSync<T>(Func<CancellationToken, Task<T>> taskFactory, int timeoutSeconds, string description)
        {
            if (taskFactory == null)
                throw new ArgumentNullException(nameof(taskFactory));

            bool bounded = timeoutSeconds > 0;

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

        // RunSync returning false and the failure message instead of throwing.
        public static bool TryRunSync<T>(Func<CancellationToken, Task<T>> taskFactory, int timeoutSeconds, string description, out T result, out string error)
        {
            try
            {
                result = RunSync(taskFactory, timeoutSeconds, description);
                error = null;
                return true;
            }
            catch (Exception e)
            {
                result = default(T);
                error = e.Message;
                return false;
            }
        }

        /***************************************************/

        // RunSync recording a failure, with its consequence, as an error or a warning instead of throwing.
        public static bool TryRunSync<T>(Func<CancellationToken, Task<T>> taskFactory, int timeoutSeconds, string description, string consequence, out T result, bool isError = false)
        {
            string error;
            if (TryRunSync(taskFactory, timeoutSeconds, description, out result, out error))
                return true;

            string message = "Tekla Structural Designer failed while " + description + ", so " + consequence + ". " + error;
            if (isError)
                Engine.Base.Compute.RecordError(message);
            else
                Engine.Base.Compute.RecordWarning(message);

            return false;
        }

        /***************************************************/
        /**** Private Methods                           ****/
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
