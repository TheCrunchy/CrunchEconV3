using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    public class SphereGridTeleporter
    {
        public static void Patch(PatchContext ctx)
        {
            Core.UpdateCycle += Update100;
        }

        public static void Update100()
        {
            CheckAndTeleportGrids();
        }

        private static void CheckAndTeleportGrids()
        {
            var allGrids = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(allGrids, e => e is IMyCubeGrid);

            foreach (IMyCubeGrid grid in allGrids)
            {
                TeleportGridToEdge(grid);
            }
        }

        public static Guid GetGridId(IMyCubeGrid grid)
        {
            var parsedId = Guid.Parse("464edcf2-15d6-4ccc-96a3-e74fcf0ddcfc");
            MyModStorageComponentBase storage = grid.Storage;
            if (storage == null)
            {
                return Guid.Empty;
            }

            return storage.ContainsKey(parsedId) ? Guid.Parse(storage.GetValue(parsedId)) : Guid.Empty;
        }

        private static void TeleportGridToEdge(IMyCubeGrid grid)
        {
            var gridPosition = grid.PositionComp.GetPosition();
            var gridsKnownId = GetGridId(grid);
            foreach (var sphere in SphereManagement.Spheres)
            {
                var sphereCenter = sphere.Key;
                var sphereRadius = sphere.Value;
                var distance = Vector3D.Distance(gridPosition, sphereCenter);
                bool previouslyOutside = distance > sphereRadius;

                if (previouslyOutside)
                {
                    var direction = Vector3D.Normalize(gridPosition - sphereCenter);
                    var newPosition = sphereCenter + direction * sphereRadius;
                    var newWorldMatrix = MatrixD.CreateTranslation(newPosition);

                    grid.Teleport(newWorldMatrix);
                    SendMessage(grid, "You have left the sphere.");
                }
                else
                {
                    SendMessage(grid, "You entered the sphere.");
                }
            }
        }

        private static void SendMessage(IMyCubeGrid grid, string message)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p.Controller?.ControlledEntity?.Entity.GetTopMostParent() == grid);

            foreach (var player in players)
            {
                ulong steamId = player.SteamUserId; // Correct type for SteamUserId
                CrunchEconV3.Core.SendMessage("Contracts", message, Color.Red, steamId);
            }
        }
    }

    public static class SphereManagement
    {
        public static Dictionary<Vector3D, int> Spheres = new Dictionary<Vector3D, int>()
        {
            { new Vector3D(0, 0, 0), 2500 },
        };
    }
}
