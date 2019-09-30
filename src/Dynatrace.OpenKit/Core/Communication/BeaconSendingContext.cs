﻿//
// Copyright 2018-2019 Dynatrace LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dynatrace.OpenKit.API;
using Dynatrace.OpenKit.Core.Configuration;
using Dynatrace.OpenKit.Core.Objects;
using Dynatrace.OpenKit.Protocol;
using Dynatrace.OpenKit.Providers;
using Dynatrace.OpenKit.Util;

namespace Dynatrace.OpenKit.Core.Communication
{
    /// <summary>
    /// State context for beacon sending
    /// </summary>
    internal class BeaconSendingContext : IBeaconSendingContext
    {
        private readonly ILogger logger;

        public const int DefaultSleepTimeMilliseconds = 1000;

        private readonly IOpenKitConfiguration configuration;

        // container storing all sessions
        private readonly SynchronizedQueue<SessionWrapper> sessions = new SynchronizedQueue<SessionWrapper>();

        // reset event is set when init was done - which can either be success or failure
        private readonly ManualResetEvent resetEvent = new ManualResetEvent(false);

        // boolean indicating whether shutdown was requested or not (accessed by multiple threads)
        private volatile bool isShutdownRequested;

        // boolean indicating whether init was successful or not (accessed by multiple threads)
        private volatile bool initSucceeded;

        /// <summary>
        /// Constructor
        ///
        /// Current state is initialized to <see cref="Dynatrace.OpenKit.Core.Communication."/>
        ///
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        /// <param name="httpClientProvider"></param>
        /// <param name="timingProvider"></param>
        internal BeaconSendingContext(ILogger logger, IOpenKitConfiguration config, IHttpClientProvider httpClientProvider, ITimingProvider timingProvider)
        {
            this.logger = logger;
            configuration = config;
            HttpClientProvider = httpClientProvider;
            TimingProvider = timingProvider;

            // set current state to init state
            CurrentState = new BeaconSendingInitState();
        }

        IOpenKitConfiguration IBeaconSendingContext.Configuration => configuration;

        public IHttpClientProvider HttpClientProvider { get; }
        public ITimingProvider TimingProvider { get; }

        public AbstractBeaconSendingState CurrentState { get; internal set; }
        public AbstractBeaconSendingState NextState { get; set; }
        public long LastOpenSessionBeaconSendTime { get; set; }
        public long LastStatusCheckTime { get; set; }

        public bool IsInitialized => initSucceeded;

        public bool IsShutdownRequested
        {
            get => isShutdownRequested;
            private set => isShutdownRequested = value;
        }

        public long CurrentTimestamp => TimingProvider.ProvideTimestampInMilliseconds();
        public int SendInterval => configuration.SendInterval;
        public bool IsCaptureOn => configuration.IsCaptureOn;
        public bool IsInTerminalState => CurrentState.IsTerminalState;

        public void ExecuteCurrentState()
        {
            NextState = null;
            CurrentState.Execute(this);
            if (NextState != null && ! CurrentState.IsTerminalState) // CurrentState.Execute(...) can trigger state changes
            {
                if(logger.IsInfoEnabled)
                {
                    logger.Info("BeaconSendingContext State change from '" + CurrentState + "' to '" + NextState + "'");
                }
                CurrentState = NextState;
                NextState = null;
            }
        }

        public void RequestShutdown()
        {
            IsShutdownRequested = true;
        }

        public bool WaitForInit()
        {
            resetEvent.WaitOne();
            return initSucceeded;
        }

        public bool WaitForInit(int timeoutMillis)
        {
            resetEvent.WaitOne(TimeSpan.FromMilliseconds(timeoutMillis));
            return initSucceeded;
        }

        public void InitCompleted(bool success)
        {
            initSucceeded = success;
            resetEvent.Set();
        }

        public IHttpClient GetHttpClient()
        {
            return HttpClientProvider.CreateClient(configuration.HttpClientConfig);
        }

        public void Sleep()
        {
            Sleep(DefaultSleepTimeMilliseconds);
        }

        public void Sleep(int millis)
        {
#if !NETCOREAPP1_0 || !NETCOREAPP1_1
            TimingProvider.Sleep(millis);
#else
            // in order to avoid long sleeps (netcore1.0 doesn't provide ThreadInterruptException for sleep)
            const int sleepTimePerCycle = DEFAULT_SLEEP_TIME_MILLISECONDS;
            while (millis > 0)
            {
                TimingProvider.Sleep(Math.Min(sleepTimePerCycle, millis));
                millis -= sleepTimePerCycle;
                if (isShutdownRequested)
                {
                    break;
                }
            }
#endif
        }

        public void DisableCapture()
        {
            configuration.DisableCapture();
            ClearAllSessionData();
        }

        public void HandleStatusResponse(StatusResponse statusResponse)
        {
            configuration.UpdateSettings(statusResponse);

            if (!IsCaptureOn)
            {
                // capture was turned off
                ClearAllSessionData();
            }
        }

        /// <summary>
        /// Clear captured data from all sessions.
        /// </summary>
        private void ClearAllSessionData()
        {
            sessions.ToList().ForEach(wrapper =>
            {
                wrapper.ClearCapturedData();
                if (wrapper.IsSessionFinished)
                {
                    sessions.Remove(wrapper);
                }
            });
        }

        /// <summary>
        /// <seealso cref="IBeaconSendingContext.StartSession(ISessionInternals)"/>
        /// </summary>
        public void StartSession(ISessionInternals session)
        {
            sessions.Put(new SessionWrapper(session));
        }

        /// <summary>
        /// <seealso cref="IBeaconSendingContext.FinishSession(ISessionInternals)"/>
        /// </summary>
        public void FinishSession(ISessionInternals session)
        {
            var wrappedSession = sessions.ToList().FirstOrDefault(wrapper => wrapper.Session == session);
            if (wrappedSession != null)
            {
                wrappedSession.FinishSession();
            }
        }

        public List<SessionWrapper> NewSessions => sessions.ToList().Where(wrapper => !wrapper.IsBeaconConfigurationSet).ToList();

        public List<SessionWrapper> OpenAndConfiguredSessions => sessions.ToList()
            .Where(wrapper => wrapper.IsBeaconConfigurationSet && !wrapper.IsSessionFinished).ToList();

        public List<SessionWrapper> FinishedAndConfiguredSessions => sessions.ToList()
            .Where(wrapper => wrapper.IsBeaconConfigurationSet && wrapper.IsSessionFinished).ToList();

        public bool RemoveSession(SessionWrapper sessionWrapper)
        {
            return sessions.Remove(sessionWrapper);
        }
    }
}