using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CIM.Cson.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CIM.Cson.Internals
{
    class CustomizedJsonSerializer
    {
        private JsonSerializerSettings _serializerSettings;

        public CustomizedJsonSerializer(List<Type> includeShortHandForTypes)
        {
            _serializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                NullValueHandling = NullValueHandling.Ignore,
                SerializationBinder = new ShortNameBinder(includeShortHandForTypes),
                Converters =
                {
                   new ObjectReferenceSerializer(),
                   new StringEnumConverter()
                },
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                ContractResolver = new CustomizedContractResolver(),
            };
        }

        public string Serialize(object obj)
        {
            try
            {
                var json = JsonConvert.SerializeObject(obj, _serializerSettings);

                return json;
            }
            catch (Exception exception)
            {
                throw new SerializationException($"Could not serialize object {obj} to JSON", exception);
            }
        }

        public object Deserialize(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject(json, _serializerSettings);

                return obj;
            }
            catch (Exception exception)
            {
                throw new SerializationException($"Could not deserialize the following JSON text: '{json}'", exception);
            }
        }
    }
}
