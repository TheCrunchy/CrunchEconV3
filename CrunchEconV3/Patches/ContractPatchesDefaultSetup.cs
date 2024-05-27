﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using EmptyKeys.UserInterface.Generated.PlayerTradeView_Bindings;
using Microsoft.CodeAnalysis.CSharp;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Contracts;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.Network;
using VRage.ObjectBuilder;
using VRage.ObjectBuilders;
using VRageMath;

namespace CrunchEconV3.Patches
{
    public static class ContractPatchesDefaultSetup
    {
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(contract).Prefixes.Add(contractPatch);
            ctx.GetPattern(getContracts).Suffixes.Add(getContractPatch);
            ctx.GetPattern(getContracsForBlock).Suffixes.Add(getContractForBlockPatch);
            ctx.GetPattern(ActivateContract).Suffixes.Add(contractResultPatch);
            ctx.GetPattern(abandonContract).Suffixes.Add(abandonContractPatch);
            ctx.GetPattern(getContractsStation).Suffixes.Add(getContractForStationPatch);
        }

        internal static readonly MethodInfo contract =
            typeof(MyContractBlock).GetMethod("AcceptContract",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");
        internal static readonly MethodInfo contractPatch =
            typeof(ContractPatchesDefaultSetup).GetMethod(nameof(PatchContract), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo abandonContract =
            typeof(MyContractBlock).GetMethod("AbandonContract",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo abandonContractPatch =
            typeof(ContractPatchesDefaultSetup).GetMethod(nameof(PatchAbandonContract), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo getContracts =
            typeof(MySessionComponentContractSystem).GetMethod("GetActiveContractsForPlayer_OB",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo getContracsForBlock =
            typeof(MySessionComponentContractSystem).GetMethod("GetAvailableContractsForBlock_OB",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo getContractsStation =
            typeof(MySessionComponentContractSystem).GetMethod("GetAvailableContractsForStation_OB",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo getContractPatch =
            typeof(ContractPatchesDefaultSetup).GetMethod(nameof(PatchGetContract), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo getContractForBlockPatch =
            typeof(ContractPatchesDefaultSetup).GetMethod(nameof(PatchGetContractForBlock), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo getContractForStationPatch =
            typeof(ContractPatchesDefaultSetup).GetMethod(nameof(PatchGetContractForStation), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo ActivateContract =
            typeof(MySessionComponentContractSystem).GetMethod("ActivateContract",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo contractResultPatch =
            typeof(ContractPatchesDefaultSetup).GetMethod(nameof(ActivateContractPatch), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static void ActivateContractPatch(
            long identityId,
            long contractId,
            long stationId,
            long blockId, ref MyContractResults __result)
        {
            if (AcceptedContractsIds.Contains(contractId))
            {
                __result = MyContractResults.Success;
                AcceptedContractsIds.Remove(contractId);

            }

            if (FailedContractIds.ContainsKey(contractId))
            {
                __result = FailedContractIds[contractId];
                FailedContractIds.Remove(contractId);
            }

        }

        public static void PatchGetContractForStation(MySessionComponentContractSystem __instance, long stationId,
            ref List<MyObjectBuilder_Contract> __result)

        {

            if (Core.config.RemoveHauling)
            {
                MySessionComponentContractSystem component = MySession.Static.GetComponent<MySessionComponentContractSystem>();

                foreach (var item in __result.Where(x => x is MyObjectBuilder_ContractDeliver))
                {
                    component.RemoveContract(item.Id);
                }

                __result = __result.Where(x => x is not MyObjectBuilder_ContractDeliver).ToList();
            }

            var needsRefresh = true;
        //    var needsRefresh = StationHandler.NPCNeedsRefresh(stationId);
            if (needsRefresh)
            {
      

                var removeThese = new string[] { "Deliver", };


                foreach (var item in __result.Where(x => x is MyObjectBuilder_ContractEscort))
                {
                    item.RewardMoney *= 3;
                }

                foreach (var item in __result.Where(x => x is MyObjectBuilder_ContractObtainAndDeliver))
                {
                    var condition = item.ContractCondition as MyObjectBuilder_ContractConditionDeliverItems;
                    var priceKey = $"{condition.ItemType.ToString()}";

                    var newPrice = PriceHelper.GetPriceModel(priceKey);
                    if (!newPrice.NotFound)
                    {
                        var sellPrice = newPrice.GetSellMinAndMaxPrice(true);
                        var between = Core.random.Next((int)sellPrice.Item1, (int)sellPrice.Item2);
                        item.RewardMoney = between * condition.ItemAmount;
                    }
                }

                var contracts = StationHandler.GenerateNewContracts(stationId);
                var built = BuildContracts(contracts);
                List<ICrunchContract> BlocksContracts = built.Item2;
                __result.AddRange(built.Item1);

                if (StationHandler.BlocksContracts.TryGetValue(stationId, out var cont))
                {
                    StationHandler.BlocksContracts[stationId] = BlocksContracts;
                }
                else
                {
                    StationHandler.BlocksContracts.Add(stationId, BlocksContracts);
                }
            }
            else
            {
                if (!StationHandler.BlocksContracts.TryGetValue(stationId, out var contracts)) return;
                foreach (var contract in contracts)
                {
                    var newContract = contract.BuildUnassignedContract();
                    if (newContract != null)
                    {
                        __result.Add(newContract);
                    }
                }
            }
        }
        public static void PatchGetContractForBlock(MySessionComponentContractSystem __instance, long blockId,
            ref List<MyObjectBuilder_Contract> __result)
        {

            var needsRefresh = StationHandler.NeedsRefresh(blockId);
            if (needsRefresh)
            {
                MySessionComponentContractSystem component = MySession.Static.GetComponent<MySessionComponentContractSystem>();

                foreach (var con in __result)
                {
                    component.RemoveContract(con.Id);
                }
                __result.Clear();
                var contracts = StationHandler.GenerateNewContracts(blockId);
                var built = BuildContracts(contracts);
                List<ICrunchContract> BlocksContracts = built.Item2;
                __result = built.Item1;

                if (StationHandler.BlocksContracts.TryGetValue(blockId, out var cont))
                {
                    StationHandler.BlocksContracts[blockId] = BlocksContracts;
                }
                else
                {
                    StationHandler.BlocksContracts.Add(blockId, BlocksContracts);
                }

            }
            else
            {
                if (!StationHandler.BlocksContracts.TryGetValue(blockId, out var contracts)) return;
                foreach (var contract in contracts)
                {
                    var newContract = contract.BuildUnassignedContract();
                    if (newContract != null)
                    {
                        __result.Add(newContract);
                    }
                }
            }
        }

        public static Tuple<List<MyObjectBuilder_Contract>, List<ICrunchContract>> BuildContracts(List<ICrunchContract> contracts)
        {
            List<MyObjectBuilder_Contract> BlocksContracts = new List<MyObjectBuilder_Contract>();
            List<ICrunchContract> Contracts = new List<ICrunchContract>();
            foreach (var contract in contracts)
            {
                string definition = contract.DefinitionId;
                string contractName = contract.Name;
                string contractDescription = contract.Description;

                if (MyDefinitionId.TryParse(definition, out var definitionId))
                {
                    MyObjectBuilder_Contract newContract;
                    newContract = new MyObjectBuilder_ContractCustom
                    {
                        SubtypeName = definition.Replace("MyObjectBuilder_ContractTypeDefinition/", ""),
                        Id = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.CONTRACT,
                            MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM) + Core.random.Next(1, 200),
                        IsPlayerMade = false,
                        State = MyContractStateEnum.Inactive,
                        Owners = new MySerializableList<long>(),
                        RewardMoney = contract.RewardMoney,
                        RewardReputation = contract.ReputationGainOnComplete,
                        StartingDeposit = contract.CollateralToTake,
                        FailReputationPrice = contract.ReputationLossOnAbandon,
                        StartFaction = 1,
                        StartStation = 0,
                        StartBlock = 1,
                        Creation = 1,
                        TicksToDiscard = (int?)contract.SecondsToComplete,
                        RemainingTimeInS = contract.SecondsToComplete,
                        ContractCondition = null,
                        DefinitionId = definitionId,
                        ContractName = contractName,
                        ContractDescription = contractDescription
                    };
                    contract.ContractId = newContract.Id;
                    BlocksContracts.Add(newContract);
                    Contracts.Add(contract);
                }
            }
            return Tuple.Create(BlocksContracts, Contracts);
        }

        public static void PatchGetContract(MySessionComponentContractSystem __instance, long identityId, ref List<MyObjectBuilder_Contract> __result)
        {
            MySessionComponentContractSystem component = MySession.Static.GetComponent<MySessionComponentContractSystem>();

            List<MyObjectBuilder_Contract> newList = new List<MyObjectBuilder_Contract>();
            var steamid = MySession.Static.Players.TryGetSteamId(identityId);
            var playerData = Core.PlayerStorage.GetData(steamid);
            if (playerData != null)
            {
                List<ICrunchContract> deleteThese = new List<ICrunchContract>();
                foreach (var contract in playerData.PlayersContracts.Values)
                {
                    MySession.Static.Players.TryGetPlayerBySteamId(playerData.PlayerSteamId, out var player);
                    if (contract.ReadyToDeliver)
                    {
                        try
                        {
                            var completed = contract.TryCompleteContract(playerData.PlayerSteamId, player.Character.PositionComp.GetPosition());
                            if (completed)
                            {
                                deleteThese.Add(contract);
                                Core.SendMessage("Contracts", $"{contract.Name} completed!, you have been paid.", Color.Green, player.Id.SteamId);
                                contract.DeleteDeliveryGPS();

                                continue;
                            }
                        }
                        catch (Exception exception)
                        {
                            Core.Log.Error($"Error on try complete {exception}");
                            deleteThese.Add(contract);
                        }
                        contract.SendDeliveryGPS();
                    }

                    var builder = contract.BuildAssignedContract();
                    if (builder != null)
                    {
                        __result.Add(builder);
                    }
                }

                if (!deleteThese.Any()) return;

                foreach (var contract in deleteThese)
                {
                    playerData.RemoveContract(contract);
                }
                Task.Run(async () =>
                {
                    Core.PlayerStorage.Save(playerData);
                });
            }
        }

        private static List<long> AcceptedContractsIds = new List<long>();

        private static Dictionary<long, MyContractResults>
            FailedContractIds = new Dictionary<long, MyContractResults>();

        public static void PatchAbandonContract(MyContractBlock __instance, long identityId, long contractId)
        {
            var steamid = MySession.Static.Players.TryGetSteamId(identityId);
            var playerData = Core.PlayerStorage.GetData(steamid);
            if (playerData == null) return;
            var contract = playerData.PlayersContracts.FirstOrDefault(x => x.Key == contractId).Value ?? null;
            if (contract == null) return;
            contract.FailContract();
            contract.DeleteDeliveryGPS();
            playerData.RemoveContract(contract);
            Task.Run(async () =>
            {
                Core.PlayerStorage.Save(playerData);
            });
            return;
        }

        public static bool PatchContract(MyContractBlock __instance, long identityId, long contractId)
        {
            MySessionComponentContractSystem component = MySession.Static.GetComponent<MySessionComponentContractSystem>();
            var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
            if (faction == null)
            {
                FailedContractIds.Add(contractId, MyContractResults.Success);
                return true;
            }
            var ID = __instance.EntityId;
            MyStation keenstation = null;
            if (faction.Stations.Any(x => x.StationEntityId == __instance.CubeGrid.EntityId))
            {
                keenstation = faction.Stations.FirstOrDefault(x => x.StationEntityId == __instance.CubeGrid.EntityId);
                ID = keenstation.Id;
            }
            if (StationHandler.BlocksContracts.TryGetValue(ID, out var contracts))
            {
                var contract = contracts.FirstOrDefault(x => x.ContractId == contractId);
                if (contract != null)
                {
                    var steamid = MySession.Static.Players.TryGetSteamId(identityId);
                    var playerData = Core.PlayerStorage.GetData(steamid);
                    if (playerData != null)
                    {
                        contract.FactionId = faction.FactionId;
                        var result = ContractAcceptor.TryAcceptContract(contract, playerData, identityId, __instance, keenstation, ID);
                        if (result.Item1)
                        {
                            AcceptedContractsIds.Add(contract.ContractId);
                            contracts.Remove(contract);
                            return true;
                        }
                        FailedContractIds.Add(contract.ContractId, result.Item2);
                        return true;
                    }
                }

            }

            return true;
        }
    }
}
