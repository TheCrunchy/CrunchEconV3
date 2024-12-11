using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlay.Contracts.Configs;
using CrunchEconV3.PlugAndPlayV2.StationLogics;
using CrunchEconV3.Utils;
using EmptyKeys.UserInterface.Generated;

namespace CrunchEconV3.PlugAndPlayV2.Handlers
{
    public static class TemplateHandler
    {
        private static FileUtils utils = new FileUtils();
        private static bool HasTriedToLoad = false;
        private static Dictionary<string, StationConfig> Templates = new();
        public static StationConfig? GetTemplateFromName(string templateName)
        {
            if (!HasTriedToLoad)
            {
                LoadTemplates();
            }

            return Templates.TryGetValue(templateName, out var templated) ? templated : null;
        }

        public static void LoadTemplates()
        {
          
            var path = $"{Core.path}//TemplateStations";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                var template = new StationConfig();
                template.FileName = "BaseTemplate.json";
                template.Enabled = true;
                template.UsesDefault = true;
                template.Logics = new List<IStationLogic>()
                {
                    new StoreLogic()
                };
                template.ContractFiles = new List<string>();
                utils.WriteToJsonFile($"{path}//BaseTemplate.json", template);
                //make a template with store logic and default setup contracts 
            }

            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    var read = utils.ReadFromJsonFile<StationConfig>(file);
                    Templates[read.FileName.Replace(".json", "")] = read;
                }
                catch (Exception e)
                {
                    Core.Log.Error($"Error reading file {file} {e}");
                }
            }
        }

        public static void CreateMining(string path)
        {
            var template = new StationConfig();
            template.FileName = "MiningTemplate.json";
            template.Enabled = true;
            template.UsesDefault = false;
            template.Logics = new List<IStationLogic>()
            {
                new StoreLogic()
            };
            Directory.CreateDirectory($"{Core.path}/Stations/BaseSetup");
   
            template.ContractFiles = new List<string>() { "/Stations/BaseSetup/BasicMining.json" , "/Stations/BaseSetup/AdvancedMining.json" };
            utils.WriteToJsonFile($"{path}//MiningTemplate.json", template);

            var basicMining = new List<IContractConfig>();
            var advancedMining = new List<IContractConfig>();
            basicMining.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Iron", "Nickel", "Silicon", "Ice" },
                ReputationLossOnAbandon = 15,
                ReputationGainOnCompleteMax = 10,
                ReputationGainOnCompleteMin = 5,
                AmountToMineThenDeliverMin = 10000,
                AmountToMineThenDeliverMax = 15000,
                AmountOfContractsToGenerate = 5,
                ChanceToAppear = 0.5f,
                SecondsToComplete = 4800,
            });
            basicMining.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Ice" },
                ReputationLossOnAbandon = 15,
                ReputationGainOnCompleteMax = 10,
                ReputationGainOnCompleteMin = 5,
                AmountToMineThenDeliverMin = 100000,
                AmountToMineThenDeliverMax = 150000,
                AmountOfContractsToGenerate = 5,
                ChanceToAppear = 0.05f,
                SecondsToComplete = 4800,
            });
            basicMining.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Iron", "Nickel", "Silicon", "Ice" },
                ReputationRequired = 1000,
                ReputationLossOnAbandon = 15,
                ReputationGainOnCompleteMax = 10,
                ReputationGainOnCompleteMin = 5,
                AmountToMineThenDeliverMin = 25000,
                AmountToMineThenDeliverMax = 30000,
                AmountOfContractsToGenerate = 2,
                ChanceToAppear = 0.3f,
                SecondsToComplete = 4800,
            });
            basicMining.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Silver", "Gold", "Cobalt", "Magnesium" },
                ReputationLossOnAbandon = 20,
                ReputationGainOnCompleteMax = 5,
                ReputationGainOnCompleteMin = 3,
                AmountToMineThenDeliverMin = 1000,
                AmountToMineThenDeliverMax = 1500,
                AmountOfContractsToGenerate = 5,
                ChanceToAppear = 0.5f,
                SecondsToComplete = 4800,
            });
            advancedMining.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Silver", "Gold", "Cobalt", "Magnesium" },
                ReputationRequired = 250,
                ReputationLossOnAbandon = 20,
                ReputationGainOnCompleteMax = 10,
                ReputationGainOnCompleteMin = 5,
                AmountToMineThenDeliverMin = 10000,
                AmountToMineThenDeliverMax = 15000,
                AmountOfContractsToGenerate = 3,
                ChanceToAppear = 0.3f,
                SecondsToComplete = 4800,
            });
            advancedMining.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Uranium", "Platinum" },
                ReputationRequired = 750,
                ReputationLossOnAbandon = 30,
                ReputationGainOnCompleteMax = 15,
                ReputationGainOnCompleteMin = 10,
                AmountToMineThenDeliverMin = 10000,
                AmountToMineThenDeliverMax = 15000,
                AmountOfContractsToGenerate = 3,
                ChanceToAppear = 0.3f,
                SecondsToComplete = 4800,
            });

            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/BasicMining.json", basicMining);
            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/AdvancedMining.json", advancedMining);
        }
    }
}
