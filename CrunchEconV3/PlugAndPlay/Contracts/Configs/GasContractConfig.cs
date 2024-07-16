using System;
using System.Text;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using CrunchEconV3.PlugAndPlay.Helpers;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRageMath;

namespace CrunchEconV3.PlugAndPlay.Contracts.Configs
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
            var contract = new GasContractImplementation();
            var price = PriceHelper.GetPriceModel($"MyObjectBuilder_GasProperties/{this.GasSubType}");
            var pricing = price.GetSellMinAndMaxPrice();
            if (price.NotFound)
            {
                return null;
            }
            contract.GasAmount = CrunchEconV3.Core.random.Next((int)this.AmountInLitresMin, (int)this.AmountInLitresMax);
            contract.RewardMoney = contract.GasAmount * (Core.random.Next((int)pricing.Item1, (int)pricing.Item2));
            contract.GasName = this.GasSubType;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.Name = $"{contract.GasName} Delivery";
            contract.ReputationRequired = this.ReputationRequired;
            contract.ReadyToDeliver = true;

            description.AppendLine($"You must deliver {contract.GasAmount:##,###}L {contract.GasName} in none stockpile tanks, gas required to start contract.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            float distance = Vector3.Distance(contract.DeliverLocation, __instance != null ? __instance.CubeGrid.PositionComp.GetPosition() : keenstation.Position);
            description.AppendLine($" ||| Distance to target: {Math.Round(distance) / 1000} KM");

            contract.Description = description.ToString();
            return contract;
        }
        public string GasSubType { get; set; } = "Hydrogen";
        public long AmountInLitresMin { get; set; } = 200 * 1000;
        public long AmountInLitresMax { get; set; } = 480 * 1000;
    }
}
