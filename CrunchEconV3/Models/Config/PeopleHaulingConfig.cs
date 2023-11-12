using System;
using System.Collections.Generic;
using CrunchEconV3.Interfaces;

namespace CrunchEconV3.Models.Config
{
    public class PeopleHaulingContractConfig : IContractConfig
    {
        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 3;
        public List<string> DeliveryGPSes { get; set; }
        public long PricePerPassengerMin { get; set; } = 1;
        public long PricePerPassengerMax { get; set; } = 3;
        public long BonusPerDistance { get; set; } = 1;
        public long KilometerDistancePerBonus { get; set; } = 100000;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;

        public double ReputationMultiplierForMaximumPassengers { get; set; } = 0.3;

        public List<PassengerBlock> PassengerBlocksAvailable { get; set; }
    }
}
