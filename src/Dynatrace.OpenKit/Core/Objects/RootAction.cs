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

using Dynatrace.OpenKit.API;
using Dynatrace.OpenKit.Protocol;

namespace Dynatrace.OpenKit.Core.Objects
{
    /// <summary>
    /// Actual implementation of the RootAction interface.
    /// </summary>
    public class RootAction : BaseAction, IRootAction
    {
        #region constructors

        /// <summary>
        /// Constructor for creating a new root action instance
        /// </summary>
        /// <param name="logger">the logger used to log information</param>
        /// <param name="parentSession">the session to which this root action belongs to</param>
        /// <param name="name">the name of this root action</param>
        /// <param name="beacon">the beacon for retrieving certain data and for sending data</param>
        public RootAction(
            ILogger logger,
            Session parentSession,
            string name,
            Beacon beacon)
            : base(logger, parentSession, name, beacon)
        {
        }

        #endregion

        #region IRootAction interface implementations

        public IAction EnterAction(string actionName)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"{this} EnterAction(\"{actionName}\")");
            }
            if (string.IsNullOrEmpty(actionName))
            {
                Logger.Warn($"{this} EnterAction: actionName must not be null or empty");
                return new NullAction(this);
            }

            lock (LockObject)
            {
                if (!IsActionLeft)
                {
                    var childAction = new LeafAction(Logger, this, actionName, Beacon);
                    StoreChildInList(childAction);

                    return childAction;
                }
            }

            return new NullAction(this);
        }

        #endregion

        internal override IAction ParentAction => null; // Root actions do not have a parent action

        public override string ToString()
        {
            return $"{GetType().Name} [sn={Beacon.SessionNumber}, id={Id}, name={Name}]";
        }
    }
}
