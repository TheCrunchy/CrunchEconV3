using System;
using System.Collections.Generic;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRageMath;

namespace CrunchEconContractModels.Contracts.Quests
{
    public class QuestTextContractImplementation : ContractAbstract
    {
        public int CurrentQuestStage { get; set; }
        public string QuestName { get; set; }

        public Guid QuestId = Guid.NewGuid();

        //store anything quest data related in here for stages, eg if kill 3 things, store it here as a json string
        public Dictionary<string, string> JsonStoredData = new Dictionary<string, string>();
        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            throw new NotImplementedException();
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

        public override bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (!JsonStoredData.ContainsKey("SteamId"))
            {
                StoreIds();
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
                Core.Log.Info("Quest should be completing.");
                //quest is completed, no longer track it
                return true;
            }

            return false;
        }

    }
}
