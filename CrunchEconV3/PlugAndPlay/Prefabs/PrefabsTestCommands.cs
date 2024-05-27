using System.Collections.Generic;
using System.Linq;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlay.Prefabs
{
    public class PrefabsTestCommands : CommandModule
    {
        public static void HasSpawned()
        {

        }

        private List<string> Used = new List<string>();
        [Command("prefab", "try spawn a prefab")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportStore(string prefab)
        {
            MyPrefabManager.Static.SpawnPrefab(prefab, Context.Player.Character.PositionComp.GetPosition(), Vector3.Forward, Vector3.Up, ownerId: Context.Player.IdentityId);
        }

        [Command("combats", "try spawn a prefab")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportStore()
        {

            var randomCombat = PrefabHelper.CombatAttack.GetAllPrefabs();
            var random = randomCombat.Where(x => !Used.Contains(x)).ToList();

            MyPrefabManager.Static.SpawnPrefab(random.GetRandomItemFromList(), Context.Player.Character.PositionComp.GetPosition(), Vector3.Forward, Vector3.Up, ownerId: Context.Player.IdentityId);

        }
    }
}
