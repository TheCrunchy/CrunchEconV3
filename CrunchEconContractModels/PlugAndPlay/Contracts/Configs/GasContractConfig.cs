using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.Contracts;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.PlugAndPlay.Contracts.Configs
{
    public class GasContractConfig : ContractConfigAbstract
    {
        //check the discord for documentation on what each thing in the interface does 
        //https://discord.gg/cQFJeKvVAA

        public override ICrunchContract GenerateTheRest(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
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
            var contract = new CrunchGasContractImplementation();
            var price = PriceHelper.GetPriceModel($"MyObjectBuilder_GasProperties/{this.GasSubType}");
            var pricing = price.GetSellMinAndMaxPrice();
            contract.GasAmount = CrunchEconV3.Core.random.Next((int)this.AmountInLitresMin, (int)this.AmountInLitresMax);
            contract.RewardMoney = contract.GasAmount * (Core.random.Next((int)pricing.Item1, (int)pricing.Item2));
            contract.GasName = this.GasSubType;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.Name = $"{contract.GasName} Delivery";
            contract.ReputationRequired = this.ReputationRequired;
            contract.ReadyToDeliver = true;

            description.AppendLine($"You must deliver {contract.GasAmount:##,###}L {contract.GasName} in none stockpile tanks.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }
        public string GasSubType { get; set; } = "Hydrogen";
        public long AmountInLitresMin { get; set; } = 200 * 1000;
        public long AmountInLitresMax { get; set; } = 480 * 1000;
    }
}
