﻿using System;
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
        public StationConfig Clone()
        {
            StationConfig clonedConfig = new StationConfig
            {
                Enabled = this.Enabled,
                LocationGPS = "Put a gps here", // Default value for LocationGPS in the cloned instance
                FactionTag = this.FactionTag,
                SecondsBetweenContractRefresh = this.SecondsBetweenContractRefresh,
                ContractFiles = this.ContractFiles?.ToList(), // Copy the ContractFiles list if it's not null
                FileName = this.FileName,
                UseAsDeliveryLocation = this.UseAsDeliveryLocation,
                configs = this.configs?.ToList(), // Clone each IContractConfig in the list
                Logics = this.Logics?.ToList(), // Copy the Logics list if it's not null
                FirstLoad = this.FirstLoad
            };

            return clonedConfig;
        }
        public bool Enabled { get; set; } = true;
        private MyCubeGrid grid { get; set; }
        public string LocationGPS { get; set; } = "Put a gps here";
        public string FactionTag { get; set; } = "SPRT";
        public int SecondsBetweenContractRefresh { get; set; }
        public List<string> ContractFiles { get; set; }
        public string FileName { get; set; }
        public bool UseAsDeliveryLocation { get; set; } = true;
        private List<IContractConfig> configs = new List<IContractConfig>();

        public double StorePriceModifier { get; set; } = 1;
        public double StoreQuantityModifier { get; set; } = 1;
        public double ContractPriceModifier { get; set; } = 1;

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
            if (configs == null)
            {
                configs = Configs;
            }
            else
            {
                configs.AddRange(Configs);
            }
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