using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Models.Contracts
{
    public class CrunchItemHaulingContractImplementation : ICrunchContract
    {
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
            var contractDescription = $"You must go deliver {this.ItemToDeliver.AmountToDeliver:##,###} {this.ItemToDeliver.TypeId.Replace("MyObjectBuilder_", "")} {this.ItemToDeliver.SubTypeId}";

            contractDescription += ($" ||| Distance bonus applied {this.DistanceReward:##,###}");

            return BuildUnassignedContract(contractDescription);
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
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
        public void SendDeliveryGPS()
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

        public void DeleteDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            gpscol.SendDeleteGpsRequest(this.AssignedPlayerIdentityId, GpsId);
        }
        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
            SendDeliveryGPS();
        }

        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (DateTime.Now > ExpireAt)
            {
                FailContract();
                return true;
            }

            return false;
        }
        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
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
                    this.FactionId, this.ReputationGainOnComplete, true);
            }

            return true;
        }

        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1);
            }

            Core.SendMessage("Contracts",
                DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red,
                this.AssignedPlayerSteamId);
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

        public long DeliveryFactionId { get; set; }

        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
        public ItemToDeliver ItemToDeliver { get; set; }
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
            }
            var contract = new CrunchItemHaulingContractImplementation();

            var description = new StringBuilder();
            contract.ContractType = "CrunchItemTransport";
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
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }
            if (this.BonusPerKMDistance != 0)
            {
                var distance = Vector3.Distance(contract.DeliverLocation, __instance != null ? __instance.PositionComp.GetPosition() : keenstation.Position);
                var division = distance / 1000;
                var distanceBonus = (long)(division * this.BonusPerKMDistance);
                if (distanceBonus > 0)
                {
                    contract.DistanceReward += distanceBonus;
                }
            }

            description.AppendLine($"Deliver {contract.ItemToDeliver.AmountToDeliver} {contract.ItemToDeliver.TypeId.Replace("MyObjectBuilder_", "")} {contract.ItemToDeliver.SubTypeId}");

            description.AppendLine($" ||| Distance bonus applied {contract.DistanceReward:##,###}");

            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            if (keenstation != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    //this will only pick stations from the same faction
                    //var found = StationHandler.KeenStations.Where(x => x.FactionId == keenstation.FactionId).ToList().GetRandomItemFromList();
                    var found = StationHandler.KeenStations.GetRandomItemFromList();
                    var foundFaction = MySession.Static.Factions.TryGetFactionById(found.FactionId);
                    if (foundFaction == null)
                    {
                        i++;
                        continue;
                    }

                    return Tuple.Create(found.Position, foundFaction.FactionId);
                }
            }

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
            var thisStation = StationHandler.GetStationNameForBlock(idUsedForDictionary);
            for (int i = 0; i < 10; i++)
            {

                var station = Core.StationStorage.GetAll().Where(x => x.UseAsDeliveryLocation).ToList().GetRandomItemFromList();
                if (station.FileName == thisStation)
                {
                    i++;
                    continue;
                }
                var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                var GPS = GPSHelper.ScanChat(station.LocationGPS);
                return Tuple.Create(GPS.Coords, foundFaction.FactionId);
            }

            return Tuple.Create(Vector3D.Zero, 0l);
        }

        public int AmountOfContractsToGenerate { get; set; } = 3;
        public string ContractName { get; set; } = "Item Delivery";
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 3;
        public List<string> DeliveryGPSes { get; set; }
        public long BonusPerKMDistance { get; set; } = 1;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public List<ItemHaul> ItemsAvailable { get; set; }
    }

    public class ItemHaul
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int AmountMin { get; set; }
        public int AmountMax { get; set; }
        public int PricePerItemMin { get; set; }
        public int PricePerItemMax { get; set; }
    }

    public class ItemToDeliver
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int AmountToDeliver { get; set; }
        public long Pay { get; set; }

        public static explicit operator ItemToDeliver(ItemHaul v)
        {
            var amount = Core.random.Next(v.AmountMin, v.AmountMax);
            return new ItemToDeliver()
            {
                AmountToDeliver = amount,
                Pay = Core.random.Next(v.PricePerItemMin, v.PricePerItemMax) * amount,
                SubTypeId = v.SubTypeId,
                TypeId = v.TypeId,
            };
        }
    }
}
