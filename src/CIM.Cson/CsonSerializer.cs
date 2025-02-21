using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using CIM.PhysicalNetworkModel;
using CIM.Cson.Internals;

namespace CIM.Cson
{
    /// <summary>
    /// CIM JSON serializer
    /// </summary>
    public class CsonSerializer
    {
        readonly CustomizedJsonSerializer _serializer = new CustomizedJsonSerializer();
        readonly int _lineBufferSize;

        /// <summary>
        /// Creates the CSON serializer with the given line buffer size
        /// </summary>
        public CsonSerializer(int lineBufferSize = 1024) => _lineBufferSize = lineBufferSize;

        /// <summary>
        /// Serializes a single object to a JSON string
        /// </summary>
        public string SerializeObject(IdentifiedObject obj) => _serializer.Serialize(obj);

        /// <summary>
        /// Deserializes a single object into its <see cref="IdentifiedObject"/> subclass
        /// </summary>
        public IdentifiedObject DeserializeObject(string json)
        {
            var obj = _serializer.Deserialize(json);

            try
            {
                return (IdentifiedObject)obj;
            }
            catch (Exception exception)
            {
                throw new SerializationException($"The type returned from deserialization {obj.GetType()} could be turned into IdentifiedObject", exception);
            }
        }

        /// <summary>
        /// Returns a JSONL stream from the given <paramref name="objects"/>
        /// </summary>
        public Stream SerializeObjects(IEnumerable<IdentifiedObject> objects)
        {
            var enumerator = objects.GetEnumerator();

            var callbackStream = new CallbackStream(request =>
            {
                var linesStringBuilder = new StringBuilder();

                while (enumerator.MoveNext())
                {
                    linesStringBuilder.Append(SerializeObject(enumerator.Current)).Append(Environment.NewLine);

                    if (linesStringBuilder.Length >= _lineBufferSize)
                    {
                        request.Write(Encoding.UTF8.GetBytes(linesStringBuilder.ToString()));
                        linesStringBuilder.Clear();
                        return;
                    }
                }

                if (linesStringBuilder.Length >= 0)
                {
                    request.Write(Encoding.UTF8.GetBytes(linesStringBuilder.ToString()));
                }
            }, enumerator);

            return callbackStream;
        }

        /// <summary>
        /// Deserializes the given JSON stream and returns <see cref="IdentifiedObject"/> while traversing it
        /// </summary>
        public IEnumerable<IdentifiedObject> DeserializeObjects(Stream source)
        {
            var lineCounter = 0;

            using (var reader = new StreamReader(source, Encoding.UTF8))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    lineCounter++;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var obj = DeserializeObjectFromLine(line, lineCounter);

                    yield return obj;
                }
            }
        }

        IdentifiedObject DeserializeObjectFromLine(string line, int lineCounter)
        {
            try
            {
                return DeserializeObject(line);
            }
            catch (Exception exception)
            {
                throw new SerializationException($"Could not get object from line {lineCounter}", exception);
            }
        }
    }
}
