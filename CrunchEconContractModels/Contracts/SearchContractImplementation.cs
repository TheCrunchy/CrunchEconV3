using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrunchEconV3;
using CrunchEconV3.APIs;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class SearchContractImplementation : ICrunchContract
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
            var contractDescription = this.Description;
            return BuildUnassignedContract(contractDescription);
        }


        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {

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
            this.DeliverLocation = PointsToExplore.First().Value.Location;
            SendDeliveryGPS();
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
        }


        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (DateTime.Now >= ExpireAt)
            {
                if (ReadyToDeliver)
                {
                    TryCompleteContract(this.AssignedPlayerSteamId, PlayersCurrentPosition);
                    return true;
                }
                FailContract();
                return true;
            }

            foreach (var explorePoint in PointsToExplore)
            {
                double distance = Vector3D.Distance(new Vector3D(PlayersCurrentPosition), explorePoint.Value.Location);

                if (distance <= explorePoint.Value.DistanceToTrigger)
                {
                    bool eventTriggered = TriggerEvent(explorePoint.Value.ChanceToTrigger);

                    if (eventTriggered)
                    {
                        var spawns = 0;
                        foreach (var grid in explorePoint.Value.GridsToSpawn)
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
                            var faction = MySession.Static.Factions.TryGetFactionByTag(grid.FacTagToOwnGrid);
                            if (faction == null)
                            {
                                Core.Log.Info($"{grid.FacTagToOwnGrid} faction not found");
                                continue;
                            }
                            Position.Add(new Vector3(Core.random.Next(grid.MinDistance, grid.MaxDistance),
                                Core.random.Next(grid.MinDistance, grid.MaxDistance),
                                Core.random.Next(grid.MinDistance, grid.MaxDistance)));

                            if (grid.WaterModSpawn)
                            {
                                if (WaterModAPI.Registered)
                                {
                                    var pos = WaterModAPI.GetClosestSurfacePoint(Position, null);
                                    if (pos != null && !pos.Equals(Vector3D.Zero))
                                    {
                                        Position = pos;
                                    }
                                }
                            }
                            if (!File.Exists($"{Core.path}//Grids//{grid.ExportedGridName}")) continue;
                            if (!GridManager.LoadGrid($"{Core.path}//Grids//{grid.ExportedGridName}", Position, false,
                                    (ulong)faction.Members.FirstOrDefault().Key, grid.ExportedGridName.Replace(".sbc", ""), false))
                            {
                                Core.Log.Info($"Could not load grid {grid.ExportedGridName}");
                            }
                            else
                            {
                                spawns += 1;
                            }
                        }
                    }

                    foreach (var onlinePlayer in MySession.Static.Players.GetOnlinePlayers())
                    {
                        Vector3D playerPosition = onlinePlayer.Character?.PositionComp.GetPosition() ?? Vector3D.Zero;

                        if (playerPosition == Vector3D.Zero) continue;
                        distance = Vector3D.Distance(PlayersCurrentPosition, playerPosition);

                        if (distance <= explorePoint.Value.DistanceToTrigger)
                        {
                            Core.SendMessage(explorePoint.Value.MessageAuthor, explorePoint.Value.MessageToPlayer, Color.Red,
                                onlinePlayer.Id.SteamId);
                        }
                    }
                    PointsToExplore.Remove(explorePoint.Key);
                    if (PointsToExplore.Any())
                    {
                        DeliverLocation = PointsToExplore.Values.ToList().GetRandomItemFromList().Location;
                        DeleteDeliveryGPS();
                        SendDeliveryGPS();
                    }

                    if (PointsToExplore.Count == 0 || explorePoint.Value.PointTriggersCompletion)
                    {
                        ReadyToDeliver = true; // No more points to explore
                        DeleteDeliveryGPS();
                        //send the player a chat message for ready to complete = true
                    }

                    return false;
                }
            }

            return false;
        }

        private bool TriggerEvent(double chanceToTrigger)
        {
            var random = Core.random.NextDouble();
            return random < chanceToTrigger;
        }
        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            if (this.ReadyToDeliver)
            {
                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);

                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, true);
                }
                return true;
            }
            return false;
        }

        public void FailContract()
        {
            DeleteDeliveryGPS();
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1);
            }

            Core.SendMessage("Contracts",
                DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red,
                this.AssignedPlayerSteamId);
        }

        public void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Search Area");
            sb.AppendLine("Exploration point");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"Search Point";
            gpsRef.GPSColor = Color.Yellow;
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

        public Dictionary<int, ExplorePoint> PointsToExplore = new Dictionary<int, ExplorePoint>();
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
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
        public long DeliveryFactionId { get; set; }
    }

    public class SearchConfig : IContractConfig
    {
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
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
            var contract = new SearchContractImplementation();
            var description = new StringBuilder();
            var contractContractType = "CrunchAreaSurvey";
            contract.ContractType = contractContractType;
            contract.BlockId = idUsedForDictionary;
            contract.RewardMoney = MinimumPay;
            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Find";
            contract.Name = this.ContractName;
            contract.ReputationRequired = this.ReputationRequired;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            contract.Description = this.ContractDescription;
            description.AppendLine($"{this.ContractDescription}");

            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            var points = GenerateExplorePoints(this.AmountOfLocationsToGenerateMin, this.AmountOfLocationsToGenerateMax, this.PointsToUse, Vector3D.Zero, 10000, 100000);

            var completion = points.GetRandomItemFromList().PointTriggersCompletion = true;

            return Tuple.Create(Vector3D.Zero, 0l);
        }


        public ExplorePoint ConvertFromConfig(ExplorePointConfig configuration, Vector3 baseLocation, int minDistance, int maxDistance)
        {
            ExplorePoint point = new ExplorePoint
            {
                GridsToSpawn = configuration.GridsToSpawn,
                MessageToPlayer = configuration.MessageToPlayer,
                MessageAuthor = configuration.MessageAuthor,
                ChanceToTrigger = configuration.ChanceToTrigger,
                PointTriggersCompletion = false,
                DistanceToTrigger = configuration.DistanceToTrigger
            };

            double distance = Core.random.Next(minDistance, maxDistance);
            double angle = Core.random.NextDouble() * Math.PI * 2;

            double x = baseLocation.X + distance * Math.Cos(angle);
            double y = baseLocation.Y + distance * Math.Sin(angle);
            double z = baseLocation.Z + distance;

            point.Location = new Vector3((float)x, (float)y, (float)z);

            return point;
        }

        public List<ExplorePoint> GenerateExplorePoints(int minAmount, int maxAmount, List<ExplorePointConfig> configurations,
            Vector3 baseLocation, int minDistance, int maxDistance)
        {
            var explorePoints = new List<ExplorePoint>();
            var random = new Random();

            int amountToGenerate = random.Next(minAmount, maxAmount + 1);

            for (int i = 0; i < amountToGenerate; i++)
            {
                ExplorePointConfig randomConfig = configurations.GetRandomItemFromList();
                ExplorePoint newPoint = ConvertFromConfig(randomConfig, baseLocation, minDistance, maxDistance);

                explorePoints.Add(newPoint);
            }

            return explorePoints;
        }

        public string ContractName { get; set; } = "Area Survey Contract";
        public string ContractDescription { get; set; } = "Search the areas for activity";
        public int AmountOfLocationsToGenerateMax { get; set; } = 5;
        public int AmountOfLocationsToGenerateMin { get; set; } = 1;
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

        public List<ExplorePointConfig> PointsToUse { get; set; }

    }
    public class ExplorePointConfig
    {
        public List<SpawnModel> GridsToSpawn = new List<SpawnModel>();
        public string MessageToPlayer { get; set; } = "Example message";
        public string MessageAuthor { get; set; } = "Example Author";
        public double ChanceToTrigger { get; set; } = 0.5;
        public int DistanceToTrigger { get; set; } = 5000;
    }

    public class ExplorePoint
    {
        public Vector3 Location { get; set; }
        public List<SpawnModel> GridsToSpawn = new List<SpawnModel>();
        public string MessageToPlayer { get; set; } = "Example message";
        public string MessageAuthor { get; set; } = "Example Author";
        public double ChanceToTrigger { get; set; } = 0.5;
        public bool PointTriggersCompletion { get; set; } = false;
        public int DistanceToTrigger { get; set; } = 5000;
    }
    public class SpawnModel
    {
        public string ExportedGridName { get; set; }
        public int MinDistance { get; set; } = 1000;
        public int MaxDistance { get; set; } = 5000;
        public double ChanceToSpawn { get; set; } = 0.5;
        public string FacTagToOwnGrid { get; set; } = "SPRT";
        public bool WaterModSpawn { get; set; } = false;

    }
}