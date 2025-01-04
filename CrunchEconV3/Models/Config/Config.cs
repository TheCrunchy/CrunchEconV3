using System;
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
        public bool OverrideKeenStores = false;
        public HashSet<KeenStoreFileEntry> KeenNPCStoresOverrides { get; set; } = new HashSet<KeenStoreFileEntry>();

        public float GravityPriceModifier = 1;

        public bool DebugMode = false;
    }
}
