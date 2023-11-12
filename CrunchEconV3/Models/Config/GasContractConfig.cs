using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;

namespace CrunchEconV3.Models.Config
{
    public class GasContractConfig : IContractConfig
    {
        public int AmountOfContractsToGenerate { get; set; } = 2;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public int ReputationRequired { get; set; } = 0;
        public float ChanceToAppear { get; set; } = 1;
        public long CollateralMin { get; set; } = 1000;
        public long CollateralMax { get; set; } = 5000;
        public string GasSubType { get; set; } = "Hydrogen";
        public long AmountInLitresMin { get; set; } = 200 * 1000;
        public long AmountInLitresMax { get; set; } = 480 * 1000;
        public long PricePerLitreMin { get; set; } = 50;
        public long PricePerLitreMax { get; set; } = 75;
    }
}
