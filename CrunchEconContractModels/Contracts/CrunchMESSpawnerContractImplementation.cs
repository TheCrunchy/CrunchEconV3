using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.APIs;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ModAPI;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts.MES
{
    public class CrunchMESSpawnerContractImplementation : ContractAbstract
    {
        public override string GetStatus()
        {
            return $"{this.Name}";
        }
        private bool HasPower(VRage.Game.ModAPI.IMyCubeGrid grid)
        {
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            foreach (var block in blocks)
            {
                var terminalBlock = block.FatBlock as IMyTerminalBlock;
                if (terminalBlock != null)
                {
                    MyResourceSourceComponent powerProducer;
                    if (terminalBlock.Components.TryGet(out powerProducer) && powerProducer.CurrentOutput > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private bool HasActiveThrusters(IMyCubeGrid grid)
        {
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            foreach (var block in blocks)
            {
                var terminalBlock = block.FatBlock as IMyTerminalBlock;
                if (terminalBlock != null && terminalBlock is IMyThrust)
                {
                    var thrust = terminalBlock as IMyThrust;
                    if (thrust.IsWorking)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            return BuildUnassignedContract(Description);
        }

        public override Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId,
            MyContractBlock __instance)
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
        private bool HasStarted = false;

        public string CommandToExecute { get; set; }
        public int DistanceBeforeSpawnAtGPSInKM { get; set; }

        private void OnEntityAdd(IMyEntity entity)
        {
            if (entity is IMyCubeGrid grid)
            {
                var distance = Vector3.Distance(grid.PositionComp.GetPosition(), this.DeliverLocation);
                if (distance <= this.DistanceBeforeSpawnAtGPSInKM + 10000)
                {
                    var gridOwner = FacUtils.GetFactionTag(FacUtils.GetOwner(grid as MyCubeGrid));
                    if (string.IsNullOrEmpty(gridOwner) || gridOwner == null)
                    {
                        return;
                    }
                    var payForThis = GridsToDestroy.FirstOrDefault(x => x.GridToDestroy == grid.DisplayName && gridOwner == x.FacTagToOwnThisGrid);
                    if (payForThis != null)
                    {
                        var cube = grid as MyCubeGrid;
                        MappedGrids.Add(grid.EntityId, grid as MyCubeGrid);
                        GridIdsToPay.Add(grid.EntityId, payForThis.Payment);
                        StartingBlockCounts.Add(grid.EntityId, cube.BlocksCount);
                    }

                }
            }
        }

        public override bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (DateTime.Now > ExpireAt)
            {
                TryCompleteContract(this.AssignedPlayerSteamId, PlayersCurrentPosition);
                return true;
            }

            if (HasStarted)
            {
                if (this.UncollectedPay >= this.RewardMoney)
                {
                    return TryCompleteContract(this.AssignedPlayerSteamId, PlayersCurrentPosition);
                }
                var temp = new List<long>();
                foreach (var item in GridIdsToPay)
                {
                    if (MappedGrids.TryGetValue(item.Key, out var grid))
                    {
                        if (grid.Closed || grid.MarkedForClose)
                        {
                            UncollectedPay += item.Value;
                            temp.Add(item.Key);
                            Core.SendMessage($"{this.Name}", $"{grid.DisplayName} destroyed.", Color.LightGreen, this.AssignedPlayerSteamId);
                        }
                        else
                        {
                            if (!HasPower(grid) || !HasActiveThrusters(grid) || grid.BlocksCount <= StartingBlockCounts[grid.EntityId] / 2)
                            {
                                UncollectedPay += item.Value;
                                temp.Add(item.Key);
                                grid.SwitchPower();
                                Core.SendMessage($"{this.Name}", $"{grid.DisplayName} destroyed.", Color.LightGreen, this.AssignedPlayerSteamId);
                                //      Core.Log.Info("grid has no power");
                            }
                            else
                            {
                                //       Core.Log.Info("grid has power");
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            if (MyAPIGateway.Entities.TryGetEntityById(item.Key, out var foundGrid))
                            {
                                MappedGrids.Add(item.Key, foundGrid as MyCubeGrid);
                            }

                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
                //dead grids should no longer be tracked
                GridIdsToPay = GridIdsToPay.Where(x => temp.All(z => z != x.Key)).ToDictionary(x => x.Key, x => x.Value);
                MappedGrids = MappedGrids.Where(x => temp.All(z => z != x.Key)).ToDictionary(x => x.Key, x => x.Value);
                return false;
            }

            var distance = Vector3.Distance(PlayersCurrentPosition, DeliverLocation);
            if (distance <= DistanceBeforeSpawnAtGPSInKM)
            {

                if (MySession.Static.Players.TryGetPlayerBySteamId(this.AssignedPlayerSteamId, out var player))
                {
                    Core.MesAPI.ChatCommand(CommandToExecute, player.Character.WorldMatrix, AssignedPlayerIdentityId,
                        AssignedPlayerSteamId);
                    HasStarted = true;

                    MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
                }
            }

            return false;
        }

        public override bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            if (DateTime.Now >= this.ExpireAt)
            {
                var playerData = Core.PlayerStorage.GetData(this.AssignedPlayerSteamId);
                MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
                if (this.UncollectedPay >= this.RewardMoney)
                {
                    CrunchEconV3.Core.SendMessage("Contracts",
                        DateTime.Now > ExpireAt ? $"{this.Name}, Contract completed." : $"{this.Name}", Color.Green,
                        this.AssignedPlayerSteamId);
                }
                else
                {
                    CrunchEconV3.Core.SendMessage("Contracts",
                        DateTime.Now > ExpireAt ? $"{this.Name}, Contract time expired." : $"{this.Name}", Color.Green,
                        this.AssignedPlayerSteamId);
                }
     
                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.UncollectedPay);
                return true;
            }

            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            if (this.UncollectedPay >= 0)
            {
                var playerData = Core.PlayerStorage.GetData(this.AssignedPlayerSteamId);
                CrunchEconV3.Core.SendMessage("Contracts",
                    DateTime.Now > ExpireAt ? $"{this.Name}, Time Expired." : $"{this.Name}", Color.Green,
                    this.AssignedPlayerSteamId);

                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.UncollectedPay);
                return true;
            }

            return false;
        }

        public override void FailContract()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            this.DeleteDeliveryGPS();
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId,
                    ReputationLossOnAbandon *= -1, ReputationChangeReason.Contract);
            }
            var playerData = Core.PlayerStorage.GetData(this.AssignedPlayerSteamId);
            CrunchEconV3.Core.SendMessage("Contracts",
                DateTime.Now > ExpireAt ? $"{this.Name}, Abandoned." : $"{this.Name}", Color.Red,
                this.AssignedPlayerSteamId);
        }

        public override void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{this.Name} Location");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"{this.Name} Location";
            gpsRef.GPSColor = Color.Red;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = new TimeSpan?();
            gpsRef.Description = sb.ToString();
            gpsRef.UpdateHash();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }

        public List<GridDestruction> GridsToDestroy = new List<GridDestruction>();
        public long UncollectedPay = 0;

        public Dictionary<long, long> GridIdsToPay = new Dictionary<long, long>();
        private Dictionary<long, MyCubeGrid> MappedGrids = new Dictionary<long, MyCubeGrid>();
        public Dictionary<long, int> StartingBlockCounts = new Dictionary<long, int>();
    }

    public class GridDestruction
    {
        public string FacTagToOwnThisGrid = "SPRT";
        public string GridToDestroy = "pirate";
        public long Payment = 50000;
    }
    public class CrunchMESSpawnerConfig : IContractConfig
    {
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
            GridsToDestroy = new List<GridDestruction>() { new GridDestruction() };
        }

        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary)
        {
            if (this.ChanceToAppear < 1)
            {
                var random = CrunchEconV3.Core.random.NextDouble();
                if (random > this.ChanceToAppear)
                {
                    return null;
                }
            }

            var contract = new CrunchMESSpawnerContractImplementation();
            var description = new StringBuilder();
            var contractContractType = "CrunchMESSpawner";
            contract.ContractType = contractContractType;
            contract.BlockId = idUsedForDictionary;
            contract.RewardMoney = ExpectedReward;
            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Escort";
            contract.Name = this.ContractName;
            contract.ReputationRequired = this.ReputationRequired;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.CommandToExecute = this.CommandToRun;
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            contract.DistanceBeforeSpawnAtGPSInKM = this.PlayerDistanceToGpsBeforeSpawn;
            contract.GridsToDestroy = this.GridsToDestroy;
            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }

            description.AppendLine($"{this.Description}");
            contract.ReadyToDeliver = false;
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }



        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary)
        {
            var min = 100 * 1000;
            var max = 200 * 1000;
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
                        var planets = MyPlanets.GetPlanets();
                        MyPlanet lowestDistancePlanet = null;
                        var lowestDistance = 0f;
                        foreach (var planet in planets)
                        {
                            var planetPosition = planet.PositionComp.GetPosition();
                            var distance = Vector3.Distance(planetPosition, Position);
                            if (lowestDistance == 0)
                            {
                                lowestDistance = distance;
                                lowestDistancePlanet = planet;
                            }

                            if (distance < lowestDistance)
                            {
                                lowestDistance = distance;
                                lowestDistancePlanet = planet;
                            }
                        }

                        Position = lowestDistancePlanet.GetClosestSurfacePointLocal(ref Position);
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
                    Vector3 Position = __instance.CubeGrid.PositionComp.GetPosition() + randomDirection * randomDistance;
                    if (MyGravityProviderSystem.IsPositionInNaturalGravity(Position))
                    {
                        var planets = MyPlanets.GetPlanets();
                        MyPlanet lowestDistancePlanet = null;
                        var lowestDistance = 0f;
                        foreach (var planet in planets)
                        {
                            var planetPosition = planet.PositionComp.GetPosition();
                            var distance = Vector3.Distance(planetPosition, Position);
                            if (lowestDistance == 0)
                            {
                                lowestDistance = distance;
                                lowestDistancePlanet = planet;
                            }

                            if (distance < lowestDistance)
                            {
                                lowestDistance = distance;
                                lowestDistancePlanet = planet;
                            }
                        }

                        Position = lowestDistancePlanet.GetClosestSurfacePointLocal(ref Position);
                    }
                    var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                    if (faction != null)
                    {
                        return Tuple.Create(new Vector3D(Position), faction.FactionId);
                    }
                }
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
        public int PlayerDistanceToGpsBeforeSpawn = 15000;
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
        public string ContractName { get; set; } = "MES Spawner";
        public string Description { get; set; } = "Put a description here";

        public string CommandToRun = "Put a command here";
        public int ExpectedReward { get; set; } = 0;
        public List<GridDestruction> GridsToDestroy = new List<GridDestruction>();
    }
}