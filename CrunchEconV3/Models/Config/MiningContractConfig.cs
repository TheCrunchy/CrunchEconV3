using System;
using System.Collections.Generic;
using CrunchEconV3.Interfaces;

namespace CrunchEconV3.Models.Config
{
    public class MiningContractConfig : IContractConfig
    {
        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long PricePerItemMin { get; set; } = 1;
        public long PricePerItemMax { get; set; } = 3;
        public int AmountToMineThenDeliverMin { get; set; } = 1;
        public int AmountToMineThenDeliverMax { get; set; } = 10;
        public long SecondsToComplete { get; set; }
        public int ReputationRequired { get; set; }
        public int ReputationGainOnCompleteMin { get; set; }
        public int ReputationGainOnCompleteMax { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public List<String> OresToPickFrom { get; set; }
    }
}
