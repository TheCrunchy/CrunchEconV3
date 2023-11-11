using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;

namespace CrunchEconV3.Models
{
    public class StationConfig
    {
        public bool Enabled { get; set; } = true;
        public string LocationGPS { get; set; } = "Put a gps here";
        public string FactionTag { get; set; } = "SPRT";
        public int SecondsBetweenContractRefresh { get; set; } = 600;
        public List<IContractConfig> Contracts = new List<IContractConfig>();
        public DateTime NextContractRefresh { get; set; } = DateTime.Now;
        public DateTime NextSellRefresh { get; set; } = DateTime.Now;
        public DateTime NextBuyRefresh { get; set; } = DateTime.Now;
        public string FileName { get; set; }
    }
}
