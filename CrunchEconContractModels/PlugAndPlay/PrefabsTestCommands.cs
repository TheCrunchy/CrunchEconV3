using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using NLog.LayoutRenderers;
using Sandbox.Definitions;
using Sandbox.Game.Contracts;
using Sandbox.Game.Entities;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.PlugAndPlay
{
    public class PrefabsTestCommands : CommandModule
    {
        public static void HasSpawned()
        {

        }
        [Command("testprefab2", "try spawn a prefab")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportStore()
        {
            MyContractRepair myContractRepair = new MyContractRepair();
            var definition = myContractRepair.GetDefinition();

            // Get the type of the internal class
            Type definitionType = definition.GetType();

            // Check if the type name matches the internal class name
            if (definitionType.FullName == "Sandbox.Definitions.MyContractTypeRepairDefinition")
            {
                // Use reflection to get the 'PrefabNames' field
                var prefabNamesField = definitionType.GetField("PrefabNames", BindingFlags.Public | BindingFlags.Instance);
                if (prefabNamesField != null)
                {
                    var prefabNames = prefabNamesField.GetValue(definition) as List<string>;
                    if (prefabNames != null)
                    {
                      //  Context.Respond(string.Join(",", prefabNames));
                        var resultList = new List<MyCubeGrid>();
                        Stack<Action> Callbacks = new Stack<Action>();
                        Callbacks.Push(() =>
                        {
                            Context.Respond($"{resultList.Count}");
                            Context.Respond("Callback");
                        });
                        MyPrefabManager.Static.SpawnPrefab(resultList,prefabNames.GetRandomItemFromList(), Context.Player.Character.PositionComp.GetPosition(), Vector3.Forward, Vector3.Up, ownerId:Context.Player.IdentityId, callbacks:Callbacks);

                    }
                    else
                    {
                        Context.Respond("PrefabNames field is null or not a string array.");
                    }
                }
                else
                {
                    Context.Respond("PrefabNames field not found.");
                }
            }
            else
            {
                Context.Respond("It's not what I want");
            }
        }
    }
}
