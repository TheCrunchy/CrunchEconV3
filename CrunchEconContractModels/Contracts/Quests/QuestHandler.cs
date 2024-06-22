using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRageMath;

namespace CrunchEconContractModels.Contracts.Quests
{
    public static class QuestHandler
    {
        public static Dictionary<string, Quest> Quests = new Dictionary<string, Quest>();
        public static Dictionary<ulong, PlayerQuestData> QuestDatas = new Dictionary<ulong, PlayerQuestData>();
        private static FileUtils fileUtils = new FileUtils();

        private static int Ticks = 0;
        public static void Update100()
        {
         //   Ticks++;

            //if (Ticks % 600 == 0)
            //{
            //    foreach (var player in MySession.Static.Players.GetOnlinePlayers())
            //    {
            //        var data = Core.PlayerStorage.GetData(player.Id.SteamId);
            //        if (data != null)
            //        {
            //            var hasQuest = data.PlayersContracts.Any(x =>
            //                x.Value.ContractType == nameof(CrunchQuestContractImplementation));
            //            if (!hasQuest)
            //            {
            //                MyVisualScriptLogicProvider.SetQuestlogVisible(false, player.Identity.IdentityId);
            //            }
            //            else
            //            {
            //                var contract = data.PlayersContracts.FirstOrDefault(x =>
            //                    x.Value.ContractType == nameof(CrunchQuestContractImplementation));

            //                var quest = contract.Value as CrunchQuestContractImplementation;
            //                MyVisualScriptLogicProvider.SetQuestlogVisible(true, (long)contract.Value.AssignedPlayerIdentityId);
            //                MyVisualScriptLogicProvider.SetQuestlogTitle(quest.QuestName, (long)contract.Value.AssignedPlayerIdentityId);

            //                MyVisualScriptLogicProvider.SetQuestlog(true, quest.QuestName, (long)contract.Value.AssignedPlayerIdentityId);
            //            }
            //        }
            //    }
            //}
        }

        public static void Patch(PatchContext ctx)
        {
            Core.UpdateCycle += Update100;
            LoadQuests();
        }

        public static void LoadQuests()
        {
            var folder = $"{Core.path}\\Quests\\";
            Directory.CreateDirectory(folder);

            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Quest quest = fileUtils.ReadFromJsonFile<Quest>(file);
                    Quests[quest.QuestName] = quest;
                }
                catch (Exception e)
                {
                    Core.Log.Error($"Error reading quest file - {file}");
                }
            }

            folder = $"{Core.path}\\PlayerQuestsData\\";
            Directory.CreateDirectory(folder);

            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    PlayerQuestData data = fileUtils.ReadFromJsonFile<PlayerQuestData>(file);
                    QuestDatas[data.SteamId] = data;
                }
                catch (Exception e)
                {
                    Core.Log.Error($"Error reading quest file - {file}");
                }
            }
        }

        public static bool HasPlayerCompletedQuest(ulong steamid, string questName)
        {
            if (QuestHandler.QuestDatas.TryGetValue(steamid, out var quests))
            {
                return quests.CompletedQuestNames.Contains(questName);
            }
            return false;
        }

        public static void SaveQuestCompleted(ulong steamId, string questName)
        {
            var folder = $"{Core.path}\\PlayerQuestsData\\{steamId}.json";
            if (QuestDatas.TryGetValue(steamId, out var data))
            {
                data.CompletedQuestNames.Add(questName);
                fileUtils.WriteToJsonFile(folder, data);
            }
            else
            {
                data = new PlayerQuestData()
                {
                    SteamId = steamId,
                    CompletedQuestNames = new List<string>() { questName }
                };
                QuestDatas.Add(steamId, data);
                fileUtils.WriteToJsonFile(folder, data);
            }
        }

        public static void SaveQuest(Quest quest)
        {

            var path = $"{Core.path}\\Quests\\{quest.QuestName}.json";
            fileUtils.WriteToJsonFile(path, quest);
        }
    }

    public class PlayerQuestData
    {
        public ulong SteamId { get; set; }
        public List<string> CompletedQuestNames { get; set; }
    }

  
}
