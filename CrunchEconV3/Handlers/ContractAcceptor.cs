using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Contracts;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Handlers
{
    public static class ContractAcceptor
    {
        public static Tuple<bool, MyContractResults> TryAcceptContract(ICrunchContract contract,
            CrunchPlayerData playerData, long identityId, MyContractBlock __instance, MyStation keenstation,
            long contractDictionaryId)
        {
            var result = contract.TryAcceptContract(playerData, identityId, __instance);
            {
                if (result.Item1)
                {
                    playerData.AddContract(contract);
                    contract.Start();
                    contract.SendDeliveryGPS();
                    StationHandler.BlocksContracts[contractDictionaryId].Remove(contract);
                    Task.Run(async () => { Core.PlayerStorage.Save(playerData); });
                }

                return result;
            }
        }
    }

}