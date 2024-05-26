using CrunchEconContractModels.PlugAndPlay.Prefabs;
using CrunchEconContractModels.PlugAndPlay.Prefabs.Helpers;
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
            Repairs.Init();
            CombatAttack.Init();
            CombatEscort.Init();
            Search.Init();
        }
    }
}
