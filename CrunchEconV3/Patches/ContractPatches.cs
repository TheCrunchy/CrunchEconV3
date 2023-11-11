using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
            ctx.GetPattern(contract).Suffixes.Add(contractPatch);
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
            Core.Log.Info("Getting available contracts");
        }

        public static void PatchGetContract(MySessionComponentContractSystem __instance, long identityId, ref List<MyObjectBuilder_Contract> __result)
        {
            List<MyObjectBuilder_Contract> newList = new List<MyObjectBuilder_Contract>();
            Core.Log.Info("Get accepted contracts");
            for (int i = 0; i <= 10; i++)
            {
                if (MyDefinitionId.TryParse("MyObjectBuilder_ContractTypeDefinition/CrunchContract", out var definitionId))
                {
                    MyObjectBuilder_ContractCustom newContract = new MyObjectBuilder_ContractCustom
                    {
                        SubtypeName = "Crunch",
                        Id = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.CONTRACT, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM),
                        IsPlayerMade = false,
                        State = MyContractStateEnum.Inactive,
                        Owners = new MySerializableList<long>(),
                        RewardMoney = 0,
                        RewardReputation = 0,
                        StartingDeposit = 0,
                        FailReputationPrice = 0,
                        StartFaction = 0,
                        StartStation = 0,
                        StartBlock = 1,
                        Creation = 1,
                        TicksToDiscard = 500000000,
                        RemainingTimeInS = 500000000,
                        ContractCondition = null,
                        DefinitionId = definitionId,
                        ContractName = $"Hope this works {i}",
                        ContractDescription = "HELLO"
                    };

                    __result.Add(newContract);
                }
            }

        }

        public static void PatchContract(MyContractBlock __instance, long identityId, long contractId)
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
            }
            else
            {
                Core.Log.Error("Null definition Id");
            }

            return;
        }

    }
}
