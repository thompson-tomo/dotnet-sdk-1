﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.IsolatedStorage;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Statsig.Client.Storage;
using Statsig.Network;

namespace Statsig.Client
{
    public class ClientDriver : IDisposable
    {
        const string gatesStoreKey = "statsig::featureGates";
        const string configsStoreKey = "statsig::configs";

        readonly ConnectionOptions _options;
        internal readonly string _clientKey;
        bool _disposed;
        RequestDispatcher _requestDispatcher;
        EventLogger _eventLogger;
        StatsigUser _user;
        Dictionary<string, FeatureGate> _gates;
        Dictionary<string, DynamicConfig> _configs;
        Dictionary<string, string> _statsigMetadata;

        public ClientDriver(string clientKey, ConnectionOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(clientKey))
            {
                throw new ArgumentException("clientKey cannot be empty.", "clientKey");
            }
            if (!clientKey.StartsWith("client-") && !clientKey.StartsWith("test-"))
            {
                throw new ArgumentException("Invalid key provided. Please check your Statsig console to get the right server key.", "serverSecret");
            }
            if (options == null)
            {
                options = new ConnectionOptions();
            }
            _clientKey = clientKey;
            _options = options;

            _requestDispatcher = new RequestDispatcher(_clientKey, _options.ApiUrlBase);
            _eventLogger = new EventLogger(
                _requestDispatcher,
                Constants.CLIENT_MAX_LOGGER_QUEUE_LENGTH,
                Constants.CLIENT_MAX_LOGGER_WAIT_TIME_IN_SEC
            );

            _gates = PersistentStore.GetValue(gatesStoreKey, new Dictionary<string, FeatureGate>());
            _configs = PersistentStore.GetValue(configsStoreKey, new Dictionary<string, DynamicConfig>());
        }

        public async Task Initialize(StatsigUser user)
        {
            if (user == null)
            {
                user = new StatsigUser();
            }

            _user = user;
            var response = await _requestDispatcher.Fetch(
                "initialize",
                new Dictionary<string, object>
                {
                    ["user"] = _user,
                    ["statsigMetadata"] = GetStatsigMetadata(),
                }
            );

            if (response == null)
            {
                return;
            }

            ParseInitResponse(response);
            PersistentStore.SetValue(gatesStoreKey, _gates);
            PersistentStore.SetValue(configsStoreKey, _configs);
        }

        public void Shutdown()
        {
            ((IDisposable)this).Dispose();
        }

        public bool CheckGate(string gateName)
        {
            var hashedName = GetNameHash(gateName);
            FeatureGate gate;
            if (!_gates.TryGetValue(hashedName, out gate))
            {
                _gates.TryGetValue(gateName, out gate);
            }

            if (gate != null) {
                _eventLogger.Enqueue(EventLog.CreateGateExposureLog(_user, gateName, gate.Value.ToString(), gate.RuleID));
            }

            
            return value;
        }

        public DynamicConfig GetConfig(string configName)
        {
            var hashedName = GetNameHash(configName);
            DynamicConfig value;
            if (!_configs.TryGetValue(hashedName, out value))
            {
                if (!_configs.TryGetValue(configName, out value))
                {
                    value = new DynamicConfig(configName);
                }
            }

            _eventLogger.Enqueue(EventLog.CreateConfigExposureLog(_user, configName, null));
            return value;
        }

        public async Task UpdateUser(StatsigUser newUser)
        {
            _statsigMetadata = null;
            await Initialize(newUser);
        }

        public void LogEvent(
            string eventName,
            string value = null,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            if (value != null && value.Length > Constants.MAX_SCALAR_LENGTH)
            {
                value = value.Substring(0, Constants.MAX_SCALAR_LENGTH);
            }

            LogEventHelper(eventName, value, metadata);
        }

        public void LogEvent(
            string eventName,
            int value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            LogEventHelper(eventName, value, metadata);
        }

        public void LogEvent(
            string eventName,
            double value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            LogEventHelper(eventName, value, metadata);
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ClientDriver");
            }

            _eventLogger.ForceFlush();
            _disposed = true;
        }

        #region Private helpers

        void LogEventHelper(
            string eventName,
            object value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            if (eventName == null)
            {
                return;
            }

            if (eventName.Length > Constants.MAX_SCALAR_LENGTH)
            {
                eventName = eventName.Substring(0, Constants.MAX_SCALAR_LENGTH);
            }

            var eventLog = new EventLog
            {
                EventName = eventName,
                Value = value,
                User = _user,
                Metadata = EventLog.TrimMetadataAsNeeded(metadata),
            };

            _eventLogger.Enqueue(eventLog);
        }

        string GetNameHash(string name)
        {
            using (var sha = SHA256.Create())
            {
                var buffer = sha.ComputeHash(Encoding.UTF8.GetBytes(name));
                return Convert.ToBase64String(buffer);
            }
        }

        void ParseInitResponse(IReadOnlyDictionary<string, JToken> response)
        { 
            try
            {
                JToken objVal;
                if (response.TryGetValue("featureGates", out objVal))
                {
                    _gates = objVal.ToObject<Dictionary<string, FeatureGate>>();
                }
            }
            catch
            {
                // Gates parsing failed.  TODO: Log this
            }

            try
            {
                JToken objVal;
                if (response.TryGetValue("configs", out objVal))
                {
                    var configMap = objVal.ToObject<Dictionary<string, object>>();
                    foreach (var kv in configMap)
                    {
                        _configs[kv.Key] = DynamicConfig.FromJObject(kv.Key, kv.Value as JObject);
                    }
                }
            }
            catch
            {
                // Configs parsing failed.  TODO: Log this
            }
        }

        IReadOnlyDictionary<string, string> GetStatsigMetadata()
        {
            if (_statsigMetadata == null)
            {
                string systemName = "unknown";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    systemName = "Mac OS";
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    systemName = "Windows";
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    systemName = "Linux";
                } 
                _statsigMetadata = new Dictionary<string, string>
                {
                    ["sessionID"] = Guid.NewGuid().ToString(),
                    ["stableID"] = PersistentStore.StableID,
                    ["locale"] = CultureInfo.CurrentUICulture.Name,
                    ["appVersion"] = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                    ["systemVersion"] = Environment.OSVersion.Version.ToString(),
                    ["systemName"] = systemName,
                };
            }
            return _statsigMetadata;
        }

        #endregion
    }
}
