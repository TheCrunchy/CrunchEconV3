using CrunchEconV3;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class GridSalesTwo
    {
        public static void Patch(PatchContext ctx)
        {
            var confirm = new GridSales.Confirm();
			Core.Log.Error("This shit compiled");
        }
    }
}
