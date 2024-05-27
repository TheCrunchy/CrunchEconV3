using System.Collections.Generic;

namespace CrunchEconV3.Models.Config
{
    public class Config
    {
        public bool UseDefaultSetup = false;
        public string StoragePath = $"default";
        public List<KeenNPCEntry> KeenNPCContracts { get; set; } = new List<KeenNPCEntry>();
        public int KeenNPCSecondsBetweenRefresh { get; set; } = 300;
        public bool SetMinPricesTo1 = false;
        public bool RemoveKeenContractsOnStations = true;
    }
}
