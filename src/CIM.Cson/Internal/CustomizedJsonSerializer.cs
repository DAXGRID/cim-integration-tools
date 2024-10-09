using System;
using System.Runtime.Serialization;
using CIM.Cson.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CIM.Cson.Internals
{
    class CustomizedJsonSerializer
    {
        static readonly JsonSerializerSettings SerializerSettings = GetSettings();

        static JsonSerializerSettings GetSettings()
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                NullValueHandling = NullValueHandling.Ignore,
                Binder = new ShortNameBinder(),
                SerializationBinder = TypeNameAssemblyExcludingSerializationBinder.Instance,
                Converters =
                {
                   new ObjectReferenceSerializer(),
                   new MeasurementTypeSerializer3(),
                   new StringEnumConverter()
                },
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                ContractResolver = new CustomizedContractResolver(),
            };

            return settings;
        }

        public string Serialize(object obj)
        {
            try
            {
                var json = JsonConvert.SerializeObject(obj, SerializerSettings);

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
                var obj = JsonConvert.DeserializeObject(json, SerializerSettings);

                return obj;
            }
            catch (Exception exception)
            {
                throw new SerializationException($"Could not deserialize the following JSON text: '{json}'", exception);
            }
        }
    }
}
