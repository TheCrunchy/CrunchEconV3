using System.Collections.Generic;

namespace CrunchEconV3.Models.Config
{
    public class Config
    {
        public string StoragePath = $"default";
        public List<KeenNPCEntry> KeenNPCContracts { get; set; }
        public int KeenNPCSecondsBetweenRefresh { get; set; } = 300;
    }
}
