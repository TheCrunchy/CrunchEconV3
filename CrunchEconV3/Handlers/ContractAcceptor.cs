using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Models.Contracts;
using CrunchEconV3.Utils;
using Sandbox.Game.Contracts;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace CrunchEconV3.Handlers
{
    public static class ContractAcceptor
    {
        public static Tuple<bool, MyContractResults> TryAcceptContract(ICrunchContract contract,
            CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (contract.CollateralToTake > 0)
            {
                if (EconUtils.getBalance(identityId) < contract.CollateralToTake)
                {
                    return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientFunds);
                }
            }

            var result = playerData.AddContract(contract, __instance.GetOwnerFactionTag(), identityId, __instance);
            if (result.Item1)
            {
                if (contract.CollateralToTake > 0)
                {
                    EconUtils.takeMoney(identityId, contract.CollateralToTake);
                }

                var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                contract.FactionId = faction.FactionId;

                contract.AssignedPlayerIdentityId = identityId;
                contract.AssignedPlayerSteamId = playerData.PlayerSteamId;
                if (contract is CrunchMiningContract)
                {
                    contract.DeliverLocation = __instance.PositionComp.GetPosition();
                }

                contract.Start();
                contract.SendDeliveryGPS();
                StationHandler.BlocksContracts[__instance.EntityId].Remove(contract);
                Core.PlayerStorage.Save(playerData);
            }

            return result;
        }
    }
}
