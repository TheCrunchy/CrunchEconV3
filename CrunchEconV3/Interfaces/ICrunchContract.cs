using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using VRageMath;

namespace CrunchEconV3.Interfaces
{
    public interface ICrunchContract
    {
        public CrunchContractTypes ContractType { get; set; }
        public void Start();
        public int ReputationRequired { get; set; }
        public long ContractId { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public ulong AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public long DistanceReward { get; set; }
        public Vector3 DeliverLocation { get; set; }
        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition);
        public void FailContract();
        public bool CanAutoComplete { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }

        public void SendDeliveryGPS();
        public void DeleteDeliveryGPS();
        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }

    }
}
