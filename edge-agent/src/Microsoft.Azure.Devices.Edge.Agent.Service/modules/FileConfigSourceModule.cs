﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class FileConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly string configFilename;
        readonly IConfiguration configuration;

        public FileConfigSourceModule(string configFilename, IConfiguration configuration)
        {
            this.configFilename = configFilename;
            this.configuration = configuration;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ISerde<ModuleSet>
            builder.Register(c => new ModuleSetSerde(
                    new Dictionary<string, Type>
                    {
                        { DockerType, typeof(DockerModule) }
                    }
                ))
                .As<ISerde<ModuleSet>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var dockerFactory = new DockerCommandFactory(
                            c.Resolve<IDockerClient>(),
                            c.Resolve<DockerLoggingConfig>(),
                            await c.Resolve<Task<IConfigSource>>());
                        return new LoggingCommandFactory(dockerFactory, c.Resolve<ILoggerFactory>());
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                async c =>
                {
                    IConfigSource config = await FileConfigSource.Create(
                        this.configFilename,
                        c.Resolve<ISerde<ModuleSet>>(),
                        this.configuration);
                    return config;
                })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}