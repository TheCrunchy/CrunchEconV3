using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Models.Config;
using CrunchEconV3.Models.Contracts;
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
                    Core.Log.Info($"{string.Join(",",mining.OresToPickFrom)}");
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
            return null;
        }
    }
}
