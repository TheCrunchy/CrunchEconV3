using System;
using System.Collections.Generic;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRageMath;

namespace CrunchEconContractModels.Contracts.Quests
{
    public class QuestTextContractImplementation : ContractAbstract
    {
        public int CurrentQuestStage { get; set; }
        public string QuestName { get; set; }

        public Guid QuestId = Guid.NewGuid();

        public bool RequireCompletedQuest { get; set; }
        public string RequiredQuestName { get; set; }

        public bool CanRepeat { get; set; }

        //store anything quest data related in here for stages, eg if kill 3 things, store it here as a json string
        public Dictionary<string, string> JsonStoredData = new Dictionary<string, string>();
        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"{Description}";
            return BuildUnassignedContract(contractDescription);
        }

        public override void Start()
        {
            StoreIds();
        }

        private void StoreIds()
        {
            JsonStoredData["SteamId"] = this.AssignedPlayerSteamId.ToString();
            JsonStoredData["IdentityId"] = this.AssignedPlayerIdentityId.ToString();
        }

        public virtual Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId,
            MyContractBlock __instance)
        {
            if (this.RequireCompletedQuest)
            {
                var hasCompleted =
                    QuestHandler.HasPlayerCompletedQuest(playerData.PlayerSteamId, this.RequiredQuestName);
                if (!hasCompleted)
                {
                    return new Tuple<bool, MyContractResults>(false, MyContractResults.Fail_CannotAccess);
                }
            }

            if (!CanRepeat)
            {
                var hasCompleted =
                    QuestHandler.HasPlayerCompletedQuest(playerData.PlayerSteamId, this.QuestName);
                if (hasCompleted)
                {
                    return new Tuple<bool, MyContractResults>(false, MyContractResults.Fail_CannotAccess);
                }
            }

            if (this.ReputationRequired != 0)
            {
                var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                if (faction != null)
                {
                    var reputation =
                        MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(identityId, faction.FactionId);
                    if (this.ReputationRequired > 0)
                    {
                        if (reputation.Item2 < this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                    else
                    {
                        if (reputation.Item2 > this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                }
            }
            if (this.CollateralToTake > 0)
            {
                if (EconUtils.getBalance(identityId) < this.CollateralToTake)
                {
                    return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientFunds);
                }
            }

            var current = playerData.GetContractsForType(this.ContractType);
            if (IsAboveContractsOfThisTypeLimit(current))
            {
                return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
            }

            if (this.CollateralToTake > 0)
            {
                EconUtils.takeMoney(identityId, this.CollateralToTake);
            }
            this.AssignedPlayerIdentityId = identityId;
            this.AssignedPlayerSteamId = playerData.PlayerSteamId;

            return new Tuple<bool, MyContractResults>(true, MyContractResults.Success);
        }

        public override bool Update100(Vector3 PlayersCurrentPosition)
        {

            if (!JsonStoredData.ContainsKey("SteamId"))
            {
                StoreIds();
                MyVisualScriptLogicProvider.SetQuestlog(true, QuestName, (long)this.AssignedPlayerIdentityId);
                MyVisualScriptLogicProvider.SetQuestlogTitle(QuestName, (long)this.AssignedPlayerIdentityId);
                MyVisualScriptLogicProvider.SetQuestlogVisible(true, (long)this.AssignedPlayerIdentityId);
            }
            //get the quest
            if (!QuestHandler.Quests.TryGetValue(QuestName, out var currentQuest))
                return false;

            if (CurrentQuestStage == 0)
            {
                currentQuest.QuestStages[1].StartStage(PlayersCurrentPosition, JsonStoredData, QuestId);
                CurrentQuestStage += 1;
                return false;
            }

            if (!currentQuest.QuestStages.TryGetValue(CurrentQuestStage, out var stage))
                return false;

            var stageCompleteResult = stage.TryCompleteStage(PlayersCurrentPosition, JsonStoredData, QuestId);
            if (!stageCompleteResult)
                return false;

            if (currentQuest.CanAdvanceStage(this.CurrentQuestStage))
            {

                CurrentQuestStage += 1;
                currentQuest.QuestStages.TryGetValue(CurrentQuestStage, out var nextStage);
                nextStage?.StartStage(PlayersCurrentPosition, JsonStoredData, QuestId);
            }
            else
            {
                this.ReadyToDeliver = true;
                //quest is completed, no longer track it
                QuestHandler.SaveQuestCompleted(this.AssignedPlayerSteamId, this.QuestName);
                return true;
            }

            return false;
        }

    }
}
