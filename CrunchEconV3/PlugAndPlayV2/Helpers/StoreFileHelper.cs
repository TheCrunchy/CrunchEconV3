using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.PlugAndPlayV2.Models;
using CrunchEconV3.Utils;

namespace CrunchEconV3.PlugAndPlayV2.Helpers
{
    public static class StoreFileHelper
    {

        public static Dictionary<string, StoreLists> MappedBlockNames = new Dictionary<string, StoreLists>();
        public static FileUtils utils = new FileUtils();
        public const string EndFolder = "StoreFilesV3";
        public static void LoadTheFiles()
        {
            MappedBlockNames.Clear();
            
            foreach (var file in Directory.GetFiles($"{CrunchEconV3.Core.path}/{EndFolder}/"))
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    MappedBlockNames[name] = utils.ReadFromJsonFile<StoreLists>(file);
                }
                catch (Exception e)
                {
                    CrunchEconV3.Core.Log.Error($"Error reading store entry file {file}");
                    throw;
                }
            }
        }

        public static StoreLists? GetList(string name)
        {
            name = name.Replace(".json", "").Replace(".Json", "");
            if (!MappedBlockNames.Any())
            {
                LoadTheFiles();
            }
            if (MappedBlockNames.TryGetValue(name, out var item))
            {
                return item;
            }

            return null;
        }

        public static void SaveFile(StoreLists file, string name)
        {
            Directory.CreateDirectory($"{Core.path}/{EndFolder}");
            utils.WriteToJsonFile($"{Core.path}/{EndFolder}/{name}", file);
        }
    }
}
