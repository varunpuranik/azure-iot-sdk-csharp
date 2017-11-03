// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class EdgeAgentDockerRuntimeModuleTest
    {
        [Fact]
        [Unit]
        public void TestJsonSerialize()
        {
            // Arrange
            var module = new EdgeAgentDockerRuntimeModule(new DockerConfig("booyah"), ModuleStatus.Running, new ConfigurationInfo("bing"));

            // Act
            JToken json = JToken.Parse(JsonConvert.SerializeObject(module));

            // Assert
            var expected = JToken.FromObject(new
            {
                runtimeStatus = "running",
                type = "docker",
                settings = new
                {
                    image = "booyah",
                    createOptions = "{}"
                },
                configuration = new
                {
                    id = "bing"
                }
            });

            Assert.True(JToken.DeepEquals(expected, json));
        }

        [Fact]
        [Unit]
        public void TestJsonDeserialize()
        {
            // Arrange
            string json = JsonConvert.SerializeObject(new
            {
                type = "docker",
                runtimeStatus = "running",
                settings = new
                {
                    image = "Unknown",
                    createOptions = "{}"
                }
            });

            // Act
            var edgeAgent = JsonConvert.DeserializeObject<EdgeAgentDockerRuntimeModule>(json);

            // Assert
            Assert.Equal("docker", edgeAgent.Type);
            Assert.Equal(ModuleStatus.Running, edgeAgent.RuntimeStatus);
            Assert.Equal("Unknown", edgeAgent.Config.Image);
        }

        [Fact]
        [Unit]
        public void TestJsonDeserialize2()
        {
            // Arrange
            string json = JsonConvert.SerializeObject(new
            {
                type = "docker",
                runtimeStatus = "running",
                settings = new
                {
                    image = "Unknown",
                    createOptions = "{}"
                },
                configuration = new
                {
                    id = "bing"
                }
            });

            // Act
            var edgeAgent = JsonConvert.DeserializeObject<EdgeAgentDockerRuntimeModule>(json);

            // Assert
            Assert.Equal("docker", edgeAgent.Type);
            Assert.Equal(ModuleStatus.Running, edgeAgent.RuntimeStatus);
            Assert.Equal("Unknown", edgeAgent.Config.Image);
            Assert.Equal("bing", edgeAgent.ConfigurationInfo.Id);
        }
    }
}