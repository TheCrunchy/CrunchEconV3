using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Torch;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class CrunchWaveDefenceCombatContractImplementation : ICrunchContract
    {
        public long ContractId { get; set; }
        public string ContractType { get; set; }
        public MyObjectBuilder_Contract BuildUnassignedContract(string descriptionOverride = "")
        {
            throw new NotImplementedException();
        }

        public MyObjectBuilder_Contract BuildAssignedContract()
        {
            throw new NotImplementedException();
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }


        private DateTime NextSpawn = DateTime.Now;
        public bool Update100(Vector3 PlayersCurrentPosition)
        {

            if (DateTime.Now >= NextSpawn)
            {
                NextSpawn = DateTime.Now.AddSeconds(20);
                var player = MySession.Static.Players.TryGetPlayerBySteamId(this.AssignedPlayerSteamId);
                Vector3 Position = player.Character.PositionComp.GetPosition();
                Random random = new Random();
                var faction = MySession.Static.Factions.TryGetFactionByTag("SPRT");

                Position.Add(new Vector3(random.Next(0, 5000), random.Next(0, 5000), random.Next(0, 5000)));
                if (GridManager.LoadGrid(Core.path + "//Grids//pirate.sbc", Position, false, (ulong)faction.Members.FirstOrDefault().Value.PlayerId, "Spawned grid", false))
                {
                    Core.Log.Info("Loaded grid");
                }

            }

            return false;
        }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            throw new NotImplementedException();
        }

        public void FailContract()
        {
            throw new NotImplementedException();
        }

        public void SendDeliveryGPS()
        {
            throw new NotImplementedException();
        }

        public void DeleteDeliveryGPS()
        {
            throw new NotImplementedException();
        }

        public int ReputationRequired { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public ulong AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public long DistanceReward { get; set; }
        public Vector3 DeliverLocation { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
        public long DeliveryFactionId { get; set; }
    }

    public class GridSpawnModel
    {
        public string FilePathToGrid = $"{Core.path}//Grids//pirate.sbc";
        public double ChanceToSpawn = 0.5;
        public string FacTagToOwnThisGrid = "SPRT";
    }

    public class BlockDestruction
    {
        public string BlockPairName = "LargeReactor";
        public long Payment = 50000;
    }

}
