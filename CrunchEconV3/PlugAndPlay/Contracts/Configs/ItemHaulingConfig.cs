using System;
using System.Collections.Generic;
using System.Text;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using CrunchEconV3.PlugAndPlay.Models;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlay.Contracts.Configs
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
            }
            var contract = new ItemHaulingContractImplementation();

            var description = new StringBuilder();

            contract.ItemToDeliver = (ItemToDeliver)this.ItemsAvailable.GetRandomItemFromList();
            if (contract.ItemToDeliver == null)
            {
                return null;
            }
            contract.RewardMoney = contract.ItemToDeliver.Pay;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.Name = this.ContractName;
            contract.CargoNames = this.CargoNames;
            contract.PlaceItemsInTargetStation = this.PlaceItemsInTargetStation;
            contract.ReadyToDeliver = true;
            description.AppendLine(
                $"Deliver {contract.ItemToDeliver.AmountToDeliver} {contract.ItemToDeliver.TypeId.Replace("MyObjectBuilder_", "")} {contract.ItemToDeliver.SubTypeId}");
            float distance = 0;
   
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
            CargoNames = new List<string>() { "Cargo1", "Cargo2" };
        }
        public string ContractName { get; set; } = "Item Delivery";
        public List<ItemHaul> ItemsAvailable { get; set; }
        public bool PlaceItemsInTargetStation { get; set; }
        public List<string> CargoNames = new List<string>();
    }

  

}