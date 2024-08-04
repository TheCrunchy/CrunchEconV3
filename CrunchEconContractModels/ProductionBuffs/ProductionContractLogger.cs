using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using CrunchEconV3.PlugAndPlay.Contracts;
using CrunchEconV3.Utils;
using Sandbox.Definitions;
using Sandbox.Game.World;
using Torch.API;
using Torch.API.Managers;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.ProductionBuffs
{
    public static class ProductionContractLogger
    {
        private static string Folder;
        private static FileUtils Utils = new FileUtils();
        public static OreBuffThresholds OreBuffs;
        public static DrillBuffThresholds DrillBuffs;
        public static void Patch(PatchContext ctx)
        {
            Core.Session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined += LoadLogin;
            Core.UpdateCycle += UpdateExample;

            Folder = $"{Core.path}\\CompletedMiningData";
            var orePath = $"{Core.path}\\OreProductionBuffs.json";
            var drillPath = $"{Core.path}\\DrillYieldBuffs.json";
            if (File.Exists(drillPath))
            {
                DrillBuffs = Utils.ReadFromJsonFile<DrillBuffThresholds>(drillPath);
            }
            else
            {
                DrillBuffs = new DrillBuffThresholds()
                {
                    DrillYieldBuffs = new Dictionary<string, List<BuffThreshold>>()
                
                };
                foreach (var definition in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    if ((definition as MyPhysicalItemDefinition) == null) 
                        continue;
                    if (definition.Id != null && definition.Id.TypeId.ToString() != "MyObjectBuilder_Ore") 
                        continue;
                    DrillBuffs.DrillYieldBuffs[definition.Id.SubtypeName] = new List<BuffThreshold>
                    {
                        new BuffThreshold() { Amount = 1000000, Buff = 1.025f },
                        new BuffThreshold() { Amount = 2500000, Buff = 1.05f },
                        new BuffThreshold() { Amount = 5000000, Buff = 1.1f },
                        new BuffThreshold() { Amount = 10000000, Buff = 1.15f },
                        new BuffThreshold() { Amount = 100000000, Buff = 1.20f }
                    };
                }
                Utils.WriteToJsonFile(drillPath, DrillBuffs);
            }
            if (File.Exists(orePath))
            {
                OreBuffs = Utils.ReadFromJsonFile<OreBuffThresholds>(orePath);
            }
            else
            {
                OreBuffs = new OreBuffThresholds()
                {
                    SpeedBuffs =
                        new Dictionary<string, List<BuffThreshold>>(),
                    YieldBuffs = new Dictionary<string, List<BuffThreshold>>(),
                };
                foreach (var definition in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    if ((definition as MyPhysicalItemDefinition) == null)
                        continue;
                    if (definition.Id != null && definition.Id.TypeId.ToString() != "MyObjectBuilder_Ore")
                        continue;
                    OreBuffs.YieldBuffs[definition.Id.SubtypeName] = new List<BuffThreshold>
                    {
                        new BuffThreshold() { Amount = 1000000, Buff = 1.025f },
                        new BuffThreshold() { Amount = 2500000, Buff = 1.05f },
                        new BuffThreshold() { Amount = 5000000, Buff = 1.1f },
                        new BuffThreshold() { Amount = 10000000, Buff = 1.15f },
                        new BuffThreshold() { Amount = 100000000, Buff = 1.20f }
                    };
                    OreBuffs.SpeedBuffs[definition.Id.SubtypeName] = new List<BuffThreshold>
                    {
                        new BuffThreshold() { Amount = 1000000, Buff = 1.025f },
                        new BuffThreshold() { Amount = 2500000, Buff = 1.05f },
                        new BuffThreshold() { Amount = 5000000, Buff = 1.1f },
                        new BuffThreshold() { Amount = 10000000, Buff = 1.15f },
                        new BuffThreshold() { Amount = 100000000, Buff = 1.20f }
                    };
                }
                Utils.WriteToJsonFile(orePath, OreBuffs);
            }
            Directory.CreateDirectory(Folder);
        }
        private static int ticks;

        public static void UpdateExample()
        {
            if (ticks == 0)
            {
                Core.PlayerStorage.ContractFinished += Finished;
            }
            ticks++;
            if (ticks % 3000 == 0)
            {
                foreach (var player in MySession.Static.Players.GetOnlinePlayers())
                {
                    if (!SetupPlayers.TryGetValue(player.Identity.IdentityId, out var data))
                    {
                        Load(player.Identity.IdentityId, player.Id.SteamId);
                    }
                }
            }
        }

        public static Dictionary<long, FinishedContractsModel> SetupPlayers =
            new Dictionary<long, FinishedContractsModel>();

        public static void LoadLogin(IPlayer Player)
        {
            var playerId = Player.SteamId;
            if (MySession.Static.Players.TryGetPlayerBySteamId(playerId, out var player))
            {
                Load(player.Identity.IdentityId, player.Id.SteamId);
            }
        }

        public static void Load(long identityId, ulong steamId)
        {
            Task.Run(() =>
            {
                var path = $"{Folder}\\{steamId}.json";
                if (File.Exists(path))
                {
                    var data = Utils.ReadFromJsonFile<FinishedContractsModel>(path);
                    SetupPlayers[identityId] = data;
                }
                else
                {
                    SetupPlayers[identityId] = new FinishedContractsModel();
                }
            });
        }

        public static void Finished(bool successful, ICrunchContract Arg2)
        {
            var playerId = Arg2.AssignedPlayerIdentityId;
            var path = $"{Folder}\\{Arg2.AssignedPlayerSteamId}.json";
            long NumToStore;
            Core.Log.Info($"{Arg2.GetType().ToString()}");
            switch (Arg2.GetType().Name)
            {
                case "CrunchMiningContractImplementation":
                case "MiningContractImplementation":
                    {
                        NumToStore = ReflectMiningValue(Arg2);
                        break;
                    }
                default:
                    {
                        return;
                    }
            }

            if (SetupPlayers.TryGetValue(playerId, out var data))
            {
                if (data.FinishedTypes.TryGetValue(Arg2.Name, out var value))
                {
                    data.FinishedTypes[Arg2.Name] = value + NumToStore;
                }
                else
                {
                    data.FinishedTypes[Arg2.Name] = NumToStore;
                }
            }
            else
            {
                SetupPlayers.Add(playerId, new FinishedContractsModel()
                {
                    FinishedTypes = new Dictionary<string, long>() { { Arg2.Name, NumToStore } }
                });
            }

            Task.Run(() =>
            {
                Utils.WriteToJsonFile(path, SetupPlayers[playerId]);
            });
            Core.Log.Error($"{playerId} finished {Arg2.Name} {NumToStore}");
        }

        public static long ReflectMiningValue(ICrunchContract contract)
        {
            Type contractType = contract.GetType();
            PropertyInfo minedOreAmountProperty = contractType.GetProperty("AmountToMine");
            if (minedOreAmountProperty != null)
            {
                int minedOreAmount = (int)minedOreAmountProperty.GetValue(contract);
                return minedOreAmount;
            }

            return 0;
        }

        public class FinishedContractsModel
        {
            public Dictionary<string, long> FinishedTypes = new Dictionary<string, long>();
        }

        public class OreBuffThresholds
        {
            public Dictionary<string, List<BuffThreshold>> YieldBuffs =
                new Dictionary<string, List<BuffThreshold>>();

            public Dictionary<string, List<BuffThreshold>> SpeedBuffs =
                new Dictionary<string, List<BuffThreshold>>();
        }
        public class DrillBuffThresholds
        {
            public Dictionary<string, List<BuffThreshold>> DrillYieldBuffs =
                new Dictionary<string, List<BuffThreshold>>();

        }
        public class BuffThreshold
        {
            public long Amount;
            public float Buff;
        }
    }
}
