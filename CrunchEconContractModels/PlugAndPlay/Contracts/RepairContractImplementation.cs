using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconContractModels.PlugAndPlay.Prefabs;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.Library.Collections;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.PlugAndPlay.Contracts
{
    public class RepairContractImplementation : ContractAbstract
    {
        private MyCubeGrid Grid { get; set; }
        public long GridEntityId { get; set; }
        public bool HasSpawnedGrid { get; set; } = false;
        public bool DeleteGridOnComplete { get; set; }
        private bool brokeBlocks { get; set; } = false;
        public int BlocksToRepair { get; set; }

        public DateTime NextMessage = DateTime.Now;
        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must repair the grid found at the Repair location GPS.";
            return BuildUnassignedContract(contractDescription);
        }

        private bool SetupFailCondition = false;
        public override bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (this.brokeBlocks)
            {
                return true;
            }
            if (Core.NexusInstalled)
            {
                var thisSector = NexusAPI.GetThisServer();

                var sector = NexusAPI.GetServerIDFromPosition(DeliverLocation);

                if (sector != thisSector.ServerID)
                {
                    return false;
                }
            }
            if (HasSpawnedGrid && GetGrid() != null)
            {
                if (Grid.Closed)
                {
                    FailContract();
                    return true;
                }
       
                if (GridEntityId == 0)
                {
                    GridEntityId = GetGrid().EntityId;
                }

                if (!SetupFailCondition)
                {
                    Grid.OnClosing += MarkClose;
                    Grid.OnBlockRemoved += BlockRemoved;
                    SetupFailCondition = true;
                }
                var repair = GetGrid().CubeBlocks.Count(cubeBlock => !cubeBlock.ComponentStack.IsFunctional);

                BlocksToRepair = repair;
            }

            if (GetGrid() != null && !HasSpawnedGrid)
            {
                HasSpawnedGrid = true;
                return false;
            }

            if (this.BlocksToRepair > 0 && DateTime.Now >= NextMessage)
            {
                NextMessage = NextMessage.AddMinutes(1);
                Core.SendMessage("Contracts", $"{this.BlocksToRepair} blocks left to repair.", Color.Yellow,
                    AssignedPlayerSteamId);
            }
            if (this.BlocksToRepair <= 0 && HasSpawnedGrid)
            {
                return TryCompleteContract(this.AssignedPlayerSteamId, PlayersCurrentPosition);
            }

            if (DateTime.Now > ExpireAt)
            {
                FailContract();
                return true;
            }

            if (HasSpawnedGrid && GetGrid() != null)
            {
                return false;
            }

            if (HasSpawnedGrid)
            {
                if (GetGrid() == null)
                {
                    return false;
                }
            }

            var distance = Vector3.Distance(PlayersCurrentPosition, DeliverLocation);
            if (distance > 5000)
            {
                return false;
            }
            var faction = MySession.Static.Factions.TryGetFactionById(this.FactionId);
            if (faction == null)
            {
                Core.Log.Info($"{this.FactionId} faction not found");
                return false;
            }

            var prefab = PrefabHelper.Repairs.GetRandomPrefab();
            Core.Log.Info($"{prefab}");


            var spawnPos = MyAPIGateway.Entities.FindFreePlace(this.DeliverLocation, 2000);
            if (!spawnPos.HasValue)
            {
                Core.Log.Info(
                    $"Unable to find a free place for prefab spawning for {this.Name} {this.AssignedPlayerSteamId}");
                return false;
            }

            var resultList = new List<MyCubeGrid>();
            Stack<Action> Callbacks = new Stack<Action>();
            Callbacks.Push(() =>
            {
                if (!resultList.Any())
                {
                    Core.Log.Info(
                        $"Could not load grid for prefab spawning {this.Name} {this.AssignedPlayerSteamId}");
                }
                else
                {
                    var main = resultList.OrderByDescending(x => x.BlocksCount).FirstOrDefault();
                    Grid = main;
                    GridEntityId = main.EntityId;
                    HasSpawnedGrid = true;
                    this.DeliverLocation = main.PositionComp.GetPosition();
                    this.DeleteDeliveryGPS();
                    this.SendDeliveryGPS();
                    var data = Core.PlayerStorage.GetData(this.AssignedPlayerSteamId);
                    Task.Run(async () =>
                    {
                        CrunchEconV3.Core.PlayerStorage.Save(data);
                    });
                }
            });

            var shouldOwnThis = MySession.Static.Factions.GetNpcFactions().Where(x => x.Stations.Any()).ToList()
            .GetRandomItemFromList();

            MyPrefabManager.Static.SpawnPrefab(resultList, prefab, spawnPos.Value,
                   Vector3.Forward, Vector3.Up, ownerId: shouldOwnThis.FounderId, callbacks: Callbacks);

            return false;
        }

        public override bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            try
            {
                if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                    return false;

                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
                }
                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);

                if (this.DeleteGridOnComplete && GetGrid() != null)
                {
        
                    GetGrid().Close();
                }

                CrunchEconV3.Core.SendMessage("Contracts", $"{this.Name} completed, you have been paid.", Color.Green, this.AssignedPlayerSteamId);
                return true;
            }
            catch (Exception e)
            {
                Core.Log.Error($"Repair try complete error {e}");
                return true;
            }
        }

        private MyCubeGrid GetGrid()
        {
            if (Grid != null)
            {
                return Grid;
            }
            var found = MyAPIGateway.Entities.GetEntityById(GridEntityId);
            if (found == null) return null;
            Grid = (MyCubeGrid)found;
            return Grid;
        }
        private void MarkClose(MyEntity obj)
        {
            Grid.OnBlockRemoved -= BlockRemoved;
            Grid.OnClosing -= MarkClose;
        }
        private void BlockRemoved(MySlimBlock obj)
        {
            if (obj.FatBlock is IMyDoor) return;
            this.brokeBlocks = true;
            FailContract();
        }

        public override void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Repair Grid Found At");
            sb.AppendLine("Repair Location.");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"--> REPAIR HERE <--";
            gpsRef.GPSColor = Color.Orange;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = new TimeSpan?();
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }
    }
}
