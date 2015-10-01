using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Helpers.Serialization
{
    /// <summary>
    /// Interface for deserialization for objects
    /// </summary>
    public interface ISerializable
    {
        /// <summary>
        /// Invoke after deserialization ends
        /// </summary>
        void OnDeserialized();
    }

    /// <summary>
    /// Extensions for serialization methods
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// Serialize this current object to byte array
        /// </summary>
        /// <param name="source">Object to serialize</param>
        /// <param name="compressed">Use GZip compression</param>
        /// <returns>Byte array of serialized object</returns>
        public static byte[] SerializeToBytes(this object source, bool compressed)
        {
            byte[] result = new byte[] { };
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, source);
                if (compressed)
                    using (MemoryStream compressedStream = new MemoryStream())
                    {
                        using (GZipStream compressionStream = new GZipStream(compressedStream, CompressionMode.Compress))
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            stream.CopyTo(compressionStream);
                        }
                        result = compressedStream.ToArray();
                    }
                else
                    result = stream.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Serialize this current object to Base64 string
        /// </summary>
        /// <param name="source">Object to serialize</param>
        /// <param name="compressed">Use GZip compression</param>
        /// <returns>Base64 string of object serialization</returns>
        public static string SerializeToBase64(this object source, bool compressed)
        {
            var bytes = SerializeToBytes(source, compressed);
            return System.Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Serialize this current object to XML string
        /// </summary>
        /// <param name="source">Object to serialize</param>
        /// <param name="clean">Is XML result clean</param>
        /// <returns>XML string of object serialization</returns>
        public static string SerializeToXML(this object source, bool clean = true)
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");

            string result = string.Empty;

            System.Xml.Serialization.XmlSerializer s = new System.Xml.Serialization.XmlSerializer(source.GetType());
            using (MemoryStream stream = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings()
                {
                    Indent = !clean,
                    OmitXmlDeclaration = true,
                    Encoding = Encoding.UTF8
                };
                using(XmlWriter writer = XmlWriter.Create(stream, settings))
                { 
                    s.Serialize(writer, source, ns);
                    stream.Seek(0, SeekOrigin.Begin);
                    result = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                }
            }
            return result;
        }

        /// <summary>
        /// Deserialization byte array to selected object. <i>Using: typeof(item).DeserializeFromBytes(...)</i>
        /// </summary>
        /// <typeparam name="type">Object type</typeparam>
        /// <param name="typeOfObject">Object type</param>
        /// <param name="bytes">Byte array to deserialization</param>
        /// <param name="compressed">Is stream was compressed</param>
        /// <param name="result">Deserializated object</param>
        public static void DeserializeFromBytes<type>(this Type typeOfObject, byte[] bytes, bool compressed, out type result)
        {
            object res = null;
            using (var compressedStream = new MemoryStream(bytes))
            {
                if (compressed)
                using (var decompressStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    res = (new BinaryFormatter()).Deserialize(decompressStream);
                } else
                    res = (new BinaryFormatter()).Deserialize(compressedStream);

                result = (type)res;

                ISerializable iResult = result as ISerializable;
                if (iResult != null)
                    iResult.OnDeserialized();
            }
        }

        /// <summary>
        /// Deserialization base64 string to selected object. <i>Using: typeof(item).DeserializeFromBase64(...)</i>
        /// </summary>
        /// <typeparam name="type">Object type</typeparam>
        /// <param name="typeOfObject">Object type</param>
        /// <param name="base64String">Base64 string to deserialization</param>
        /// <param name="compressed">Is stream was compressed</param>
        /// <param name="result">Deserializated object</param>
        public static void DeserializeFromBase64<type>(this Type typeOfObject, string base64String, bool compressed, out type result)
        {
            var bytes = System.Convert.FromBase64String(base64String);
            DeserializeFromBytes<type>(typeOfObject, bytes, compressed, out result);
        }

        /// <summary>
        /// Deserialization XML string to selected object. <i>Using: typeof(item).DeserializeFromXML(...)</i>
        /// </summary>
        /// <typeparam name="type">Object type</typeparam>
        /// <param name="typeOfObject">Object type</param>
        /// <param name="xml">Xml string to deserialization</param>
        /// <param name="result">Deserializated object</param>
        public static void DeserializeFromXML<type>(this Type typeOfObject, string xml, out type result)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                System.Xml.Serialization.XmlSerializer s = new System.Xml.Serialization.XmlSerializer(typeOfObject);
                var res = s.Deserialize(stream);

                result = (type)res;

                ISerializable iResult = result as ISerializable;
                if (iResult != null)
                    iResult.OnDeserialized();
            }
        }

        /// <summary>
        /// Compress current string to bytes using GZip
        /// </summary>
        /// <param name="source">String to compress</param>
        /// <returns>Compressed byte array</returns>
        public static byte[] CompressToBytes(this string source)
        {
            byte[] result = Encoding.UTF8.GetBytes(source);
            using (var stream = new MemoryStream(result))
            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (GZipStream compressionStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(compressionStream);
                }
                result = compressedStream.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Decompress byte array to string
        /// </summary>
        /// <param name="source">Source byte array</param>
        /// <returns>Decompressed string</returns>
        public static string DecompressFromBytes(this byte[] source)
        {
            string result = string.Empty;
            using (var compressedStream = new MemoryStream(source))
            using (var decompressStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(decompressStream, Encoding.UTF8))
                result = reader.ReadToEnd();       
            return result;
        }
    }
}
