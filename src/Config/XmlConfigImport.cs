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
        private const float WeeklyPassiveIncomeThroughputMultiplier = 500f;

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

            return PopulateResourcesDocument(document, catalog, "Resources.xml", false);
        }

        public static bool TryPopulateAddonResources(LsolAddonCatalog addonCatalog, ExternalConfigCatalog catalog)
        {
            return TryPopulateAddonDocuments(addonCatalog, catalog, LsolAddonCatalog.CapabilityResources, PopulateResourcesDocument);
        }

        public static bool TryPopulateAddonSites(LsolAddonCatalog addonCatalog, ExternalConfigCatalog catalog)
        {
            return TryPopulateAddonDocuments(addonCatalog, catalog, LsolAddonCatalog.CapabilitySites, PopulateSitesDocument);
        }

        public static bool TryPopulateAddonVehicles(LsolAddonCatalog addonCatalog, ExternalConfigCatalog catalog)
        {
            return TryPopulateAddonDocuments(addonCatalog, catalog, LsolAddonCatalog.CapabilityVehicles, PopulateVehiclesDocument);
        }

        public static bool TryPopulateAddonOfficeObjects(LsolAddonCatalog addonCatalog, ExternalConfigCatalog catalog)
        {
            return TryPopulateAddonDocuments(addonCatalog, catalog, LsolAddonCatalog.CapabilityOfficeObjects, PopulateOfficeObjectsDocument);
        }

        private static bool PopulateResourcesDocument(XDocument document, ExternalConfigCatalog catalog, string sourceName, bool rejectDuplicates)
        {
            if (document == null || document.Root == null || catalog == null)
            {
                return false;
            }

            var loadedAny = false;

            foreach (var element in document.Root.Elements("Group"))
            {
                var groupName = ReadAttribute(element, "name");
                var cargoType = ParseCargoType(ReadAttribute(element, "cargoType", groupName));
                var commodities = new List<string>();
                var commodityBasePrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                var commodityElements = element.Elements("Commodity").ToList();

                if (commodityElements.Count > 0)
                {
                    foreach (var commodityElement in commodityElements)
                    {
                        var rawCommodity = ReadAttribute(commodityElement, "name");
                        var commodity = CommodityCatalog.Normalize(rawCommodity);
                        if (string.IsNullOrWhiteSpace(commodity) || commodity.Equals("None", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!commodities.Contains(commodity, StringComparer.OrdinalIgnoreCase))
                        {
                            commodities.Add(commodity);
                        }

                        if (!rejectDuplicates && !CommodityCatalog.IsKnownCommodity(commodity))
                        {
                            catalog.ValidationMessages.Add(string.Format("{0} '{1}' references unknown commodity '{2}'.", sourceName, groupName, rawCommodity.Trim()));
                        }

                        var basePrice = Math.Max(0f, ReadFloatAttribute(commodityElement, "basePrice", 0f));
                        if (basePrice > 0f)
                        {
                            commodityBasePrices[commodity] = basePrice;
                        }

                        var economySemantics = ParseCommodityEconomySemantics(commodityElement, catalog.ValidationMessages, sourceName, groupName + " -> " + commodity);
                        if (economySemantics != null && economySemantics.HasConfiguredValues)
                        {
                            if (!catalog.ResourcesByCommodity.ContainsKey(commodity))
                            {
                                catalog.ResourcesByCommodity[commodity] = new ExternalResourceConfig
                                {
                                    Commodity = commodity,
                                    GroupName = groupName,
                                    CargoType = cargoType,
                                };
                            }

                            catalog.ResourcesByCommodity[commodity].EconomySemantics = economySemantics;
                        }
                    }
                }
                else
                {
                    commodities = ParseCommodityList(ReadAttribute(element, "commodities"), catalog.ValidationMessages, "Resources.xml", groupName);
                    var groupBasePrice = Math.Max(0f, ReadFloatAttribute(element, "basePrice", 0f));
                    if (groupBasePrice > 0f && commodities.Count == 1)
                    {
                        commodityBasePrices[commodities[0]] = groupBasePrice;
                    }
                }

                if (string.IsNullOrWhiteSpace(groupName) || cargoType == VehicleCargoType.Unknown || commodities.Count == 0)
                {
                    catalog.ValidationMessages.Add(string.Format("{0} contains a group with missing or invalid attributes.", sourceName));
                    continue;
                }

                if (rejectDuplicates)
                {
                    if (catalog.ResourceGroups.Any(group => group != null && string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase)))
                    {
                        catalog.ValidationMessages.Add(string.Format("{0} resource group '{1}' duplicates an existing resource group and was skipped.", sourceName, groupName));
                        continue;
                    }

                    var duplicateCommodity = commodities.FirstOrDefault(commodity => catalog.ResourcesByCommodity.ContainsKey(commodity));
                    if (!string.IsNullOrWhiteSpace(duplicateCommodity))
                    {
                        catalog.ValidationMessages.Add(string.Format("{0} resource group '{1}' duplicates commodity '{2}' and was skipped.", sourceName, groupName, duplicateCommodity));
                        continue;
                    }
                }

                catalog.ResourceGroups.Add(new ResourceGroupConfig
                {
                    Name = groupName,
                    CargoType = cargoType,
                    Commodities = commodities,
                });

                for (int i = 0; i < commodities.Count; i++)
                {
                    float basePrice;
                    commodityBasePrices.TryGetValue(commodities[i], out basePrice);
                    catalog.ResourcesByCommodity[commodities[i]] = new ExternalResourceConfig
                    {
                        Commodity = commodities[i],
                        GroupName = groupName,
                        CargoType = cargoType,
                        BasePrice = Math.Max(0f, basePrice),
                        EconomySemantics = catalog.ResourcesByCommodity.ContainsKey(commodities[i])
                            ? catalog.ResourcesByCommodity[commodities[i]].EconomySemantics
                            : null,
                    };

                    if (basePrice > 0f)
                    {
                        catalog.CommodityBasePrices[commodities[i]] = basePrice;
                    }
                }

                loadedAny = true;
            }

            return loadedAny;
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

        public static bool TryPopulateVehicleObjectLayouts(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "VehiclesObjects.xml",
                catalog.ValidationMessages,
                "VehiclesObjects.xml missing. Vehicle cargo object layouts will use legacy placement.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            var loadedAny = false;

            foreach (var element in document.Root.Elements("Vehicle"))
            {
                var modelName = ReadAttribute(element, "model");
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    catalog.ValidationMessages.Add("VehiclesObjects.xml contains a vehicle layout with no model.");
                    continue;
                }

                if (catalog.VehicleObjectLayouts.Any(existing => existing != null && string.Equals(existing.ModelName, modelName, StringComparison.OrdinalIgnoreCase)))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' duplicates an existing vehicle layout and was skipped.", modelName));
                    continue;
                }

                var centerX = ReadFloatAttribute(element, "centerX", float.NaN);
                var centerY = ReadFloatAttribute(element, "centerY", float.NaN);
                var centerZ = ReadFloatAttribute(element, "centerZ", float.NaN);
                if (float.IsNaN(centerX) || float.IsNaN(centerY) || float.IsNaN(centerZ))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' is missing valid center coordinates.", modelName));
                    continue;
                }

                var objectKey = ReadAttribute(element, "objectKey");
                if (!string.IsNullOrWhiteSpace(objectKey) && !catalog.ObjectModels.ContainsKey(objectKey))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' references unknown objectKey '{1}'.", modelName, objectKey));
                }

                var layout = new VehicleObjectLayoutDefinition
                {
                    ModelName = modelName,
                    DisplayName = ReadAttribute(element, "name", modelName),
                    ObjectKey = objectKey,
                    CenterOffset = new Vector3(centerX, centerY, centerZ),
                    MaxLine = Math.Max(0, ReadIntAttribute(element, "maxLine", 0)),
                    MaxRow = Math.Max(0, ReadIntAttribute(element, "maxRow", 0)),
                    IsEnabled = ReadBoolAttribute(element, "enabled", true),
                    PlacementMode = ParseVehicleObjectPlacementMode(ReadAttribute(element, "placement"), catalog.ValidationMessages, string.Format("VehiclesObjects.xml vehicle model '{0}'", modelName)),
                };

                PopulateVehicleObjectLayoutCommodityOverrides(element, layout, catalog);
                PopulateVehicleLooseCargoVisuals(element, layout, catalog);

                catalog.VehicleObjectLayouts.Add(layout);

                loadedAny = true;
            }

            return loadedAny;
        }

        private static void PopulateVehicleObjectLayoutCommodityOverrides(XElement vehicleElement, VehicleObjectLayoutDefinition layout, ExternalConfigCatalog catalog)
        {
            if (vehicleElement == null || layout == null || catalog == null)
            {
                return;
            }

            foreach (var commodityElement in vehicleElement.Elements("Commodity"))
            {
                var commodity = CommodityCatalog.Normalize(ReadAttribute(commodityElement, "name", ReadAttribute(commodityElement, "commodity")));
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has a commodity override with no commodity.", layout.ModelName));
                    continue;
                }

                if (layout.CommodityOverrides.Any(existing => existing != null && existing.MatchesCommodity(commodity)))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' defines duplicate commodity override '{1}'.", layout.ModelName, commodity));
                    continue;
                }

                var objectKey = ReadAttribute(commodityElement, "objectKey");
                if (!string.IsNullOrWhiteSpace(objectKey) && !catalog.ObjectModels.ContainsKey(objectKey))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' commodity override '{1}' references unknown objectKey '{2}'.", layout.ModelName, commodity, objectKey));
                }

                layout.CommodityOverrides.Add(new VehicleCommodityObjectLayoutDefinition
                {
                    Commodity = commodity,
                    ObjectKey = objectKey,
                    CenterOffset = TryReadOptionalVector3(commodityElement, layout.ModelName, string.Format("commodity override '{0}'", commodity), catalog.ValidationMessages),
                    MaxLine = TryReadOptionalIntAttribute(commodityElement, "maxLine"),
                    MaxRow = TryReadOptionalIntAttribute(commodityElement, "maxRow"),
                    PlacementMode = ParseVehicleObjectPlacementMode(
                        ReadAttribute(commodityElement, "placement"),
                        catalog.ValidationMessages,
                        string.Format("VehiclesObjects.xml vehicle model '{0}' commodity override '{1}'", layout.ModelName, commodity)),
                });
            }
        }

        private static void PopulateVehicleLooseCargoVisuals(XElement vehicleElement, VehicleObjectLayoutDefinition layout, ExternalConfigCatalog catalog)
        {
            if (vehicleElement == null || layout == null || catalog == null)
            {
                return;
            }

            foreach (var looseElement in vehicleElement.Elements("LooseCargo"))
            {
                var objectKey = ReadAttribute(looseElement, "objectKey");
                if (string.IsNullOrWhiteSpace(objectKey))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has a loose cargo rule with no objectKey.", layout.ModelName));
                    continue;
                }

                if (!catalog.ObjectModels.ContainsKey(objectKey))
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' loose cargo rule references unknown objectKey '{1}'.", layout.ModelName, objectKey));
                }

                var commodities = SplitCsv(ReadAttribute(looseElement, "commodities"))
                    .Concat(SplitCsv(ReadAttribute(looseElement, "commodity")))
                    .Select(CommodityCatalog.Normalize)
                    .Where(commodity => !string.IsNullOrWhiteSpace(commodity))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var cargoType = ParseCargoType(ReadAttribute(looseElement, "cargoType"));
                if (commodities.Count == 0 && cargoType == VehicleCargoType.Unknown)
                {
                    catalog.ValidationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has a loose cargo rule with no commodities or cargoType.", layout.ModelName));
                    continue;
                }

                var looseVisual = new VehicleLooseCargoVisualDefinition
                {
                    ObjectKey = objectKey,
                    CargoType = cargoType,
                    CenterOffset = TryReadOptionalVector3(looseElement, layout.ModelName, "loose cargo rule", catalog.ValidationMessages),
                    MaxPropCount = Math.Max(1, ReadIntAttribute(looseElement, "maxPropCount", 1)),
                    SpreadX = Math.Max(0f, ReadFloatAttribute(looseElement, "spreadX", 0.5f)),
                    SpreadY = Math.Max(0f, ReadFloatAttribute(looseElement, "spreadY", 0.35f)),
                    YawJitterDegrees = Math.Max(0f, ReadFloatAttribute(looseElement, "yawJitter", 12f)),
                    PitchJitterDegrees = Math.Max(0f, ReadFloatAttribute(looseElement, "pitchJitter", 2f)),
                    RollJitterDegrees = Math.Max(0f, ReadFloatAttribute(looseElement, "rollJitter", 2f)),
                    IsEnabled = ReadBoolAttribute(looseElement, "enabled", true),
                };

                for (int i = 0; i < commodities.Count; i++)
                {
                    looseVisual.Commodities.Add(commodities[i]);
                }

                PopulateVehicleLooseCargoSlots(looseElement, layout.ModelName, looseVisual, catalog.ValidationMessages);

                layout.LooseCargoVisuals.Add(looseVisual);
            }
        }

        private static void PopulateVehicleLooseCargoSlots(XElement looseElement, string modelName, VehicleLooseCargoVisualDefinition looseVisual, List<string> validationMessages)
        {
            if (looseElement == null || looseVisual == null)
            {
                return;
            }

            foreach (var slotElement in looseElement.Elements("Slot"))
            {
                if (AreAttributesBlank(slotElement, "x", "y", "z"))
                {
                    continue;
                }

                Vector3 slotOffset;
                if (!TryReadRequiredVector3(slotElement, "x", "y", "z", out slotOffset))
                {
                    if (validationMessages != null)
                    {
                        validationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has invalid loose cargo Slot coordinates.", modelName));
                    }

                    continue;
                }

                var headingDegrees = 0f;
                if (!TryReadOptionalSlotHeading(slotElement, out headingDegrees))
                {
                    if (validationMessages != null)
                    {
                        validationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has invalid loose cargo Slot heading.", modelName));
                    }

                    continue;
                }

                var pitchDegrees = 0f;
                if (!TryReadOptionalFloatAttributeValue(slotElement, "pitch", out pitchDegrees))
                {
                    if (validationMessages != null)
                    {
                        validationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has invalid loose cargo Slot pitch.", modelName));
                    }

                    continue;
                }

                var rollDegrees = 0f;
                if (!TryReadOptionalFloatAttributeValue(slotElement, "roll", out rollDegrees))
                {
                    if (validationMessages != null)
                    {
                        validationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has invalid loose cargo Slot roll.", modelName));
                    }

                    continue;
                }

                looseVisual.ManualSlots.Add(new VehicleLooseCargoSlotDefinition
                {
                    Offset = slotOffset,
                    HeadingDegrees = headingDegrees,
                    PitchDegrees = pitchDegrees,
                    RollDegrees = rollDegrees,
                });
            }
        }

        private static VehicleObjectPlacementMode ParseVehicleObjectPlacementMode(string raw, List<string> validationMessages, string context)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return VehicleObjectPlacementMode.Default;
            }

            VehicleObjectPlacementMode placementMode;
            if (Enum.TryParse(raw.Trim(), true, out placementMode))
            {
                return placementMode;
            }

            if (validationMessages != null)
            {
                validationMessages.Add(string.Format("{0} uses unknown placement '{1}'.", context, raw));
            }

            return VehicleObjectPlacementMode.Default;
        }

        private static Vector3? TryReadOptionalVector3(XElement element, string modelName, string context, List<string> validationMessages)
        {
            if (element == null)
            {
                return null;
            }

            var hasX = element.Attribute("centerX") != null;
            var hasY = element.Attribute("centerY") != null;
            var hasZ = element.Attribute("centerZ") != null;
            if (!hasX && !hasY && !hasZ)
            {
                return null;
            }

            if (AreAttributesBlank(element, "centerX", "centerY", "centerZ"))
            {
                return null;
            }

            float x;
            float y;
            float z;
            if (!TryReadFloatAttribute(element, "centerX", out x)
                || !TryReadFloatAttribute(element, "centerY", out y)
                || !TryReadFloatAttribute(element, "centerZ", out z))
            {
                if (validationMessages != null)
                {
                    validationMessages.Add(string.Format("VehiclesObjects.xml vehicle model '{0}' has invalid center coordinates for {1}.", modelName, context));
                }

                return null;
            }

            return new Vector3(x, y, z);
        }

        private static bool TryReadRequiredVector3(XElement element, string xAttributeName, string yAttributeName, string zAttributeName, out Vector3 value)
        {
            value = Vector3.Zero;
            if (element == null)
            {
                return false;
            }

            float x;
            float y;
            float z;
            if (!TryReadFloatAttribute(element, xAttributeName, out x)
                || !TryReadFloatAttribute(element, yAttributeName, out y)
                || !TryReadFloatAttribute(element, zAttributeName, out z))
            {
                return false;
            }

            value = new Vector3(x, y, z);
            return true;
        }

        private static bool AreAttributesBlank(XElement element, params string[] attributeNames)
        {
            if (element == null || attributeNames == null || attributeNames.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < attributeNames.Length; i++)
            {
                var attribute = element.Attribute(attributeNames[i]);
                if (attribute == null || !string.IsNullOrWhiteSpace(attribute.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadOptionalSlotHeading(XElement element, out float headingDegrees)
        {
            headingDegrees = 0f;
            if (element == null)
            {
                return true;
            }

            var headingAttribute = element.Attribute("heading");
            if (headingAttribute != null)
            {
                if (string.IsNullOrWhiteSpace(headingAttribute.Value))
                {
                    return true;
                }

                return TryReadFloatAttribute(element, "heading", out headingDegrees);
            }

            var yawAttribute = element.Attribute("yaw");
            if (yawAttribute == null || string.IsNullOrWhiteSpace(yawAttribute.Value))
            {
                return true;
            }

            return TryReadFloatAttribute(element, "yaw", out headingDegrees);
        }

        private static bool TryReadOptionalFloatAttributeValue(XElement element, string attributeName, out float value)
        {
            value = 0f;
            if (element == null)
            {
                return true;
            }

            var attribute = element.Attribute(attributeName);
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                return true;
            }

            return TryReadFloatAttribute(element, attributeName, out value);
        }

        private static int? TryReadOptionalIntAttribute(XElement element, string attributeName)
        {
            if (element == null || element.Attribute(attributeName) == null)
            {
                return null;
            }

            return Math.Max(0, ReadIntAttribute(element, attributeName, 0));
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

            return PopulateSitesDocument(document, catalog, "Sites.xml", false);
        }

        private static bool PopulateSitesDocument(XDocument document, ExternalConfigCatalog catalog, string sourceName, bool rejectDuplicates)
        {
            if (document == null || document.Root == null || catalog == null)
            {
                return false;
            }

            var loadedAny = false;

            foreach (var element in document.Root.Elements("Site"))
            {
                var legacyKey = ReadAttribute(element, "legacyKey");
                if (string.IsNullOrWhiteSpace(legacyKey))
                {
                    catalog.ValidationMessages.Add(string.Format("{0} contains a site with no legacyKey.", sourceName));
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
                    DeliveryPayoutMultiplier = Math.Max(0f, ReadFloatAttribute(element, "deliveryPayoutMultiplier", 1f)),
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
                    SourceFileName = sourceName,
                };

                var inputsElement = element.Element("Inputs");
                location.Inputs = ParseCommoditySet(ReadAttribute(inputsElement, "primary"), catalog.ValidationMessages, sourceName, legacyKey);
                location.OptionalInputs = ParseCommoditySet(ReadAttribute(inputsElement, "optional"), catalog.ValidationMessages, sourceName, legacyKey);
                location.BoostInputs = ParseCommoditySet(ReadAttribute(inputsElement, "boost"), catalog.ValidationMessages, sourceName, legacyKey);
                location.Outputs = ParseCommoditySet(ReadAttribute(inputsElement, "outputs"), catalog.ValidationMessages, sourceName, legacyKey);
                location.RecipeInputWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "recipeInputs"), catalog.ValidationMessages, sourceName, legacyKey);
                location.RecipeOutputWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "recipeOutputs"), catalog.ValidationMessages, sourceName, legacyKey);
                location.RecipeVariants = ParseRecipeVariants(element, catalog.ValidationMessages, sourceName, legacyKey);
                location.InputCapacityWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "inputCapacityWeights"), catalog.ValidationMessages, sourceName, legacyKey);
                location.OutputCapacityWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "outputCapacityWeights"), catalog.ValidationMessages, sourceName, legacyKey);
                location.SinkPreferenceWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "sinkPreferenceWeights"), catalog.ValidationMessages, sourceName, legacyKey);
                location.Kind = ResolveLocationKind(location.SiteRole, location.Inputs, location.Outputs);
                location.SinkElasticityMultiplier = Math.Max(0.05f, ReadFloatAttribute(element, "sinkElasticityMultiplier", 1f));

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
                        else if (presetName.Equals("Impossible", StringComparison.OrdinalIgnoreCase))
                        {
                            location.ImpossibleEconomy = preset;
                        }
                    }
                }

                if (!TryReadFloatAttribute(element, "weeklyPassiveIncome", out var weeklyPassiveIncome))
                {
                    location.WeeklyPassiveIncome = ServiceSiteEconomyPolicy.NormalizeWeeklyPassiveIncome(location.SiteRole, location.RefuelIsFree, DeriveWeeklyPassiveIncome(location));
                }
                else
                {
                    location.WeeklyPassiveIncome = ServiceSiteEconomyPolicy.NormalizeWeeklyPassiveIncome(location.SiteRole, location.RefuelIsFree, weeklyPassiveIncome);
                }

                var primaryEconomy = location.StandardEconomy
                    ?? location.CasualEconomy
                    ?? location.HardcoreEconomy
                    ?? location.ImpossibleEconomy;

                location.FactoryProductionRatio = Math.Max(
                    0.1f,
                    primaryEconomy != null && primaryEconomy.ProductionRatio > 0f
                        ? primaryEconomy.ProductionRatio
                        : 1f);
                location.IndustryPrice = primaryEconomy != null
                    ? Math.Max(0f, primaryEconomy.PurchasePrice)
                    : 0f;

                if (string.IsNullOrWhiteSpace(location.DistrictName) || !catalog.Districts.ContainsKey(location.DistrictName))
                {
                    catalog.ValidationMessages.Add(string.Format("{0} site '{1}' references unknown district '{2}'.", sourceName, location.LegacyKey, location.DistrictName));
                }

                if (location.Position == Vector3.Zero)
                {
                    catalog.ValidationMessages.Add(string.Format("{0} site '{1}' is missing marker coordinates.", sourceName, location.LegacyKey));
                }

                if (rejectDuplicates && catalog.Locations.ContainsKey(location.Id))
                {
                    catalog.ValidationMessages.Add(string.Format("{0} site '{1}' duplicates an existing site id and was skipped.", sourceName, location.Id));
                    continue;
                }

                catalog.Locations[location.Id] = location;
                loadedAny = true;
            }

            return loadedAny;
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

            return PopulateVehiclesDocument(document, catalog, "Vehicles.xml", false);
        }

        private static bool PopulateVehiclesDocument(XDocument document, ExternalConfigCatalog catalog, string sourceName, bool rejectDuplicates)
        {
            if (document == null || document.Root == null || catalog == null)
            {
                return false;
            }

            var loadedAny = false;

            foreach (var element in document.Root.Elements("Vehicle"))
            {
                var modelName = ReadAttribute(element, "model");
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    catalog.ValidationMessages.Add(string.Format("{0} contains a vehicle entry with no model.", sourceName));
                    continue;
                }

                var displayName = ReadAttribute(element, "name", modelName);
                var vehicleType = ReadAttribute(element, "type");
                var vehicleId = ReadAttribute(element, "id");
                var acceptedCommodities = ParseCommoditySet(ReadAttribute(element, "acceptedResources"), catalog.ValidationMessages, sourceName, modelName);
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

                if (rejectDuplicates)
                {
                    if (!string.IsNullOrWhiteSpace(vehicleId)
                        && catalog.VehicleDefinitions.Any(existing => existing != null && string.Equals(existing.Id, vehicleId, StringComparison.OrdinalIgnoreCase)))
                    {
                        catalog.ValidationMessages.Add(string.Format("{0} vehicle id '{1}' duplicates an existing vehicle and was skipped.", sourceName, vehicleId));
                        continue;
                    }

                    if (catalog.VehicleDefinitions.Any(existing => existing != null && string.Equals(existing.ModelName, modelName, StringComparison.OrdinalIgnoreCase)))
                    {
                        catalog.ValidationMessages.Add(string.Format("{0} vehicle model '{1}' duplicates an existing vehicle and was skipped.", sourceName, modelName));
                        continue;
                    }
                }

                catalog.VehicleDefinitions.Add(new VehicleDefinition
                {
                    Id = vehicleId,
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

                loadedAny = true;
            }

            return loadedAny;
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
                    FacilityAnchors = ParseOfficeFacilityAnchors(element, catalog.ValidationMessages, officeId),
                    OfficePrice = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                    WeeklyOfficeRent = Math.Max(0f, ReadFloatAttribute(element, "weeklyRent", 0f)),
                    MaxCommercialVehicles = Math.Max(0, ReadIntAttribute(element, "maxCommercialVehicles", 0)),
                    Description = ReadElementValue(element.Element("Description")),
                });
            }

            return catalog.OfficeDefinitions.Count > 0;
        }

        public static bool TryPopulateBanks(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Banks.xml",
                catalog.ValidationMessages,
                "Banks.xml missing. Company bank loan interactions will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            foreach (var element in document.Root.Elements("Bank"))
            {
                var bankId = ReadAttribute(element, "id");
                var bankName = ReadAttribute(element, "name", bankId);
                var position = ReadOptionalVector3(element.Element("Position"));
                var maxLoan = ReadFloatAttribute(element, "loanAmountMaxLimit", float.NaN);
                var minInterest = ReadFloatAttribute(element, "loanInterestMin", float.NaN);
                var maxInterest = ReadFloatAttribute(element, "loanInterestMax", float.NaN);
                if (string.IsNullOrWhiteSpace(bankId) || string.IsNullOrWhiteSpace(bankName) || !position.HasValue)
                {
                    catalog.ValidationMessages.Add("Banks.xml contains a bank with missing id, name, or position.");
                    continue;
                }

                if (float.IsNaN(maxLoan) || maxLoan <= 0f)
                {
                    catalog.ValidationMessages.Add(string.Format("Banks.xml bank '{0}' has an invalid loanAmountMaxLimit.", bankId));
                    continue;
                }

                if (float.IsNaN(minInterest) || float.IsNaN(maxInterest) || minInterest < 0f || maxInterest < minInterest)
                {
                    catalog.ValidationMessages.Add(string.Format("Banks.xml bank '{0}' has an invalid weekly interest range.", bankId));
                    continue;
                }

                catalog.BankDefinitions.Add(new BankDefinition
                {
                    BankId = bankId,
                    Name = bankName,
                    Position = position.Value,
                    LoanAmountMaxLimit = maxLoan,
                    LoanInterestMin = minInterest,
                    LoanInterestMax = maxInterest,
                });
            }

            return catalog.BankDefinitions.Count > 0;
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

            return PopulateOfficeObjectsDocument(document, catalog, "OfficeObjects.xml", false);
        }

        private static bool PopulateOfficeObjectsDocument(XDocument document, ExternalConfigCatalog catalog, string sourceName, bool rejectDuplicates)
        {
            if (document == null || document.Root == null || catalog == null)
            {
                return false;
            }

            var loadedAny = false;

            foreach (var element in document.Root.Elements("Object"))
            {
                var objectId = ReadIntAttribute(element, "id", 0);
                var displayName = ReadAttribute(element, "name");
                var modelName = ReadAttribute(element, "model");
                int modelHash;
                var hasModelHash = TryReadHashAttribute(element, "hash", out modelHash);
                var size = ParseOfficeObjectSize(ReadAttribute(element, "size"));
                var function = ParseOfficeObjectFunction(ReadAttribute(element, "function"));
                var anchorType = ParseOfficeFacilityAnchorType(ReadAttribute(element, "anchor"));
                var resourceType = CommodityCatalog.Normalize(ReadAttribute(element, "resource"));
                if (resourceType.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    resourceType = string.Empty;
                }

                if (objectId <= 0 || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(modelName) || !hasModelHash)
                {
                    catalog.ValidationMessages.Add(string.Format("{0} contains an object with missing id, name, model, or hash.", sourceName));
                    continue;
                }

                if (rejectDuplicates && catalog.OfficeObjectDefinitions.Any(existing => existing != null && existing.ObjectId == objectId))
                {
                    catalog.ValidationMessages.Add(string.Format("{0} office object id '{1}' duplicates an existing office object and was skipped.", sourceName, objectId));
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
                    PlacementContext = ParseOfficeObjectPlacementContext(ReadAttribute(element, "placement"), anchorType),
                    AnchorType = anchorType,
                    InteractionType = ParseOfficeFacilityInteractionType(ReadAttribute(element, "interaction")),
                    AmbientStaffRole = ParseOfficeAmbientStaffRole(ReadAttribute(element, "ambientRole")),
                    AmbientStaffCount = Math.Max(0, ReadIntAttribute(element, "staffCount", 0)),
                    RequiresOwnedOffice = ReadBoolAttribute(element, "requiresOwned", function == OfficeObjectFunction.Headquarters),
                    AmbientScenarioName = ReadAttribute(element, "scenario"),
                    PerOfficeLimit = Math.Max(0, ReadIntAttribute(element, "limit", 0)),
                    Price = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                });

                loadedAny = true;
            }

            return loadedAny;
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

        public static bool TryPopulateMotels(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var document = LoadDocument(
                configDirectory,
                "Motels.xml",
                catalog.ValidationMessages,
                "Motels.xml missing. Motel rest will be unavailable.");
            if (document == null || document.Root == null)
            {
                return false;
            }

            var loadedAny = false;
            foreach (var element in document.Root.Elements("Motel"))
            {
                var motelId = ReadAttribute(element, "id");
                var motelName = ReadAttribute(element, "name", motelId);
                var exteriorPosition = ReadOptionalVector3(element.Element("ExteriorPosition"));
                var restPrice = ReadFloatAttribute(element, "restPrice", float.NaN);
                var contextName = !string.IsNullOrWhiteSpace(motelName) ? motelName : motelId;

                if (string.IsNullOrWhiteSpace(motelId) || string.IsNullOrWhiteSpace(motelName))
                {
                    catalog.ValidationMessages.Add("Motels.xml contains a motel with a missing id or name.");
                    continue;
                }

                if (float.IsNaN(restPrice))
                {
                    catalog.ValidationMessages.Add(string.Format("Motels.xml motel '{0}' has an invalid restPrice and was skipped.", contextName));
                    continue;
                }

                if (!exteriorPosition.HasValue)
                {
                    catalog.ValidationMessages.Add(string.Format("Motels.xml motel '{0}' has missing or invalid exterior coordinates and was skipped.", contextName));
                    continue;
                }

                catalog.MotelDefinitions.Add(new MotelDefinition
                {
                    MotelId = motelId,
                    MotelName = motelName,
                    MotelIgName = ReadAttribute(element, "igName"),
                    MotelType = ReadAttribute(element, "type"),
                    RestPrice = Math.Max(0f, restPrice),
                    ExteriorPosition = exteriorPosition.Value,
                });
                loadedAny = true;
            }

            return loadedAny;
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

        private static bool TryPopulateAddonDocuments(
            LsolAddonCatalog addonCatalog,
            ExternalConfigCatalog catalog,
            string capability,
            Func<XDocument, ExternalConfigCatalog, string, bool, bool> populateDocument)
        {
            if (addonCatalog == null || catalog == null || populateDocument == null)
            {
                return false;
            }

            var loadedAny = false;
            foreach (var package in addonCatalog.GetPackagesForCapability(capability))
            {
                string contentDirectory;
                if (package == null || !package.TryGetContentDirectory(capability, out contentDirectory) || !Directory.Exists(contentDirectory))
                {
                    continue;
                }

                var files = Directory.GetFiles(contentDirectory, "*.xml", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                for (int i = 0; i < files.Length; i++)
                {
                    var sourceName = string.Format("Add-on '{0}' file '{1}'", package.DisplayLabel, Path.GetFileName(files[i]));
                    var document = LoadDocumentFromPath(files[i], catalog.ValidationMessages, sourceName);
                    if (document == null)
                    {
                        continue;
                    }

                    if (document.Root == null)
                    {
                        catalog.ValidationMessages.Add(string.Format("{0} is empty.", sourceName));
                        continue;
                    }

                    loadedAny = populateDocument(document, catalog, sourceName, true) || loadedAny;
                }
            }

            return loadedAny;
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

        private static XDocument LoadDocumentFromPath(string filePath, ICollection<string> validationMessages, string sourceName)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                return XDocument.Load(filePath, LoadOptions.None);
            }
            catch (Exception ex)
            {
                validationMessages?.Add(string.Format("{0} could not be loaded: {1}", sourceName, ex.Message));
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

        private static Dictionary<string, float> ParseCommodityWeightMap(string raw, ICollection<string> validationMessages, string sourceName, string context)
        {
            return ParseCommodityWeightMap(raw, validationMessages, sourceName, context, true);
        }

        private static Dictionary<string, float> ParseCommodityWeightMap(string raw, ICollection<string> validationMessages, string sourceName, string context, bool validateKnownCommodity)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in SplitCsv(raw))
            {
                var token = (part ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var split = token.Split(new[] { ':', '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length != 2)
                {
                    validationMessages?.Add(string.Format("{0} '{1}' has an invalid weighted commodity token '{2}'.", sourceName, context, token));
                    continue;
                }

                var commodity = CommodityCatalog.Normalize(split[0]);
                if (string.IsNullOrWhiteSpace(commodity) || commodity.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (validateKnownCommodity && !CommodityCatalog.IsKnownCommodity(commodity))
                {
                    validationMessages?.Add(string.Format("{0} '{1}' references unknown commodity '{2}'.", sourceName, context, split[0].Trim()));
                }

                float weight;
                if (!float.TryParse(split[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out weight)
                    && !float.TryParse(split[1].Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out weight))
                {
                    validationMessages?.Add(string.Format("{0} '{1}' has an invalid weight '{2}' for commodity '{3}'.", sourceName, context, split[1].Trim(), split[0].Trim()));
                    continue;
                }

                if (weight <= 0f)
                {
                    continue;
                }

                result[commodity] = weight;
            }

            return result;
        }

        private static CommodityEconomySemantics ParseCommodityEconomySemantics(XElement element, ICollection<string> validationMessages, string sourceName, string context)
        {
            if (element == null)
            {
                return null;
            }

            var semantics = new CommodityEconomySemantics
            {
                SubstituteFamily = ReadAttribute(element, "substituteFamily"),
                DemandClasses = ParseSemanticClassSet(ReadAttribute(element, "demandClasses")),
                Substitutes = ParseCommodityWeightMap(ReadAttribute(element, "substitutes"), validationMessages, sourceName, context, false),
            };

            float value;
            if (TryReadFloatAttribute(element, "sinkElasticity", out value))
            {
                semantics.SinkElasticity = Math.Max(0f, value);
            }

            if (TryReadFloatAttribute(element, "scarcitySensitivity", out value))
            {
                semantics.ScarcitySensitivity = Math.Max(0f, value);
            }

            if (TryReadFloatAttribute(element, "sinkPreference", out value))
            {
                semantics.SinkPreferenceWeight = Math.Max(0f, value);
            }

            if (TryReadFloatAttribute(element, "eventAffinity", out value))
            {
                semantics.EventResponseAffinity = Math.Max(0f, value);
            }

            if (TryReadFloatAttribute(element, "volatility", out value))
            {
                semantics.Volatility = Math.Max(0f, value);
            }

            if (TryReadFloatAttribute(element, "perishability", out value))
            {
                semantics.Perishability = Math.Max(0f, value);
            }

            return semantics.HasConfiguredValues ? semantics : null;
        }

        private static HashSet<string> ParseSemanticClassSet(string raw)
        {
            return new HashSet<string>(
                SplitCsv(raw)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()),
                StringComparer.OrdinalIgnoreCase);
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

        private static OfficeObjectPlacementContext ParseOfficeObjectPlacementContext(string raw, OfficeFacilityAnchorType anchorType)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
            if (normalized.Equals("Room", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Interior", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectPlacementContext.Room;
            }

            if (normalized.Equals("Either", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Both", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("RoomOrYard", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectPlacementContext.Either;
            }

            return anchorType != OfficeFacilityAnchorType.None
                ? OfficeObjectPlacementContext.Either
                : OfficeObjectPlacementContext.Yard;
        }

        private static OfficeFacilityAnchorType ParseOfficeFacilityAnchorType(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
            if (normalized.Equals("Reception", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("ReceptionDesk", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityAnchorType.ReceptionDesk;
            }

            if (normalized.Equals("Dispatch", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("DispatchDesk", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityAnchorType.DispatchDesk;
            }

            if (normalized.Equals("Boardroom", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("MeetingRoom", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityAnchorType.Boardroom;
            }

            if (normalized.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("MaintenanceDesk", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("MaintenanceOffice", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityAnchorType.MaintenanceDesk;
            }

            if (normalized.Equals("Fuel", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("FuelDesk", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("FuelCounter", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("ServiceCounter", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityAnchorType.FuelDesk;
            }

            if (normalized.Equals("BreakRoom", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("AdminNook", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityAnchorType.BreakRoom;
            }

            if (normalized.Equals("Worker", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("WorkerFallback", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityAnchorType.WorkerFallback;
            }

            return OfficeFacilityAnchorType.None;
        }

        private static OfficeFacilityInteractionType ParseOfficeFacilityInteractionType(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
            if (normalized.Equals("OfficeSummary", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Reception", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityInteractionType.OfficeSummary;
            }

            if (normalized.Equals("HireNpc", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Npc", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Dispatch", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityInteractionType.HireNpc;
            }

            if (normalized.Equals("Repair", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("RepairVehicle", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityInteractionType.RepairVehicle;
            }

            if (normalized.Equals("Fuel", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("FuelManagement", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityInteractionType.FuelManagement;
            }

            if (normalized.Equals("Headquarters", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Doctrine", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("CompanyStatus", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("HeadquartersStatus", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeFacilityInteractionType.HeadquartersStatus;
            }

            return OfficeFacilityInteractionType.None;
        }

        private static OfficeAmbientStaffRole ParseOfficeAmbientStaffRole(string raw)
        {
            var normalized = (raw ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
            if (normalized.Equals("Receptionist", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeAmbientStaffRole.Receptionist;
            }

            if (normalized.Equals("Dispatcher", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeAmbientStaffRole.Dispatcher;
            }

            if (normalized.Equals("Mechanic", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("YardTech", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeAmbientStaffRole.Mechanic;
            }

            if (normalized.Equals("Loader", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("SupportWorker", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Worker", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeAmbientStaffRole.SupportWorker;
            }

            if (normalized.Equals("Security", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Guard", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeAmbientStaffRole.Security;
            }

            if (normalized.Equals("Manager", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("FloorManager", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeAmbientStaffRole.Manager;
            }

            if (normalized.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("AdminClerk", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Clerk", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeAmbientStaffRole.AdminClerk;
            }

            return OfficeAmbientStaffRole.None;
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

            if (normalized.Equals("Headquarters", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("HQ", StringComparison.OrdinalIgnoreCase))
            {
                return OfficeObjectFunction.Headquarters;
            }

            return OfficeObjectFunction.Decorative;
        }

        private static List<OfficeFacilityAnchorDefinition> ParseOfficeFacilityAnchors(XElement officeElement, IList<string> validationMessages, string officeId)
        {
            var anchors = new List<OfficeFacilityAnchorDefinition>();
            if (officeElement == null)
            {
                return anchors;
            }

            foreach (var anchorElement in officeElement.Elements("FacilityAnchor"))
            {
                var position = ReadOptionalVector3(anchorElement);
                if (!position.HasValue)
                {
                    validationMessages.Add(string.Format("Office '{0}' contains a facility anchor with missing coordinates.", officeId ?? string.Empty));
                    continue;
                }

                var anchorType = ParseOfficeFacilityAnchorType(ReadAttribute(anchorElement, "type"));
                if (anchorType == OfficeFacilityAnchorType.None)
                {
                    validationMessages.Add(string.Format("Office '{0}' contains a facility anchor with an unknown type.", officeId ?? string.Empty));
                    continue;
                }

                var anchorId = ReadAttribute(anchorElement, "id");
                if (string.IsNullOrWhiteSpace(anchorId))
                {
                    anchorId = string.Format("{0}_{1}_{2}", officeId ?? "office", anchorType, anchors.Count + 1);
                }

                anchors.Add(new OfficeFacilityAnchorDefinition
                {
                    AnchorId = anchorId,
                    AnchorType = anchorType,
                    Label = ReadAttribute(anchorElement, "label", anchorType.ToString()),
                    Position = position.Value,
                    Heading = ReadFloatAttribute(anchorElement, "heading", 0f),
                    InteractionRadius = Math.Max(1.25f, ReadFloatAttribute(anchorElement, "radius", 2.2f)),
                    ScenarioName = ReadAttribute(anchorElement, "scenario"),
                });
            }

            return anchors;
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

        private static List<IndustryRecipeVariantConfig> ParseRecipeVariants(XElement siteElement, ICollection<string> validationMessages, string sourceName, string context)
        {
            var result = new List<IndustryRecipeVariantConfig>();
            if (siteElement == null)
            {
                return result;
            }

            var recipeElements = siteElement.Element("Recipes") != null
                ? siteElement.Element("Recipes").Elements("Recipe")
                : Enumerable.Empty<XElement>();

            recipeElements = recipeElements.Concat(siteElement.Elements("Recipe"));

            foreach (var recipeElement in recipeElements)
            {
                var variantId = ReadAttribute(recipeElement, "id", ReadAttribute(recipeElement, "name"));
                var displayName = ReadAttribute(recipeElement, "name", variantId);
                var inputsElement = recipeElement.Element("Inputs") ?? recipeElement;
                var inputs = ParseCommoditySet(ReadAttribute(inputsElement, "primary"), validationMessages, sourceName, context + " recipe " + variantId);
                var optionalInputs = ParseCommoditySet(ReadAttribute(inputsElement, "optional"), validationMessages, sourceName, context + " recipe " + variantId);
                var boostInputs = ParseCommoditySet(ReadAttribute(inputsElement, "boost"), validationMessages, sourceName, context + " recipe " + variantId);
                var outputs = ParseCommoditySet(ReadAttribute(inputsElement, "outputs"), validationMessages, sourceName, context + " recipe " + variantId);
                var recipeInputs = ParseCommodityWeightMap(ReadAttribute(inputsElement, "recipeInputs"), validationMessages, sourceName, context + " recipe " + variantId);
                var recipeOutputs = ParseCommodityWeightMap(ReadAttribute(inputsElement, "recipeOutputs"), validationMessages, sourceName, context + " recipe " + variantId);
                var optionalInputWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "optionalInputWeights"), validationMessages, sourceName, context + " recipe " + variantId);
                var inputCapacityWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "inputCapacityWeights"), validationMessages, sourceName, context + " recipe " + variantId);
                var outputCapacityWeights = ParseCommodityWeightMap(ReadAttribute(inputsElement, "outputCapacityWeights"), validationMessages, sourceName, context + " recipe " + variantId);

                if (string.IsNullOrWhiteSpace(variantId) || outputs.Count == 0)
                {
                    validationMessages?.Add(string.Format("{0} site '{1}' contains a recipe variant with missing id or outputs.", sourceName, context));
                    continue;
                }

                result.Add(new IndustryRecipeVariantConfig
                {
                    Id = variantId,
                    DisplayName = displayName,
                    SelectionPriority = Math.Max(0, ReadIntAttribute(recipeElement, "priority", ReadIntAttribute(recipeElement, "selectionPriority", 0))),
                    Inputs = inputs,
                    OptionalInputs = optionalInputs,
                    BoostInputs = boostInputs,
                    Outputs = outputs,
                    RecipeInputWeights = recipeInputs,
                    RecipeOutputWeights = recipeOutputs,
                    OptionalInputWeights = optionalInputWeights,
                    InputCapacityWeights = inputCapacityWeights,
                    OutputCapacityWeights = outputCapacityWeights,
                });
            }

            return result;
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

        private static float DeriveWeeklyPassiveIncome(ExternalLocationConfig location)
        {
            if (location == null)
            {
                return 0f;
            }

            if (location.Kind != ExternalLocationKind.Store && location.Kind != ExternalLocationKind.GasStation)
            {
                return 0f;
            }

            var inputCapacityTons = ResolveWeeklyPassiveIncomeInputCapacityTons(location);
            var emptyingRate = Math.Max(0f, location.EmptyingRate);
            if (inputCapacityTons <= 0.001f || emptyingRate <= 0.001f)
            {
                return 0f;
            }

            return inputCapacityTons * emptyingRate * WeeklyPassiveIncomeThroughputMultiplier;
        }

        private static float ResolveWeeklyPassiveIncomeInputCapacityTons(ExternalLocationConfig location)
        {
            var preset = location != null
                ? (location.StandardEconomy ?? location.CasualEconomy ?? location.HardcoreEconomy ?? location.ImpossibleEconomy)
                : null;
            if (preset != null && preset.InputCapacityTons > 0.001f)
            {
                return preset.InputCapacityTons;
            }

            var normalizedDensity = NormalizeDensity(location != null ? location.Density : string.Empty, string.Empty);
            if (location != null && location.Kind == ExternalLocationKind.Store)
            {
                if (normalizedDensity.Equals("VeryLow", StringComparison.OrdinalIgnoreCase))
                {
                    return 6f;
                }

                if (normalizedDensity.Equals("Low", StringComparison.OrdinalIgnoreCase))
                {
                    return 12f;
                }

                if (normalizedDensity.Equals("High", StringComparison.OrdinalIgnoreCase))
                {
                    return 40f;
                }

                return 24f;
            }

            if (location != null && location.Kind == ExternalLocationKind.GasStation)
            {
                if (normalizedDensity.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    return 0f;
                }

                if (normalizedDensity.Equals("VeryLow", StringComparison.OrdinalIgnoreCase))
                {
                    return 20f;
                }

                if (normalizedDensity.Equals("Low", StringComparison.OrdinalIgnoreCase))
                {
                    return 35f;
                }

                if (normalizedDensity.Equals("High", StringComparison.OrdinalIgnoreCase))
                {
                    return 90f;
                }

                return 60f;
            }

            return 0f;
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