using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletLocationRowFormattingTests
    {
        [TestMethod]
        public void BuildLocationCaption_ReturnsSiteNameWithoutBracketedOwnershipTag()
        {
            var summary = new TabletLocationSummary
            {
                Name = "Alpha Plant",
                OwnershipTag = "~g~[OWNED]~s~",
            };

            Assert.AreEqual("Alpha Plant", TabletUiHelpers.BuildLocationCaption(summary));
        }

        [TestMethod]
        public void BuildLocationOverviewDetail_PlacesOwnershipBeforeAccessAndPreservesOverviewText()
        {
            var summary = new TabletLocationSummary
            {
                OwnershipTag = "~r~[NOT OWNED]~s~",
                PermitTag = "~r~[LOCKED]~s~",
                OverviewDetail = "Storage 482.7t | Omega 0.0t",
            };

            Assert.AreEqual(
                "~r~NOT OWNED~s~ | ~r~LOCKED~s~ | Storage 482.7t | Omega 0.0t",
                TabletUiHelpers.BuildLocationOverviewDetail(summary));
        }

        [TestMethod]
        public void WarehouseRowDetail_CanPrefixAcceptedResourcesAndKeepOwnershipBeforeAccess()
        {
            var summary = new TabletLocationSummary
            {
                OwnershipTag = "~g~[OWNED]~s~",
                PermitTag = "~g~[OPEN]~s~",
                OverviewDetail = "Storage 80.0t | Omega 0.0t",
            };

            var detail = string.Format(
                "Accepts Fuel, Oil | {0}",
                TabletUiHelpers.BuildLocationOverviewDetail(summary));

            Assert.AreEqual(
                "Accepts Fuel, Oil | ~g~OWNED~s~ | ~g~OPEN~s~ | Storage 80.0t | Omega 0.0t",
                detail);
        }

        [TestMethod]
        public void BuildPermitCaption_ReturnsSiteNameWithoutPermitTag()
        {
            var summary = new TabletLocationSummary
            {
                Name = "Bravo Foundry",
                PermitTag = "~g~[PERMIT]~s~",
            };

            Assert.AreEqual("Bravo Foundry", TabletUiHelpers.BuildPermitCaption(summary));
            Assert.AreEqual("Bravo Foundry", TabletUiHelpers.BuildPermitCaption("Bravo Foundry"));
        }
    }
}