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
    public class Update
    {
        private static int ticks;

        public static void UpdateExample()
        {
            ticks++;
            if (ticks % 20 == 0)
            {
                //once a second



                //load a file and cache the contracts, or get the contracts from a loaded station 
                //cache them here for the acceptor to work 
           //     StationHandler.BlocksContracts[somerandomid] = new list<ICrunchContract>();

                ICrunchContract contract;
               // ContractAcceptor.TryAcceptContract(contract, playerData, identityId, null, null, somerandomid);
            }

        }

        public static void Patch(PatchContext ctx)
        {
            Core.UpdateCycle += UpdateExample;
        }
    }
}
