using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Patches
{
    [PatchShim]
    public static class ContractNullChecker
    {
        private static int patchCount = 0;

        public static void Patch(PatchContext ctx)
        {

            patchCount++;
            if (patchCount > 1)
            {
                return;
            }

            Core.Log.Info("Patching button for grid sales");
            ctx.GetPattern(methodToPatch).Prefixes.Add(patchMethod);
        }

        internal static readonly MethodInfo methodToPatch =
            typeof(MyContractBlock).GetMethod("GetAllOwnedContractBlocks",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null ) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo patchMethod =
            typeof(ContractNullChecker).GetMethod(nameof(PatchedMethod), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");


        public static bool PatchedMethod(MyContractBlock __instance, long identityId)
        {
            List<MyContractBlock.MyEntityInfoWrapper> entityInfoWrapperList = new List<MyContractBlock.MyEntityInfoWrapper>();
            MyIdentity identity = MySession.Static.Players.TryGetIdentity(identityId);
            if (identity != null)
            {
                if (identity.BlockLimits == null)
                {
                    Core.Log.Info("GetAllOwnedContractBlocks identity.BlockLimits are null, preventing crash");
                    return false;
                }
                if (identity.BlockLimits.BlocksBuiltByGrid == null)
                {
                    Core.Log.Info("GetAllOwnedContractBlocks identity.BlockLimits.BlocksBuiltByGrid are null, preventing crash");
                    return false;
                }
                foreach (KeyValuePair<long, MyBlockLimits.MyGridLimitData> keyValuePair in identity.BlockLimits.BlocksBuiltByGrid)
                {
                    MyCubeGrid entity;
                    if (!Sandbox.Game.Entities.MyEntities.TryGetEntityById<MyCubeGrid>(keyValuePair.Key, out entity, false) || entity.BigOwners.Contains(identityId))
                    {
                        foreach (MySlimBlock block in entity.GetBlocks())
                        {
                            if (block.FatBlock != null && block.FatBlock is MyContractBlock)
                                entityInfoWrapperList.Add(new MyContractBlock.MyEntityInfoWrapper()
                                {
                                    NamePrefix = string.IsNullOrEmpty(entity.DisplayName) ? string.Empty : entity.DisplayName,
                                    NameSuffix = string.IsNullOrEmpty(block.FatBlock.DisplayNameText) ? string.Empty : block.FatBlock.DisplayNameText,
                                    Id = block.FatBlock.EntityId
                                });
                        }
                    }
                }
            }
            Core.Log.Info("GetAllOwnedContractBlocks didnt crash this time");
            return true;
        }
    }
}
