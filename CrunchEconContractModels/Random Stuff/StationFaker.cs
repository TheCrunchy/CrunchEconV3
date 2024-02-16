using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class StationFaker
    {
        public static void Patch(PatchContext ctx)
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            Core.Fakes.Clear();
            // Iterate through all existing grids when the mod initializes
            var grids = new List<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    OnEntityAdd(entity);
                }
                return false;
            });
        }
        private static void OnEntityAdd(IMyEntity entity)
        {
            if (entity is MyCubeGrid grid)
            {
                if (!grid.IsStatic)
                {
                    return;
                }
                if (grid.EntityId != grid.GetBiggestGridInGroup().EntityId)
                {
                    return;
                }

                var owner = FacUtils.GetOwner((Sandbox.Game.Entities.MyCubeGrid)grid);
                var faction = FacUtils.GetPlayersFaction(owner);
                if (faction != null && Core.config.KeenNPCContracts.Any(x => x.NPCFactionTags.Contains(faction.Tag)))
                {
                    if (grid.GetFatBlocks().OfType<MyContractBlock>().Any())
                    {
                        var entry = Core.config.KeenNPCContracts.FirstOrDefault(x =>
                            x.NPCFactionTags.Contains(faction.Tag));
                        var fake = new StationConfig();
                        foreach (var item in entry.ContractFiles)
                        {
                            if (item.Contains("/Stations/"))
                            {
                                fake.ContractFiles.Add(item.Replace("/Stations", ""));
                            }
                            else
                            {
                                fake.ContractFiles.Add(item);
                            }
                        }
                        var gps = new MyGps();
                        fake.FactionTag = faction.Tag;
                        gps.Name = "Fake Station";
                        gps.Coords = grid.GetBiggestGridInGroup().PositionComp.GetPosition();
                        fake.LocationGPS = gps.ToString();
                        fake.SetFake();
                        fake.SetGrid(grid.GetBiggestGridInGroup());
                        fake.FileName = grid.GetBiggestGridInGroup().DisplayName;
                        Core.Fakes.Add(fake);
                        Core.Log.Info("Adding a fake");
                    }
                }
            }
        }
    }
}
