using System.Collections.Generic;
using System.Linq;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class AlertRulesEvaluatorTests
    {
        [TestMethod]
        public void BuildCandidates_WithRentLeadTime_IncludesOnlyMatchingRentBills()
        {
            var context = new AlertRulesEvaluationContext
            {
                UpcomingBills = new[]
                {
                    new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.OfficeRent,
                        Label = "Arcadius",
                        Detail = "Weekly office rent",
                        Amount = 2500f,
                        DueInMinutes = 120,
                    },
                    new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.OfficeRent,
                        Label = "Maze Bank West",
                        Detail = "Weekly office rent",
                        Amount = 2500f,
                        DueInMinutes = 420,
                    },
                    new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.CorporateOverhead,
                        Label = "Corporate overhead",
                        Detail = "Scale score 4",
                        Amount = 900f,
                        DueInMinutes = 90,
                    },
                },
            };
            var settings = new AlertRulesPersistenceSnapshot
            {
                RentLeadTime = AlertLeadTimeMode.Within180Minutes,
                ContractLeadTime = AlertLeadTimeMode.Off,
                FleetMode = FleetAlertMode.Off,
                TerritoryMode = TerritoryAlertMode.Off,
            };

            var candidates = AlertRulesEvaluator.BuildCandidates(settings, context);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(AlertRuleDomain.Rent, candidates[0].Domain);
            StringAssert.Contains(candidates[0].Message, "Arcadius");
            StringAssert.Contains(candidates[0].Message, "in 2h");
        }

        [TestMethod]
        public void BuildCandidates_WithContractLeadTime_IncludesPlayerExpiryAndNpcPayroll()
        {
            var context = new AlertRulesEvaluationContext
            {
                AcceptedPlayerContracts = new[]
                {
                    new PlayerContractListingSummary
                    {
                        Id = "pc-1",
                        Commodity = "Steel",
                        OriginName = "Alpha Mine",
                        DestinationName = "Bravo Yard",
                        RemainingExpiryMinutes = 45,
                        Status = PlayerContractStatus.Accepted,
                        StatusDetail = "Load before the board rolls over.",
                    },
                },
                UpcomingBills = new[]
                {
                    new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.NpcWages,
                        Label = "Quarry Loop",
                        Detail = "Weekly payroll for Veteran",
                        Amount = 1800f,
                        DueInMinutes = 30,
                    },
                },
            };
            var settings = new AlertRulesPersistenceSnapshot
            {
                RentLeadTime = AlertLeadTimeMode.Off,
                ContractLeadTime = AlertLeadTimeMode.Within60Minutes,
                FleetMode = FleetAlertMode.Off,
                TerritoryMode = TerritoryAlertMode.Off,
            };

            var candidates = AlertRulesEvaluator.BuildCandidates(settings, context);

            Assert.AreEqual(2, candidates.Count);
            Assert.IsTrue(candidates.Any(candidate => candidate.Message.Contains("Steel | Alpha Mine -> Bravo Yard")));
            Assert.IsTrue(candidates.Any(candidate => candidate.Message.Contains("Quarry Loop")));
        }

        [TestMethod]
        public void BuildCandidates_WithFleetCriticalMode_SkipsFuelWatchButKeepsCriticalIssues()
        {
            var context = new AlertRulesEvaluationContext
            {
                FleetSummary = new TabletFleetAlertSummary
                {
                    HasActiveCompanyVehicle = true,
                    ActiveVehicleName = "Hauler Alpha",
                    FuelCurrentLiters = 35f,
                    FuelCapacityLiters = 100f,
                    FuelRatio = 0.35f,
                    OverdueInspectionCount = 1,
                    WorstInspectionOverdueWeeks = 2,
                    WorstOverdueVehicleName = "Hauler Beta",
                },
            };
            var settings = new AlertRulesPersistenceSnapshot
            {
                RentLeadTime = AlertLeadTimeMode.Off,
                ContractLeadTime = AlertLeadTimeMode.Off,
                FleetMode = FleetAlertMode.CriticalOnly,
                TerritoryMode = TerritoryAlertMode.Off,
            };

            var candidates = AlertRulesEvaluator.BuildCandidates(settings, context);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(AlertRuleDomain.Fleet, candidates[0].Domain);
            StringAssert.Contains(candidates[0].Message, "Inspection Overdue");
            Assert.IsFalse(candidates[0].Message.Contains("Fuel Watch"));
        }

        [TestMethod]
        public void BuildCandidates_WithTerritoryChargeAndRisk_IncludesBothChargeAndDistrictPressure()
        {
            var context = new AlertRulesEvaluationContext
            {
                UpcomingBills = new[]
                {
                    new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.TerritoryOperations,
                        Label = "Terminal charter",
                        Detail = "Charter Active | Ops $1,200/wk",
                        Amount = 1200f,
                        DueInMinutes = 120,
                    },
                },
                DistrictComparisons = new[]
                {
                    new TabletDistrictComparison
                    {
                        DistrictName = "Terminal",
                        WeeklyOperationsCost = 1200f,
                        LicenseStatus = DistrictLicenseStatus.Probation,
                        LicenseActivityTons = 14f,
                        LicenseTargetTons = 40f,
                        CorridorRiskCount = 1,
                        ServiceRiskCount = 0,
                        CompetitivePressurePercent = 74f,
                        CompetitiveOpportunityPercent = 18f,
                        ControlledSites = 2,
                    },
                },
            };
            var settings = new AlertRulesPersistenceSnapshot
            {
                RentLeadTime = AlertLeadTimeMode.Off,
                ContractLeadTime = AlertLeadTimeMode.Off,
                FleetMode = FleetAlertMode.Off,
                TerritoryMode = TerritoryAlertMode.ChargesAndRisk,
            };

            var candidates = AlertRulesEvaluator.BuildCandidates(settings, context);

            Assert.AreEqual(2, candidates.Count);
            Assert.IsTrue(candidates.Any(candidate => candidate.Message.Contains("Terminal charter")));
            Assert.IsTrue(candidates.Any(candidate => candidate.Message.Contains("Terminal charter on probation") || candidate.Message.Contains("Terminal")));
        }

        [TestMethod]
        public void BuildCandidates_WithLiveDistrictEvent_IncludesTerritoryEventNotification()
        {
            var context = new AlertRulesEvaluationContext
            {
                DistrictComparisons = new[]
                {
                    new TabletDistrictComparison
                    {
                        DistrictName = "Airport",
                        LicenseStatus = DistrictLicenseStatus.Active,
                        DistrictEventHeadline = "Airport fuel shortage",
                        DistrictEventCommodity = "Fuel",
                        DistrictEventStatus = "Critical | Relief 2.0/10.0t | closes week 2",
                        DistrictEventSeverityPercent = 78f,
                        DistrictEventSeverityLabel = "Critical",
                    },
                },
            };
            var settings = new AlertRulesPersistenceSnapshot
            {
                RentLeadTime = AlertLeadTimeMode.Off,
                ContractLeadTime = AlertLeadTimeMode.Off,
                FleetMode = FleetAlertMode.Off,
                TerritoryMode = TerritoryAlertMode.ChargesAndRisk,
            };

            var candidates = AlertRulesEvaluator.BuildCandidates(settings, context);

            Assert.AreEqual(1, candidates.Count);
            StringAssert.Contains(candidates[0].Message, "Airport fuel shortage");
            StringAssert.Contains(candidates[0].Message, "Fuel");
            Assert.AreEqual(AlertRuleDomain.Territory, candidates[0].Domain);
        }

        [TestMethod]
        public void SelectNextNotification_RespectsPerKeyCooldown()
        {
            var context = new AlertRulesEvaluationContext
            {
                UpcomingBills = new[]
                {
                    new TabletUpcomingBillEntry
                    {
                        Category = CompanyFinanceCategory.OfficeRent,
                        Label = "Arcadius",
                        Detail = "Weekly office rent",
                        Amount = 2500f,
                        DueInMinutes = 60,
                    },
                },
            };
            var settings = new AlertRulesPersistenceSnapshot
            {
                RentLeadTime = AlertLeadTimeMode.Within180Minutes,
                ContractLeadTime = AlertLeadTimeMode.Off,
                FleetMode = FleetAlertMode.Off,
                TerritoryMode = TerritoryAlertMode.Off,
            };
            var lastShown = new Dictionary<string, int>();

            var first = AlertRulesEvaluator.SelectNextNotification(settings, context, lastShown, 100000, 90000);

            Assert.IsNotNull(first);
            lastShown[first.Key] = 100000;

            var suppressed = AlertRulesEvaluator.SelectNextNotification(settings, context, lastShown, 150000, 90000);
            var returned = AlertRulesEvaluator.SelectNextNotification(settings, context, lastShown, 190001, 90000);

            Assert.IsNull(suppressed);
            Assert.IsNotNull(returned);
            Assert.AreEqual(first.Key, returned.Key);
        }
    }
}