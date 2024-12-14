using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Utils;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlayV2.Helpers
{
    public static class DatapadHelper
    {
        public static Dictionary<string, List<string>>
            DatapadEntriesBySubtypes = new Dictionary<string, List<string>>();

        public static void Setup()
        {
            FileUtils utils = new FileUtils();
            var path = $"{Core.path}Datapads.json";
            if (File.Exists(path))
            {
                DatapadEntriesBySubtypes = utils.ReadFromJsonFile<Dictionary<string, List<string>>>(path);
            }
            else
            {
                DatapadEntriesBySubtypes.Add("Datapad", new List<string>() { "{StationGps}", "{StationGps}" });
                utils.WriteToJsonFile(path, DatapadEntriesBySubtypes);
            }
        }

        public static string GetRandomStation()
        {
            List<Tuple<Vector3D, long>> availablePositions = new List<Tuple<Vector3D, long>>();

            // If it's not a custom station, get random keen ones
            var stations = Core.StationStorage.GetAll()
                    .Where(x => x.UseAsDeliveryLocation)
                    .ToList();

            foreach (var station in stations)
            {
                var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                var GPS = GPSHelper.ScanChat(station.LocationGPS);
                availablePositions.Add(Tuple.Create(GPS.Coords, foundFaction.FactionId));
            }


            if (MySession.Static.Settings.EnableEconomy)
            {
                var positions = MySession.Static.Factions.GetNpcFactions()
                    .Where(x => x.Stations.Any())
                    .SelectMany(x => x.Stations)
                    .Select(x => Tuple.Create(x.Position, x.FactionId))
                    .ToList();
                availablePositions.AddRange(positions);
            }

            var chosen = availablePositions.GetRandomItemFromList();
            var faction = MySession.Static.Factions.TryGetFactionById(chosen.Item2);
            var gps = new MyGps()
            {
                Coords = chosen.Item1,
                Name = $"{faction.Tag} - Station",
                DisplayName = $"{faction.Tag} - Station",
            };
            return gps.ToString();
        }
    }
}
