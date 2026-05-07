using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Config
{
    internal static class CsvConfigImport
    {
        public static bool TryPopulateDistricts(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var table = LoadCsvTable(configDirectory, "Districts.csv", catalog.ValidationMessages);
            if (table == null)
            {
                catalog.ValidationMessages.Add("Districts.csv missing. Territory control will have limited fidelity.");
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in table.Rows)
            {
                var districtId = row.GetString(table, "DistrictID", "DistrictId", "Id");
                var districtName = row.GetString(table, "DistrictName", "Name", "District");
                if (string.IsNullOrWhiteSpace(districtName))
                {
                    catalog.ValidationMessages.Add(string.Format("Districts.csv line {0}: missing district name.", row.LineNumber));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(districtId))
                {
                    districtId = districtName;
                }

                if (!seenIds.Add(districtId) || !seenNames.Add(districtName))
                {
                    catalog.ValidationMessages.Add(string.Format("Districts.csv line {0}: duplicate district '{1}'.", row.LineNumber, districtName));
                    continue;
                }

                var district = new DistrictConfig
                {
                    Id = districtId,
                    Name = districtName,
                };

                for (int i = 1; i <= 12; i++)
                {
                    float x;
                    float y;
                    if (!row.TryGetFloat(table, out x, "MarkerX" + i, "X" + i, "VertexX" + i))
                    {
                        continue;
                    }

                    if (!row.TryGetFloat(table, out y, "MarkerY" + i, "Y" + i, "VertexY" + i))
                    {
                        continue;
                    }

                    district.PolygonVertices.Add(new Vector2(x, y));
                }

                if (district.PolygonVertices.Count < 3)
                {
                    catalog.ValidationMessages.Add(string.Format("Districts.csv line {0}: district '{1}' has fewer than 3 polygon points.", row.LineNumber, districtName));
                    continue;
                }

                catalog.Districts[district.Name] = district;
            }

            return catalog.Districts.Count > 0;
        }

        public static bool TryPopulateSites(string configDirectory, ExternalConfigCatalog catalog, IDictionary<string, ExternalLocationConfig> legacyLocations)
        {
            if (catalog == null)
            {
                return false;
            }

            var table = LoadCsvTable(configDirectory, "Sites.csv", catalog.ValidationMessages);
            if (table == null)
            {
                catalog.ValidationMessages.Add("Sites.csv missing. Falling back to legacy location INIs.");
                return false;
            }

            var seenCatalogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenLegacyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in table.Rows)
            {
                var catalogId = row.GetString(table, "Id", "SiteId");
                var legacyKey = row.GetString(table, "LegacyKey", "LegacyId", "Key");
                if (string.IsNullOrWhiteSpace(legacyKey))
                {
                    catalog.ValidationMessages.Add(string.Format("Sites.csv line {0}: missing LegacyKey.", row.LineNumber));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(catalogId) && !seenCatalogIds.Add(catalogId))
                {
                    catalog.ValidationMessages.Add(string.Format("Sites.csv line {0}: duplicate site Id '{1}'.", row.LineNumber, catalogId));
                    continue;
                }

                if (!seenLegacyKeys.Add(legacyKey))
                {
                    catalog.ValidationMessages.Add(string.Format("Sites.csv line {0}: duplicate LegacyKey '{1}'.", row.LineNumber, legacyKey));
                    continue;
                }

                ExternalLocationConfig legacy = null;
                legacyLocations?.TryGetValue(legacyKey, out legacy);

                var location = new ExternalLocationConfig
                {
                    CatalogId = catalogId,
                    Id = legacyKey.Trim(),
                    LegacyKey = legacyKey.Trim(),
                    Name = FirstNonEmpty(row.GetString(table, "SiteName", "Name"), legacy != null ? legacy.Name : string.Empty, legacyKey),
                    SiteRole = SiteMetadataParser.ParseRole(row.GetString(table, "SiteRole", "Role")),
                    DistrictName = row.GetString(table, "District", "DistrictName"),
                    Company = row.GetString(table, "Company"),
                    Enabled = GetBool(row, table, true, "Enabled"),
                    IndustryOwnerCut = GetFloat(row, table, legacy != null ? legacy.IndustryOwnerCut : 0.5f, "IndustryOwnerCut"),
                    StartingTankRatio = GetClampedRatio(row, table, legacy != null ? legacy.StartingTankRatio : 0f, "StartingResources", "StartingTank"),
                    Density = NormalizeDensity(row.GetString(table, "Density"), legacy != null ? legacy.Density : string.Empty),
                    EmptyingRate = GetFloat(row, table, 0f, "EmptyingRate"),
                    OwnershipTier = SiteMetadataParser.ParseOwnershipTier(row.GetString(table, "OwnershipTier")),
                    GatePosition = TryGetVector3(row, table, "GateX", "GateY", "GateZ"),
                    WorkerPosition = TryGetVector3(row, table, "WorkerX", "WorkerY", "WorkerZ"),
                    VehicleSpawnPosition = TryGetVector3(row, table, "SpawnAX", "SpawnAY", "SpawnAZ"),
                    VehicleSpawnHeading = GetOptionalFloat(row, table, "SpawnAHeading"),
                    SpawnedVehiclePosition = TryGetVector3(row, table, "SpawnOX", "SpawnOY", "SpawnOZ"),
                    SpawnedVehicleHeading = GetOptionalFloat(row, table, "SpawnOHeading"),
                    MaxSpawnedVehiclesLine = GetOptionalInt(row, table, "MaxObjectLine", "MaxSpawnedVehiclesLine"),
                    MaxSpawnedVehiclesRow = GetOptionalInt(row, table, "MaxObjectsRow", "MaxSpawnedVehiclesRow"),
                    DisplayObjectModelHash = GetOptionalInt(row, table, "ObjectModel"),
                    BarrierModelHash = GetOptionalInt(row, table, "BarrierModel"),
                    FactoryDoorPosition = legacy != null ? legacy.FactoryDoorPosition : null,
                    ObjectToDelete = legacy != null ? legacy.ObjectToDelete : string.Empty,
                };

                location.Position = TryGetVector3(row, table, "MarkerX", "MarkerY", "MarkerZ") ?? (legacy != null ? legacy.Position : Vector3.Zero);
                location.Inputs = ParseCommoditySet(row.GetString(table, "Input", "Inputs"), catalog.ValidationMessages, table.SourceName, row.LineNumber);
                location.OptionalInputs = ParseCommoditySet(row.GetString(table, "OptionalInput", "OptionalInputs"), catalog.ValidationMessages, table.SourceName, row.LineNumber);
                location.Outputs = ParseCommoditySet(row.GetString(table, "Output", "Outputs"), catalog.ValidationMessages, table.SourceName, row.LineNumber);
                location.Kind = ResolveLocationKind(location.SiteRole, location.Inputs, location.Outputs);

                if (legacy != null)
                {
                    ApplyLegacyFallbacks(location, legacy, catalog.ValidationMessages, row.LineNumber);
                }

                if (string.IsNullOrWhiteSpace(location.DistrictName) || !catalog.Districts.ContainsKey(location.DistrictName))
                {
                    catalog.ValidationMessages.Add(string.Format("Sites.csv line {0}: site '{1}' references unknown district '{2}'.", row.LineNumber, location.LegacyKey, location.DistrictName));
                }

                if (location.Position == Vector3.Zero)
                {
                    catalog.ValidationMessages.Add(string.Format("Sites.csv line {0}: site '{1}' is missing marker coordinates.", row.LineNumber, location.LegacyKey));
                }

                if (location.EmptyingRate <= 0f)
                {
                    location.EmptyingRate = InferEmptyingRate(location);
                }

                location.CasualEconomy = ParsePreset(row, table, "Casual", location, legacy, row.LineNumber, catalog.ValidationMessages);
                location.StandardEconomy = ParsePreset(row, table, "Standard", location, legacy, row.LineNumber, catalog.ValidationMessages);
                location.HardcoreEconomy = ParsePreset(row, table, "Hardcore", location, legacy, row.LineNumber, catalog.ValidationMessages);
                location.FactoryProductionRatio = Math.Max(0.1f, location.StandardEconomy != null && location.StandardEconomy.ProductionRatio > 0f ? location.StandardEconomy.ProductionRatio : 1f);
                location.IndustryPrice = location.StandardEconomy != null ? Math.Max(0f, location.StandardEconomy.PurchasePrice) : 0f;

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

            var table = LoadCsvTable(configDirectory, "Vehicles.csv", catalog.ValidationMessages);
            if (table == null)
            {
                catalog.ValidationMessages.Add("Vehicles.csv missing. Falling back to LSOL.ini vehicle sections.");
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in table.Rows)
            {
                var vehicleId = row.GetString(table, "VehicleID", "VehicleId", "Id");
                if (!string.IsNullOrWhiteSpace(vehicleId) && !seenIds.Add(vehicleId))
                {
                    catalog.ValidationMessages.Add(string.Format("Vehicles.csv line {0}: duplicate vehicle id '{1}'.", row.LineNumber, vehicleId));
                    continue;
                }

                var modelName = row.GetString(table, "ModelName", "Model");
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    catalog.ValidationMessages.Add(string.Format("Vehicles.csv line {0}: missing ModelName.", row.LineNumber));
                    continue;
                }

                var displayName = FirstNonEmpty(row.GetString(table, "VehicleName", "Name"), modelName);
                var vehicleType = row.GetString(table, "VehiceType", "VehicleType", "Type");
                var acceptedCommodities = ParseCommoditySet(row.GetString(table, "AcceptedResources", "AcceptedCommodities", "Resources"), catalog.ValidationMessages, table.SourceName, row.LineNumber);
                var acceptedCargoTypes = acceptedCommodities
                    .Select(CommodityCatalog.GetCargoTypeForCommodity)
                    .Where(x => x != VehicleCargoType.Unknown && x != VehicleCargoType.Trailer)
                    .Distinct()
                    .ToList();

                var capacity = Math.Max(0f, GetFloat(row, table, 0f, "VehicleCapacity", "Capacity"));
                var isTrailer = vehicleType.IndexOf("Trailer", StringComparison.OrdinalIgnoreCase) >= 0;
                var isTractor = capacity <= 0f
                    || vehicleType.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) >= 0 && acceptedCommodities.Count == 0;

                var definition = new VehicleDefinition
                {
                    Id = vehicleId,
                    SectionName = vehicleType,
                    DisplayName = displayName,
                    ModelName = modelName,
                    CargoType = ResolvePrimaryCargoType(acceptedCargoTypes, vehicleType, capacity),
                    AcceptedCommodities = acceptedCommodities,
                    CapacityTons = capacity,
                    IsEnabled = capacity > 0f || isTractor,
                    IsTrailer = isTrailer && !isTractor,
                    IsTractor = isTractor,
                };

                if (definition.IsEnabled)
                {
                    catalog.VehicleDefinitions.Add(definition);
                }
            }

            return catalog.VehicleDefinitions.Count > 0;
        }

        private static DelimitedTextTable LoadCsvTable(string configDirectory, string fileName, ICollection<string> validationMessages)
        {
            var filePath = Path.Combine(configDirectory ?? string.Empty, fileName ?? string.Empty);
            var table = DelimitedTextTable.Load(filePath, validationMessages);
            if (table != null)
            {
                return table;
            }

            string content;
            if (!TryReadEmbeddedCsv(fileName, out content))
            {
                return null;
            }

            return DelimitedTextTable.LoadFromString(fileName, content, validationMessages);
        }

        private static bool TryReadEmbeddedCsv(string fileName, out string content)
        {
            content = null;
            var resourceName = "LSOL.Configs." + (fileName ?? string.Empty);
            using (var stream = typeof(CsvConfigImport).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return false;
                }

                using (var reader = new StreamReader(stream))
                {
                    content = reader.ReadToEnd();
                }

                return !string.IsNullOrWhiteSpace(content);
            }
        }

        private static SiteEconomyPresetValues ParsePreset(
            DelimitedTextRow row,
            DelimitedTextTable table,
            string presetName,
            ExternalLocationConfig location,
            ExternalLocationConfig legacy,
            int lineNumber,
            ICollection<string> validationMessages)
        {
            var suffix = presetName ?? string.Empty;
            var productionRate = GetFloat(row, table, 0f, "IndustryProductionRate" + suffix, "ProductionRate" + suffix);
            var licencePrice = GetFloat(row, table, 0f, "IndustryLicencePrice" + suffix);
            var duplicateLicencePrice = GetFloat(row, table, licencePrice, "Licence" + suffix);
            if (licencePrice > 0f && duplicateLicencePrice > 0.01f && Math.Abs(licencePrice - duplicateLicencePrice) > 0.01f)
            {
                validationMessages.Add(string.Format("Sites.csv line {0}: licence columns disagree for {1} {2}. Using IndustryLicencePrice{2}.", lineNumber, location.LegacyKey, suffix));
            }

            if (licencePrice <= 0f)
            {
                licencePrice = duplicateLicencePrice;
            }

            var purchasePrice = GetFloat(row, table, 0f, "IndustryPrice" + suffix, "PurchasePrice" + suffix);
            var inputCapacity = GetFloat(row, table, 0f, "InputCapacity" + suffix, "InputCap" + suffix);
            var outputCapacity = GetFloat(row, table, 0f, "OutputCapacity" + suffix, "OutputCap" + suffix);
            var productionRatio = GetFloat(row, table, 0f, "ProductionRatio" + suffix, "FactoryProductionRatio" + suffix);
            bool permitRequired;
            if (!row.TryGetBool(table, out permitRequired, "Permit" + suffix))
            {
                permitRequired = licencePrice > 0f;
            }

            if (productionRate <= 0f && location.Outputs.Count > 0)
            {
                productionRate = legacy != null ? legacy.FactoryProductionRatio * 30f : 0f;
            }

            if (inputCapacity <= 0f && location.Inputs.Count > 0)
            {
                inputCapacity = legacy != null ? 0f : inputCapacity;
            }

            if (outputCapacity <= 0f && location.Outputs.Count > 0)
            {
                outputCapacity = legacy != null ? 0f : outputCapacity;
            }

            return SiteEconomyPresetValues.Create(
                Math.Max(0f, productionRate),
                Math.Max(0f, licencePrice),
                Math.Max(0f, purchasePrice),
                Math.Max(0f, inputCapacity),
                Math.Max(0f, outputCapacity),
                Math.Max(0f, productionRatio),
                permitRequired);
        }

        private static void ApplyLegacyFallbacks(ExternalLocationConfig location, ExternalLocationConfig legacy, ICollection<string> validationMessages, int lineNumber)
        {
            if (location == null || legacy == null)
            {
                return;
            }

            if (location.Position == Vector3.Zero && legacy.Position != Vector3.Zero)
            {
                location.Position = legacy.Position;
                validationMessages.Add(string.Format("Sites.csv line {0}: filled marker coordinates for '{1}' from legacy config.", lineNumber, location.LegacyKey));
            }

            if (!location.VehicleSpawnPosition.HasValue && legacy.VehicleSpawnPosition.HasValue)
            {
                location.VehicleSpawnPosition = legacy.VehicleSpawnPosition;
            }

            if (!location.VehicleSpawnHeading.HasValue && legacy.VehicleSpawnHeading.HasValue)
            {
                location.VehicleSpawnHeading = legacy.VehicleSpawnHeading;
            }

            if (location.Inputs.Count == 0 && legacy.Inputs != null && legacy.Inputs.Count > 0)
            {
                location.Inputs = new HashSet<string>(legacy.Inputs, StringComparer.OrdinalIgnoreCase);
            }

            if (location.Outputs.Count == 0 && legacy.Outputs != null && legacy.Outputs.Count > 0)
            {
                location.Outputs = new HashSet<string>(legacy.Outputs, StringComparer.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(location.Density))
            {
                location.Density = legacy.Density;
            }

            if (location.EmptyingRate <= 0f && legacy.EmptyingRate > 0f)
            {
                location.EmptyingRate = legacy.EmptyingRate;
            }

            if (string.IsNullOrWhiteSpace(location.Company))
            {
                location.Company = legacy.Company;
            }

            if (location.IndustryOwnerCut <= 0f)
            {
                location.IndustryOwnerCut = legacy.IndustryOwnerCut;
            }
        }

        private static HashSet<string> ParseCommoditySet(string raw, ICollection<string> validationMessages, string sourceName, int lineNumber)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (raw ?? string.Empty).Split(','))
            {
                var normalized = CommodityCatalog.Normalize(part);
                if (string.IsNullOrWhiteSpace(normalized) || normalized.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (!CommodityCatalog.IsKnownCommodity(normalized))
                {
                    validationMessages.Add(string.Format("{0} line {1}: unknown commodity '{2}'.", sourceName, lineNumber, part.Trim()));
                }
            }

            return result;
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

        private static float InferEmptyingRate(ExternalLocationConfig location)
        {
            if (location == null)
            {
                return 0f;
            }

            var density = NormalizeDensity(location.Density, string.Empty);
            if (density.Equals("VeryLow", StringComparison.OrdinalIgnoreCase))
            {
                return 0.35f;
            }

            if (density.Equals("Low", StringComparison.OrdinalIgnoreCase))
            {
                return 0.8f;
            }

            if (density.Equals("High", StringComparison.OrdinalIgnoreCase))
            {
                return 4f;
            }

            return 2.25f;
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

        private static Vector3? TryGetVector3(DelimitedTextRow row, DelimitedTextTable table, string xHeader, string yHeader, string zHeader)
        {
            float x;
            float y;
            float z;
            if (!row.TryGetFloat(table, out x, xHeader) || !row.TryGetFloat(table, out y, yHeader) || !row.TryGetFloat(table, out z, zHeader))
            {
                return null;
            }

            return new Vector3(x, y, z);
        }

        private static float GetFloat(DelimitedTextRow row, DelimitedTextTable table, float fallback, params string[] aliases)
        {
            float value;
            return row != null && row.TryGetFloat(table, out value, aliases)
                ? value
                : fallback;
        }

        private static float? GetOptionalFloat(DelimitedTextRow row, DelimitedTextTable table, params string[] aliases)
        {
            float value;
            return row != null && row.TryGetFloat(table, out value, aliases)
                ? (float?)value
                : null;
        }

        private static int? GetOptionalInt(DelimitedTextRow row, DelimitedTextTable table, params string[] aliases)
        {
            int value;
            return row != null && row.TryGetInt(table, out value, aliases)
                ? (int?)value
                : null;
        }

        private static bool GetBool(DelimitedTextRow row, DelimitedTextTable table, bool fallback, params string[] aliases)
        {
            bool value;
            return row != null && row.TryGetBool(table, out value, aliases)
                ? value
                : fallback;
        }

        private static float GetClampedRatio(DelimitedTextRow row, DelimitedTextTable table, float fallback, params string[] aliases)
        {
            var ratio = GetFloat(row, table, fallback, aliases);
            if (ratio < 0f)
            {
                return 0f;
            }

            if (ratio > 1f)
            {
                return 1f;
            }

            return ratio;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }
    }
}