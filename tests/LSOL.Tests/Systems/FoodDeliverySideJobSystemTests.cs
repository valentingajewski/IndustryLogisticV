using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class FoodDeliverySideJobSystemTests
    {
        private const int ExpectedVehicleCount = 2;
        private const int ExpectedRestaurantSiteCount = 8;

        // ---------------------------------------------------------------- config

        [TestMethod]
        public void LoadVehicles_ReadsTheShippedDeliveryVehicles()
        {
            var vehicles = FoodDeliverySideJobSystem.LoadVehicles(GetConfigDirectory());

            Assert.AreEqual(ExpectedVehicleCount, vehicles.Count);

            Assert.AreEqual("pizzaboy", vehicles[0].ModelName);
            Assert.AreEqual("Delivery Scooter", vehicles[0].Name);
            Assert.AreEqual(5, vehicles[0].MealCapacity);
            Assert.AreEqual(0, vehicles[0].UnlockLevel);
            Assert.AreEqual(3000f, vehicles[0].Price, 0.001f);
            Assert.AreEqual(30f, vehicles[0].FuelCapacityLiters, 0.001f);

            Assert.AreEqual("taco", vehicles[1].ModelName);
            Assert.AreEqual(40, vehicles[1].MealCapacity);
            Assert.AreEqual(10, vehicles[1].UnlockLevel);
            Assert.AreEqual(23000f, vehicles[1].Price, 0.001f);
        }

        [TestMethod]
        public void LoadVehicles_IgnoresEntriesOfOtherJobTypes()
        {
            var configDirectory = CreateTempConfigDirectory(
                "<JobCoordinates />",
                "<JobVehicles>\n" +
                "  <JobVehicle id=\"1\" name=\"Tow\" model=\"towtruck\" type=\"TowTruck\" price=\"1\" towCapacityTons=\"3\" />\n" +
                "  <JobVehicle id=\"6\" name=\"Depot Bus\" model=\"bus\" type=\"Bus\" seats=\"4\" />\n" +
                "  <JobVehicle id=\"9\" name=\"Scooter\" model=\"pizzaboy\" type=\"FoodDelivery\" mealCapacity=\"5\" />\n" +
                "</JobVehicles>");

            try
            {
                var vehicles = FoodDeliverySideJobSystem.LoadVehicles(configDirectory);

                Assert.AreEqual(1, vehicles.Count);
                Assert.AreEqual("pizzaboy", vehicles[0].ModelName);
                Assert.AreEqual(5, vehicles[0].MealCapacity);
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        [TestMethod]
        public void LoadVehicles_DefaultsTheMealCapacityWhenTheAttributeIsMissing()
        {
            var configDirectory = CreateTempConfigDirectory(
                "<JobCoordinates />",
                "<JobVehicles>\n" +
                "  <JobVehicle id=\"9\" name=\"Scooter\" model=\"pizzaboy\" type=\"FoodDelivery\" />\n" +
                "</JobVehicles>");

            try
            {
                var vehicles = FoodDeliverySideJobSystem.LoadVehicles(configDirectory);

                Assert.AreEqual(1, vehicles.Count);
                Assert.AreEqual(5, vehicles[0].MealCapacity, "the scooter-sized default keeps a broken config playable");
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        [TestMethod]
        public void GetRestaurants_ReflectsTheCatalogProvidedByTheScriptLayer()
        {
            // Restaurants are ordinary Sites.xml industries, so the job only ever sees what the script
            // layer resolves from the industry catalog: there is no per-restaurant entry in the side
            // job XML any more.
            var system = CreateSystem(GetConfigDirectory());

            Assert.AreEqual(1, system.GetRestaurants().Count);
            Assert.AreEqual("TestRestaurant", system.GetGarageRestaurant().SiteKey);

            system.RefreshRestaurants();
            Assert.AreEqual(1, system.GetRestaurants().Count);

            var empty = CreateSystem(GetConfigDirectory(), restaurants: new List<FoodDeliveryRestaurantInfo>());
            Assert.AreEqual(0, empty.GetRestaurants().Count);
            Assert.IsNull(empty.GetGarageRestaurant());
        }

        [TestMethod]
        public void RefreshRestaurants_WithAFailingCatalogDelegate_DoesNotThrow()
        {
            var system = new FoodDeliverySideJobSystem(
                GetConfigDirectory(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                () => { throw new InvalidOperationException("catalog unavailable"); },
                null,
                null,
                new Random(20260918));

            system.RefreshRestaurants();

            Assert.AreEqual(0, system.GetRestaurants().Count);
        }

        [TestMethod]
        public void LoadCustomers_ReadsTheShippedAddressPool()
        {
            var customers = FoodDeliverySideJobSystem.LoadCustomers(GetConfigDirectory());

            // The pool size is whatever the authors wrote: JobCoordinates.xml is the only authority, so
            // adding drop-offs must never require touching this test.
            Assert.AreEqual(CountAuthoredDropoffs(), customers.Count);
            Assert.IsTrue(customers.Count > 0, "the shipped pool must not be empty");
            Assert.IsTrue(customers.All(customer => customer.Position != Vector3.Zero));
            Assert.IsTrue(customers.All(customer => !string.IsNullOrWhiteSpace(customer.Name)));
            Assert.IsTrue(customers.All(customer => !string.IsNullOrWhiteSpace(customer.DistrictName)));
        }

        [TestMethod]
        public void LoadCustomers_RequiresCoordinates()
        {
            var configDirectory = CreateTempConfigDirectory(
                "<JobCoordinates>\n" +
                "  <JobPoint id=\"200\" name=\"No coords\" job=\"FoodDelivery\" function=\"CustomerDropoff\" district=\"Downtown\" />\n" +
                "  <JobPoint id=\"201\" name=\"Zero\" job=\"FoodDelivery\" function=\"CustomerDropoff\" x=\"0\" y=\"0\" z=\"0\" />\n" +
                "  <JobPoint id=\"202\" name=\"Real\" job=\"FoodDelivery\" function=\"CustomerDropoff\" x=\"10\" y=\"20\" z=\"30\" />\n" +
                "</JobCoordinates>");

            try
            {
                var customers = FoodDeliverySideJobSystem.LoadCustomers(configDirectory);

                Assert.AreEqual(1, customers.Count);
                Assert.AreEqual("Real", customers[0].Name);
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        // ---------------------------------------------------------------- shipped-config invariants

        [TestMethod]
        public void ShippedRestaurantSites_ProduceExactlyOneFinishedGoodFromProcessedFoodAndMeat()
        {
            var sites = ReadRestaurantSites();

            Assert.AreEqual(
                ExpectedRestaurantSiteCount,
                sites.Count,
                "the shipped catalog is expected to hold this many restaurants");

            foreach (var site in sites)
            {
                var key = (string)site.Attribute("legacyKey");
                var inputs = site.Element("Inputs");
                Assert.IsNotNull(inputs, key + " has no Inputs element");

                var products = SplitCsv((string)inputs.Attribute("outputs"));
                Assert.AreEqual(1, products.Count, key + " must produce exactly one finished good, not a union");
                Assert.IsTrue(
                    string.Equals(products[0], "Pizza", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(products[0], "Burger", StringComparison.OrdinalIgnoreCase),
                    key + " must produce Pizza or Burger");

                var primary = SplitCsv((string)inputs.Attribute("primary"));
                CollectionAssert.Contains(primary, "ProcessedFood", key + " must eat ProcessedFood");
                CollectionAssert.Contains(primary, "Meat", key + " must eat Meat");
            }
        }

        [TestMethod]
        public void ShippedRestaurantSites_AreInteractableIndustriesWithADeliveryGarage()
        {
            // The site marker is the only interaction point, so a restaurant without one is
            // unreachable, and without a VehicleSpawn the delivery garage has nowhere to park.
            foreach (var site in ReadRestaurantSites())
            {
                var key = (string)site.Attribute("legacyKey");

                Assert.IsNotNull(site.Element("Marker"), key + " has no Marker");
                Assert.IsNotNull(site.Element("VehicleSpawn"), key + " has no VehicleSpawn for the delivery garage");
            }
        }

        [TestMethod]
        public void ShippedRestaurantSites_UseDistrictsThatExistInDistrictsXml()
        {
            var districts = ReadDistrictNames();

            foreach (var site in ReadRestaurantSites())
            {
                var key = (string)site.Attribute("legacyKey");
                var district = (string)site.Attribute("district");

                Assert.IsFalse(string.IsNullOrWhiteSpace(district), key + " has no district");
                CollectionAssert.Contains(districts, district, key + " references an unknown district");
            }
        }

        [TestMethod]
        public void ShippedCustomers_UseDistrictsThatExistInDistrictsXml()
        {
            var customers = FoodDeliverySideJobSystem.LoadCustomers(GetConfigDirectory());
            var districts = ReadDistrictNames();

            foreach (var customer in customers)
            {
                CollectionAssert.Contains(
                    districts,
                    customer.DistrictName,
                    customer.Name + " references an unknown district");
            }
        }

        [TestMethod]
        public void ShippedCustomers_ResolveToRoutesThatHoldEveryAuthoredAddress()
        {
            var customers = FoodDeliverySideJobSystem.LoadCustomers(GetConfigDirectory());
            var routes = FoodDeliverySideJobSystem.BuildRoutes(customers);

            Assert.IsTrue(routes.Count > 0, "the shipped pool must define at least one route");
            Assert.AreEqual(
                customers.Count,
                routes.Sum(route => route.StopCount),
                "every authored address must belong to exactly one route");

            foreach (var route in routes)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(route.DistrictName), "a route without a district cannot be credited");
                Assert.IsTrue(route.StopCount > 0, route.DistrictName + " has no address");
            }
        }

        [TestMethod]
        public void ShippedCustomers_KeepTheirAuthoredCoordinates()
        {
            // The marker, the blip and the GPS are drawn on the authored coordinate: nothing may snap or
            // regenerate it.
            var document = XDocument.Load(Path.Combine(GetConfigDirectory(), "SideJobs", "JobCoordinates.xml"));
            var authored = document.Root
                .Elements("JobPoint")
                .Where(point => string.Equals(
                    (string)point.Attribute("function"),
                    "CustomerDropoff",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var customers = FoodDeliverySideJobSystem.LoadCustomers(GetConfigDirectory());

            Assert.AreEqual(authored.Count, customers.Count, "every authored drop-off must load");

            foreach (var customer in customers)
            {
                var point = authored.First(item => (int)item.Attribute("id") == customer.Id);

                Assert.AreEqual((float)point.Attribute("x"), customer.Position.X, 0.001f);
                Assert.AreEqual((float)point.Attribute("y"), customer.Position.Y, 0.001f);
                Assert.AreEqual((float)point.Attribute("z"), customer.Position.Z, 0.001f);
            }
        }

        [TestMethod]
        public void ShippedDeliveryScooter_CarriesFiveMeals()
        {
            var vehicles = FoodDeliverySideJobSystem.LoadVehicles(GetConfigDirectory());
            var scooter = vehicles.First(vehicle => vehicle.ModelName == "pizzaboy");

            Assert.AreEqual(5, scooter.MealCapacity, "the run cap is the vehicle's mealCapacity, never a code constant");
        }

        // ---------------------------------------------------------------- fare maths

        [TestMethod]
        public void ComputeMealFare_UsesTheProductBasePriceSharePlusDistance()
        {
            // 1200 base price * 0.005 = 6, plus 1 km * 30 = 30.
            var fare = FoodDeliverySideJobSystem.ComputeMealFare(1200f, 1f, 1f, 1f);

            Assert.AreEqual(36f, fare, 0.001f);
        }

        [TestMethod]
        public void ComputeMealFare_ScalesWithMealsAndTheMultiplier()
        {
            var single = FoodDeliverySideJobSystem.ComputeMealFare(1200f, 1f, 1f, 1f);
            var threeMeals = FoodDeliverySideJobSystem.ComputeMealFare(1200f, 1f, 3f, 1f);
            var boosted = FoodDeliverySideJobSystem.ComputeMealFare(1200f, 1f, 1f, 1.5f);

            Assert.AreEqual(single * 3f, threeMeals, 0.001f);
            Assert.AreEqual(single * 1.5f, boosted, 0.001f);
        }

        [TestMethod]
        public void ComputeMealFare_FallsBackToTheDefaultBasePrice()
        {
            var fare = FoodDeliverySideJobSystem.ComputeMealFare(0f, 0f, 1f, 1f);

            Assert.AreEqual(
                FoodDeliverySideJobSystem.DefaultProductBasePrice * FoodDeliverySideJobSystem.MealValueFactor,
                fare,
                0.001f);
        }

        [TestMethod]
        public void ComputeMealFare_NeverGoesNegative()
        {
            // A zero-distance delivery still pays the product's base-price share, never less.
            var baseShare = FoodDeliverySideJobSystem.DefaultProductBasePrice * FoodDeliverySideJobSystem.MealValueFactor;

            Assert.AreEqual(baseShare, FoodDeliverySideJobSystem.ComputeMealFare(0f, -5f, 1f, 1f), 0.001f);
            Assert.AreEqual(0f, FoodDeliverySideJobSystem.ComputeMealFare(1200f, 1f, 0f, 1f), 0.001f);
        }

        [TestMethod]
        public void ComputeMealFare_TreatsANonPositiveMultiplierAsNeutral()
        {
            var neutral = FoodDeliverySideJobSystem.ComputeMealFare(1200f, 1f, 1f, 0f);

            Assert.AreEqual(FoodDeliverySideJobSystem.ComputeMealFare(1200f, 1f, 1f, 1f), neutral, 0.001f);
        }

        // ---------------------------------------------------------------- ingredient maths

        [TestMethod]
        public void GetIngredientTons_SplitsProcessedFoodAndMeat()
        {
            float processedFood;
            float meat;
            var total = FoodDeliverySideJobSystem.GetIngredientTons(5, out processedFood, out meat);

            Assert.AreEqual(5f * FoodDeliverySideJobSystem.IngredientProcessedFoodPerMeal, processedFood, 0.000001f);
            Assert.AreEqual(5f * FoodDeliverySideJobSystem.IngredientMeatPerMeal, meat, 0.000001f);
            Assert.AreEqual(processedFood + meat, total, 0.000001f);
            Assert.AreEqual(0.025f, total, 0.000001f, "a full five meal load is 5 kg per meal");
        }

        [TestMethod]
        public void GetIngredientTons_OfNothingIsNothing()
        {
            float processedFood;
            float meat;

            Assert.AreEqual(0f, FoodDeliverySideJobSystem.GetIngredientTons(0, out processedFood, out meat), 0.000001f);
            Assert.AreEqual(0f, FoodDeliverySideJobSystem.GetIngredientTons(-3, out processedFood, out meat), 0.000001f);
        }

        [TestMethod]
        public void ComputeWholesaleCost_ChargesOnlyUnownedRestaurants()
        {
            Assert.AreEqual(0f, FoodDeliverySideJobSystem.ComputeWholesaleCost(5, true), 0.001f);
            Assert.AreEqual(
                5f * FoodDeliverySideJobSystem.WholesaleMealCost,
                FoodDeliverySideJobSystem.ComputeWholesaleCost(5, false),
                0.001f);
        }

        [TestMethod]
        public void GetRunContributionUnits_FullLoadIsOneUnit()
        {
            Assert.AreEqual(1f, FoodDeliverySideJobSystem.GetRunContributionUnits(5, 5), 0.001f);
            Assert.AreEqual(0.6f, FoodDeliverySideJobSystem.GetRunContributionUnits(3, 5), 0.001f);
            Assert.AreEqual(0f, FoodDeliverySideJobSystem.GetRunContributionUnits(0, 5), 0.001f);
        }

        // ---------------------------------------------------------------- delivery routes

        [TestMethod]
        public void BuildRoutes_GroupsEveryAuthoredAddressByDistrictInFileOrder()
        {
            var customers = new List<FoodDeliveryCustomerDefinition>
            {
                new FoodDeliveryCustomerDefinition { Id = 1, Name = "A", DistrictName = "Downtown", Position = new Vector3(10f, 0f, 0f) },
                new FoodDeliveryCustomerDefinition { Id = 2, Name = "B", DistrictName = "Vinewood", Position = new Vector3(20f, 0f, 0f) },
                new FoodDeliveryCustomerDefinition { Id = 3, Name = "C", DistrictName = "Downtown", Position = new Vector3(30f, 0f, 0f) },
            };

            var routes = FoodDeliverySideJobSystem.BuildRoutes(customers);

            Assert.AreEqual(2, routes.Count);
            Assert.AreEqual("Downtown", routes[0].DistrictName);
            Assert.AreEqual(2, routes[0].StopCount);
            CollectionAssert.AreEqual(new[] { 1, 3 }, routes[0].Stops.Select(stop => stop.Id).ToArray());
            Assert.AreEqual("Vinewood", routes[1].DistrictName);
            Assert.AreEqual(1, routes[1].StopCount);
        }

        [TestMethod]
        public void BuildRoutes_KeepsEveryAddressWithoutACap()
        {
            var customers = new List<FoodDeliveryCustomerDefinition>();
            for (int i = 1; i <= 250; i++)
            {
                customers.Add(new FoodDeliveryCustomerDefinition
                {
                    Id = i,
                    Name = "Stop " + i,
                    DistrictName = "Downtown",
                    Position = new Vector3(i, 0f, 0f),
                });
            }

            var routes = FoodDeliverySideJobSystem.BuildRoutes(customers);

            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(250, routes[0].StopCount, "adding addresses must never hit a limit");
        }

        [TestMethod]
        public void SelectRouteQueue_KeepsTheAuthoredOrderWhenTheVehicleCarriesEverything()
        {
            var route = CreateRoute(1, 4);

            int seed;
            bool cycleReset;
            var stops = FoodDeliverySideJobSystem.SelectRouteQueue(
                route.Stops,
                new HashSet<string>(),
                5,
                new Random(20260918),
                out seed,
                out cycleReset);

            Assert.AreEqual(0, seed, "a capacity that covers the whole route must not shuffle it");
            Assert.IsFalse(cycleReset);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, stops.Select(stop => stop.Id).ToArray());
        }

        [TestMethod]
        public void SelectRouteQueue_ShufflesWhenTheCapacityIsInferiorToTheDropOffCount()
        {
            var route = CreateRoute(1, 40);

            int seed;
            bool cycleReset;
            var stops = FoodDeliverySideJobSystem.SelectRouteQueue(
                route.Stops,
                new HashSet<string>(),
                5,
                new Random(20260918),
                out seed,
                out cycleReset);

            Assert.AreNotEqual(0, seed, "fewer meals than drop-offs must randomise the delivery order");
            Assert.AreEqual(40, stops.Count, "randomising reorders the route, it never drops an address");

            var authoredIds = route.Stops.Select(stop => stop.Id).ToArray();
            var shuffledIds = stops.Select(stop => stop.Id).ToArray();

            CollectionAssert.AreEquivalent(authoredIds, shuffledIds);
            CollectionAssert.AreNotEqual(authoredIds, shuffledIds);
        }

        [TestMethod]
        public void SelectRouteQueue_NeverQueuesTheSameAddressTwice()
        {
            var route = CreateRoute(1, 3);
            route.Stops.Add(new FoodDeliveryCustomerDefinition
            {
                Id = 2,
                Name = "Duplicate of stop 2",
                DistrictName = "Downtown",
                Position = new Vector3(2f, 0f, 0f),
            });

            int seed;
            bool cycleReset;
            var stops = FoodDeliverySideJobSystem.SelectRouteQueue(
                route.Stops,
                new HashSet<string>(),
                10,
                new Random(20260918),
                out seed,
                out cycleReset);

            Assert.AreEqual(3, stops.Count, "an address authored twice is still one address");
            CollectionAssert.AllItemsAreUnique(stops.Select(FoodDeliverySideJobSystem.GetStopKey).ToArray());
        }

        [TestMethod]
        public void SelectRouteQueue_SkipsTheAddressesTheSessionAlreadyServed()
        {
            var route = CreateRoute(1, 4);
            var served = new HashSet<string>(StringComparer.Ordinal)
            {
                FoodDeliverySideJobSystem.GetStopKey(route.Stops[0]),
                FoodDeliverySideJobSystem.GetStopKey(route.Stops[2]),
            };

            int seed;
            bool cycleReset;
            var stops = FoodDeliverySideJobSystem.SelectRouteQueue(
                route.Stops,
                served,
                5,
                new Random(20260918),
                out seed,
                out cycleReset);

            Assert.IsFalse(cycleReset);
            CollectionAssert.AreEqual(new[] { 2, 4 }, stops.Select(stop => stop.Id).ToArray());
        }

        [TestMethod]
        public void SelectRouteQueue_RestartsTheCycleOnceTheWholeRouteWasServed()
        {
            var route = CreateRoute(1, 3);
            var served = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stop in route.Stops)
            {
                served.Add(FoodDeliverySideJobSystem.GetStopKey(stop));
            }

            int seed;
            bool cycleReset;
            var stops = FoodDeliverySideJobSystem.SelectRouteQueue(
                route.Stops,
                served,
                10,
                new Random(20260918),
                out seed,
                out cycleReset);

            Assert.IsTrue(cycleReset, "a fully served district must start a new cycle instead of running dry");
            Assert.AreEqual(3, stops.Count);
            CollectionAssert.AreEquivalent(
                route.Stops.Select(stop => stop.Id).ToArray(),
                stops.Select(stop => stop.Id).ToArray());
        }

        [TestMethod]
        public void GetStopKey_UsesTheAuthoredIdAndFallsBackToTheCoordinate()
        {
            Assert.AreEqual(
                "id:201",
                FoodDeliverySideJobSystem.GetStopKey(new FoodDeliveryCustomerDefinition { Id = 201, Position = new Vector3(1f, 2f, 3f) }));

            var withoutId = FoodDeliverySideJobSystem.GetStopKey(
                new FoodDeliveryCustomerDefinition { Id = 0, Position = new Vector3(1f, 2f, 3f) });
            Assert.IsTrue(withoutId.StartsWith("pos:", StringComparison.Ordinal), withoutId);
            Assert.AreEqual(withoutId, FoodDeliverySideJobSystem.GetStopKey(new FoodDeliveryCustomerDefinition { Id = 0, Position = new Vector3(1f, 2f, 3f) }));
            Assert.AreNotEqual(withoutId, FoodDeliverySideJobSystem.GetStopKey(new FoodDeliveryCustomerDefinition { Id = 0, Position = new Vector3(1f, 2f, 4f) }));
        }

        [TestMethod]
        public void DeliverySession_NeverServesTheSameAddressTwice()
        {
            var system = CreateSystem(GetConfigDirectory());
            var route = system.GetRoutes().OrderByDescending(item => item.StopCount).First();
            Assert.IsTrue(route.StopCount >= 2, "the shipped pool must hold a route with at least two addresses");

            system.QueueRouteStops(route.DistrictName, null);
            Assert.AreEqual(route.StopCount, system.QueuedStops.Count, "a run starts with every address still to serve");
            CollectionAssert.AllItemsAreUnique(
                system.QueuedStops.Select(FoodDeliverySideJobSystem.GetStopKey).ToArray());

            // Serve half of the district, then queue again: a served address must not come back.
            var served = system.QueuedStops.Take(route.StopCount / 2).ToList();
            ServeStops(system, served);

            system.QueueRouteStops(route.DistrictName, null);
            var remaining = system.QueuedStops.Select(FoodDeliverySideJobSystem.GetStopKey).ToArray();
            foreach (var stop in served)
            {
                CollectionAssert.DoesNotContain(
                    remaining,
                    FoodDeliverySideJobSystem.GetStopKey(stop),
                    "an address already served in this session must not be queued again");
            }

            // Serving the rest exhausts the district, which starts a new cycle instead of running dry.
            ServeStops(system, system.QueuedStops.ToList());
            system.QueueRouteStops(route.DistrictName, null);

            Assert.AreEqual(route.StopCount, system.QueuedStops.Count, "a fully served district starts a new cycle");
        }

        [TestMethod]
        public void ResolveCreditDistrict_PrefersTheChosenRouteDistrict()
        {
            // The route the player picked is the district that gets the bonus, even when the restaurant
            // itself sits in another district.
            Assert.AreEqual("Grapeseed", FoodDeliverySideJobSystem.ResolveCreditDistrict("Grapeseed", "Vinewood"));

            // No route (legacy save) falls back to the restaurant's district.
            Assert.AreEqual("Vinewood", FoodDeliverySideJobSystem.ResolveCreditDistrict(null, "Vinewood"));
            Assert.AreEqual("Vinewood", FoodDeliverySideJobSystem.ResolveCreditDistrict(string.Empty, "Vinewood"));
            Assert.AreEqual(string.Empty, FoodDeliverySideJobSystem.ResolveCreditDistrict(null, null));
        }

        [TestMethod]
        public void ResolveDistanceBand_LabelsTheAuthoredDistance()
        {
            Assert.AreEqual(0, FoodDeliverySideJobSystem.ResolveDistanceBand(500f));
            Assert.AreEqual(1, FoodDeliverySideJobSystem.ResolveDistanceBand(1500f));
            Assert.AreEqual(2, FoodDeliverySideJobSystem.ResolveDistanceBand(9000f));
        }

        // ---------------------------------------------------------------- run rules

        [TestMethod]
        public void IsStoppedWithinRange_RequiresCloseAndSlow()
        {
            Assert.IsTrue(FoodDeliverySideJobSystem.IsStoppedWithinRange(10f, 0.5f, 20f));
            Assert.IsFalse(FoodDeliverySideJobSystem.IsStoppedWithinRange(30f, 0f, 20f), "too far");
            Assert.IsFalse(FoodDeliverySideJobSystem.IsStoppedWithinRange(10f, 5f, 20f), "too fast");
        }

        [TestMethod]
        public void HasAbandonedRun_UsesTheAwayTimeout()
        {
            Assert.IsFalse(FoodDeliverySideJobSystem.HasAbandonedRun(1000, 0, 30000), "not away yet");
            Assert.IsFalse(FoodDeliverySideJobSystem.HasAbandonedRun(1000, 900, 30000));
            Assert.IsTrue(FoodDeliverySideJobSystem.HasAbandonedRun(30900, 900, 30000));
        }

        [TestMethod]
        public void IsSpoiled_UsesTheDeadline()
        {
            Assert.IsFalse(FoodDeliverySideJobSystem.IsSpoiled(1000, 0), "no deadline means never spoiled");
            Assert.IsFalse(FoodDeliverySideJobSystem.IsSpoiled(999, 1000));
            Assert.IsTrue(FoodDeliverySideJobSystem.IsSpoiled(1000, 1000));
            Assert.IsTrue(FoodDeliverySideJobSystem.IsSpoiled(1001, 1000));
        }

        // ---------------------------------------------------------------- instance behaviour

        [TestMethod]
        public void TryStartRun_WithoutVehicle_Refuses()
        {
            var system = CreateSystem(GetConfigDirectory());

            string message;
            Assert.IsFalse(system.TryStartRun("WellStackedPizzaVespucci", out message));
            StringAssert.Contains(message, "delivery vehicle");
        }

        [TestMethod]
        public void TryStartRun_WithUnknownRestaurant_Refuses()
        {
            var system = CreateSystem(GetConfigDirectory());

            string message;
            Assert.IsFalse(system.TryStartRun("NoSuchRestaurant", out message));
            Assert.IsFalse(string.IsNullOrWhiteSpace(message));
        }

        [TestMethod]
        public void TryBuyVehicle_RefusesUnknownModels()
        {
            var system = CreateSystem(GetConfigDirectory());

            string message;
            Assert.IsFalse(system.TryBuyVehicle("nonexistent", out message));
            Assert.IsFalse(string.IsNullOrWhiteSpace(message));
        }

        [TestMethod]
        public void TryBuyVehicle_RefusesVehiclesAboveTheSkillLevel()
        {
            var system = CreateSystem(GetConfigDirectory());

            string message;
            // The food truck needs level 10 and the test system starts with no skill progress.
            Assert.IsFalse(system.TryBuyVehicle("taco", out message));
            StringAssert.Contains(message, "level");
        }

        [TestMethod]
        public void TryBuyVehicle_BuysTheScooterAndParksIt()
        {
            var system = CreateSystem(GetConfigDirectory(), getCompanyBalance: () => 100000f);

            string message;
            Assert.IsTrue(system.TryBuyVehicle("pizzaboy", out message), message);
            Assert.IsTrue(system.OwnsVehicle("pizzaboy"));
            Assert.AreEqual(1, system.GetOwnedVehicles().Count);

            Assert.IsFalse(system.TryBuyVehicle("pizzaboy", out message), "buying twice must be refused");
        }

        [TestMethod]
        public void TryStoreVehicle_WithoutVehicle_Refuses()
        {
            var system = CreateSystem(GetConfigDirectory());

            string message;
            Assert.IsFalse(system.TryStoreVehicle(null, out message));
            Assert.IsFalse(string.IsNullOrWhiteSpace(message));
        }

        [TestMethod]
        public void ResolveVehicleSpawnRestaurant_UsesTheRequestedRestaurantNotTheFirstCatalogOne()
        {
            var restaurants = CreateStubRestaurants();
            restaurants.Add(new FoodDeliveryRestaurantInfo
            {
                SiteKey = "SecondRestaurant",
                Name = "Second Restaurant",
                District = "Vinewood",
                ProductCommodity = "Burger",
                Position = new Vector3(500f, 500f, 30f),
                Heading = 0f,
                SpawnPosition = new Vector3(505f, 505f, 30f),
                SpawnHeading = 180f,
                SpawnResolved = true,
                ProductBasePrice = 1200f,
                IsOwned = true,
                PositionResolved = true,
            });

            var system = CreateSystem(GetConfigDirectory(), restaurants: restaurants);

            // The restaurant whose page asked for the vehicle wins, even though it is not the first one.
            var chosen = system.ResolveVehicleSpawnRestaurant("SecondRestaurant", null);
            Assert.IsNotNull(chosen);
            Assert.AreEqual("SecondRestaurant", chosen.SiteKey);
            Assert.AreEqual(505f, chosen.SpawnPosition.X);
            Assert.AreEqual(505f, chosen.SpawnPosition.Y);

            // An unusable key falls back to the job garage anchor instead of failing outright.
            var fallback = system.ResolveVehicleSpawnRestaurant("MissingRestaurant", null);
            Assert.IsNotNull(fallback);
            Assert.AreEqual("TestRestaurant", fallback.SiteKey);
        }

        [TestMethod]
        public void CancelCurrentJob_WithoutRun_DoesNotThrow()
        {
            var system = CreateSystem(GetConfigDirectory());

            system.CancelCurrentJob();
            system.ResetState();

            Assert.IsFalse(system.HasActiveRun);
            Assert.AreEqual(FoodDeliveryRunPhase.Idle, system.Phase);
        }

        [TestMethod]
        public void TryReloadConfiguration_ReReadsTheShippedConfig()
        {
            var system = CreateSystem(GetConfigDirectory());

            string message;
            Assert.IsTrue(system.TryReloadConfiguration(out message), message);
            Assert.AreEqual(1, system.GetRestaurants().Count, "restaurants come from the catalog, not from the reload");
            Assert.AreEqual(CountAuthoredDropoffs(), system.GetCustomers().Count);
        }

        [TestMethod]
        public void TryReloadConfiguration_FromAnEmptyConfigDirectory_Refuses()
        {
            var configDirectory = CreateTempConfigDirectory("<JobCoordinates />");
            try
            {
                // No catalog restaurants: the reload must refuse instead of half-applying.
                var system = CreateSystem(configDirectory, restaurants: new List<FoodDeliveryRestaurantInfo>());

                string message;
                Assert.IsFalse(system.TryReloadConfiguration(out message));
                Assert.IsFalse(string.IsNullOrWhiteSpace(message));
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        // ---------------------------------------------------------------- persistence

        [TestMethod]
        public void Snapshot_WithoutData_ReportsHasDataFalse()
        {
            var system = CreateSystem(GetConfigDirectory());

            var snapshot = system.CreatePersistenceSnapshot();

            Assert.IsFalse(snapshot.HasData);
            Assert.AreEqual(0, snapshot.OwnedVehicleModels.Count);
            Assert.AreEqual(0, snapshot.Orders.Count);
            Assert.AreEqual(1, snapshot.NextOrderId);
        }

        [TestMethod]
        public void Snapshot_WithARun_ReportsHasDataTrue()
        {
            var snapshot = new FoodDeliveryPersistenceSnapshot { LoadedMeals = 3 };

            Assert.IsTrue(snapshot.HasData);
        }

        [TestMethod]
        public void ApplyPersistenceSnapshot_RoundTripsAnActiveRun()
        {
            var system = CreateSystem(GetConfigDirectory());

            var snapshot = new FoodDeliveryPersistenceSnapshot
            {
                ActiveVehicleModelName = "pizzaboy",
                ActiveRestaurantKey = "TestRestaurant",
                ProductCommodity = "Pizza",
                LoadedMeals = 4,
                MealsDelivered = 1,
                RunsCompleted = 6,
                RunsAbandoned = 2,
                RouteCashEarned = 1234.5f,
                RouteXpEarned = 640f,
                RunFareEarned = 120f,
                ActiveOrderId = 9,
                NextOrderId = 10,
            };
            snapshot.OwnedVehicleModels.Add("pizzaboy");
            snapshot.Orders.Add(new FoodDeliveryOrderSnapshot
            {
                OrderId = 9,
                CustomerName = "Legion Square",
                CustomerDistrict = "Downtown",
                CustomerPosition = new Vector3(195f, -946f, 30f),
                DistanceBand = 1,
                DistanceMeters = 1800f,
            });

            system.ApplyPersistenceSnapshot(snapshot);

            Assert.IsTrue(system.HasActiveRun);
            Assert.AreEqual(FoodDeliveryRunPhase.EnRoute, system.Phase);
            Assert.AreEqual("TestRestaurant", system.ActiveRestaurantKey);
            Assert.AreEqual("Pizza", system.ProductCommodity);
            Assert.AreEqual(4, system.LoadedMeals);
            Assert.AreEqual(1, system.MealsDelivered);
            Assert.IsTrue(system.OwnsVehicle("pizzaboy"));
            Assert.IsNotNull(system.ActiveOrder);
            Assert.AreEqual("Legion Square", system.ActiveOrder.CustomerName);

            var roundTrip = system.CreatePersistenceSnapshot();
            Assert.AreEqual("pizzaboy", roundTrip.ActiveVehicleModelName);
            Assert.AreEqual("TestRestaurant", roundTrip.ActiveRestaurantKey);
            Assert.AreEqual("Pizza", roundTrip.ProductCommodity);
            Assert.AreEqual(4, roundTrip.LoadedMeals);
            Assert.AreEqual(1, roundTrip.MealsDelivered);
            Assert.AreEqual(6, roundTrip.RunsCompleted);
            Assert.AreEqual(2, roundTrip.RunsAbandoned);
            Assert.AreEqual(1234.5f, roundTrip.RouteCashEarned, 0.001f);
            Assert.AreEqual(640f, roundTrip.RouteXpEarned, 0.001f);
            Assert.AreEqual(9, roundTrip.ActiveOrderId);
            Assert.AreEqual(10, roundTrip.NextOrderId);
            Assert.AreEqual(1, roundTrip.Orders.Count);
            Assert.AreEqual("Downtown", roundTrip.Orders[0].CustomerDistrict);
        }

        [TestMethod]
        public void ApplyPersistenceSnapshot_WithAnUnknownRestaurant_DropsTheRun()
        {
            var system = CreateSystem(GetConfigDirectory());

            var snapshot = new FoodDeliveryPersistenceSnapshot
            {
                ActiveRestaurantKey = "RemovedRestaurant",
                LoadedMeals = 5,
                ActiveOrderId = 3,
                NextOrderId = 4,
            };
            snapshot.Orders.Add(new FoodDeliveryOrderSnapshot
            {
                OrderId = 3,
                CustomerPosition = new Vector3(1f, 2f, 3f),
                DistanceBand = 0,
            });

            system.ApplyPersistenceSnapshot(snapshot);

            Assert.IsFalse(system.HasActiveRun, "a run whose restaurant left the config must not restore");
            Assert.IsNull(system.ActiveOrder);
            Assert.AreEqual(FoodDeliveryRunPhase.Idle, system.Phase);
        }

        [TestMethod]
        public void ApplyPersistenceSnapshot_WithNull_ResetsEverything()
        {
            var system = CreateSystem(GetConfigDirectory());
            system.ApplyPersistenceSnapshot(new FoodDeliveryPersistenceSnapshot { LoadedMeals = 2 });

            system.ApplyPersistenceSnapshot(null);

            Assert.IsFalse(system.HasActiveRun);
            Assert.AreEqual(0, system.LoadedMeals);
            Assert.AreEqual(FoodDeliveryRunPhase.Idle, system.Phase);
        }

        [TestMethod]
        public void ApplyPersistenceSnapshot_DropsVehiclesMissingFromTheVehicleList()
        {
            var system = CreateSystem(GetConfigDirectory());

            var snapshot = new FoodDeliveryPersistenceSnapshot();
            snapshot.OwnedVehicleModels.Add("pizzaboy");
            snapshot.OwnedVehicleModels.Add("removed_scooter");

            system.ApplyPersistenceSnapshot(snapshot);

            Assert.IsTrue(system.OwnsVehicle("pizzaboy"));
            Assert.IsFalse(system.OwnsVehicle("removed_scooter"), "a garage entry that no longer exists is dropped");
            Assert.AreEqual(1, system.CreatePersistenceSnapshot().OwnedVehicleModels.Count);
        }

        [TestMethod]
        public void Persistence_RoundTripsThroughTheSaveFileWithVersion36()
        {
            var filePath = TestWorkspace.CreateTempFilePath("fooddelivery.state.xml");

            try
            {
                var foodDelivery = new FoodDeliveryPersistenceSnapshot
                {
                    ActiveVehicleModelName = "pizzaboy",
                    ActiveRestaurantKey = "CluckinBellDavis",
                    ProductCommodity = "Burger",
                    LoadedMeals = 3,
                    MealsDelivered = 2,
                    RunsCompleted = 11,
                    RunsAbandoned = 1,
                    RouteCashEarned = 2450.75f,
                    RouteXpEarned = 380f,
                    RunFareEarned = 90f,
                    SpoiledAtGameTime = 123456,
                    ActiveOrderId = 7,
                    NextOrderId = 8,
                    RouteDistrict = "Downtown",
                    RouteSeed = 987654,
                    RouteStopsDelivered = 2,
                };
                foodDelivery.OwnedVehicleModels.Add("pizzaboy");
                foodDelivery.OwnedVehicleModels.Add("taco");
                foodDelivery.Orders.Add(new FoodDeliveryOrderSnapshot
                {
                    OrderId = 7,
                    CustomerName = "Grove Street",
                    CustomerDistrict = "SouthLosSantos",
                    CustomerPosition = new Vector3(100f, -1900f, 30f),
                    DistanceBand = 2,
                    DistanceMeters = 2600f,
                });

                var metadata = new IndustryPersistenceMetadata { FoodDelivery = foodDelivery };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Section name=\"FoodDelivery\">");
                StringAssert.Contains(rawSave, "<Section name=\"FoodDelivery:Order:7\">");
                StringAssert.Contains(rawSave, "<Value key=\"Version\">36</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"ActiveRestaurantKey\">CluckinBellDavis</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"OwnedVehicles\">pizzaboy,taco</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"CustomerName\">Grove Street</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"CustomerDistrict\">SouthLosSantos</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"DistanceBand\">2</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"RouteDistrict\">Downtown</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"RouteSeed\">987654</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"RouteStopsDelivered\">2</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var restored = result.Metadata.FoodDelivery;

                Assert.IsNotNull(restored);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.AreEqual("pizzaboy", restored.ActiveVehicleModelName);
                Assert.AreEqual("CluckinBellDavis", restored.ActiveRestaurantKey);
                Assert.AreEqual("Burger", restored.ProductCommodity);
                Assert.AreEqual(3, restored.LoadedMeals);
                Assert.AreEqual(2, restored.MealsDelivered);
                Assert.AreEqual(11, restored.RunsCompleted);
                Assert.AreEqual(1, restored.RunsAbandoned);
                Assert.AreEqual(2450.75f, restored.RouteCashEarned, 0.001f);
                Assert.AreEqual(380f, restored.RouteXpEarned, 0.001f);
                Assert.AreEqual(90f, restored.RunFareEarned, 0.001f);
                Assert.AreEqual(123456, restored.SpoiledAtGameTime);
                Assert.AreEqual(7, restored.ActiveOrderId);
                Assert.AreEqual(8, restored.NextOrderId);
                Assert.AreEqual("Downtown", restored.RouteDistrict);
                Assert.AreEqual(987654, restored.RouteSeed);
                Assert.AreEqual(2, restored.RouteStopsDelivered);
                CollectionAssert.AreEqual(new[] { "pizzaboy", "taco" }, restored.OwnedVehicleModels.ToArray());

                Assert.AreEqual(1, restored.Orders.Count);
                Assert.AreEqual(7, restored.Orders[0].OrderId);
                Assert.AreEqual("Grove Street", restored.Orders[0].CustomerName);
                Assert.AreEqual("SouthLosSantos", restored.Orders[0].CustomerDistrict);
                Assert.AreEqual(new Vector3(100f, -1900f, 30f), restored.Orders[0].CustomerPosition);
                Assert.AreEqual(2, restored.Orders[0].DistanceBand);
                Assert.AreEqual(2600f, restored.Orders[0].DistanceMeters, 0.001f);
            }
            finally
            {
                DeleteDirectory(Path.GetDirectoryName(filePath));
            }
        }

        [TestMethod]
        public void Persistence_WithoutFoodDeliverySection_ReturnsNullSnapshot()
        {
            var filePath = TestWorkspace.CreateTempFilePath("fooddelivery.empty.xml");

            try
            {
                var metadata = new IndustryPersistenceMetadata
                {
                    Profit = 500f,
                    StartingBalance = 500f,
                };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());

                Assert.IsNull(result.Metadata.FoodDelivery);
            }
            finally
            {
                DeleteDirectory(Path.GetDirectoryName(filePath));
            }
        }

        // ---------------------------------------------------------------- helpers

        private static FoodDeliverySideJobSystem CreateSystem(
            string configDirectory,
            Func<float> getCompanyBalance = null,
            IReadOnlyList<FoodDeliveryRestaurantInfo> restaurants = null)
        {
            var list = restaurants ?? CreateStubRestaurants();

            return new FoodDeliverySideJobSystem(
                configDirectory,
                null,
                null,
                null,
                null,
                getCompanyBalance,
                null,
                null,
                null,
                () => list,
                null,
                null,
                new Random(20260918));
        }

        /// <summary>
        /// Restaurants arrive from the industry catalog, so the tests supply their own. The stub is
        /// owned, so its ingredients are free and it pays the same fares as a bought restaurant.
        /// </summary>
        private static List<FoodDeliveryRestaurantInfo> CreateStubRestaurants()
        {
            return new List<FoodDeliveryRestaurantInfo>
            {
                new FoodDeliveryRestaurantInfo
                {
                    SiteKey = "TestRestaurant",
                    Name = "Test Restaurant",
                    District = "Downtown",
                    ProductCommodity = "Pizza",
                    Position = new Vector3(195f, -946f, 30f),
                    Heading = 90f,
                    SpawnPosition = new Vector3(195f, -946f, 30f),
                    SpawnHeading = 90f,
                    SpawnResolved = true,
                    ProductBasePrice = 1200f,
                    IsOwned = true,
                    PositionResolved = true,
                },
            };
        }

        /// <summary>Marks every given address as delivered for the session, without a game world.</summary>
        private static void ServeStops(FoodDeliverySideJobSystem system, IEnumerable<FoodDeliveryCustomerDefinition> stops)
        {
            foreach (var stop in stops)
            {
                system.MarkStopDelivered(new FoodDeliveryOrder
                {
                    StopKey = FoodDeliverySideJobSystem.GetStopKey(stop),
                    Position = stop.Position,
                });
            }
        }

        /// <summary>CustomerDropoff points authored in the shipped JobCoordinates.xml.</summary>
        private static int CountAuthoredDropoffs()
        {
            var document = XDocument.Load(Path.Combine(GetConfigDirectory(), "SideJobs", "JobCoordinates.xml"));
            return document.Root
                .Elements("JobPoint")
                .Count(point => string.Equals(
                    (string)point.Attribute("function"),
                    "CustomerDropoff",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static FoodDeliveryRoute CreateRoute(int firstId, int count)
        {
            var route = new FoodDeliveryRoute("Downtown");
            for (int i = 0; i < count; i++)
            {
                route.Stops.Add(new FoodDeliveryCustomerDefinition
                {
                    Id = firstId + i,
                    Name = "Stop " + (firstId + i),
                    DistrictName = "Downtown",
                    Position = new Vector3(firstId + i, 0f, 0f),
                });
            }

            return route;
        }

        /// <summary>Every role="Restaurant" site of the shipped catalog: the only restaurant definition.</summary>
        private static List<XElement> ReadRestaurantSites()
        {
            var document = XDocument.Load(Path.Combine(GetConfigDirectory(), "Sites.xml"));
            return document.Root
                .Elements("Site")
                .Where(site => string.Equals((string)site.Attribute("role"), "Restaurant", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static string GetConfigDirectory()
        {
            return Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
        }

        private static List<string> SplitCsv(string value)
        {
            return (value ?? string.Empty)
                .Split(',')
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();
        }

        private static List<string> ReadDistrictNames()
        {
            var document = XDocument.Load(Path.Combine(GetConfigDirectory(), "Districts.xml"));
            return document.Root
                .Elements("District")
                .Select(element => (string)element.Attribute("name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }

        private static string CreateTempConfigDirectory(string jobCoordinatesXml, string jobVehiclesXml = null)
        {
            var directory = Path.Combine(Path.GetTempPath(), "LSOL.Tests", Guid.NewGuid().ToString("N"));
            var sideJobsDirectory = Path.Combine(directory, "SideJobs");
            Directory.CreateDirectory(sideJobsDirectory);
            File.WriteAllText(Path.Combine(sideJobsDirectory, "JobCoordinates.xml"), jobCoordinatesXml);
            if (!string.IsNullOrWhiteSpace(jobVehiclesXml))
            {
                File.WriteAllText(Path.Combine(sideJobsDirectory, "JobVehicles.xml"), jobVehiclesXml);
            }

            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
