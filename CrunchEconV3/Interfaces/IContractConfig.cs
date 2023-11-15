using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRageMath;

namespace CrunchEconV3.Interfaces
{
    public interface IContractConfig
    {       
        //run this whenever you want to generate a delivery location, always null check the keen station!
        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation);

        //run this whenever you want to generate a delivery location, always null check the keen station!
        public Vector3 AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation);
        public int AmountOfContractsToGenerate { get; set; }
        public long SecondsToComplete { get; set; }
        public int ReputationGainOnCompleteMin { get; set; }
        public int ReputationGainOnCompleteMax { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public int ReputationRequired { get; set; }
        public float ChanceToAppear { get; set; }
        public long CollateralMin { get; set; }
        public long CollateralMax { get; set; }
        public List<String> DeliveryGPSes { get; set; }
    }
}
