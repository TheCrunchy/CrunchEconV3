using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;

namespace CrunchEconV3.PlugAndPlayV2.Helpers
{
    public static class KeenStationPrefabHelper
    {
        public static string GetRandomStationPrefabName(MyStationTypeEnum stationType)
        {
            SetupDefinitions();

            switch (stationType)
            {
                case MyStationTypeEnum.MiningStation:
                    return GetRandomStationName(miningDefinitions);
                case MyStationTypeEnum.OrbitalStation:
                    return GetRandomStationName(orbitalDefinitions);
                case MyStationTypeEnum.Outpost:
                    return GetRandomStationName(outpostsDefinitions);
                case MyStationTypeEnum.SpaceStation:
                    return GetRandomStationName(spaceDefinitions);
                default:
                    return string.Empty;
            }
        }

        private static string GetRandomStationName(MyStationsListDefinition stationsListDef)
        {
            if (stationsListDef == null)
                return "Economy_SpaceStation_1";
            int index = MyRandom.Instance.Next(0, stationsListDef.StationNames.Count);
            return stationsListDef.StationNames[index].ToString();
        }

        private static MyDefinitionId? spaceId = null;
        private static MyDefinitionId? orbitalId = null;
        private static MyDefinitionId? outpostsId = null;
        private static MyDefinitionId? miningId = null;

        private static MyStationsListDefinition spaceDefinitions = null;
        private static MyStationsListDefinition orbitalDefinitions = null;
        private static MyStationsListDefinition outpostsDefinitions = null;
        private static MyStationsListDefinition miningDefinitions = null;

        private static void SetupDefinitions()
        {
            spaceId ??= new MyDefinitionId(typeof(MyObjectBuilder_StationsListDefinition), "SpaceStations");
            orbitalId ??= new MyDefinitionId(typeof(MyObjectBuilder_StationsListDefinition), "OrbitalStations");
            outpostsId ??= new MyDefinitionId(typeof(MyObjectBuilder_StationsListDefinition), "Outposts");
            miningId ??= new MyDefinitionId(typeof(MyObjectBuilder_StationsListDefinition), "MiningStations");
            if (spaceId != null)
            {
                spaceDefinitions ??= MyDefinitionManager.Static.GetDefinition<MyStationsListDefinition>(spaceId.Value);
            }

            if (orbitalId != null)
            {
                orbitalDefinitions ??= MyDefinitionManager.Static.GetDefinition<MyStationsListDefinition>(orbitalId.Value);
            }

            if (outpostsId != null)
            {
                outpostsDefinitions ??= MyDefinitionManager.Static.GetDefinition<MyStationsListDefinition>(outpostsId.Value);
            }

            if (miningId != null)
            {
                miningDefinitions ??= MyDefinitionManager.Static.GetDefinition<MyStationsListDefinition>(miningId.Value);
            }

        }
    }
}
