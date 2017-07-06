﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

	public class TwinConfigSource : BaseConfigSource
	{
		ISerde<ModuleSet> ModuleSetSerde { get; }

		ISerde<Diff> DiffSerde { get; }

		readonly IDeviceClient deviceClient;

		Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
		{
			try
			{
				Diff diff = this.DiffSerde.Deserialize(desiredProperties.ToJson());
				ModuleSet updated = this.ModuleSetSerde.Deserialize(desiredProperties.ToJson());
				this.OnModuleSetChanged(new ModuleSetChangedArgs(diff, updated));
				return Task.CompletedTask;
			}
			catch (Exception ex) when (!ex.IsFatal())
			{
				Events.DesiredPropertiesFailed(ex);
				this.OnFailed(ex);
				return Task.FromException(ex);
			}
		}

        TwinConfigSource(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, IConfiguration configuration)
            : base(configuration)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.ModuleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
            this.DiffSerde = Preconditions.CheckNotNull(diffSerde, nameof(diffSerde));
            Events.Created();
        }

		public override void Dispose()
		{
			this.deviceClient.Dispose();
		}

		public override async Task<ModuleSet> GetModuleSetAsync()
		{
			try
			{
				Twin twin = await this.deviceClient.GetTwinAsync();
				return this.ModuleSetSerde.Deserialize(twin.Properties.Desired.ToJson());
			}
			catch (Exception ex) when (!ex.IsFatal())
			{
				this.OnFailed(ex);
				throw;
			}
		}

		public override event EventHandler<ModuleSetChangedArgs> ModuleSetChanged;

		public void OnModuleSetChanged(ModuleSetChangedArgs updated)
		{
			this.ModuleSetChanged?.Invoke(this, updated);
		}

        public override event EventHandler<Exception> ModuleSetFailed;

        protected void OnFailed(Exception ex)
        {
            this.ModuleSetFailed?.Invoke(this, ex);
        }

		public static async Task<TwinConfigSource> Create(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, IConfiguration configuration)
		{
			var configSource = new TwinConfigSource(deviceClient, moduleSetSerde, diffSerde, configuration);
			try
			{
				await configSource.deviceClient.SetDesiredPropertyUpdateCallback(configSource.OnDesiredPropertyChanged, null);
			}
			catch (Exception e)
			{
				Events.DeviceClientException(e);
			}
			return configSource;
		}

		static class Events
		{
			static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();
			const int IdStart = AgentEventIds.TwinConfigSource;

			enum EventIds
			{
				Created = IdStart,
				DesiredPropertiesFailed,
				DeviceClientTimeout,
			}

			public static void Created()
			{
				Log.LogDebug((int)EventIds.Created, "TwinConfigSource Created");
			}

			public static void DesiredPropertiesFailed(Exception exception)
			{
				Log.LogError((int)EventIds.DesiredPropertiesFailed, exception, "TwinConfigSource failed processing desired configuration");
			}

			public static void DeviceClientException(Exception exception)
			{
				Log.LogError((int)EventIds.DeviceClientTimeout, exception, "TwinConfigSource got an exception from device client");
			}
		}
	}
}