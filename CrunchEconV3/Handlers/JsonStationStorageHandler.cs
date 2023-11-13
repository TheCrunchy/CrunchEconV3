using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            foreach (var item in Directory.GetFiles(BasePath))
            {
                var loaded = FileUtils.ReadFromJsonFile<StationConfig>(item);
                loaded.FileName = Path.GetFileName(item);

                if (loaded.ContractFiles != null)
                {
                    foreach (var file in loaded.ContractFiles)
                    {
                        try
                        {
                            if (!MappedConfigs.ContainsKey(file))
                            {
                                try
                                {
                                    MappedConfigs.Add(file, FileUtils.ReadFromJsonFile<List<IContractConfig>>($"{BasePath}/{file}"));
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
                                MappedConfigs.Add(con, FileUtils.ReadFromJsonFile<List<IContractConfig>>($"{BasePath.Replace("/Stations","")}/{con}"));
                            }
                            catch (Exception e)
                            {
                                Core.Log.Error(e);
                                continue;
                            }
                        }
                        MappedKeenConfigs.Add(item, MappedConfigs[con]);
                    }
                }
            }
        }

        public void Save(StationConfig PlayerData)
        {
            foreach (var item in Configs)
            {
                FileUtils.WriteToJsonFile($"{BasePath}/{item.FileName}.json", item);
            }
        }

        public void GenerateExample()
        {
            var example = new StationConfig();
            example.FileName = "Example.Json";
            example.Enabled = false;
            example.FactionTag = "SPRT";
            example.ContractFiles = new List<string>();
            example.ContractFiles.Add("/Example/Contracts.json");

            var examples = new List<IContractConfig>();
            var mining = new MiningContractConfig();
            mining.OresToPickFrom = new List<string>() { "Iron", "Nickel", "Cobalt" };
            var people = new PeopleHaulingContractConfig();
            people.PassengerBlocksAvailable = new List<PassengerBlock>();
            people.PassengerBlocksAvailable.Add(new PassengerBlock()
            {
                BlockPairName = "Bed",
                PassengerSpace = 2
            });
            people.PassengerBlocksAvailable.Add(new PassengerBlock()
            {
                BlockPairName = "InsetBed",
                PassengerSpace = 2
            });
            people.PassengerBlocksAvailable.Add(new PassengerBlock()
            {
                BlockPairName = "Bathroom",
                PassengerSpace = 2
            });
            people.PassengerBlocksAvailable.Add(new PassengerBlock()
            {
                BlockPairName = "BathroomOpen",
                PassengerSpace = 2
            });
            people.PassengerBlocksAvailable.Add(new PassengerBlock()
            {
                BlockPairName = "Kitchen",
                PassengerSpace = 2
            });
            examples.Add(people);
            examples.Add(mining);
            var gas = new GasContractConfig();
            gas.GasSubType = "Hydrogen";
            examples.Add(gas);

            var gas2 = new GasContractConfig();
            gas2.GasSubType = "Hydrogen";
            gas2.DeliveryGPSes = new List<string>() { "Put a gps here", "put a gps here 2" };
            examples.Add(gas2);
            FileUtils.WriteToJsonFile($"{BasePath}/Example.json", example);
            Directory.CreateDirectory($"{BasePath}/Example");
            FileUtils.WriteToJsonFile($"{BasePath}/Example/Contracts.json", examples);
        }
    }
}
