using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRage.ObjectBuilders;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
   public class BlockReplacer : CommandModule
    {
        [Command("replace", "replace gatlings with assaults")]
        [Permission(MyPromoteLevel.Admin)]
        public void Replace()
        {
            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> gridWithSubGrids =
                GridFinder.FindLookAtGridGroup(Context.Player.Character);
            foreach (var item in gridWithSubGrids)
            {
                var grids = new List<IMyCubeGrid>();
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in item.Nodes)
                {
                    MyCubeGrid grid = groupNodes.NodeData;
                    grids.Add(grid);
                }
                Context.Respond("Replacing?");
                ReplaceBlocksInGrids(grids, "MyObjectBuilder_SmallGatlingGun", "", "SmallGatlingGun", "SmallBlockAutocannon");
                Context.Respond("Done?");
            }
        }

        public void ReplaceBlocksInGrids(
            List<IMyCubeGrid> grids,
            string fromType,
            string fromSubtype,
            string toType,
            string toSubtype
        )
        {
            foreach (var grid in grids)
            {
                var blocksToReplace = new List<IMySlimBlock>();
                var allBlocks = new List<IMySlimBlock>();
                grid.GetBlocks(allBlocks);
                Core.Log.Info($"{allBlocks.Count}");
                foreach (var block in allBlocks)
                {
                    var def = block.BlockDefinition.Id;

                    // Match default subtype by checking for empty SubtypeName
                    bool typeMatches = def.TypeId.ToString() == fromType;
                    bool subtypeMatches = string.IsNullOrEmpty(fromSubtype) || def.SubtypeName == fromSubtype;

                    if (typeMatches && subtypeMatches)
                    {
                        blocksToReplace.Add(block);
                        Core.Log.Info("Matches");
                    }
                }

                foreach (var oldBlock in blocksToReplace)
                {
                    var position = oldBlock.Position;
                    var orientation = oldBlock.Orientation;
                    var colorMask = oldBlock.ColorMaskHSV;
                    var skinId = oldBlock.SkinSubtypeId;

                    grid.RemoveBlock(oldBlock, updatePhysics:true);

                    var newDef = MyDefinitionManager.Static.GetCubeBlockDefinition(
                      MyDefinitionId.Parse($"{toType}/{toSubtype}")
                    );

                    if (newDef != null)
                    {
                        Core.Log.Info("Replacing");
                        var newBlockBuilder = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(newDef.Id);
                        newBlockBuilder.Min = position;
                        newBlockBuilder.BlockOrientation = orientation;
                        newBlockBuilder.ColorMaskHSV = colorMask;
                        newBlockBuilder.SkinSubtypeId = skinId.String;
                        newBlockBuilder.Owner = oldBlock.OwnerId;
                        grid.AddBlock(newBlockBuilder, true);


                    }
                    else
                    {
                        Core.Log.Info("New defs null");
                    }
                }
                var asMy = grid as MyCubeGrid;
                asMy.RaiseGridChanged();

            }
        }
    }
}
