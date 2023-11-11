using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;

namespace CrunchEconV3.Patches
{
    [PatchShim]
   public static class ContractPatches
    {
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(contract).Prefixes.Add(contractPatch);
            ctx.GetPattern(getContracts).Suffixes.Add(getContractPatch);
            ctx.GetPattern(getContracsForBlock).Suffixes.Add(getContractForBlockPatch);
        }

        internal static readonly MethodInfo contract =
            typeof(MyContractBlock).GetMethod("AcceptContract",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");
        internal static readonly MethodInfo contractPatch =
            typeof(ContractPatches).GetMethod(nameof(PatchContract), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo getContracts =
            typeof(MySessionComponentContractSystem).GetMethod("GetActiveContractsForPlayer_OB",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo getContracsForBlock =
            typeof(MySessionComponentContractSystem).GetMethod("GetAvailableContractsForBlock_OB",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo getContractPatch =
            typeof(ContractPatches).GetMethod(nameof(PatchGetContract), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo getContractForBlockPatch =
            typeof(ContractPatches).GetMethod(nameof(PatchGetContractForBlock), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");


        public static void PatchGetContractForBlock(MySessionComponentContractSystem __instance, long blockId,
           ref List<MyObjectBuilder_Contract> __result)
        {
            var needsRefresh = StationHandler.NeedsRefresh(blockId);
            if (needsRefresh)
            {
                var contracts = StationHandler.GenerateNewContracts(blockId);

                List<ICrunchContract> BlocksContracts = new List<ICrunchContract>();
                foreach (var contract in contracts)
                {
                    string definition = contract.DefinitionId;
                    string contractName = contract.Name;
                    string contractDescription= contract.Description;
                    
                    if (MyDefinitionId.TryParse(definition, out var definitionId))
                    {
                        MyObjectBuilder_Contract newContract;
                        newContract = new MyObjectBuilder_ContractCustom
                        {
                            SubtypeName = definition.Replace("MyObjectBuilder_ContractTypeDefinition/", ""),
                            Id = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.CONTRACT, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM),
                            IsPlayerMade = false,
                            State = MyContractStateEnum.Inactive,
                            Owners = new MySerializableList<long>(),
                            RewardMoney = contract.RewardMoney,
                            RewardReputation = contract.ReputationGainOnComplete,
                            StartingDeposit = 0,
                            FailReputationPrice = contract.ReputationLossOnAbandon,
                            StartFaction = 1,
                            StartStation = 0,
                            StartBlock = 1,
                            Creation = 1,
                            TicksToDiscard = (DateTime.Now.AddSeconds(contract.SecondsToComplete) - DateTime.Now).Seconds,
                            RemainingTimeInS = (DateTime.Now.AddSeconds(contract.SecondsToComplete) - DateTime.Now).Seconds,
                            ContractCondition = null,
                            DefinitionId = definitionId,
                            ContractName = contractName,
                            ContractDescription = contractDescription
                        };
                        contract.ContractId = newContract.Id;
                        BlocksContracts.Add(contract);

                        __result.Add(newContract);
                    }
                    
                }

                if (StationHandler.BlocksContracts.TryGetValue(blockId, out var cont))
                {
                    StationHandler.BlocksContracts[blockId] =BlocksContracts;
                }
                else
                {
                    StationHandler.BlocksContracts.Add(blockId, BlocksContracts);
                }
             
            }
        }

        public static void PatchGetContract(MySessionComponentContractSystem __instance, long identityId, ref List<MyObjectBuilder_Contract> __result)
        {
            List<MyObjectBuilder_Contract> newList = new List<MyObjectBuilder_Contract>();
            Core.Log.Info("Get accepted contracts");
            //for (int i = 0; i <= 10; i++)
            //{
            //    if (MyDefinitionId.TryParse("MyObjectBuilder_ContractTypeDefinition/CrunchContract", out var definitionId))
            //    {
            //        MyObjectBuilder_ContractCustom newContract = new MyObjectBuilder_ContractCustom
            //        {
            //            SubtypeName = "Crunch",
            //            Id = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.CONTRACT, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM),
            //            IsPlayerMade = false,
            //            State = MyContractStateEnum.Inactive,
            //            Owners = new MySerializableList<long>(),
            //            RewardMoney = 0,
            //            RewardReputation = 0,
            //            StartingDeposit = 0,
            //            FailReputationPrice = 0,
            //            StartFaction = 0,
            //            StartStation = 0,
            //            StartBlock = 1,
            //            Creation = 1,
            //            TicksToDiscard = 500000000,
            //            RemainingTimeInS = 500000000,
            //            ContractCondition = null,
            //            DefinitionId = definitionId,
            //            ContractName = $"Hope this works {i}",
            //            ContractDescription = "HELLO"
            //        };

            //        __result.Add(newContract);
            //    }
            //}

        }

        public static bool PatchContract(MyContractBlock __instance, long identityId, long contractId)
        {
            MySessionComponentContractSystem component = MySession.Static.GetComponent<MySessionComponentContractSystem>();
            Core.Log.Info(component.GetContractDefinitionId(contractId)?.SubtypeName ?? "");
            //   GroupPlugin.Log.Info(component.GetContractDefinitionId(contractId)?. ?? "");
            
            Core.Log.Info("contract accepted");
            if (MyDefinitionId.TryParse("MyObjectBuilder_ContractTypeDefinition/CrunchContract", out var definitionId))
            {
                MyContractBlock block = __instance;
                Sandbox.ModAPI.Contracts.MyContractCustom newContract = new Sandbox.ModAPI.Contracts.MyContractCustom(definitionId: definitionId,
                    startBlockId: block.EntityId,
                    moneyReward: 1,
                    collateral: 1,
                    duration: 10000000,
                    name: "Test Contract",
                    description: "Test Description",
                    reputationReward: 0,
                    failReputationPrice: 0,
                    endBlockId: block.EntityId);

                var result = component.AddContract(newContract);

                component.SendNotificationToPlayer(MyContractNotificationTypes.ContractSuccessful, identityId);

                return false;
            }
            else
            {
                Core.Log.Error("Null definition Id");
            }

            return true;
        }

    }
}
