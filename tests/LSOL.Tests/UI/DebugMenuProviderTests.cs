using System.Linq;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class DebugMenuProviderTests
    {
        [TestMethod]
        public void BuildRootItems_InsertsFuelActionsBetweenCargoDeleteAndVehicleDelete()
        {
            var provider = new DebugMenuProvider();
            var callbacks = new DebugMenuCallbacks
            {
                IndustryCaption = () => "Industry",
                IndustryDetail = () => "Detail",
                ResourceCaption = () => "Resource",
                ResourceDetail = () => "Detail",
                ResourceAmountCaption = () => "Amount",
                MoneyAmountCaption = () => "Money",
                DistrictCaption = () => "District",
                DistrictDetail = () => "Detail",
                DistrictReputationAmountCaption = () => "Reputation",
                DistrictStateCaption = () => "State",
                MissionBoardDetail = () => "Missions",
                SelectedMoneyAmount = () => 1000f,
                SelectedDistrictState = () => "Established",
                SelectedDistrictReputationAmount = () => 10f,
            };

            var captions = provider
                .BuildRootItems(callbacks)
                .Where(item => item != null && item.CaptionFactory != null)
                .Select(item => item.CaptionFactory())
                .ToList();

            var deleteCargoIndex = captions.IndexOf("Delete vehicle cargo");
            var emptyFuelIndex = captions.IndexOf("Empty fuel tank");
            var fillFuelIndex = captions.IndexOf("Fill fuel tank");
            var deleteVehicleIndex = captions.IndexOf("Delete current vehicle");

            Assert.IsTrue(deleteCargoIndex >= 0);
            Assert.AreEqual(deleteCargoIndex + 1, emptyFuelIndex);
            Assert.AreEqual(emptyFuelIndex + 1, fillFuelIndex);
            Assert.AreEqual(fillFuelIndex + 1, deleteVehicleIndex);
        }
    }
}