using System;
using System.Collections.Generic;
using CrunchEconV3.Interfaces;

namespace CrunchEconV3.Models.Config
{
    public class MiningContractConfig : IContractConfig
    {
        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 1;
        public List<string> DeliveryGPSes { get; set; }
        public long PricePerItemMin { get; set; } = 1;
        public long PricePerItemMax { get; set; } = 3;
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
