﻿using Sandbox.Game.Entities.Cube;
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

        public const string PluginName = "CrunchEconV3";
        public static Logger Log = LogManager.GetLogger(PluginName);
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
  
        }

        public DateTime NextContractGps = DateTime.Now;
        public DateTime NextKeenMap = DateTime.Now;
        public override void Update()
        {

            ticks++;
            if (ticks % 256 == 0 && TorchState == TorchSessionState.Loaded)
            {

                foreach (var player in MySession.Static.Players.GetOnlinePlayers())
                {
                    List<ICrunchContract> deleteThese = new List<ICrunchContract>();
                    var data = PlayerStorage.GetData(player.Id.SteamId);
                    foreach (var contract in data.PlayersContracts)
                    {
                        if (contract.Value.Update100(player.GetPosition()))
                        {
                            deleteThese.Add(contract.Value);
                        }
                    }
                    foreach (var contract in deleteThese)
                    {
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
            var path = StoragePath + @$"\{PluginName}";
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


            var folder = StoragePath.Replace(@"\Instance", "");
            var tempfolder = StoragePath + "/CRUNCHECONTEMP/";
            if (Directory.Exists(tempfolder))
            {
                Directory.Delete(tempfolder, true);
            }
            Core.Log.Error(1);
            Directory.CreateDirectory(tempfolder);
            Core.Log.Error(2);
            var plugins = $"{folder}/plugins/CrunchEconV3.zip";
            Core.Log.Error(3);
            ZipFile.ExtractToDirectory(plugins, tempfolder);
            Core.Log.Error(4);
            foreach (var item in Directory.GetFiles(tempfolder).Where(x => x.EndsWith(".dll")))
            {
                Core.Log.Error(5);
                File.Copy(item, $"{basePath}/{PluginName}/{Path.GetFileName(item)}", true);
                Core.Log.Error(6);
            }
            foreach (var item in Directory.GetFiles(tempfolder).Where(x => x.EndsWith(".cs") && x.Contains("Config")))
            {
                Core.Log.Error(7);
                Directory.CreateDirectory($"{basePath}/{PluginName}/Scripts/");
                File.Copy(item, $"{basePath}/{PluginName}/Scripts/{Path.GetFileName(item)}", true);
                Core.Log.Error(8);

            }
            foreach (var item in Directory.GetFiles(tempfolder).Where(x => x.EndsWith(".cs") && x.Contains("Implementation")))
            {
                Directory.CreateDirectory($"{basePath}/{PluginName}/Scripts/");
                File.Copy(item, $"{basePath}/{PluginName}/Scripts/{Path.GetFileName(item)}", true);

            }
            Directory.Delete(tempfolder, true);
        }

  
        public static void ReloadConfig()
        {
            FileUtils utils = new FileUtils();
            config = utils.ReadFromXmlFile<Config>($"{basePath}/{PluginName}/Config.xml");
        }

        public string CreatePath()
        {

            var folder = "";
            folder = config.StoragePath.Equals("default") ? Path.Combine(StoragePath + $"\\{PluginName}\\") : config.StoragePath;
            path = folder;
            Directory.CreateDirectory(folder);

            return folder;
        }
        private void SessionChanged(ITorchSession session, TorchSessionState newState)
        {
            TorchState = newState;
            if (newState is TorchSessionState.Loaded)
            {
                PlayerStorage = new JsonPlayerStorageHandler(path);
                session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined += PlayerStorage.LoadLogin;
                try
                {
                    foreach (var item in Directory.GetFiles($"{path}/Scripts/").Where(x => x.EndsWith(".cs") && x.Contains("Config")))
                    {
                        Core.Log.Error($"compile first");
                        Compiler.Compile(item);
                        Core.Log.Error($"compile second");
                    }
                    foreach (var item in Directory.GetFiles($"{path}/Scripts/").Where(x => x.EndsWith(".cs") && x.Contains("Implementation")))
                    {
                        Compiler.Compile(item);
                    }
                }
                catch (Exception e)
                {
                    Core.Log.Error($"compile error {e}");
                    throw;
                }

            
                StationStorage = new JsonStationStorageHandler(path);
            }
        }
    }
}

