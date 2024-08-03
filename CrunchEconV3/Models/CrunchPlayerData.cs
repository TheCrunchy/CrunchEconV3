using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace CrunchEconV3.Models
{
    public class CrunchPlayerData
    {
        public ulong PlayerSteamId { get; set; }

        public Dictionary<long, ICrunchContract> PlayersContracts = new Dictionary<long, ICrunchContract>();

        public Dictionary<string, string> RandomJsonStuff = new Dictionary<string, string>();

        public List<ICrunchContract> GetContractsForType(string type)
        {
            return PlayersContracts.Where(x => x.Value.ContractType.Equals(type)).Select(x => x.Value).ToList();
        }

        public Tuple<bool, MyContractResults> AddContract(ICrunchContract contract)
        {
            PlayersContracts.Add(contract.ContractId, contract);
            return Tuple.Create(true, MyContractResults.Success);
        }

        public void RemoveContract(ICrunchContract contract)
        {
            PlayersContracts.Remove(contract.ContractId);
        }

        [JsonIgnore]
        public Action<bool, ICrunchContract> ContractFinished { get; set; }
    }
}
