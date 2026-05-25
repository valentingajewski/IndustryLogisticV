using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class TabletStateStoreDirtyRefreshTests
    {
        [TestMethod]
        public void RefreshDirtySnapshotSlices_WhenOnlyBalanceIsDirty_OnlyRebuildsBalanceSlice()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var globalMarket = new GlobalMarketManager(0);

            var counters = new DelegateCounters();
            var profit = 125f;
            var statusBanner = "Stable";
            var controlledDistrictCount = 2;
            var activeCorridorCount = 1;
            var securedSupportSiteCount = 1;
            var nearestIndustry = industryManager.Industries.FirstOrDefault(industry => industry != null);
            Assert.IsNotNull(nearestIndustry, "Expected at least one industry from config.");

            var store = CreateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                globalMarket,
                counters,
                () => profit,
                () => statusBanner,
                () => controlledDistrictCount,
                () => activeCorridorCount,
                () => securedSupportSiteCount,
                () => nearestIndustry);

            var snapshot = InvokePrivate(store, "BuildSnapshot");
            InvokePrivate(store, "ClearSnapshotDirtyState");

            var baselineStatus = GetPropertyValue<string>(snapshot, "StatusBanner");
            var baselineNearestIndustryName = GetPropertyValue<string>(snapshot, "NearestIndustryName");
            var baselineControlledDistrictCount = GetPropertyValue<int>(snapshot, "ControlledDistrictCount");
            var baselineBalanceCalls = counters.BalanceCalls;
            var baselineStatusCalls = counters.StatusBannerCalls;
            var baselineControlledCalls = counters.ControlledDistrictCalls;
            var baselineNearestCalls = counters.NearestIndustryCalls;

            profit = 410f;
            statusBanner = "Changed";
            controlledDistrictCount = 7;
            nearestIndustry = industryManager.Industries.Last(industry => industry != null);

            store.MarkBalanceDirty();
            InvokePrivate(store, "RefreshDirtySnapshotSlices", snapshot);

            Assert.AreEqual(410f, GetPropertyValue<float>(snapshot, "Balance"), 0.01f);
            Assert.AreEqual(baselineStatus, GetPropertyValue<string>(snapshot, "StatusBanner"));
            Assert.AreEqual(baselineNearestIndustryName, GetPropertyValue<string>(snapshot, "NearestIndustryName"));
            Assert.AreEqual(baselineControlledDistrictCount, GetPropertyValue<int>(snapshot, "ControlledDistrictCount"));
            Assert.AreEqual(baselineBalanceCalls + 1, counters.BalanceCalls);
            Assert.AreEqual(baselineStatusCalls, counters.StatusBannerCalls);
            Assert.AreEqual(baselineControlledCalls, counters.ControlledDistrictCalls);
            Assert.AreEqual(baselineNearestCalls, counters.NearestIndustryCalls);
        }

        [TestMethod]
        public void RefreshDirtySnapshotSlices_WhenNearestIndustryIsDirty_ReusesSharedContextAndRefreshesNearestAndMarketSlices()
        {
            var config = ModConfig.Load(Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config"));
            var industryManager = new IndustryManager(config);
            var fleetManager = new FleetManager(config);
            var fuelSystem = new VehicleFuelSystem(fleetManager, null);
            var pair = FindIndustriesWithDistinctOutputCommodities(industryManager);
            var globalMarket = new GlobalMarketManager(0, new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [pair.CommodityA] = 10000f,
                [pair.CommodityB] = 10000f,
            });
            globalMarket.SetTemporaryDemandShock("nearest-dirty", pair.CommodityA, 9f);

            var counters = new DelegateCounters();
            var profit = 250f;
            var statusBanner = "Nominal";
            var controlledDistrictCount = 3;
            var activeCorridorCount = 2;
            var securedSupportSiteCount = 2;
            var nearestIndustry = pair.IndustryA;

            var store = CreateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                globalMarket,
                counters,
                () => profit,
                () => statusBanner,
                () => controlledDistrictCount,
                () => activeCorridorCount,
                () => securedSupportSiteCount,
                () => nearestIndustry);

            var snapshot = InvokePrivate(store, "BuildSnapshot");
            InvokePrivate(store, "ClearSnapshotDirtyState");

            var baselineBalance = GetPropertyValue<float>(snapshot, "Balance");
            var baselineStatus = GetPropertyValue<string>(snapshot, "StatusBanner");
            var baselineNearestCalls = counters.NearestIndustryCalls;
            var baselineBalanceCalls = counters.BalanceCalls;
            var baselineStatusCalls = counters.StatusBannerCalls;

            Assert.AreEqual(pair.CommodityA, GetFirstHighlightCommodity(snapshot));

            globalMarket.ClearTemporaryDemandShock("nearest-dirty");
            globalMarket.SetTemporaryDemandShock("nearest-dirty", pair.CommodityB, 9f);
            nearestIndustry = pair.IndustryB;
            profit = 999f;
            statusBanner = "Should stay unchanged";

            store.MarkNearestIndustryDirty();
            InvokePrivate(store, "RefreshDirtySnapshotSlices", snapshot);

            Assert.AreEqual(pair.IndustryB.Name, GetPropertyValue<string>(snapshot, "NearestIndustryName"));
            Assert.AreEqual(pair.CommodityB, GetFirstHighlightCommodity(snapshot));
            Assert.AreEqual(baselineBalance, GetPropertyValue<float>(snapshot, "Balance"), 0.01f);
            Assert.AreEqual(baselineStatus, GetPropertyValue<string>(snapshot, "StatusBanner"));
            Assert.AreEqual(baselineNearestCalls + 1, counters.NearestIndustryCalls, "Nearest industry context should be created once and reused across nearest-industry and market slice refreshes.");
            Assert.AreEqual(baselineBalanceCalls, counters.BalanceCalls);
            Assert.AreEqual(baselineStatusCalls, counters.StatusBannerCalls);
        }

        private static TabletStateStore CreateStore(
            IndustryManager industryManager,
            FleetManager fleetManager,
            VehicleFuelSystem fuelSystem,
            GlobalMarketManager globalMarket,
            DelegateCounters counters,
            Func<float> getProfit,
            Func<string> getStatusBanner,
            Func<int> getControlledDistrictCount,
            Func<int> getActiveCorridorCount,
            Func<int> getSecuredSupportSiteCount,
            Func<Industry> getNearestIndustry)
        {
            return new TabletStateStore(
                industryManager,
                fleetManager,
                fuelSystem,
                globalMarket,
                null,
                null,
                null,
                null,
                null,
                null,
                () => 0,
                null,
                () =>
                {
                    counters.NearestIndustryCalls += 1;
                    return getNearestIndustry();
                },
                () =>
                {
                    counters.BalanceCalls += 1;
                    return getProfit();
                },
                () => VehicleCargoType.Aggregates,
                _ => Vector3.Zero,
                () => false,
                () =>
                {
                    counters.StatusBannerCalls += 1;
                    return getStatusBanner();
                },
                () =>
                {
                    counters.ControlledDistrictCalls += 1;
                    return getControlledDistrictCount();
                },
                () =>
                {
                    counters.ActiveCorridorCalls += 1;
                    return getActiveCorridorCount();
                },
                () =>
                {
                    counters.SecuredSupportSiteCalls += 1;
                    return getSecuredSupportSiteCount();
                },
                null,
                null,
                null);
        }

        private static (Industry IndustryA, string CommodityA, Industry IndustryB, string CommodityB) FindIndustriesWithDistinctOutputCommodities(IndustryManager industryManager)
        {
            var candidates = industryManager.Industries
                .Where(industry => industry != null && industry.GetSortedOutputs().Count > 0)
                .Select(industry => new
                {
                    Industry = industry,
                    Commodity = industry.GetSortedOutputs()[0],
                })
                .GroupBy(candidate => candidate.Commodity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(2)
                .ToList();

            Assert.IsTrue(candidates.Count >= 2, "Expected at least two industries with distinct output commodities.");
            return (candidates[0].Industry, candidates[0].Commodity, candidates[1].Industry, candidates[1].Commodity);
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            return method.Invoke(target, arguments);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            return (T)property.GetValue(target, null);
        }

        private static string GetFirstHighlightCommodity(object snapshot)
        {
            var highlights = GetPropertyValue<IEnumerable>(snapshot, "MarketHighlights");
            foreach (var highlight in highlights)
            {
                return GetPropertyValue<string>(highlight, "Commodity");
            }

            Assert.Fail("Expected at least one market highlight.");
            return string.Empty;
        }

        private sealed class DelegateCounters
        {
            public int BalanceCalls { get; set; }
            public int StatusBannerCalls { get; set; }
            public int ControlledDistrictCalls { get; set; }
            public int ActiveCorridorCalls { get; set; }
            public int SecuredSupportSiteCalls { get; set; }
            public int NearestIndustryCalls { get; set; }
        }
    }
}