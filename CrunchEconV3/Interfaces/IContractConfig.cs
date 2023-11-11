using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;

namespace CrunchEconV3.Interfaces
{
    public interface IContractConfig
    {
        public CrunchContractTypes Type { get; set; }
        public int AmountOfContractsToGenerate { get; set; }
        public long FactionId { get; set; }
        public long SecondsToComplete { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public float ChanceToAppear { get; set; }
    }
}
