using System;
using System.Collections.Generic;
using System.Text;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlay.Contracts.Configs
{
    public class MiningContractConfig : ContractConfigAbstract
    {
        //check the discord for documentation on what each thing in the interface does 
        //https://discord.gg/cQFJeKvVAA
        public override void Setup()
        {
            DeliveryGPSes = new List<string>() { "Not used for this contract type" };
            OresToPickFrom = new List<string>() { "Iron", "Nickel", "Gold", "Silver" };
        }
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
            var contract = new MiningContractImplementation();
            contract.AmountToMine = Core.random.Next(this.AmountToMineThenDeliverMin, this.AmountToMineThenDeliverMax);
            contract.OreSubTypeName = this.OresToPickFrom.GetRandomItemFromList();
            var minPrice = PriceHelper.GetPriceModel($"MyObjectBuilder_Ore/{contract.OreSubTypeName}");
            var pricing = minPrice.GetSellMinAndMaxPrice(true);
            contract.RewardMoney = contract.AmountToMine * (Core.random.Next((int)pricing.Item1, (int)pricing.Item2));

            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            contract.ContractType = contract.GetType().Name;
            contract.BlockId = idUsedForDictionary;

            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/ObtainAndDeliver";
            contract.Name = $"{contract.OreSubTypeName} Mining Contract";
            contract.SpawnOreInStation = this.SpawnOreInStation;
            description.AppendLine($"You must go mine {contract.AmountToMine:##,###} {contract.OreSubTypeName} using a ship drill, then return here.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine(
                    $" ||| Reputation with owner required: {this.ReputationRequired}".PadRight(69, '_'));
            }

            contract.Description = description.ToString();

            return contract;
        }

        public override Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary)
        {
            if (keenstation != null)
            {
                return Tuple.Create(keenstation.Position, keenstation.FactionId);
            }

            var faction = FacUtils.GetPlayersFaction(__instance.OwnerId);
            if (faction == null)
            {
                Core.Log.Info("Faction was null");
                return Tuple.Create(__instance.PositionComp.GetPosition(), 0l);
            }

            return Tuple.Create(__instance.PositionComp.GetPosition(), faction.FactionId);
        }

       
        public int AmountToMineThenDeliverMin { get; set; } = 1;
        public int AmountToMineThenDeliverMax { get; set; } = 10;
        public List<String> OresToPickFrom { get; set; }
        public bool SpawnOreInStation { get; set; }
    }
}
