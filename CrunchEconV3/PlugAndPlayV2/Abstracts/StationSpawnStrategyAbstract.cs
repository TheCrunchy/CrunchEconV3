using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlayV2.Handlers;
using CrunchEconV3.PlugAndPlayV2.Interfaces;
using CrunchEconV3.PlugAndPlayV2.StationLogics;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMySlimBlock = VRage.Game.ModAPI.IMySlimBlock;

namespace CrunchEconV3.PlugAndPlayV2.Abstracts
{
    public abstract class StationSpawnStrategyAbstract : IStationSpawnStrategy
    {
        public abstract List<StationConfig> SpawnStations(List<MyFaction> availableFactions, string templateName, int maximumToSpawn, List<MyPlanet> planets = null);

        private readonly Vector3 MASK_COLOR = new Vector3(1f, 0.2f, 0.55f);

        protected virtual void RecolorBlocks(IEnumerable<IMyEntity> prefabEntities, SerializableVector3? color)
        {
            // Iterate through all entities in the prefab
            foreach (var entity in prefabEntities)
            {
                if (entity is IMyCubeGrid cubeGrid)
                {
                    // Get all blocks in the grid
                    var blockList = new List<IMySlimBlock>();
                    cubeGrid.GetBlocks(blockList);

                    foreach (var slimBlock in blockList)
                    {
                        // Check if the block is a cube block and recolor it
                        if (slimBlock is MySlimBlock slim)
                        {
                            if (((Vector3)slimBlock.ColorMaskHSV).Equals(MASK_COLOR, 0.01f))
                            {
                                slim.ColorMaskHSV = color ?? Vector3.One; // Default to white if no color is provided
                            }
                        }
                    }
                }
            }
        }

        protected virtual bool DoesTemplateExist(string templateName)
        {
            var templateStation = TemplateHandler.GetTemplateFromName(templateName);
            return templateStation != null;
        }

        protected virtual StationConfig GenerateAtLocation(Vector3D location, string npcFacTag, string templateName)
        {
            var gps = GPSHelper.CreateGps(location, Color.Orange, "Economy Station", "");

            var templateStation = TemplateHandler.GetTemplateFromName(templateName);
            if (templateStation == null)
            {
                return null;
            }
            var cloned = templateStation.Clone();
            cloned.LocationGPS = gps.ToString();
            cloned.Enabled = true;
            cloned.FactionTag = npcFacTag;
            cloned.FileName = $"{Guid.NewGuid()}.json";
            cloned.UsedTemplate = templateName;
            return cloned;
        }
    }
}
