using System;
using System.Collections.Generic;
using System.Linq;
using CrunchEconV3;
using Sandbox.Game;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Contracts.Hud
{
    public class ContractHud
    {
        private static int ticks;

        private static HashSet<ulong> GivenHud = new HashSet<ulong>();
        public static void UpdateExample()
        {
            ticks++;
            if (ticks % 3000 == 0)
            {
                foreach (var player in MySession.Static.Players.GetOnlinePlayers())
                {
                    var data = Core.PlayerStorage.GetData(player.Id.SteamId);
                    if (data != null)
                    {
                        var hasContract = data.PlayersContracts.Any();
                        if (!hasContract)
                        {
                            MyVisualScriptLogicProvider.SetQuestlogVisible(false, player.Identity.IdentityId);
                            GivenHud.Remove(player.Id.SteamId);
                        }
                        else
                        {
                            if (!GivenHud.Contains(player.Id.SteamId))
                            {
                                MyVisualScriptLogicProvider.SetQuestlogVisible(true, player.Identity.IdentityId);
                                MyVisualScriptLogicProvider.SetQuestlogTitle("Contracts", player.Identity.IdentityId);
                                GivenHud.Add(player.Id.SteamId);
                            }
                         
                            MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId: player.Identity.IdentityId);

                            foreach (var item in data.PlayersContracts.Values)
                            {
                                var timeLeft = item.ExpireAt.Subtract(DateTime.Now);
                                if (item.ReadyToDeliver)
                                {
                                    MyVisualScriptLogicProvider.AddQuestlogDetail($"{item.Name}, {timeLeft.Hours} Hours, {timeLeft.Minutes} Minutes, Can be turned in.", playerId: player.Identity.IdentityId, useTyping: false, completePrevious: false);

                                }
                                else
                                {
                                    MyVisualScriptLogicProvider.AddQuestlogDetail($"{item.Name}, {timeLeft.Hours} Hours, {timeLeft.Minutes} Minutes", playerId: player.Identity.IdentityId, useTyping: false, completePrevious: false);

                                }

                            }
                        }
                    }
                }
            }

        }

        public static void Patch(PatchContext ctx)
        {
            Core.UpdateCycle += UpdateExample;
        }
    }
}
