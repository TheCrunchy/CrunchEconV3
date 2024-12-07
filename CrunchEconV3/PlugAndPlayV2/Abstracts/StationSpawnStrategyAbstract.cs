using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlayV2.Interfaces;
using CrunchEconV3.PlugAndPlayV2.StationLogics;
using CrunchEconV3.Utils;
using Sandbox.Game.World;
using VRageMath;

namespace CrunchEconV3.PlugAndPlayV2.Abstracts
{
    public abstract class StationSpawnStrategyAbstract : IStationSpawnStrategy
    {
        public abstract List<StationConfig> SpawnStations(List<MyFaction> AvailableFactions,string TemplateName, int MaximumToSpawn);

        protected virtual StationConfig GenerateAtLocation(Vector3D location, string npcFacTag, string templateName)
        {
            var gps = GPSHelper.CreateGps(location, Color.Orange, "Economy Station", "");
            var templateStation = new StationConfig();

            var cloned = templateStation.Clone();
            cloned.LocationGPS = gps.ToString();
            cloned.Enabled = true;
            cloned.FactionTag = npcFacTag;

            return cloned;
        }
    }
}
