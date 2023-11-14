using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Models.Config;
using CrunchEconV3.Models.Contracts;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using SpaceEngineers.Game.EntityComponents.Blocks;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Handlers
{
    public static class ContractGenerator
    {
        public static ICrunchContract GenerateContract(IContractConfig config, Vector3D location, long blockId)
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
                        if (mining.DeliveryGPSes != null && mining.DeliveryGPSes.Any())
                        {
                            var random = mining.DeliveryGPSes.GetRandomItemFromList();
                            var GPS = GPSHelper.ScanChat(random);
                            if (GPS != null)
                            {
                                contract.DeliverLocation = GPS.Coords;
                            }
                        }
                        contract.ContractType = "Mining";
                        contract.BlockId = blockId;
                        contract.CanAutoComplete = false;
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
                            description.AppendLine($" ||| Reputation with owner required: {mining.ReputationRequired}".PadRight(69, '_'));
                        }

                        contract.Description = description.ToString();
                        return contract;
                    }
                case PeopleHaulingContractConfig people:
                    {
                        var contract = new CrunchPeopleHaulingContract();
                        contract.RewardMoney = Core.random.Next((int)people.PricePerPassengerMin,
                            (int)people.PricePerPassengerMax);
     
                        if (people.DeliveryGPSes != null && people.DeliveryGPSes.Any())
                        {
                            var random = people.DeliveryGPSes.GetRandomItemFromList();
                            var GPS = GPSHelper.ScanChat(random);
                            if (GPS != null)
                            {
                                contract.DeliverLocation = GPS.Coords;
                            }
                        }

                        var description = new StringBuilder();
                        contract.ContractType = "PeopleTransport";
                        contract.BlockId = blockId;
                        contract.CanAutoComplete = false;
                        contract.PassengerBlocks = people.PassengerBlocksAvailable;
                        contract.ReputationGainOnComplete = Core.random.Next(people.ReputationGainOnCompleteMin, people.ReputationGainOnCompleteMax);
                        contract.ReputationLossOnAbandon = people.ReputationLossOnAbandon;
                        contract.SecondsToComplete = people.SecondsToComplete;
                        contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
                        contract.Name = $"People Transport Contract";
                        contract.ReputationRequired = people.ReputationRequired;
                        contract.CanAutoComplete = true;
                        contract.CollateralToTake = (Core.random.Next((int)people.CollateralMin, (int)people.CollateralMax));
                        description.AppendLine($"Reward = {contract.RewardMoney} multiplied by Passenger count");
                        description.AppendLine($" ||| Maximum possible passengers: {1500 * people.ReputationMultiplierForMaximumPassengers}");
                        foreach (var passengerBlock in people.PassengerBlocksAvailable)
                        {
                            description.AppendLine($"||| {passengerBlock.BlockPairName} provides {passengerBlock.PassengerSpace} capacity");
                        }
                    
                        description.AppendLine($" ||| Distance bonus applied {contract.DistanceReward:##,###}");
               
                        if (people.ReputationRequired != 0)
                        {
                            description.AppendLine($" ||| Reputation with owner required: {people.ReputationRequired}");
                        }

                        contract.Description = description.ToString();
                        return contract;
                    }
                case GasContractConfig gas:
                    {
                        var description = new StringBuilder();
                        var contract = new CrunchGasContract();

                        for (int i = 0; i < 10; i++)
                        {
                            var thisStation = StationHandler.GetStationNameForBlock(blockId);
                            var station = Core.StationStorage.GetAll().GetRandomItemFromList();
                            if (station.FileName == thisStation)
                            {
                                i++;
                                continue;
                            }
                            var GPS = GPSHelper.ScanChat(station.LocationGPS);
                            contract.DeliverLocation = GPS.Coords;
                            break;
                        }
                        if (gas.DeliveryGPSes != null && gas.DeliveryGPSes.Any())
                        {
                            var random = gas.DeliveryGPSes.GetRandomItemFromList();
                            var GPS = GPSHelper.ScanChat(random);
                            if (GPS != null)
                            {
                                contract.DeliverLocation = GPS.Coords;
                            }
                        }

                        contract.GasAmount = Core.random.Next((int)gas.AmountInLitresMin, (int)gas.AmountInLitresMax);
                        contract.RewardMoney = contract.GasAmount * (Core.random.Next((int)gas.PricePerLitreMin, (int)gas.PricePerLitreMax));
                        contract.ContractType = "GasHauling";
                        contract.BlockId = blockId;
                        contract.CanAutoComplete = false;
                        contract.GasName = gas.GasSubType;
                        contract.ReputationGainOnComplete = Core.random.Next(gas.ReputationGainOnCompleteMin, gas.ReputationGainOnCompleteMax);
                        contract.ReputationLossOnAbandon = gas.ReputationLossOnAbandon;
                        contract.SecondsToComplete = gas.SecondsToComplete;
                        contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
                        contract.Name = $"{contract.GasName} Delivery Contract";
                        contract.ReputationRequired = gas.ReputationRequired;
                        contract.CanAutoComplete = true;
                        contract.ReadyToDeliver = true;
                        contract.CollateralToTake = (Core.random.Next((int)gas.CollateralMin, (int)gas.CollateralMax));
                        description.AppendLine($"You must deliver {contract.GasAmount:##,###}L {contract.GasName} in non stockpile tanks.");
                        if (gas.ReputationRequired != 0)
                        {
                            description.AppendLine($" ||| Reputation with owner required: {gas.ReputationRequired}");
                        }

                        contract.Description = description.ToString();
                        return contract;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static MyObjectBuilder_Contract BuildFromUnacceptedExisting(ICrunchContract contract, string overrideDescription = "")
        {
            MyObjectBuilder_Contract newContract = null;
            switch (contract)
            {
                case CrunchPeopleHaulingContract:
                case CrunchGasContract:
                case CrunchMiningContract:
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
                    break;
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
                        contractDescription = $"You must go mine {crunchMiningContract.AmountToMine - crunchMiningContract.MinedOreAmount:##,###} {crunchMiningContract.OreSubTypeName} using a ship drill, then return here.";
                    }
                    break;
                case CrunchPeopleHaulingContract crunchPeople:
                {

                    contractDescription = $"You must go deliver {crunchPeople.PassengerCount} passengers, using the ship that accepted the contract.";
                    contractDescription += $" ||| Distance bonus: {crunchPeople.DistanceReward:##,###}";
                        foreach (var passengerBlock in crunchPeople.PassengerBlocks)
                    {
                        contractDescription += $" ||| block {passengerBlock.BlockPairName} provides {passengerBlock.PassengerSpace} capacity";
                    }
                }
                    break;
                case CrunchGasContract crunchGas:
                {
                    contractDescription =
                        $"You must deliver {crunchGas.GasAmount:##,###}L {crunchGas.GasName} in non stockpile tanks.";
                }
                    break;
                default:
                    break;
            }

            newContract = BuildFromUnacceptedExisting(contract, contractDescription);
            newContract.TicksToDiscard = (int?)(contract.ExpireAt - DateTime.Now).TotalSeconds;
            newContract.RemainingTimeInS = (contract.ExpireAt - DateTime.Now).TotalSeconds;

            return newContract;
        }
    }
}
