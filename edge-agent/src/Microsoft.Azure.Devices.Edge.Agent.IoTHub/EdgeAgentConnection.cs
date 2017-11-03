// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    public class EdgeAgentConnection : IEdgeAgentConnection
    {
        internal const string ExpectedSchemaVersion = "1.0";
        readonly IDeviceClient deviceClient;
        readonly AsyncLock twinLock = new AsyncLock();
        readonly ISerde<DeploymentConfig> desiredPropertiesSerDe;
        TwinCollection desiredProperties;
        Option<TwinCollection> reportedProperties;
        Option<DeploymentConfigInfo> deploymentConfigInfo;

        EdgeAgentConnection(IDeviceClient deviceClient, ISerde<DeploymentConfig> desiredPropertiesSerDe)
        {
            this.deviceClient = deviceClient;
            this.desiredPropertiesSerDe = desiredPropertiesSerDe;
            this.deploymentConfigInfo = Option.None<DeploymentConfigInfo>();
            this.reportedProperties = Option.None<TwinCollection>();
        }

        public static async Task<EdgeAgentConnection> Create(IDeviceClient deviceClient, ISerde<DeploymentConfig> desiredPropertiesSerDe)
        {
            var edgeAgentConnection = new EdgeAgentConnection(deviceClient, desiredPropertiesSerDe);
            deviceClient.SetConnectionStatusChangedHandler(edgeAgentConnection.OnConnectionStatusChanged);
            await deviceClient.SetDesiredPropertyUpdateCallback(edgeAgentConnection.OnDesiredPropertiesUpdated, null);
            Events.Created();
            return edgeAgentConnection;
        }

        async void OnConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            try
            {
                Events.ConnectionStatusChanged(status, reason);
                if (status == ConnectionStatus.Connected)
                {
                    using (await this.twinLock.LockAsync())
                    {
                        await this.RefreshTwinAsync();
                    }
                }
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Events.ConnectionStatusChangedHandlingError(ex);
            }
        }

        async Task OnDesiredPropertiesUpdated(TwinCollection desiredPropertiesPatch, object userContext)
        {
            Events.DesiredPropertiesUpdated();
            using (await this.twinLock.LockAsync())
            {
                if (this.desiredProperties == null || this.desiredProperties.Version + 1 != desiredPropertiesPatch.Version)
                {
                    await this.RefreshTwinAsync();
                }
                else
                {
                    await this.ApplyPatchAsync(desiredPropertiesPatch);
                }
            }
        }

        async Task RefreshTwinAsync()
        {
            try
            {
                Twin twin = await this.deviceClient.GetTwinAsync();
                this.desiredProperties = twin.Properties.Desired;
                this.reportedProperties = Option.Some(twin.Properties.Reported);
                await this.UpdateDeploymentConfig();
                Events.TwinRefreshSuccess();
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties?.Version ?? 0, ex));
                Events.TwinRefreshError(ex);
            }
        }

        // This method updates local state and should be called only after acquiring twinLock
        async Task ApplyPatchAsync(TwinCollection patch)
        {
            try
            {
                string mergedJson = JsonEx.Merge(this.desiredProperties, patch, true);
                this.desiredProperties = new TwinCollection(mergedJson);
                await this.UpdateDeploymentConfig();
                Events.DesiredPropertiesPatchApplied();
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties?.Version ?? 0, ex));
                Events.DesiredPropertiesPatchFailed(ex);
                // Update reported properties with last desired status
            }
        }

        Task UpdateDeploymentConfig()
        {
            DeploymentConfig deploymentConfig;

            try
            {
                string desiredPropertiesJson = this.desiredProperties.ToJson();
                deploymentConfig = this.desiredPropertiesSerDe.Deserialize(desiredPropertiesJson);
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Events.ErrorUpdatingDeploymentConfig(ex);
                // TODO: Localize this error?
                throw new ConfigFormatException("Agent configuration format is invalid.", ex);
            }

            try
            {
                // Do any validation on deploymentConfig if necessary
                if (!deploymentConfig.SchemaVersion.Equals(ExpectedSchemaVersion, StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Localize this error?
                    throw new InvalidOperationException($"Received schema with version {deploymentConfig.SchemaVersion}, but only version {ExpectedSchemaVersion} is supported.");
                }
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties.Version, deploymentConfig));
                Events.UpdatedDeploymentConfig();
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Events.ErrorUpdatingDeploymentConfig(ex);
                throw;
            }

            return Task.CompletedTask;
        }

        public Task<Option<DeploymentConfigInfo>> GetDeploymentConfigInfoAsync() => Task.FromResult(this.deploymentConfigInfo);

        public Option<TwinCollection> ReportedProperties => this.reportedProperties;

        public void Dispose() => this.deviceClient?.Dispose();

        public Task UpdateReportedPropertiesAsync(TwinCollection patch) => this.deviceClient.UpdateReportedPropertiesAsync(patch);

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeAgentConnection>();
            const int IdStart = AgentEventIds.EdgeAgentConnection;

            enum EventIds
            {
                Created = IdStart,
                DesiredPropertiesFailed,
                ConnectionStatusChanged,
                DesiredPropertiesPatchApplied,
                DesiredPropertiesUpdated,
                DeploymentConfigUpdated,
                ErrorUpdatingDeploymentConfig,
                ErrorRefreshingTwin,
                TwinRefreshSuccess,
                ErrorHandlingConnectionChangeEvent
            }

            public static void Created()
            {
                Log.LogDebug((int)EventIds.Created, "EdgeAgentConnection Created");
            }

            public static void DesiredPropertiesPatchFailed(Exception exception)
            {
                Log.LogError((int)EventIds.DesiredPropertiesFailed, exception, "EdgeAgentConnection failed to process desired properties update patch");
            }

            public static void ConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Log.LogInformation((int)EventIds.ConnectionStatusChanged, $"Connection status changed from to {status} with reason {reason}");
            }

            internal static void DesiredPropertiesUpdated()
            {
                Log.LogDebug((int)EventIds.DesiredPropertiesUpdated, "Edge Agent desired properties updated callback invoked.");
            }

            internal static void DesiredPropertiesPatchApplied()
            {
                Log.LogDebug((int)EventIds.DesiredPropertiesPatchApplied, "Edge Agent desired properties patch applied successfully.");
            }

            internal static void ConnectionStatusChangedHandlingError(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorHandlingConnectionChangeEvent, ex, "Edge agent connection error handing connection change callback.");
            }

            internal static void TwinRefreshSuccess()
            {
                Log.LogDebug((int)EventIds.TwinRefreshSuccess, "Updated Edge agent configuration from twin.");
            }

            internal static void TwinRefreshError(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorRefreshingTwin, ex, "Error refreshing Edge agent configuration from twin.");
            }

            internal static void ErrorUpdatingDeploymentConfig(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorUpdatingDeploymentConfig, ex, "Error updating deployment config from Edge agent desired properties.");
            }

            internal static void UpdatedDeploymentConfig()
            {
                Log.LogDebug((int)EventIds.DeploymentConfigUpdated, "EdgeAgentConnection updated deployment config from desired properties.");
            }
        }
    }
}