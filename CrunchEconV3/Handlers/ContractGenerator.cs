using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Models.Config;
using CrunchEconV3.Models.Contracts;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Handlers
{
    public static class ContractGenerator
    {
        public static ICrunchContract GenerateContract(IContractConfig config, Vector3D location, long blockId = 1)
        {
            switch (config)
            {
                case MiningContractConfig mining:
                    {
                        var description = new StringBuilder();
                        var contract = new CrunchMiningContract();
                        contract.AmountToMine = Core.random.Next(mining.AmountToMineThenDeliverMin, mining.AmountToMineThenDeliverMax);
                        contract.RewardMoney = contract.AmountToMine * (Core.random.Next((int)mining.PricePerItemMin, (int)mining.PricePerItemMax));
                        contract.DeliverLocation = location;
                        contract.BlockId = blockId;
                        contract.CanAutoComplete = false;
                        contract.ContractType = CrunchContractTypes.Mining;
                        contract.OreSubTypeName = mining.OresToPickFrom.GetRandomItemFromList();
                        contract.ReputationGainOnComplete = Core.random.Next(mining.ReputationGainOnCompleteMin, mining.ReputationGainOnCompleteMax);
                        contract.ReputationLossOnAbandon = mining.ReputationLossOnAbandon;
                        contract.SecondsToComplete = mining.SecondsToComplete;
                        contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/ObtainAndDeliver";
                        contract.Name = $"{contract.OreSubTypeName} Mining Contract";
                        contract.ReputationRequired = mining.ReputationRequired;
                        contract.CanAutoComplete = true;
                        contract.CollateralToTake = (Core.random.Next((int)mining.CollateralMin, (int)mining.CollateralMax));
                        contract.SpawnOreInStation = mining.SpawnOreInStation;
                        description.AppendLine($"You must go mine {contract.AmountToMine:##,###} {contract.OreSubTypeName} using a ship drill, then return here.");
                        if (mining.ReputationRequired != 0)
                        {
                            description.AppendLine($"Reputation with owner required: {mining.ReputationRequired}");
                        }

                        contract.Description = description.ToString();
                        return contract;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static MyObjectBuilder_Contract BuildFromUnacceptedExisting(ICrunchContract contract, string overrideDescription = "")
        {
            MyObjectBuilder_Contract newContract = null;
            switch (contract)
            {
                case CrunchMiningContract crunchMiningContract:
                    {
                        string definition = contract.DefinitionId;
                        string contractName = contract.Name;
                        string contractDescription;
                        contractDescription = overrideDescription != "" ? overrideDescription : contract.Description;

                        if (!MyDefinitionId.TryParse(definition, out var definitionId)) break;
                        newContract = new MyObjectBuilder_ContractCustom
                        {
                            SubtypeName = definition.Replace("MyObjectBuilder_ContractTypeDefinition/", ""),
                            Id = contract.ContractId,
                            IsPlayerMade = false,
                            State = MyContractStateEnum.Active,
                            Owners = new MySerializableList<long>(),
                            RewardMoney = contract.RewardMoney,
                            RewardReputation = contract.ReputationGainOnComplete,
                            StartingDeposit = contract.CollateralToTake,
                            FailReputationPrice = contract.ReputationLossOnAbandon,
                            StartFaction = 1,
                            StartStation = 0,
                            StartBlock = contract.BlockId,
                            Creation = 1,
                            TicksToDiscard = (int?)contract.SecondsToComplete,
                            RemainingTimeInS = contract.SecondsToComplete,
                            ContractCondition = null,
                            DefinitionId = definitionId,
                            ContractName = contractName,
                            ContractDescription = contractDescription
                        };
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contract));
            }

            return newContract;
        }

        public static MyObjectBuilder_Contract BuildFromPlayersExisting(ICrunchContract contract)
        {
            MyObjectBuilder_Contract newContract = null;
            var definition = contract.DefinitionId;
            var contractDescription = contract.Description;
            switch (contract)
            {
                case CrunchMiningContract crunchMiningContract:
                    {
                        if (crunchMiningContract.MinedOreAmount >= crunchMiningContract.AmountToMine)
                        {
                            contractDescription = $"Click Accept to complete contract!";
                        }
                        else
                        {
                            contractDescription = $"You must go mine {crunchMiningContract.AmountToMine - crunchMiningContract.MinedOreAmount:##,###} {crunchMiningContract.OreSubTypeName} using a ship drill, then return here.";
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contract));
            }

            newContract = BuildFromUnacceptedExisting(contract, contractDescription);
            newContract.TicksToDiscard = (int?)(contract.ExpireAt - DateTime.Now).TotalSeconds;
            newContract.RemainingTimeInS = (contract.ExpireAt - DateTime.Now).TotalSeconds;

            return newContract;
        }
    }
}
