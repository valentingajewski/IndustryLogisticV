using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class CompanyMapForecastFormatterTests
    {
        [TestMethod]
        public void BuildDistrictLicenseForecast_WhenActiveAndCompliant_ShowsSafeState()
        {
            var district = new TerritoryDistrictState
            {
                LicenseStatus = DistrictLicenseStatus.Active,
                CurrentWeekActivityTons = 42f,
                RequiredWeeklyActivityTons = 40f,
            };

            Assert.AreEqual(
                "Forecast: safe this week.",
                CompanyMapForecastFormatter.BuildDistrictLicenseForecast(district));
        }

        [TestMethod]
        public void BuildDistrictLicenseForecast_WhenProbationAndShort_ShowsSuspensionRisk()
        {
            var district = new TerritoryDistrictState
            {
                LicenseStatus = DistrictLicenseStatus.Probation,
                LicenseStrikeCount = 1,
                CurrentWeekActivityTons = 18f,
                RequiredWeeklyActivityTons = 32f,
            };

            Assert.AreEqual(
                "Forecast: suspension risk | Need 14t more before maintenance.",
                CompanyMapForecastFormatter.BuildDistrictLicenseForecast(district));
        }

        [TestMethod]
        public void BuildCorridorForecast_WhenWatch_ShowsShortfallGuidance()
        {
            var corridor = new TerritoryCorridorState
            {
                RightLevel = CorridorRightLevel.Corridor,
                CurrentWeekDeliveredTons = 28f,
                RequiredWeeklyDeliveredTons = 55f,
                UpkeepStatus = "Watch (28/55 t)",
            };

            Assert.AreEqual(
                "Watch | Need 27t this week to stay clear.",
                CompanyMapForecastFormatter.BuildCorridorForecast(corridor));
        }

        [TestMethod]
        public void BuildCorridorForecast_WhenAtRisk_ShowsDecayWarningAndShortfall()
        {
            var corridor = new TerritoryCorridorState
            {
                RightLevel = CorridorRightLevel.Priority,
                CurrentWeekDeliveredTons = 28f,
                RequiredWeeklyDeliveredTons = 110f,
                UpkeepStatus = "At risk (28/110 t)",
            };

            Assert.AreEqual(
                "At risk of decay | Need 82t this week to avoid degradation.",
                CompanyMapForecastFormatter.BuildCorridorForecast(corridor));
        }
    }
}