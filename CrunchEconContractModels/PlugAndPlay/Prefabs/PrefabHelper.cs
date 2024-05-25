using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.PlugAndPlay.Prefabs.Helpers;
using HarmonyLib;
using Sandbox.Game.Contracts;
using Torch.Managers.PatchManager;
using VRage.Utils;

namespace CrunchEconContractModels.PlugAndPlay
{
    public static class PrefabHelper
    {

        public static PrefabsHelperAbstract Repairs = new RepairPrefabHelper();
        public static PrefabsHelperAbstract CombatAttack = new AttackPrefabHelper();
        public static PrefabsHelperAbstract CombatEscort = new EscortPrefabHelper();
        public static PrefabsHelperAbstract Search = new SearchPrefabHelper();

        public static void Patch(PatchContext ctx)
        {
            Repairs.Init();
            CombatAttack.Init();
            CombatEscort.Init();
            Search.Init();
        }
    }
}
