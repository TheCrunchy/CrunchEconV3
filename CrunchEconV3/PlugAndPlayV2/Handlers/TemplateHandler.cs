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
using CrunchEconV3.PlugAndPlay.Models;
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
                template.ContractFiles = new List<string>() { "/BaseSetup/IceMining.json", "/BaseSetup/GasHauling.json", "/BaseSetup/BasicHauling.json" };
                template.SecondsBetweenContractRefresh = 1200;
                utils.WriteToJsonFile($"{path}//BaseTemplate.json", template);

                CreateMining(path);

                //make a template with store logic and default setup contracts 
            }

            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    var read = utils.ReadFromJsonFile<StationConfig>(file);
                    Templates[read.FileName.Replace(".json", "").Replace(".Json", "")] = read;
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
            template.SecondsBetweenContractRefresh = 1200;
            template.Logics = new List<IStationLogic>()
            {
                new StoreLogic()
            };
            Directory.CreateDirectory($"{Core.path}/Stations/BaseSetup");

            template.ContractFiles = new List<string>() { "/BaseSetup/IceMining.json", "/BaseSetup/BasicMining.json", "/BaseSetup/AdvancedMining.json", "/BaseSetup/GasHauling.json" };
            utils.WriteToJsonFile($"{path}//MiningTemplate.json", template);

            var ice = new List<IContractConfig>();
            ice.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Ice" },
                ReputationLossOnAbandon = 15,
                ReputationGainOnCompleteMax = 10,
                ReputationGainOnCompleteMin = 5,
                AmountToMineThenDeliverMin = 5000,
                AmountToMineThenDeliverMax = 25000,
                AmountOfContractsToGenerate = 5,
                ChanceToAppear = 0.8f,
                SecondsToComplete = 4800,
            });
            ice.Add(new MiningContractConfig()
            {
                OresToPickFrom = new List<string>() { "Ice" },
                ReputationLossOnAbandon = 15,
                ReputationGainOnCompleteMax = 10,
                ReputationGainOnCompleteMin = 5,
                AmountToMineThenDeliverMin = 10000,
                AmountToMineThenDeliverMax = 45000,
                AmountOfContractsToGenerate = 3,
                ChanceToAppear = 0.2f,
                SecondsToComplete = 4800,
            });
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

            Directory.CreateDirectory($"{Core.path}/Stations/BaseSetup/");
            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/BasicMining.json", basicMining);
            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/AdvancedMining.json", advancedMining);
            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/IceMining.json", ice);
        }

        public static void CreateHauling(string path)
        {
            var template = new StationConfig();
            template.FileName = "HaulingTemplate.json";
            template.Enabled = true;
            template.UsesDefault = false;
            template.SecondsBetweenContractRefresh = 1200;
            template.Logics = new List<IStationLogic>()
            {
                new StoreLogic()
            };
            Directory.CreateDirectory($"{Core.path}/Stations/BaseSetup");

            template.ContractFiles = new List<string>() { "/BaseSetup/GasHauling.json", "/BaseSetup/BasicHauling.json", "/BaseSetup/AdvancedHauling.json" };
            utils.WriteToJsonFile($"{path}//HaulingTemplate.json", template);

            var gases = new List<IContractConfig>();
            gases.Add(new GasContractConfig()
            {
                GasSubType = "Oxygen",
                ReputationLossOnAbandon = 10,
                ReputationGainOnCompleteMax = 5,
                ReputationGainOnCompleteMin = 1,
                AmountOfContractsToGenerate = 3,
                ChanceToAppear = 0.7f,
                SecondsToComplete = 4800,
                AmountInLitresMax = 2000000,
                AmountInLitresMin = 1000000,
            });
            gases.Add(new GasContractConfig()
            {
                GasSubType = "Hydrogen",
                ReputationLossOnAbandon = 10,
                ReputationGainOnCompleteMax = 5,
                ReputationGainOnCompleteMin = 1,
                AmountOfContractsToGenerate = 3,
                ChanceToAppear = 0.7f,
                SecondsToComplete = 4800,
                AmountInLitresMax = 2000000,
                AmountInLitresMin = 1000000,
            });
            var basic = new List<IContractConfig>();
            var advanced = new List<IContractConfig>();
            basic.Add(new ItemHaulingConfig()
            {
                ReputationLossOnAbandon = 10,
                ReputationGainOnCompleteMax = 5,
                ReputationGainOnCompleteMin = 1,
                AmountOfContractsToGenerate = 5,
                ChanceToAppear = 0.8f,
                SecondsToComplete = 4800,
                ItemsAvailable = new List<ItemHaul>()
                {
                    new ItemHaul()
                    {
                        AmountMax = 100000,
                        AmountMin = 30000,
                        TypeId = "MyObjectBuilder_Ore",
                        SubTypeId = "Ice"
                    },
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 3000,
                        TypeId = "MyObjectBuilder_Ore",
                        SubTypeId = "Iron"
                    },   
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 3000,
                        TypeId = "MyObjectBuilder_Ore",
                        SubTypeId = "Nickel"
                    },
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 3000,
                        TypeId = "MyObjectBuilder_Ore",
                        SubTypeId = "Silicon"
                    },

                }
            });
            advanced.Add(new ItemHaulingConfig()
            {
                ReputationLossOnAbandon = 10,
                ReputationGainOnCompleteMax = 5,
                ReputationGainOnCompleteMin = 1,
                AmountOfContractsToGenerate = 5,
                ChanceToAppear = 0.25f,
                SecondsToComplete = 4800,
                ItemsAvailable = new List<ItemHaul>()
                    {
                        new ItemHaul()
                        {
                            AmountMax = 100000,
                            AmountMin = 10000,
                            TypeId = "MyObjectBuilder_Component",
                            SubTypeId = "Girder"
                        },
                        new ItemHaul()
                        {
                            AmountMax = 100000,
                            AmountMin = 10000,
                            TypeId = "MyObjectBuilder_Component",
                            SubTypeId = "Construction"
                        },
                        new ItemHaul()
                        {
                            AmountMax = 5000,
                            AmountMin = 1000,
                            TypeId = "MyObjectBuilder_Component",
                            SubTypeId = "MetalGrid"
                        },
                        new ItemHaul()
                        {
                            AmountMax = 100000,
                            AmountMin = 10000,
                            TypeId = "MyObjectBuilder_Component",
                            SubTypeId = "InteriorPlate"
                        },
                        new ItemHaul()
                        {
                            AmountMax = 100000,
                            AmountMin = 10000,
                            TypeId = "MyObjectBuilder_Component",
                            SubTypeId = "SteelPlate"
                        },
                        new ItemHaul()
                        {
                            AmountMax = 1000,
                            AmountMin = 100,
                            TypeId = "MyObjectBuilder_Component",
                            SubTypeId = "GravityGenerator"
                        },
                        new ItemHaul()
                        {
                            AmountMax = 1000,
                            AmountMin = 100,
                            TypeId = "MyObjectBuilder_Component",
                            SubTypeId = "Thrust"
                        }
                    }
            });


            Directory.CreateDirectory($"{Core.path}/Stations/BaseSetup/");
            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/BasicHauling.json", basic);
            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/AdvancedHauling.json", advanced);
            utils.WriteToJsonFile($"{Core.path}/Stations/BaseSetup/GasHauling.json", gases);
        }
    }
}
