using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Definitions;
using VRage.Utils;

namespace CrunchEconContractModels.PlugAndPlay.Prefabs
{
    public abstract class PrefabsHelperAbstract
    {
        protected List<string> _prefabs = new List<string>();

        public string GetRandomPrefab()
        {
            return _prefabs.GetRandomItemFromList();
        }

        public List<string> GetAllPrefabs()
        {
            return _prefabs.ToList();
        }

        internal void ReflectPrefabs(Type definitionType, MyContractTypeDefinition definition, string expectedName, string fieldName)
        {
            if (definitionType.FullName == expectedName)
            {
                // Use reflection to get the 'PrefabNames' field
                var prefabNamesField = definitionType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (prefabNamesField != null)
                {
                    var prefabNames = prefabNamesField.GetValue(definition) as List<string>;
                    if (prefabNames != null)
                    {
                        _prefabs = prefabNames;
                    }
                }
            }
        }
        public abstract void Init();
    }
}