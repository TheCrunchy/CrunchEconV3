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
            Core.Log.Info("Generating 1");
            switch (config.Type)
            {
                case CrunchContractTypes.Mining:
                {
                    var contract = new CrunchMiningContract();
                    var mining = config as MiningContractConfig;
                    contract.AmountToMine = Core.random.Next(mining.AmountToMineThenDeliverMax, mining.AmountToMineThenDeliverMax);
                    contract.RewardMoney = contract.AmountToMine * (Core.random.Next((int)mining.PricePerItemMin, (int)mining.PricePerItemMax));
                    contract.DeliverLocation = location;
                    contract.BlockId = blockId;
                    contract.CanAutoComplete = false;
                    contract.ContractType = CrunchContractTypes.Mining;
                    contract.OreSubTypeName = mining.OresToPickFrom.GetRandomItemFromList();
                    contract.ReputationGainOnComplete = mining.ReputationGainOnComplete;
                    contract.ReputationLossOnAbandon = mining.ReputationLossOnAbandon;
                    contract.SecondsToComplete = mining.SecondsToComplete;
                    contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/CrunchContract";
                    contract.Name = $"{contract.OreSubTypeName} Mining Contract";
                    contract.Description = $"You must go mine {contract.AmountToMine:##,###} {contract.OreSubTypeName} then return here.";

                    return contract;
                }
                    break;
                case CrunchContractTypes.PeopleTransport:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            Core.Log.Info("Generating 2");
            return null;
        }
    }
}
