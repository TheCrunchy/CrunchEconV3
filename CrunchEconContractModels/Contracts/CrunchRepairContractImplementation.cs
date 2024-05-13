using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class CrunchRepairContractImplementation : ICrunchContract
    {
        public string ContractType { get; set; }
        private MyCubeGrid Grid { get; set; }
        public long GridEntityId { get; set; }
        public bool HasSpawnedGrid { get; set; } = false;
        public string PrefabToSpawn { get; set; }
        public bool DeleteGridOnComplete { get; set; }
        private bool brokeBlocks { get; set; } = false;

        public int BlocksToRepair { get; set; }

        //no cheeky repairing the same block
        public List<long> BlockIdsToRepair { get; set; }

        public DateTime NextMessage = DateTime.Now;
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
            var contractDescription = $"You must repair the grid found at the repair location GPS.";
            return BuildUnassignedContract(contractDescription);
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (this.DeliverLocation.Equals(Vector3.Zero))
            {
                Core.Log.Error("Error getting a repair point for this contract");
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
            BlockIdsToRepair = new List<long>();
            return Tuple.Create(true, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
        }

        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
            this.ReadyToDeliver = false;
            SendDeliveryGPS();
        }

        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (this.brokeBlocks)
            {
                return true;
            }

            if (HasSpawnedGrid && GetGrid() != null)
            {
                foreach (var block in GetGrid().GetFatBlocks().Where(x => !x.IsFunctional))
                {
                    block.SlimBlock.IncreaseMountLevel(100, block.OwnerId);
                }

                BlocksToRepair = GetGrid().GetFatBlocks().Count(x => !x.IsFunctional);
            }
            if (GetGrid() != null && !HasSpawnedGrid)
            {
                GetGrid().OnBlockRemoved += BlockRemoved;
                HasSpawnedGrid = true;
                return false;
            }

            if (this.BlocksToRepair > 0 && DateTime.Now >= NextMessage)
            {
                NextMessage = NextMessage.AddMinutes(1);
                Core.SendMessage("Contracts", $"{this.BlocksToRepair} blocks left to repair.", Color.Yellow,
                    AssignedPlayerSteamId);
            }
            if (this.BlocksToRepair <= 0 && HasSpawnedGrid)
            {
                return TryCompleteContract(this.AssignedPlayerSteamId, PlayersCurrentPosition);
            }

            if (DateTime.Now > ExpireAt)
            {
                FailContract();
                return true;
            }

            if (HasSpawnedGrid && GetGrid() != null)
            {
                return false;
            }

            if (HasSpawnedGrid)
            {
                if (GetGrid() == null)
                {
                    return false;
                }
            }

            var distance = Vector3.Distance(PlayersCurrentPosition, DeliverLocation);
            if (distance > 5000)
            {
                return false;
            }
            var faction = MySession.Static.Factions.TryGetFactionById(this.FactionId);
            if (faction == null)
            {
                Core.Log.Info($"{this.FactionId} faction not found");
                return false;
            }
            Vector3 Position = new Vector3D(PlayersCurrentPosition);

            //Position.Add(new Vector3(Core.random.Next(-5000, 5000),
            //    Core.random.Next(-5000, 5000),
            //    Core.random.Next(-5000, 5000)));

            if (!File.Exists($"{Core.path}//Grids//{this.PrefabToSpawn}")) return false;


            var Ids = GridManagerUpdated.LoadGrid($"{Core.path}//Grids//{this.PrefabToSpawn}", DeliverLocation, false,
                (ulong)faction.Members.FirstOrDefault().Key, this.PrefabToSpawn.Replace(".sbc", ""), false);
            if (!Ids.Any())
            {
                Core.Log.Info($"Could not load grid {this.PrefabToSpawn}");
            }
            else
            {
                var main = Ids.OrderByDescending(x => x.BlocksCount).FirstOrDefault();
                Grid = main;
                GridEntityId = Grid.EntityId;
            }

            return false;
        }

        private MyCubeGrid GetGrid()
        {
            if (Grid != null)
            {
                return Grid;
            }
            var found = MyAPIGateway.Entities.GetEntityById(GridEntityId);
            if (found == null) return null;
            Grid = (MyCubeGrid)found;
            return Grid;
        }

        private void BlockRemoved(MySlimBlock obj)
        {
            if (obj.FatBlock is IMyDoor) return;
            this.brokeBlocks = true;
            FailContract();
        }

        public void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Repair Grid Found At");
            sb.AppendLine("Repair Location.");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"--> REPAIR HERE <--";
            gpsRef.GPSColor = Color.Orange;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = new TimeSpan?();
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }

        public void DeleteDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            gpscol.SendDeleteGpsRequest(this.AssignedPlayerIdentityId, GpsId);
        }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            try
            {
                if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                    return false;

                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
                }
                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);

                if (this.DeleteGridOnComplete && GetGrid() != null)
                {
                    GetGrid().Close();
                }
                CrunchEconV3.Core.SendMessage("Contracts", $"{this.Name} completed, you have been paid.", Color.Green, this.AssignedPlayerSteamId);
                return true;
            }
            catch (Exception e)
            {
                Core.Log.Error($"Repair try complete error {e}");
                return true;
            }
        }

        public void FailContract()
        {

            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1, ReputationChangeReason.Contract);
            }
            if (brokeBlocks)
            {
                CrunchEconV3.Core.SendMessage("Contracts", $"{this.Name} failed, non-door blocks broken.", Color.Red, this.AssignedPlayerSteamId);
            }
            else
            {
                CrunchEconV3.Core.SendMessage("Contracts", DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red, this.AssignedPlayerSteamId);
            }

            if (this.DeleteGridOnComplete && GetGrid() != null)
            {
                GetGrid().Close();
            }
        }

        public int ReputationRequired { get; set; }
        public long ContractId { get; set; }
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
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
        public long DeliveryFactionId { get; set; }
    }

    public class RepairContractConfig : IContractConfig
    {
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
            PrefabNames = new List<string>() { "Pirate.sbc" };
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
            var description = new StringBuilder();
            var contract = new CrunchRepairContractImplementation();
            var prefabName = PrefabNames.GetRandomItemFromList();
            contract.RewardMoney = CrunchEconV3.Core.random.Next((int)this.RewardMin, (int)this.RewardMax);
            contract.ContractType = "CrunchRepair";
            contract.BlockId = idUsedForDictionary;
            contract.DeleteGridOnComplete = this.DeleteGridOnCompletion;
            contract.ReputationGainOnComplete = CrunchEconV3.Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Repair";
            contract.Name = $"Repair {prefabName.Replace(".sbc", "")}";
            contract.ReputationRequired = this.ReputationRequired;
            contract.ReadyToDeliver = true;
            contract.PrefabToSpawn = prefabName;
            contract.CollateralToTake = (CrunchEconV3.Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            description.AppendLine($"{this.Description.Replace("{prefabName}", prefabName.Replace(".sbc", ""))}");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }
            contract.Description = description.ToString();
            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary)
        {
            var min = MinSpawnRangeInKM * 1000;
            var max = MaxSpawnRangeInKM * 1000;
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

                    Vector3D randomDirection = MyUtils.GetRandomVector3Normalized();

                    // Generate a random distance within the specified range
                    double randomDistance = MyUtils.GetRandomDouble(min, max);

                    // Calculate the new position by adding the random direction multiplied by the random distance
                    Vector3 Position = keenstation.Position + randomDirection * randomDistance;

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
                    // Generate a random direction vector
                    Vector3D randomDirection = MyUtils.GetRandomVector3Normalized();

                    // Generate a random distance within the specified range
                    double randomDistance = MyUtils.GetRandomDouble(min, max);

                    // Calculate the new position by adding the random direction multiplied by the random distance
                    Vector3D Position = __instance.CubeGrid.PositionComp.GetPosition() + randomDirection * randomDistance;
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

        public int AmountOfContractsToGenerate { get; set; } = 2;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public int ReputationRequired { get; set; } = 0;
        public float ChanceToAppear { get; set; } = 1;
        public long CollateralMin { get; set; } = 1000;
        public long CollateralMax { get; set; } = 5000;
        public List<string> DeliveryGPSes { get; set; }
        public List<string> PrefabNames { get; set; }
        public long RewardMin { get; set; } = 50;
        public long RewardMax { get; set; } = 75;
        public bool DeleteGridOnCompletion { get; set; } = true;
        public string Description = "You must repair {prefabName} found at the repair location";
        public int MinSpawnRangeInKM { get; set; } = 50;
        public int MaxSpawnRangeInKM { get; set; } = 75;
    }
}
