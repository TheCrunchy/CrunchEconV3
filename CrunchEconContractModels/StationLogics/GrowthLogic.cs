using System;
using System.Linq;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using Sandbox.Game.Entities;

// Consider having the extension method in an appropriate namespace

namespace CrunchEconContractModels.StationLogics
{
    public class GrowthLogic : IStationLogic
    {
        public double MaximumStorePriceModifier { get; set; } = 1.2;
        public double MaximumStoreQuantityModifier { get; set; } = 1.2;
        public double MaximumContractPriceModifier { get; set; } = 1.2;

        public double MinimumStorePriceModifier { get; set; } = 0.8;
        public double MinimumStoreQuantityModifier { get; set; } = 0.8;
        public double MinimumContractPriceModifier { get; set; } = 0.8;

        public DateTime NextRefresh { get; set; }
        public int SecondsBetweenRefresh = 600;

        public int CurrentPopulation { get; set; }

        public void Setup()
        {
            // Initialize the last spawn time to the current time
            NextRefresh = DateTime.Now;
        }

        public Task<bool> DoLogic(MyCubeGrid grid)
        {
	
            // Check if 2 minutes have passed since the last spawn
            if ((DateTime.Now < NextRefresh))
            {
                return Task.FromResult(true);
            }

            NextRefresh = DateTime.Now.AddSeconds(SecondsBetweenRefresh);
            var Station = Core.StationStorage.GetAll().FirstOrDefault(x => x.GetGrid() == grid);
            if (Station == null)
            {
                return Task.FromResult(true);
            }

            // Return true to continue logic execution
            return Task.FromResult(true);
        }

        public int Priority { get; set; }
    }
}
