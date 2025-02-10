﻿using CrunchEconV3;
using CrunchEconV3.PlugAndPlay.Prefabs;
using CrunchEconV3.PlugAndPlay.Prefabs.Helpers;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.PlugAndPlay.Helpers
{
    public static class PrefabHelper
    {
        public static PrefabsHelperAbstract Repairs = new RepairPrefabHelper();
        public static PrefabsHelperAbstract CombatAttack = new AttackPrefabHelper();
        public static PrefabsHelperAbstract CombatEscort = new EscortPrefabHelper();
        public static PrefabsHelperAbstract Search = new SearchPrefabHelper();

        public static void Patch(PatchContext ctx)
        {
            try
            {
                Repairs.Init();
                CombatAttack.Init();
                CombatEscort.Init();
                Search.Init();
            }
            catch (System.Exception e)
            {
                Core.Log.Error(e);
            }
        }
    }
}
