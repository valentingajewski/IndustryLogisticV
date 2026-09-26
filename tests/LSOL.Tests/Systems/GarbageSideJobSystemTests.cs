using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GTA;
using GTA.Math;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class GarbageSideJobSystemTests
    {
        private static readonly string[] ExpectedRouteIds =
        {
            "DelPerro",
            "GroveStreet",
            "MirrorPark",
            "PaletoBay",
            "SandyShores",
            "Vespucci",
            "Vinewood",
        };

        private static readonly string[] ExpectedRouteNames =
        {
            "West Los Santos | Del Perro",
            "South Los Santos | Grove Street",
            "East Los Santos | Mirror Park",
            "Paleto Bay",
            "Grand Senora | Sandy Shores",
            "West Los Santos | Vespucci",
            "Vinewood",
        };

        private static readonly string[] ExpectedDistrictNames =
        {
            "WestLosSantos",
            "SouthLosSantos",
            "EastLosSantos",
            "PaletoBay",
            "GrandSenora",
            "WestLosSantos",
            "Vinewood",
        };

        private static readonly int[] ExpectedBagCounts = { 53, 42, 47, 38, 33, 50, 50 };

        private static readonly int[] ExpectedStopCounts = { 12, 8, 14, 11, 9, 14, 15 };

        [TestMethod]
        public void LoadRoutes_ParsesEveryRouteWithStopsAndBags()
        {
            var routes = GarbageSideJobSystem.LoadRoutes(GetConfigDirectory());

            Assert.AreEqual(ExpectedRouteIds.Length, routes.Count);
            for (int i = 0; i < ExpectedRouteIds.Length; i++)
            {
                var route = routes[i];
                Assert.AreEqual(ExpectedRouteIds[i], route.RouteId);
                Assert.AreEqual(ExpectedRouteNames[i], route.DisplayName);
                Assert.AreEqual(ExpectedDistrictNames[i], route.DistrictName, "district for {0}", route.RouteId);
                Assert.AreEqual(ExpectedStopCounts[i], route.StopCount, "stop count for {0}", route.RouteId);
                Assert.AreEqual(ExpectedBagCounts[i], route.TotalBags, "bag count for {0}", route.RouteId);
                Assert.IsTrue(route.Stops.All(stop => stop != null), "route {0} must not contain null stops", route.RouteId);
                Assert.IsFalse(route.Stops.Any(stop => stop.BagCount == 0), "route {0} must not contain empty stops", route.RouteId);
            }
        }

        [TestMethod]
        public void LoadRoutes_EveryRouteDistrictMatchesTheDistrictPrefixOfItsName()
        {
            var routes = GarbageSideJobSystem.LoadRoutes(GetConfigDirectory());

            Assert.AreEqual(ExpectedRouteIds.Length, routes.Count);
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(route.DistrictName), "route {0} must declare a district", route.RouteId);

                var districtLabel = ModFormatting.FormatDistrictName(route.DistrictName);
                var separatorIndex = route.DisplayName.IndexOf(" | ", StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    // A route whose district and name describe the same place keeps the bare name.
                    Assert.AreEqual(districtLabel, route.DisplayName, "route {0} must be named after its district", route.RouteId);
                    continue;
                }

                Assert.IsTrue(
                    route.DisplayName.Length > separatorIndex + 3,
                    "route {0} display name must keep the original route name after the separator",
                    route.RouteId);

                // The district tag and the name prefix must describe the same district:
                // "West Los Santos | Del Perro" pairs with district="WestLosSantos".
                Assert.AreEqual(districtLabel, route.DisplayName.Substring(0, separatorIndex), "district tag for {0}", route.RouteId);
            }
        }

        [TestMethod]
        public void LoadGarbageTrucks_ParsesBothTrashTrucks()
        {
            var trucks = GarbageSideJobSystem.LoadGarbageTrucks(GetConfigDirectory());

            Assert.AreEqual(2, trucks.Count);
            Assert.AreEqual("trash", trucks[0].ModelName);
            Assert.AreEqual(0, trucks[0].UnlockLevel);
            Assert.AreEqual(10f, trucks[0].CapacityTons, 0.001f);
            Assert.AreEqual("trash2", trucks[1].ModelName);
            Assert.AreEqual(20, trucks[1].UnlockLevel);
            Assert.AreEqual(20f, trucks[1].CapacityTons, 0.001f);
            Assert.IsTrue(trucks.All(truck => truck.Price >= 0f));
            Assert.IsTrue(trucks.All(truck => truck.DailyRent >= 0f));
        }

        [TestMethod]
        public void ReadVector3_ReadsAttributeFormCoordinates()
        {
            var document = XDocument.Load(Path.Combine(GetConfigDirectory(), "SideJobs", "GarbageRoute.xml"));
            var firstBag = document
                .Descendants("GarbageRoute")
                .First(route => string.Equals(route.Attribute("id").Value, "DelPerro", StringComparison.Ordinal))
                .Descendants("Vector3")
                .First();

            var position = GarbageSideJobSystem.ReadVector3(firstBag);

            Assert.AreEqual(-1003.40f, position.X, 0.001f);
            Assert.AreEqual(-796.96f, position.Y, 0.001f);
            Assert.AreEqual(16.17f, position.Z, 0.001f);
        }

        [TestMethod]
        public void ReadVector3_MissingAttributes_ReturnsZero()
        {
            var element = XElement.Parse("<Vector3 X=\"12.5\" />");

            var position = GarbageSideJobSystem.ReadVector3(element);

            Assert.AreEqual(12.5f, position.X, 0.001f);
            Assert.AreEqual(0f, position.Y, 0.001f);
            Assert.AreEqual(0f, position.Z, 0.001f);
        }

        [TestMethod]
        public void ComputeCentroid_AveragesEveryBagPosition()
        {
            var positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 4f, 6f),
                new Vector3(4f, 2f, 3f),
            };

            var centroid = GarbageSideJobSystem.ComputeCentroid(positions);

            Assert.AreEqual(2f, centroid.X, 0.001f);
            Assert.AreEqual(2f, centroid.Y, 0.001f);
            Assert.AreEqual(3f, centroid.Z, 0.001f);
        }

        [TestMethod]
        public void ComputeCentroid_EmptyInput_ReturnsZero()
        {
            Assert.AreEqual(Vector3.Zero, GarbageSideJobSystem.ComputeCentroid(new List<Vector3>()));
            Assert.AreEqual(Vector3.Zero, GarbageSideJobSystem.ComputeCentroid(null));
        }

        [TestMethod]
        public void ComputeCentroid_MatchesFirstStopOfDelPerro()
        {
            var routes = GarbageSideJobSystem.LoadRoutes(GetConfigDirectory());
            var delPerro = routes.First(route => route.RouteId == "DelPerro");
            var bags = delPerro.Stops[0].BagPositions;

            var centroid = GarbageSideJobSystem.ComputeCentroid(bags);

            var expectedX = bags.Average(bag => (double)bag.X);
            var expectedY = bags.Average(bag => (double)bag.Y);
            var expectedZ = bags.Average(bag => (double)bag.Z);
            Assert.AreEqual(expectedX, centroid.X, 0.001f);
            Assert.AreEqual(expectedY, centroid.Y, 0.001f);
            Assert.AreEqual(expectedZ, centroid.Z, 0.001f);
        }

        [TestMethod]
        public void ComputeTipCash_CombinesHandlingTippingAndSkillBonus()
        {
            // 10 bags at 120 each + 2 t at 150 = 1500, no bonus.
            Assert.AreEqual(1500f, GarbageSideJobSystem.ComputeTipCash(10, 2f, 1f), 0.001f);

            // A skill bonus scales the gross payout (rounded to whole currency units).
            Assert.AreEqual(1650f, GarbageSideJobSystem.ComputeTipCash(10, 2f, 1.1f), 0.001f);
            Assert.AreEqual(1559f, GarbageSideJobSystem.ComputeTipCash(10, 1.9f, 1.05f), 0.001f);

            // Guards against invalid input.
            Assert.AreEqual(0f, GarbageSideJobSystem.ComputeTipCash(0, 0f, 1f), 0.001f);
            Assert.AreEqual(1500f, GarbageSideJobSystem.ComputeTipCash(10, 2f, 0f), 0.001f);
            Assert.AreEqual(0f, GarbageSideJobSystem.ComputeTipCash(-5, -2f, 1f), 0.001f);
        }

        [TestMethod]
        public void ComputeTipCash_TypicalRoutePayout_MatchesDesignTarget()
        {
            // Del Perro is the largest route: 53 bags. At the average bag weight (0.2 t) the
            // full run should land on the documented ~10,250 cash target.
            const int bags = 53;
            var averageWeight = (GarbageSideJobSystem.BagWeightMin + GarbageSideJobSystem.BagWeightMax) * 0.5f;
            var averageTotal = GarbageSideJobSystem.ComputeTipCash(bags, bags * averageWeight, 1f)
                + GarbageSideJobSystem.RouteCompletionBonus
                + GarbageSideJobSystem.PerfectRouteBonus;

            Assert.AreEqual(10250f, averageTotal, 0.5f);

            var minWeightTotal = GarbageSideJobSystem.ComputeTipCash(bags, bags * GarbageSideJobSystem.BagWeightMin, 1f)
                + GarbageSideJobSystem.RouteCompletionBonus
                + GarbageSideJobSystem.PerfectRouteBonus;
            var maxWeightTotal = GarbageSideJobSystem.ComputeTipCash(bags, bags * GarbageSideJobSystem.BagWeightMax, 1f)
                + GarbageSideJobSystem.RouteCompletionBonus
                + GarbageSideJobSystem.PerfectRouteBonus;

            Assert.AreEqual(9455f, minWeightTotal, 0.5f);
            Assert.AreEqual(11045f, maxWeightTotal, 0.5f);
            Assert.IsTrue(minWeightTotal >= 8000f, "expected a full route to clear 8,000 cash");
            Assert.IsTrue(maxWeightTotal <= 11500f, "expected a full route to stay below 11,500 cash");
        }

        [TestMethod]
        public void CanLoadBag_RefusesBagThatExceedsCapacity()
        {
            // Fits exactly.
            Assert.IsTrue(GarbageSideJobSystem.CanLoadBag(9.7f, 0.3f, 10f));
            Assert.IsTrue(GarbageSideJobSystem.CanLoadBag(0f, 0.1f, 10f));

            // Would exceed the truck capacity and must be refused.
            Assert.IsFalse(GarbageSideJobSystem.CanLoadBag(9.8f, 0.3f, 10f));
            Assert.IsFalse(GarbageSideJobSystem.CanLoadBag(10f, 0.1f, 10f));
            Assert.IsFalse(GarbageSideJobSystem.CanLoadBag(19.9f, 0.3f, 20f));
        }

        [TestMethod]
        public void RollBagWeight_AlwaysLandsInsideConfiguredBand()
        {
            var random = new Random(20260915);
            var sawMinimumSide = false;
            var sawMaximumSide = false;

            for (int i = 0; i < 2000; i++)
            {
                var weight = GarbageSideJobSystem.RollBagWeight(random);
                Assert.IsTrue(weight >= GarbageSideJobSystem.BagWeightMin, "weight {0} fell below the minimum", weight);
                Assert.IsTrue(weight <= GarbageSideJobSystem.BagWeightMax, "weight {0} rose above the maximum", weight);

                if (weight < GarbageSideJobSystem.BagWeightMin + 0.05f)
                {
                    sawMinimumSide = true;
                }

                if (weight > GarbageSideJobSystem.BagWeightMax - 0.05f)
                {
                    sawMaximumSide = true;
                }
            }

            Assert.IsTrue(sawMinimumSide && sawMaximumSide, "weights should spread across the band");
        }

        [TestMethod]
        public void ClampBagWeight_KeepsPersistedWeightsInsideTheBand()
        {
            Assert.AreEqual(0.1f, GarbageSideJobSystem.ClampBagWeight(0f), 0.0001f);
            Assert.AreEqual(0.1f, GarbageSideJobSystem.ClampBagWeight(-1f), 0.0001f);
            Assert.AreEqual(0.3f, GarbageSideJobSystem.ClampBagWeight(5f), 0.0001f);
            Assert.AreEqual(0.23f, GarbageSideJobSystem.ClampBagWeight(0.23f), 0.0001f);
        }

        [TestMethod]
        public void GarbageSnapshot_WithoutRouteOrBags_HasNoData()
        {
            var snapshot = new GarbagePersistenceSnapshot
            {
                ActiveTruckModelName = "trash",
            };

            Assert.IsFalse(snapshot.HasData);

            snapshot.RouteId = "DelPerro";
            Assert.IsTrue(snapshot.HasData);

            snapshot.RouteId = string.Empty;
            snapshot.OnBoardBags = 1;
            Assert.IsTrue(snapshot.HasData);

            snapshot.OnBoardBags = 0;
            snapshot.Bags.Add(new GarbagePendingBagSnapshot { SpawnId = 1, StopIndex = 0, WeightTons = 0.2f });
            Assert.IsTrue(snapshot.HasData);

            // A truck parked in the job garage is saved on its own, with no route running.
            var garageOnly = new GarbagePersistenceSnapshot();
            garageOnly.OwnedTruckModels.Add("trash");
            Assert.IsTrue(garageOnly.HasData);
        }

        [TestMethod]
        public void TrashBagBlips_UseClassicYellowOnMissionSprite()
        {
            // radar_on_mission is exposed by SHVDN as BlipSprite.OnMission; the trash bag blips
            // must not reuse the depot's garbage truck sprite.
            Assert.AreEqual(271, (int)BlipSprite.OnMission);
            Assert.AreNotEqual(BlipSprite.OnMission, BlipSprite.GarbageTruck);
        }

        [TestMethod]
        public void Persistence_RoundTrip_RestoresGarbageSnapshotAndBagEntries()
        {
            var filePath = TestWorkspace.CreateTempFilePath("garbage.state.xml");

            try
            {
                var garbage = new GarbagePersistenceSnapshot
                {
                    RouteId = "DelPerro",
                    NextBagSpawnId = 12,
                    OnBoardBags = 3,
                    OnBoardTons = 0.66f,
                    RouteBagsCollected = 9,
                    ActiveTruckModelName = "trash",
                };
                garbage.Bags.Add(new GarbagePendingBagSnapshot
                {
                    SpawnId = 4,
                    StopIndex = 1,
                    Position = new Vector3(-1089.80f, -726.44f, 19.40f),
                    WeightTons = 0.22f,
                });
                garbage.Bags.Add(new GarbagePendingBagSnapshot
                {
                    SpawnId = 7,
                    StopIndex = 3,
                    Position = new Vector3(-1003.40f, -796.96f, 16.17f),
                    WeightTons = 0.17f,
                });
                garbage.OwnedTruckModels.Add("trash");
                var metadata = new IndustryPersistenceMetadata { Garbage = garbage };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Section name=\"Garbage\">");
                StringAssert.Contains(rawSave, "<Section name=\"Garbage:Bag:4\">");
                StringAssert.Contains(rawSave, "<Section name=\"Garbage:Bag:7\">");
                StringAssert.Contains(rawSave, "<Value key=\"Version\">31</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"RouteId\">DelPerro</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"ActiveTruckModelName\">trash</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"OwnedTrucks\">trash</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var restored = result.Metadata.Garbage;

                Assert.IsNotNull(restored);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.AreEqual("DelPerro", restored.RouteId);
                Assert.AreEqual(12, restored.NextBagSpawnId);
                Assert.AreEqual(3, restored.OnBoardBags);
                Assert.AreEqual(0.66f, restored.OnBoardTons, 0.001f);
                Assert.AreEqual(9, restored.RouteBagsCollected);
                Assert.AreEqual("trash", restored.ActiveTruckModelName);
                Assert.AreEqual(1, restored.OwnedTruckModels.Count);
                Assert.AreEqual("trash", restored.OwnedTruckModels[0]);
                Assert.AreEqual(2, restored.Bags.Count);
                Assert.AreEqual(4, restored.Bags[0].SpawnId);
                Assert.AreEqual(1, restored.Bags[0].StopIndex);
                Assert.AreEqual(-1089.80f, restored.Bags[0].Position.X, 0.001f);
                Assert.AreEqual(-726.44f, restored.Bags[0].Position.Y, 0.001f);
                Assert.AreEqual(19.40f, restored.Bags[0].Position.Z, 0.001f);
                Assert.AreEqual(0.22f, restored.Bags[0].WeightTons, 0.001f);
                Assert.IsFalse(restored.Bags[0].Collected);
                Assert.AreEqual(7, restored.Bags[1].SpawnId);
                Assert.AreEqual(3, restored.Bags[1].StopIndex);
                Assert.AreEqual(0.17f, restored.Bags[1].WeightTons, 0.001f);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        [TestMethod]
        public void Persistence_WithoutGarbageSection_ReturnsNullSnapshot()
        {
            var filePath = TestWorkspace.CreateTempFilePath("no-garbage.state.xml");

            try
            {
                IndustryPersistenceManager.Save(
                    filePath,
                    Array.Empty<Industry>(),
                    new IndustryPersistenceMetadata { Profit = 1000f },
                    null);

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());

                Assert.IsNull(result.Metadata.Garbage);
            }
            finally
            {
                DeleteTempDirectory(filePath);
            }
        }

        private static string GetConfigDirectory()
        {
            return Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
        }

        private static void DeleteTempDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
