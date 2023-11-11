using System;
using CrunchEconV3.Interfaces;
using VRageMath;

namespace CrunchEconV3.Models.Contracts
{
    public class CrunchMiningContract : ICrunchContract
    {
        public CrunchContractTypes ContractType { get; set; }
        public long ContractId { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public long AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public Vector3 DeliverLocation { get; set; }

        public String OreSubTypeName { get; set; }
        public int MinedOreAmount { get; set; }
        public int AmountToMine { get; set; }

        public bool CanAutoComplete { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }

        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
        }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            if (MinedOreAmount >= AmountToMine)
            {

            }
            return false;
        }
    }
}
