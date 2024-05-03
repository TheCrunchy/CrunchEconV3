//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using System.Text;
//using System.Threading.Tasks;
//using Sandbox.Definitions;
//using Sandbox.Game.Entities;
//using Sandbox.Game.Entities.Character;
//using Sandbox.Game.SessionComponents;
//using Sandbox.Game.World;
//using SpaceEngineers.Game.World;
//using Torch.Managers.PatchManager;
//using VRage.Game.Entity;
//using VRageMath;

//namespace CrunchEconContractModels.Random_Stuff
//{
//    [PatchShim]
//    public static class SpawnPatchSuit
//    {
//        internal static readonly MethodInfo flee =
//            typeof(MySpaceRespawnComponent).GetMethod("SpawnInSuit",
//                BindingFlags.Static | BindingFlags.NonPublic) ??
//            throw new Exception("Failed to find patch method");

//        internal static readonly MethodInfo patchFlee =
//            typeof(SpawnPatchTwo).GetMethod(nameof(SpawnInSuit),
//                BindingFlags.Static | BindingFlags.Public) ??
//            throw new Exception("Failed to find patch method");

//        public static void Patch(PatchContext ctx)
//        {
//            ctx.GetPattern(flee).Prefixes.Add(patchFlee);

//        }

//        private voist SpawnInSuit(
//            MyPlayer player,
//            MyEntity spawnedBy,
//            MyBotDefinition botDefinition,
//            string modelName,
//            Color color)
//        {
//            Vector3D position;
//            Vector3 forward;
//            Vector3 up;
//            MySpaceRespawnComponent.GetSpawnPositionInSpace(new MySpaceRespawnComponent.SpawnInfo()
//            {
//                CollisionRadius = 10f,
//                SpawnNearPlayers = false,
//                PlanetDeployAltitude = 10f,
//                IdentityId = player.Identity.IdentityId
//            }, out position, out forward, out up);
//            MyCharacter character = MyCharacter.CreateCharacter(MatrixD.CreateWorld(position, forward, up), Vector3.Zero, player.Identity.DisplayPlatformName, modelName, new Vector3?(color.ToVector3()), botDefinition, true, false, (MyCockpit)null, true, player.Identity.IdentityId, true);
//            Sync.Players.SetPlayerCharacter(player, character, spawnedBy);
//            Sync.Players.RevivePlayer(player);
//        }
//    }
//}
