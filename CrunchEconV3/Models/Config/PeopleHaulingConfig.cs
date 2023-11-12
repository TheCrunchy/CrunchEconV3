using System;
using System.Collections.Generic;
using CrunchEconV3.Interfaces;

namespace CrunchEconV3.Models.Config
{
    public class PeopleHaulingContractConfig : IContractConfig
    {
        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; }
        public long CollateralMax { get; set; }
        public long PricePerPassengerMin { get; set; } = 1;
        public long PricePerPassengerMax { get; set; } = 3;
        public long BonusPerDistance { get; set; } = 1;
        public long KilometerDistancePerBonus { get; set; } = 100000;
        public long SecondsToComplete { get; set; }
        public int ReputationRequired { get; set; }
        public int ReputationGainOnCompleteMin { get; set; }
        public int ReputationGainOnCompleteMax { get; set; }
        public int ReputationLossOnAbandon { get; set; }

        public double ReputationMultiplierForMaximumPassengers { get; set; } = 0.3;

        public List<PassengerBlock> PassengerBlocksAvailable { get; set; }
    }
}
