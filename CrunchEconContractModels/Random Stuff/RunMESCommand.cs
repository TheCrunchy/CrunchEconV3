using System;
using System.Collections.Generic;
using CrunchEconV3;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    public class RunMESCommand : CommandModule
    {
        private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(3);
        private static readonly Dictionary<ulong, DateTime> Cooldowns = new Dictionary<ulong, DateTime>();

        [Command("runmes", "Run an MES command")]
        [Permission(MyPromoteLevel.Admin)]
        public void RunCommand(string command, long playerSteamId = 0L)
        {
            ulong steamId = (playerSteamId != 0L) ? (ulong)playerSteamId : Context.Player.SteamUserId;

            ExecuteMesCommand(command, steamId);
            Context.Respond("Command executed.");
        }

        [Command("gts", "Run an MES command")]
        [Permission(MyPromoteLevel.None)]
        public void RunCommand()
        {
            ulong steamId = Context.Player.SteamUserId;

            if (Cooldowns.TryGetValue(steamId, out var cooldownEndTime))
            {
                if (DateTime.UtcNow < cooldownEndTime)
                {
                    var remainingTime = cooldownEndTime - DateTime.UtcNow;
                    Context.Respond($"Command is on cooldown. Please wait {remainingTime.Seconds} seconds before using it again.");
                    return;
                }
            }

            Core.MesAPI.ChatCommand("/MES.GTS", Context.Player.Character.PositionComp.WorldMatrixRef, Context.Player.IdentityId, Context.Player.SteamUserId);
            Cooldowns[steamId] = DateTime.UtcNow.Add(CooldownPeriod);
            Context.Respond("Command executed.");
        }

        private void ExecuteMesCommand(string command, ulong playerId)
        {
            var identity = MyAPIGateway.Players.TryGetIdentityId(playerId);
            Core.MesAPI.ChatCommand(command, new MatrixD
            {
                Translation = Vector3D.Zero,
                Forward = Vector3D.Zero,
            }, identity, playerId);
        }
    }
}