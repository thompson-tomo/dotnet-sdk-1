﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Statsig
{
    public class EventLog
    {
        [JsonProperty("eventName")]
        public string EventName { get; internal set; }
        [JsonProperty("user")]
        public StatsigUser User { get; internal set; }
        [JsonProperty("metadata")]
        public IReadOnlyDictionary<string, string> Metadata { get; internal set; }
        [JsonProperty("value")]
        public object Value { get; internal set; }

        [JsonIgnore]
        internal bool IsErrorLog { get; set; }
        [JsonIgnore]
        internal string ErrorKey { get; set; }

        internal EventLog()
        {
        }

        internal static EventLog CreateGateExposureLog(
            StatsigUser user,
            string gateName,
            string gateValue)
        {
            return new EventLog
            {
                User = user,
                EventName = Constants.GATE_EXPOSURE_EVENT,
                Metadata = new Dictionary<string, string>
                {
                    ["gate"] = gateName,
                    ["gateValue"] = gateValue,
                }
            };
        }

        internal static EventLog CreateConfigExposureLog(
            StatsigUser user,
            string configName,
            string groupName)
        {
            return new EventLog
            {
                User = user,
                EventName = Constants.CONFIG_EXPOSURE_EVENT,
                Metadata = new Dictionary<string, string>
                {
                    ["config"] = configName,
                    ["configGroup"] = groupName,
                }
            };
        }

        internal static EventLog CreateErrorLog(string eventName, string errorMessage = null)
        {
            if (errorMessage == null)
            {
                errorMessage = eventName;
            }

            return new EventLog
            {
                EventName = eventName,
                Metadata = new Dictionary<string, string>
                {
                    ["error"] = errorMessage
                },
                IsErrorLog = true,
                ErrorKey = errorMessage,
            };
        }
    }
}
