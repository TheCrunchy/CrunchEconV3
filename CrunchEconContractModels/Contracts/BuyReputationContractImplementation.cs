using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class BuyReputationContractImplementation : ICrunchContract
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
            return BuildUnassignedContract(Description);
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (this.CollateralToTake > 0)
            {
                if (EconUtils.getBalance(identityId) < this.CollateralToTake)
                {
                    return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientFunds);
                }
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
            if (this.ReputationGainOnComplete != 0)
            {
                var faction = MySession.Static.Factions.TryGetFactionByTag(FactionToGiveRepFor);
                if (faction != null)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        faction.FactionId, this.ReputationGainOnComplete, true);
                }
            }
        }

        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            return true;
        }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            return true;
        }

        public void FailContract()
        {
            
        }

        public void SendDeliveryGPS()
        {
          
        }

        public void DeleteDeliveryGPS()
        {
           
        }
        public string FactionToGiveRepFor { get; set; }
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
    public class BuyRepContractConfig : IContractConfig
    {
        //check the discord for documentation on what each thing in the interface does 
        //https://discord.gg/cQFJeKvVAA
        public void Setup()
        {
            FactionTagsEligible = new List<string>() { "TAG1, TAG2" };
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
            var contract = new BuyReputationContractImplementation();
           
            contract.RewardMoney = 0;
            contract.ContractType = "BuyReputation";
            contract.BlockId = idUsedForDictionary;
            contract.ReputationGainOnComplete = ReputationGiven;
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.FactionToGiveRepFor = FactionTagsEligible.GetRandomItemFromList();
            contract.Name = $"Buy {ReputationGiven} Reputation with {contract.FactionToGiveRepFor}";
            contract.ReputationRequired = this.ReputationRequired;
            contract.ReadyToDeliver = true;
            contract.CollateralToTake = (CrunchEconV3.Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            description.AppendLine($"Purchase to increase reputation with target faction.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }
            contract.DeliverLocation = new Vector3();
            contract.DeliveryFactionId = 0;
   
            contract.Description = description.ToString();
            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary)
        {
            return Tuple.Create(Vector3D.Zero, 0l);
        }

        public int ReputationGiven { get; set; } = 250;
        public List<string> FactionTagsEligible { get; set; }
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

    }
}
