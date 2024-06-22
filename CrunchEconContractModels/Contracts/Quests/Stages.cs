using System;
using System.Collections.Generic;
using System.Linq;
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
using VRageMath;

namespace CrunchEconContractModels.Contracts.Quests
{
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
                QuestName = this.QuestName,
                QuestStages = new Dictionary<int, QuestStage>()
            };

            // Deep copy of QuestStages dictionary
            var json = JsonConvert.SerializeObject(this.QuestStages, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Binder = new MySerializationBinder(),
                Formatting = Newtonsoft.Json.Formatting.Indented
            });

            var asItem = JsonConvert.DeserializeObject<Dictionary<int, QuestStage>>(json, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Binder = new MySerializationBinder(),
                Formatting = Newtonsoft.Json.Formatting.Indented
            });

            clonedQuest.QuestStages = asItem;

            return clonedQuest;
        }
    }

    public abstract class QuestStage
    {
        public virtual bool TryCompleteStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData, Guid QuestId)
        {
            return false;
        }

        public virtual void SetPosition(Vector3 EditorsCurrentPosition)
        {
            //implement in the inheriting stage
        }

        public virtual void StartStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData, Guid QuestId)
        {

        }
    }

    public class TextPositionStage : QuestStage
    {
        public Vector3 Position { get; set; }
        public int DistanceToPosition { get; set; } = 5;
        public string GpsName { get; set; } = "Quest Location";
        public string TextToSend { get; set; } = "Example Quest text";
        public bool GiveDataPad { get; set; } = true;
        public string DatapadName { get; set; } = "Example Quest Datapad";
        public string MessageSenderName { get; set; } = "Quests";
        public string DatapadAddedMessage { get; set; } = "Datapad Added to Inventory";

        //Set the position from the editor
        public override void SetPosition(Vector3 EditorsCurrentPosition)
        {
            Position = EditorsCurrentPosition;
        }

        public override bool TryCompleteStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData, Guid QuestId)
        {
            if (Vector3.Distance(Position, PlayersCurrentPosition) > DistanceToPosition)
            {
                return false;
            }

            if (MySession.Static.Players.TryGetPlayerBySteamId(ulong.Parse(jsonStoredData["SteamId"]), out var player))
            {
                var identityId = long.Parse(jsonStoredData["IdentityId"]);
                MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId: identityId);
                MyVisualScriptLogicProvider.AddQuestlogDetail(TextToSend, playerId: identityId, useTyping: false, completePrevious: false);
                if (GiveDataPad)
                {
                    Core.SendMessage(MessageSenderName, $"{DatapadAddedMessage} {DatapadName}", Color.Aqua, player.Id.SteamId);
                    var datapadBuilder = new MyObjectBuilder_Datapad() { SubtypeName = "Datapad" };
                    datapadBuilder.Data = TextToSend;
                    datapadBuilder.Name = DatapadName;
                    player.Character.GetInventory().AddItems(1, datapadBuilder);
                }

                MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
                try
                {
                    gpscol.SendDeleteGpsRequest(player.Identity.IdentityId, int.Parse(jsonStoredData[$"{QuestId}-Position"]));
                }
                catch (Exception)
                {
                }
                return true;
            }
            //player is close enough to position, send them some text 

            return false;
        }

        public override void StartStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData, Guid QuestId)
        {
            var identityId = long.Parse(jsonStoredData["IdentityId"]);
            var steamId = long.Parse(jsonStoredData["SteamId"]);

            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = Position;
            gpsRef.Name = $"{GpsName}";
            gpsRef.GPSColor = Color.OrangeRed;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = new TimeSpan?();
            gpsRef.UpdateHash();
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(identityId, ref gpsRef);

            jsonStoredData[$"{QuestId}-Position"] = gpsRef.Hash.ToString();
            //      MyVisualScriptLogicProvider.AddGPSObjective("Gps Objective", "Gps Description", Position, Color.OrangeRed, playerId:identityId);
            MyVisualScriptLogicProvider.AddQuestlogDetail($"Travel to {GpsName}", playerId: identityId);

        }

    }

    public class SpaceCreditRewardStage : QuestStage
    {
        public long MoneyReward { get; set; }
        public override bool TryCompleteStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData, Guid QuestId)
        {
            var identityId = long.Parse(jsonStoredData["IdentityId"]);
            EconUtils.addMoney(identityId, MoneyReward);
            return true;
        }
    }

    public class DelayStage : QuestStage
    {
        public int SecondsToDelay = 10;
        public override bool TryCompleteStage(Vector3 PlayersCurrentPosition, Dictionary<string, string> jsonStoredData, Guid QuestId)
        {
            if (jsonStoredData.TryGetValue("SecondsToDelay", out var value))
            {
                var parsed = DateTime.Parse(value);
                if (DateTime.Now > parsed)
                {
               //     Core.Log.Info("Delay stage returning true");
                    jsonStoredData.Remove("SecondsToDelay");
                    return true;
                }
            }
            else
            {
                jsonStoredData.Add("SecondsToDelay", DateTime.Now.AddSeconds(SecondsToDelay).ToString());
            }
          //  Core.Log.Info("Delay stage returning false");
            return false;
        }
    }
}
