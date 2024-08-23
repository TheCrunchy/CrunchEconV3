using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Models.Config;
using CrunchEconV3.Utils;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers;
using Torch.Managers.PatchManager;
using Torch.Session;
using VRageMath;
using System.IO.Compression;
using CoreSystems.Api;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3.APIs;
using CrunchEconV3.Patches;
using CrunchEconV3.PlugAndPlay;
using CrunchEconV3.PlugAndPlay.Contracts.Configs;
using CrunchEconV3.PlugAndPlay.Helpers;
using CrunchEconV3.PlugAndPlay.Models;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI;
using Torch.Commands;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace CrunchEconV3
{
    public class Core : TorchPluginBase
    {
        public static List<Assembly> myAssemblies { get; set; } = new List<Assembly>();
        public static Config config;
        public int ticks;
        public TorchSessionState TorchState;
        public static Random random = new Random();
        public static ICrunchPlayerStorage PlayerStorage;
        public static ICrunchStationStorage StationStorage;
        public static string path;
        public static string basePath;
        public static MESApi MesAPI;
        public static WaterModAPI WaterAPI;
        public static RemoteBotAPI AIEnabledAPI;
        public static WcApi WeaponcoreAPI;
        public static WcApi.DamageHandlerHelper DamageHandlerWeaponCore;
        public static bool Paused { get; set; } = false;
        public const string PluginName = "CrunchEconV3";
        public static Logger Log = LogManager.GetLogger(PluginName);
        public static ITorchPlugin PluginInstance;
        public static bool NexusInstalled = false;
        public static NexusAPI Nexus;
        public static bool CompileFailed = false;
        public static Action UpdateCycle;
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();

            if (sessionManager != null)
            {
                sessionManager.SessionStateChanged += SessionChanged;
            }

            SetupConfig();
            CreatePath();
            PluginInstance = this;
            Directory.CreateDirectory(path + "/Scripts/");
        }

        public static ITorchSession Session;

        public DateTime NextContractGps = DateTime.Now;
        public DateTime NextKeenMap = DateTime.Now;

        public void InitNexus()
        {
            var Plugins = Session.Managers.GetManager<PluginManager>();
            if (Plugins.Plugins.TryGetValue(new Guid("28a12184-0422-43ba-a6e6-2e228611cca5"), out ITorchPlugin torchPlugin))
            {
                Type type = torchPlugin.GetType();
                Type type2 = ((type != null) ? type.Assembly.GetType("Nexus.API.PluginAPISync") : null);
                if (type2 != null)
                {
                    type2.GetMethod("ApplyPatching", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[]
                    {
                        typeof(NexusAPI),
                        $"{PluginName}"
                    });
                    Nexus = new NexusAPI(4326);

                    NexusInstalled = true;
                }
            }
        }

        public override void Update()
        {
            if (Paused)
            {
                return;
            }

            if (ticks == 0)
            {
                try
                {
                    Core.Log.Info("Running compiler");
                    Compiler.Compile($"{Core.path}/Scripts/");
                    InitNexus();
                }

                catch (Exception e)
                {
                    Core.Log.Error($"compile error {e}");
                }
                if (!CompileFailed)
                {
                    StationStorage = new JsonStationStorageHandler(path);
                    PlayerStorage = new JsonPlayerStorageHandler(path);
                    Session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined += PlayerStorage.LoadLogin;
                    GenerateDefaults();
                    var patches = Core.Session.Managers.GetManager<PatchManager>();
                    KeenStoreManagement.Patch(patches.AcquireContext());
                    patches.Commit();
                    ScriptMangerPatch.ScriptInit(MySession.Static);
                }
                else
                {
                    Core.Log.Error("Compile failed, station and player data not loaded");
                }

            }
            try
            {
                ticks++;
                try
                {
                    UpdateCycle?.Invoke();
                }
                catch (Exception e)
                {
                    Core.Log.Error(e);
                }

                if (ticks % 100 == 0 && TorchState == TorchSessionState.Loaded)
                {
                    if (KeenStoreManagement.UpdatingStoreFiles)
                    {
                        KeenStoreManagement.ForceTick();
                    }
                    if (CompileFailed)
                    {
                        Core.Log.Error("Compile failed, read the compile errors and fix them.");
                        return;
                    }
                    try
                    {
                        StationHandler.DoStationLoop();
                    }
                    catch (Exception e)
                    {
                        Core.Log.Error($"Station logic loop error {e}");
                    }

                    foreach (var player in MySession.Static.Players.GetOnlinePlayers().Where(x => x.Character != null))
                    {
                        List<ICrunchContract> deleteThese = new List<ICrunchContract>();
                        var data = PlayerStorage.GetData(player.Id.SteamId);
                        foreach (var contract in data.PlayersContracts)
                        {
                            try
                            {
                                if (contract.Value.Update100((Vector3)player.Character.PositionComp.GetPosition()))
                                {
                                    deleteThese.Add(contract.Value);
                                }
                            }
                            catch (Exception exception)
                            {
                                Core.Log.Error($"Error on update100 {exception}");
                            }
                        }
                        foreach (var contract in deleteThese)
                        {
                            contract.DeleteDeliveryGPS();
                            data.RemoveContract(contract);
                        }

                        foreach (var contract in data.PlayersContracts.Where(x => x.Value.ReadyToDeliver))
                        {
                            if (contract.Value.ReadyToDeliver && DateTime.Now >= NextContractGps)
                            {
                                contract.Value.DeleteDeliveryGPS();
                                contract.Value.SendDeliveryGPS();
                            }
                        }

                        if (!deleteThese.Any()) continue;

                        foreach (var contract in deleteThese)
                        {
                            data.RemoveContract(contract);
                        }

                        Task.Run(async () =>
                        {
                            PlayerStorage.Save(data);
                        });
                    }

                    if (DateTime.Now >= NextContractGps)
                    {
                        NextContractGps = DateTime.Now.AddMinutes(10);
                    }
                    if (DateTime.Now >= NextKeenMap)
                    {
                        NextKeenMap = DateTime.Now.AddMinutes(10);
                        StationHandler.KeenStations.Clear();
                        foreach (var faction in MySession.Static.Factions.Where(x => x.Value.Stations.Any()))
                        {
                            var stations = faction.Value.Stations;
                            StationHandler.KeenStations.AddRange(stations.Select(x => x));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Core.Log.Error($"Econ update loop error {e}");
            }
        }
        public static void SendMessage(string author, string message, Color color, ulong steamID)
        {
            Logger _chatLog = LogManager.GetLogger("Chat");
            ScriptedChatMsg scriptedChatMsg1 = new ScriptedChatMsg();
            scriptedChatMsg1.Author = author;
            scriptedChatMsg1.Text = message;
            scriptedChatMsg1.Font = "White";
            scriptedChatMsg1.Color = color;
            scriptedChatMsg1.Target = Sync.Players.TryGetIdentityId(steamID);
            ScriptedChatMsg scriptedChatMsg2 = scriptedChatMsg1;
            MyMultiplayerBase.SendScriptedChatMessage(ref scriptedChatMsg2);
        }
        private void SetupConfig()
        {
            FileUtils utils = new FileUtils();
            path = StoragePath + @$"\{PluginName}";
     
            basePath = StoragePath;
            Directory.CreateDirectory(path);
            path += @"\Config.xml";
            if (File.Exists(path))
            {
                config = utils.ReadFromXmlFile<Config>(path);
                utils.WriteToXmlFile<Config>(path, config, false);
            }
            else
            {
                config = new Config();
                config.KeenNPCContracts = new List<KeenNPCEntry>();
                var temp = new KeenNPCEntry();
                temp.ContractFiles = new List<string>() { "/Stations/Example/Contracts.json" };
                temp.NPCFactionTags = new List<string>() { "SPRT" };
                config.KeenNPCContracts.Add(temp);
                utils.WriteToXmlFile<Config>(path, config, false);

            }

            //foreach (var item in Directory.GetFiles(tempfolder).Where(x => x.EndsWith(".cs")))
            //{

            //    File.Copy(item, $"{path}/Scripts/{Path.GetFileName(item)}", true);

            //}

        }


        public static void ReloadConfig()
        {
            FileUtils utils = new FileUtils();
            config = utils.ReadFromXmlFile<Config>($"{basePath}/{PluginName}/Config.xml");
        }

        public string CreatePath()
        {

            var folder = "";
            folder = config.StoragePath.Equals("default") ? Path.Combine(StoragePath + $"\\{PluginName}\\") : Path.Combine(config.StoragePath + $"\\{PluginName}\\");
            path = folder;
            Directory.CreateDirectory(folder);

            return folder;
        }

        private void SessionChanged(ITorchSession session, TorchSessionState newState)
        {
            Session = session;
            TorchState = newState;
            if (newState is TorchSessionState.Unloading)
            {
                if (StationStorage != null)
                {
                    foreach (var station in StationStorage.GetAll())
                    {
                        try
                        {
                            Core.StationStorage.Save(station);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                }

                if (PlayerStorage != null)
                {
                    foreach (var player in MySession.Static.Players.GetOnlinePlayers())
                    {
                        try
                        {
                            var data = Core.PlayerStorage.GetData(player.Id.SteamId);
                            Core.PlayerStorage.Save(data);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                }
            }

            if (newState is TorchSessionState.Loaded)
            {
                Session = session;
                var patchManager = session.Managers.GetManager<PatchManager>();
                var patchContext = patchManager.AcquireContext();

                switch (config.UseDefaultSetup)
                {
                    case true:
                        ContractPatchesDefaultSetup.Patch(patchContext);
                        PriceHelper.Patch(patchContext);
                        PrefabHelper.Patch(patchContext);
                        KeenStoreManagement.Patch(patchContext);
                        Core.Log.Error("Patching defaults");

                        break;
                    default:
                        Core.Log.Error("Patching regular");
                        ContractPatches.Patch(patchContext);
                        break;
                }

                patchManager.Commit();
                Core.Log.Info($"base {basePath}");
            }
        }

        public static void GenerateDefaults()
        {
            var fileUtils = new FileUtils();
            var path = $"{Core.path}/DefaultContracts.json";
            if (File.Exists(path))
            {
                var read = fileUtils.ReadFromJsonFile<List<IContractConfig>>(path);
                StationHandler.DefaultAvailables = read;
            }
            else
            {
                SetupDefaults();
            }
            
            fileUtils.WriteToJsonFile(path, StationHandler.DefaultAvailables);
        }

        private static void SetupDefaults()
        {
            StationHandler.DefaultAvailables.Add(new MiningContractConfig()
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
            StationHandler.DefaultAvailables.Add(new MiningContractConfig()
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
            StationHandler.DefaultAvailables.Add(new MiningContractConfig()
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
            StationHandler.DefaultAvailables.Add(new MiningContractConfig()
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
            StationHandler.DefaultAvailables.Add(new MiningContractConfig()
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
            StationHandler.DefaultAvailables.Add(new MiningContractConfig()
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
            StationHandler.DefaultAvailables.Add(new GasContractConfig()
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
            StationHandler.DefaultAvailables.Add(new GasContractConfig()
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
            StationHandler.DefaultAvailables.Add(new ItemHaulingConfig()
            {
                ReputationLossOnAbandon = 10,
                ReputationGainOnCompleteMax = 5,
                ReputationGainOnCompleteMin = 1,
                AmountOfContractsToGenerate = 5,
                ChanceToAppear = 0.7f,
                SecondsToComplete = 4800,
                ItemsAvailable = new List<ItemHaul>()
                {
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 1000,
                        TypeId = "MyObjectBuilder_Component",
                        SubTypeId = "Girder"
                    },
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 1000,
                        TypeId = "MyObjectBuilder_Component",
                        SubTypeId = "Construction"
                    },
                    new ItemHaul()
                    {
                        AmountMax = 500,
                        AmountMin = 100,
                        TypeId = "MyObjectBuilder_Component",
                        SubTypeId = "MetalGrid"
                    },
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 1000,
                        TypeId = "MyObjectBuilder_Component",
                        SubTypeId = "InteriorPlate"
                    },
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 1000,
                        TypeId = "MyObjectBuilder_Component",
                        SubTypeId = "SteelPlate"
                    }
                }
            });

            StationHandler.DefaultAvailables.Add(new ItemHaulingConfig()
            {
                ReputationLossOnAbandon = 10,
                ReputationGainOnCompleteMax = 5,
                ReputationGainOnCompleteMin = 1,
                AmountOfContractsToGenerate = 2,
                ChanceToAppear = 0.25f,
                SecondsToComplete = 4800,
                ItemsAvailable = new List<ItemHaul>()
                {
                    new ItemHaul()
                    {
                        AmountMax = 10000,
                        AmountMin = 3000,
                        TypeId = "MyObjectBuilder_Ore",
                        SubTypeId = "Ice"
                    },

                }
            });
        }

        public static List<StationConfig> Fakes { get; set; } = new List<StationConfig>();

    }
}

