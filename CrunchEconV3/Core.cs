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
using CrunchEconV3.APIs;
using CrunchEconV3.Patches;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.Components;

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
        public static RemoteBotAPI AIEnabledAPI;
        public static bool Paused { get; set; } = false;
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
        public override async void Update()
        {
            if (Paused)
            {
                return;
            }

            try
            {
                ticks++;
                if (ticks % 100 == 0 && TorchState == TorchSessionState.Loaded)
                {
                    try
                    {
                        Task.Run(async () =>
                                {
                                    StationHandler.DoStationLoop();
                                });
                    }
                    catch (Exception e)
                    {
                        Core.Log.Error($"Station logic loop error {e}");
                    }

                    foreach (var player in MySession.Static.Players.GetOnlinePlayers())
                    {
                        List<ICrunchContract> deleteThese = new List<ICrunchContract>();
                        var data = PlayerStorage.GetData(player.Id.SteamId);
                        foreach (var contract in data.PlayersContracts)
                        {
                            try
                            {
                                if (contract.Value.Update100(player.GetPosition()))
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

            var folder = StoragePath.Replace(@"\Instance", "");
            var tempfolder = StoragePath + "/CRUNCHECONTEMP/";

            CreatePath();
            if (Directory.Exists(tempfolder))
            {
                Directory.Delete(tempfolder, true);
            }
            Directory.CreateDirectory(tempfolder);

            var plugins = $"{folder}/plugins/CrunchEconV3.zip";

            ZipFile.ExtractToDirectory(plugins, tempfolder);
            Directory.CreateDirectory($"{path}/Scripts/");

            foreach (var item in Directory.GetFiles(tempfolder).Where(x => x.EndsWith(".dll")))
            {

                File.Copy(item, $"{path}/{Path.GetFileName(item)}", true);

            }

            //foreach (var item in Directory.GetFiles(tempfolder).Where(x => x.EndsWith(".cs")))
            //{

            //    File.Copy(item, $"{path}/Scripts/{Path.GetFileName(item)}", true);

            //}
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
            folder = config.StoragePath.Equals("default") ? Path.Combine(StoragePath + $"\\{PluginName}\\") : Path.Combine(config.StoragePath + $"\\{PluginName}\\");
            path = folder;
            Directory.CreateDirectory(folder);

            return folder;
        }
        private void SessionChanged(ITorchSession session, TorchSessionState newState)
        {
            TorchState = newState;
            if (newState is TorchSessionState.Unloading)
            {
                foreach (var station in StationStorage.GetAll())
                {
                    Core.StationStorage.Save(station);
                }
            }

            if (newState is TorchSessionState.Loaded)
            {
            
                var patches = session.Managers.GetManager<PatchManager>();
                try
                {
                    foreach (var item in Directory.GetFiles($"{path}/Scripts/").Where(x => x.EndsWith(".cs")))
                    {
                        Compiler.Compile(item);
                    }

                    var typesWithPatchShimAttribute = Core.myAssemblies.Select(x => x)
                        .SelectMany(x => x.GetTypes())
                        .Where(type => type.IsClass && type.GetCustomAttributes(typeof(PatchShimAttribute), true).Length > 0);

                    patches.AcquireContext();

                    foreach (var type in typesWithPatchShimAttribute)
                    {
                        MethodInfo method = type.GetMethod("Patch", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                        if (method == null)
                        {
                            Core.Log.Error($"Patch shim type {type.FullName} doesn't have a static Patch method.");
                            return;
                        }
                        ParameterInfo[] ps = method.GetParameters();
                        if (ps.Length != 1 || ps[0].IsOut || ps[0].IsOptional || ps[0].ParameterType.IsByRef ||
                            ps[0].ParameterType != typeof(PatchContext) || method.ReturnType != typeof(void))
                        {
                            Core.Log.Error($"Patch shim type {type.FullName} doesn't have a method with signature `void Patch(PatchContext)`");
                            return;
                        }

                        var context = patches.AcquireContext();
                        method.Invoke(null, new object[] { context });
                    }
                    patches.Commit();
                }

                catch (Exception e)
                {
                    Core.Log.Error($"compile error {e}");
                    throw;
                }

                //if (config.SetMinPricesTo1)
                //{
                //    foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
                //    {

                //        if ((def as MyComponentDefinition) != null)
                //        {
                //            (def as MyComponentDefinition).MinimalPricePerUnit = 1;
                //        }
                //        if ((def as MyPhysicalItemDefinition) != null)
                //        {
                //            (def as MyPhysicalItemDefinition).MinimalPricePerUnit = 1;
                //        }
                //    }
                //}
                
                StationStorage = new JsonStationStorageHandler(path);
                PlayerStorage = new JsonPlayerStorageHandler(path);
                session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined += PlayerStorage.LoadLogin;
            }
        }
    }
}

