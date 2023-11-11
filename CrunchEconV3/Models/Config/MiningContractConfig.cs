using System;
using System.Collections.Generic;
using CrunchEconV3.Interfaces;

namespace CrunchEconV3.Models.Config
{
    public class MiningContractConfig : IContractConfig
    {
        public int AmountOfContractsToGenerate { get; set; } = 3;
        public long PricePerItem { get; set; } = 1;
        public int AmountToMineThenDeliverMin { get; set; } = 1;
        public int AmountToMineThenDeliverMax { get; set; } = 10;
        public float ChanceToAppear { get; set; } = 0.5f;
        public bool DeliverToStationTakenFrom { get; set; } = true;
        public List<String> OresToPickFrom { get; set; } = new List<string>() { "Iron", "Nickel", "Cobalt" };
        public CrunchContractTypes Type { get; set; }
    }
}
