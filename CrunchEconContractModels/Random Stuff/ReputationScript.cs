using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class ReputationScript
    {
        public static Dictionary<string, int> ReputationKeys = new Dictionary<string, int>();
        public static void Patch(PatchContext ctx)
        {
            MySession.Static.Factions.FactionCreated += FactionsOnFactionCreated;
            ReputationKeys.Add("HOS", -1500);
            ReputationKeys.Add("FRE", 1500);
            ReputationKeys.Add("NEU", 0);

            foreach (var faction in MySession.Static.Factions)
            {
                if (faction.Value.IsEveryoneNpc())
                {
                    continue;
                }
                ProcessFaction(faction.Value);
            }
        }

        private static void FactionsOnFactionCreated(long Obj)
        {
            var faction = MySession.Static.Factions.TryGetFactionById(Obj);
            if (faction != null)
            {
                ProcessFaction(faction);
            }
        }

        private static void ProcessFaction(IMyFaction faction)
        {
            foreach (var item in ReputationKeys)
            {
                var found = MySession.Static.Factions.TryGetFactionByTag(item.Key);
                if (found != null)
                {
                    MySession.Static.Factions.SetReputationBetweenFactions(faction.FactionId, found.FactionId, item.Value);
                    foreach (var member in faction.Members.Values.Select(x => x.PlayerId).Distinct())
                    {
                        MySession.Static.Factions.SetReputationBetweenPlayerAndFaction(member, found.FactionId, item.Value, ReputationChangeReason.Admin);
                    }
                }
            }
        }
    }
}
