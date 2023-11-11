using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;

namespace CrunchEconV3.Models
{
    public class CrunchPlayerData
    {
        public ulong PlayerSteamId { get; set; }

        public Dictionary<long, ICrunchContract> PlayersContracts = new Dictionary<long, ICrunchContract>();

        private List<ICrunchContract> GetContractsForType(CrunchContractTypes type)
        {
            return PlayersContracts.Where(x => x.Value.ContractType == type).Select(x => x.Value).ToList();
        }

        public Tuple<bool, MyContractResults> AddContract(ICrunchContract contract, string factionTag, long playerIdentity)
        {
            if (contract.ReputationRequired != 0)
            {
                var faction = MySession.Static.Factions.TryGetFactionByTag(factionTag);
                if (faction != null)
                {
                    var reputation =
                        MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(playerIdentity, faction.FactionId);
                    if (contract.ReputationRequired > 0)
                    {
                        if (reputation.Item2 < contract.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                    else
                    {
                        if (reputation.Item2 > contract.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                }
            }
            var current = GetContractsForType(contract.ContractType);
            switch (contract.ContractType)
            {
                case CrunchContractTypes.Mining:
                    if (current.Count >= 3)
                    {
                        return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
                    }
                    break;
                case CrunchContractTypes.PeopleTransport:
                    if (current.Count >= 1)
                    {
                        return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{contract.ContractType} contract type not added to switch");
            }
            PlayersContracts.Add(contract.ContractId, contract);
            return Tuple.Create(true, MyContractResults.Success);
        }

        public void RemoveContract(ICrunchContract contract)
        {
            PlayersContracts.Remove(contract.ContractId);
        }
    }
}
