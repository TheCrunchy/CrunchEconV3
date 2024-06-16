using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace CrunchEconContractModels.Contracts.Quests
{
    [Category("qb")]
    public class QuestBuilder : CommandModule
    {

        public static Dictionary<ulong, Quest> QuestsBeingBuilt = new Dictionary<ulong, Quest>();

        [Command("start", "Start building a quest")]
        [Permission(MyPromoteLevel.Admin)]
        public void Start(string questName)
        {
            if (Context.Player == null)
            {
                Context.Respond("Player only command.");
                return;
            }
            if (QuestsBeingBuilt.TryGetValue(Context.Player.SteamUserId, out var quest))
            {
                Context.Respond("Quest is currently being built. Run !qb finish or !qb discard to stop editing.");
                return;
            }

            if (QuestHandler.Quests.TryGetValue(questName, out var existing))
            {
                QuestsBeingBuilt[Context.Player.SteamUserId] = (Quest)existing.Clone();
            }
            else
            {
                Quest newQuest = new Quest()
                {
                    QuestName = questName,
                    QuestStages = new Dictionary<int, QuestStage>()
                };

                QuestsBeingBuilt[Context.Player.SteamUserId] = newQuest;
            }
       

            Context.Respond("Quest editing started.");
        }

        [Command("setstage", "Set the stage of currently edited quest.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetStage(int stage, string stageType)
        {

            if (Context.Player == null)
            {
                Context.Respond("Player only command.");
                return;
            }

            if (QuestsBeingBuilt.TryGetValue(Context.Player.SteamUserId, out var quest))
            {
                var existingStages = from t in Core.myAssemblies.SelectMany(x => x.GetTypes())
                    where t.IsClass && t.IsSubclassOf(typeof(QuestStage))
                    select t;

                if (!existingStages.Any())
                {
                    Context.Respond("No quest stages found.");
                    return;
                }

                var myStage = existingStages.FirstOrDefault(x => x.Name.ToLower() == stageType.ToLower()) ?? null;
                if (myStage == null)
                {
                    Context.Respond("Stage type not found, available are: ");
                    foreach (var type in existingStages)
                    {
                        Context.Respond(type.Name);
                    }
                    return;
                }

                var instance = Activator.CreateInstance(myStage);
                quest.QuestStages[stage] = (QuestStage)instance;
            }
            else
            {
                Context.Respond("Not currently editing a quest. !qb start");
            }
        
        }
    }
}
