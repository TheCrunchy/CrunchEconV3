﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CrunchEconV3.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CrunchEconV3.Utils
{
    public static class FileUtils
    {

        public static void WriteToJsonFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            try
            {
                // Check if the file is locked before opening it

                var contentsToWriteToFile = JsonConvert.SerializeObject(objectToWrite, new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    SerializationBinder = new MySerializationBinder(),
                    Formatting = Newtonsoft.Json.Formatting.Indented
                });

                // Use a FileStream to keep the file open while writing
                using (var fileStream = new FileStream(filePath, append ? FileMode.Append : FileMode.Create,
                           FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(fileStream))
                    {
                        writer.Write(contentsToWriteToFile);
                    }
                }
            }
            catch (IOException ex)
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    var fileContents = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<T>(fileContents, new JsonSerializerSettings()
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        SerializationBinder = new MySerializationBinder(),
                        Formatting = Newtonsoft.Json.Formatting.Indented
                    });
                }
            }
            catch (Exception e)
            {
                //   Core.Log.Error($"Error reading file, moved to backups");
                Core.Log.Error($"Error reading file {filePath} {e}");

                //     Directory.CreateDirectory($"{Core.path}/ErroredFileBackups/");
                //  File.Move(filePath, $"{Core.path}/ErroredFileBackups/{Path.GetFileNameWithoutExtension(filePath)}-{DateTime.Now:HH-mm-ss-dd-MM-yyyy}.json");

                return new T();
            }

        }


        public static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                writer = new StreamWriter(filePath, append);
                serializer.Serialize(writer, objectToWrite);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        public static T ReadFromXmlFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                reader = new StreamReader(filePath);
                return (T)serializer.Deserialize(reader);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
    }

    public class MySerializationBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {

            // Check if the type is already loaded in the current AppDomain
            Type existingType = Type.GetType(typeName);

            if (existingType != null)
            {
                // Type is already loaded, return it
                ///   Core.Log.Info($"Type '{typeName}' is already loaded in the current project.");
                return existingType;
            }

            // Type is not loaded, try to load it from specified assemblies
            var t = Core.myAssemblies
                .SelectMany(x => x.GetTypes())
                .FirstOrDefault(x => x.FullName == typeName);

            if (t == null)
            {
                Core.Log.Info($"Cannot resolve type {typeName}");
            }

            return t;
        }
    }
}

