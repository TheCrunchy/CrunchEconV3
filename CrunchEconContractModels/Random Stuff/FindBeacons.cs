using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public class FindBeacons
    {
        private static int ticks;

        public static void UpdateExample()
        {
            ticks++;
            if (ticks % 20 == 0)
            {
          
            }

        }

        public static void Patch(PatchContext ctx)
        {
            Core.UpdateCycle += UpdateExample;
        }
    }
}
