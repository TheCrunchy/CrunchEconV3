   [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class CrunchContractCore : MySessionComponentBase
    {
		public override void LoadData()
        {
            //  Force localization load for English for
            //  the contract category.
            MyTexts.LoadTexts(ModContext.ModPath);
		}
	}