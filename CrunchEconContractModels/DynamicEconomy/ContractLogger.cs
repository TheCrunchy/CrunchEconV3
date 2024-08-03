using System;
using System.Collections.Generic;
using System.Linq;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using Sandbox.Game;
using Sandbox.Game.World;
using Torch.API;
using Torch.API.Managers;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.DynamicEconomy
{
    public static class ContractLogger
    {
        public static void Patch(PatchContext ctx)
        {
            Core.Session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined += LoadLogin;
            Core.UpdateCycle += UpdateExample;
        }
        private static int ticks;

        public static void UpdateExample()
        {
            if (ticks == 0)
            {
                Core.PlayerStorage.ContractFinished += Finished;
            }
            ticks++;
            if (ticks % 3000 == 0)
            {
                foreach (var player in MySession.Static.Players.GetOnlinePlayers())
                {
                    if (!SetupPlayers.TryGetValue(player.Id.SteamId, out var data))
                    {
                        Load(player.Id.SteamId);
                    }
                }
            }
        }

        public static Dictionary<ulong, FinishedContractsModel> SetupPlayers =
            new Dictionary<ulong, FinishedContractsModel>();

        public static void LoadLogin(IPlayer Player)
        {
     
            var playerId = Player.SteamId;
            Load(playerId);
        }

        public static void Load(ulong steamId)
        {
            Core.Log.Info($"Setting up player join invokes for {steamId}");
            var playerData = Core.PlayerStorage.GetData(steamId);

            //check if file exists and read it 
            SetupPlayers[steamId] = new FinishedContractsModel();
        }

        public static void Finished(bool successful, ICrunchContract Arg2)
        {
            var playerSteamId = Arg2.AssignedPlayerSteamId;
            Core.Log.Error($"{playerSteamId} finished {Arg2.Name}");
        }

        public class FinishedContractsModel
        {
            public Dictionary<string, int> FinishedTypes = new Dictionary<string, int>();
        }
    }
}
