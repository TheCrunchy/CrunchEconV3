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
        private string BasePath { get; set; }
        private FileUtils FileUtils { get; set; } = new FileUtils();
        public JsonStationStorageHandler(string BasePath)
        {
            this.BasePath = $"{BasePath}/Stations/";
            Directory.CreateDirectory(this.BasePath);
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

        public void LoadAll()
        {
            Configs.Clear();
            foreach (var item in Directory.GetFiles(BasePath))
            {
                var loaded = FileUtils.ReadFromJsonFile<StationConfig>(item);
                loaded.FileName = Path.GetFileName(item);
                if (loaded.Enabled)
                {
                    Configs.Add(loaded);
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
            example.Contracts = new List<IContractConfig>();
            var mining = new MiningContractConfig();
            mining.OresToPickFrom = new List<string>() { "Iron", "Nickel", "Cobalt" };
            var people = new PeopleHaulingContractConfig();
            people.PassengerBlocksAvailable = new List<PassengerBlock>();
            people.PassengerBlocksAvailable.Add(new PassengerBlock()
            {
                BlockPairName = "Bed",
                PassengerSpace = 2
            });
            example.Contracts.Add(people);
            example.Contracts.Add(mining);
            var gas = new GasContractConfig();
            gas.GasSubType = "Hydrogen";
            gas.AmountInLitresMin = 480 * 1000;
            example.Contracts.Add(gas);
            FileUtils.WriteToJsonFile($"{BasePath}/Example.json", example);

        }
    }
}
