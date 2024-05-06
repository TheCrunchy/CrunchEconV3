using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
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
                var identity = MySession.Static.Players.TryGetIdentityId((ulong)playerSteamId);

                Context.Respond("Command executed?");
                Core.MesAPI.ChatCommand(command, new MatrixD()
                {
                    Translation = Vector3D.Zero,
                    Forward = Vector3D.Zero,
                }, identity, (ulong)playerSteamId);
                return;

            }
        }

        [Command("gts", "Run an MES command")]
        [Permission(MyPromoteLevel.None)]
        public void RunCommand()
        {

            Core.MesAPI.ChatCommand("/MES.GTS", Context.Player.Character.PositionComp.WorldMatrixRef, Context.Player.IdentityId, Context.Player.SteamUserId);

            Context.Respond("Command executed");
        }
    }
}