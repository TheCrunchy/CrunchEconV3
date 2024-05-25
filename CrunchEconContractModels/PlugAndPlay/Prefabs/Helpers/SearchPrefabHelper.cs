using System;
using Sandbox.Game.Contracts;

namespace CrunchEconContractModels.PlugAndPlay.Prefabs.Helpers
{
    public class SearchPrefabHelper : PrefabsHelperAbstract
    {
        public override void Init()
        {
            MyContractFind myContract = new MyContractFind();
            var definition = myContract.GetDefinition();

            // Get the type of the internal class
            Type definitionType = definition.GetType();

            // Check if the type name matches the internal class name
            ReflectPrefabs(definitionType, definition, "Sandbox.Definitions.MyContractTypeFindDefinition", "PrefabsSearchableGrids");
        }
    }
}
