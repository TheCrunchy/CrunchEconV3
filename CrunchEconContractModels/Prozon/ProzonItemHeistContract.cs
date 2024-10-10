using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Models.ContractStuff;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Prozon
{
    public class ProzonItemHeistContract : ContractAbstract
    {
        public List<VRage.Game.ModAPI.IMyInventory> GetStationInventories(MyCubeGrid grid)
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<VRage.Game.ModAPI.IMyInventory>();
            var gridOwnerFac = FacUtils.GetOwner(grid);

            foreach (var block in grid.GetFatBlocks().Where(x => x.OwnerId == gridOwnerFac))
            {
                if (block.DisplayNameText != null && !this.CargoNames.Contains(block.DisplayNameText))
                {
                    continue;
                }

                for (int i = 0; i < block.InventoryCount; i++)
                {
                    VRage.Game.ModAPI.IMyInventory inv = ((VRage.Game.ModAPI.IMyCubeBlock)block).GetInventory(i);
                    inventories.Add(inv);
                }
            }
            return inventories;
        }

        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must go obtain {this.ItemToDeliver.AmountToDeliver:##,###} {this.ItemToDeliver.TypeId.Replace("MyObjectBuilder_", "")} {this.ItemToDeliver.SubTypeId} from the target, then deliver it back here.";

            return BuildUnassignedContract(contractDescription);
        }

        public override Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (this.DeliverLocation.Equals(Vector3.Zero))
            {
                Core.Log.Error("Error getting a delivery point for this contract");
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

            this.ReadyToDeliver = true;

            if (this.CollateralToTake > 0)
            {
                EconUtils.takeMoney(identityId, this.CollateralToTake);
            }

            this.AssignedPlayerIdentityId = identityId;
            this.AssignedPlayerSteamId = playerData.PlayerSteamId;
            return Tuple.Create(true, MyContractResults.Success);
        }
        public override void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Deliver items to");
            sb.AppendLine("Hauling Contract Delivery Location.");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"Hauling Delivery Location";
            gpsRef.GPSColor = Color.Orange;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = new TimeSpan?();
            gpsRef.UpdateHash();
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }

        public bool HasStarted = false;
        public string CommandToExecute;
        public Vector3 SpawnLocation { get; set; }
        public override bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (DateTime.Now > ExpireAt)
            {
                FailContract();
                return true;
            }

            if (HasStarted)
            {

                return TryCompleteContract(this.AssignedPlayerSteamId, PlayersCurrentPosition);
            }
            
            var distance = Vector3.Distance(PlayersCurrentPosition, SpawnLocation);
            if (distance <= 15000)
            {

                if (MySession.Static.Players.TryGetPlayerBySteamId(this.AssignedPlayerSteamId, out var player))
                {
                    Core.MesAPI.ChatCommand(CommandToExecute, player.Character.WorldMatrix, AssignedPlayerIdentityId,
                        AssignedPlayerSteamId);
                    HasStarted = true;
                }
            }
            return false;
        }
        public override bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                return false;

            float distance = Vector3.Distance(this.DeliverLocation, (Vector3)currentPosition);
            if (!(distance <= 500)) return false;

            var sphere = new BoundingSphereD(this.DeliverLocation, 1000 * 2);
            var playersGrids = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                .Where(x => x.BlocksCount > 0 && FacUtils.IsOwnerOrFactionOwned(x, this.AssignedPlayerIdentityId, true)).ToList();


            Dictionary<MyDefinitionId, int> itemsToRemove = new Dictionary<MyDefinitionId, int>();
            var parseThis = $"{ItemToDeliver.TypeId}/" + this.ItemToDeliver.SubTypeId;
            if (MyDefinitionId.TryParse(parseThis, out MyDefinitionId id))
            {
                itemsToRemove.Add(id, this.ItemToDeliver.AmountToDeliver);
            }

            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<IMyInventory>();
            foreach (var grid in playersGrids)
            {
                inventories.AddRange(InventoriesHandler.GetInventoriesForContract(grid));
            }

            if (!InventoriesHandler.ConsumeComponents(inventories, itemsToRemove, player.Id.SteamId)) return false;


            EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney + this.DistanceReward);
            if (this.ReputationGainOnComplete != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                    this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
            }

            inventories.Clear();

            if (this.PlaceItemsInTargetStation)
            {
                var foundCargo = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                    .Where(x => x.BlocksCount > 0).ToList();
                if (foundCargo.Any())
                {
                    foreach (var cargo in foundCargo)
                    {

                        var owner = FacUtils.GetOwner(cargo);
                        var fac = MySession.Static.Factions.TryGetPlayerFaction(owner);

                        if (fac != null && fac.FactionId == this.DeliveryFactionId)
                        {
                            inventories.AddRange(GetStationInventories(cargo));
                        }
                    }

                    InventoriesHandler.SpawnItems(id, this.ItemToDeliver.AmountToDeliver, inventories);
                }
            }

            return true;
        }

        public ItemToDeliver ItemToDeliver { get; set; }
        public bool PlaceItemsInTargetStation { get; set; }
        public List<string> CargoNames = new List<string>();

    }


    public class ItemHaulingContractConfig : IContractConfig
    {
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
            ItemsAvailable = new List<ItemHaul>()
            {
                new ItemHaul()
                {
                    TypeId = "MyObjectBuilder_Ingot",
                    SubTypeId = "Iron",
                    AmountMax = 50000,
                    AmountMin = 25000
                }
            };
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
                this.CargoNames = new List<string>() { "Cargo1", "Cargo2" };
            }
            var contract = new ProzonItemHeistContract();

            var description = new StringBuilder();
            contract.ContractType = "ProzonHeistContract";
            contract.BlockId = idUsedForDictionary;
            contract.ItemToDeliver = (ItemToDeliver)this.ItemsAvailable.GetRandomItemFromList();
            contract.RewardMoney = contract.ItemToDeliver.Pay;

            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.Name = this.ContractName;
            contract.ReputationRequired = this.ReputationRequired;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            var result = GetLocation(__instance, keenstation, idUsedForDictionary);
            var result2 = GetLocation(__instance, keenstation, idUsedForDictionary);
            contract.SpawnLocation = result.Item1;
            contract.DeliverLocation = result2.Item1;
            contract.DeliveryFactionId = result.Item2;
            contract.CargoNames = this.CargoNames;
            contract.PlaceItemsInTargetStation = this.PlaceItemsInTargetStation;
            contract.CommandToExecute = this.CommandToRun.Split(',').GetRandomItem();
            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }
            description.AppendLine($"Deliver {contract.ItemToDeliver.AmountToDeliver} {contract.ItemToDeliver.TypeId.Replace("MyObjectBuilder_", "")} {contract.ItemToDeliver.SubTypeId}");


            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }
        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            List<Tuple<Vector3D, long>> availablePositions = new List<Tuple<Vector3D, long>>();

            // If it's not a custom station, get random keen ones
            var thisStation = StationHandler.GetStationNameForBlock(idUsedForDictionary);
            if (thisStation != null)
            {
                var stations = Core.StationStorage.GetAll()
                    .Where(x => x.UseAsDeliveryLocation && !string.Equals(x.FileName, thisStation))
                    .ToList();

                foreach (var station in stations)
                {
                    var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                    var GPS = GPSHelper.ScanChat(station.LocationGPS);
                    availablePositions.Add(Tuple.Create(GPS.Coords, foundFaction.FactionId));
                }
            }

            if (MySession.Static.Settings.EnableEconomy)
            {
                var positions = MySession.Static.Factions.GetNpcFactions()
                    .Where(x => x.Stations.Any())
                    .SelectMany(x => x.Stations)
                    .Where(x => x.StationEntityId != keenstation?.StationEntityId)
                    .Select(x => Tuple.Create(x.Position, x.FactionId))
                    .ToList();
                availablePositions.AddRange(positions);
            }


            return availablePositions.GetRandomItemFromList() ?? Tuple.Create(Vector3D.Zero, 0l);
        }

        public Tuple<Vector3D, long> GetLocation(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            if (this.DeliveryGPSes.Any())
            {
                if (this.DeliveryGPSes != null && this.DeliveryGPSes.Any())
                {
                    var random = this.DeliveryGPSes.GetRandomItemFromList();
                    var GPS = GPSHelper.ScanChat(random);
                    if (GPS != null)
                    {
                        return Tuple.Create(GPS.Coords, 0l);
                    }
                }
            }
            return Tuple.Create(Vector3D.Zero, 0l);
        }

        public int AmountOfContractsToGenerate { get; set; } = 3;
        public string ContractName { get; set; } = "Heist Contract";
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 3;
        [JsonIgnore]
        public List<string> DeliveryGPSes { get; set; }
        public List<string> TargetLocations { get; set; }

        public string CommandToRun { get; set; }

        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public List<ItemHaul> ItemsAvailable { get; set; }
        public bool PlaceItemsInTargetStation { get; set; }
        public List<string> CargoNames = new List<string>();
    }

   
}
