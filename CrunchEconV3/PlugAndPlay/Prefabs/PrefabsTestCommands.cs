using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
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

        //[Command("listobjectbuildertypes", "Lists all MyObjectBuilder_* types in the game and saves them to a CSV file.")]
        //[Permission(MyPromoteLevel.None)]
        //public void ListObjectBuilderTypesCommand()
        //{

        //    Assembly gameAssembly = typeof(MyObjectBuilder_Base).Assembly;

        //    var types = gameAssembly.GetTypes()
        //        .Where(t => t.Name.StartsWith("MyObjectBuilder_") && !t.IsAbstract);

        //    foreach (var type in types)
        //    {
        //        var instance = Activator.CreateInstance(type);

        //        if (instance is MyObjectBuilder_FactionTypeDefinition)
        //        {
        //            Context.Respond("Got the thing");
        //            var def = instance as MyObjectBuilder_FactionTypeDefinition;
        //            if (def.OffersList != null && def.OffersList.Any())
        //            {
        //                Context.Respond("Has offers");
        //            }
        //            else
        //            {
        //                Context.Respond("has no offers, keeeeeeeeeeeeeen");
        //            }
        //        }
        //    }
        //}
    }
}
