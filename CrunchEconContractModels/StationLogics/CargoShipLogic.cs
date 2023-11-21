using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrunchEconV3.APIs;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath; // Consider having the extension method in an appropriate namespace

namespace CrunchEconV3.Interfaces
{
    public class CargoShipLogic : IStationLogic
    {
        private DateTime lastSpawnTime;

        public void Setup()
        {
            // Initialize the last spawn time to the current time
            lastSpawnTime = DateTime.Now;
        }

        public Task<bool> DoLogic(MyCubeGrid grid)
        {
	
            // Check if 2 minutes have passed since the last spawn
            if ((DateTime.Now - lastSpawnTime).TotalMinutes >= 0.25)
            {
                CrunchEconV3.Core.Log.Info("Trying to spawn");
                // Update the last spawn time to the current time
                lastSpawnTime = DateTime.Now;
                Vector3D randomDirection = new Vector3D(
                    Core.random.NextDouble() * 2 - 1,
                    Core.random.NextDouble() * 2 - 1,
                    Core.random.NextDouble() * 2 - 1);

                // Normalize the direction vector
                randomDirection.Normalize();

                // Generate a random distance within the specified range (10 km)
                double randomDistance = Core.random.NextDouble() * 3000; // 10 km in meters
                Vector3D gridPosition = grid.PositionComp.GetPosition();
                // Calculate the random position within 10 km of the grid
                Vector3D randomPosition = gridPosition + randomDirection * randomDistance;

                // Get the grid's position
            
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    var pos = MatrixD.CreateWorld(randomPosition, grid.WorldMatrix.Forward, grid.WorldMatrix.Up);
                    List<string> cargoShips = new List<string>
                    {
                        "Reaver-SpawnGroup-Invader-Space",
                    };
                    CrunchEconV3.Core.Log.Info($"Ready? {Core.MesAPI.MESApiReady}");
                    bool spawned = CrunchEconV3.Core.MesAPI.CustomSpawnRequest(cargoShips, pos, new Vector3(3),true, "SPRT", "23232");
                    CrunchEconV3.Core.Log.Info($"{spawned}");
                });


                // Return true to continue logic execution
                return Task.FromResult(true);
            }

            // Return true to continue logic execution
            return Task.FromResult(true);
        }

        public int Priority { get; set; }
    }
}
