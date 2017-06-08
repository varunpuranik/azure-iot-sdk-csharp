﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class TwinConfigSourceTest
    {
        readonly ModuleSetSerde moduleSetSerde;
        readonly DiffSerde diffSerde;
        DesiredPropertyUpdateCallback desiredPropertyCallback;

        public TwinConfigSourceTest()
        {
            var serializerInputTable = new Dictionary<string, Type> { { "Test", typeof(TestModule) } };
            this.moduleSetSerde = new ModuleSetSerde(serializerInputTable);
            this.diffSerde = new DiffSerde(serializerInputTable);
        }

        [Fact]
        [Unit]
        public async void CreateInvalidInputs()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();

            // Act
            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => TwinConfigSource.Create(null, this.moduleSetSerde, this.diffSerde));
            await Assert.ThrowsAsync<ArgumentNullException>(() => TwinConfigSource.Create(deviceClient.Object, null, this.diffSerde));
            await Assert.ThrowsAsync<ArgumentNullException>(() => TwinConfigSource.Create(deviceClient.Object, this.moduleSetSerde, null));
        }

        [Fact]
        [Unit]
        public async void CreateSuccess()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();

            // Act
            using (TwinConfigSource twinConfig = await TwinConfigSource.Create(deviceClient.Object, this.moduleSetSerde, this.diffSerde))
            {
                // Assert
                Assert.NotNull(twinConfig);
            }
        }

        [Fact]
        [Unit]
        public async void GetConfigAsyncSuccess()
        {
            // Arrange
            var twin = new Twin();
            var config1 = new TestConfig("image1");
            IModule module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, config1);
            ModuleSet moduleSet1 = ModuleSet.Create(module1);

            var desiredreportedProperties = new TwinCollection();
            desiredreportedProperties["modules"] = moduleSet1.Modules;
            desiredreportedProperties["$version"] = 123;
            twin.Properties.Desired = desiredreportedProperties;

            var deviceClient = new Mock<IDeviceClient>();
            deviceClient.Setup(t => t.GetTwinAsync()).ReturnsAsync(twin);

            using (TwinConfigSource twinConfig = await TwinConfigSource.Create(deviceClient.Object, this.moduleSetSerde, this.diffSerde))
            {
                // Act
                ModuleSet startingSet = await twinConfig.GetConfigAsync();

                IModule returnedModule1 = startingSet.Modules["mod1"];
                // Assert  
                Assert.True(module1.Equals(returnedModule1));
            }
        }

        [Fact]
        [Unit]
        public async void GetConfigAsyncThrows()
        {
            // Arrange
            var twin = new Twin();
            var config1 = new TestConfig("image1");
            IModule module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, config1);
            ModuleSet moduleSet1 = ModuleSet.Create(module1);

            var desiredreportedProperties = new TwinCollection();
            desiredreportedProperties["modules"] = moduleSet1.Modules;
            desiredreportedProperties["$version"] = 123;
            twin.Properties.Desired = desiredreportedProperties;

            var deviceClient = new Mock<IDeviceClient>();
            deviceClient.Setup(t => t.GetTwinAsync()).ReturnsAsync(twin);

            var moduleSetSerdeMocked = new Mock<ISerde<ModuleSet>>();
            moduleSetSerdeMocked.Setup(t => t.Deserialize(It.IsAny<string>())).Throws(new Exception("Any Exception"));

            using (TwinConfigSource twinConfig = await TwinConfigSource.Create(deviceClient.Object, moduleSetSerdeMocked.Object, this.diffSerde))
            {
                bool failEventCalled = false;

                twinConfig.Failed += (sender, ex) =>
                {
                    failEventCalled = true;
                };

                // Act
                // Assert  
                await Assert.ThrowsAsync<Exception>(() => twinConfig.GetConfigAsync());
                Assert.True(failEventCalled);
            }
        }


        [Fact]
        [Unit]
        public async void OnDesiredPropertyChangedSuccess()
        {
            // Arrange
            var twin = new Twin();
            var config1 = new TestConfig("image1");
            IModule module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, config1);
            ModuleSet moduleSet1 = ModuleSet.Create(module1);
            var moduleWithRemove = new Dictionary<string, IModule>()
            {
                {
                    "Module2",
                    null
                }
            };


            var desiredreportedProperties = new TwinCollection();
            desiredreportedProperties["modules"] = moduleSet1.Modules;
            desiredreportedProperties["$version"] = 123;
            twin.Properties.Desired = desiredreportedProperties;

            var desiredPropertiesWithRemove = new TwinCollection();
            desiredPropertiesWithRemove["modules"] = moduleWithRemove;
            desiredPropertiesWithRemove["$version"] = 123;

            var deviceClient = new Mock<IDeviceClient>();
            deviceClient.Setup(t => t.GetTwinAsync()).ReturnsAsync(twin);

            deviceClient
                .Setup(t => t.SetDesiredPropertyUpdateCallback(It.IsAny<DesiredPropertyUpdateCallback>(), It.IsAny<object>()))
                .Callback<DesiredPropertyUpdateCallback, object>(
                    (i, j) =>
                    {
                        this.desiredPropertyCallback = i;
                    })
                .Returns(Task.FromResult(0));

            using (TwinConfigSource twinConfig = await TwinConfigSource.Create(deviceClient.Object, this.moduleSetSerde, this.diffSerde))
            {
                // Act
                bool changeEventCalled = false;
                Diff receivedDiff = null;
                twinConfig.Changed += (sender, diff) =>
                {
                    changeEventCalled = true;
                    receivedDiff = diff;
                };

                await this.desiredPropertyCallback(desiredreportedProperties, null);

                // Assert
                Assert.True(changeEventCalled);
                Assert.False(receivedDiff.IsEmpty);
                Assert.True(receivedDiff.Updated.Count == 1);
                Assert.True(receivedDiff.Removed.Count == 0);

                // Arrange
                changeEventCalled = false;
                receivedDiff = null;

                // Act
                await this.desiredPropertyCallback(desiredPropertiesWithRemove, null);

                // Assert
                Assert.True(changeEventCalled);
                Assert.False(receivedDiff.IsEmpty);
                Assert.True(receivedDiff.Updated.Count == 0);
                Assert.True(receivedDiff.Removed.Count == 1);
            }
        }


    }
}