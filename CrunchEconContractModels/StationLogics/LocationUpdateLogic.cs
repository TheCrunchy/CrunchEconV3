using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using NLog.Fluent;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Groups;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace CrunchEconContractModels.StationLogics
{
    public class LocationUpdateLogic : IStationLogic
    {
        public void Setup()
        {
            
        }

        public Task<bool> DoLogic(MyCubeGrid grid)
        {
            if (DateTime.Now >= NextRefresh)
            {
                NextRefresh = DateTime.Now.AddSeconds(SecondsBetweenRefresh);
            }
            else
            {
                return Task.FromResult(true);
            }

            if (DebugMessages)
            {
                Core.Log.Error("Location Update running");
            }

            var Station = Core.StationStorage.GetAll().FirstOrDefault(x => x.GetGrid() == grid);
            if (Station == null)
            {
                if (DebugMessages)
                {
                    Core.Log.Error("Station grid is null");
                }

                return Task.FromResult(true);
            }
            
            var gps = GPSHelper.CreateGps(grid.PositionComp.GetPosition(), Color.Orange, "Station", "").ToString();

            Station.LocationGPS = gps.ToString();
            Station.UseAsDeliveryLocation = false;

            if (DebugMessages)
            {
                Core.Log.Error("beginning store loop");
            }

            if (DebugMessages)
            {
                Core.Log.Error($"Ending location update loop");
            }

            return Task.FromResult(true);
        }

        public int Priority { get; set; }
        public DateTime NextRefresh { get; set; }
        public int SecondsBetweenRefresh = 1;
        public bool DebugMessages { get; set; } = false;
    }
}
