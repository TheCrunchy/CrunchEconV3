using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models.Contracts;
using CrunchEconV3.Utils;
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

        private List<ICrunchContract> GetContractsForType(CrunchContractTypes type)
        {
            return PlayersContracts.Where(x => x.Value.ContractType == type).Select(x => x.Value).ToList();
        }

        public Tuple<bool, MyContractResults> AddContract(ICrunchContract contract, string factionTag, long playerIdentity, MyContractBlock __instance)
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
            switch (contract)
            {
                case CrunchGasContract gas:
                    {
                        if (current.Count >= 1)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
                        }
                        var test = __instance.CubeGrid.GetGridGroup(GridLinkTypeEnum.Physical);
                        var grids = new List<IMyCubeGrid>();
                        var tanks = new List<IMyGasTank>();

                        test.GetGrids(grids);
                        foreach (var gridInGroup in grids)
                        {
                            tanks.AddRange(gridInGroup.GetFatBlocks<IMyGasTank>());
                        }

                        var playerTanks = TankHelper.MakeTankGroup(tanks, playerIdentity, __instance.OwnerId, gas.GasName);
                        if (playerTanks.GasInTanks < gas.GasAmount)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
                        }
                    }
                    break;
                case CrunchMiningContract:
                    {
                        if (current.Count >= 3)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
                        }
                    }
                    break;
                case CrunchPeopleHaulingContract people:
                    {
                        if (current.Count >= 1)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
                        }

                        var test = __instance.CubeGrid.GetGridGroup(GridLinkTypeEnum.Physical);
                        var grids = new List<IMyCubeGrid>();
                        test.GetGrids(grids);
                        var capacity = 0;
                        foreach (var gridInGroup in grids)
                        {

                            var owner = FacUtils.IsOwnerOrFactionOwned(gridInGroup as MyCubeGrid, playerIdentity, true);
                            if (owner)
                            {
                                capacity += TransportUtils.GetPassengerCount(gridInGroup as MyCubeGrid, people);
                            }
                        }

                        if (capacity <= 0)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
                        }

                        var max = capacity;
                        var calculated = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(playerIdentity,
                                contract.FactionId);
                        var maximumPossiblePassengers =
                            calculated.Item2 * people.ReputationMultiplierForMaximumPassengers;
                        if (maximumPossiblePassengers < max)
                        {
                            max = (int)maximumPossiblePassengers;
                        }

                        if (max < 1)
                        {
                            max = 1;
                        }
                        people.PassengerCount = max;
                        people.RewardMoney = contract.RewardMoney * people.PassengerCount;
                        people.RewardMoney += contract.DistanceReward;
                        contract = people;
                        contract.ReadyToDeliver = true;

                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{contract.GetType()} contract type not added to switch");
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
