using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.Network;
using VRage.ObjectBuilder;

namespace ChangeMe
{
    [PatchShim]
    public static class ContractPatchesNoLogic
    {
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(acceptContract).Prefixes.Add(acceptContractPatch);
            ctx.GetPattern(getContractsForBlock).Suffixes.Add(getContractForBlockPatch);
        }

        internal static readonly MethodInfo acceptContract =
            typeof(MyContractBlock).GetMethod("AcceptContract",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");
        internal static readonly MethodInfo acceptContractPatch =
            typeof(ContractPatchesNoLogic).GetMethod(nameof(PatchAcceptContract), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo getContractsForBlock =
            typeof(MySessionComponentContractSystem).GetMethod("GetAvailableContractsForBlock_OB",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo getContractForBlockPatch =
            typeof(ContractPatchesNoLogic).GetMethod(nameof(PatchGetContractForBlock), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");


        //god awful way to do this as it will never clear it in future currently, a better way would be storing a new class that contains an expiry time
        //and periodically clearing it 
        public static Dictionary<ulong, List<Tuple<long, string>>> AwaitingConfirms =
            new Dictionary<ulong, List<Tuple<long, string>>>();

        public static void PatchGetContractForBlock(MySessionComponentContractSystem __instance, long blockId,
            ref List<MyObjectBuilder_Contract> __result)
        {
            MyContractBlock location = (MyContractBlock)MyAPIGateway.Entities.GetEntityById(blockId);
            if (location != null)
            {
                if ((!string.IsNullOrWhiteSpace(location.DisplayNameText) && !location.DisplayNameText.Contains("Hangar")))
                {
                    return;
                }
            }

            var id = MyEventContext.Current.Sender;
            //clear the players waiting list
            AwaitingConfirms.Remove(id.Value);
            Core.Log.Info(id.Value);
            MySessionComponentContractSystem component = MySession.Static.GetComponent<MySessionComponentContractSystem>();

            foreach (var con in __result)
            {
                component.RemoveContract(con.Id);
            }

            __result.Clear();

            var built = BuildContracts(id.Value);
            __result = built;

            //store these by contract id 
        }
        private static Random random = new Random();
        public static List<MyObjectBuilder_Contract> BuildContracts(ulong steamId)
        {
            List<MyObjectBuilder_Contract> BlocksContracts = new List<MyObjectBuilder_Contract>();
            //or do some other stuff if file name matters
            var playerships = new List<string>() { "Ship1", "Ship2" };

            //loop over the ships here 
            foreach (var ship in playerships)
            {
                string definition = "MyObjectBuilder_ContractTypeDefinition/ObtainAndDeliver";
                string contractName = $"Retrieve ship {ship}";
                string contractDescription = "Accept me to load Ship Name";

                if (MyDefinitionId.TryParse(definition, out var definitionId))
                {
                    MyObjectBuilder_Contract newContract;
                    newContract = new MyObjectBuilder_ContractCustom
                    {
                        SubtypeName = definition.Replace("MyObjectBuilder_ContractTypeDefinition/", ""),
                        Id = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.CONTRACT,
                            MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM) + random.Next(1, 200),
                        IsPlayerMade = false,
                        State = MyContractStateEnum.Inactive,
                        Owners = new MySerializableList<long>(),
                        RewardMoney = 0,
                        RewardReputation = 0,
                        //change this if you want it to cost anything to load a ship
                        StartingDeposit = 0,
                        FailReputationPrice = 0,
                        StartFaction = 1,
                        StartStation = 0,
                        StartBlock = 1,
                        Creation = 1,
                        TicksToDiscard = null,
                        RemainingTimeInS = 8888888,
                        ContractCondition = null,
                        DefinitionId = definitionId,
                        ContractName = contractName,
                        ContractDescription = contractDescription
                    };
                  
                    BlocksContracts.Add(newContract);
                    var entry = Tuple.Create(newContract.Id, "ship name");
                    if (AwaitingConfirms.TryGetValue(steamId, out var itemsInHangar))
                    {
                        itemsInHangar.Add(entry);
                    }
                    else
                    {
                        AwaitingConfirms.Add(steamId, new List<Tuple<long, string>>() { entry});
                    }
                }
            }

            return BlocksContracts;
        }

        public static bool PatchAcceptContract(MyContractBlock __instance, long identityId, long contractId)
        {
            //read what the contract is from the dictionary of contract Ids 
            //if its one you are tracking, return false 
            var steamid = MySession.Static.Players.TryGetSteamId(identityId);

            if (steamid != 0)
            {
                if (AwaitingConfirms.TryGetValue(steamid, out var hangarLoads))
                {
                    var hangarLoad = hangarLoads.FirstOrDefault(x => x.Item1 == contractId);
                    if (hangarLoad != null)
                    {
                        Core.Log.Info("Load the ship");
                        //load the ship
                    }
                }
            }

            return true;
        }
    }
}
