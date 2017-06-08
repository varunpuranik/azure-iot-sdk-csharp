﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test.Commands
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Binding = Microsoft.Azure.Devices.Edge.Agent.Docker.PortBinding;

    [ExcludeFromCodeCoverage]
    [Collection("Docker")]
    public class CreateCommandTest
    {
        static readonly IDockerClient Client = DockerHelper.Client;

        [Fact]
        [Integration]
        public async Task SmokeTest()
        {
            const string Image = "hello-world";
            const string Tag = "latest";
            const string Name = "test-helloworld";

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await Client.PullImageAsync(Image, Tag, cts.Token);

                    var config = new DockerConfig(Image, Tag, new[] { new Binding("80", "8080", PortBindingType.Tcp) } );
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, config);
                    var command = new CreateCommand(Client, module);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created
                    ContainerInspectResponse container = await Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse("version", "missing"));
                    Assert.Equal("8080/tcp", container.HostConfig.PortBindings.First().Key);
                }
            }
            finally
            {
                await Client.CleanupContainerAsync(Name, Image);
            }
        }

        [Fact]
        [Integration]
        public async Task TestUdpModuleConfig()
        {
            const string Image = "hello-world";
            const string Tag = "latest";
            const string Name = "test-helloworld";

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await Client.PullImageAsync(Image, Tag, cts.Token);

                    var config = new DockerConfig(Image, Tag, new[] { new Binding("42", "42", PortBindingType.Udp)});
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, config);
                    var command = new CreateCommand(Client, module);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created
                    ContainerInspectResponse container = await Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse("version", "missing"));
                    Assert.Equal(1, container.HostConfig.PortBindings.Count);
                }
            }
            finally
            {
                await Client.CleanupContainerAsync(Name, Image);
            }
        }
    }
}