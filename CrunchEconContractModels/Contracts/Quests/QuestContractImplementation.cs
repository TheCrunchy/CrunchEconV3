using System;
using System.Collections.Generic;
using CrunchEconV3.Abstracts;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRageMath;

namespace CrunchEconContractModels.Contracts.Quests
{
    public class QuestTextContractImplementation : ContractAbstract
    {
        public int CurrentQuestStage { get; set; }
        public string QuestName { get; set; }

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
            if (QuestHandler.Quests.TryGetValue(QuestName, out var currentQuest))
            {
                if (currentQuest.QuestStages.TryGetValue(CurrentQuestStage, out var stage))
                {
                    var stageCompleteResult = stage.TryCompleteStage(PlayersCurrentPosition, JsonStoredData);
                    if (stageCompleteResult)
                    {
                        if (currentQuest.CanAdvanceStage(this.CurrentQuestStage))
                        {
                            CurrentQuestStage += 1;
                        }
                        else
                        {
                            //quest is completed, no longer track it
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
