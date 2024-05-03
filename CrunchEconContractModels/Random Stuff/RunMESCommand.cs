using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    public class RunMESCommand : CommandModule
    {
        [Command("runmes", "Run an MES command")]
        [Permission(MyPromoteLevel.Admin)]
        public void RunCommand(string command, long playerSteamId = 0l)
        {
            if (playerSteamId == 0l)
            {
                Core.MesAPI.ChatCommand(command, new MatrixD()
                {
                    Translation = Vector3D.Zero,
                    Forward = Vector3D.Zero,
                }, 0, 0);

                Context.Respond("Command executed?");
            }
            else
            {
                if (Sync.Players.TryGetPlayerBySteamId((ulong)playerSteamId, out var player))
                {
                    Context.Respond("Command executed?");
                    Core.MesAPI.ChatCommand(command, new MatrixD()
                    {
                        Translation = Vector3D.Zero,
                        Forward = Vector3D.Zero,
                    }, player.Identity.IdentityId, player.Id.SteamId);
                    return;
                }

                Context.Respond("Player with that steam id not found");
            }
        }
    }
}