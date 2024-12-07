using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;

namespace CrunchEconV3.PlugAndPlayV2.Interfaces
{
    public interface IStationSpawnStrategy
    {
        public List<StationConfig> SpawnStations(string templateName, int maximumToSpawn);
    }
}
