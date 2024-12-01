using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;

namespace CrunchEconV3.PlugAndPlay.Extensions
{
    public static class CubeGridExtensions
    {
        public static long GetGridOwner(this MyCubeGrid grid)
        {
            var gridOwnerList = grid.BigOwners;
            var ownerCnt = gridOwnerList.Count;
            var gridOwner = 0L;

            if (ownerCnt > 0 && gridOwnerList[0] != 0)
                gridOwner = gridOwnerList[0];
            else if (ownerCnt > 1)
                gridOwner = gridOwnerList[1];

            return gridOwner;
        }
        public static MyFaction? GetGridOwnerFaction(this MyCubeGrid grid)
        {
            var gridOwnerList = grid.BigOwners;
            var ownerCnt = gridOwnerList.Count;
            var gridOwner = 0L;

            if (ownerCnt > 0 && gridOwnerList[0] != 0)
                gridOwner = gridOwnerList[0];
            else if (ownerCnt > 1)
                gridOwner = gridOwnerList[1];

            var faction = MySession.Static.Factions.GetPlayerFaction(gridOwner);

            return faction;
        }

        public static List<MyCharacter> GetControllingPlayers(this MyCubeGrid grid)
        {
            var returnItems = new List<MyCharacter?>();
            foreach (var cockpit in grid.GetFatBlocks().OfType<IMyCockpit>())
            {
                if (!cockpit.IsUnderControl) continue;
                var asCockpit = cockpit as MyCockpit;
                if (asCockpit?.Pilot != null)
                {
                    returnItems.Add(asCockpit.Pilot);
                }
            }

            return returnItems;
        }
    }
}
