using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using VRageMath;

namespace CrunchEconContractModels.Contracts.Quests
{
    public static class QuestHandler
    {
        public static Dictionary<string, Quest> Quests = new Dictionary<string, Quest>();

        public static void LoadQuests()
        {

        }
    }

    public class Quest : ICloneable
    {
        public string QuestName { get; set; }
        public Dictionary<int, QuestStage> QuestStages = new Dictionary<int, QuestStage>();

        public bool CanAdvanceStage(int currentStage)
        {
            return QuestStages.ContainsKey(currentStage + 1);
        }

        public object Clone()
        {
            Quest clonedQuest = new Quest
            {
                QuestName = this.QuestName
            };

            // Deep copy of QuestStages dictionary
            foreach (var kvp in this.QuestStages)
            {
                //This is a very lazy way of doing this since the Stage is an abstract 
                var json = JsonConvert.SerializeObject(kvp.Value, new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Binder = new MySerializationBinder(),
                    Formatting = Newtonsoft.Json.Formatting.Indented
                });

                var asItem = JsonConvert.DeserializeObject<QuestStage>(json, new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Binder = new MySerializationBinder(),
                    Formatting = Newtonsoft.Json.Formatting.Indented
                });

                clonedQuest.QuestStages[kvp.Key] = asItem;
            }

            return clonedQuest;
        }
    }

    public abstract class QuestStage
    {
        public virtual bool TryCompleteStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData)
        {
            return false;
        }

        public virtual void StartStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData)
        {

        }
    }

    public class TextPositionStage : QuestStage
    {
        public Vector3 Position { get; set; }
        public int DistanceToPosition { get; set; }
        public override bool TryCompleteStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData)
        {
            if (Vector3.Distance(Position, PlayersCurrentPosition) > DistanceToPosition)
            {
                return false;
            }

            //player is close enough to position, send them some text 

            return true;
        }

        public override void StartStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData)
        {
            //send them a gps of where to go 
        }
    }

    public class SpaceCreditRewardStage : QuestStage
    {
        public long MoneyReward { get; set; } 
        public override bool TryCompleteStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData)
        {
            var identityId = long.Parse(jsonStoredData["IdentityId"]);
            EconUtils.addMoney(identityId, MoneyReward);
            return true;
        }
    }
}
