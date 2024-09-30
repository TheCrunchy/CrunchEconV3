using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;

namespace CrunchEconContractModels.Prozon
{
    [PatchShim]
    public static class ReputationLogging
    {
        public static Logger log = LogManager.GetLogger("Reputation");

        public static void ApplyLogging()
        {

            var rules = LogManager.Configuration.LoggingRules;

            for (int i = rules.Count - 1; i >= 0; i--)
            {

                var rule = rules[i];

                if (rule.LoggerNamePattern == "Reputation")
                    rules.RemoveAt(i);
            }

            var logTarget = new FileTarget
            {
                FileName = "Logs/Reputation-" + DateTime.Now.Day + "-" + DateTime.Now.Month + "-" + DateTime.Now.Year +
                           ".txt",
                Layout = "${var:logStamp} ${var:logContent}"
            };

            var logRule = new LoggingRule("Reputation", LogLevel.Debug, logTarget)
            {
                Final = true
            };

            rules.Insert(0, logRule);

            LogManager.Configuration.Reload();
        }
        //internal static readonly MethodInfo update =
        //typeof(MyFactionCollection).GetMethod("DamageFactionPlayerReputation", BindingFlags.Instance | BindingFlags.Public) ??
        //throw new Exception("Failed to find patch method 1A");
        //internal static readonly MethodInfo updatePatch =
        //        typeof(ReputationLogging).GetMethod(nameof(DamageFactionPlayerReputation), BindingFlags.Static | BindingFlags.Public) ??
        //        throw new Exception("Failed to find patch method 1");
        internal static readonly MethodInfo update2 =
       typeof(MyFactionCollection).GetMethod("AddFactionPlayerReputation", BindingFlags.Instance | BindingFlags.Public) ??
       throw new Exception("Failed to find patch method 2A");
        internal static readonly MethodInfo updatePatch2 =
                typeof(ReputationLogging).GetMethod(nameof(Log1), BindingFlags.Static | BindingFlags.Public) ??
                throw new Exception("Failed to find patch method 2 ");

        internal static readonly MethodInfo AddFactionRepSuccess =
typeof(MyFactionCollection).GetMethod("AddFactionPlayerReputationSuccess", BindingFlags.Static | BindingFlags.NonPublic) ??
throw new Exception("Failed to find patch method 3A");
        internal static readonly MethodInfo RepSuccessPatch =
                typeof(ReputationLogging).GetMethod(nameof(Log3), BindingFlags.Static | BindingFlags.Public) ??
                throw new Exception("Failed to find patch method 3");


        internal static readonly MethodInfo update3 =
typeof(MyFactionCollection).GetMethod("ChangeReputationWithPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
throw new Exception("Failed to find patch method 4A");
        internal static readonly MethodInfo updatePatch3 =
                typeof(ReputationLogging).GetMethod(nameof(Log2), BindingFlags.Static | BindingFlags.Public) ??
                throw new Exception("Failed to find patch method 4");

        public static void Patch(PatchContext ctx)
        {

            ApplyLogging();
          //  ctx.GetPattern(update).Prefixes.Add(updatePatch);
            ctx.GetPattern(update2).Prefixes.Add(updatePatch2);
            ctx.GetPattern(update3).Prefixes.Add(updatePatch3);
            ctx.GetPattern(AddFactionRepSuccess).Prefixes.Add(RepSuccessPatch);
        }

        public static void Log1(long playerIdentityId,
            long factionId,
            int delta,
            bool propagate = true,
            bool adminChange = false)
        {

            IMyFaction fac = MySession.Static.Factions.TryGetFactionById(factionId);
            if (delta != 0 && fac != null)
            {
                log.Info("Reputation logging - AddFactionPlayerRep -- Player: " + playerIdentityId + " faction:" +
                         factionId + " tag:" + fac.Tag + " amount:" + delta);
            }

        }

        public static void Log2(long fromPlayerId, long toFactionId, int reputation)
        {
            IMyFaction fac = MySession.Static.Factions.TryGetFactionById(toFactionId);
            if (fac != null)
            {
                log.Info("Reputation logging - ChangeReputationWithPlayer -- Player: " + fromPlayerId + " faction:" +
                         toFactionId + " tag:" + fac.Tag + " amount:" + reputation);
            }
        }

        public static void Log3(long playerId, List<MyFactionCollection.MyReputationChangeWrapper> changes)
        {
            foreach (MyFactionCollection.MyReputationChangeWrapper change in changes)
            {
                IMyFaction fac = MySession.Static.Factions.TryGetFactionById(change.FactionId);
                if (fac != null)
                {
                    log.Info("Reputation logging -- Player: " + playerId + " faction " + change.FactionId + " tag:" +
                             fac.Tag + " amount:" + change.Change + " total:" + change.RepTotal);
                }
            }
        }
    }
}