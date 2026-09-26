using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GTA;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class BusSideJobSystemTests
    {
        private static readonly string[] ExpectedRouteIds =
        {
            "KortzCenter",
            "PaletoBay",
            "BlaineCounty",
            "WestLosSantos",
            "DowntownVinewood",
            "EastSouthAirport",
        };

        private static readonly string[][] ExpectedDistricts =
        {
            new[] { "NorthLosSantos" },
            new[] { "PaletoBay" },
            new[] { "Grapeseed", "GrandSenora" },
            new[] { "WestLosSantos", "NorthLosSantos" },
            new[] { "Downtown", "Vinewood", "SouthLosSantos" },
            new[] { "EastLosSantos", "SouthLosSantos", "Airport", "Port" },
        };

        /// <summary>
        /// The shipped catalogue is authored content: every count below is read back from the XML
        /// instead of being pinned here, so editing BusRoute.xml never breaks the suite while a
        /// loader that drops a station, a spot or a shelter still fails it.
        /// </summary>
        private static XDocument LoadShippedCatalogue()
        {
            return XDocument.Load(Path.Combine(GetConfigDirectory(), "SideJobs", "BusRoute.xml"));
        }

        private static XElement FindRouteElement(XDocument catalogue, string routeId)
        {
            return catalogue.Root
                .Elements("BusRoute")
                .Single(element => string.Equals((string)element.Attribute("id"), routeId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Passenger spot elements of a station, including a nested wrapper element.</summary>
        private static IEnumerable<XElement> PedElements(XElement stationElement)
        {
            if (stationElement == null)
            {
                yield break;
            }

            foreach (var child in stationElement.Elements())
            {
                var name = child.Name.LocalName;
                if (string.Equals(name, "Ped", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Passenger", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Stand", StringComparison.OrdinalIgnoreCase))
                {
                    yield return child;
                    continue;
                }

                if (string.Equals(name, "Peds", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Passengers", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var ped in child.Elements())
                    {
                        yield return ped;
                    }
                }
            }
        }

        private static float ReadFloat(XElement element, string attribute)
        {
            var value = element != null ? element.Attribute(attribute) : null;
            return value != null ? float.Parse(value.Value, CultureInfo.InvariantCulture) : 0f;
        }

        private static float ReadHeading(XElement element)
        {
            var value = element != null ? element.Attribute("heading") ?? element.Attribute("h") : null;
            return value != null ? float.Parse(value.Value, CultureInfo.InvariantCulture) : 0f;
        }

        private static int ReadInt(XElement element, string attribute)
        {
            var value = element != null ? element.Attribute(attribute) : null;
            return value != null ? int.Parse(value.Value, CultureInfo.InvariantCulture) : -1;
        }

        [TestMethod]
        public void LoadRoutes_ParsesEveryRouteWithItsStations()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());
            var catalogue = LoadShippedCatalogue();
            var routeElements = catalogue.Root.Elements("BusRoute").ToList();

            Assert.AreEqual(routeElements.Count, routes.Count);
            CollectionAssert.AreEquivalent(ExpectedRouteIds, routes.Select(route => route.RouteId).ToArray());

            var totalStations = 0;
            foreach (var route in routes)
            {
                var routeElement = FindRouteElement(catalogue, route.RouteId);

                Assert.AreEqual((string)routeElement.Attribute("name"), route.DisplayName);
                Assert.AreEqual(
                    routeElement.Elements("Station").Count(),
                    route.StationCount,
                    "station count for route {0}",
                    route.RouteId);
                Assert.IsTrue(
                    route.Stations.All(station => station != null && !string.IsNullOrWhiteSpace(station.Name)),
                    "route {0} must not contain null or unnamed stations",
                    route.RouteId);

                totalStations += route.StationCount;
            }

            Assert.AreEqual(
                routeElements.SelectMany(element => element.Elements("Station")).Count(),
                totalStations);
        }

        [TestMethod]
        public void LoadRoutes_OrdersStationsByIndexAndRenumbersThemContiguously()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());

            foreach (var route in routes)
            {
                for (int i = 0; i < route.StationCount; i++)
                {
                    Assert.AreEqual(i, route.Stations[i].Index, "route {0} station {1}", route.RouteId, i);
                }
            }
        }

        [TestMethod]
        public void LoadRoutes_ParsesTheDistrictsBeaconIntoCanonicalNames()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());

            foreach (var route in routes)
            {
                var expected = ExpectedDistricts[Array.IndexOf(ExpectedRouteIds, route.RouteId)];
                CollectionAssert.AreEqual(expected, route.DistrictNames.ToArray(), "districts for route {0}", route.RouteId);
                Assert.AreEqual(expected[0], route.PrimaryDistrictName, "primary district for route {0}", route.RouteId);
            }

            Assert.IsTrue(
                routes.All(route => route.DistrictNames.All(name => !name.Any(char.IsWhiteSpace))),
                "district names must be canonical (no spaces)");
        }

        [TestMethod]
        public void LoadRoutes_ShippedCatalogueHasNoClosedLoopLines()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());

            // The shipped lines are all point to point: the last stop is never the first one again.
            Assert.IsTrue(
                routes.All(route => !route.IsClosedLoop),
                "no shipped route may be flagged as a loop: {0}",
                string.Join(", ", routes.Where(route => route.IsClosedLoop).Select(route => route.RouteId)));
        }

        [TestMethod]
        public void ClosedLoop_IsDetectedWhenTheLastStationRepeatsTheFirst()
        {
            var loop = BuildSyntheticRoute("Loop", new[] { "A", "B", "C", "A" });
            var open = BuildSyntheticRoute("Open", new[] { "A", "B", "C", "D" });

            Assert.IsTrue(loop.IsClosedLoop);
            Assert.IsFalse(open.IsClosedLoop);
        }

        [TestMethod]
        public void LoadRoutes_ReadsTheExportedPassengerSpotsPerStation()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());
            var catalogue = LoadShippedCatalogue();

            foreach (var route in routes)
            {
                var routeElement = FindRouteElement(catalogue, route.RouteId);

                for (int s = 0; s < route.StationCount; s++)
                {
                    var stationElement = routeElement.Elements("Station").ElementAt(s);
                    var station = route.Stations[s];
                    var pedElements = PedElements(stationElement).ToList();

                    Assert.AreEqual(
                        pedElements.Count,
                        station.PedSpawnPoints.Count,
                        "ped spots of route {0} station {1}",
                        route.RouteId,
                        s);
                    Assert.IsTrue(
                        station.PedSpawnPoints.All(point => point != null && point.Position != Vector3.Zero),
                        "route {0} station {1} must not contain an empty spot",
                        route.RouteId,
                        s);
                    Assert.IsTrue(
                        station.PedSpawnPoints.All(point => point.Heading >= 0f && point.Heading < 360f),
                        "route {0} station {1} headings must be normalized",
                        route.RouteId,
                        s);

                    // Spot coordinates and headings are read verbatim (heading modulo 360) from the
                    // authored station element.
                    for (int p = 0; p < pedElements.Count; p++)
                    {
                        var pedElement = pedElements[p];
                        var spot = station.PedSpawnPoints[p];

                        Assert.AreEqual(ReadFloat(pedElement, "x"), spot.Position.X, 0.001f);
                        Assert.AreEqual(ReadFloat(pedElement, "y"), spot.Position.Y, 0.001f);
                        Assert.AreEqual(ReadFloat(pedElement, "z"), spot.Position.Z, 0.001f);
                        Assert.AreEqual(
                            BusSideJobSystem.NormalizeHeading(ReadHeading(pedElement)),
                            spot.Heading,
                            0.001f);
                    }
                }
            }
        }

        [TestMethod]
        public void LoadRoutes_ReadsEveryAuthoredPassengerSpot()
        {
            // The authored spots live in the station elements, so this proves the whole catalogue is
            // consumed: every station of the file is loaded, every spot is bound to its station and
            // every exported stop id is unique.
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());
            var stationElements = LoadShippedCatalogue().Root
                .Elements("BusRoute")
                .SelectMany(routeElement => routeElement.Elements("Station"))
                .ToList();

            var totalSpots = 0;
            var exportIds = new List<int>();

            foreach (var route in routes)
            {
                foreach (var station in route.Stations)
                {
                    Assert.IsTrue(
                        station.PedSpawnPoints.Count > 0,
                        "route {0} station {1} ({2}) has no authored passenger spots",
                        route.RouteId,
                        station.Index,
                        station.Name);
                    // Stop ids start at 0 in the authored data, so -1 is the "no export id" marker.
                    Assert.IsTrue(
                        station.ExportId >= 0,
                        "route {0} station {1} ({2}) has no exported stop id",
                        route.RouteId,
                        station.Index,
                        station.Name);

                    totalSpots += station.PedSpawnPoints.Count;
                    exportIds.Add(station.ExportId);
                }
            }

            Assert.AreEqual(stationElements.Count, exportIds.Count);
            Assert.AreEqual(
                stationElements.Sum(stationElement => PedElements(stationElement).Count()),
                totalSpots);
            Assert.AreEqual(exportIds.Count, exportIds.Distinct().Count(), "every exported stop id must be unique");
        }

        [TestMethod]
        public void LoadRoutes_ReadsPedAndShelterChildElements()
        {
            var configDirectory = CreateTempConfigDirectory(
                "<BusRoutes>\n" +
                "  <BusRoute id=\"7\" name=\"7 Element Line\" districts=\"Downtown\">\n" +
                "    <Station index=\"0\" name=\"Alpha\" x=\"0\" y=\"0\" z=\"10\" exportId=\"3\">\n" +
                "      <Ped x=\"1\" y=\"2\" z=\"3\" heading=\"90\" />\n" +
                "      <Ped x=\"4\" y=\"5\" z=\"6\" h=\"180\" />\n" +
                "      <Shelter model=\"2142033519\" x=\"-1\" y=\"-2\" z=\"-3\" heading=\"45\" />\n" +
                "    </Station>\n" +
                "    <Station index=\"1\" name=\"Beta\" x=\"100\" y=\"0\" z=\"10\" exportId=\"4\">\n" +
                "      <Peds>\n" +
                "        <Ped x=\"7\" y=\"8\" z=\"9\" heading=\"270\" />\n" +
                "      </Peds>\n" +
                "    </Station>\n" +
                "    <Station index=\"2\" name=\"Gamma\" x=\"200\" y=\"0\" z=\"10\" />\n" +
                "  </BusRoute>\n" +
                "</BusRoutes>");

            try
            {
                var routes = BusSideJobSystem.LoadRoutes(configDirectory);

                Assert.AreEqual(1, routes.Count);
                var route = routes[0];

                var alpha = route.Stations[0];
                Assert.AreEqual(3, alpha.ExportId);
                Assert.AreEqual(2, alpha.PedSpawnPoints.Count);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), alpha.PedSpawnPoints[0].Position);
                Assert.AreEqual(90f, alpha.PedSpawnPoints[0].Heading, 0.001f);
                Assert.AreEqual(180f, alpha.PedSpawnPoints[1].Heading, 0.001f, "the short h attribute must be read");
                Assert.AreEqual(2142033519, alpha.ShelterModel);
                Assert.AreEqual(new Vector3(-1f, -2f, -3f), alpha.ShelterPosition);
                Assert.AreEqual(45f, alpha.ShelterHeading, 0.001f);

                // The <Peds> wrapper is accepted as well.
                var beta = route.Stations[1];
                Assert.AreEqual(4, beta.ExportId);
                Assert.AreEqual(1, beta.PedSpawnPoints.Count);
                Assert.AreEqual(270f, beta.PedSpawnPoints[0].Heading, 0.001f);

                // A station with no child elements keeps the legacy fallback behaviour.
                var gamma = route.Stations[2];
                Assert.AreEqual(-1, gamma.ExportId);
                Assert.AreEqual(0, gamma.PedSpawnPoints.Count);
                Assert.AreEqual(-1, gamma.ShelterModel);

                // The waiting position of the first passenger is the authored spot.
                Vector3 position;
                float heading;
                Assert.IsTrue(BusSideJobSystem.TryGetWaitingSpot(alpha, 0, out position, out heading));
                Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
                Assert.AreEqual(90f, heading, 0.001f);
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        [TestMethod]
        public void LoadRoutes_ReadsTheShelterOfExportedStops()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());
            var catalogue = LoadShippedCatalogue();
            var shelteredCount = 0;
            var unshelteredCount = 0;

            foreach (var route in routes)
            {
                var routeElement = FindRouteElement(catalogue, route.RouteId);

                for (int s = 0; s < route.StationCount; s++)
                {
                    var stationElement = routeElement.Elements("Station").ElementAt(s);
                    var shelterElement = stationElement.Elements("Shelter").FirstOrDefault();
                    var station = route.Stations[s];

                    if (shelterElement == null)
                    {
                        Assert.AreEqual(-1, station.ShelterModel, "station {0} must stay unsheltered", station.Name);
                        unshelteredCount++;
                        continue;
                    }

                    shelteredCount++;
                    Assert.AreEqual(ReadInt(shelterElement, "model"), station.ShelterModel, "shelter model of {0}", station.Name);
                    Assert.AreEqual(ReadFloat(shelterElement, "x"), station.ShelterPosition.X, 0.001f);
                    Assert.AreEqual(ReadFloat(shelterElement, "y"), station.ShelterPosition.Y, 0.001f);
                    Assert.AreEqual(ReadFloat(shelterElement, "z"), station.ShelterPosition.Z, 0.001f);
                    Assert.AreEqual(
                        BusSideJobSystem.NormalizeHeading(ReadHeading(shelterElement)),
                        station.ShelterHeading,
                        0.001f);
                }
            }

            Assert.IsTrue(shelteredCount > 0, "the shipped catalogue must declare at least one shelter");
            Assert.IsTrue(unshelteredCount > 0, "the shipped catalogue must contain stops without a shelter");
        }

        [TestMethod]
        public void ParsePedSpawnPoints_ReadsTheExportedList()
        {
            const string comment =
                " export id=10 | peds(2): (-2292.75,438.79,174.60,h97.80) (-2293.64,438.67,174.60,h103.56) " +
                "| shelter: model=2142033519 at (-794.72,-1145.16,8.96) h205.85 ";

            var points = BusSideJobSystem.ParsePedSpawnPoints(comment);

            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(-2292.75f, points[0].Position.X, 0.001f);
            Assert.AreEqual(438.79f, points[0].Position.Y, 0.001f);
            Assert.AreEqual(174.60f, points[0].Position.Z, 0.001f);
            Assert.AreEqual(97.80f, points[0].Heading, 0.001f);
            Assert.AreEqual(103.56f, points[1].Heading, 0.001f);
        }

        [TestMethod]
        public void ParsePedSpawnPoints_WithoutThePedSection_ReturnsAnEmptyList()
        {
            Assert.AreEqual(0, BusSideJobSystem.ParsePedSpawnPoints("export id=4 | shelter: model=1 at (1,2,3) h4").Count);
            Assert.AreEqual(0, BusSideJobSystem.ParsePedSpawnPoints("export id=4").Count);
            Assert.AreEqual(0, BusSideJobSystem.ParsePedSpawnPoints(string.Empty).Count);
            Assert.AreEqual(0, BusSideJobSystem.ParsePedSpawnPoints(null).Count);
        }

        [TestMethod]
        public void TryParseExportId_ReadsTheStopId()
        {
            int exportId;

            Assert.IsTrue(BusSideJobSystem.TryParseExportId(" export id=41 | peds(7): (1,2,3,h4) ", out exportId));
            Assert.AreEqual(41, exportId);

            Assert.IsFalse(BusSideJobSystem.TryParseExportId(" peds(7): (1,2,3,h4) ", out exportId));
            Assert.AreEqual(-1, exportId);
        }

        [TestMethod]
        public void TryParseShelter_ReadsModelPositionAndHeading()
        {
            int model;
            Vector3 position;
            float heading;

            Assert.IsTrue(BusSideJobSystem.TryParseShelter(
                "export id=55 | peds(6): (1,2,3,h4) | shelter: model=2142033519 at (-794.72,-1145.16,8.96) h205.85",
                out model,
                out position,
                out heading));
            Assert.AreEqual(2142033519, model);
            Assert.AreEqual(-794.72f, position.X, 0.001f);
            Assert.AreEqual(-1145.16f, position.Y, 0.001f);
            Assert.AreEqual(8.96f, position.Z, 0.001f);
            Assert.AreEqual(205.85f, heading, 0.001f);

            Assert.IsFalse(BusSideJobSystem.TryParseShelter("export id=55 | peds(6): (1,2,3,h4)", out model, out position, out heading));
            Assert.AreEqual(-1, model);

            // The "shelter:" prefix is optional so the value also works as a bare attribute.
            Assert.IsTrue(BusSideJobSystem.TryParseShelter("model=99 at (1,2,3) h45", out model, out position, out heading));
            Assert.AreEqual(99, model);
            Assert.AreEqual(45f, heading, 0.001f);
        }

        [TestMethod]
        public void ApplyExportData_WithoutExportData_LeavesTheStationUntouched()
        {
            var station = new BusStationDefinition { Name = "Legacy", Position = new Vector3(1f, 2f, 3f) };

            BusSideJobSystem.ApplyExportData(station, null);

            // No following sibling at all.
            BusSideJobSystem.ApplyExportData(station, XElement.Parse("<Station x=\"1\" />"));

            // A plain comment without export data, and a station followed by the next station.
            var withPlainComment = XElement.Parse("<R><Station /><!-- nothing to export --></R>").Element("Station");
            BusSideJobSystem.ApplyExportData(station, withPlainComment);
            var followedByElement = XElement.Parse("<R><Station /><Station /></R>").Element("Station");
            BusSideJobSystem.ApplyExportData(station, followedByElement);

            Assert.AreEqual(-1, station.ExportId);
            Assert.AreEqual(-1, station.ShelterModel);
            Assert.AreEqual(0, station.PedSpawnPoints.Count);
        }

        [TestMethod]
        public void ApplyExportData_ReadsUncommentedTextAndAttributes()
        {
            // Shape 2: the shipped comment with its markers removed is a plain text node after the
            // station, which must keep working.
            var uncommented = XDocument.Parse(
                "<R>\n" +
                "  <Station x=\"1\" />\n" +
                "  export id=7 | peds(2): (1,2,3,h4) (5,6,7,h8)\n" +
                "</R>");
            var fromText = new BusStationDefinition();
            BusSideJobSystem.ApplyExportData(fromText, uncommented.Root.Element("Station"));

            Assert.AreEqual(7, fromText.ExportId);
            Assert.AreEqual(2, fromText.PedSpawnPoints.Count);
            Assert.AreEqual(1f, fromText.PedSpawnPoints[0].Position.X, 0.001f);
            Assert.AreEqual(8f, fromText.PedSpawnPoints[1].Heading, 0.001f);

            // Shape 2b: the same line inside an element wrapper.
            var wrapped = XElement.Parse(
                "<R><Station /><Peds>export id=8 | peds(1): (9,8,7,h6)</Peds></R>").Element("Station");
            var fromElement = new BusStationDefinition();
            BusSideJobSystem.ApplyExportData(fromElement, wrapped);

            Assert.AreEqual(8, fromElement.ExportId);
            Assert.AreEqual(1, fromElement.PedSpawnPoints.Count);
            Assert.AreEqual(9f, fromElement.PedSpawnPoints[0].Position.X, 0.001f);

            // Shape 3: inline attributes on the station itself.
            var inline = new BusStationDefinition();
            BusSideJobSystem.ApplyExportData(
                inline,
                XElement.Parse(
                    "<Station exportId=\"12\" peds=\"(1,2,3,h4) (5,6,7,h8)\" shelter=\"model=99 at (10,20,30) h45\" />"));

            Assert.AreEqual(12, inline.ExportId);
            Assert.AreEqual(2, inline.PedSpawnPoints.Count);
            Assert.AreEqual(99, inline.ShelterModel);
            Assert.AreEqual(10f, inline.ShelterPosition.X, 0.001f);
            Assert.AreEqual(45f, inline.ShelterHeading, 0.001f);
        }

        [TestMethod]
        public void ReadExportText_ReadsTheFollowingCommentOrTextOnly()
        {
            var document = XDocument.Parse(
                "<BusRoutes>\n" +
                "  <BusRoute id=\"X\">\n" +
                "    <Station index=\"0\" name=\"A\" x=\"0\" y=\"0\" z=\"0\" />\n" +
                "    <!-- export id=7 | peds(1): (1,2,3,h4) -->\n" +
                "    <Station index=\"1\" name=\"B\" x=\"1\" y=\"0\" z=\"0\" />\n" +
                "  </BusRoute>\n" +
                "</BusRoutes>");

            var stations = document.Descendants("Station").ToArray();

            StringAssert.Contains(BusSideJobSystem.ReadExportText(stations[0]), "export id=7");
            Assert.AreEqual(string.Empty, BusSideJobSystem.ReadExportText(stations[1]));
            Assert.AreEqual(string.Empty, BusSideJobSystem.ReadExportText(null));
        }

        [TestMethod]
        public void TryGetWaitingSpot_PrefersTheAuthoredSpotThenTheShelter()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());
            var route = routes.First(entry => entry.RouteId == "KortzCenter");
            var station = route.Stations[0];

            Vector3 position;
            float heading;

            Assert.IsTrue(BusSideJobSystem.TryGetWaitingSpot(station, 0, out position, out heading));
            Assert.AreEqual(station.PedSpawnPoints[0].Position, position);
            Assert.AreEqual(station.PedSpawnPoints[0].Heading, heading, 0.001f);

            // The sixth passenger of a six spot stop wraps onto the first spot.
            Assert.IsTrue(BusSideJobSystem.TryGetWaitingSpot(station, 6, out position, out heading));
            Assert.AreEqual(station.PedSpawnPoints[0].Position, position);

            // A shelter-only stop waits in front of the shelter.
            var shelterOnly = new BusStationDefinition
            {
                Position = new Vector3(100f, 100f, 10f),
                ShelterModel = 2142033519,
                ShelterPosition = new Vector3(50f, 60f, 5f),
                ShelterHeading = 90f,
            };
            Assert.IsTrue(BusSideJobSystem.TryGetWaitingSpot(shelterOnly, 0, out position, out heading));
            Assert.IsTrue(position.DistanceTo(shelterOnly.ShelterPosition) < 2f, "the crowd must wait at the shelter");
            Assert.AreEqual(90f, heading, 0.001f);

            // A legacy stop with neither spot data nor a shelter has no authored spot.
            var legacy = new BusStationDefinition { Position = new Vector3(1f, 2f, 3f) };
            Assert.IsFalse(BusSideJobSystem.TryGetWaitingSpot(legacy, 0, out position, out heading));
            Assert.IsFalse(BusSideJobSystem.TryGetWaitingSpot(null, 0, out position, out heading));
        }

        [TestMethod]
        public void LoadRoutes_DerivesMissingHeadingsFromTheBearingToTheNextStation()
        {
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());
            var route = routes.First(entry => entry.RouteId == "PaletoBay");

            Assert.IsTrue(route.StationCount > 1);

            for (int i = 0; i < route.StationCount - 1; i++)
            {
                var station = route.Stations[i];
                var next = route.Stations[i + 1];

                Assert.IsFalse(station.HasHeadingOverride, "the shipped catalogue has no station headings");
                var expected = BusSideJobSystem.ComputeBearingDegrees(
                    station.Position.X,
                    station.Position.Y,
                    next.Position.X,
                    next.Position.Y);
                Assert.AreEqual(expected, station.Heading, 0.001f, "route {0} station {1}", route.RouteId, i);
            }

            // The last station keeps the direction the line was travelling in.
            var last = route.Stations[route.StationCount - 1];
            var previous = route.Stations[route.StationCount - 2];
            var lastExpected = BusSideJobSystem.ComputeBearingDegrees(
                previous.Position.X,
                previous.Position.Y,
                last.Position.X,
                last.Position.Y);
            Assert.AreEqual(lastExpected, last.Heading, 0.001f);
        }

        [TestMethod]
        public void LoadRoutes_ExplicitStationHeadingWinsAndLegacyXmlStillLoads()
        {
            var configDirectory = CreateTempConfigDirectory(
                "<BusRoutes>\n" +
                "  <BusRoute id=\"9\" name=\"9 Test Line\">\n" +
                "    <Station index=\"2\" name=\"C\" x=\"200\" y=\"0\" z=\"10\" />\n" +
                "    <Station index=\"0\" name=\"A\" x=\"0\" y=\"0\" z=\"10\" heading=\"45\" />\n" +
                "    <Station index=\"1\" name=\"B\" x=\"100\" y=\"0\" z=\"10\" />\n" +
                "  </BusRoute>\n" +
                "</BusRoutes>");

            try
            {
                var routes = BusSideJobSystem.LoadRoutes(configDirectory);

                Assert.AreEqual(1, routes.Count);
                var route = routes[0];
                Assert.AreEqual("9 Test Line", route.DisplayName);
                Assert.AreEqual(0, route.DistrictNames.Count, "a route without the districts beacon stays loadable");
                Assert.AreEqual(string.Empty, route.PrimaryDistrictName);

                // The declared indices drive the order; the runtime indices are the positions.
                Assert.AreEqual("A", route.Stations[0].Name);
                Assert.AreEqual("B", route.Stations[1].Name);
                Assert.AreEqual("C", route.Stations[2].Name);
                Assert.AreEqual(0, route.Stations[0].Index);
                Assert.AreEqual(1, route.Stations[1].Index);
                Assert.AreEqual(2, route.Stations[2].Index);

                // The explicit heading is preserved; the others follow the line direction (east).
                Assert.IsTrue(route.Stations[0].HasHeadingOverride);
                Assert.AreEqual(45f, route.Stations[0].Heading, 0.001f);
                Assert.IsFalse(route.Stations[1].HasHeadingOverride);
                Assert.AreEqual(270f, route.Stations[1].Heading, 0.001f);
                Assert.AreEqual(270f, route.Stations[2].Heading, 0.001f);

                // Legacy XML has neither exported spots nor shelters.
                Assert.AreEqual(-1, route.Stations[0].ExportId);
                Assert.AreEqual(0, route.Stations[0].PedSpawnPoints.Count);
                Assert.AreEqual(-1, route.Stations[0].ShelterModel);
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        [TestMethod]
        public void ReadPosition_ReadsTheCoordinateAttributes()
        {
            var position = BusSideJobSystem.ReadPosition(XElement.Parse("<Station x=\"-2118.58\" y=\"-242.09\" z=\"16.29\" />"));

            Assert.AreEqual(-2118.58f, position.X, 0.001f);
            Assert.AreEqual(-242.09f, position.Y, 0.001f);
            Assert.AreEqual(16.29f, position.Z, 0.001f);

            var missing = BusSideJobSystem.ReadPosition(XElement.Parse("<Station x=\"12.5\" />"));
            Assert.AreEqual(12.5f, missing.X, 0.001f);
            Assert.AreEqual(0f, missing.Y, 0.001f);
            Assert.AreEqual(0f, missing.Z, 0.001f);

            // The catalogue may carry whitespace around the equals sign.
            var spaced = BusSideJobSystem.ReadPosition(XElement.Parse("<Station x=  \"261.88\" y=\"-1123.94\" z=\"29.21\" />"));
            Assert.AreEqual(261.88f, spaced.X, 0.001f);
        }

        [TestMethod]
        public void ReadDistricts_SplitsTrimsAndDropsDuplicateEntries()
        {
            var element = XElement.Parse(
                "<BusRoute districts=\"WestLosSantos, Downtown , ,North Los Santos,westLossantos,vinewood\" />");

            var districts = BusSideJobSystem.ReadDistricts(element);

            // \"North Los Santos\" is normalized to its canonical spelling and \"westLossantos\" is the
            // same district as \"WestLosSantos\", so it must not be listed twice.
            CollectionAssert.AreEqual(
                new[] { "WestLosSantos", "Downtown", "NorthLosSantos", "vinewood" },
                districts.ToArray());
        }

        [TestMethod]
        public void ReadDistricts_WithoutTheAttribute_ReturnsAnEmptyList()
        {
            Assert.AreEqual(0, BusSideJobSystem.ReadDistricts(XElement.Parse("<BusRoute />")).Count);
            Assert.AreEqual(0, BusSideJobSystem.ReadDistricts(null).Count);
        }

        [TestMethod]
        public void LoadRoutes_StationCoordinateIsTheStop_WithoutAnyStreetSnapping()
        {
            var configDirectory = CreateTempConfigDirectory(
                "<BusRoutes>\n" +
                "  <BusRoute id=\"9\" name=\"9 Stop Line\" districts=\"Downtown\">\n" +
                "    <Station index=\"0\" name=\"Sidewalk Stop\" x=\"-2297.95\" y=\"434.36\" z=\"175.30\" />\n" +
                "  </BusRoute>\n" +
                "</BusRoutes>");

            try
            {
                var station = BusSideJobSystem.LoadRoutes(configDirectory)[0].Stations[0];

                // The authored coordinate is used verbatim: nothing snaps it to the street graph and
                // no hidden "anchor" keeps a second, different target around.
                Assert.AreEqual(new Vector3(-2297.95f, 434.36f, 175.30f), station.Position);
                Assert.AreEqual(
                    0,
                    typeof(BusStationDefinition).GetProperties().Count(
                        property => property.Name.IndexOf("Anchor", StringComparison.Ordinal) >= 0),
                    "the station must carry exactly one coordinate and no anchor");
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        [TestMethod]
        public void LoadPedLayerEnabled_ReadsTheCoreSwitchAndDefaultsToOn()
        {
            var directory = Path.Combine(Path.GetTempPath(), "LSOL.Tests", Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(directory);
                Assert.IsTrue(BusSideJobSystem.LoadPedLayerEnabled(directory), "a missing Core.xml keeps the crowd on");
                Assert.IsTrue(BusSideJobSystem.LoadPedLayerEnabled(null));

                File.WriteAllText(Path.Combine(directory, "Core.xml"), "<Core><Controls openModMenu=\"F7\" /></Core>");
                Assert.IsTrue(BusSideJobSystem.LoadPedLayerEnabled(directory), "an absent attribute keeps the crowd on");

                File.WriteAllText(Path.Combine(directory, "Core.xml"), "<Core><Controls busPedLayer=\"false\" /></Core>");
                Assert.IsFalse(BusSideJobSystem.LoadPedLayerEnabled(directory));

                File.WriteAllText(Path.Combine(directory, "Core.xml"), "<Core><Controls busPedLayer=\"true\" /></Core>");
                Assert.IsTrue(BusSideJobSystem.LoadPedLayerEnabled(directory));

                File.WriteAllText(Path.Combine(directory, "Core.xml"), "<Core><Controls busPedLayer=\"nonsense\" /></Core>");
                Assert.IsTrue(BusSideJobSystem.LoadPedLayerEnabled(directory), "an unparseable value keeps the crowd on");

                File.WriteAllText(Path.Combine(directory, "Core.xml"), "<Core");
                Assert.IsTrue(BusSideJobSystem.LoadPedLayerEnabled(directory), "a broken Core.xml keeps the crowd on");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void ShippedCoreConfig_LeavesTheWaitingCrowdOn()
        {
            // The waiting passengers are part of the job: the shipped config must not switch them off
            // and the layer must not be runtime-dependent any more.
            Assert.IsTrue(BusSideJobSystem.LoadPedLayerEnabled(GetConfigDirectory()));
        }

        [TestMethod]
        public void PedLayer_StartsFromTheConfigAndCanBeToggledAtRuntime()
        {
            const string busRouteXml =
                "<BusRoutes>\n" +
                "  <BusRoute id=\"1\" name=\"Line\" districts=\"Downtown\">\n" +
                "    <Station index=\"0\" name=\"Stop\" x=\"0\" y=\"0\" z=\"10\" />\n" +
                "  </BusRoute>\n" +
                "</BusRoutes>";

            var onDirectory = CreateTempConfigDirectory(busRouteXml);
            var offDirectory = CreateTempConfigDirectory(
                busRouteXml,
                null,
                "<Core><Controls busPedLayer=\"false\" /></Core>");

            try
            {
                var on = CreateSystem(onDirectory);
                var off = CreateSystem(offDirectory);

                Assert.IsTrue(on.IsPedLayerActive, "the crowd is on unless Core.xml says otherwise");
                Assert.IsFalse(off.IsPedLayerActive, "busPedLayer=false must keep the crowd away");

                on.SetPedLayerEnabled(false);
                Assert.IsFalse(on.IsPedLayerActive);

                on.SetPedLayerEnabled(true);
                Assert.IsTrue(on.IsPedLayerActive);

                off.SetPedLayerEnabled(true);
                Assert.IsTrue(off.IsPedLayerActive, "the runtime setter is authoritative after load");
            }
            finally
            {
                DeleteDirectory(onDirectory);
                DeleteDirectory(offDirectory);
            }
        }

        [TestMethod]
        public void ExchangeValve_OnlyClosesAStalledOrEndlessStop()
        {
            // A stop where people keep moving is never cut short, however long it runs.
            Assert.IsFalse(BusSideJobSystem.ShouldCloseExchange(0, 0));
            Assert.IsFalse(BusSideJobSystem.ShouldCloseExchange(30000, 30000));
            Assert.IsFalse(BusSideJobSystem.ShouldCloseExchange(29999, 60000));

            // 30 s without a passenger moving is a stall, and the ceiling always closes the stop.
            Assert.IsTrue(BusSideJobSystem.ShouldCloseExchange(30001, 30001));
            Assert.IsTrue(BusSideJobSystem.ShouldCloseExchange(0, 120001));
        }

        [TestMethod]
        public void SeatingWithoutAnimation_RequiresThePassengerToStandAtTheBus()
        {
            var bus = new Vector3(0f, 0f, 0f);

            // At the bus: stepping in is invisible, so the timeout may seat the passenger directly.
            Assert.IsTrue(BusSideJobSystem.IsAtBusForSeating(new Vector3(2f, 0f, 0f), bus));
            Assert.IsTrue(BusSideJobSystem.IsAtBusForSeating(new Vector3(5f, 0f, 0f), bus));

            // Still on the sidewalk: the boarding animation must be kept, the ped is retried instead.
            Assert.IsFalse(BusSideJobSystem.IsAtBusForSeating(new Vector3(5.5f, 0f, 0f), bus));
            Assert.IsFalse(BusSideJobSystem.IsAtBusForSeating(new Vector3(12f, 4f, 0f), bus));
        }

        [TestMethod]
        public void ComputeBearingDegrees_UsesTheGtaHeadingConvention()
        {
            // 0 = north (+Y), 90 = west (-X), 180 = south (-Y), 270 = east (+X).
            Assert.AreEqual(0f, BusSideJobSystem.ComputeBearingDegrees(0f, 0f, 0f, 10f), 0.001f);
            Assert.AreEqual(90f, BusSideJobSystem.ComputeBearingDegrees(0f, 0f, -10f, 0f), 0.001f);
            Assert.AreEqual(180f, BusSideJobSystem.ComputeBearingDegrees(0f, 0f, 0f, -10f), 0.001f);
            Assert.AreEqual(270f, BusSideJobSystem.ComputeBearingDegrees(0f, 0f, 10f, 0f), 0.001f);

            // A degenerate segment has no direction.
            Assert.AreEqual(0f, BusSideJobSystem.ComputeBearingDegrees(5f, 5f, 5f, 5f), 0.001f);
        }

        [TestMethod]
        public void NormalizeHeading_WrapsIntoTheZeroToThreeSixtyRange()
        {
            Assert.AreEqual(350f, BusSideJobSystem.NormalizeHeading(-10f), 0.001f);
            Assert.AreEqual(10f, BusSideJobSystem.NormalizeHeading(370f), 0.001f);
            Assert.AreEqual(0f, BusSideJobSystem.NormalizeHeading(360f), 0.001f);
            Assert.AreEqual(0f, BusSideJobSystem.NormalizeHeading(float.NaN), 0.001f);
            Assert.AreEqual(0f, BusSideJobSystem.NormalizeHeading(float.PositiveInfinity), 0.001f);
        }

        [TestMethod]
        public void LoadBusVehicles_ParsesEveryBusWithItsSeatsAndUnlockLevel()
        {
            var buses = BusSideJobSystem.LoadBusVehicles(GetConfigDirectory());

            Assert.AreEqual(2, buses.Count);

            Assert.AreEqual("bus", buses[0].ModelName);
            Assert.AreEqual("City Bus", buses[0].Name);
            Assert.AreEqual(30, buses[0].Seats);
            Assert.AreEqual(0, buses[0].UnlockLevel);

            Assert.AreEqual("coach", buses[1].ModelName);
            Assert.AreEqual(30, buses[1].Seats);
            Assert.AreEqual(30, buses[1].UnlockLevel);

            Assert.IsTrue(buses.All(entry => entry.Price > 0f), "every bus must have a purchase price");
            Assert.IsTrue(buses.All(entry => entry.DailyRent >= 0f));
            Assert.IsTrue(buses.All(entry => entry.FuelCapacityLiters > 0f));
        }

        [TestMethod]
        public void LoadBusVehicles_ReadsOptionalDoorIndices()
        {
            var configDirectory = CreateTempConfigDirectory(
                "<BusRoutes />",
                "<JobVehicles>\n" +
                "  <JobVehicle id=\"1\" name=\"Two Door Bus\" model=\"bus\" type=\"Bus\" unlockLevel=\"0\" price=\"1\" seats=\"30\" doorIndices=\"0,2\" />\n" +
                "  <JobVehicle id=\"2\" name=\"Plain Bus\" model=\"coach\" type=\"Bus\" unlockLevel=\"0\" price=\"1\" seats=\"30\" />\n" +
                "</JobVehicles>");

            try
            {
                var buses = BusSideJobSystem.LoadBusVehicles(configDirectory);

                Assert.AreEqual(2, buses.Count);
                CollectionAssert.AreEqual(new[] { 0, 2 }, buses[0].DoorIndices.ToArray());
                Assert.AreEqual(0, buses[1].DoorIndices.Count, "a missing doorIndices attribute keeps the default pair");
            }
            finally
            {
                DeleteDirectory(configDirectory);
            }
        }

        [TestMethod]
        public void AllocateSeats_RespectsWaitingFreeSeatsAndTheBoardingCap()
        {
            // Nothing to board, no room, or no cap.
            Assert.AreEqual(0, BusSideJobSystem.AllocateSeats(0, 10, 6));
            Assert.AreEqual(0, BusSideJobSystem.AllocateSeats(-4, 10, 6));
            Assert.AreEqual(0, BusSideJobSystem.AllocateSeats(5, 0, 6));
            Assert.AreEqual(0, BusSideJobSystem.AllocateSeats(5, 5, 0));

            // Only the waiting passengers board when the bus is nearly empty.
            Assert.AreEqual(3, BusSideJobSystem.AllocateSeats(3, 30, 6));

            // The per-stop cap limits a full crowd.
            Assert.AreEqual(4, BusSideJobSystem.AllocateSeats(20, 30, BusSideJobSystem.MaxBoardPerStop));

            // Free seats limit the wave when the bus is nearly full.
            Assert.AreEqual(2, BusSideJobSystem.AllocateSeats(20, 2, 6));
        }

        [TestMethod]
        public void RollRideOffset_NeverReturnsZeroAndSpreadsAcrossTheRideBands()
        {
            var random = new Random(20260917);
            var oneStop = 0;
            var twoStops = 0;
            var longRides = 0;

            for (int i = 0; i < 20000; i++)
            {
                var offset = BusSideJobSystem.RollRideOffset(random);

                Assert.IsTrue(offset >= 1, "a ride is never zero stops");
                Assert.IsTrue(offset <= BusSideJobSystem.MaxRideStops, "offset {0} exceeded the rideable window", offset);

                if (offset == 1)
                {
                    oneStop += 1;
                }
                else if (offset == 2)
                {
                    twoStops += 1;
                }
                else
                {
                    longRides += 1;
                }
            }

            // Design shares: half ride one stop, three in ten ride two, the rest ride further.
            Assert.IsTrue(Math.Abs((oneStop / 20000.0) - BusSideJobSystem.OneStopRideShare) < 0.03, "one-stop share was {0}", oneStop / 20000.0);
            Assert.IsTrue(Math.Abs((twoStops / 20000.0) - BusSideJobSystem.TwoStopRideShare) < 0.03, "two-stop share was {0}", twoStops / 20000.0);
            Assert.IsTrue(longRides > 0, "some passengers must ride three stops or more");
            Assert.IsTrue(oneStop > twoStops && twoStops > longRides, "the bands must thin out with the ride length");
        }

        [TestMethod]
        public void ResolveDestinationIndex_ClampsOnOpenLinesAndWrapsOnLoops()
        {
            // Open line of 13 stations: the destination can never pass the terminus.
            Assert.AreEqual(3, BusSideJobSystem.ResolveDestinationIndex(0, 3, 13, false));
            Assert.AreEqual(12, BusSideJobSystem.ResolveDestinationIndex(11, 3, 13, false));
            Assert.AreEqual(12, BusSideJobSystem.ResolveDestinationIndex(12, 1, 13, false));

            // Closed loop of 13 entries (12 distinct stops): the offset wraps back to the origin.
            Assert.AreEqual(3, BusSideJobSystem.ResolveDestinationIndex(0, 3, 13, true));
            Assert.AreEqual(0, BusSideJobSystem.ResolveDestinationIndex(11, 1, 13, true));
            Assert.AreEqual(2, BusSideJobSystem.ResolveDestinationIndex(11, 3, 13, true));

            // Degenerate input never produces a negative or out-of-range destination.
            Assert.AreEqual(0, BusSideJobSystem.ResolveDestinationIndex(0, 1, 1, false));
            Assert.AreEqual(0, BusSideJobSystem.ResolveDestinationIndex(0, 1, 0, false));
        }

        [TestMethod]
        public void ResolveRiddenStops_NeverRidesPastTheEndOfTheLine()
        {
            var openLine = BuildSyntheticRoute("Open", new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K" });
            var loopLine = BuildSyntheticRoute("Loop", new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "A" });

            Assert.IsFalse(openLine.IsClosedLoop);
            Assert.IsTrue(loopLine.IsClosedLoop);

            // Open line: a passenger boarding at the last station rides exactly one stop, and a wild
            // offset is capped by the stations left before the terminus.
            Assert.AreEqual(1, BusSideJobSystem.ResolveRiddenStops(6, openLine.StationCount - 1, openLine, false));
            Assert.AreEqual(6, BusSideJobSystem.ResolveRiddenStops(6, 0, openLine, false));
            Assert.AreEqual(9, BusSideJobSystem.ResolveRiddenStops(99, 2, openLine, false));

            // Closed loop: the terminus is the origin station, so a passenger boarding there still has
            // a whole loop ahead and is capped by the distinct stops (StationCount - 1).
            Assert.AreEqual(6, BusSideJobSystem.ResolveRiddenStops(6, loopLine.StationCount - 1, loopLine, true));
            Assert.AreEqual(6, BusSideJobSystem.ResolveRiddenStops(6, 0, loopLine, true));
            Assert.AreEqual(loopLine.StationCount - 6, BusSideJobSystem.ResolveRiddenStops(99, 5, loopLine, true));
        }

        [TestMethod]
        public void RollWaitingPassengers_AlwaysLandsInsideConfiguredBand()
        {
            var random = new Random(20260917);
            var sawMinimum = false;
            var sawMaximum = false;

            for (int i = 0; i < 2000; i++)
            {
                var waiting = BusSideJobSystem.RollWaitingPassengers(random);

                Assert.IsTrue(waiting >= BusSideJobSystem.MinWaitingPassengers, "waiting {0} fell below the minimum", waiting);
                Assert.IsTrue(waiting <= BusSideJobSystem.MaxWaitingPassengers, "waiting {0} rose above the maximum", waiting);

                sawMinimum |= waiting == BusSideJobSystem.MinWaitingPassengers;
                sawMaximum |= waiting == BusSideJobSystem.MaxWaitingPassengers;
            }

            Assert.IsTrue(sawMinimum && sawMaximum, "waiting counts should spread across the band");
        }

        [TestMethod]
        public void ComputeFare_AddsThePerStationRateAndScalesWithTheMultiplier()
        {
            // 25 base fare + 5 stations at 12 each = 85.
            Assert.AreEqual(85f, BusSideJobSystem.ComputeFare(5, 1f), 0.001f);
            Assert.AreEqual(BusSideJobSystem.BaseFare, BusSideJobSystem.ComputeFare(0, 1f), 0.001f);
            Assert.AreEqual(37f, BusSideJobSystem.ComputeFare(1, 1f), 0.001f);
            Assert.AreEqual(102f, BusSideJobSystem.ComputeFare(5, 1.2f), 0.001f);

            // Guards against invalid input.
            Assert.AreEqual(85f, BusSideJobSystem.ComputeFare(5, 0f), 0.001f);
            Assert.AreEqual(BusSideJobSystem.BaseFare, BusSideJobSystem.ComputeFare(-3, 1f), 0.001f);
        }

        [TestMethod]
        public void FareEconomy_MidSizeLine_LandsInsideTheDesignedPayoutBand()
        {
            // Replays a mid-size line with a 30-seat bus on the pure helpers to check the money curve,
            // the same way the garbage job checks its full-route target.
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());
            var route = routes.First(entry => entry.RouteId == "DowntownVinewood");
            var random = new Random(20260917);

            const int seats = 30;
            var onBoardDestinations = new List<int>();
            var fares = 0f;

            for (int index = 0; index < route.StationCount; index++)
            {
                var isTerminus = index >= route.StationCount - 1;
                if (isTerminus)
                {
                    onBoardDestinations.Clear();
                    break;
                }

                var waiting = BusSideJobSystem.RollWaitingPassengers(random);
                if (route.Stations[index].PedSpawnPoints.Count > 0)
                {
                    // Same rule as the system: never ask for more passengers than the stop has spots.
                    waiting = Math.Min(waiting, route.Stations[index].PedSpawnPoints.Count);
                }

                for (int i = onBoardDestinations.Count - 1; i >= 0; i--)
                {
                    if (onBoardDestinations[i] == index)
                    {
                        onBoardDestinations.RemoveAt(i);
                    }
                }

                var boarded = BusSideJobSystem.AllocateSeats(
                    waiting,
                    Math.Max(0, seats - onBoardDestinations.Count),
                    BusSideJobSystem.MaxBoardPerStop);
                for (int i = 0; i < boarded; i++)
                {
                    var riddenStops = BusSideJobSystem.ResolveRiddenStops(
                        BusSideJobSystem.RollRideOffset(random),
                        index,
                        route,
                        route.IsClosedLoop);
                    var destination = BusSideJobSystem.ResolveDestinationIndex(
                        index,
                        riddenStops,
                        route.StationCount,
                        route.IsClosedLoop);
                    onBoardDestinations.Add(destination);
                    fares += BusSideJobSystem.ComputeFare(riddenStops, 1f);
                }
            }

            var total = fares + BusSideJobSystem.RouteCompletionBonus + BusSideJobSystem.PerfectRouteBonus;

            Assert.IsTrue(total >= 3000f, "a 14-station line should clear 3,000 cash, got {0}", total);
            Assert.IsTrue(total <= 12000f, "a 14-station line should stay below 12,000 cash, got {0}", total);
        }

        [TestMethod]
        public void ResolveDistrictName_PrefersContainmentAndFallsBackToTheNearestCentroid()
        {
            var districts = BusSideJobSystem.LoadDistricts(GetConfigDirectory());
            Assert.IsTrue(districts.Count >= 11, "Districts.xml must load its polygons");

            // Downtown Bus Depot sits in the documented coverage gap between SouthLosSantos and
            // Downtown: the ray-cast misses it, so the nearest centroid must decide.
            var depotDistrict = BusSideJobSystem.ResolveDistrictName(new Vector3(445.28f, -582.88f, 28.50f), districts);
            Assert.IsFalse(string.IsNullOrWhiteSpace(depotDistrict), "the depot must resolve to some district");

            // A point well inside Paleto Bay resolves to Paleto Bay.
            var paleto = districts.First(district => district.Name == "PaletoBay");
            var centroid = paleto.GetCentroid();
            var resolved = BusSideJobSystem.ResolveDistrictName(new Vector3(centroid.X, centroid.Y, 0f), districts);
            Assert.AreEqual("PaletoBay", resolved);

            Assert.AreEqual(string.Empty, BusSideJobSystem.ResolveDistrictName(Vector3.Zero, null));
            Assert.AreEqual(string.Empty, BusSideJobSystem.ResolveDistrictName(Vector3.Zero, new List<DistrictConfig>()));
        }

        [TestMethod]
        public void LoadRoutes_EveryStationResolvesToAKnownDistrict()
        {
            var districts = BusSideJobSystem.LoadDistricts(GetConfigDirectory());
            var knownNames = new HashSet<string>(districts.Select(district => district.Name), StringComparer.OrdinalIgnoreCase);
            var routes = BusSideJobSystem.LoadRoutes(GetConfigDirectory());

            foreach (var route in routes)
            {
                foreach (var station in route.Stations)
                {
                    var resolved = BusSideJobSystem.ResolveDistrictName(station.Position, districts);
                    Assert.IsTrue(
                        !string.IsNullOrWhiteSpace(resolved) && knownNames.Contains(resolved),
                        "route {0} station {1} ({2}) resolved to '{3}'",
                        route.RouteId,
                        station.Index,
                        station.Name,
                        resolved);
                }
            }
        }

        [TestMethod]
        public void BusDepotBlip_DoesNotReuseTheStationSprite()
        {
            // Stations use the classic yellow on-mission sprite (radar_on_mission = 271); the depot
            // uses the bus sprite so the two are never confused on the map.
            Assert.AreEqual(271, (int)BlipSprite.OnMission);
            Assert.AreNotEqual((int)BlipSprite.OnMission, (int)BlipSprite.Bus);
        }

        [TestMethod]
        public void BusSnapshot_WithoutRouteStationsOrGarage_HasNoData()
        {
            var snapshot = new BusPersistenceSnapshot
            {
                ActiveBusModelName = "bus",
                DoorsOpen = true,
            };

            Assert.IsFalse(snapshot.HasData);

            snapshot.RouteId = "DowntownVinewood";
            Assert.IsTrue(snapshot.HasData);

            snapshot.RouteId = string.Empty;
            snapshot.OnBoardRiders.Add(new BusRiderSnapshot { RiderId = 1, DestinationIndex = 4, RiddenStops = 2 });
            Assert.IsTrue(snapshot.HasData);

            snapshot.OnBoardRiders.Clear();
            snapshot.StationWaiting.Add(new BusStationWaitingSnapshot { StationIndex = 2, WaitingPassengers = 3 });
            Assert.IsTrue(snapshot.HasData);

            snapshot.StationWaiting.Clear();
            snapshot.StationsServiced = 1;
            Assert.IsTrue(snapshot.HasData);

            snapshot.StationsServiced = 0;
            snapshot.RouteCashEarned = 120f;
            Assert.IsTrue(snapshot.HasData);

            snapshot.RouteCashEarned = 0f;
            Assert.IsFalse(snapshot.HasData, "a parked bus with no earnings and no line is not save data");

            // A bus parked in the job garage is saved on its own, with no route running.
            var garageOnly = new BusPersistenceSnapshot();
            garageOnly.OwnedBusModels.Add("bus");
            Assert.IsTrue(garageOnly.HasData);
        }

        [TestMethod]
        public void Persistence_RoundTrip_RestoresBusSnapshotRidersAndStations()
        {
            var filePath = TestWorkspace.CreateTempFilePath("bus.state.xml");

            try
            {
                var bus = new BusPersistenceSnapshot
                {
                    RouteId = "DowntownVinewood",
                    CurrentStationIndex = 4,
                    StationsServiced = 4,
                    CompletedLoops = 1,
                    RoutePassengersDelivered = 9,
                    PassengersLeftBehind = 2,
                    RouteCashEarned = 1430.5f,
                    RouteXpEarned = 390f,
                    DoorsOpen = true,
                    ActiveBusModelName = "bus",
                };
                bus.OwnedBusModels.Add("bus");
                bus.OwnedBusModels.Add("coach");
                bus.OnBoardRiders.Add(new BusRiderSnapshot
                {
                    RiderId = 1,
                    BoardedStationIndex = 2,
                    DestinationIndex = 6,
                    RiddenStops = 4,
                    SeatIndex = 0,
                });
                bus.OnBoardRiders.Add(new BusRiderSnapshot
                {
                    RiderId = 2,
                    BoardedStationIndex = 3,
                    DestinationIndex = 8,
                    RiddenStops = 5,
                    SeatIndex = 1,
                });

                var station = new BusStationWaitingSnapshot { StationIndex = 4, WaitingPassengers = 2 };
                station.RideOffsets.Add(1);
                station.RideOffsets.Add(3);
                bus.StationWaiting.Add(station);

                var metadata = new IndustryPersistenceMetadata { Bus = bus };

                IndustryPersistenceManager.Save(filePath, Array.Empty<Industry>(), metadata, null);

                var rawSave = File.ReadAllText(filePath);
                StringAssert.Contains(rawSave, "<Section name=\"Bus\">");
                StringAssert.Contains(rawSave, "<Section name=\"Bus:Rider:1\">");
                StringAssert.Contains(rawSave, "<Section name=\"Bus:Rider:2\">");
                StringAssert.Contains(rawSave, "<Section name=\"Bus:Station:4\">");
                StringAssert.Contains(rawSave, "<Value key=\"Version\">33</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"RouteId\">DowntownVinewood</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"ActiveBusModelName\">bus</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"OwnedBuses\">bus,coach</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"DoorsOpen\">true</Value>");
                StringAssert.Contains(rawSave, "<Value key=\"RideOffsets\">1,3</Value>");

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());
                var restored = result.Metadata.Bus;

                Assert.IsNotNull(restored);
                Assert.IsTrue(result.Metadata.HasGameplayMetadata);
                Assert.AreEqual("DowntownVinewood", restored.RouteId);
                Assert.AreEqual(4, restored.CurrentStationIndex);
                Assert.AreEqual(4, restored.StationsServiced);
                Assert.AreEqual(1, restored.CompletedLoops);
                Assert.AreEqual(9, restored.RoutePassengersDelivered);
                Assert.AreEqual(2, restored.PassengersLeftBehind);
                Assert.AreEqual(1430.5f, restored.RouteCashEarned, 0.001f);
                Assert.AreEqual(390f, restored.RouteXpEarned, 0.001f);
                Assert.IsTrue(restored.DoorsOpen);
                Assert.AreEqual("bus", restored.ActiveBusModelName);
                CollectionAssert.AreEqual(new[] { "bus", "coach" }, restored.OwnedBusModels.ToArray());

                Assert.AreEqual(2, restored.OnBoardRiders.Count);
                Assert.AreEqual(1, restored.OnBoardRiders[0].RiderId);
                Assert.AreEqual(2, restored.OnBoardRiders[0].BoardedStationIndex);
                Assert.AreEqual(6, restored.OnBoardRiders[0].DestinationIndex);
                Assert.AreEqual(4, restored.OnBoardRiders[0].RiddenStops);
                Assert.AreEqual(0, restored.OnBoardRiders[0].SeatIndex);
                Assert.AreEqual(2, restored.OnBoardRiders[1].RiderId);
                Assert.AreEqual(5, restored.OnBoardRiders[1].RiddenStops);
                Assert.AreEqual(1, restored.OnBoardRiders[1].SeatIndex);

                Assert.AreEqual(1, restored.StationWaiting.Count);
                Assert.AreEqual(4, restored.StationWaiting[0].StationIndex);
                Assert.AreEqual(2, restored.StationWaiting[0].WaitingPassengers);
                CollectionAssert.AreEqual(new[] { 1, 3 }, restored.StationWaiting[0].RideOffsets.ToArray());
            }
            finally
            {
                DeleteDirectory(Path.GetDirectoryName(filePath));
            }
        }

        [TestMethod]
        public void Persistence_WithoutBusSection_ReturnsNullSnapshot()
        {
            var filePath = TestWorkspace.CreateTempFilePath("no-bus.state.xml");

            try
            {
                IndustryPersistenceManager.Save(
                    filePath,
                    Array.Empty<Industry>(),
                    new IndustryPersistenceMetadata { Profit = 1000f },
                    null);

                var result = IndustryPersistenceManager.LoadWithMetadata(filePath, Array.Empty<Industry>());

                Assert.IsNull(result.Metadata.Bus);
            }
            finally
            {
                DeleteDirectory(Path.GetDirectoryName(filePath));
            }
        }

        private static BusRouteDefinition BuildSyntheticRoute(string routeId, string[] stationNames)
        {
            var route = new BusRouteDefinition
            {
                RouteId = routeId,
                DisplayName = routeId,
                DistrictNames = new List<string> { "Downtown" },
            };

            var stations = new List<BusStationDefinition>();
            for (int i = 0; i < stationNames.Length; i++)
            {
                stations.Add(new BusStationDefinition
                {
                    Index = i,
                    Name = stationNames[i],
                    Position = new Vector3(i * 100f, 0f, 10f),
                });
            }

            route.Stations = stations;
            return route;
        }

        private static BusSideJobSystem CreateSystem(string configDirectory)
        {
            return new BusSideJobSystem(
                configDirectory,
                new PlayerSkillSystem(),
                message => { },
                amount => { },
                () => { });
        }

        private static string GetConfigDirectory()
        {
            return Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
        }

        private static string CreateTempConfigDirectory(string busRouteXml, string jobVehiclesXml = null, string coreXml = null)
        {
            var directory = Path.Combine(Path.GetTempPath(), "LSOL.Tests", Guid.NewGuid().ToString("N"));
            var sideJobsDirectory = Path.Combine(directory, "SideJobs");
            Directory.CreateDirectory(sideJobsDirectory);
            File.WriteAllText(Path.Combine(sideJobsDirectory, "BusRoute.xml"), busRouteXml);
            if (!string.IsNullOrWhiteSpace(jobVehiclesXml))
            {
                File.WriteAllText(Path.Combine(sideJobsDirectory, "JobVehicles.xml"), jobVehiclesXml);
            }

            if (!string.IsNullOrWhiteSpace(coreXml))
            {
                File.WriteAllText(Path.Combine(directory, "Core.xml"), coreXml);
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
