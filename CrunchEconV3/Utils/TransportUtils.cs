using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models.Contracts;
using Sandbox.Game.Entities;

namespace CrunchEconV3.Utils
{
    public static class TransportUtils
    {
        public static int GetPassengerCount(MyCubeGrid grid, CrunchPeopleHaulingContract contract)
        {
            var count = 0;
            foreach (var passengerBlock in contract.PassengerBlocks)
            {
                var blockCount = grid.GetBlocks()
                    .Count(x => x.BlockDefinition != null && x.BlockDefinition?.BlockPairName == passengerBlock.BlockPairName);
                count += blockCount * passengerBlock.PassengerSpace;
            }

            return count;
        }
    }
}
