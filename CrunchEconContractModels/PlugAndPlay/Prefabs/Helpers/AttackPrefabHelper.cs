using System;
using Sandbox.Game.Contracts;

namespace CrunchEconContractModels.PlugAndPlay.Prefabs.Helpers
{
    public class AttackPrefabHelper : PrefabsHelperAbstract
    {
        public override void Init()
        {
            MyContractEscort myContract = new MyContractEscort();
            var definition = myContract.GetDefinition();

            // Get the type of the internal class
            Type definitionType = definition.GetType();

            // Check if the type name matches the internal class name
            ReflectPrefabs(definitionType, definition, "Sandbox.Definitions.MyContractTypeEscortDefinition", "PrefabsEscortDrones");
        }
    }
}
