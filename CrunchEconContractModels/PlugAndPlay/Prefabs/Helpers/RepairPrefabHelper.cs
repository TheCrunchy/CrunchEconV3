using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.Contracts;

namespace CrunchEconContractModels.PlugAndPlay
{
    public class RepairPrefabHelper : PrefabsHelperAbstract
    {
        public override void Init()
        {
            MyContractRepair myContract = new MyContractRepair();
            var definition = myContract.GetDefinition();

            // Get the type of the internal class
            Type definitionType = definition.GetType();

            // Check if the type name matches the internal class name
            ReflectPrefabs(definitionType, definition, "Sandbox.Definitions.MyContractTypeRepairDefinition", "PrefabNames");
        }
    }
}
