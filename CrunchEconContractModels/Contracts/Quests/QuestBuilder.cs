using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage;
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
                Context.Respond("Quest is currently being built. Run !qb save or !qb discard to stop editing.");
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
                newQuest.QuestStages[0] = new DelayStage()
                {
                    SecondsToDelay = 5
                };
                QuestsBeingBuilt[Context.Player.SteamUserId] = newQuest;
            }
       

            Context.Respond("Quest editing started.");
        }


        [Command("test", "Give yourself a quest for testing")]
        [Permission(MyPromoteLevel.Admin)]
        public void Test(string questName)
        {
            if (Context.Player == null)
            {
                Context.Respond("Player only command.");
                return;
            }

            if (QuestHandler.Quests.TryGetValue(questName, out var existing))
            {
                var contract = new CrunchQuestContractImplementation()
                {
                    QuestName = questName,
                    AssignedPlayerIdentityId = Context.Player.IdentityId,
                    AssignedPlayerSteamId = Context.Player.SteamUserId,
                };
                var playerdata = Core.PlayerStorage.GetData(Context.Player.SteamUserId);
                var id = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.CONTRACT,
                    MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM) + Core.random.Next(1, 200);
                contract.ContractId = id;
                playerdata.PlayersContracts.Add(id, contract);
                Context.Respond("Quest Given.");
            }
            else
            {
                Context.Respond("Quest not found.");
            }
        }

        [Command("removestage", "Remove a stage")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetStage(int stage)
        {

            if (Context.Player == null)
            {
                Context.Respond("Player only command.");
                return;
            }

            if (QuestsBeingBuilt.TryGetValue(Context.Player.SteamUserId, out var quest))
            {
                quest.QuestStages.Remove(stage);
                Context.Respond("Stage removed if it existed.");
            }
            else
            {
                Context.Respond("Not currently editing a quest. !qb start");
            }

        }

        [Command("save", "Save currently edited quest.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Save()
        {

            if (Context.Player == null)
            {
                Context.Respond("Player only command.");
                return;
            }

            if (QuestsBeingBuilt.TryGetValue(Context.Player.SteamUserId, out var quest))
            {
                QuestHandler.Quests[quest.QuestName] = quest;
                QuestHandler.SaveQuest(quest);
                Context.Respond("Quest saved. To stop editing use !qb discard");
            }
            else
            {
                Context.Respond("Not currently editing a quest. !qb start");
            }

        }

        [Command("discard", "Stop editing a quest.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Discard()
        {

            if (Context.Player == null)
            {
                Context.Respond("Player only command.");
                return;
            }

            if (QuestsBeingBuilt.TryGetValue(Context.Player.SteamUserId, out var quest))
            {
                QuestsBeingBuilt.Remove(Context.Player.SteamUserId);
                Context.Respond("Quest discarded, changes will not be saved.");
            }
            else
            {
                Context.Respond("Not currently editing a quest. !qb start");
            }

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
                    where t.IsSubclassOf(typeof(QuestStage))
                    select t;

                var enumerable = existingStages.ToList();
                if (!enumerable.Any())
                {
                    Context.Respond("No quest stages found.");
                    return;
                }

                var myStage = enumerable.FirstOrDefault(x => String.Equals(x.Name, stageType, StringComparison.CurrentCultureIgnoreCase)) ?? null;
                if (myStage == null)
                {
                    Context.Respond("Stage type not found, available are: ");
                    foreach (var type in enumerable)
                    {
                        Context.Respond(type.Name);
                    }
                    return;
                }

                QuestStage instance = (QuestStage)Activator.CreateInstance(myStage);
                instance.SetPosition(Context.Player.Character.PositionComp.GetPosition());
                quest.QuestStages[stage] = instance;
                Context.Respond($"Stage added to position {stage}");
            }
            else
            {
                Context.Respond("Not currently editing a quest. !qb start");
            }
        
        }
    }
}
