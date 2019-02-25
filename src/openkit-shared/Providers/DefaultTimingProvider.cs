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
using System.Threading;

namespace Dynatrace.OpenKit.Providers
{
    public class DefaultTimingProvider : ITimingProvider
    {
        private static readonly DateTime jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public virtual long ProvideTimestampInMilliseconds()
        {
            return (long)(DateTime.UtcNow - jan1st1970).TotalMilliseconds;
        }

        public virtual void Sleep(int milliseconds)
        {
#if WINDOWS_UWP || NETPCL4_5
            System.Threading.Tasks.Task.Delay(milliseconds).Wait();
#else
            Thread.Sleep(milliseconds);
#endif
        }
    }
}
