using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Config
{
    internal static class XmlConfigImport
    {
        public static CoreXmlConfig LoadCoreConfig(string configDirectory, ICollection<string> validationMessages)
        {
            var coreConfig = new CoreXmlConfig();
            var document = LoadDocument(
                configDirectory,
                "Core.xml",
                validationMessages,
                "Core.xml missing. Using built-in defaults for core settings.");
            if (document == null || document.Root == null)
            {
                return coreConfig;
            }

            var root = document.Root;
            var general = root.Element("General");
            if (general != null)
            {
                coreConfig.OmegaMultiplier = Math.Max(1f, ReadFloatAttribute(general, "omegaMultiplier", coreConfig.OmegaMultiplier));
                coreConfig.IndustryOmegaCapacityMultiplier = Math.Max(0.01f, ReadFloatAttribute(general, "industryOmegaCapacityMultiplier", coreConfig.IndustryOmegaCapacityMultiplier));
            }

            var markers = root.Element("Markers");
            if (markers != null)
            {
                coreConfig.MarkerRadius = Math.Max(0.2f, ReadFloatAttribute(markers, "radius", coreConfig.MarkerRadius));
                coreConfig.MarkerHeight = Math.Max(0.5f, ReadFloatAttribute(markers, "height", coreConfig.MarkerHeight));
            }

            coreConfig.MainOfficePosition = ReadRequiredVector3(root.Element("MainOffice"), coreConfig.MainOfficePosition);

            var vehicleSpawn = root.Element("VehicleSpawn");
            if (vehicleSpawn != null)
            {
                coreConfig.VehicleSpawnPosition = ReadRequiredVector3(vehicleSpawn, coreConfig.VehicleSpawnPosition);
                coreConfig.VehicleSpawnHeading = ReadFloatAttribute(vehicleSpawn, "heading", coreConfig.VehicleSpawnHeading);
            }

            var controls = root.Element("Controls");
            if (controls != null)
            {
                coreConfig.Controls.ToggleDashboard = ControlBindings.ParseOrDefault(ReadAttribute(controls, "toggleDashboard"), coreConfig.Controls.ToggleDashboard);
                coreConfig.Controls.OpenModMenu = ControlBindings.ParseOrDefault(ReadAttribute(controls, "openModMenu"), coreConfig.Controls.OpenModMenu);
                coreConfig.Controls.OpenDebugMenu = ControlBindings.ParseOrDefault(ReadAttribute(controls, "openDebugMenu"), coreConfig.Controls.OpenDebugMenu);
                coreConfig.Controls.Interact = ControlBindings.ParseOrDefault(ReadAttribute(controls, "interact"), coreConfig.Controls.Interact);
                coreConfig.Controls.GateInteract = ControlBindings.ParseOrDefault(ReadAttribute(controls, "gateInteract"), coreConfig.Controls.GateInteract);
                coreConfig.Controls.OpenUpgrade = ControlBindings.ParseOrDefault(ReadAttribute(controls, "openUpgrade"), coreConfig.Controls.OpenUpgrade);
                coreConfig.Controls.MenuUp = ControlBindings.ParseOrDefault(ReadAttribute(controls, "menuUp"), coreConfig.Controls.MenuUp);
                coreConfig.Controls.MenuDown = ControlBindings.ParseOrDefault(ReadAttribute(controls, "menuDown"), coreConfig.Controls.MenuDown);
                coreConfig.Controls.MenuLeft = ControlBindings.ParseOrDefault(ReadAttribute(controls, "menuLeft"), coreConfig.Controls.MenuLeft);
                coreConfig.Controls.MenuRight = ControlBindings.ParseOrDefault(ReadAttribute(controls, "menuRight"), coreConfig.Controls.MenuRight);
                coreConfig.Controls.MenuSelect = ControlBindings.ParseOrDefault(ReadAttribute(controls, "menuSelect"), coreConfig.Controls.MenuSelect);
                coreConfig.Controls.MenuBack = ControlBindings.ParseOrDefault(ReadAttribute(controls, "menuBack", "Backspace"), coreConfig.Controls.MenuBack);
            }

            var densityProfiles = root.Element("DensityProfiles");
            if (densityProfiles != null)
            {
                PopulateDensityProfile(densityProfiles.Element("Stores"), coreConfig.StoreDensityProfile);
                PopulateDensityProfile(densityProfiles.Element("GasStations"), coreConfig.GasStationDensityProfile);
            }

            var workers = root.Element("Workers");
            if (workers != null)
            {
                var workerModels = workers.Elements("Model")
                    .Select(element => ReadAttribute(element, "name"))
                    .Where(model => !string.IsNullOrWhiteSpace(model))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (workerModels.Count > 0)
                {
                    coreConfig.WorkerModels.Clear();
                    coreConfig.WorkerModels.AddRange(workerModels);
                }
            }

            return coreConfig;
        }

        public static bool TryPopulateResources(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Resources.xml",
                catalog.ValidationMessages,
                "Resources.xml missing. Commodity groups will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Group"))
            {
                var groupName = ReadAttribute(element, "name");
                var cargoType = ParseCargoType(ReadAttribute(element, "cargoType", groupName));
                var commodities = ParseCommodityList(ReadAttribute(element, "commodities"), catalog.ValidationMessages, "Resources.xml", groupName);
                if (string.IsNullOrWhiteSpace(groupName) || cargoType == VehicleCargoType.Unknown || commodities.Count == 0)
                {
                    catalog.ValidationMessages.Add("Resources.xml contains a group with missing or invalid attributes.");
                    continue;
                }

                catalog.ResourceGroups.Add(new ResourceGroupConfig
                {
                    Name = groupName,
                    CargoType = cargoType,
                    Commodities = commodities,
                });

                for (int i = 0; i < commodities.Count; i++)
                {
                    catalog.ResourcesByCommodity[commodities[i]] = new ExternalResourceConfig
                    {
                        Commodity = commodities[i],
                        GroupName = groupName,
                        CargoType = cargoType,
                    };
                }
            }

            return catalog.ResourceGroups.Count > 0;
        }

        public static bool TryPopulateObjects(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Objects.xml",
                catalog.ValidationMessages,
                "Objects.xml missing. Cargo prop aliases will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Group"))
            {
                var key = ReadAttribute(element, "key");
                var modelNames = SplitCsv(ReadAttribute(element, "models"))
                    .Where(model => !string.IsNullOrWhiteSpace(model))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (string.IsNullOrWhiteSpace(key) || modelNames.Count == 0)
                {
                    catalog.ValidationMessages.Add("Objects.xml contains a group with missing key or models.");
                    continue;
                }

                catalog.ObjectModels[key] = modelNames;
            }

            return catalog.ObjectModels.Count > 0;
        }

        public static bool TryPopulateDistricts(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Districts.xml",
                catalog.ValidationMessages,
                "Districts.xml missing. Territory control will have limited fidelity.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("District"))
            {
                var districtId = ReadAttribute(element, "id");
                var districtName = ReadAttribute(element, "name", districtId);
                if (string.IsNullOrWhiteSpace(districtName))
                {
                    catalog.ValidationMessages.Add("Districts.xml contains a district with no name.");
                    continue;
                }

                var district = new DistrictConfig
                {
                    Id = string.IsNullOrWhiteSpace(districtId) ? districtName : districtId,
                    Name = districtName,
                };

                foreach (var point in element.Elements("Point"))
                {
                    float x;
                    float y;
                    if (!TryReadFloatAttribute(point, "x", out x) || !TryReadFloatAttribute(point, "y", out y))
                    {
                        continue;
                    }

                    district.PolygonVertices.Add(new Vector2(x, y));
                }

                if (district.PolygonVertices.Count < 3)
                {
                    catalog.ValidationMessages.Add(string.Format("Districts.xml district '{0}' has fewer than 3 polygon points.", districtName));
                    continue;
                }

                catalog.Districts[district.Name] = district;
            }

            return catalog.Districts.Count > 0;
        }

        public static bool TryPopulateSites(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Sites.xml",
                catalog.ValidationMessages,
                "Sites.xml missing. No site catalog loaded from LSOL_Config.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Site"))
            {
                var legacyKey = ReadAttribute(element, "legacyKey");
                if (string.IsNullOrWhiteSpace(legacyKey))
                {
                    catalog.ValidationMessages.Add("Sites.xml contains a site with no legacyKey.");
                    continue;
                }

                var location = new ExternalLocationConfig
                {
                    CatalogId = ReadAttribute(element, "catalogId"),
                    Id = ReadAttribute(element, "id", legacyKey),
                    LegacyKey = legacyKey,
                    Name = ReadAttribute(element, "name", legacyKey),
                    SiteRole = SiteMetadataParser.ParseRole(ReadAttribute(element, "role")),
                    OwnershipTier = SiteMetadataParser.ParseOwnershipTier(ReadAttribute(element, "ownershipTier")),
                    DistrictName = ReadAttribute(element, "district"),
                    Company = ReadAttribute(element, "company"),
                    Enabled = ReadBoolAttribute(element, "enabled", true),
                    IndustryOwnerCut = Clamp01(ReadFloatAttribute(element, "industryOwnerCut", 0.5f)),
                    StartingTankRatio = Clamp01(ReadFloatAttribute(element, "startingTankRatio", 0f)),
                    Density = NormalizeDensity(ReadAttribute(element, "density"), string.Empty),
                    RefuelIsFree = ReadBoolAttribute(element, "refuelIsFree", false),
                    DisplayObjectsAtGroundLevel = ReadBoolAttribute(element, "groundLevel", ReadBoolAttribute(element, "displayObjectsAtGroundLevel", false)),
                    ObjectToDelete = ReadAttribute(element, "objectToDelete"),
                    Position = ReadRequiredVector3(element.Element("Marker"), Vector3.Zero),
                    VehicleSpawnPosition = ReadOptionalVector3(element.Element("VehicleSpawn")),
                    VehicleSpawnHeading = ReadOptionalFloatAttribute(element.Element("VehicleSpawn"), "heading"),
                    SpawnedVehiclePosition = ReadOptionalVector3(element.Element("DisplaySpawn")),
                    SpawnedVehicleHeading = ReadOptionalFloatAttribute(element.Element("DisplaySpawn"), "heading"),
                    GatePosition = ReadOptionalVector3(element.Element("Gate")),
                    BarrierModelHash = ReadOptionalIntAttribute(element.Element("Gate"), "barrierModelHash"),
                    WorkerPosition = ReadOptionalVector3(element.Element("Worker")),
                    DisplayObjectModelHash = ReadOptionalIntAttribute(element.Element("Display"), "objectModelHash"),
                    MaxSpawnedVehiclesLine = ReadOptionalIntAttribute(element.Element("Display"), "maxLine"),
                    MaxSpawnedVehiclesRow = ReadOptionalIntAttribute(element.Element("Display"), "maxRow"),
                };

                var inputsElement = element.Element("Inputs");
                location.Inputs = ParseCommoditySet(ReadAttribute(inputsElement, "primary"), catalog.ValidationMessages, "Sites.xml", legacyKey);
                location.OptionalInputs = ParseCommoditySet(ReadAttribute(inputsElement, "optional"), catalog.ValidationMessages, "Sites.xml", legacyKey);
                location.Outputs = ParseCommoditySet(ReadAttribute(inputsElement, "outputs"), catalog.ValidationMessages, "Sites.xml", legacyKey);
                location.Kind = ResolveLocationKind(location.SiteRole, location.Inputs, location.Outputs);

                if (!TryReadFloatAttribute(element, "emptyingRate", out var emptyingRate))
                {
                    location.EmptyingRate = InferEmptyingRate(location.Density);
                    location.HasConfiguredEmptyingRate = false;
                }
                else
                {
                    location.EmptyingRate = emptyingRate;
                    location.HasConfiguredEmptyingRate = true;
                }

                var economyElement = element.Element("Economy");
                if (economyElement != null)
                {
                    foreach (var presetElement in economyElement.Elements())
                    {
                        var presetName = presetElement.Name.LocalName;
                        if (presetName.Equals("Preset", StringComparison.OrdinalIgnoreCase))
                        {
                            presetName = ReadAttribute(presetElement, "name");
                        }

                        var preset = ReadPreset(presetElement);
                        if (presetName.Equals("Casual", StringComparison.OrdinalIgnoreCase))
                        {
                            location.CasualEconomy = preset;
                        }
                        else if (presetName.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                        {
                            location.StandardEconomy = preset;
                        }
                        else if (presetName.Equals("Hardcore", StringComparison.OrdinalIgnoreCase))
                        {
                            location.HardcoreEconomy = preset;
                        }
                    }
                }

                location.FactoryProductionRatio = Math.Max(
                    0.1f,
                    location.StandardEconomy != null && location.StandardEconomy.ProductionRatio > 0f
                        ? location.StandardEconomy.ProductionRatio
                        : 1f);
                location.IndustryPrice = location.StandardEconomy != null
                    ? Math.Max(0f, location.StandardEconomy.PurchasePrice)
                    : 0f;

                if (string.IsNullOrWhiteSpace(location.DistrictName) || !catalog.Districts.ContainsKey(location.DistrictName))
                {
                    catalog.ValidationMessages.Add(string.Format("Sites.xml site '{0}' references unknown district '{1}'.", location.LegacyKey, location.DistrictName));
                }

                if (location.Position == Vector3.Zero)
                {
                    catalog.ValidationMessages.Add(string.Format("Sites.xml site '{0}' is missing marker coordinates.", location.LegacyKey));
                }

                catalog.Locations[location.Id] = location;
            }

            return catalog.Locations.Count > 0;
        }

        public static bool TryPopulateVehicles(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Vehicles.xml",
                catalog.ValidationMessages,
                "Vehicles.xml missing. Commercial vehicle catalog will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Vehicle"))
            {
                var modelName = ReadAttribute(element, "model");
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    catalog.ValidationMessages.Add("Vehicles.xml contains a vehicle entry with no model.");
                    continue;
                }

                var displayName = ReadAttribute(element, "name", modelName);
                var vehicleType = ReadAttribute(element, "type");
                var acceptedCommodities = ParseCommoditySet(ReadAttribute(element, "acceptedResources"), catalog.ValidationMessages, "Vehicles.xml", modelName);
                var acceptedCargoTypes = acceptedCommodities
                    .Select(CommodityCatalog.GetCargoTypeForCommodity)
                    .Where(cargoType => cargoType != VehicleCargoType.Unknown && cargoType != VehicleCargoType.Trailer)
                    .Distinct()
                    .ToList();
                var capacity = Math.Max(0f, ReadFloatAttribute(element, "capacityTons", 0f));
                var isTrailer = vehicleType.IndexOf("Trailer", StringComparison.OrdinalIgnoreCase) >= 0;
                var isTractor = capacity <= 0f
                    || vehicleType.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) >= 0 && acceptedCommodities.Count == 0;
                var fuelCapacityLiters = ReadFloatAttribute(element, "fuelCapacityLiters", float.NaN);
                if (float.IsNaN(fuelCapacityLiters))
                {
                    fuelCapacityLiters = ResolveDefaultFuelCapacityLiters(vehicleType, capacity, isTractor, isTrailer);
                }

                catalog.VehicleDefinitions.Add(new VehicleDefinition
                {
                    Id = ReadAttribute(element, "id"),
                    SectionName = vehicleType,
                    DisplayName = displayName,
                    ModelName = modelName,
                    CargoType = ResolvePrimaryCargoType(acceptedCargoTypes, vehicleType, capacity),
                    AcceptedCommodities = acceptedCommodities,
                    CapacityTons = capacity,
                    FuelCapacityLiters = Math.Max(0f, fuelCapacityLiters),
                    Price = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                    DailyRent = Math.Max(0f, ReadFloatAttribute(element, "dailyRent", 0f)),
                    IsEnabled = ReadBoolAttribute(element, "enabled", capacity > 0f || isTractor),
                    IsTrailer = isTrailer && !isTractor,
                    IsTractor = isTractor,
                });
            }

            return catalog.VehicleDefinitions.Count > 0;
        }

        public static bool TryPopulateOffices(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Offices.xml",
                catalog.ValidationMessages,
                "Offices.xml missing. Office ownership progression will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Office"))
            {
                var officeId = ReadAttribute(element, "id");
                var siteName = ReadAttribute(element, "name", officeId);
                var markerPosition = ReadOptionalVector3(element.Element("Marker"));
                var spawnElement = element.Element("Spawn");
                var spawnPosition = ReadOptionalVector3(spawnElement);
                if (string.IsNullOrWhiteSpace(officeId) || string.IsNullOrWhiteSpace(siteName) || !markerPosition.HasValue || !spawnPosition.HasValue)
                {
                    catalog.ValidationMessages.Add("Offices.xml contains an office with missing id, name, marker, or spawn coordinates.");
                    continue;
                }

                catalog.OfficeDefinitions.Add(new OfficeDefinition
                {
                    OfficeId = officeId,
                    LegacyKey = ReadAttribute(element, "legacyKey"),
                    SiteName = siteName,
                    DistrictName = ReadAttribute(element, "district"),
                    MarkerPosition = markerPosition.Value,
                    SpawnPosition = spawnPosition.Value,
                    SpawnHeading = ReadFloatAttribute(spawnElement, "heading", 0f),
                    GatePosition = ReadOptionalVector3(element.Element("Gate")),
                    BarrierModelHash = ReadOptionalIntAttribute(element.Element("Gate"), "barrierModelHash"),
                    WorkerPosition = ReadOptionalVector3(element.Element("Worker")),
                    OfficePrice = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                    WeeklyOfficeRent = Math.Max(0f, ReadFloatAttribute(element, "weeklyRent", 0f)),
                    MaxCommercialVehicles = Math.Max(0, ReadIntAttribute(element, "maxCommercialVehicles", 0)),
                    Description = ReadElementValue(element.Element("Description")),
                });
            }

            return catalog.OfficeDefinitions.Count > 0;
        }

        public static bool TryPopulateOfficeObjects(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "OfficeObjects.xml",
                catalog.ValidationMessages,
                "OfficeObjects.xml missing. Office object catalog will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Object"))
            {
                var objectId = ReadIntAttribute(element, "id", 0);
                var displayName = ReadAttribute(element, "name");
                var modelName = ReadAttribute(element, "model");
                int modelHash;
                var hasModelHash = TryReadHashAttribute(element, "hash", out modelHash);
                var size = ParseOfficeObjectSize(ReadAttribute(element, "size"));
                var function = ParseOfficeObjectFunction(ReadAttribute(element, "function"));
                var resourceType = CommodityCatalog.Normalize(ReadAttribute(element, "resource"));
                if (resourceType.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    resourceType = string.Empty;
                }

                if (objectId <= 0 || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(modelName) || !hasModelHash)
                {
                    catalog.ValidationMessages.Add("OfficeObjects.xml contains an object with missing id, name, model, or hash.");
                    continue;
                }

                catalog.OfficeObjectDefinitions.Add(new OfficeObjectDefinition
                {
                    ObjectId = objectId,
                    DisplayName = displayName,
                    ModelName = modelName,
                    ModelHash = modelHash,
                    Size = size,
                    Function = function,
                    ResourceType = resourceType,
                    Capacity = Math.Max(0f, ReadFloatAttribute(element, "capacity", 0f)),
                    PerOfficeLimit = Math.Max(0, ReadIntAttribute(element, "limit", 0)),
                    Price = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                });
            }

            return catalog.OfficeObjectDefinitions.Count > 0;
        }

        public static bool TryPopulateInteriors(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Interiors.xml",
                catalog.ValidationMessages,
                "Interiors.xml missing. Apartment ownership progression will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Interior"))
            {
                var interiorId = ReadAttribute(element, "id");
                var interiorName = ReadAttribute(element, "name", interiorId);
                var interiorPosition = ReadOptionalVector3(element.Element("InteriorPosition"));
                var exteriorPosition = ReadOptionalVector3(element.Element("ExteriorPosition"));
                var garagePosition = ReadOptionalVector3(element.Element("GaragePosition"));
                if (string.IsNullOrWhiteSpace(interiorId) || string.IsNullOrWhiteSpace(interiorName) || !interiorPosition.HasValue || !exteriorPosition.HasValue || !garagePosition.HasValue)
                {
                    catalog.ValidationMessages.Add("Interiors.xml contains an interior with missing id, name, or coordinates.");
                    continue;
                }

                catalog.InteriorDefinitions.Add(new InteriorDefinition
                {
                    InteriorId = interiorId,
                    InteriorName = interiorName,
                    InteriorIgName = ReadAttribute(element, "igName"),
                    InteriorPosition = interiorPosition.Value,
                    InteriorType = ReadAttribute(element, "type"),
                    InteriorPrice = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                    InteriorWeeklyRent = Math.Max(0f, ReadFloatAttribute(element, "weeklyRent", 0f)),
                    ExteriorPosition = exteriorPosition.Value,
                    GaragePosition = garagePosition.Value,
                });
            }

            return catalog.InteriorDefinitions.Count > 0;
        }

        public static bool TryPopulateDealershipVehicles(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Dealership.xml",
                catalog.ValidationMessages,
                "Dealership.xml missing. Personal vehicle dealership will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            var modelNodes = document.Root.Element("Models");
            if (modelNodes == null)
            {
                catalog.ValidationMessages.Add("Dealership.xml is missing the Models section.");
                return false;
            }

            foreach (var element in modelNodes.Elements("Model"))
            {
                var displayName = ReadAttribute(element, "name");
                var modelName = ReadAttribute(element, "model");
                var category = ReadAttribute(element, "category");
                var price = ReadFloatAttribute(element, "price", float.NaN);
                if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(displayName))
                {
                    catalog.ValidationMessages.Add("Dealership.xml contains a model entry with a missing name or model attribute.");
                    continue;
                }

                if (float.IsNaN(price))
                {
                    catalog.ValidationMessages.Add(string.Format("Dealership.xml vehicle '{0}' has an invalid price.", displayName));
                    continue;
                }

                catalog.PersonalVehicleDefinitions.Add(new DealershipVehicleDefinition
                {
                    VehicleId = modelName.Trim(),
                    DisplayName = displayName.Trim(),
                    ModelName = modelName.Trim(),
                    Category = category,
                    Price = Math.Max(0f, price),
                });
            }

            return catalog.PersonalVehicleDefinitions.Count > 0;
        }

        private static void PopulateDensityProfile(XElement element, XmlDensityProfileConfig profile)
        {
            if (element == null || profile == null)
            {
                return;
            }

            profile.VeryLowCapacity = Math.Max(0f, ReadFloatAttribute(element, "veryLowCapacity", profile.VeryLowCapacity));
            profile.LowCapacity = Math.Max(0f, ReadFloatAttribute(element, "lowCapacity", profile.LowCapacity));
            profile.MediumCapacity = Math.Max(0f, ReadFloatAttribute(element, "mediumCapacity", profile.MediumCapacity));
            profile.HighCapacity = Math.Max(0f, ReadFloatAttribute(element, "highCapacity", profile.HighCapacity));
            profile.VeryLowEmptyingRate = Math.Max(0f, ReadFloatAttribute(element, "veryLowEmptyingRate", profile.VeryLowEmptyingRate));
            profile.LowEmptyingRate = Math.Max(0f, ReadFloatAttribute(element, "lowEmptyingRate", profile.LowEmptyingRate));
            profile.MediumEmptyingRate = Math.Max(0f, ReadFloatAttribute(element, "mediumEmptyingRate", profile.MediumEmptyingRate));
            profile.HighEmptyingRate = Math.Max(0f, ReadFloatAttribute(element, "highEmptyingRate", profile.HighEmptyingRate));
        }

        private static SiteEconomyPresetValues ReadPreset(XElement element)
        {
            if (element == null)
            {
                return null;
            }

            return SiteEconomyPresetValues.Create(
                Math.Max(0f, ReadFloatAttribute(element, "productionRate", 0f)),
                Math.Max(0f, ReadFloatAttribute(element, "licencePrice", ReadFloatAttribute(element, "licensePrice", 0f))),
                Math.Max(0f, ReadFloatAttribute(element, "purchasePrice", 0f)),
                Math.Max(0f, ReadFloatAttribute(element, "inputCapacityTons", 0f)),
                Math.Max(0f, ReadFloatAttribute(element, "outputCapacityTons", 0f)),
                Math.Max(0f, ReadFloatAttribute(element, "productionRatio", 0f)),
                ReadBoolAttribute(element, "permitRequired", false));
        }

        private static XDocument LoadDocument(string configDirectory, string fileName, ICollection<string> validationMessages, string missingMessage)
        {
            var filePath = Path.Combine(configDirectory ?? string.Empty, fileName ?? string.Empty);
            if (!File.Exists(filePath))
            {
                validationMessages?.Add(missingMessage);
                return null;
            }

            try
            {
                return XDocument.Load(filePath, LoadOptions.None);
            }
            catch (Exception ex)
            {
                validationMessages?.Add(string.Format("{0} could not be loaded: {1}", fileName, ex.Message));
                return null;
            }
        }

        private static HashSet<string> ParseCommoditySet(string raw, ICollection<string> validationMessages, string sourceName, string context)
        {
            return new HashSet<string>(ParseCommodityList(raw, validationMessages, sourceName, context), StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> ParseCommodityList(string raw, ICollection<string> validationMessages, string sourceName, string context)
        {
            var result = new List<string>();
            foreach (var part in SplitCsv(raw))
            {
                var normalized = CommodityCatalog.Normalize(part);
                if (string.IsNullOrWhiteSpace(normalized) || normalized.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(normalized);
                }

                if (!CommodityCatalog.IsKnownCommodity(normalized))
                {
                    validationMessages?.Add(string.Format("{0} '{1}' references unknown commodity '{2}'.", sourceName, context, part.Trim()));
                }
            }

            return result;
        }

        private static IEnumerable<string> SplitCsv(string raw)
        {
            return (raw ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string ReadAttribute(XElement element, string name, string fallback = "")
        {
            return element != null && element.Attribute(name) != null
                ? (element.Attribute(name).Value ?? string.Empty).Trim()
                : fallback;
        }

        private static string ReadElementValue(XElement element, string fallback = "")
        {
            return element != null
                ? (element.Value ?? string.Empty).Trim()
                : fallback;
        }

        private static bool ReadBoolAttribute(XElement element, string name, bool fallback)
        {
            if (element == null || element.Attribute(name) == null)
            {
                return fallback;
            }

            bool parsed;
            return bool.TryParse(element.Attribute(name).Value, out parsed)
                ? parsed
                : fallback;
        }

        private static float ReadFloatAttribute(XElement element, string name, float fallback)
        {
            float parsed;
            return TryReadFloatAttribute(element, name, out parsed)
                ? parsed
                : fallback;
        }

        private static bool TryReadFloatAttribute(XElement element, string name, out float value)
        {
            value = 0f;
            if (element == null || element.Attribute(name) == null)
            {
                return false;
            }

            var raw = element.Attribute(name).Value;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static int ReadIntAttribute(XElement element, string name, int fallback)
        {
            int parsed;
            return TryReadIntAttribute(element, name, out parsed)
                ? parsed
                : fallback;
        }

        private static int? ReadOptionalIntAttribute(XElement element, string name)
        {
            int parsed;
            return TryReadIntAttribute(element, name, out parsed)
                ? (int?)parsed
                : null;
        }

        private static bool TryReadIntAttribute(XElement element, string name, out int value)
        {
            value = 0;
            if (element == null || element.Attribute(name) == null)
            {
                return false;
            }

            var raw = element.Attribute(name).Value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
        }

        private static bool TryReadHashAttribute(XElement element, string name, out int value)
        {
            value = 0;
            if (element == null || element.Attribute(name) == null)
            {
                return false;
            }

            var raw = element.Attribute(name).Value;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var normalized = raw.Trim();
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                uint hexValue;
                if (uint.TryParse(normalized.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out hexValue))
                {
                    value = unchecked((int)hexValue);
                    return true;
                }
            }

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(normalized, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            uint unsignedValue;
            if (uint.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out unsignedValue)
                || uint.TryParse(normalized, NumberStyles.Integer, CultureInfo.CurrentCulture, out unsignedValue))
            {
                value = unchecked((int)unsignedValue);
                return true;
            }

            return false;
        }

        private static float? ReadOptionalFloatAttribute(XElement element, string name)
        {
            float parsed;
            return TryReadFloatAttribute(element, name, out parsed)
                ? (float?)parsed
                : null;
        }

        private static Vector3 ReadRequiredVector3(XElement element, Vector3 fallback)
        {
            var vector = ReadOptionalVector3(element);
            return vector ?? fallback;
        }

        private static Vector3? ReadOptionalVector3(XElement element)
        {
            if (element == null)
            {
                return null;
            }

            float x;
            float y;
            float z;
            if (!TryReadFloatAttribute(element, "x", out x) || !TryReadFloatAttribute(element, "y", out y) || !TryReadFloatAttribute(element, "z", out z))
            {
                return null;
            }

            return new Vector3(x, y, z);
        }

        private static VehicleCargoType ParseCargoType(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace(" ", string.Empty);
            if (normalized.Equals("Aggregates", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Loose", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Aggregates;
            }

            if (normalized.Equals("OpenHull", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Solid", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.OpenHull;
            }

            if (normalized.Equals("Wood", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Wood;
            }

            if (normalized.Equals("CraftedGoods", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Crate", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.CraftedGoods;
            }

            if (normalized.Equals("Liquid", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Fluid", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Liquid;
            }

            if (normalized.Equals("DryBulk", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.DryBulk;
            }

            if (normalized.Equals("Refrigeration", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Refrigeration;
            }

            if (normalized.Equals("Recyclable", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Recyclables", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Recyclable;
            }

            if (normalized.Equals("Vehicles", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Vehicles;
            }

            if (normalized.Equals("Trailer", StringComparison.OrdinalIgnoreCase))
            {
                return VehicleCargoType.Trailer;
            }

            return VehicleCargoType.Unknown;
        }

        private static OfficeObjectSize ParseOfficeObjectSize(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim();
            if (normalized.Equals("Medium", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectSize.Medium;
            }

            if (normalized.Equals("Big", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Large", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectSize.Big;
            }

            return OfficeObjectSize.Small;
        }

        private static OfficeObjectFunction ParseOfficeObjectFunction(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace(" ", string.Empty);
            if (normalized.Equals("Refuel", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectFunction.Refuel;
            }

            if (normalized.Equals("Repair", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectFunction.Repair;
            }

            if (normalized.Equals("Npc", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectFunction.Npc;
            }

            return OfficeObjectFunction.Decorative;
        }

        private static ExternalLocationKind ResolveLocationKind(SiteRole role, HashSet<string> inputs, HashSet<string> outputs)
        {
            if (role == SiteRole.StoreSink)
            {
                return ExternalLocationKind.Store;
            }

            if (role == SiteRole.FuelSink)
            {
                return ExternalLocationKind.GasStation;
            }

            var hasFuelOnlyInput = inputs != null && inputs.Count == 1 && inputs.Contains("Fuel");
            if ((outputs == null || outputs.Count == 0) && hasFuelOnlyInput)
            {
                return ExternalLocationKind.GasStation;
            }

            return ExternalLocationKind.Industry;
        }

        private static VehicleCargoType ResolvePrimaryCargoType(IReadOnlyList<VehicleCargoType> acceptedCargoTypes, string vehicleType, float capacity)
        {
            if (acceptedCargoTypes != null && acceptedCargoTypes.Count == 1)
            {
                return acceptedCargoTypes[0];
            }

            if (acceptedCargoTypes != null && acceptedCargoTypes.Count > 1)
            {
                var preferredOrder = new[]
                {
                    VehicleCargoType.Aggregates,
                    VehicleCargoType.OpenHull,
                    VehicleCargoType.Wood,
                    VehicleCargoType.CraftedGoods,
                    VehicleCargoType.Liquid,
                    VehicleCargoType.DryBulk,
                    VehicleCargoType.Refrigeration,
                    VehicleCargoType.Recyclable,
                    VehicleCargoType.Vehicles,
                };

                for (int i = 0; i < preferredOrder.Length; i++)
                {
                    if (acceptedCargoTypes.Contains(preferredOrder[i]))
                    {
                        return preferredOrder[i];
                    }
                }
            }

            if ((vehicleType ?? string.Empty).IndexOf("Trailer", StringComparison.OrdinalIgnoreCase) >= 0 && capacity <= 0f)
            {
                return VehicleCargoType.Trailer;
            }

            return VehicleCargoType.CraftedGoods;
        }

        private static float ResolveDefaultFuelCapacityLiters(string vehicleType, float capacityTons, bool isTractor, bool isTrailer)
        {
            if (isTrailer)
            {
                return 0f;
            }

            var normalizedType = (vehicleType ?? string.Empty).Trim();
            if (isTractor)
            {
                return 400f;
            }

            if (normalizedType.IndexOf("Van", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 90f;
            }

            if (normalizedType.IndexOf("BigTruck", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 220f;
            }

            if (normalizedType.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedType.IndexOf("Mixer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedType.IndexOf("Dumper", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return capacityTons >= 18f ? 200f : 150f;
            }

            return capacityTons >= 8f ? 120f : 70f;
        }

        private static string NormalizeDensity(string raw, string fallback)
        {
            var normalized = (raw ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                normalized = (fallback ?? string.Empty).Trim();
            }

            if (normalized.Length == 0)
            {
                return "Medium";
            }

            if (normalized.Equals("very low", StringComparison.OrdinalIgnoreCase) || normalized.Equals("verylow", StringComparison.OrdinalIgnoreCase))
            {
                return "VeryLow";
            }

            if (normalized.Equals("low", StringComparison.OrdinalIgnoreCase))
            {
                return "Low";
            }

            if (normalized.Equals("high", StringComparison.OrdinalIgnoreCase))
            {
                return "High";
            }

            if (normalized.Equals("very high", StringComparison.OrdinalIgnoreCase) || normalized.Equals("veryhigh", StringComparison.OrdinalIgnoreCase))
            {
                return "VeryHigh";
            }

            return "Medium";
        }

        private static float InferEmptyingRate(string density)
        {
            var normalizedDensity = NormalizeDensity(density, string.Empty);
            if (normalizedDensity.Equals("VeryLow", StringComparison.OrdinalIgnoreCase))
            {
                return 0.35f;
            }

            if (normalizedDensity.Equals("Low", StringComparison.OrdinalIgnoreCase))
            {
                return 0.8f;
            }

            if (normalizedDensity.Equals("High", StringComparison.OrdinalIgnoreCase))
            {
                return 4f;
            }

            return 2.25f;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}