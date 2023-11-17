using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using Sandbox.Game.Entities;

namespace CrunchEconV3.Models
{
    public class StationConfig
    {
        public bool Enabled { get; set; } = true;
        private MyCubeGrid grid { get; set; }
        public string LocationGPS { get; set; } = "Put a gps here";
        public string FactionTag { get; set; } = "SPRT";
        public int SecondsBetweenContractRefresh { get; set; }
        public List<string> ContractFiles { get; set; }
        public DateTime NextSellRefresh { get; set; } = DateTime.Now;
        public DateTime NextBuyRefresh { get; set; } = DateTime.Now;
        public string FileName { get; set; }
        public bool UseAsDeliveryLocation { get; set; } = true;
        private List<IContractConfig> configs;

        public void SetGrid(MyCubeGrid grid)
        {
            this.grid = grid;
        }

        public MyCubeGrid GetGrid()
        {
            return grid;
        }

        public List<IContractConfig> GetConfigs()
        {
            return configs;
        }
        public void SetConfigs(List<IContractConfig> Configs)
        {
            configs = Configs;
        }

        public List<IStationLogic> Logics;

        public bool IsFirstLoad()
        {
            return FirstLoad;
        }

        public void SetFirstLoad(bool firstLoad)
        {
            FirstLoad = firstLoad;
        }

        private bool FirstLoad = true;
    }
}
//List<IContractConfig> Contracts = new List<IContractConfig>();