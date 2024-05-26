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
using CrunchEconV3.APIs;
using CrunchEconV3.Patches;
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
                    Compiler.Compile($"{Core.path}/Scripts/");
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
            }
        }

        public static List<StationConfig> Fakes { get; set; } = new List<StationConfig>();

    }
}

