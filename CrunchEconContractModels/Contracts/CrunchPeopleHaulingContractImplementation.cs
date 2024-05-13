using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
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
    public class CrunchPeopleHaulingContractImplementation : ICrunchContract
    {
        public string ContractType { get; set; }
        public bool ConsumeOxygen { get; set; } = false;
        public long OxygenLitrePerPassenger { get; set; } = 1000;
        public int SecondsBetweenOxygenChecks { get; set; } = 60;
        public DateTime NextOxygenCheck { get; set; }
        public int DeadPassengers { get; set; }
        public double PercentDeathsPerFail { get; set; } = 0.1;
        public double BaseRewardPerPassenger { get; set; }
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
            var contractDescription = $"You must go deliver {this.PassengerCount} passengers, using the ship that accepted the contract.";
            contractDescription += $" ||| Distance bonus: {this.DistanceReward:##,###}";
            if (this.ConsumeOxygen)
            {
                contractDescription += $" ||| Oxygen Consumption: {this.OxygenLitrePerPassenger * this.PassengerCount:##,###}, per {this.SecondsBetweenOxygenChecks} seconds.";
            }

            foreach (var passengerBlock in this.PassengerBlocks)
            {
                contractDescription += $" ||| block {passengerBlock.BlockPairName} provides {passengerBlock.PassengerSpace} capacity";
            }
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

            var test = __instance.CubeGrid.GetGridGroup(GridLinkTypeEnum.Physical);
            var grids = new List<IMyCubeGrid>();
            test.GetGrids(grids);
            var capacity = 0;
            foreach (var gridInGroup in grids)
            {

                var owner = FacUtils.IsOwnerOrFactionOwned(gridInGroup as MyCubeGrid, identityId, true);
                if (owner)
                {
                    capacity += PassengerTransportUtils.GetPassengerCount(gridInGroup as MyCubeGrid, this);
                }
            }

            if (capacity <= 0)
            {
                return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
            }

            var max = capacity;
            var calculated = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(identityId,
                    this.FactionId);
            var maximumPossiblePassengers =
                calculated.Item2 * this.ReputationMultiplierForMaximumPassengers;
            if (maximumPossiblePassengers < max)
            {
                max = (int)maximumPossiblePassengers;
            }

            if (max < 1)
            {
                max = 1;
            }

            this.BaseRewardPerPassenger = this.RewardMoney;
            this.PassengerCount = max;
            this.RewardMoney *= this.PassengerCount;
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
            sb.AppendLine("Deliver passengers to");
            sb.AppendLine("Contract Delivery Location.");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"Passenger Delivery Location";
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

            if (this.ConsumeOxygen && this.NextOxygenCheck <= DateTime.Now)
            {
                NextOxygenCheck = DateTime.Now.AddSeconds(this.SecondsBetweenOxygenChecks);
                var toConsume = this.PassengerCount * this.OxygenLitrePerPassenger;
                MySession.Static.Players.TryGetPlayerBySteamId(this.AssignedPlayerSteamId, out var player);
                MyCubeGrid playersGrid;
                if (!(player?.Controller?.ControlledEntity is MyCockpit))
                {
                 //   Core.Log.Info("players grid is null");
                    var sphere = new BoundingSphereD(PlayersCurrentPosition, 1000 * 2);
                    var playersGrids = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                        .Where(x => x.BlocksCount > 0 && FacUtils.IsOwnerOrFactionOwned(x, this.AssignedPlayerIdentityId, true)).ToList();

                    if (playersGrids.Any())
                    {
                        playersGrid = playersGrids.First().GetBiggestGridInGroup();
                    }
                    else
                    {
                        double dying = PassengerCount * PercentDeathsPerFail;
                        if (dying < 1)
                        {
                            dying = 1;
                        }

                        DeadPassengers += (int)dying;
                        this.PassengerCount -= (int)dying;
                        return false;
                    }
                }
                else
                {
                    var cockpit = player?.Controller?.ControlledEntity as MyCockpit;
                    playersGrid = cockpit.CubeGrid;
                }

                if (playersGrid == null)
                {
                    KillPassengers();
                    return false;
                }
                var test = playersGrid.GetGridGroup(GridLinkTypeEnum.Physical);
                var grids = new List<IMyCubeGrid>();
                var tanks = new List<IMyGasTank>();

                test.GetGrids(grids);
             //   grids.Add(playersGrid);
                foreach (var gridInGroup in grids)
                {
                    tanks.AddRange(gridInGroup.GetFatBlocks<IMyGasTank>());
                }
                
                var playerTanks = TankHelper.MakeTankGroup(tanks, this.AssignedPlayerIdentityId, 0, "Oxygen");
             //   Core.Log.Info(playerTanks.Capacity);
             //   Core.Log.Info(playerTanks.TanksInGroup.Count);
             //   Core.Log.Info(playerTanks.GasInTanks);
                if (playerTanks.GasInTanks <= toConsume)
                {
                    KillPassengers();
                }
                else
                {
                    Core.SendMessage($"Contracts", $"{toConsume:##,##} Litres of Oxygen Consumed.", Color.Green, this.AssignedPlayerSteamId);
                    TankHelper.RemoveGasFromTanksInGroup(playerTanks, toConsume);
                }
            }

            if (PassengerCount <= 0)
            {
                FailContract();
                return true;
            }
            return false;
        }

        private void KillPassengers()
        {
            double dying = PassengerCount * PercentDeathsPerFail;
            if (dying < 1)
            {
                dying = 1;
            }

            DeadPassengers += (int)dying;
            this.PassengerCount -= (int)dying;
            Core.SendMessage($"Contracts", $"{(int)dying} passengers have suffocated.", Color.DarkRed,
                this.AssignedPlayerSteamId);
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

            var passengerCount = 0;

            foreach (var grid in playersGrids)
            {
                passengerCount += PassengerTransportUtils.GetPassengerCount(grid, this);
            }
            if (passengerCount < this.PassengerCount)
            {
                return false;
            }

            if (DeadPassengers != 0)
            {
                var alive = this.PassengerCount - this.DeadPassengers;
                if (alive >= 1)
                {
                    this.RewardMoney = (long)(this.BaseRewardPerPassenger * alive + this.DistanceReward);
                }
                else
                {
                    this.RewardMoney = 0;
                }
                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);
                if (this.ReputationLossOnAbandon != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1,ReputationChangeReason.Contract);
                }
                Core.SendMessage("Contracts", $"{this.Name} completed, passengers have died. You have been docked pay.", Color.Green, this.AssignedPlayerSteamId);
                return true;
            }
            else
            {
                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney + this.DistanceReward);
                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
                }
                Core.SendMessage("Contracts", $"{this.Name} completed.", Color.Green, this.AssignedPlayerSteamId);
            }

            return true;
        }

        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1, ReputationChangeReason.Contract);
            }

            if (this.PassengerCount <= 0)
            {
                Core.SendMessage("Contracts", $"{this.Name} failed. All Passengers have suffocated.", Color.Red,
                    this.AssignedPlayerSteamId);
            }
            else
            {
                Core.SendMessage("Contracts",
                    DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red,
                    this.AssignedPlayerSteamId);
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
        public List<PassengerBlock> PassengerBlocks { get; set; }
        public int PassengerCount { get; set; }
        public double ReputationMultiplierForMaximumPassengers { get; set; } = 0.3;
        public bool CanAutoComplete { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }

        public long DeliveryFactionId { get; set; }

        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
    }
    public static class PassengerTransportUtils
    {
        public static int GetPassengerCount(MyCubeGrid grid, CrunchPeopleHaulingContractImplementation ContractImplementation)
        {
            var count = 0;
            foreach (var passengerBlock in ContractImplementation.PassengerBlocks)
            {
                var blockCount = grid.GetBlocks()
                    .Count(x => x.BlockDefinition != null && x.BlockDefinition?.BlockPairName == passengerBlock.BlockPairName);
                count += blockCount * passengerBlock.PassengerSpace;
            }

            return count;
        }
    }

    public class PeopleHaulingContractConfig : IContractConfig
    {       //check the discord for documentation on what each thing in the interface does 
        //https://discord.gg/cQFJeKvVAA
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
            PassengerBlocksAvailable = new List<PassengerBlock>()
            {
                new PassengerBlock()
                {
                    BlockPairName = "Bed",
                    PassengerSpace = 2
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
            var contract = new CrunchPeopleHaulingContractImplementation();
            contract.RewardMoney = Core.random.Next((int)this.PricePerPassengerMin,
                (int)this.PricePerPassengerMax);

            var description = new StringBuilder();
            contract.ContractType = "CrunchPeopleTransport";
            contract.BlockId = idUsedForDictionary;
            contract.CanAutoComplete = false;
            contract.PassengerBlocks = this.PassengerBlocksAvailable;
            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.Name = $"People Transport";
            contract.ReputationRequired = this.ReputationRequired;
            contract.CanAutoComplete = true;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            contract.ConsumeOxygen = this.ConsumeOxygen;
            contract.OxygenLitrePerPassenger = this.OxygenLitrePerPassenger;
            contract.SecondsBetweenOxygenChecks = this.SecondsBetweenOxygenChecks;
            contract.PercentDeathsPerFail = this.PercentDeathsPerFail;
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

            description.AppendLine($"Reward = {contract.RewardMoney} multiplied by Passenger count");
            description.AppendLine($" ||| Maximum possible passengers: {1500 * this.ReputationMultiplierForMaximumPassengers}");
            if (this.ConsumeOxygen)
            {
                description.AppendLine($"||| Oxygen Consumption: {this.OxygenLitrePerPassenger:##,###} per passenger, per {this.SecondsBetweenOxygenChecks} seconds.");
            }

            foreach (var passengerBlock in this.PassengerBlocksAvailable)
            {
                description.AppendLine($"||| {passengerBlock.BlockPairName} provides {passengerBlock.PassengerSpace} capacity");
            }

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
            var keenEndResult = StationHandler.KeenStations.GetRandomItemFromList();
            if (keenEndResult != null)
            {
                var foundFaction = MySession.Static.Factions.TryGetFactionById(keenEndResult.FactionId);
                if (foundFaction != null)
                {
                    return Tuple.Create(keenEndResult.Position, foundFaction.FactionId);
                }
            }
            return Tuple.Create(Vector3D.Zero, 0l);
        }
        public bool ConsumeOxygen { get; set; } = false;
        public long OxygenLitrePerPassenger { get; set; } = 1000;
        public int SecondsBetweenOxygenChecks { get; set; } = 60;
        public DateTime NextOxygenCheck { get; set; }
        public int DeadPassengers { get; set; }
        public double PercentDeathsPerFail { get; set; } = 0.1;
        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 3;
        public List<string> DeliveryGPSes { get; set; }
        public long PricePerPassengerMin { get; set; } = 1;
        public long PricePerPassengerMax { get; set; } = 3;
        public long BonusPerKMDistance { get; set; } = 1;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public double ReputationMultiplierForMaximumPassengers { get; set; } = 0.3;
        public List<PassengerBlock> PassengerBlocksAvailable { get; set; }
    }
}
