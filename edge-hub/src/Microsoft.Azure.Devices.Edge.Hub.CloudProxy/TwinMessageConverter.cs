// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinMessageConverter : IMessageConverter<Twin>
    {
        public IMessage ToMessage(Twin sourceMessage)
        {
            var json = new StringBuilder();
            using (JsonWriter writer = new JsonTextWriter(new StringWriter(json)))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(TwinNames.Desired);
                writer.WriteRawValue(sourceMessage.Properties.Desired.ToJson());
                writer.WritePropertyName(TwinNames.Reported);
                writer.WriteRawValue(sourceMessage.Properties.Reported.ToJson());
                writer.WriteEndObject();
                writer.Flush();
            }

            byte[] body = Encoding.UTF8.GetBytes(json.ToString());
            return new CloudEdgeMessage(body, null, new Dictionary<string, string>
            {
                [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o")
            });
        }

        public Twin FromMessage(IMessage message)
        {
            throw new NotImplementedException();
        }
    }
}