using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;

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

        public Tuple<bool, string> AddContract(ICrunchContract contract)
        {
            var current = GetContractsForType(contract.ContractType);
            switch (contract.ContractType)
            {
                case CrunchContractTypes.Mining:
                    if (current.Count >= 3)
                    {
                        return Tuple.Create(false, $"You can only have 3 active {contract.ContractType} contracts.");
                    }
                    break;
                case CrunchContractTypes.PeopleTransport:
                    if (current.Count >= 1)
                    {
                        return Tuple.Create(false, $"You can only have 1 active {contract.ContractType} contracts.");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{contract.ContractType} contract type not added to switch");
            }
            PlayersContracts.Add(contract.ContractId, contract);
            return Tuple.Create(false, $"Contract added!");
        }

        public void RemoveContract(ICrunchContract contract)
        {
            PlayersContracts.Remove(contract.ContractId);
        }
    }
}
