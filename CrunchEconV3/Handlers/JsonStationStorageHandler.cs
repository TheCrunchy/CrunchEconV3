using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Models.Config;
using CrunchEconV3.Utils;

namespace CrunchEconV3.Handlers
{
    public class JsonStationStorageHandler : ICrunchStationStorage
    {
        private List<StationConfig> Configs { get; set; } = new List<StationConfig>();

        private Dictionary<string, List<IContractConfig>> MappedConfigs { get; set; } = new Dictionary<string, List<IContractConfig>>();
        private Dictionary<string, List<IContractConfig>> MappedKeenConfigs { get; set; } = new Dictionary<string, List<IContractConfig>>();
        private string BasePath { get; set; }
        private FileUtils FileUtils { get; set; } = new FileUtils();
        public JsonStationStorageHandler(string BasePath)
        {
            this.BasePath = $"{BasePath}/Stations/";
            Directory.CreateDirectory(this.BasePath);
            GenerateExample();
            LoadAll();
        }

        public List<StationConfig> GetAll()
        {
            if (Configs.Any())
            {
                return Configs;
            }

            LoadAll();

            return Configs;
        }

        public List<IContractConfig> GetForKeen(string factionTag)
        {
            return MappedKeenConfigs.TryGetValue(factionTag, out var con) ? con : new List<IContractConfig>();
        }

        public void LoadAll()
        {
            Configs.Clear();
            MappedConfigs.Clear();
            MappedKeenConfigs.Clear();
            //   Thread.Sleep(Core.random.next);
            foreach (var fake in Core.Fakes)
            {
                SetupContracts(fake);
            }

            foreach (var item in Directory.GetFiles(BasePath))
            {
                var loaded = FileUtils.ReadFromJsonFile<StationConfig>(item);
                loaded.FileName = Path.GetFileName(item);
                var gps = GPSHelper.ScanChat(loaded.LocationGPS);
                if (gps == null)
                {
                    Core.Log.Error($"GPS for station {loaded.FileName} is not valid, station not loaded");
                    continue;
                }
                SetupContracts(loaded);
                foreach (var substation in loaded.SubstationGpsStrings)
                {
                    var station = loaded.Clone();
                    station.FileName = $"{station.FileName} clone";
                    gps = GPSHelper.ScanChat(substation);
                    if (gps == null)
                    {
                        Core.Log.Error($"GPS for station {loaded.FileName} is not valid, station not loaded");
                        continue;
                    }
                    station.LocationGPS = substation;
                    station.SetFake();
                    station.SetFirstLoad(true);
                    SetupContracts(station);
                }
            }

            foreach (var NPC in Core.config.KeenNPCContracts)
            {
                foreach (var item in NPC.NPCFactionTags)
                {
                    MappedKeenConfigs.Remove(item);
                    foreach (var con in NPC.ContractFiles)
                    {
                        if (!MappedConfigs.ContainsKey(con))
                        {
                            try
                            {
                                MappedConfigs.Add(con, FileUtils.ReadFromJsonFile<List<IContractConfig>>($"{BasePath.Replace("/Stations", "")}/{con}"));
                            }
                            catch (Exception e)
                            {
                                Core.Log.Error(e);
                                continue;
                            }
                        }

                        if (MappedKeenConfigs.TryGetValue(item, out var cons))
                        {
                            cons.AddRange(MappedConfigs[con]);
                            MappedKeenConfigs[item] = cons;
                        }
                        else
                        {
                            MappedKeenConfigs.Add(item, MappedConfigs[con]);
                        }
                    }
                }
            }
        }

        private void SetupContracts(StationConfig loaded)
        {
            if (loaded.ContractFiles != null || loaded.ContractFiles.Any())
            {
                foreach (var file in loaded.ContractFiles)
                {
                    try
                    {
                        if (!MappedConfigs.ContainsKey(file))
                        {
                            try
                            {
                                MappedConfigs.Add(file,
                                    FileUtils.ReadFromJsonFile<List<IContractConfig>>($"{BasePath}/{file}"));
                            }
                            catch (Exception e)
                            {
                                Core.Log.Error(e);
                                continue;
                            }
                        }

                        loaded.SetConfigs(MappedConfigs[file]);
                    }
                    catch (Exception exception)
                    {
                        Core.Log.Error(exception);
                    }
                }

                if (loaded.Enabled)
                {
                    Configs.Add(loaded);
                }
            }
            else
            {
                loaded.ContractFiles = new List<string>();
                loaded.SetConfigs(new List<IContractConfig>());
                if (loaded.Enabled)
                {
                    Configs.Add(loaded);
                }
            }
        }

        public void Save(StationConfig StationData)
        {
            if (StationData.GetFake())
            {
                return;
            }
            FileUtils.WriteToJsonFile($"{BasePath}/{StationData.FileName}", StationData);
        }

        public void GenerateExample()
        {
            try
            {
                var example = new StationConfig();
                example.FileName = "Example.Json";
                example.Enabled = false;
                example.FactionTag = "SPRT";
                example.ContractFiles = new List<string>();
                example.ContractFiles.Add("/Example/Contracts.json");
                example.Logics = new List<IStationLogic>();

                example.LocationGPS = "GPS:New:0:0:0:#FF75C9F1:";
                var examples = new List<IContractConfig>();

                var configs = from t in Core.myAssemblies.Select(x => x)
                        .SelectMany(x => x.GetTypes())
                              where t.IsClass && t.GetInterfaces().Contains(typeof(IContractConfig))
                              select t;

                var configs2 = from t in Core.myAssemblies.Select(x => x)
                        .SelectMany(x => x.GetTypes())
                               where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                               select t;

                foreach (var config in configs)
                {

                    IContractConfig instance = (IContractConfig)Activator.CreateInstance(config);
                    instance.Setup();
                    examples.Add(instance);

                }
                foreach (var config in configs2)
                {
                    IStationLogic instance = (IStationLogic)Activator.CreateInstance(config);
                    instance.Setup();
                    example.Logics.Add(instance);

                }

                example.Logics = example.Logics.OrderBy(x => x.Priority).ToList();

                FileUtils.WriteToJsonFile($"{BasePath}/Example.json", example);
                Directory.CreateDirectory($"{BasePath}/Example");
                FileUtils.WriteToJsonFile($"{BasePath}/Example/Contracts.json", examples);
            }
            catch (Exception e)
            {
                Core.Log.Error($"example error {e}");
            }
        }
    }
}
