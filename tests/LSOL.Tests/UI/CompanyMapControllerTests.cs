using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class CompanyMapControllerTests
    {
        [TestMethod]
        public void GetNetworkViewSnapshot_AggregatesTerritoryOperationsAndCorridorLookups()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out var districtB);

            var snapshot = InvokeGetNetworkViewSnapshot(controller);
            var operationsSummary = territoryManager.GetOperationsSummary();
            var expectedOperationsByDistrict = operationsSummary.Districts
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.DistrictName))
                .ToDictionary(entry => entry.DistrictName, entry => entry, StringComparer.OrdinalIgnoreCase);

            var operationsByDistrict = GetPropertyValue<IDictionary<string, TerritoryDistrictOperationsEntry>>(snapshot, "OperationsByDistrict");
            var visibleCorridors = GetPropertyValue<IList<TerritoryCorridorState>>(snapshot, "VisibleCorridors");
            var visibleCorridorsByDistrict = GetPropertyValue<IDictionary<string, List<TerritoryCorridorState>>>(snapshot, "VisibleCorridorsByDistrict");
            var supportBonusByDistrict = GetPropertyValue<IDictionary<string, float>>(snapshot, "SupportBonusByDistrict");
            var npcReadyByDistrict = GetPropertyValue<IDictionary<string, bool>>(snapshot, "NpcReadyByDistrict");

            Assert.AreEqual(territoryManager.GetControlledDistrictCount(), GetPropertyValue<int>(snapshot, "ControlledDistrictCount"));
            Assert.AreEqual(territoryManager.GetActiveCorridorCount(), GetPropertyValue<int>(snapshot, "ActiveCorridorCount"));
            Assert.AreEqual(1, visibleCorridors.Count, "Only active corridors should be exposed in the network snapshot.");
            Assert.AreEqual(CorridorRightLevel.Priority, visibleCorridors[0].RightLevel);
            Assert.AreEqual(territoryManager.GetDistrictSupportBonus(districtA), supportBonusByDistrict[districtA], 0.001f);
            Assert.AreEqual(territoryManager.GetDistrictSupportBonus(districtB), supportBonusByDistrict[districtB], 0.001f);
            Assert.AreEqual(territoryManager.IsDistrictEstablishedForNpc(districtA), npcReadyByDistrict[districtA]);
            Assert.AreEqual(territoryManager.IsDistrictEstablishedForNpc(districtB), npcReadyByDistrict[districtB]);
            Assert.IsTrue(visibleCorridorsByDistrict.ContainsKey(districtA));
            Assert.IsTrue(visibleCorridorsByDistrict.ContainsKey(districtB));
            Assert.AreEqual(1, visibleCorridorsByDistrict[districtA].Count);
            Assert.AreEqual(1, visibleCorridorsByDistrict[districtB].Count);
            Assert.AreEqual(expectedOperationsByDistrict[districtA].TotalWeeklyCost, operationsByDistrict[districtA].TotalWeeklyCost, 0.01f);
            Assert.AreEqual(expectedOperationsByDistrict[districtA].CorridorRiskCount, operationsByDistrict[districtA].CorridorRiskCount);
            Assert.AreEqual(expectedOperationsByDistrict[districtA].ServiceRiskCount, operationsByDistrict[districtA].ServiceRiskCount);
            Assert.AreEqual(expectedOperationsByDistrict[districtB].TotalWeeklyCost, operationsByDistrict[districtB].TotalWeeklyCost, 0.01f);
        }

        [TestMethod]
        public void GetNetworkViewSnapshot_ReusesCacheUntilTerritoryStateChanges()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out _);

            var first = InvokeGetNetworkViewSnapshot(controller);
            var second = InvokeGetNetworkViewSnapshot(controller);

            Assert.AreSame(first, second);

            territoryManager.GetDistrictState(districtA).InfluenceRatio += 0.05f;

            var refreshed = InvokeGetNetworkViewSnapshot(controller);

            Assert.AreNotSame(first, refreshed);
        }

        [TestMethod]
        public void GetNetworkViewSnapshot_ReusesCacheUntilCorridorCompetitionStateChanges()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out var districtB);

            var first = InvokeGetNetworkViewSnapshot(controller);

            territoryManager.GetCorridorState(districtA, districtB).CompetitivePressure += 0.08f;

            var refreshed = InvokeGetNetworkViewSnapshot(controller);

            Assert.AreNotSame(first, refreshed);
        }

        [TestMethod]
        public void GetNetworkViewSnapshot_ReusesCacheUntilCarrierMetadataChanges()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out var districtB);

            var first = InvokeGetNetworkViewSnapshot(controller);

            territoryManager.GetDistrictState(districtA).ActiveCarrierCount = 2;
            territoryManager.GetDistrictState(districtA).DominantCarrierName = "Port Freight";
            territoryManager.GetCorridorState(districtA, districtB).ActiveCarrierCount = 1;
            territoryManager.GetCorridorState(districtA, districtB).DominantCarrierName = "Port Freight";

            var refreshed = InvokeGetNetworkViewSnapshot(controller);

            Assert.AreNotSame(first, refreshed);
        }

        [TestMethod]
        public void GetNetworkViewSnapshot_ReusesCacheUntilPlannerOverlayStateChanges()
        {
            RoutePlannerOverlaySnapshot overlay = null;
            var controller = CreateControllerWithScenario(
                out var territoryManager,
                out var districtA,
                out var districtB,
                () => overlay);

            controller.OpenNetworkView(showPlannerOverlay: true);

            overlay = new RoutePlannerOverlaySnapshot
            {
                SelectedCandidateId = "lane-a",
                SelectedDistrictA = districtA,
                SelectedDistrictB = districtB,
                Lanes = new[]
                {
                    new RoutePlannerOverlayLane
                    {
                        CandidateId = "lane-a",
                        DistrictA = districtA,
                        DistrictB = districtB,
                        Label = "Fuel planner lane",
                        Kind = RoutePlannerOverlayLaneKind.Recommended,
                        IsSelected = true,
                    },
                },
            };

            var first = InvokeGetNetworkViewSnapshot(controller);

            overlay = new RoutePlannerOverlaySnapshot
            {
                SelectedCandidateId = "lane-b",
                SelectedDistrictA = districtB,
                SelectedDistrictB = districtA,
                Lanes = new[]
                {
                    new RoutePlannerOverlayLane
                    {
                        CandidateId = "lane-b",
                        DistrictA = districtB,
                        DistrictB = districtA,
                        Label = "Blocked planner lane",
                        Kind = RoutePlannerOverlayLaneKind.Blocked,
                        IsSelected = true,
                    },
                },
            };

            var refreshed = InvokeGetNetworkViewSnapshot(controller);

            Assert.AreNotSame(first, refreshed);
        }

        [TestMethod]
        public void GetNetworkViewSnapshot_HidesPlannerOverlayUnlessRoutePlannerMapRequested()
        {
            var overlay = new RoutePlannerOverlaySnapshot
            {
                SelectedCandidateId = "lane-a",
                SelectedDistrictA = "Port",
                SelectedDistrictB = "GrandSenora",
                Lanes = new[]
                {
                    new RoutePlannerOverlayLane
                    {
                        CandidateId = "lane-a",
                        DistrictA = "Port",
                        DistrictB = "GrandSenora",
                        Label = "Fuel planner lane",
                        Kind = RoutePlannerOverlayLaneKind.Selected,
                        IsSelected = true,
                    },
                },
            };
            var controller = CreateControllerWithScenario(
                out var territoryManager,
                out _,
                out _,
                () => overlay);

            var snapshot = InvokeGetNetworkViewSnapshot(controller);
            var plannerOverlay = GetPropertyValue<RoutePlannerOverlaySnapshot>(snapshot, "PlannerOverlay");

            Assert.IsNotNull(plannerOverlay);
            Assert.AreEqual(0, plannerOverlay.Lanes.Count, "Company Map should not show planner overlay unless it was opened from the route planner.");

            controller.OpenNetworkView(showPlannerOverlay: true);

            snapshot = InvokeGetNetworkViewSnapshot(controller);
            plannerOverlay = GetPropertyValue<RoutePlannerOverlaySnapshot>(snapshot, "PlannerOverlay");

            Assert.AreEqual(1, plannerOverlay.Lanes.Count);
            Assert.AreEqual("lane-a", plannerOverlay.SelectedCandidateId);
        }

        [TestMethod]
        public void GetNetworkViewSnapshot_ExposesContestedCorridorMetadata()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out var districtB);

            var snapshot = InvokeGetNetworkViewSnapshot(controller);
            var visibleCorridors = GetPropertyValue<IList<TerritoryCorridorState>>(snapshot, "VisibleCorridors");

            Assert.AreEqual(1, visibleCorridors.Count);
            Assert.AreEqual(2, visibleCorridors[0].ActiveCompetitionJobs);
            Assert.AreEqual(1, visibleCorridors[0].CompetitiveWinCount);
            Assert.IsTrue(visibleCorridors[0].CompetitivePressure > 0.4f);
            StringAssert.Contains(visibleCorridors[0].CompetitionStatus, "Contested");

            var district = territoryManager.GetDistrictState(districtA);
            Assert.IsNotNull(district);
            Assert.AreEqual(1, district.ContestedCorridorCount);
            Assert.AreEqual(districtB, district.HottestCorridorName);
        }

        [TestMethod]
        public void GetNetworkViewSnapshot_HidesCompetitionOnlyCorridorsWithoutRights()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out var districtB);
            territoryManager.ApplySnapshot(new TerritoryPersistenceSnapshot
            {
                Corridors =
                {
                    new TerritoryCorridorSnapshot
                    {
                        DistrictA = districtA,
                        DistrictB = districtB,
                        RightLevel = CorridorRightLevel.None,
                        CurrentWeekDeliveryCount = 3,
                        CurrentWeekDeliveredTons = 18f,
                        CompetitivePressure = 0.36f,
                        ActiveCompetitionJobs = 2,
                        ActiveCarrierCount = 1,
                        DominantCarrierName = "Senora Line",
                        CompetitiveWinCount = 1,
                        ContestedWeekStreak = 2,
                    },
                },
            });

            var snapshot = InvokeGetNetworkViewSnapshot(controller);
            var visibleCorridors = GetPropertyValue<IList<TerritoryCorridorState>>(snapshot, "VisibleCorridors");

            Assert.AreEqual(0, GetPropertyValue<int>(snapshot, "ActiveCorridorCount"));
            Assert.AreEqual(0, visibleCorridors.Count, "Competition-only corridors without route rights should stay hidden in the network graph.");
        }

        [TestMethod]
        public void GetSupportSiteDetail_IncludesActiveDepotSpecializationEffectSummary()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out _, out _);
            var supportSite = territoryManager.GetDepotIndustries()
                .First(industry => industry != null && territoryManager.GetDepotSpecialization(industry) == DepotSpecialization.Support);

            var detail = InvokePrivate<string>(controller, "GetSupportSiteDetail", supportSite);

            StringAssert.Contains(detail, "Support: amplifies district support bonuses and stabilizes licensed territory.");
            StringAssert.Contains(detail, "Staff 2/0/0/1");
        }

        [TestMethod]
        public void BuildCorridorNetworkSecondaryText_IncludesDominantCarrierMetadata()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out var districtB);
            var corridor = territoryManager.GetCorridorState(districtA, districtB);

            corridor.ActiveCarrierCount = 2;
            corridor.DominantCarrierName = "Senora Line";

            var detail = InvokePrivate<string>(controller, "BuildCorridorNetworkSecondaryText", corridor);

            StringAssert.Contains(detail, "Carriers 2");
            StringAssert.Contains(detail, "Lead Senora Line");
        }

        [TestMethod]
        public void BuildDistrictServiceSupportDetail_IncludesDistrictDepotRoleSummary()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out var districtA, out _);
            var district = territoryManager.GetDistrictState(districtA);
            var operations = territoryManager.GetOperationsSummary().Districts.Single(entry => string.Equals(entry.DistrictName, districtA, StringComparison.OrdinalIgnoreCase));

            var detail = InvokePrivate<string>(controller, "BuildDistrictServiceSupportDetail", district, operations);

            StringAssert.Contains(detail, "Support bonus");
            StringAssert.Contains(detail, "Depot roles: Support: amplifies district support bonuses and stabilizes licensed territory.");
        }

        [TestMethod]
        public void BuildMetroAnchorRatios_ReflectsExpectedGeographicRelationships()
        {
            CreateControllerWithScenario(out var territoryManager, out _, out _);

            var districtNames = territoryManager.DistrictStates
                .Where(state => state != null)
                .Select(state => state.DistrictName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var anchors = CompanyMapController.BuildMetroAnchorRatios(districtNames, territoryManager.GetDistrictCentroidsByName());

            Assert.IsTrue(anchors["PaletoBay"].Y < anchors["GrandSenora"].Y);
            Assert.IsTrue(anchors["Grapeseed"].Y < anchors["GrandSenora"].Y);
            Assert.IsTrue(anchors["GrandSenora"].Y < anchors["SouthLosSantos"].Y);
            Assert.IsTrue(anchors["Port"].Y > anchors["Downtown"].Y);
            Assert.IsTrue(anchors["Airport"].Y > anchors["WestLosSantos"].Y);
            Assert.IsTrue(anchors["EastLosSantos"].X > anchors["WestLosSantos"].X);
        }

        [TestMethod]
        public void BuildMetroAnchorRatios_AssignsDeterministicFallbackForUnknownDistricts()
        {
            var anchors = CompanyMapController.BuildMetroAnchorRatios(
                new[] { "Port", "FutureHarbor", "FutureHighlands" },
                new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Port", new PointF(300f, -2400f) },
                });

            Assert.IsTrue(anchors.ContainsKey("FutureHarbor"));
            Assert.IsTrue(anchors.ContainsKey("FutureHighlands"));
            Assert.IsTrue(anchors["FutureHarbor"].X >= 0.04f && anchors["FutureHarbor"].X <= 0.96f);
            Assert.IsTrue(anchors["FutureHarbor"].Y >= 0.04f && anchors["FutureHarbor"].Y <= 0.96f);
            Assert.AreNotEqual(anchors["FutureHarbor"], anchors["FutureHighlands"]);
        }

        [TestMethod]
        public void BuildNetworkLayouts_KeepsNodeCardsInsideGraphBounds()
        {
            var controller = CreateControllerWithScenario(out var territoryManager, out _, out _);
            var districts = territoryManager.DistrictStates
                .Where(state => state != null)
                .OrderBy(state => state.DistrictName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rawLayouts = InvokePrivate<object>(controller, "BuildNetworkLayouts", new Size(1280, 720), districts);
            var layouts = ((System.Collections.IEnumerable)rawLayouts).Cast<object>().ToList();
            var graphBounds = CompanyMapController.GetNetworkGraphBounds(new Size(1280, 720));

            Assert.AreEqual(districts.Count, layouts.Count);
            foreach (var layout in layouts)
            {
                var centerX = GetPropertyValue<float>(layout, "CenterX");
                var centerY = GetPropertyValue<float>(layout, "CenterY");
                var width = GetPropertyValue<float>(layout, "Width");
                var height = GetPropertyValue<float>(layout, "Height");

                Assert.IsTrue(centerX - (width * 0.5f) >= graphBounds.Left - 0.1f);
                Assert.IsTrue(centerX + (width * 0.5f) <= graphBounds.Right + 0.1f);
                Assert.IsTrue(centerY - (height * 0.5f) >= graphBounds.Top - 0.1f);
                Assert.IsTrue(centerY + (height * 0.5f) <= graphBounds.Bottom + 0.1f);
            }
        }

        private static CompanyMapController CreateControllerWithScenario(
            out TerritoryManager territoryManager,
            out string districtA,
            out string districtB,
            Func<RoutePlannerOverlaySnapshot> getRoutePlannerOverlaySnapshot = null)
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var config = ModConfig.Load(configDirectory);
            var industryManager = new IndustryManager(config);
            territoryManager = new TerritoryManager(config, industryManager);

            var supportSites = territoryManager.GetDepotIndustries()
                .Where(industry => industry != null && !industry.IsStarterHeadquarters)
                .GroupBy(industry => industry.DistrictName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(2)
                .ToList();

            Assert.IsTrue(supportSites.Count >= 2, "Expected at least two non-starter support sites in different districts.");

            var inactiveCorridorDistrict = territoryManager.DistrictStates
                .Select(state => state != null ? state.DistrictName : string.Empty)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)
                    && !string.Equals(name, supportSites[0].DistrictName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, supportSites[1].DistrictName, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(string.IsNullOrWhiteSpace(inactiveCorridorDistrict), "Expected a third district for inactive corridor filtering coverage.");

            districtA = supportSites[0].DistrictName;
            districtB = supportSites[1].DistrictName;

            var snapshot = new TerritoryPersistenceSnapshot();
            snapshot.Sites.Add(new TerritorySiteSnapshot
            {
                SiteId = supportSites[0].Id,
                ControlLevel = TerritoryControlLevel.Owned,
                CrewAssigned = true,
                LoaderCount = 2,
                ManagerCount = 1,
                CurrentWeekServiceDeliveries = 4,
                CurrentWeekServiceTons = 18f,
                ServicePenaltySteps = 1,
                DepotSpecialization = DepotSpecialization.Support,
            });
            snapshot.Sites.Add(new TerritorySiteSnapshot
            {
                SiteId = supportSites[1].Id,
                ControlLevel = TerritoryControlLevel.Leased,
                CrewAssigned = true,
                MechanicCount = 1,
                GuardCount = 1,
                CurrentWeekServiceDeliveries = 3,
                CurrentWeekServiceTons = 12f,
                DepotSpecialization = DepotSpecialization.Dispatch,
            });
            snapshot.Districts.Add(new TerritoryDistrictSnapshot
            {
                DistrictName = districtA,
                LicenseStatus = DistrictLicenseStatus.Active,
                CurrentWeekActivityCount = 5,
                CurrentWeekActivityTons = 40f,
                CompetitivePressure = 0.2f,
                CompetitiveOpportunity = 0.1f,
            });
            snapshot.Districts.Add(new TerritoryDistrictSnapshot
            {
                DistrictName = districtB,
                LicenseStatus = DistrictLicenseStatus.Probation,
                CurrentWeekActivityCount = 3,
                CurrentWeekActivityTons = 24f,
                CompetitivePressure = 0.35f,
                CompetitiveOpportunity = 0.08f,
            });
            snapshot.Corridors.Add(new TerritoryCorridorSnapshot
            {
                DistrictA = districtA,
                DistrictB = districtB,
                RightLevel = CorridorRightLevel.Priority,
                DeliveryCount = 12,
                TotalDeliveredTons = 180f,
                CurrentWeekDeliveryCount = 4,
                CurrentWeekDeliveredTons = 28f,
                DecayPressure = 0.1f,
                CompetitivePressure = 0.44f,
                CompetitiveOpportunity = 0.18f,
                ActiveCompetitionJobs = 2,
                VisibleCompetitionCount = 1,
                CompetitiveTons = 24f,
                CompetitiveWinCount = 1,
                ContestedWeekStreak = 2,
            });
            snapshot.Corridors.Add(new TerritoryCorridorSnapshot
            {
                DistrictA = districtA,
                DistrictB = inactiveCorridorDistrict,
                RightLevel = CorridorRightLevel.None,
                DeliveryCount = 2,
                TotalDeliveredTons = 10f,
            });

            territoryManager.ApplySnapshot(snapshot);
            territoryManager.GetDistrictState(districtA).InfluenceRatio = 0.72f;
            territoryManager.GetDistrictState(districtA).ReputationScore = 78f;
            territoryManager.GetDistrictState(districtA).ReputationLabel = "Anchored";
            territoryManager.GetDistrictState(districtB).InfluenceRatio = 0.41f;
            territoryManager.GetDistrictState(districtB).ReputationScore = 39f;
            territoryManager.GetDistrictState(districtB).ReputationLabel = "Emerging";

            return new CompanyMapController(
                new ControlBindings(),
                territoryManager,
                industryManager,
                () => { },
                () => 125000f,
                _ => string.Empty,
                _ => string.Empty,
                _ => string.Empty,
                (_, __) => string.Empty,
                (_, __) => string.Empty,
                _ => { },
                getRoutePlannerOverlaySnapshot);
        }

        private static object InvokeGetNetworkViewSnapshot(CompanyMapController controller)
        {
            var method = typeof(CompanyMapController).GetMethod("GetNetworkViewSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "GetNetworkViewSnapshot");
            return method.Invoke(controller, null);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            return (T)method.Invoke(target, arguments);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            return (T)property.GetValue(target, null);
        }
    }
}