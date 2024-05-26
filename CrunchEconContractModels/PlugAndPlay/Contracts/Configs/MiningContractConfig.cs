﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.Contracts;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.PlugAndPlay.Contracts.Configs
{
    public class MiningContractConfig : ContractConfigAbstract
    {
        //check the discord for documentation on what each thing in the interface does 
        //https://discord.gg/cQFJeKvVAA
        public override void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
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
            var pricing = minPrice.GetSellMinAndMaxPrice();
            contract.RewardMoney = contract.AmountToMine * (Core.random.Next((int)pricing.Item1, (int)pricing.Item2));

            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            contract.ContractType = contract.GetType().Name;
            contract.BlockId = idUsedForDictionary;

            contract.ReputationGainOnComplete =
                Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/ObtainAndDeliver";
            contract.Name = $"{contract.OreSubTypeName} Mining Contract";
            contract.ReputationRequired = this.ReputationRequired;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            contract.SpawnOreInStation = this.SpawnOreInStation;
            description.AppendLine(
                $"You must go mine {contract.AmountToMine:##,###} {contract.OreSubTypeName} using a ship drill, then return here.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine(
                    $" ||| Reputation with owner required: {this.ReputationRequired}".PadRight(69, '_'));
            }

            contract.Description = description.ToString();

            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
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

        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 1;
        public List<string> DeliveryGPSes { get; set; }
        public int AmountToMineThenDeliverMin { get; set; } = 1;
        public int AmountToMineThenDeliverMax { get; set; } = 10;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 5;
        public int ReputationLossOnAbandon { get; set; } = 10;
        public List<String> OresToPickFrom { get; set; }
        public bool SpawnOreInStation { get; set; }
    }
}
