// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using LodeRunner.Model;

namespace LodeRunner
{
    /// <summary>
    /// Shared state for the Timer Request Tasks
    /// </summary>
    internal class TimerRequestState : IDisposable
    {
        private static Semaphore loopController;
        private System.Timers.Timer timer;
        private bool disposedValue;

        /// <summary>
        /// gets or sets the server name
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// gets or sets the http client to use
        /// </summary>
        public HttpClient Client { get; set; }

        /// <summary>
        /// gets or sets the request index
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// gets or sets the max request index
        /// </summary>
        public int MaxIndex { get; set; }

        /// <summary>
        /// gets or sets the count
        /// </summary>
        public long Count { get; set; }

        /// <summary>
        /// gets or sets the duration in ms
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// gets or sets the number of errors
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use random requests
        /// </summary>
        public bool Random { get; set; }

        /// <summary>
        /// gets the lock object
        /// </summary>
        public object Lock { get; } = new object();

        /// <summary>
        /// gets or sets the lode runner object
        /// </summary>
        public ValidationTest Test { get; set; }

        /// <summary>
        /// gets or sets the current date time
        /// </summary>
        public DateTime CurrentLogTime { get; set; }

        /// <summary>
        /// gets or sets the cancellation token
        /// </summary>
        public CancellationToken Token { get; set; }

        public List<Request> RequestList { get; set; }

        public void Run(double interval, int maxConcurrent)
        {
            loopController = new Semaphore(maxConcurrent, maxConcurrent);

            timer = new System.Timers.Timer(interval)
            {
                Enabled = true,
            };
            timer.Elapsed += TimerEvent;
            timer.Start();
        }

        private async void TimerEvent(object sender, System.Timers.ElapsedEventArgs e)
        {
            int index = 0;

            // exit if cancelled
            if (Token.IsCancellationRequested)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
                Client.Dispose();
                Client = null;

                return;
            }

            // verify http client
            if (Client == null)
            {
                Console.WriteLine($"{ValidationTest.Now}\tError\tTimerState http client is null");
                return;
            }

            // get a semaphore slot - rate limit the requests
            if (!loopController.WaitOne(10))
            {
                return;
            }

            // lock the state for updates
            lock (Lock)
            {
                index = Index;

                // increment
                Index++;

                // keep the index in range
                if (Index >= MaxIndex)
                {
                    Index = 0;
                }
            }

            // randomize request index
            if (Random)
            {
                index = DateTime.UtcNow.Millisecond % MaxIndex;
            }

            Request req = RequestList[index];

            try
            {
                // Execute the request
                PerfLog p = await Test.ExecuteRequest(Client, Server, req).ConfigureAwait(false);

                lock (Lock)
                {
                    // increment
                    Count++;
                    Duration += p.Duration;
                }
            }
            catch (Exception ex)
            {
                // log and ignore any error
                Console.WriteLine($"{ValidationTest.Now}\tLodeRunnerException\t{ex.Message}");
            }

            // make sure to release the semaphore
            loopController.Release();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "grouping IDispose methods")]
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (timer != null)
                    {
                        timer.Stop();
                        timer.Dispose();
                    }
                }

                disposedValue = true;
            }
        }
    }
}
