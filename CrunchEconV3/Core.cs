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
using CrunchEconV3.Models.Config;
using CrunchEconV3.Utils;
using NLog;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers;
using Torch.Managers.PatchManager;
using Torch.Session;

namespace CrunchEconV3
{
    public class Core : TorchPluginBase
    {
        public static Config config;
        public int ticks;
        public TorchSessionState TorchState;

        public ICrunchPlayerStorage PlayerStorage;
        public ICrunchStationStorage StationStorage;
        private static string path;
        private static string basePath;
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
            PlayerStorage = new JsonPlayerStorageHandler(path);
            StationStorage = new JsonStationStorageHandler(path);
        }

        public override void Update()
        {

            ticks++;
            if (ticks % 256 == 0 && TorchState == TorchSessionState.Loaded)
            {

                foreach (var player in MySession.Static.Players.GetOnlinePlayers())
                {
                    List<ICrunchContract> deleteThese = new List<ICrunchContract>();
                    var data = PlayerStorage.GetData(player.Id.SteamId);
                    foreach (var contract in data.PlayersContracts.Where(x => x.Value.CanAutoComplete))
                    {
                        var completed = contract.Value.TryCompleteContract(player.Id.SteamId, player.Character?.PositionComp?.GetPosition());
                        if (completed)
                        {
                            deleteThese.Add(contract.Value);
                        }
                    }

                    if (!deleteThese.Any()) continue;

                    foreach (var contract in deleteThese)
                    {
                        data.RemoveContract(contract);
                    }
                    PlayerStorage.Save(data);

                }
            }
        }

        private void SetupConfig()
        {
            FileUtils utils = new FileUtils();
            var path = StoragePath + $"\\{PluginName}\\Config.xml";
            basePath = StoragePath;
            Directory.CreateDirectory(path);
            if (File.Exists(path))
            {
                config = utils.ReadFromXmlFile<Config>(path);
                utils.WriteToXmlFile<Config>(path, config, false);
            }
            else
            {
                config = new Config();
                utils.WriteToXmlFile<Config>(path, config, false);
            }

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
                session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined += PlayerStorage.LoadLogin;

                StationStorage.GenerateExample();
            }
        }
    }
}

