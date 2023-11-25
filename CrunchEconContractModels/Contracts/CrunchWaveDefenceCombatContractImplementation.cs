﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using Torch;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class CrunchWaveDefenceCombatContractImplementation : ICrunchContract
    {
        public long ContractId { get; set; }
        public string ContractType { get; set; }
        public MyObjectBuilder_Contract BuildUnassignedContract(string descriptionOverride = "")
        {
            string definition = this.DefinitionId;
            string contractName = this.Name;
            string contractDescription;
            contractDescription = descriptionOverride != "" ? descriptionOverride : this.Description;

            if (!MyDefinitionId.TryParse(definition, out var definitionId)) return null;
            var newContract = new MyObjectBuilder_ContractCustom
            {
                SubtypeName = definition.Replace("MyObjectBuilder_ContractTypeDefinition/", ""),
                Id = this.ContractId,
                IsPlayerMade = false,
                State = MyContractStateEnum.Active,
                Owners = new MySerializableList<long>(),
                RewardMoney = this.RewardMoney,
                RewardReputation = this.ReputationGainOnComplete,
                StartingDeposit = this.CollateralToTake,
                FailReputationPrice = this.ReputationLossOnAbandon,
                StartFaction = 1,
                StartStation = 0,
                StartBlock = this.BlockId,
                Creation = 1,
                TicksToDiscard = (int?)this.SecondsToComplete,
                RemainingTimeInS = this.SecondsToComplete,
                ContractCondition = null,
                DefinitionId = definitionId,
                ContractName = contractName,
                ContractDescription = contractDescription
            };

            return newContract;
        }

        public MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must go engage the enemy at the target GPS.";
            return BuildUnassignedContract(contractDescription);
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (this.DeliverLocation.Equals(Vector3.Zero))
            {
                Core.Log.Error("Error getting a target point for this contract");
                return Tuple.Create(false, MyContractResults.Error_InvalidData);
            }
            if (this.ReputationRequired != 0)
            {
                var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                if (faction != null)
                {
                    var reputation =
                        MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(identityId, faction.FactionId);
                    if (this.ReputationRequired > 0)
                    {
                        if (reputation.Item2 < this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                    else
                    {
                        if (reputation.Item2 > this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                }
            }
            if (this.CollateralToTake > 0)
            {
                if (EconUtils.getBalance(identityId) < this.CollateralToTake)
                {
                    return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientFunds);
                }
            }

            var current = playerData.GetContractsForType(this.ContractType);
            if (current.Count >= 1)
            {
                return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
            }

            if (this.CollateralToTake > 0)
            {
                EconUtils.takeMoney(identityId, this.CollateralToTake);
            }
            this.AssignedPlayerIdentityId = identityId;
            this.AssignedPlayerSteamId = playerData.PlayerSteamId;
            return Tuple.Create(true, MyContractResults.Success);
        }

        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
            SendDeliveryGPS();
        }


        private DateTime NextSpawn = DateTime.Now;
        public int CurrentWave = 0;
        private bool HasStarted = false;
        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (DateTime.Now > ExpireAt)
            {
                if (UncollectedPay >= this.RewardMoney)
                {
                    EconUtils.addMoney(this.AssignedPlayerIdentityId, this.UncollectedPay + this.RewardMoney);
                    Core.SendMessage("Contracts", $"{this.Name} completed!, you have been paid.", Color.Green, this.AssignedPlayerSteamId);
                    return true;
                }
                else
                {
                    FailContract();
                    return true;
                }
            }
            if (!HasStarted)
            {
                var distance = Vector3.Distance(PlayersCurrentPosition, DeliverLocation);
                if (distance < 15000)
                {
                    HasStarted = true;
                    NextSpawn = DateTime.Now;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                var distance = Vector3.Distance(PlayersCurrentPosition, DeliverLocation);
                if (distance > 50000)
                {
                    return false;
                }
            }

            if (DateTime.Now < NextSpawn) return false;

            if (!Waves.Any(x => x.WaveNumber > CurrentWave) && DateTime.Now > NextSpawn || Waves.Any(x => x.WaveNumber == CurrentWave && x.Repeat))
            {
       
                if (UncollectedPay >= this.RewardMoney)
                {
                    EconUtils.addMoney(this.AssignedPlayerIdentityId, this.UncollectedPay + this.RewardMoney);
                    Core.SendMessage("Contracts", $"{this.Name} completed!, you have been paid.", Color.Green, this.AssignedPlayerSteamId);
                    return true;
                }
                else
                {
                    FailContract();
                    return true;
                }
            }

            var spawn = Waves.FirstOrDefault(x => x.WaveNumber == CurrentWave + 1);
            if (!spawn.Repeat)
            {
                CurrentWave = spawn.WaveNumber;
            }

            NextSpawn = DateTime.Now.AddSeconds(spawn.SecondsBeforeNextWave);
            var player = MySession.Static.Players.TryGetPlayerBySteamId(this.AssignedPlayerSteamId);

            if (!spawn.SpawnGrids)
            {
                return false;
            }

            var spawns = 0;
            foreach (var grid in spawn.GridsInWave)
            {
                if (grid.ChanceToSpawn < 1)
                {
                    var random = CrunchEconV3.Core.random.NextDouble();
                    if (random > grid.ChanceToSpawn)
                    {
                        continue;
                    }
                }

                Vector3 Position = new Vector3D(PlayersCurrentPosition);
                var faction = MySession.Static.Factions.TryGetFactionByTag(grid.FacTagToOwnThisGrid);
                if (faction == null)
                {
                    Core.Log.Info($"{grid.FacTagToOwnThisGrid} faction not found");
                    continue;
                }
                Position.Add(new Vector3(Core.random.Next(spawn.MinDistance, spawn.MaxDistance),
                    Core.random.Next(spawn.MinDistance, spawn.MaxDistance),
                    Core.random.Next(spawn.MinDistance, spawn.MaxDistance)));
                if (!File.Exists($"{Core.path}//Grids//{grid.GridName}")) continue;
                if (!GridManager.LoadGrid($"{Core.path}//Grids//{grid.GridName}", Position, false,
                        (ulong)faction.Members.FirstOrDefault().Key, "Spawned grid", false))
                {
                    Core.Log.Info($"Could not load grid {grid.GridName}");
                }
                else
                {
                    spawns += 1;
                }
            }
            
            if (spawns <= 0) return false;
            foreach (var onlinePlayer in MySession.Static.Players.GetOnlinePlayers())
            {
                Vector3D playerPosition = onlinePlayer.Character?.PositionComp.GetPosition() ?? Vector3D.Zero;
                
                if (playerPosition == Vector3D.Zero) continue;
                double distance = Vector3D.Distance(PlayersCurrentPosition, playerPosition);

                if (distance <= 10000)
                {
                    Core.SendMessage(spawn.WaveMessageSender, spawn.WaveMessage, Color.Red,
                        onlinePlayer.Id.SteamId);
                }
            }

            return false;
        }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            return false;
        }

        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1);
            }

            CrunchEconV3.Core.SendMessage("Contracts", DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red, this.AssignedPlayerSteamId);
        }

        public void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Anti Piracy Operations Location");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"Anti Piracy Operations Location";
            gpsRef.GPSColor = Color.Red;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = TimeSpan.FromSeconds(6000);
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }

        public void DeleteDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            gpscol.SendDeleteGpsRequest(this.AssignedPlayerIdentityId, GpsId);
        }


        public int ReputationRequired { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public ulong AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public long DistanceReward { get; set; }
        public Vector3 DeliverLocation { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; } = "MyObjectBuilder_ContractTypeDefinition/Escort";
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
        public long DeliveryFactionId { get; set; }

        public List<BlockDestruction> BlocksToDestroy = new List<BlockDestruction>();

        public long UncollectedPay = 0;

        public List<SpawnWave> Waves = new List<SpawnWave>();
    }

    public class WaveDefenceConfig : IContractConfig
    {
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
            Waves = new List<SpawnWave>();
            Waves.Add(new SpawnWave()
            {
                WaveNumber = 1,
                SecondsBeforeNextWave = 60,
                GridsInWave = new List<GridSpawnModel>()
                {
                    new GridSpawnModel()
                }
            });
            Waves.Add(new SpawnWave()
            {
                WaveNumber = 2,
                SecondsBeforeNextWave = 60,
                GridsInWave = new List<GridSpawnModel>()
                {
                    new GridSpawnModel()
                }
            });
            BlocksToDestroy = new List<BlockDestruction>() { new BlockDestruction() };
        }

        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            if (this.ChanceToAppear < 1)
            {
                var random = CrunchEconV3.Core.random.NextDouble();
                if (random > this.ChanceToAppear)
                {
                    return null;
                }
            }
            var contract = new CrunchWaveDefenceCombatContractImplementation();
            var description = new StringBuilder();
            var contractContractType = "CrunchWaveDefence";
            contract.ContractType = contractContractType;
            contract.BlockId = idUsedForDictionary;
            contract.RewardMoney = MinimumPay;
            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Escort";
            contract.Name = $"Anti Piracy Operations";
            contract.ReputationRequired = this.ReputationRequired;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }

            contract.Waves = this.Waves;
            contract.BlocksToDestroy = this.BlocksToDestroy;
            description.AppendLine($"Reward is calculated from blocks destroyed. Minimum payout of {MinimumPay:##,###}");

            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            var min = 100;
            var max = 300;
            if (this.DeliveryGPSes.Any())
            {
                if (this.DeliveryGPSes != null && this.DeliveryGPSes.Any())
                {
                    var random = this.DeliveryGPSes.GetRandomItemFromList();
                    var GPS = GPSHelper.ScanChat(random);
                    if (GPS != null)
                    {
                        if (__instance != null)
                        {
                            var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                            if (faction != null)
                            {
                                return Tuple.Create(GPS.Coords, faction.FactionId);
                            }

                        }
                        if (keenstation != null)
                        {
                            return Tuple.Create(GPS.Coords, keenstation.FactionId);
                        }
                        return Tuple.Create(GPS.Coords, 0l);
                    }
                }
            }
            if (keenstation != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 Position = keenstation.Position;
                    Position.Add(new Vector3(Core.random.Next(min * 1000, max * 1000), Core.random.Next(min * 1000, max * 1000), Core.random.Next(min * 1000, max * 1000)));

                    if (MyGravityProviderSystem.IsPositionInNaturalGravity(Position))
                    {
                        min += 100;
                        max += 100;
                        continue;
                    }
                    return Tuple.Create(new Vector3D(Position), keenstation.FactionId);
                }
            }


            if (__instance != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 Position = __instance.PositionComp.GetPosition();
                    Position.Add(new Vector3(Core.random.Next(min * 1000, max * 1000), Core.random.Next(min * 1000, max * 1000), Core.random.Next(min * 1000, max * 1000)));
                    if (MyGravityProviderSystem.IsPositionInNaturalGravity(Position))
                    {
                        min += 100;
                        max += 100;
                        continue;
                    }
                    var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                    if (faction != null)
                    {
                        return Tuple.Create(new Vector3D(Position), faction.FactionId);
                    }
                }
            }
            return Tuple.Create(Vector3D.Zero, 0l);
        }

        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 3;
        public List<string> DeliveryGPSes { get; set; }
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public long MinimumPay { get; set; }

        public List<BlockDestruction> BlocksToDestroy = new List<BlockDestruction>();

        public List<SpawnWave> Waves = new List<SpawnWave>();

    }
    public class SpawnWave
    {
        public List<GridSpawnModel> GridsInWave = new List<GridSpawnModel>();
        public string WaveMessage = "Something a pirate would say";
        public string WaveMessageSender = "Jack Sparrow";
        public int WaveNumber = 1;
        public int SecondsBeforeNextWave = 60;
        public int MinDistance = 1000;
        public int MaxDistance = 5000;
        public bool SpawnGrids = true;
        public bool Repeat = false;
    }

    public class GridSpawnModel
    {
        public string GridName = $"pirate.sbc";
        public double ChanceToSpawn = 0.5;
        public string FacTagToOwnThisGrid = "SPRT";
    }

    public class BlockDestruction
    {
        public string BlockPairName = "LargeReactor";
        public long Payment = 50000;
    }

    [PatchShim]
    public static class SlimBlockPatch
    {
        internal static readonly MethodInfo DamageRequest =
            typeof(MySlimBlock).GetMethod("DoDamage", BindingFlags.Instance | BindingFlags.Public, null,
                new Type[]
                {
                    typeof(float), typeof(MyStringHash), typeof(bool), typeof(MyHitInfo?), typeof(long), typeof(long),
                    typeof(bool)
                }, null) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo patchSlimDamage =
            typeof(SlimBlockPatch).GetMethod(nameof(OnDamageRequest), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo destroyRequest =
            typeof(MyCubeBlock).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo patchDestroy =
            typeof(SlimBlockPatch).GetMethod(nameof(OnDestroy), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static void Patch(PatchContext ctx)
        {

            ctx.GetPattern(DamageRequest).Suffixes.Add(patchSlimDamage);
            ctx.GetPattern(destroyRequest).Suffixes.Add(patchDestroy);
            Core.Log.Info("Patching slim block");
        }

        public static Dictionary<long, long> LastAttacker = new Dictionary<long, long>();
        public static void OnDestroy(MyCubeBlock __instance)
        {
            if (!LastAttacker.TryGetValue(__instance.EntityId, out var attacker)) return;
            if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)attacker, out var player)) return;
            var playerData = Core.PlayerStorage.GetData((ulong)attacker, false);
            var forCombat = playerData.GetContractsForType("CrunchWaveDefence");
            foreach (var contract in from contract in forCombat let location = contract.DeliverLocation let distance = Vector3.Distance(location, __instance.CubeGrid.PositionComp.GetPosition()) where !(distance > 50000) select contract)
            {
                //     Core.Log.Info("block death");
                var combat = contract as CrunchWaveDefenceCombatContractImplementation;
                if (combat == null || !combat.BlocksToDestroy.Any(x => x.BlockPairName == __instance.BlockDefinition?.BlockPairName)) continue;

                if (!combat.Waves.Any(x => x.GridsInWave.Any(z => z.FacTagToOwnThisGrid == __instance.GetOwnerFactionTag()))) continue;

                var pay = combat.BlocksToDestroy.FirstOrDefault(x => x.BlockPairName == __instance.BlockDefinition?.BlockPairName)?.Payment ?? 0;
                combat.UncollectedPay += pay;
                playerData.PlayersContracts[contract.ContractId] = combat;
                Task.Run(async () =>
                {
                    CrunchEconV3.Core.PlayerStorage.Save(playerData);
                });
                return;


            }
        }

        public static void OnDamageRequest(MySlimBlock __instance, float damage,
        MyStringHash damageType,
        bool sync,
        MyHitInfo? hitInfo,
        long attackerId, long realHitEntityId = 0, bool shouldDetonateAmmo = true)
        {
            if (__instance.FatBlock != null)
            {
                var attacker = GetAttacker(attackerId);

                var steam = MySession.Static.Players.TryGetSteamId(attacker);
                //    Core.Log.Info("Adding attacker");
                if (steam != 0l)
                {
                    //      Core.Log.Info("steam id attacker");
                    if (LastAttacker.ContainsKey(__instance.FatBlock.EntityId))
                    {
                        LastAttacker[__instance.FatBlock.EntityId] = (long)steam;
                    }
                    else
                    {
                        LastAttacker.Add(__instance.FatBlock.EntityId, (long)steam);
                    }
                }
            }
            return;
        }





        public static long GetAttacker(long attackerId)
        {

            var entity = MyAPIGateway.Entities.GetEntityById(attackerId);

            if (entity == null)
                return 0L;

            if (entity is MyPlanet)
            {

                return 0L;
            }

            if (entity is MyCharacter character)
            {

                return character.GetPlayerIdentityId();
            }

            if (entity is IMyEngineerToolBase toolbase)
            {

                return toolbase.OwnerIdentityId;

            }

            if (entity is MyLargeTurretBase turret)
            {

                return turret.OwnerId;

            }

            if (entity is MyShipToolBase shipTool)
            {

                return shipTool.OwnerId;
            }


            if (entity is IMyGunBaseUser gunUser)
            {

                return gunUser.OwnerId;

            }

            if (entity is MyFunctionalBlock block)
            {

                return block.OwnerId;
            }

            if (entity is MyCubeGrid grid)
            {

                var gridOwnerList = grid.BigOwners;
                var ownerCnt = gridOwnerList.Count;
                var gridOwner = 0L;

                if (ownerCnt > 0 && gridOwnerList[0] != 0)
                    gridOwner = gridOwnerList[0];
                else if (ownerCnt > 1)
                    gridOwner = gridOwnerList[1];

                return gridOwner;

            }

            return 0L;
        }
    }


}
