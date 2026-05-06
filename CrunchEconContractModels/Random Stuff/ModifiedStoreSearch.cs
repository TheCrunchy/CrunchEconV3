using System;
using System.Collections.Generic;
using System.Reflection;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.SessionComponents;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class ModifiedStoreSearch
    {
        internal static readonly MethodInfo getStores =
            typeof(MySessionComponentEconomy).GetMethod(
                "GetPlayersStoreBlocks",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? throw new Exception("Failed to find GetPlayersStoreBlocks");

        internal static readonly MethodInfo suffix =
            typeof(StorePatch).GetMethod(
                nameof(StorePatch.Suffix),
                BindingFlags.Static | BindingFlags.Public
            ) ?? throw new Exception("Failed to find Suffix");

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(getStores).Suffixes.Add(suffix);
        }
    }

    public static class StorePatch
    {
        public static void Suffix(ref HashSet<MyStoreBlock> __result)
        {
            
            var filtered = new HashSet<MyStoreBlock>();

            foreach (var store in __result)
            {
                if (store == null)
                    continue;

                //if its not searchable, skip it 
                if (!store.DisplayNameText.Contains("!SEARCHABLE!", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }
                
                if (store.IsFunctional)
                    filtered.Add(store);
            }

            __result = filtered;
        }
    }
}