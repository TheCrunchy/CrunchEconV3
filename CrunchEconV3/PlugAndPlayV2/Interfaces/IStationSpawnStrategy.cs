using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using Sandbox.Game.World;

namespace CrunchEconV3.PlugAndPlayV2.Interfaces
{
    public interface IStationSpawnStrategy
    {
        public List<StationConfig> SpawnStations(List<MyFaction> availableFactions,string templateName, int maximumToSpawn);
    }
}
