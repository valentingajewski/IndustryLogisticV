using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class AmbientWorldDispatchTextTests
    {
        [TestMethod]
        public void FormatJobType_RivalFreight_UsesNeutralFreightLabel()
        {
            Assert.AreEqual("Freight", AmbientWorldDispatchText.FormatJobType(NpcWorldJobType.RivalFreight));
        }

        [TestMethod]
        public void NormalizeStatusText_LegacyRivalAndSpotStatuses_UsesNeutralAmbientText()
        {
            Assert.AreEqual("Queued for dispatch", AmbientWorldDispatchText.NormalizeStatusText("Spot market window open"));
            Assert.AreEqual("Queued for dispatch", AmbientWorldDispatchText.NormalizeStatusText("Rival freight listed"));
            Assert.AreEqual("NPC convoy en route", AmbientWorldDispatchText.NormalizeStatusText("Rival convoy en route"));
            Assert.AreEqual("NPC convoy en route", AmbientWorldDispatchText.NormalizeStatusText("Spot window closed; NPC convoy en route"));
            Assert.AreEqual("Ambient convoy hidden after repeated path failure.", AmbientWorldDispatchText.NormalizeStatusText("Rival convoy hidden after repeated path failure."));
            Assert.AreEqual("Ambient convoy hidden: visual spawn unavailable.", AmbientWorldDispatchText.NormalizeStatusText("Rival convoy hidden: visual spawn unavailable."));
        }

        [TestMethod]
        public void BuildBlipNameAndHiddenStatus_AvoidRivalAndSpotWording()
        {
            Assert.AreEqual("Ambient Freight: Steel", AmbientWorldDispatchText.BuildBlipName("Steel"));
            Assert.AreEqual("Ambient convoy hidden: route endpoints unavailable.", AmbientWorldDispatchText.BuildHiddenStatusText("route endpoints unavailable"));
        }
    }
}