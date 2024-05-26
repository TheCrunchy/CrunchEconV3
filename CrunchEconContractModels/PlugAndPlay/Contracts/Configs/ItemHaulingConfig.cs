using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.PlugAndPlay.Contracts.Configs
{
    public class ItemHaulingConfig : ContractConfigAbstract
    {
        public override ICrunchContract GenerateTheRest(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary)
        {
            if (this.ChanceToAppear < 1)
            {
                var random = CrunchEconV3.Core.random.NextDouble();
                if (random > this.ChanceToAppear)
                {
                    return null;
                }

                this.CargoNames = new List<string>() { "Cargo1", "Cargo2" };
            }

            var contract = new ItemHaulingContractImplementation();

            var description = new StringBuilder();

            contract.ItemToDeliver = (ItemToDeliver)this.ItemsAvailable.GetRandomItemFromList();
            contract.RewardMoney = contract.ItemToDeliver.Pay;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.Name = this.ContractName;
            contract.CargoNames = this.CargoNames;
            contract.PlaceItemsInTargetStation = this.PlaceItemsInTargetStation;
         

            if (this.BonusPerKMDistance != 0)
            {
                var distance = Vector3.Distance(contract.DeliverLocation,
                    __instance != null ? __instance.PositionComp.GetPosition() : keenstation.Position);
                var division = distance / 1000;
                var distanceBonus = (long)(division * this.BonusPerKMDistance);
                if (distanceBonus > 0)
                {
                    contract.DistanceReward += distanceBonus;
                }
            }

            description.AppendLine(
                $"Deliver {contract.ItemToDeliver.AmountToDeliver} {contract.ItemToDeliver.TypeId.Replace("MyObjectBuilder_", "")} {contract.ItemToDeliver.SubTypeId}");

            description.AppendLine($" ||| Distance bonus applied {contract.DistanceReward:##,###}");

            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }

        public override void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here, optional" };
            ItemsAvailable = new List<ItemHaul>()
            {
                new ItemHaul()
                {
                    TypeId = "MyObjectBuilder_Ingot",
                    SubTypeId = "Iron",
                    AmountMax = 50000,
                    AmountMin = 25000
                }
            };
        }

        public long BonusPerKMDistance { get; set; } = 1;
        public string ContractName { get; set; } = "Item Delivery";
        public List<ItemHaul> ItemsAvailable { get; set; }
        public bool PlaceItemsInTargetStation { get; set; }
        public List<string> CargoNames = new List<string>();
    }

    public class ItemHaul
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int AmountMin { get; set; }
        public int AmountMax { get; set; }
    }

    public class ItemToDeliver
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int AmountToDeliver { get; set; }
        public long Pay { get; set; }

        public static explicit operator ItemToDeliver(ItemHaul v)
        {
            var amount = Core.random.Next(v.AmountMin, v.AmountMax);
            var price = PriceHelper.GetPriceModel($"{v.TypeId}/{v.SubTypeId}");
            var pricing = price.GetSellMinAndMaxPrice();
            return new ItemToDeliver()
            {
                AmountToDeliver = amount,
                Pay = Core.random.Next((int)pricing.Item1, (int)pricing.Item2) * amount,
                SubTypeId = v.SubTypeId,
                TypeId = v.TypeId,
            };
        }
    }
}