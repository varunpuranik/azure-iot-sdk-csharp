﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Serde
{
	using System;
	using System.Collections.Generic;
	using Microsoft.Azure.Devices.Edge.Util;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Newtonsoft.Json.Serialization;

	public class ModuleSetSerde : ISerde<ModuleSet>
	{
		readonly JsonSerializerSettings jsonSerializerSettings;

		public ModuleSetSerde(IDictionary<string, Type> deserializerTypes)
		{
			IDictionary<string, Type> converters = new Dictionary<string, Type>(
				Preconditions.CheckNotNull(deserializerTypes, nameof(deserializerTypes)),
				StringComparer.OrdinalIgnoreCase
			);

			this.jsonSerializerSettings = new JsonSerializerSettings
			{
				ContractResolver = new CamelCasePropertyNamesContractResolver(),
				Converters = new List<JsonConverter>
				{
					new ModuleJsonConverter(converters)
				},
			};
		}

		public string Serialize(ModuleSet moduleSet)
		{
			return JsonConvert.SerializeObject(moduleSet, this.jsonSerializerSettings);
		}

		public ModuleSet Deserialize(string json) => this.Deserialize<ModuleSet>(json);

		public T Deserialize<T>(string json) where T : ModuleSet
		{
			try
			{
				return JsonConvert.DeserializeObject<T>(json, this.jsonSerializerSettings);
			}
			catch (ArgumentNullException e)
			{
				throw new JsonSerializationException(e.Message);
			}
		}

		class ModuleJsonConverter : JsonConverter
		{
			readonly IDictionary<string, Type> converters;

			readonly ModuleSerde moduleSerde = ModuleSerde.Instance;

			public ModuleJsonConverter(IDictionary<string, System.Type> deserializerTypes)
			{
				this.converters = new Dictionary<string, Type>(deserializerTypes, StringComparer.OrdinalIgnoreCase);
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotSupportedException();
			}

			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				// The null check is required to gracefully handle a null object
				if (reader.TokenType == JsonToken.Null)
				{
					return null;
				}

				JObject obj = JObject.Load(reader);
				var converterType = obj.Get<JToken>("type");

				if (!this.converters.TryGetValue(converterType.Value<string>(), out Type serializeType))
				{
					throw new JsonSerializationException($"Could not find right converter given a type {converterType.Value<string>()}");
				}

				return this.moduleSerde.Deserialize(obj.ToString(), serializeType);
			}

			public override bool CanConvert(Type objectType) => objectType == typeof(IModule);
		}
	}
}