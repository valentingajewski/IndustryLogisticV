using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class CompanyMapDepotSpecializationFormatterTests
    {
        [TestMethod]
        public void BuildDepotListDetail_WithSupportRole_IncludesEffectSummaryAndStaffCounts()
        {
            var detail = CompanyMapDepotSpecializationFormatter.BuildDepotListDetail(
                "Port",
                "Owned and operational",
                new CompanyMapDepotSpecializationInfo
                {
                    Specialization = DepotSpecialization.Support,
                    Label = "Support",
                    EffectSummary = "Support: amplifies district support bonuses and stabilizes licensed territory.",
                },
                new TerritorySiteState
                {
                    LoaderCount = 2,
                    MechanicCount = 0,
                    GuardCount = 1,
                    ManagerCount = 1,
                });

            Assert.AreEqual(
                "Port | Owned and operational | Support: amplifies district support bonuses and stabilizes licensed territory. | Staff 2/0/1/1",
                detail);
        }

        [TestMethod]
        public void BuildDepotListDetail_WithNoRole_UsesGeneralCoverageFallback()
        {
            var detail = CompanyMapDepotSpecializationFormatter.BuildDepotListDetail(
                "Davis",
                "Leased and crewed",
                new CompanyMapDepotSpecializationInfo
                {
                    Specialization = DepotSpecialization.None,
                    Label = "General",
                    EffectSummary = "No specialization selected. Use left/right to assign a district role.",
                },
                new TerritorySiteState());

            StringAssert.Contains(detail, "General coverage only. Use depot detail to assign a district role.");
        }

        [TestMethod]
        public void BuildDistrictSupportDetail_WithSpecializedDepots_AppendsRoleSummary()
        {
            var detail = CompanyMapDepotSpecializationFormatter.BuildDistrictSupportDetail(
                2,
                3,
                8f,
                " | Risk C1/S0",
                new[]
                {
                    new CompanyMapDepotSpecializationInfo
                    {
                        Specialization = DepotSpecialization.Dispatch,
                        Label = "Dispatch",
                        EffectSummary = "Dispatch: stronger delivery returns in this district and softer corridor upkeep targets.",
                    },
                    new CompanyMapDepotSpecializationInfo
                    {
                        Specialization = DepotSpecialization.Support,
                        Label = "Support",
                        EffectSummary = "Support: amplifies district support bonuses and stabilizes licensed territory.",
                    },
                });

            Assert.AreEqual(
                "Franchises 2 | Corridor rights 3 | Support bonus +8.00% | Risk C1/S0 | Depot roles: Dispatch: stronger delivery returns in this district and softer corridor upkeep targets; Support: amplifies district support bonuses and stabilizes licensed territory.",
                detail);
        }

        [TestMethod]
        public void BuildNetworkSupportSummary_WithNoSpecializedDepots_UsesGeneralCoverageFallback()
        {
            var detail = CompanyMapDepotSpecializationFormatter.BuildNetworkSupportSummary(
                1,
                0,
                2,
                5f,
                string.Empty,
                new[]
                {
                    new CompanyMapDepotSpecializationInfo
                    {
                        Specialization = DepotSpecialization.None,
                        Label = "General",
                        EffectSummary = "No specialization selected. Use left/right to assign a district role.",
                    },
                });

            Assert.AreEqual(
                "Depots 1 | Franchises 0\nCorridor rights 2 | Support bonus +5.00%\nDepot roles: general coverage only.",
                detail);
        }
    }
}