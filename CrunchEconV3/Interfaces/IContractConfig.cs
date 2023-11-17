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
        //initialize any lists with new values etc, this method is used when the plugin generates the example jsons 
        public void Setup();

        //run this whenever you want to generate a delivery location, always null check the keen station!, the blockId may also be the stationId so dont assume its an entity you can grab 
        //this can return null if the contract fails to generate, example being it having a 10% chance to appear 
        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary);

        //run this whenever you want to generate a delivery location, always null check the keen station!
        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary);
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
