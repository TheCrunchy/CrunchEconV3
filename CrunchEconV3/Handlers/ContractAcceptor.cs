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
using VRageMath;

namespace CrunchEconV3.Handlers
{
    public static class ContractAcceptor
    {
        public static Tuple<bool, MyContractResults> TryAcceptContract(ICrunchContract contract,
            CrunchPlayerData playerData, long identityId, MyContractBlock __instance, MyStation keenstation, long contractDictionaryId)
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
           
               
                if (keenstation != null)
                {
                    if (MySession.Static.Factions.GetRandomFriendlyStation(faction.FactionId, keenstation.Id,
                            out var friendly, out var target, true))
                    {
                        contract.DeliverLocation = target.Position;
                    }
                    else
                    {
                        Core.Log.Info("Couldnt find a fucking station");
                    }
                }

                if (contract is CrunchMiningContract)
                {
                    contract.DeliverLocation = __instance.PositionComp.GetPosition();
                }

                if (contract is CrunchPeopleHaulingContract people && contract.DeliverLocation.Equals(Vector3.Zero))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var thisStation = StationHandler.GetStationNameForBlock(contractDictionaryId);
                        var station = Core.StationStorage.GetAll().GetRandomItemFromList();
                        if (station.FileName == thisStation)
                        {
                            i++;
                            continue;
                        }
                        var GPS = GPSHelper.ScanChat(station.LocationGPS);
                        contract.DeliverLocation = GPS.Coords;
                        if (people.KilometerDistancePerBonus != 0)
                        {
                            var distance = Vector3.Distance(contract.DeliverLocation, contract.DeliverLocation);
                            var division = distance / people.KilometerDistancePerBonus;
                            var distanceBonus = (long)(division * people.BonusPerDistance);
                            contract.DistanceReward += distanceBonus;
                        }
                        break;
                    }
                }

                contract.Start();
                contract.SendDeliveryGPS();
                StationHandler.BlocksContracts[contractDictionaryId].Remove(contract);
                Task.Run(async () =>
                {
                    Core.PlayerStorage.Save(playerData);
                });
            }

            return result;
        }
    }
}
