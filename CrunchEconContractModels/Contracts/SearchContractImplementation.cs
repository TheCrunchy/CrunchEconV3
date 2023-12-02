using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CrunchEconContractModels.Contracts;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Models
{
    public class SearchContractImplementation : ICrunchContract
    {
        public long ContractId { get; set; }
        public string ContractType { get; set; }
        public MyObjectBuilder_Contract BuildUnassignedContract(string descriptionOverride = "")
        {
            // Logic for generating the unassigned contract...
            return null;
        }

        public MyObjectBuilder_Contract BuildAssignedContract()
        {
            // Logic for generating the assigned contract...
            return null;
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            // Logic for accepting the contract...
            return null;
        }

        public void Start()
        {
            // Logic to start the contract...
        }

        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            // Check if the player reached the current target location
            Vector3D playerPosition = new Vector3D(PlayersCurrentPosition);



            return false;
        }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            // Logic for completing the contract...
            return false;
        }

        public void FailContract()
        {
            // Logic for failing the contract...
        }

        public void SendDeliveryGPS()
        {
            // Logic for sending delivery GPS...
        }

        public void DeleteDeliveryGPS()
        {
            // Logic for deleting delivery GPS...
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

        public void Setup()
        {
            // Logic to set up contract specifics...
        }

        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            // Logic for generating the contract from a configuration...
            return null;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            // Logic to assign the delivery GPS...
            return null;
        }


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

        public int MinDistance { get; set; } = 15000;
        public int MaxDistance { get; set; } = 150000;
        public int AmountOfLocationsToGenerateMax { get; set; } = 3;
        public int AmountOfLocationsToGenerateMin { get; set; } = 3;
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

    }
    public class ExplorePoint
    {
        public Vector3 Location { get; set; }
        public List<SpawnModel> GridsToSpawn = new List<SpawnModel>();
        public string MessageToPlayer { get; set; } = "Example message";
        public string MessageAuthor { get; set; } = "Example Author";
    }

    public class SpawnModel
    {
        public string ExportedGridName { get; set; }
        public int MinDistance { get; set; } = 1000;
        public int MaxDistance { get; set; } = 5000;

    }
}