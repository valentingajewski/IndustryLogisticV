using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using GTA;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.UI;

namespace LSOL.Systems
{
    public static class IndustryPersistenceManager
    {
        private const string PersistenceRootElementName = "LSOLState";
        private const string PersistenceSectionElementName = "Section";
        private const string PersistenceValueElementName = "Value";

        public static int Load(string filePath, IEnumerable<Industry> industries)
        {
            return LoadWithMetadata(filePath, industries).RestoredCount;
        }

        public static int Load(string filePath, IEnumerable<Industry> industries, TerritoryManager territoryManager)
        {
            return LoadWithMetadata(filePath, industries, territoryManager).RestoredCount;
        }

        public static IndustryPersistenceLoadResult LoadWithMetadata(string filePath, IEnumerable<Industry> industries)
        {
            return LoadWithMetadata(filePath, industries, null);
        }

        public static IndustryPersistenceLoadResult LoadWithMetadata(string filePath, IEnumerable<Industry> industries, TerritoryManager territoryManager)
        {
            var result = new IndustryPersistenceLoadResult
            {
                Metadata = new IndustryPersistenceMetadata(),
            };

            if (string.IsNullOrWhiteSpace(filePath) || industries == null)
            {
                return result;
            }

            var ini = LoadPersistenceIni(filePath);
            if (ini == null)
            {
                return result;
            }

            result.Metadata = ReadMetadata(ini);
            var territorySnapshot = territoryManager != null
                ? ReadTerritorySnapshot(ini)
                : null;

            var restoredCount = 0;

            foreach (var industry in industries)
            {
                if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                {
                    continue;
                }

                var section = BuildIndustrySectionName(industry.Id);
                if (!ini.HasSection(section))
                {
                    continue;
                }

                var productionRate = ini.GetFloat(section, "ProductionRate", industry.ProductionRate);
                var inputCapacityTons = ini.GetFloat(section, "InputCapacityTons", industry.InputCapacityTons);
                var outputCapacityTons = ini.GetFloat(section, "OutputCapacityTons", industry.OutputCapacityTons);
                var omegaCapacityTons = ini.GetFloat(section, "OmegaCapacityTons", industry.OmegaCapacityTons);
                var omegaStorage = ini.GetFloat(section, "OmegaStorage", industry.OmegaStorage);

                var productionModuleLevel = ParseInt(ini.GetString(section, "ProductionModuleLevel", industry.ProductionModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.ProductionModuleLevel);
                var inputStorageModuleLevel = ParseInt(ini.GetString(section, "InputStorageModuleLevel", industry.InputStorageModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.InputStorageModuleLevel);
                var outputStorageModuleLevel = ParseInt(ini.GetString(section, "OutputStorageModuleLevel", industry.OutputStorageModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.OutputStorageModuleLevel);
                var omegaStorageModuleLevel = ParseInt(ini.GetString(section, "OmegaStorageModuleLevel", industry.OmegaStorageModuleLevel.ToString(CultureInfo.InvariantCulture)), industry.OmegaStorageModuleLevel);
                var isOwned = ini.GetBool(section, "IsOwned", industry.IsOwned);
                var hasContractorPermit = ini.GetBool(section, "HasContractorPermit", industry.HasContractorPermit);
                var storageCondition = ini.GetFloat(section, "StorageCondition", 1f);
                var lastStoragePressureDayIndex = ParseInt(ini.GetString(section, "LastStoragePressureDayIndex", "-1"), -1);
                var lifetimeStorageLossTons = ini.GetFloat(section, "LifetimeStorageLossTons", 0f);
                var lifetimeStorageLossValue = ini.GetFloat(section, "LifetimeStorageLossValue", 0f);

                var bufferStorage = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                var block = ini.GetSection(section);
                foreach (var pair in block)
                {
                    if (pair.Key == null || !pair.Key.StartsWith("Buffer.", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var commodity = pair.Key.Substring("Buffer.".Length).Trim();
                    if (string.IsNullOrWhiteSpace(commodity))
                    {
                        continue;
                    }

                    var value = ParseFloat(pair.Value, 0f);
                    bufferStorage[commodity] = Math.Max(0f, value);
                }

                industry.ApplyPersistentState(
                    bufferStorage,
                    omegaStorage,
                    productionRate,
                    inputCapacityTons,
                    outputCapacityTons,
                    omegaCapacityTons,
                    productionModuleLevel,
                    inputStorageModuleLevel,
                    outputStorageModuleLevel,
                    omegaStorageModuleLevel,
                    isOwned,
                    hasContractorPermit);
                industry.ApplyStoragePressureState(storageCondition, lastStoragePressureDayIndex, lifetimeStorageLossTons, lifetimeStorageLossValue);

                restoredCount += 1;
            }

            if (territoryManager != null && (restoredCount > 0 || (result.Metadata != null && result.Metadata.HasGameplayMetadata) || HasTerritoryData(territorySnapshot)))
            {
                territoryManager.ApplySnapshot(territorySnapshot);
            }

            result.RestoredCount = restoredCount;
            return result;
        }

        public static void Save(string filePath, IEnumerable<Industry> industries)
        {
            Save(filePath, industries, null);
        }

        public static void Save(string filePath, IEnumerable<Industry> industries, IndustryPersistenceMetadata metadata)
        {
            Save(filePath, industries, metadata, null);
        }

        public static void Save(string filePath, IEnumerable<Industry> industries, IndustryPersistenceMetadata metadata, TerritoryPersistenceSnapshot territorySnapshot)
        {
            if (string.IsNullOrWhiteSpace(filePath) || industries == null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = BuildPersistenceXmlDocument(industries, metadata, territorySnapshot);
            document.Save(filePath);
        }

        private static void WriteLegacyIniPersistence(StreamWriter writer, IEnumerable<Industry> industries, IndustryPersistenceMetadata metadata, TerritoryPersistenceSnapshot territorySnapshot)
        {
            if (writer == null || industries == null)
            {
                return;
            }

            writer.WriteLine("[Meta]");
            var persistenceVersion = 1;
            if (metadata != null || territorySnapshot != null)
            {
                persistenceVersion = 6;
            }

            if (metadata != null && metadata.Analytics != null)
            {
                persistenceVersion = 7;
            }

            if (metadata != null && (HasOwnedFleetData(metadata.OwnedFleet) || HasNpcLogisticsData(metadata.NpcLogistics)))
            {
                persistenceVersion = 8;
            }

            if (metadata != null && (metadata.Language.HasValue || metadata.ColorblindMode.HasValue))
            {
                persistenceVersion = 9;
            }

            if (metadata != null && HasSpecialMissionData(metadata.SpecialMissions))
            {
                persistenceVersion = 11;
            }
            if (metadata != null && HasPropertyOwnershipData(metadata.PropertyOwnership))
            {
                persistenceVersion = 12;
            }

            if (metadata != null && HasNpcWorldDispatchData(metadata.NpcLogistics))
            {
                persistenceVersion = 13;
            }

            if (metadata != null && HasFinanceData(metadata.Finance))
            {
                persistenceVersion = 14;
            }

            if (metadata != null && HasBankLoanData(metadata.BankLoans))
            {
                persistenceVersion = 15;
            }

            if (metadata != null && HasPlayerStatisticsData(metadata.PlayerStatistics))
            {
                persistenceVersion = 16;
            }

            if (HasTerritoryDistrictReputationOffsetData(territorySnapshot))
            {
                persistenceVersion = 17;
            }

            if (metadata != null && HasGlobalMarketData(metadata.Market))
            {
                persistenceVersion = 18;
            }

            if (HasTerritoryData(territorySnapshot))
            {
                persistenceVersion = 19;
            }

            if ((metadata != null && HasPhaseFourPlayerStatisticsData(metadata.PlayerStatistics)) || HasTerritoryCompetitionData(territorySnapshot))
            {
                persistenceVersion = 20;
            }

            if (metadata != null && HasPlayerContractsData(metadata.PlayerContracts))
            {
                persistenceVersion = 21;
            }

            writer.WriteLine(
                "Version={0}",
                persistenceVersion);
            writer.WriteLine("SavedAtUtc={0}", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            if (metadata != null)
            {
                writer.WriteLine("Profit={0}", FormatFloat(metadata.Profit));
                writer.WriteLine("StartingBalance={0}", FormatFloat(metadata.StartingBalance));
                if (metadata.Language.HasValue)
                {
                    writer.WriteLine("Language={0}", metadata.Language.Value);
                }

                if (metadata.ColorblindMode.HasValue)
                {
                    writer.WriteLine("ColorblindMode={0}", metadata.ColorblindMode.Value);
                }

                writer.WriteLine("UseMetricSpeedDisplay={0}", metadata.UseMetricSpeedDisplay ? "true" : "false");
                writer.WriteLine("VehicleFuelDifficultyEnabled={0}", metadata.VehicleFuelDifficultyEnabled ? "true" : "false");
                writer.WriteLine("CargoWeightPowerDifficultyEnabled={0}", metadata.CargoWeightPowerDifficultyEnabled ? "true" : "false");
                writer.WriteLine("CargoDamageDifficultyEnabled={0}", metadata.CargoDamageDifficultyEnabled ? "true" : "false");
                writer.WriteLine("IndustryPricingDifficultyEnabled={0}", metadata.IndustryPricingDifficultyEnabled ? "true" : "false");
                writer.WriteLine("LicensingDifficultyEnabled={0}", metadata.LicensingDifficultyEnabled ? "true" : "false");
                writer.WriteLine("CorridorRestrictionDifficultyEnabled={0}", metadata.CorridorRestrictionDifficultyEnabled ? "true" : "false");
                writer.WriteLine("ReputationDifficultyEnabled={0}", metadata.ReputationDifficultyEnabled ? "true" : "false");
                writer.WriteLine("OfficeGarageLimitDifficultyEnabled={0}", metadata.OfficeGarageLimitDifficultyEnabled ? "true" : "false");
                writer.WriteLine("OfficeNpcLimitDifficultyEnabled={0}", metadata.OfficeNpcLimitDifficultyEnabled ? "true" : "false");
                writer.WriteLine("EconomyDifficultyPreset={0}", metadata.EconomyDifficultyPreset);
                writer.WriteLine("NpcWeeklyWageDifficulty={0}", metadata.NpcWeeklyWageDifficulty);
                writer.WriteLine("NpcRouteLimit={0}", metadata.NpcRouteLimit);
                writer.WriteLine("DifficultySettingsLocked={0}", metadata.DifficultySettingsLocked ? "true" : "false");
            }
            writer.WriteLine();

            foreach (var industry in industries.OrderBy(x => x != null ? x.Id : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildIndustrySectionName(industry.Id));
                writer.WriteLine("Name={0}", industry.Name ?? string.Empty);
                writer.WriteLine("ProductionRate={0}", FormatFloat(industry.ProductionRate));
                writer.WriteLine("InputCapacityTons={0}", FormatFloat(industry.InputCapacityTons));
                writer.WriteLine("OutputCapacityTons={0}", FormatFloat(industry.OutputCapacityTons));
                writer.WriteLine("OmegaCapacityTons={0}", FormatFloat(industry.OmegaCapacityTons));
                writer.WriteLine("OmegaStorage={0}", FormatFloat(industry.OmegaStorage));
                writer.WriteLine("ProductionModuleLevel={0}", industry.ProductionModuleLevel);
                writer.WriteLine("InputStorageModuleLevel={0}", industry.InputStorageModuleLevel);
                writer.WriteLine("OutputStorageModuleLevel={0}", industry.OutputStorageModuleLevel);
                writer.WriteLine("OmegaStorageModuleLevel={0}", industry.OmegaStorageModuleLevel);
                writer.WriteLine("IsOwned={0}", industry.IsOwned ? "true" : "false");
                writer.WriteLine("HasContractorPermit={0}", industry.HasContractorPermit ? "true" : "false");
                writer.WriteLine("StorageCondition={0}", FormatFloat(industry.StorageCondition));
                writer.WriteLine("LastStoragePressureDayIndex={0}", industry.LastStoragePressureDayIndex);
                writer.WriteLine("LifetimeStorageLossTons={0}", FormatFloat(industry.LifetimeStorageLossTons));
                writer.WriteLine("LifetimeStorageLossValue={0}", FormatFloat(industry.LifetimeStorageLossValue));

                foreach (var stock in industry.BufferStorage.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var commodity = CommodityCatalog.Normalize(stock.Key);
                    if (string.IsNullOrWhiteSpace(commodity))
                    {
                        continue;
                    }

                    writer.WriteLine("Buffer.{0}={1}", commodity, FormatFloat(Math.Max(0f, stock.Value)));
                }

                writer.WriteLine();
            }

            if (territorySnapshot != null)
            {
                WriteTerritorySnapshot(writer, territorySnapshot);
            }

            if (metadata != null && metadata.Analytics != null)
            {
                WriteAnalyticsSnapshot(writer, metadata.Analytics);
            }

            if (metadata != null && HasGlobalMarketData(metadata.Market))
            {
                WriteGlobalMarketSnapshot(writer, metadata.Market);
            }

            if (metadata != null && HasOwnedFleetData(metadata.OwnedFleet))
            {
                WriteOwnedFleetSnapshot(writer, metadata.OwnedFleet);
            }

            if (metadata != null && HasNpcLogisticsData(metadata.NpcLogistics))
            {
                WriteNpcLogisticsSnapshot(writer, metadata.NpcLogistics);
            }

            if (metadata != null && HasSpecialMissionData(metadata.SpecialMissions))
            {
                WriteSpecialMissionSnapshot(writer, metadata.SpecialMissions);
            }
            if (metadata != null && HasPropertyOwnershipData(metadata.PropertyOwnership))
            {
                WritePropertyOwnershipSnapshot(writer, metadata.PropertyOwnership);
            }

            if (metadata != null && HasFinanceData(metadata.Finance))
            {
                WriteFinanceSnapshot(writer, metadata.Finance);
            }

            if (metadata != null && HasBankLoanData(metadata.BankLoans))
            {
                WriteBankLoanSnapshot(writer, metadata.BankLoans);
            }

            if (metadata != null && HasPlayerStatisticsData(metadata.PlayerStatistics))
            {
                WritePlayerStatisticsSnapshot(writer, metadata.PlayerStatistics);
            }

            if (metadata != null && HasPlayerContractsData(metadata.PlayerContracts))
            {
                WritePlayerContractsSnapshot(writer, metadata.PlayerContracts);
            }
        }

        private static IndustryPersistenceMetadata ReadMetadata(IniFile ini)
        {
            var metadata = new IndustryPersistenceMetadata();
            if (ini == null || !ini.HasSection("Meta"))
            {
                return metadata;
            }

            metadata.HasGameplayMetadata =
                ini.HasKey("Meta", "Profit") ||
                ini.HasKey("Meta", "StartingBalance") ||
                ini.HasKey("Meta", "Language") ||
                ini.HasKey("Meta", "ColorblindMode") ||
                ini.HasKey("Meta", "UseMetricSpeedDisplay") ||
                ini.HasKey("Meta", "VehicleFuelDifficultyEnabled") ||
                ini.HasKey("Meta", "CargoWeightPowerDifficultyEnabled") ||
                ini.HasKey("Meta", "CargoDamageDifficultyEnabled") ||
                ini.HasKey("Meta", "IndustryPricingDifficultyEnabled") ||
                ini.HasKey("Meta", "LicensingDifficultyEnabled") ||
                ini.HasKey("Meta", "CorridorRestrictionDifficultyEnabled") ||
                ini.HasKey("Meta", "ReputationDifficultyEnabled") ||
                ini.HasKey("Meta", "OfficeGarageLimitDifficultyEnabled") ||
                ini.HasKey("Meta", "OfficeNpcLimitDifficultyEnabled") ||
                ini.HasKey("Meta", "EconomyDifficultyPreset") ||
                ini.HasKey("Meta", "NpcWeeklyWageDifficulty") ||
                ini.HasKey("Meta", "NpcRouteLimit") ||
                ini.HasKey("Meta", "DifficultySettingsLocked");

            metadata.StartingBalance = ini.GetFloat("Meta", "StartingBalance", 0f);
            metadata.Profit = ini.GetFloat("Meta", "Profit", metadata.StartingBalance);
            metadata.Language = ParseModLanguage(
                ini.GetString("Meta", "Language", string.Empty),
                ModLanguage.English);
            metadata.ColorblindMode = ParseColorblindMode(
                ini.GetString("Meta", "ColorblindMode", string.Empty),
                ColorblindMode.Off);
            metadata.UseMetricSpeedDisplay = ini.GetBool("Meta", "UseMetricSpeedDisplay", false);
            metadata.VehicleFuelDifficultyEnabled = ini.GetBool("Meta", "VehicleFuelDifficultyEnabled", false);
            metadata.CargoWeightPowerDifficultyEnabled = ini.GetBool("Meta", "CargoWeightPowerDifficultyEnabled", false);
            metadata.CargoDamageDifficultyEnabled = ini.GetBool("Meta", "CargoDamageDifficultyEnabled", true);
            metadata.IndustryPricingDifficultyEnabled = ini.GetBool("Meta", "IndustryPricingDifficultyEnabled", false);
            metadata.LicensingDifficultyEnabled = ini.GetBool("Meta", "LicensingDifficultyEnabled", false);
            metadata.CorridorRestrictionDifficultyEnabled = ini.GetBool("Meta", "CorridorRestrictionDifficultyEnabled", true);
            metadata.ReputationDifficultyEnabled = ini.GetBool("Meta", "ReputationDifficultyEnabled", true);
            metadata.OfficeGarageLimitDifficultyEnabled = ini.GetBool("Meta", "OfficeGarageLimitDifficultyEnabled", true);
            metadata.OfficeNpcLimitDifficultyEnabled = ini.GetBool("Meta", "OfficeNpcLimitDifficultyEnabled", false);
            metadata.EconomyDifficultyPreset = ParseEconomyDifficultyPreset(
                ini.GetString("Meta", "EconomyDifficultyPreset", EconomyDifficultyPreset.Standard.ToString()),
                EconomyDifficultyPreset.Standard);
            metadata.NpcWeeklyWageDifficulty = ParseNpcWeeklyWageDifficulty(
                ini.GetString("Meta", "NpcWeeklyWageDifficulty", NpcWeeklyWageDifficulty.Standard.ToString()),
                NpcWeeklyWageDifficulty.Standard);
            metadata.NpcRouteLimit = ParseInt(
                ini.GetString("Meta", "NpcRouteLimit", "5"),
                5);
            metadata.DifficultySettingsLocked = ini.GetBool("Meta", "DifficultySettingsLocked", false);
            metadata.Analytics = ReadAnalyticsSnapshot(ini);
            metadata.Market = ReadGlobalMarketSnapshot(ini);
            metadata.OwnedFleet = ReadOwnedFleetSnapshot(ini);
            metadata.NpcLogistics = ReadNpcLogisticsSnapshot(ini);
            metadata.SpecialMissions = ReadSpecialMissionSnapshot(ini);
            metadata.PropertyOwnership = ReadPropertyOwnershipSnapshot(ini);
            metadata.Finance = ReadFinanceSnapshot(ini);
            metadata.BankLoans = ReadBankLoanSnapshot(ini);
            metadata.PlayerStatistics = ReadPlayerStatisticsSnapshot(ini);
            metadata.PlayerContracts = ReadPlayerContractsSnapshot(ini);
            metadata.HasGameplayMetadata = metadata.HasGameplayMetadata
                || HasGlobalMarketData(metadata.Market)
                || HasBankLoanData(metadata.BankLoans)
                || HasPlayerStatisticsData(metadata.PlayerStatistics)
                || HasPlayerContractsData(metadata.PlayerContracts);
            return metadata;
        }

        private static GlobalMarketPersistenceSnapshot ReadGlobalMarketSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new GlobalMarketPersistenceSnapshot();
            var hasMarket = false;
            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section)
                    || !section.StartsWith("Market:Commodity:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var commodityKey = section.Substring("Market:Commodity:".Length).Trim();
                if (string.IsNullOrWhiteSpace(commodityKey))
                {
                    continue;
                }

                snapshot.CommodityStates.Add(new GlobalMarketCommodityPersistenceEntry
                {
                    Commodity = CommodityCatalog.Normalize(commodityKey),
                    PriceMultiplier = Math.Max(1f, ParseFloat(ini.GetString(section, "PriceMultiplier", "1"), 1f)),
                    RemainingScarcityMs = Math.Max(0, ParseInt(ini.GetString(section, "RemainingScarcityMs", "0"), 0)),
                });
                hasMarket = true;
            }

            return hasMarket ? snapshot : null;
        }

        private static CompanyFinancePersistenceSnapshot ReadFinanceSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new CompanyFinancePersistenceSnapshot();
            var hasFinance = false;
            if (ini.HasSection("FinanceMeta"))
            {
                snapshot.NextSequence = ParseInt(ini.GetString("FinanceMeta", "NextSequence", "1"), 1);
                hasFinance = true;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("Finance:Transaction:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                snapshot.Transactions.Add(new CompanyFinanceTransactionSnapshot
                {
                    Sequence = ParseInt(ini.GetString(section, "Sequence", "0"), 0),
                    InGameMinute = ParseInt(ini.GetString(section, "InGameMinute", "0"), 0),
                    Flow = ParseCompanyFinanceFlow(ini.GetString(section, "Flow", CompanyFinanceFlow.Expense.ToString()), CompanyFinanceFlow.Expense),
                    Category = ParseCompanyFinanceCategory(ini.GetString(section, "Category", CompanyFinanceCategory.OtherExpense.ToString()), CompanyFinanceCategory.OtherExpense),
                    Amount = ini.GetFloat(section, "Amount", 0f),
                    Description = ini.GetString(section, "Description", string.Empty),
                    RouteContractId = ParseInt(ini.GetString(section, "RouteContractId", "0"), 0),
                    RouteLabel = ini.GetString(section, "RouteLabel", string.Empty),
                });
                hasFinance = true;
            }

            return hasFinance ? snapshot : null;
        }

        private static TabletAnalyticsPersistenceSnapshot ReadAnalyticsSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new TabletAnalyticsPersistenceSnapshot();
            var hasAnalytics = false;
            if (ini.HasSection("AnalyticsMeta"))
            {
                hasAnalytics = true;
                snapshot.SelectedGraphTimeframe = ParseGraphTimeframe(
                    ini.GetString("AnalyticsMeta", "SelectedGraphTimeframe", TabletGraphTimeframe.ThirtyMinutes.ToString()),
                    TabletGraphTimeframe.ThirtyMinutes);
                snapshot.SelectedTrendCommodity = CommodityCatalog.Normalize(
                    ini.GetString("AnalyticsMeta", "SelectedTrendCommodity", string.Empty));
            }

            var profitHistory = ReadTimeSeriesPersistence(ini, BuildAnalyticsProfitSectionName());
            if (profitHistory != null)
            {
                snapshot.ProfitHistory = profitHistory;
                hasAnalytics = true;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                if (section.StartsWith("Analytics:Commodity:", StringComparison.OrdinalIgnoreCase))
                {
                    var commodityKey = section.Substring("Analytics:Commodity:".Length).Trim();
                    var series = ReadTimeSeriesPersistence(ini, section);
                    if (!string.IsNullOrWhiteSpace(commodityKey) && series != null)
                    {
                        snapshot.CommodityPriceHistories.Add(new TabletNamedTimeSeriesPersistence
                        {
                            Key = CommodityCatalog.Normalize(commodityKey),
                            Series = series,
                        });
                        hasAnalytics = true;
                    }

                    continue;
                }

                if (section.StartsWith("Analytics:SiteUtilization:", StringComparison.OrdinalIgnoreCase))
                {
                    var siteKey = section.Substring("Analytics:SiteUtilization:".Length).Trim();
                    var series = ReadTimeSeriesPersistence(ini, section);
                    if (!string.IsNullOrWhiteSpace(siteKey) && series != null)
                    {
                        snapshot.SiteUtilizationHistories.Add(new TabletNamedTimeSeriesPersistence
                        {
                            Key = siteKey,
                            Series = series,
                        });
                        hasAnalytics = true;
                    }

                    continue;
                }

                if (section.StartsWith("Analytics:SiteStorage:", StringComparison.OrdinalIgnoreCase))
                {
                    var siteKey = section.Substring("Analytics:SiteStorage:".Length).Trim();
                    var series = ReadTimeSeriesPersistence(ini, section);
                    if (!string.IsNullOrWhiteSpace(siteKey) && series != null)
                    {
                        snapshot.SiteStorageHistories.Add(new TabletNamedTimeSeriesPersistence
                        {
                            Key = siteKey,
                            Series = series,
                        });
                        hasAnalytics = true;
                    }
                }
            }

            return hasAnalytics ? snapshot : null;
        }

        private static void WriteAnalyticsSnapshot(StreamWriter writer, TabletAnalyticsPersistenceSnapshot analytics)
        {
            if (writer == null || analytics == null)
            {
                return;
            }

            writer.WriteLine("[AnalyticsMeta]");
            writer.WriteLine("SelectedGraphTimeframe={0}", analytics.SelectedGraphTimeframe);
            writer.WriteLine("SelectedTrendCommodity={0}", analytics.SelectedTrendCommodity ?? string.Empty);
            writer.WriteLine();

            WriteTimeSeriesPersistence(writer, BuildAnalyticsProfitSectionName(), analytics.ProfitHistory);
            WriteNamedSeriesPersistence(writer, "Analytics:Commodity:", analytics.CommodityPriceHistories, CommodityCatalog.Normalize);
            WriteNamedSeriesPersistence(writer, "Analytics:SiteUtilization:", analytics.SiteUtilizationHistories, key => key);
            WriteNamedSeriesPersistence(writer, "Analytics:SiteStorage:", analytics.SiteStorageHistories, key => key);
        }

        private static void WriteGlobalMarketSnapshot(StreamWriter writer, GlobalMarketPersistenceSnapshot market)
        {
            if (writer == null || market == null || !market.HasData)
            {
                return;
            }

            foreach (var entry in market.CommodityStates
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Commodity))
                .OrderBy(item => item.Commodity, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteLine("[Market:Commodity:{0}]", CommodityCatalog.Normalize(entry.Commodity));
                writer.WriteLine("PriceMultiplier={0}", FormatFloat(Math.Max(1f, entry.PriceMultiplier)));
                writer.WriteLine("RemainingScarcityMs={0}", Math.Max(0, entry.RemainingScarcityMs).ToString(CultureInfo.InvariantCulture));
                writer.WriteLine();
            }
        }

        private static void WriteFinanceSnapshot(StreamWriter writer, CompanyFinancePersistenceSnapshot finance)
        {
            if (writer == null || finance == null || finance.Transactions == null || finance.Transactions.Count == 0)
            {
                return;
            }

            writer.WriteLine("[FinanceMeta]");
            writer.WriteLine("NextSequence={0}", Math.Max(1, finance.NextSequence));
            writer.WriteLine();

            foreach (var transaction in finance.Transactions
                .Where(entry => entry != null && entry.Amount > 0f)
                .OrderBy(entry => entry.Sequence))
            {
                writer.WriteLine("[Finance:Transaction:{0:D4}]", Math.Max(0, transaction.Sequence));
                writer.WriteLine("Sequence={0}", transaction.Sequence);
                writer.WriteLine("InGameMinute={0}", transaction.InGameMinute);
                writer.WriteLine("Flow={0}", transaction.Flow);
                writer.WriteLine("Category={0}", transaction.Category);
                writer.WriteLine("Amount={0}", FormatFloat(transaction.Amount));
                writer.WriteLine("Description={0}", transaction.Description ?? string.Empty);
                writer.WriteLine("RouteContractId={0}", Math.Max(0, transaction.RouteContractId));
                writer.WriteLine("RouteLabel={0}", transaction.RouteLabel ?? string.Empty);
                writer.WriteLine();
            }
        }

        private static void WriteBankLoanSnapshot(StreamWriter writer, BankLoanPersistenceSnapshot snapshot)
        {
            if (writer == null || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            if (snapshot.ActiveLoan != null)
            {
                writer.WriteLine("[CompanyLoan]");
                writer.WriteLine("BankId={0}", snapshot.ActiveLoan.BankId ?? string.Empty);
                writer.WriteLine("BankName={0}", snapshot.ActiveLoan.BankName ?? string.Empty);
                writer.WriteLine("OriginalPrincipal={0}", FormatFloat(snapshot.ActiveLoan.OriginalPrincipal));
                writer.WriteLine("LockedInterestRatePercent={0}", FormatFloat(snapshot.ActiveLoan.LockedInterestRatePercent));
                writer.WriteLine("TotalRepayment={0}", FormatFloat(snapshot.ActiveLoan.TotalRepayment));
                writer.WriteLine("RemainingBalance={0}", FormatFloat(snapshot.ActiveLoan.RemainingBalance));
                writer.WriteLine("WeeklyInstallment={0}", FormatFloat(snapshot.ActiveLoan.WeeklyInstallment));
                writer.WriteLine("TermWeeks={0}", snapshot.ActiveLoan.TermWeeks);
                writer.WriteLine("WeeksPaid={0}", snapshot.ActiveLoan.WeeksPaid);
                writer.WriteLine("LastProcessedWeekIndex={0}", snapshot.ActiveLoan.LastProcessedWeekIndex);
                writer.WriteLine();
            }

            foreach (var offer in snapshot.OfferedRates.OrderBy(entry => entry != null ? entry.BankId : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (offer == null || string.IsNullOrWhiteSpace(offer.BankId))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildBankOfferSectionName(offer.BankId));
                writer.WriteLine("WeekIndex={0}", offer.WeekIndex);
                writer.WriteLine("RatePercent={0}", FormatFloat(offer.RatePercent));
                writer.WriteLine();
            }
        }

        private static BankLoanPersistenceSnapshot ReadBankLoanSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new BankLoanPersistenceSnapshot();
            var hasData = false;
            if (ini.HasSection("CompanyLoan"))
            {
                snapshot.ActiveLoan = new CompanyLoanState
                {
                    BankId = ini.GetString("CompanyLoan", "BankId", string.Empty),
                    BankName = ini.GetString("CompanyLoan", "BankName", string.Empty),
                    OriginalPrincipal = ini.GetFloat("CompanyLoan", "OriginalPrincipal", 0f),
                    LockedInterestRatePercent = ini.GetFloat("CompanyLoan", "LockedInterestRatePercent", 0f),
                    TotalRepayment = ini.GetFloat("CompanyLoan", "TotalRepayment", 0f),
                    RemainingBalance = ini.GetFloat("CompanyLoan", "RemainingBalance", 0f),
                    WeeklyInstallment = ini.GetFloat("CompanyLoan", "WeeklyInstallment", 0f),
                    TermWeeks = ParseInt(ini.GetString("CompanyLoan", "TermWeeks", "0"), 0),
                    WeeksPaid = ParseInt(ini.GetString("CompanyLoan", "WeeksPaid", "0"), 0),
                    LastProcessedWeekIndex = ParseInt(ini.GetString("CompanyLoan", "LastProcessedWeekIndex", "-1"), -1),
                };
                hasData = true;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("BankOffer:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var bankId = section.Substring("BankOffer:".Length).Trim();
                if (string.IsNullOrWhiteSpace(bankId))
                {
                    continue;
                }

                snapshot.OfferedRates.Add(new BankOfferRateSnapshot
                {
                    BankId = bankId,
                    WeekIndex = ParseInt(ini.GetString(section, "WeekIndex", "0"), 0),
                    RatePercent = ini.GetFloat(section, "RatePercent", 0f),
                });
                hasData = true;
            }

            return hasData ? snapshot : null;
        }

        private static void WritePlayerStatisticsSnapshot(StreamWriter writer, PlayerStatisticsPersistenceSnapshot snapshot)
        {
            if (writer == null || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            writer.WriteLine("[Successes]");
            writer.WriteLine("Initialized={0}", snapshot.IsInitialized ? "true" : "false");
            writer.WriteLine("HighestCompanyBalanceEver={0}", FormatFloat(snapshot.HighestCompanyBalanceEver));
            writer.WriteLine("HighestCompanyBalanceBeforeFirstNpcHire={0}", FormatFloat(snapshot.HighestCompanyBalanceBeforeFirstNpcHire));
            writer.WriteLine("HighestCompanyBalanceBeforeFirstLoan={0}", FormatFloat(snapshot.HighestCompanyBalanceBeforeFirstLoan));
            writer.WriteLine("TotalSuccessfulDeliveries={0}", Math.Max(0, snapshot.TotalSuccessfulDeliveries));
            writer.WriteLine("TotalSuccessfulCleanDeliveries={0}", Math.Max(0, snapshot.TotalSuccessfulCleanDeliveries));
            writer.WriteLine("DeliveriesBeforeFirstNpcHire={0}", Math.Max(0, snapshot.DeliveriesBeforeFirstNpcHire));
            writer.WriteLine("TotalTransportedTons={0}", FormatFloat(snapshot.TotalTransportedTons));
            writer.WriteLine("HasEverHiredNpc={0}", snapshot.HasEverHiredNpc ? "true" : "false");
            writer.WriteLine("HasEverTakenLoan={0}", snapshot.HasEverTakenLoan ? "true" : "false");
            writer.WriteLine("CumulativeNpcDeliveryIncome={0}", FormatFloat(snapshot.CumulativeNpcDeliveryIncome));
            writer.WriteLine("TotalSpecialMissionsCompleted={0}", Math.Max(0, snapshot.TotalSpecialMissionsCompleted));
            writer.WriteLine("TotalEmergencyServiceUsages={0}", Math.Max(0, snapshot.TotalEmergencyServiceUsages));
            writer.WriteLine("HighestPrestigeScore={0}", FormatFloat(snapshot.HighestPrestigeScore));
            writer.WriteLine("HighestDoctrineId={0}", snapshot.HighestDoctrineId ?? string.Empty);
            writer.WriteLine("HighestDoctrineTier={0}", Math.Max(0, snapshot.HighestDoctrineTier));
            writer.WriteLine();

            if (snapshot.CommodityTotals != null && snapshot.CommodityTotals.Count > 0)
            {
                writer.WriteLine("[Successes:CommodityTotals]");
                foreach (var commodity in snapshot.CommodityTotals
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.CommodityId) && entry.Tons > 0.001f)
                    .OrderBy(entry => entry.CommodityId, StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteLine("{0}={1}", CommodityCatalog.Normalize(commodity.CommodityId), FormatFloat(commodity.Tons));
                }

                writer.WriteLine();
            }

            if (snapshot.UnlockedSuccessIds != null && snapshot.UnlockedSuccessIds.Count > 0)
            {
                writer.WriteLine("[Successes:Unlocked]");
                foreach (var successId in snapshot.UnlockedSuccessIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteLine("{0}=true", successId.Trim());
                }

                writer.WriteLine();
            }
        }

        private static PlayerStatisticsPersistenceSnapshot ReadPlayerStatisticsSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var hasMainSection = ini.HasSection("Successes");
            var hasCommoditySection = ini.HasSection("Successes:CommodityTotals");
            var hasUnlockedSection = ini.HasSection("Successes:Unlocked");
            if (!hasMainSection && !hasCommoditySection && !hasUnlockedSection)
            {
                return null;
            }

            var snapshot = new PlayerStatisticsPersistenceSnapshot
            {
                IsInitialized = !hasMainSection || ini.GetBool("Successes", "Initialized", true),
                HighestCompanyBalanceEver = ini.GetFloat("Successes", "HighestCompanyBalanceEver", 0f),
                HighestCompanyBalanceBeforeFirstNpcHire = ini.GetFloat("Successes", "HighestCompanyBalanceBeforeFirstNpcHire", 0f),
                HighestCompanyBalanceBeforeFirstLoan = ini.GetFloat("Successes", "HighestCompanyBalanceBeforeFirstLoan", 0f),
                TotalSuccessfulDeliveries = ParseInt(ini.GetString("Successes", "TotalSuccessfulDeliveries", "0"), 0),
                TotalSuccessfulCleanDeliveries = ParseInt(ini.GetString("Successes", "TotalSuccessfulCleanDeliveries", "0"), 0),
                DeliveriesBeforeFirstNpcHire = ParseInt(ini.GetString("Successes", "DeliveriesBeforeFirstNpcHire", "0"), 0),
                TotalTransportedTons = ini.GetFloat("Successes", "TotalTransportedTons", 0f),
                HasEverHiredNpc = ini.GetBool("Successes", "HasEverHiredNpc", false),
                HasEverTakenLoan = ini.GetBool("Successes", "HasEverTakenLoan", false),
                CumulativeNpcDeliveryIncome = ini.GetFloat("Successes", "CumulativeNpcDeliveryIncome", 0f),
                TotalSpecialMissionsCompleted = ParseInt(ini.GetString("Successes", "TotalSpecialMissionsCompleted", "0"), 0),
                TotalEmergencyServiceUsages = ParseInt(ini.GetString("Successes", "TotalEmergencyServiceUsages", "0"), 0),
                HighestPrestigeScore = ini.GetFloat("Successes", "HighestPrestigeScore", 0f),
                HighestDoctrineId = ini.GetString("Successes", "HighestDoctrineId", string.Empty),
                HighestDoctrineTier = ParseInt(ini.GetString("Successes", "HighestDoctrineTier", "0"), 0),
            };

            if (hasCommoditySection)
            {
                var commoditySection = ini.GetSection("Successes:CommodityTotals");
                foreach (var pair in commoditySection.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var commodityId = CommodityCatalog.Normalize(pair.Key);
                    if (string.IsNullOrWhiteSpace(commodityId))
                    {
                        continue;
                    }

                    snapshot.CommodityTotals.Add(new PlayerCommodityStatisticSnapshot
                    {
                        CommodityId = commodityId,
                        Tons = Math.Max(0f, ParseFloat(pair.Value, 0f)),
                    });
                }
            }

            if (hasUnlockedSection)
            {
                var unlockedSection = ini.GetSection("Successes:Unlocked");
                foreach (var pair in unlockedSection.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        snapshot.UnlockedSuccessIds.Add(pair.Key.Trim());
                    }
                }
            }

            return snapshot;
        }

        private static void WritePlayerContractsSnapshot(StreamWriter writer, PlayerContractsPersistenceSnapshot snapshot)
        {
            if (writer == null || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            writer.WriteLine("[PlayerContracts]");
            writer.WriteLine("NextContractId={0}", Math.Max(1, snapshot.NextContractId));
            writer.WriteLine("LastBoardRefreshMinute={0}", snapshot.LastBoardRefreshMinute);
            writer.WriteLine("SelectedCommodityFilter={0}", snapshot.SelectedCommodityFilter ?? string.Empty);
            writer.WriteLine();

            if (snapshot.Contracts != null)
            {
                foreach (var contract in snapshot.Contracts.OrderBy(entry => entry != null ? entry.Id : string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    if (contract == null || string.IsNullOrWhiteSpace(contract.Id))
                    {
                        continue;
                    }

                    writer.WriteLine("[{0}]", BuildPlayerContractSectionName(contract.Id));
                    writer.WriteLine("Type={0}", contract.Type);
                    writer.WriteLine("Status={0}", contract.Status);
                    writer.WriteLine("Commodity={0}", contract.Commodity ?? string.Empty);
                    writer.WriteLine("OriginIndustryId={0}", contract.OriginIndustryId ?? string.Empty);
                    writer.WriteLine("DestinationIndustryId={0}", contract.DestinationIndustryId ?? string.Empty);
                    writer.WriteLine("ListedTons={0}", FormatFloat(contract.ListedTons));
                    writer.WriteLine("LoadedTons={0}", FormatFloat(contract.LoadedTons));
                    writer.WriteLine("DeliveredTons={0}", FormatFloat(contract.DeliveredTons));
                    writer.WriteLine("RouteDistanceMeters={0}", FormatFloat(contract.RouteDistanceMeters));
                    writer.WriteLine("QuotedUnitPrice={0}", FormatFloat(contract.QuotedUnitPrice));
                    writer.WriteLine("QuotedGrossPayout={0}", FormatFloat(contract.QuotedGrossPayout));
                    writer.WriteLine("QuotedImbalanceScore={0}", FormatFloat(contract.QuotedImbalanceScore));
                    writer.WriteLine("ListedAtMinute={0}", contract.ListedAtMinute);
                    writer.WriteLine("ExpiryMinute={0}", contract.ExpiryMinute);
                    writer.WriteLine("AcceptedAtMinute={0}", contract.AcceptedAtMinute);
                    writer.WriteLine("AcceptedExpiryMinute={0}", contract.AcceptedExpiryMinute);
                    writer.WriteLine("VehicleRequirementLabel={0}", contract.VehicleRequirementLabel ?? string.Empty);
                    writer.WriteLine("SuppliesVehicle={0}", contract.SuppliesVehicle ? "true" : "false");
                    writer.WriteLine("RequiresOwnedVehicle={0}", contract.RequiresOwnedVehicle ? "true" : "false");
                    writer.WriteLine("AssignedCommercialVehicleAssetId={0}", contract.AssignedCommercialVehicleAssetId ?? string.Empty);
                    writer.WriteLine("AssignedCommercialVehicleDisplayName={0}", contract.AssignedCommercialVehicleDisplayName ?? string.Empty);
                    writer.WriteLine("QuickJobPoweredModelName={0}", contract.QuickJobPoweredModelName ?? string.Empty);
                    writer.WriteLine("QuickJobCargoModelName={0}", contract.QuickJobCargoModelName ?? string.Empty);
                    writer.WriteLine("QuickJobHasSeparateCargoVehicle={0}", contract.QuickJobHasSeparateCargoVehicle ? "true" : "false");
                    writer.WriteLine("QuickJobCapacityTons={0}", FormatFloat(contract.QuickJobCapacityTons));
                    writer.WriteLine("QuickJobNeedsDeploy={0}", contract.QuickJobNeedsDeploy ? "true" : "false");
                    writer.WriteLine("CargoCondition={0}", FormatFloat(contract.CargoCondition));
                    writer.WriteLine("TotalLostTons={0}", FormatFloat(contract.TotalLostTons));
                    writer.WriteLine("SourceDistrictName={0}", contract.SourceDistrictName ?? string.Empty);
                    writer.WriteLine("StatusMessage={0}", contract.StatusMessage ?? string.Empty);
                    writer.WriteLine();
                }
            }

            if (snapshot.Cooldowns == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Cooldowns.Count; i++)
            {
                var cooldown = snapshot.Cooldowns[i];
                if (cooldown == null || string.IsNullOrWhiteSpace(cooldown.RouteKey))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildPlayerContractCooldownSectionName(i + 1));
                writer.WriteLine("RouteKey={0}", cooldown.RouteKey ?? string.Empty);
                writer.WriteLine("AvailableAgainMinute={0}", cooldown.AvailableAgainMinute);
                writer.WriteLine();
            }
        }

        private static PlayerContractsPersistenceSnapshot ReadPlayerContractsSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new PlayerContractsPersistenceSnapshot();
            if (ini.HasSection("PlayerContracts"))
            {
                snapshot.NextContractId = ParseInt(ini.GetString("PlayerContracts", "NextContractId", "1"), 1);
                snapshot.LastBoardRefreshMinute = ParseInt(ini.GetString("PlayerContracts", "LastBoardRefreshMinute", "-1"), -1);
                snapshot.SelectedCommodityFilter = CommodityCatalog.Normalize(ini.GetString("PlayerContracts", "SelectedCommodityFilter", string.Empty));
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                if (section.StartsWith("PlayerContract:", StringComparison.OrdinalIgnoreCase))
                {
                    var contractId = section.Substring("PlayerContract:".Length).Trim();
                    if (string.IsNullOrWhiteSpace(contractId))
                    {
                        continue;
                    }

                    snapshot.Contracts.Add(new PlayerContractSnapshot
                    {
                        Id = contractId,
                        Type = ParsePlayerContractType(ini.GetString(section, "Type", PlayerContractType.QuickJob.ToString()), PlayerContractType.QuickJob),
                        Status = ParsePlayerContractStatus(ini.GetString(section, "Status", PlayerContractStatus.Accepted.ToString()), PlayerContractStatus.Accepted),
                        Commodity = CommodityCatalog.Normalize(ini.GetString(section, "Commodity", string.Empty)),
                        OriginIndustryId = ini.GetString(section, "OriginIndustryId", string.Empty),
                        DestinationIndustryId = ini.GetString(section, "DestinationIndustryId", string.Empty),
                        ListedTons = ini.GetFloat(section, "ListedTons", 0f),
                        LoadedTons = ini.GetFloat(section, "LoadedTons", 0f),
                        DeliveredTons = ini.GetFloat(section, "DeliveredTons", 0f),
                        RouteDistanceMeters = ini.GetFloat(section, "RouteDistanceMeters", 0f),
                        QuotedUnitPrice = ini.GetFloat(section, "QuotedUnitPrice", 0f),
                        QuotedGrossPayout = ini.GetFloat(section, "QuotedGrossPayout", 0f),
                        QuotedImbalanceScore = ini.GetFloat(section, "QuotedImbalanceScore", 0f),
                        ListedAtMinute = ParseInt(ini.GetString(section, "ListedAtMinute", "0"), 0),
                        ExpiryMinute = ParseInt(ini.GetString(section, "ExpiryMinute", "0"), 0),
                        AcceptedAtMinute = ParseInt(ini.GetString(section, "AcceptedAtMinute", "0"), 0),
                        AcceptedExpiryMinute = ParseInt(ini.GetString(section, "AcceptedExpiryMinute", "0"), 0),
                        VehicleRequirementLabel = ini.GetString(section, "VehicleRequirementLabel", string.Empty),
                        SuppliesVehicle = ini.GetBool(section, "SuppliesVehicle", false),
                        RequiresOwnedVehicle = ini.GetBool(section, "RequiresOwnedVehicle", false),
                        AssignedCommercialVehicleAssetId = ini.GetString(section, "AssignedCommercialVehicleAssetId", string.Empty),
                        AssignedCommercialVehicleDisplayName = ini.GetString(section, "AssignedCommercialVehicleDisplayName", string.Empty),
                        QuickJobPoweredModelName = ini.GetString(section, "QuickJobPoweredModelName", string.Empty),
                        QuickJobCargoModelName = ini.GetString(section, "QuickJobCargoModelName", string.Empty),
                        QuickJobHasSeparateCargoVehicle = ini.GetBool(section, "QuickJobHasSeparateCargoVehicle", false),
                        QuickJobCapacityTons = ini.GetFloat(section, "QuickJobCapacityTons", 0f),
                        QuickJobNeedsDeploy = ini.GetBool(section, "QuickJobNeedsDeploy", false),
                        CargoCondition = ini.GetFloat(section, "CargoCondition", 1f),
                        TotalLostTons = ini.GetFloat(section, "TotalLostTons", 0f),
                        SourceDistrictName = ini.GetString(section, "SourceDistrictName", string.Empty),
                        StatusMessage = ini.GetString(section, "StatusMessage", string.Empty),
                    });

                    continue;
                }

                if (!section.StartsWith("PlayerContractCooldown:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                snapshot.Cooldowns.Add(new PlayerContractCooldownSnapshot
                {
                    RouteKey = ini.GetString(section, "RouteKey", string.Empty),
                    AvailableAgainMinute = ParseInt(ini.GetString(section, "AvailableAgainMinute", "0"), 0),
                });
            }

            return snapshot.HasData ? snapshot : null;
        }

        private static void WriteNamedSeriesPersistence(
            StreamWriter writer,
            string sectionPrefix,
            IEnumerable<TabletNamedTimeSeriesPersistence> entries,
            Func<string, string> normalizeKey)
        {
            if (writer == null || entries == null)
            {
                return;
            }

            foreach (var entry in entries.OrderBy(x => x != null ? x.Key : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Series == null)
                {
                    continue;
                }

                var normalizedKey = normalizeKey != null ? normalizeKey(entry.Key) : entry.Key;
                if (string.IsNullOrWhiteSpace(normalizedKey))
                {
                    continue;
                }

                WriteTimeSeriesPersistence(writer, sectionPrefix + normalizedKey.Trim(), entry.Series);
            }
        }

        private static void WriteTimeSeriesPersistence(StreamWriter writer, string sectionName, TabletTimeSeriesPersistence series)
        {
            if (writer == null || string.IsNullOrWhiteSpace(sectionName) || series == null || !series.HasData)
            {
                return;
            }

            writer.WriteLine("[{0}]", sectionName);
            if (series.Timeframes != null)
            {
                foreach (var timeframe in series.Timeframes.OrderBy(x => x != null ? x.Timeframe : TabletGraphTimeframe.FiveMinutes))
                {
                    if (timeframe == null || !timeframe.HasData)
                    {
                        continue;
                    }

                    writer.WriteLine("{0}.Values={1}", timeframe.Timeframe, FormatFloatList(timeframe.Values));
                    writer.WriteLine("{0}.PendingSampleCount={1}", timeframe.Timeframe, timeframe.PendingSampleCount);
                    writer.WriteLine("{0}.PendingSum={1}", timeframe.Timeframe, FormatFloat(timeframe.PendingSum));
                }
            }

            writer.WriteLine();
        }

        private static TabletTimeSeriesPersistence ReadTimeSeriesPersistence(IniFile ini, string sectionName)
        {
            if (ini == null || string.IsNullOrWhiteSpace(sectionName) || !ini.HasSection(sectionName))
            {
                return null;
            }

            var persistence = new TabletTimeSeriesPersistence();
            var hasData = false;
            for (int i = 0; i < TabletGraphTimeframeCatalog.All.Count; i++)
            {
                var timeframe = TabletGraphTimeframeCatalog.All[i];
                var values = ParseFloatList(ini.GetString(sectionName, timeframe + ".Values", string.Empty));
                var pendingSampleCount = ParseInt(ini.GetString(sectionName, timeframe + ".PendingSampleCount", "0"), 0);
                var pendingSum = ini.GetFloat(sectionName, timeframe + ".PendingSum", 0f);
                if (values.Count == 0 && pendingSampleCount <= 0 && Math.Abs(pendingSum) <= 0.001f)
                {
                    continue;
                }

                var timeframePersistence = new TabletTimeframeHistoryPersistence
                {
                    Timeframe = timeframe,
                    PendingSampleCount = pendingSampleCount,
                    PendingSum = pendingSum,
                };
                for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
                {
                    timeframePersistence.Values.Add(values[valueIndex]);
                }

                persistence.Timeframes.Add(timeframePersistence);
                hasData = true;
            }

            return hasData ? persistence : null;
        }

        private static string BuildAnalyticsProfitSectionName()
        {
            return "Analytics:Profit";
        }

        private static void WriteOwnedFleetSnapshot(StreamWriter writer, OwnedFleetPersistenceSnapshot snapshot)
        {
            if (writer == null || snapshot == null || snapshot.Vehicles == null || snapshot.Vehicles.Count == 0)
            {
                return;
            }

            var orderedVehicles = snapshot.Vehicles
                .Where(vehicle => vehicle != null && !string.IsNullOrWhiteSpace(vehicle.PoweredModelName))
                .OrderBy(vehicle => vehicle.PoweredModelName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(vehicle => vehicle.PoweredPosition.X)
                .ThenBy(vehicle => vehicle.PoweredPosition.Y)
                .ToList();

            for (int i = 0; i < orderedVehicles.Count; i++)
            {
                var vehicle = orderedVehicles[i];
                writer.WriteLine("[{0}]", BuildOwnedFleetSectionName(i + 1));
                writer.WriteLine("PoweredModelName={0}", vehicle.PoweredModelName ?? string.Empty);
                writer.WriteLine("CargoModelName={0}", vehicle.CargoModelName ?? string.Empty);
                writer.WriteLine("HasSeparateCargoVehicle={0}", vehicle.HasSeparateCargoVehicle ? "true" : "false");
                writer.WriteLine("PoweredPosition={0}", FormatVector3(vehicle.PoweredPosition));
                writer.WriteLine("PoweredHeading={0}", FormatFloat(vehicle.PoweredHeading));
                writer.WriteLine("CargoType={0}", vehicle.CargoType);
                writer.WriteLine("CapacityTons={0}", FormatFloat(vehicle.CapacityTons));
                writer.WriteLine("Commodity={0}", vehicle.Commodity ?? string.Empty);
                writer.WriteLine("WeightTons={0}", FormatFloat(vehicle.WeightTons));
                writer.WriteLine("CargoCondition={0}", FormatFloat(vehicle.CargoCondition));
                writer.WriteLine("TotalLostTons={0}", FormatFloat(vehicle.TotalLostTons));
                writer.WriteLine("SourceIndustryId={0}", vehicle.SourceIndustryId ?? string.Empty);
                writer.WriteLine("SourceDistrictName={0}", vehicle.SourceDistrictName ?? string.Empty);
                writer.WriteLine("PlayerContractId={0}", vehicle.PlayerContractId ?? string.Empty);
                writer.WriteLine("PlayerContractDestinationIndustryId={0}", vehicle.PlayerContractDestinationIndustryId ?? string.Empty);
                writer.WriteLine("CurrentFuelLiters={0}", FormatFloat(vehicle.CurrentFuelLiters));
                writer.WriteLine("MaintenanceCondition={0}", FormatFloat(vehicle.MaintenanceCondition));
                writer.WriteLine("LastMaintenanceWeekIndex={0}", vehicle.LastMaintenanceWeekIndex);
                writer.WriteLine("LastInspectionWeekIndex={0}", vehicle.LastInspectionWeekIndex);
                writer.WriteLine("InspectionOverdueWeeks={0}", vehicle.InspectionOverdueWeeks);
                writer.WriteLine("LifetimeMaintenanceCost={0}", FormatFloat(vehicle.LifetimeMaintenanceCost));
                writer.WriteLine();
            }
        }

        private static OwnedFleetPersistenceSnapshot ReadOwnedFleetSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new OwnedFleetPersistenceSnapshot();
            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("OwnedFleet:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var poweredModelName = ini.GetString(section, "PoweredModelName", string.Empty);
                if (string.IsNullOrWhiteSpace(poweredModelName))
                {
                    continue;
                }

                snapshot.Vehicles.Add(new OwnedFleetVehicleSnapshot
                {
                    PoweredModelName = poweredModelName,
                    CargoModelName = ini.GetString(section, "CargoModelName", string.Empty),
                    HasSeparateCargoVehicle = ini.GetBool(section, "HasSeparateCargoVehicle", false),
                    PoweredPosition = ParseVector3(ini.GetString(section, "PoweredPosition", string.Empty), Vector3.Zero),
                    PoweredHeading = ini.GetFloat(section, "PoweredHeading", 0f),
                    CargoType = ParseVehicleCargoType(ini.GetString(section, "CargoType", VehicleCargoType.Unknown.ToString()), VehicleCargoType.Unknown),
                    CapacityTons = ini.GetFloat(section, "CapacityTons", 0f),
                    Commodity = CommodityCatalog.Normalize(ini.GetString(section, "Commodity", string.Empty)),
                    WeightTons = ini.GetFloat(section, "WeightTons", 0f),
                    CargoCondition = ini.GetFloat(section, "CargoCondition", 1f),
                    TotalLostTons = ini.GetFloat(section, "TotalLostTons", 0f),
                    SourceIndustryId = ini.GetString(section, "SourceIndustryId", string.Empty),
                    SourceDistrictName = ini.GetString(section, "SourceDistrictName", string.Empty),
                    PlayerContractId = ini.GetString(section, "PlayerContractId", string.Empty),
                    PlayerContractDestinationIndustryId = ini.GetString(section, "PlayerContractDestinationIndustryId", string.Empty),
                    CurrentFuelLiters = ini.GetFloat(section, "CurrentFuelLiters", 0f),
                    MaintenanceCondition = ini.GetFloat(section, "MaintenanceCondition", 1f),
                    LastMaintenanceWeekIndex = ParseInt(ini.GetString(section, "LastMaintenanceWeekIndex", "-1"), -1),
                    LastInspectionWeekIndex = ParseInt(ini.GetString(section, "LastInspectionWeekIndex", "-1"), -1),
                    InspectionOverdueWeeks = ParseInt(ini.GetString(section, "InspectionOverdueWeeks", "0"), 0),
                    LifetimeMaintenanceCost = ini.GetFloat(section, "LifetimeMaintenanceCost", 0f),
                });
            }

            return snapshot.HasData ? snapshot : null;
        }
        private static void WritePropertyOwnershipSnapshot(StreamWriter writer, PropertyOwnershipPersistenceSnapshot snapshot)
        {
            if (writer == null || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            writer.WriteLine("[Properties]");
            writer.WriteLine("ActiveOfficeId={0}", snapshot.ActiveOfficeId ?? string.Empty);
            writer.WriteLine("ActiveApartmentId={0}", snapshot.ActiveApartmentId ?? string.Empty);
            writer.WriteLine("LastSuccessfulApartmentSleepMinute={0}", snapshot.LastSuccessfulApartmentSleepMinute);
            writer.WriteLine("LastCorporateOverheadWeekIndex={0}", snapshot.LastCorporateOverheadWeekIndex);
            writer.WriteLine();

            foreach (var office in snapshot.Offices.OrderBy(entry => entry != null ? entry.OfficeId : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (office == null || string.IsNullOrWhiteSpace(office.OfficeId))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildPropertyOfficeSectionName(office.OfficeId));
                writer.WriteLine("IsOwned={0}", office.IsOwned ? "true" : "false");
                writer.WriteLine("IsRented={0}", office.IsRented ? "true" : "false");
                writer.WriteLine("IsAccessSuspended={0}", office.IsAccessSuspended ? "true" : "false");
                writer.WriteLine("OutstandingRent={0}", FormatFloat(office.OutstandingRent));
                writer.WriteLine("LastChargedWeekIndex={0}", office.LastChargedWeekIndex);
                writer.WriteLine();
            }

            foreach (var officeObject in snapshot.OfficeObjects.OrderBy(entry => entry != null ? entry.InstanceId : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (officeObject == null || string.IsNullOrWhiteSpace(officeObject.InstanceId) || string.IsNullOrWhiteSpace(officeObject.OfficeId) || officeObject.DefinitionId <= 0)
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildPropertyOfficeObjectSectionName(officeObject.InstanceId));
                writer.WriteLine("OfficeId={0}", officeObject.OfficeId ?? string.Empty);
                writer.WriteLine("DefinitionId={0}", officeObject.DefinitionId);
                writer.WriteLine("IsPlaced={0}", officeObject.IsPlaced ? "true" : "false");
                writer.WriteLine("Position={0}", FormatVector3(officeObject.Position));
                writer.WriteLine("Rotation={0}", FormatVector3(officeObject.Rotation));
                writer.WriteLine("StoredResourceAmount={0}", FormatFloat(officeObject.StoredResourceAmount));
                writer.WriteLine();
            }

            foreach (var apartment in snapshot.Apartments.OrderBy(entry => entry != null ? entry.InteriorId : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (apartment == null || string.IsNullOrWhiteSpace(apartment.InteriorId))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildPropertyApartmentSectionName(apartment.InteriorId));
                writer.WriteLine("IsOwned={0}", apartment.IsOwned ? "true" : "false");
                writer.WriteLine("IsRented={0}", apartment.IsRented ? "true" : "false");
                writer.WriteLine("IsAccessSuspended={0}", apartment.IsAccessSuspended ? "true" : "false");
                writer.WriteLine("OutstandingRent={0}", FormatFloat(apartment.OutstandingRent));
                writer.WriteLine("LastChargedWeekIndex={0}", apartment.LastChargedWeekIndex);
                writer.WriteLine();
            }

            foreach (var vehicle in snapshot.CommercialVehicles.OrderBy(entry => entry != null ? entry.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.AssetId) || string.IsNullOrWhiteSpace(vehicle.PoweredModelName))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildPropertyCommercialVehicleSectionName(vehicle.AssetId));
                writer.WriteLine("DisplayName={0}", vehicle.DisplayName ?? string.Empty);
                writer.WriteLine("PoweredModelName={0}", vehicle.PoweredModelName ?? string.Empty);
                writer.WriteLine("CargoModelName={0}", vehicle.CargoModelName ?? string.Empty);
                writer.WriteLine("HasSeparateCargoVehicle={0}", vehicle.HasSeparateCargoVehicle ? "true" : "false");
                writer.WriteLine("PurchasePrice={0}", FormatFloat(vehicle.PurchasePrice));
                writer.WriteLine("AssignedOfficeId={0}", vehicle.AssignedOfficeId ?? string.Empty);
                writer.WriteLine("IsRental={0}", vehicle.IsRental ? "true" : "false");
                writer.WriteLine("DailyRent={0}", FormatFloat(vehicle.DailyRent));
                writer.WriteLine("LastChargedDayIndex={0}", vehicle.LastChargedDayIndex);
                writer.WriteLine("InActiveGarage={0}", vehicle.InActiveGarage ? "true" : "false");
                writer.WriteLine("IsDeployed={0}", vehicle.IsDeployed ? "true" : "false");
                writer.WriteLine("PoweredPosition={0}", FormatVector3(vehicle.PoweredPosition));
                writer.WriteLine("PoweredHeading={0}", FormatFloat(vehicle.PoweredHeading));
                writer.WriteLine("CargoType={0}", vehicle.CargoType);
                writer.WriteLine("CapacityTons={0}", FormatFloat(vehicle.CapacityTons));
                writer.WriteLine("Commodity={0}", vehicle.Commodity ?? string.Empty);
                writer.WriteLine("WeightTons={0}", FormatFloat(vehicle.WeightTons));
                writer.WriteLine("CargoCondition={0}", FormatFloat(vehicle.CargoCondition));
                writer.WriteLine("TotalLostTons={0}", FormatFloat(vehicle.TotalLostTons));
                writer.WriteLine("SourceIndustryId={0}", vehicle.SourceIndustryId ?? string.Empty);
                writer.WriteLine("SourceDistrictName={0}", vehicle.SourceDistrictName ?? string.Empty);
                writer.WriteLine("PlayerContractId={0}", vehicle.PlayerContractId ?? string.Empty);
                writer.WriteLine("PlayerContractDestinationIndustryId={0}", vehicle.PlayerContractDestinationIndustryId ?? string.Empty);
                writer.WriteLine("CurrentFuelLiters={0}", FormatFloat(vehicle.CurrentFuelLiters));
                writer.WriteLine("MaintenanceCondition={0}", FormatFloat(vehicle.MaintenanceCondition));
                writer.WriteLine("LastMaintenanceWeekIndex={0}", vehicle.LastMaintenanceWeekIndex);
                writer.WriteLine("LastInspectionWeekIndex={0}", vehicle.LastInspectionWeekIndex);
                writer.WriteLine("InspectionOverdueWeeks={0}", vehicle.InspectionOverdueWeeks);
                writer.WriteLine("LifetimeMaintenanceCost={0}", FormatFloat(vehicle.LifetimeMaintenanceCost));
                WriteVehicleAppearanceSnapshot(writer, "PoweredAppearance", vehicle.PoweredAppearance);
                WriteVehicleAppearanceSnapshot(writer, "CargoAppearance", vehicle.CargoAppearance);
                writer.WriteLine();
            }

            foreach (var vehicle in snapshot.PersonalVehicles.OrderBy(entry => entry != null ? entry.DisplayName : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.AssetId) || string.IsNullOrWhiteSpace(vehicle.ModelName))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildPropertyPersonalVehicleSectionName(vehicle.AssetId));
                writer.WriteLine("DisplayName={0}", vehicle.DisplayName ?? string.Empty);
                writer.WriteLine("ModelName={0}", vehicle.ModelName ?? string.Empty);
                writer.WriteLine("Category={0}", vehicle.Category ?? string.Empty);
                writer.WriteLine("PurchasePrice={0}", FormatFloat(vehicle.PurchasePrice));
                writer.WriteLine("AssignedApartmentId={0}", vehicle.AssignedApartmentId ?? string.Empty);
                writer.WriteLine("IsDeployed={0}", vehicle.IsDeployed ? "true" : "false");
                writer.WriteLine("Position={0}", FormatVector3(vehicle.Position));
                writer.WriteLine("Heading={0}", FormatFloat(vehicle.Heading));
                WriteVehicleAppearanceSnapshot(writer, "Appearance", vehicle.Appearance);
                writer.WriteLine();
            }
        }

        private static PropertyOwnershipPersistenceSnapshot ReadPropertyOwnershipSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new PropertyOwnershipPersistenceSnapshot();
            if (ini.HasSection("Properties"))
            {
                snapshot.ActiveOfficeId = ini.GetString("Properties", "ActiveOfficeId", string.Empty);
                snapshot.ActiveApartmentId = ini.GetString("Properties", "ActiveApartmentId", string.Empty);
                snapshot.LastSuccessfulApartmentSleepMinute = ParseInt(ini.GetString("Properties", "LastSuccessfulApartmentSleepMinute", "-1"), -1);
                snapshot.LastCorporateOverheadWeekIndex = ParseInt(ini.GetString("Properties", "LastCorporateOverheadWeekIndex", "-1"), -1);
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                if (section.StartsWith("PropertyOffice:", StringComparison.OrdinalIgnoreCase))
                {
                    var officeId = section.Substring("PropertyOffice:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(officeId))
                    {
                        snapshot.Offices.Add(new OfficeOwnershipPersistenceEntry
                        {
                            OfficeId = officeId,
                            IsOwned = ini.GetBool(section, "IsOwned", false),
                            IsRented = ini.GetBool(section, "IsRented", false),
                            IsAccessSuspended = ini.GetBool(section, "IsAccessSuspended", false),
                            OutstandingRent = ini.GetFloat(section, "OutstandingRent", 0f),
                            LastChargedWeekIndex = ParseInt(ini.GetString(section, "LastChargedWeekIndex", "-1"), -1),
                        });
                    }

                    continue;
                }

                if (section.StartsWith("PropertyApartment:", StringComparison.OrdinalIgnoreCase))
                {
                    var interiorId = section.Substring("PropertyApartment:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(interiorId))
                    {
                        snapshot.Apartments.Add(new ApartmentOwnershipPersistenceEntry
                        {
                            InteriorId = interiorId,
                            IsOwned = ini.GetBool(section, "IsOwned", false),
                            IsRented = ini.GetBool(section, "IsRented", false),
                            IsAccessSuspended = ini.GetBool(section, "IsAccessSuspended", false),
                            OutstandingRent = ini.GetFloat(section, "OutstandingRent", 0f),
                            LastChargedWeekIndex = ParseInt(ini.GetString(section, "LastChargedWeekIndex", "-1"), -1),
                        });
                    }

                    continue;
                }

                if (section.StartsWith("PropertyOfficeObject:", StringComparison.OrdinalIgnoreCase))
                {
                    var instanceId = section.Substring("PropertyOfficeObject:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(instanceId))
                    {
                        snapshot.OfficeObjects.Add(new OfficeObjectPersistenceEntry
                        {
                            InstanceId = instanceId,
                            OfficeId = ini.GetString(section, "OfficeId", string.Empty),
                            DefinitionId = ParseInt(ini.GetString(section, "DefinitionId", "0"), 0),
                            IsPlaced = ini.GetBool(section, "IsPlaced", false),
                            Position = ParseVector3(ini.GetString(section, "Position", string.Empty), Vector3.Zero),
                            Rotation = ParseVector3(ini.GetString(section, "Rotation", string.Empty), Vector3.Zero),
                            StoredResourceAmount = ini.GetFloat(section, "StoredResourceAmount", 0f),
                        });
                    }

                    continue;
                }

                if (section.StartsWith("PropertyCommercialVehicle:", StringComparison.OrdinalIgnoreCase))
                {
                    var assetId = section.Substring("PropertyCommercialVehicle:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(assetId))
                    {
                        snapshot.CommercialVehicles.Add(new OwnedCommercialVehiclePersistenceEntry
                        {
                            AssetId = assetId,
                            DisplayName = ini.GetString(section, "DisplayName", string.Empty),
                            PoweredModelName = ini.GetString(section, "PoweredModelName", string.Empty),
                            CargoModelName = ini.GetString(section, "CargoModelName", string.Empty),
                            HasSeparateCargoVehicle = ini.GetBool(section, "HasSeparateCargoVehicle", false),
                            PurchasePrice = ini.GetFloat(section, "PurchasePrice", 0f),
                            AssignedOfficeId = ini.GetString(section, "AssignedOfficeId", string.Empty),
                            IsRental = ini.GetBool(section, "IsRental", false),
                            DailyRent = ini.GetFloat(section, "DailyRent", 0f),
                            LastChargedDayIndex = ParseInt(ini.GetString(section, "LastChargedDayIndex", "-1"), -1),
                            InActiveGarage = ini.GetBool(section, "InActiveGarage", false),
                            IsDeployed = ini.GetBool(section, "IsDeployed", false),
                            PoweredPosition = ParseVector3(ini.GetString(section, "PoweredPosition", string.Empty), Vector3.Zero),
                            PoweredHeading = ini.GetFloat(section, "PoweredHeading", 0f),
                            CargoType = ParseVehicleCargoType(ini.GetString(section, "CargoType", VehicleCargoType.Unknown.ToString()), VehicleCargoType.Unknown),
                            CapacityTons = ini.GetFloat(section, "CapacityTons", 0f),
                            Commodity = CommodityCatalog.Normalize(ini.GetString(section, "Commodity", string.Empty)),
                            WeightTons = ini.GetFloat(section, "WeightTons", 0f),
                            CargoCondition = ini.GetFloat(section, "CargoCondition", 1f),
                            TotalLostTons = ini.GetFloat(section, "TotalLostTons", 0f),
                            SourceIndustryId = ini.GetString(section, "SourceIndustryId", string.Empty),
                            SourceDistrictName = ini.GetString(section, "SourceDistrictName", string.Empty),
                            PlayerContractId = ini.GetString(section, "PlayerContractId", string.Empty),
                            PlayerContractDestinationIndustryId = ini.GetString(section, "PlayerContractDestinationIndustryId", string.Empty),
                            CurrentFuelLiters = ini.GetFloat(section, "CurrentFuelLiters", 0f),
                            MaintenanceCondition = ini.GetFloat(section, "MaintenanceCondition", 1f),
                            LastMaintenanceWeekIndex = ParseInt(ini.GetString(section, "LastMaintenanceWeekIndex", "-1"), -1),
                            LastInspectionWeekIndex = ParseInt(ini.GetString(section, "LastInspectionWeekIndex", "-1"), -1),
                            InspectionOverdueWeeks = ParseInt(ini.GetString(section, "InspectionOverdueWeeks", "0"), 0),
                            LifetimeMaintenanceCost = ini.GetFloat(section, "LifetimeMaintenanceCost", 0f),
                            PoweredAppearance = ReadVehicleAppearanceSnapshot(ini, section, "PoweredAppearance"),
                            CargoAppearance = ReadVehicleAppearanceSnapshot(ini, section, "CargoAppearance"),
                        });
                    }

                    continue;
                }

                if (section.StartsWith("PropertyPersonalVehicle:", StringComparison.OrdinalIgnoreCase))
                {
                    var assetId = section.Substring("PropertyPersonalVehicle:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(assetId))
                    {
                        snapshot.PersonalVehicles.Add(new OwnedPersonalVehiclePersistenceEntry
                        {
                            AssetId = assetId,
                            DisplayName = ini.GetString(section, "DisplayName", string.Empty),
                            ModelName = ini.GetString(section, "ModelName", string.Empty),
                            Category = ini.GetString(section, "Category", string.Empty),
                            PurchasePrice = ini.GetFloat(section, "PurchasePrice", 0f),
                            AssignedApartmentId = ini.GetString(section, "AssignedApartmentId", string.Empty),
                            IsDeployed = ini.GetBool(section, "IsDeployed", false),
                            Position = ParseVector3(ini.GetString(section, "Position", string.Empty), Vector3.Zero),
                            Heading = ini.GetFloat(section, "Heading", 0f),
                            Appearance = ReadVehicleAppearanceSnapshot(ini, section, "Appearance"),
                        });
                    }
                }
            }

            return snapshot.HasData ? snapshot : null;
        }

        private static void WriteVehicleAppearanceSnapshot(StreamWriter writer, string keyPrefix, VehicleAppearancePersistenceSnapshot snapshot)
        {
            if (writer == null || string.IsNullOrWhiteSpace(keyPrefix) || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            if (snapshot.ColorCombination.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "ColorCombination"), snapshot.ColorCombination.Value);
            }

            writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "LicensePlate"), snapshot.LicensePlate ?? string.Empty);

            if (snapshot.LicensePlateStyle.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "LicensePlateStyle"), snapshot.LicensePlateStyle.Value);
            }

            if (snapshot.WindowTint.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "WindowTint"), snapshot.WindowTint.Value);
            }

            if (snapshot.Livery.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "Livery"), snapshot.Livery.Value);
            }

            if (snapshot.WheelType.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "WheelType"), snapshot.WheelType.Value);
            }

            if (snapshot.PrimaryColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "PrimaryColor"), snapshot.PrimaryColor.Value);
            }

            if (snapshot.SecondaryColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "SecondaryColor"), snapshot.SecondaryColor.Value);
            }

            if (snapshot.PearlescentColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "PearlescentColor"), snapshot.PearlescentColor.Value);
            }

            if (snapshot.RimColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "RimColor"), snapshot.RimColor.Value);
            }

            if (snapshot.DashboardColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "DashboardColor"), snapshot.DashboardColor.Value);
            }

            if (snapshot.TrimColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "TrimColor"), snapshot.TrimColor.Value);
            }

            if (snapshot.CustomPrimaryColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "CustomPrimaryColor"), FormatColor(snapshot.CustomPrimaryColor.Value));
            }

            if (snapshot.CustomSecondaryColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "CustomSecondaryColor"), FormatColor(snapshot.CustomSecondaryColor.Value));
            }

            if (snapshot.NeonLightsColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "NeonLightsColor"), FormatColor(snapshot.NeonLightsColor.Value));
            }

            if (snapshot.TireSmokeColor.HasValue)
            {
                writer.WriteLine("{0}={1}", BuildAppearanceKey(keyPrefix, "TireSmokeColor"), FormatColor(snapshot.TireSmokeColor.Value));
            }

            if (snapshot.Mods != null)
            {
                foreach (var mod in snapshot.Mods
                    .Where(entry => entry != null)
                    .OrderBy(entry => entry.Type.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteLine("{0}={1}", BuildAppearanceModKey(keyPrefix, mod.Type, "Index"), mod.Index);
                    writer.WriteLine("{0}={1}", BuildAppearanceModKey(keyPrefix, mod.Type, "Variation"), mod.Variation ? "true" : "false");
                }
            }

            if (snapshot.ToggleMods != null)
            {
                foreach (var toggleMod in snapshot.ToggleMods
                    .Where(entry => entry != null)
                    .OrderBy(entry => entry.Type.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteLine("{0}={1}", BuildAppearanceToggleModKey(keyPrefix, toggleMod.Type), toggleMod.IsInstalled ? "true" : "false");
                }
            }
        }

        private static VehicleAppearancePersistenceSnapshot ReadVehicleAppearanceSnapshot(IniFile ini, string section, string keyPrefix)
        {
            if (ini == null || string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(keyPrefix) || !ini.HasSection(section))
            {
                return null;
            }

            var block = ini.GetSection(section);
            var scopedPrefix = keyPrefix + ".";
            if (block.Count == 0 || !block.Keys.Any(key => key != null && key.StartsWith(scopedPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var snapshot = new VehicleAppearancePersistenceSnapshot();
            string rawValue;
            if (TryGetAppearanceValue(block, keyPrefix, "ColorCombination", out rawValue))
            {
                snapshot.ColorCombination = ParseInt(rawValue, 0);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "LicensePlate", out rawValue))
            {
                snapshot.LicensePlate = rawValue;
            }

            if (TryGetAppearanceValue(block, keyPrefix, "LicensePlateStyle", out rawValue))
            {
                snapshot.LicensePlateStyle = ParseOptionalEnum<LicensePlateStyle>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "WindowTint", out rawValue))
            {
                snapshot.WindowTint = ParseOptionalEnum<VehicleWindowTint>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "Livery", out rawValue))
            {
                snapshot.Livery = ParseInt(rawValue, -1);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "WheelType", out rawValue))
            {
                snapshot.WheelType = ParseOptionalEnum<VehicleWheelType>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "PrimaryColor", out rawValue))
            {
                snapshot.PrimaryColor = ParseOptionalEnum<VehicleColor>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "SecondaryColor", out rawValue))
            {
                snapshot.SecondaryColor = ParseOptionalEnum<VehicleColor>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "PearlescentColor", out rawValue))
            {
                snapshot.PearlescentColor = ParseOptionalEnum<VehicleColor>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "RimColor", out rawValue))
            {
                snapshot.RimColor = ParseOptionalEnum<VehicleColor>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "DashboardColor", out rawValue))
            {
                snapshot.DashboardColor = ParseOptionalEnum<VehicleColor>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "TrimColor", out rawValue))
            {
                snapshot.TrimColor = ParseOptionalEnum<VehicleColor>(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "CustomPrimaryColor", out rawValue))
            {
                snapshot.CustomPrimaryColor = ParseColor(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "CustomSecondaryColor", out rawValue))
            {
                snapshot.CustomSecondaryColor = ParseColor(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "NeonLightsColor", out rawValue))
            {
                snapshot.NeonLightsColor = ParseColor(rawValue);
            }

            if (TryGetAppearanceValue(block, keyPrefix, "TireSmokeColor", out rawValue))
            {
                snapshot.TireSmokeColor = ParseColor(rawValue);
            }

            var modsByType = new Dictionary<VehicleModType, VehicleModPersistenceEntry>();
            foreach (var pair in block)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                if (pair.Key.StartsWith(scopedPrefix + "Mod.", StringComparison.OrdinalIgnoreCase))
                {
                    var remainder = pair.Key.Substring((scopedPrefix + "Mod.").Length).Trim();
                    var separatorIndex = remainder.LastIndexOf('.');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var modTypeRaw = remainder.Substring(0, separatorIndex).Trim();
                    var propertyName = remainder.Substring(separatorIndex + 1).Trim();
                    VehicleModType modType;
                    if (!Enum.TryParse(modTypeRaw, true, out modType))
                    {
                        continue;
                    }

                    VehicleModPersistenceEntry modEntry;
                    if (!modsByType.TryGetValue(modType, out modEntry))
                    {
                        modEntry = new VehicleModPersistenceEntry
                        {
                            Type = modType,
                            Index = -1,
                        };
                        modsByType[modType] = modEntry;
                        snapshot.Mods.Add(modEntry);
                    }

                    if (propertyName.Equals("Index", StringComparison.OrdinalIgnoreCase))
                    {
                        modEntry.Index = ParseInt(pair.Value, -1);
                    }
                    else if (propertyName.Equals("Variation", StringComparison.OrdinalIgnoreCase))
                    {
                        modEntry.Variation = ParseBoolValue(pair.Value, false);
                    }

                    continue;
                }

                if (!pair.Key.StartsWith(scopedPrefix + "ToggleMod.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var toggleTypeRaw = pair.Key.Substring((scopedPrefix + "ToggleMod.").Length).Trim();
                VehicleToggleModType toggleType;
                if (!Enum.TryParse(toggleTypeRaw, true, out toggleType))
                {
                    continue;
                }

                snapshot.ToggleMods.Add(new VehicleToggleModPersistenceEntry
                {
                    Type = toggleType,
                    IsInstalled = ParseBoolValue(pair.Value, false),
                });
            }

            return snapshot.HasData ? snapshot : null;
        }

        private static string BuildAppearanceKey(string keyPrefix, string propertyName)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", keyPrefix ?? string.Empty, propertyName ?? string.Empty);
        }

        private static string BuildAppearanceModKey(string keyPrefix, VehicleModType modType, string propertyName)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.Mod.{1}.{2}", keyPrefix ?? string.Empty, modType, propertyName ?? string.Empty);
        }

        private static string BuildAppearanceToggleModKey(string keyPrefix, VehicleToggleModType toggleModType)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.ToggleMod.{1}", keyPrefix ?? string.Empty, toggleModType);
        }

        private static bool TryGetAppearanceValue(Dictionary<string, string> block, string keyPrefix, string propertyName, out string value)
        {
            value = string.Empty;
            if (block == null)
            {
                return false;
            }

            return block.TryGetValue(BuildAppearanceKey(keyPrefix, propertyName), out value);
        }

        private static void WriteNpcLogisticsSnapshot(StreamWriter writer, NpcLogisticsPersistenceSnapshot snapshot)
        {
            if (writer == null || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            if (HasNpcWorldDispatchData(snapshot))
            {
                writer.WriteLine("[NpcWorldDispatch]");
                writer.WriteLine("DispatchPolicy={0}", snapshot.DispatchPolicy);
                writer.WriteLine("PriorityCommodity={0}", snapshot.PriorityCommodity ?? string.Empty);
                writer.WriteLine("PriorityDistrict={0}", snapshot.PriorityDistrict ?? string.Empty);
                writer.WriteLine("PremiumDispatchEnabled={0}", snapshot.PremiumDispatchEnabled ? "true" : "false");
                writer.WriteLine("OfficeDeliveryNotificationsEnabled={0}", snapshot.OfficeDeliveryNotificationsEnabled ? "true" : "false");
                writer.WriteLine("LastWorldEvaluationClockMinute={0}", snapshot.LastWorldEvaluationClockMinute);
                writer.WriteLine("CompletedWorldDispatches={0}", snapshot.CompletedWorldDispatches);
                writer.WriteLine();
            }

            foreach (var contract in snapshot.Contracts.OrderBy(entry => entry != null ? entry.Id : 0))
            {
                if (contract == null || string.IsNullOrWhiteSpace(contract.OriginIndustryId) || string.IsNullOrWhiteSpace(contract.DestinationIndustryId))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildNpcContractSectionName(contract.Id));
                writer.WriteLine("OriginIndustryId={0}", contract.OriginIndustryId ?? string.Empty);
                writer.WriteLine("DestinationIndustryId={0}", contract.DestinationIndustryId ?? string.Empty);
                writer.WriteLine("Commodity={0}", contract.Commodity ?? string.Empty);
                writer.WriteLine("TierId={0}", contract.TierId ?? string.Empty);
                writer.WriteLine("AssignedVehicleAssetId={0}", contract.AssignedVehicleAssetId ?? string.Empty);
                writer.WriteLine("AssignedVehicleDisplayName={0}", contract.AssignedVehicleDisplayName ?? string.Empty);
                writer.WriteLine("OriginTriggerThresholdPercent={0}", contract.OriginTriggerThresholdPercent);
                writer.WriteLine("DestinationTriggerThresholdPercent={0}", contract.DestinationTriggerThresholdPercent);
                writer.WriteLine("CurrentRouteIndex={0}", contract.CurrentRouteIndex);
                writer.WriteLine("RouteCount={0}", contract.Routes != null ? contract.Routes.Count : 0);
                if (contract.Routes != null)
                {
                    for (int routeIndex = 0; routeIndex < contract.Routes.Count; routeIndex++)
                    {
                        var route = contract.Routes[routeIndex];
                        if (route == null)
                        {
                            continue;
                        }

                        writer.WriteLine("Route{0}OriginIndustryId={1}", routeIndex, route.OriginIndustryId ?? string.Empty);
                        writer.WriteLine("Route{0}DestinationIndustryId={1}", routeIndex, route.DestinationIndustryId ?? string.Empty);
                        writer.WriteLine("Route{0}Commodity={1}", routeIndex, route.Commodity ?? string.Empty);
                        writer.WriteLine("Route{0}AssignedVehicleAssetId={1}", routeIndex, route.AssignedVehicleAssetId ?? string.Empty);
                        writer.WriteLine("Route{0}AssignedVehicleDisplayName={1}", routeIndex, route.AssignedVehicleDisplayName ?? string.Empty);
                        writer.WriteLine("Route{0}OriginTriggerThresholdPercent={1}", routeIndex, route.OriginTriggerThresholdPercent);
                        writer.WriteLine("Route{0}DestinationTriggerThresholdPercent={1}", routeIndex, route.DestinationTriggerThresholdPercent);
                    }
                }
                writer.WriteLine("ContractCost={0}", FormatFloat(contract.ContractCost));
                writer.WriteLine("PayrollElapsedInGameMinutes={0}", contract.PayrollElapsedInGameMinutes);
                writer.WriteLine("CompletedPayrollCycles={0}", contract.CompletedPayrollCycles);
                writer.WriteLine("TotalWeeklyWagesPaid={0}", FormatFloat(contract.TotalWeeklyWagesPaid));
                writer.WriteLine("CompletedDeliveries={0}", contract.CompletedDeliveries);
                writer.WriteLine("TotalDeliveredTons={0}", FormatFloat(contract.TotalDeliveredTons));
                writer.WriteLine("TotalProfitEarned={0}", FormatFloat(contract.TotalProfitEarned));
                writer.WriteLine("LastJourneyLossRatio={0}", FormatFloat(contract.LastJourneyLossRatio));
                writer.WriteLine();
            }

            if (snapshot.WorldJobs == null)
            {
                return;
            }

            foreach (var job in snapshot.WorldJobs.OrderBy(entry => entry != null ? entry.Id : 0))
            {
                if (job == null)
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildNpcWorldJobSectionName(job.Id));
                writer.WriteLine("Type={0}", job.Type);
                writer.WriteLine("Phase={0}", job.Phase);
                writer.WriteLine("Commodity={0}", job.Commodity ?? string.Empty);
                writer.WriteLine("SourceLabel={0}", job.SourceLabel ?? string.Empty);
                writer.WriteLine("DestinationLabel={0}", job.DestinationLabel ?? string.Empty);
                writer.WriteLine("OriginIndustryId={0}", job.OriginIndustryId ?? string.Empty);
                writer.WriteLine("DestinationIndustryId={0}", job.DestinationIndustryId ?? string.Empty);
                writer.WriteLine("Tons={0}", FormatFloat(job.Tons));
                writer.WriteLine("RemainingInGameMinutes={0}", job.RemainingInGameMinutes);
                writer.WriteLine("TotalInGameMinutes={0}", job.TotalInGameMinutes);
                writer.WriteLine("CreatedClockMinute={0}", job.CreatedClockMinute);
                writer.WriteLine("IsSpotOpportunity={0}", job.IsSpotOpportunity ? "true" : "false");
                writer.WriteLine("UsesPremiumDispatch={0}", job.UsesPremiumDispatch ? "true" : "false");
                writer.WriteLine("IsPriorityMatch={0}", job.IsPriorityMatch ? "true" : "false");
                writer.WriteLine("HasVisibleConvoy={0}", job.HasVisibleConvoy ? "true" : "false");
                writer.WriteLine("IsRivalJob={0}", job.IsRivalJob ? "true" : "false");
                writer.WriteLine("BackhaulDepth={0}", job.BackhaulDepth);
                writer.WriteLine("StatusText={0}", job.StatusText ?? string.Empty);
                writer.WriteLine();
            }
        }

        private static NpcLogisticsPersistenceSnapshot ReadNpcLogisticsSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new NpcLogisticsPersistenceSnapshot();
            if (ini.HasSection("NpcWorldDispatch"))
            {
                snapshot.DispatchPolicy = ParseNpcWorldDispatchPolicy(
                    ini.GetString("NpcWorldDispatch", "DispatchPolicy", NpcWorldDispatchPolicy.Balanced.ToString()),
                    NpcWorldDispatchPolicy.Balanced);
                snapshot.PriorityCommodity = CommodityCatalog.Normalize(ini.GetString("NpcWorldDispatch", "PriorityCommodity", string.Empty));
                snapshot.PriorityDistrict = ini.GetString("NpcWorldDispatch", "PriorityDistrict", string.Empty);
                snapshot.PremiumDispatchEnabled = ini.GetBool("NpcWorldDispatch", "PremiumDispatchEnabled", false);
                snapshot.OfficeDeliveryNotificationsEnabled = ini.GetBool("NpcWorldDispatch", "OfficeDeliveryNotificationsEnabled", true);
                snapshot.LastWorldEvaluationClockMinute = ParseInt(ini.GetString("NpcWorldDispatch", "LastWorldEvaluationClockMinute", "-1"), -1);
                snapshot.CompletedWorldDispatches = ParseInt(ini.GetString("NpcWorldDispatch", "CompletedWorldDispatches", "0"), 0);
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                if (section.StartsWith("NpcContract:", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.Contracts.Add(new NpcLogisticsContractSnapshot
                    {
                        Id = ParseInt(section.Substring("NpcContract:".Length).Trim(), 0),
                        OriginIndustryId = ini.GetString(section, "OriginIndustryId", string.Empty),
                        DestinationIndustryId = ini.GetString(section, "DestinationIndustryId", string.Empty),
                        Commodity = CommodityCatalog.Normalize(ini.GetString(section, "Commodity", string.Empty)),
                        TierId = ini.GetString(section, "TierId", string.Empty),
                        AssignedVehicleAssetId = ini.GetString(section, "AssignedVehicleAssetId", string.Empty),
                        AssignedVehicleDisplayName = ini.GetString(section, "AssignedVehicleDisplayName", string.Empty),
                        OriginTriggerThresholdPercent = ParseInt(ini.GetString(section, "OriginTriggerThresholdPercent", "0"), 0),
                        DestinationTriggerThresholdPercent = ParseInt(ini.GetString(section, "DestinationTriggerThresholdPercent", "100"), 100),
                        CurrentRouteIndex = ParseInt(ini.GetString(section, "CurrentRouteIndex", "0"), 0),
                        ContractCost = ini.GetFloat(section, "ContractCost", 0f),
                        PayrollElapsedInGameMinutes = ParseInt(ini.GetString(section, "PayrollElapsedInGameMinutes", "0"), 0),
                        CompletedPayrollCycles = ParseInt(ini.GetString(section, "CompletedPayrollCycles", "0"), 0),
                        TotalWeeklyWagesPaid = ini.GetFloat(section, "TotalWeeklyWagesPaid", 0f),
                        CompletedDeliveries = ParseInt(ini.GetString(section, "CompletedDeliveries", "0"), 0),
                        TotalDeliveredTons = ini.GetFloat(section, "TotalDeliveredTons", 0f),
                        TotalProfitEarned = ini.GetFloat(section, "TotalProfitEarned", 0f),
                        LastJourneyLossRatio = ini.GetFloat(section, "LastJourneyLossRatio", 0f),
                    });

                    var contractSnapshot = snapshot.Contracts[snapshot.Contracts.Count - 1];
                    var routeCount = ParseInt(ini.GetString(section, "RouteCount", "0"), 0);
                    for (int routeIndex = 0; routeIndex < routeCount; routeIndex++)
                    {
                        contractSnapshot.Routes.Add(new NpcLogisticsRouteSnapshot
                        {
                            OriginIndustryId = ini.GetString(section, string.Format("Route{0}OriginIndustryId", routeIndex), string.Empty),
                            DestinationIndustryId = ini.GetString(section, string.Format("Route{0}DestinationIndustryId", routeIndex), string.Empty),
                            Commodity = CommodityCatalog.Normalize(ini.GetString(section, string.Format("Route{0}Commodity", routeIndex), string.Empty)),
                            AssignedVehicleAssetId = ini.GetString(section, string.Format("Route{0}AssignedVehicleAssetId", routeIndex), string.Empty),
                            AssignedVehicleDisplayName = ini.GetString(section, string.Format("Route{0}AssignedVehicleDisplayName", routeIndex), string.Empty),
                            OriginTriggerThresholdPercent = ParseInt(ini.GetString(section, string.Format("Route{0}OriginTriggerThresholdPercent", routeIndex), "0"), 0),
                            DestinationTriggerThresholdPercent = ParseInt(ini.GetString(section, string.Format("Route{0}DestinationTriggerThresholdPercent", routeIndex), "100"), 100),
                        });
                    }

                    continue;
                }

                if (!section.StartsWith("NpcWorldJob:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                snapshot.WorldJobs.Add(new NpcWorldLogisticsJobSnapshot
                {
                    Id = ParseInt(section.Substring("NpcWorldJob:".Length).Trim(), 0),
                    Type = ParseNpcWorldJobType(ini.GetString(section, "Type", NpcWorldJobType.OverflowRescue.ToString()), NpcWorldJobType.OverflowRescue),
                    Phase = ParseNpcWorldJobPhase(ini.GetString(section, "Phase", NpcWorldJobPhase.Listed.ToString()), NpcWorldJobPhase.Listed),
                    Commodity = CommodityCatalog.Normalize(ini.GetString(section, "Commodity", string.Empty)),
                    SourceLabel = ini.GetString(section, "SourceLabel", string.Empty),
                    DestinationLabel = ini.GetString(section, "DestinationLabel", string.Empty),
                    OriginIndustryId = ini.GetString(section, "OriginIndustryId", string.Empty),
                    DestinationIndustryId = ini.GetString(section, "DestinationIndustryId", string.Empty),
                    Tons = ini.GetFloat(section, "Tons", 0f),
                    RemainingInGameMinutes = ParseInt(ini.GetString(section, "RemainingInGameMinutes", "0"), 0),
                    TotalInGameMinutes = ParseInt(ini.GetString(section, "TotalInGameMinutes", "0"), 0),
                    CreatedClockMinute = ParseInt(ini.GetString(section, "CreatedClockMinute", "0"), 0),
                    IsSpotOpportunity = ini.GetBool(section, "IsSpotOpportunity", false),
                    UsesPremiumDispatch = ini.GetBool(section, "UsesPremiumDispatch", false),
                    IsPriorityMatch = ini.GetBool(section, "IsPriorityMatch", false),
                    HasVisibleConvoy = ini.GetBool(section, "HasVisibleConvoy", false),
                    IsRivalJob = ini.GetBool(section, "IsRivalJob", false),
                    BackhaulDepth = ParseInt(ini.GetString(section, "BackhaulDepth", "0"), 0),
                    StatusText = ini.GetString(section, "StatusText", string.Empty),
                });
            }

            return snapshot.HasData ? snapshot : null;
        }

        private static void WriteSpecialMissionSnapshot(StreamWriter writer, SpecialMissionPersistenceSnapshot snapshot)
        {
            if (writer == null || snapshot == null || !snapshot.HasData)
            {
                return;
            }

            if (snapshot.ActiveMission != null && !string.IsNullOrWhiteSpace(snapshot.ActiveMission.MissionId))
            {
                writer.WriteLine("[SpecialMissions]");
                writer.WriteLine("ActiveMissionId={0}", snapshot.ActiveMission.MissionId ?? string.Empty);
                writer.WriteLine("ActiveStageIndex={0}", snapshot.ActiveMission.StageIndex);
                writer.WriteLine("HandlerContainerPickedUp={0}", snapshot.ActiveMission.HandlerContainerPickedUp ? "true" : "false");
                writer.WriteLine("HandlerContainerLoaded={0}", snapshot.ActiveMission.HandlerContainerLoaded ? "true" : "false");
                writer.WriteLine("DynamicDeliveredTons={0}", FormatFloat(snapshot.ActiveMission.DynamicDeliveredTons));
                writer.WriteLine();
            }

            if (snapshot.CompletedMissions == null)
            {
                return;
            }

            foreach (var entry in snapshot.CompletedMissions.OrderBy(x => x != null ? x.MissionId : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.MissionId) || entry.CompletionCount <= 0)
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildSpecialMissionProgressSectionName(entry.MissionId));
                writer.WriteLine("CompletionCount={0}", entry.CompletionCount);
                writer.WriteLine("LastCompletedInGameMinute={0}", entry.LastCompletedInGameMinute);
                writer.WriteLine();
            }

            if (snapshot.AvailableMissionAnnouncements == null)
            {
                return;
            }

            foreach (var entry in snapshot.AvailableMissionAnnouncements.OrderBy(x => x != null ? x.MissionId : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.MissionId))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildSpecialMissionAvailabilitySectionName(entry.MissionId));
                writer.WriteLine("AnnouncedAvailable=true");
                writer.WriteLine();
            }
        }

        private static SpecialMissionPersistenceSnapshot ReadSpecialMissionSnapshot(IniFile ini)
        {
            if (ini == null)
            {
                return null;
            }

            var snapshot = new SpecialMissionPersistenceSnapshot();
            if (ini.HasSection("SpecialMissions"))
            {
                var missionId = ini.GetString("SpecialMissions", "ActiveMissionId", string.Empty);
                if (!string.IsNullOrWhiteSpace(missionId))
                {
                    snapshot.ActiveMission = new ActiveSpecialMissionPersistenceSnapshot
                    {
                        MissionId = missionId,
                        StageIndex = ParseInt(ini.GetString("SpecialMissions", "ActiveStageIndex", "0"), 0),
                        HandlerContainerPickedUp = ini.GetBool("SpecialMissions", "HandlerContainerPickedUp", false),
                        HandlerContainerLoaded = ini.GetBool("SpecialMissions", "HandlerContainerLoaded", false),
                        DynamicDeliveredTons = ParseFloat(ini.GetString("SpecialMissions", "DynamicDeliveredTons", "0"), 0f),
                    };
                }
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("SpecialMissionProgress:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var missionId = section.Substring("SpecialMissionProgress:".Length).Trim();
                if (string.IsNullOrWhiteSpace(missionId))
                {
                    continue;
                }

                var completionCount = ParseInt(ini.GetString(section, "CompletionCount", "0"), 0);
                if (completionCount <= 0)
                {
                    continue;
                }

                snapshot.CompletedMissions.Add(new SpecialMissionCompletionSnapshot
                {
                    MissionId = missionId,
                    CompletionCount = completionCount,
                    LastCompletedInGameMinute = ParseInt(ini.GetString(section, "LastCompletedInGameMinute", "0"), 0),
                });
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section) || !section.StartsWith("SpecialMissionAvailability:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ini.GetBool(section, "AnnouncedAvailable", false))
                {
                    continue;
                }

                var missionId = section.Substring("SpecialMissionAvailability:".Length).Trim();
                if (string.IsNullOrWhiteSpace(missionId))
                {
                    continue;
                }

                snapshot.AvailableMissionAnnouncements.Add(new SpecialMissionAvailabilitySnapshot
                {
                    MissionId = missionId,
                });
            }

            return snapshot.HasData ? snapshot : null;
        }

        private static TabletGraphTimeframe ParseGraphTimeframe(string raw, TabletGraphTimeframe fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            TabletGraphTimeframe parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static string FormatFloatList(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                parts[i] = FormatFloat(values[i]);
            }

            return string.Join(",", parts);
        }

        private static List<float> ParseFloatList(string raw)
        {
            var values = new List<float>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return values;
            }

            var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                values.Add(ParseFloat(parts[i], 0f));
            }

            return values;
        }

        private static EconomyDifficultyPreset ParseEconomyDifficultyPreset(string raw, EconomyDifficultyPreset fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            EconomyDifficultyPreset parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static NpcWeeklyWageDifficulty ParseNpcWeeklyWageDifficulty(string raw, NpcWeeklyWageDifficulty fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            NpcWeeklyWageDifficulty parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static NpcWorldDispatchPolicy ParseNpcWorldDispatchPolicy(string raw, NpcWorldDispatchPolicy fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            NpcWorldDispatchPolicy parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static NpcWorldJobType ParseNpcWorldJobType(string raw, NpcWorldJobType fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            NpcWorldJobType parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static NpcWorldJobPhase ParseNpcWorldJobPhase(string raw, NpcWorldJobPhase fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            NpcWorldJobPhase parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static ModLanguage? ParseModLanguage(string raw, ModLanguage fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            ModLanguage parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static ColorblindMode? ParseColorblindMode(string raw, ColorblindMode fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            ColorblindMode parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static string BuildIndustrySectionName(string industryId)
        {
            return "Industry:" + (industryId ?? string.Empty).Trim();
        }

        private static void WriteTerritorySnapshot(StreamWriter writer, TerritoryPersistenceSnapshot territorySnapshot)
        {
            if (writer == null || territorySnapshot == null)
            {
                return;
            }

            if (territorySnapshot.LastOperationsChargeWeekIndex >= 0 || territorySnapshot.LastMaintenanceWeekIndex >= 0)
            {
                writer.WriteLine("[TerritoryMeta]");
                writer.WriteLine("LastOperationsChargeWeekIndex={0}", territorySnapshot.LastOperationsChargeWeekIndex);
                writer.WriteLine("LastMaintenanceWeekIndex={0}", territorySnapshot.LastMaintenanceWeekIndex);
                writer.WriteLine();
            }

            foreach (var site in territorySnapshot.Sites.OrderBy(x => x.SiteId, StringComparer.OrdinalIgnoreCase))
            {
                if (site == null || string.IsNullOrWhiteSpace(site.SiteId))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildTerritorySiteSectionName(site.SiteId));
                writer.WriteLine("ControlLevel={0}", site.ControlLevel);
                writer.WriteLine("CrewAssigned={0}", site.CrewAssigned ? "true" : "false");
                writer.WriteLine("LoadRuns={0}", site.LoadRuns);
                writer.WriteLine("UnloadRuns={0}", site.UnloadRuns);
                writer.WriteLine("TotalDeliveries={0}", site.TotalDeliveries);
                writer.WriteLine("TotalDeliveredTons={0}", FormatFloat(site.TotalDeliveredTons));
                writer.WriteLine("TotalLoadedTons={0}", FormatFloat(site.TotalLoadedTons));
                writer.WriteLine("FranchiseLevel={0}", site.FranchiseLevel);
                writer.WriteLine("LoaderCount={0}", site.LoaderCount);
                writer.WriteLine("MechanicCount={0}", site.MechanicCount);
                writer.WriteLine("GuardCount={0}", site.GuardCount);
                writer.WriteLine("ManagerCount={0}", site.ManagerCount);
                writer.WriteLine("Repossessions={0}", site.Repossessions);
                writer.WriteLine("NpcLoads={0}", site.NpcLoads);
                writer.WriteLine("NpcDeliveries={0}", site.NpcDeliveries);
                writer.WriteLine("LastCommodity={0}", site.LastCommodity ?? string.Empty);
                writer.WriteLine("DepotSpecialization={0}", site.DepotSpecialization);
                writer.WriteLine("CurrentWeekServiceDeliveries={0}", site.CurrentWeekServiceDeliveries);
                writer.WriteLine("CurrentWeekServiceTons={0}", FormatFloat(site.CurrentWeekServiceTons));
                writer.WriteLine("ServicePenaltySteps={0}", site.ServicePenaltySteps);
                writer.WriteLine("ServiceSuccessStreak={0}", site.ServiceSuccessStreak);
                writer.WriteLine("ServiceTargetMetLastWeek={0}", site.ServiceTargetMetLastWeek ? "true" : "false");
                writer.WriteLine("SiteOperatorAssigned={0}", site.SiteOperatorAssigned ? "true" : "false");
                writer.WriteLine("LastPassiveIncomeAmount={0}", FormatFloat(site.LastPassiveIncomeAmount));
                writer.WriteLine("LastPassiveIncomeWeekIndex={0}", site.LastPassiveIncomeWeekIndex);
                writer.WriteLine("LastPassiveIncomeStatus={0}", site.LastPassiveIncomeStatus ?? string.Empty);
                writer.WriteLine();
            }

            foreach (var district in territorySnapshot.Districts.OrderBy(x => x.DistrictName, StringComparer.OrdinalIgnoreCase))
            {
                if (district == null || string.IsNullOrWhiteSpace(district.DistrictName))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildTerritoryDistrictSectionName(district.DistrictName));
                writer.WriteLine("DistrictName={0}", district.DistrictName);
                writer.WriteLine("LicenseStatus={0}", district.LicenseStatus);
                writer.WriteLine("LicenseStrikeCount={0}", district.LicenseStrikeCount);
                writer.WriteLine("CurrentWeekActivityCount={0}", district.CurrentWeekActivityCount);
                writer.WriteLine("CurrentWeekActivityTons={0}", FormatFloat(district.CurrentWeekActivityTons));
                writer.WriteLine("CompetitivePressure={0}", FormatFloat(district.CompetitivePressure));
                writer.WriteLine("CompetitiveOpportunity={0}", FormatFloat(district.CompetitiveOpportunity));
                writer.WriteLine("ActiveCompetitionJobs={0}", district.ActiveCompetitionJobs);
                writer.WriteLine("VisibleCompetitionCount={0}", district.VisibleCompetitionCount);
                writer.WriteLine("CompetitiveTons={0}", FormatFloat(district.CompetitiveTons));
                writer.WriteLine("CompetitiveResponseCount={0}", district.CompetitiveResponseCount);
                writer.WriteLine("CompetitiveWinCount={0}", district.CompetitiveWinCount);
                writer.WriteLine();
            }

            foreach (var corridor in territorySnapshot.Corridors.OrderBy(x => BuildTerritoryCorridorSectionName(x.DistrictA, x.DistrictB), StringComparer.OrdinalIgnoreCase))
            {
                if (corridor == null || string.IsNullOrWhiteSpace(corridor.DistrictA) || string.IsNullOrWhiteSpace(corridor.DistrictB))
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildTerritoryCorridorSectionName(corridor.DistrictA, corridor.DistrictB));
                writer.WriteLine("DistrictA={0}", corridor.DistrictA);
                writer.WriteLine("DistrictB={0}", corridor.DistrictB);
                writer.WriteLine("DeliveryCount={0}", corridor.DeliveryCount);
                writer.WriteLine("TotalDeliveredTons={0}", FormatFloat(corridor.TotalDeliveredTons));
                writer.WriteLine("RightLevel={0}", corridor.RightLevel);
                writer.WriteLine("CurrentWeekDeliveryCount={0}", corridor.CurrentWeekDeliveryCount);
                writer.WriteLine("CurrentWeekDeliveredTons={0}", FormatFloat(corridor.CurrentWeekDeliveredTons));
                writer.WriteLine("DecayPressure={0}", FormatFloat(corridor.DecayPressure));
                writer.WriteLine();
            }

            foreach (var offset in territorySnapshot.DistrictReputationOffsets.OrderBy(x => x.DistrictName, StringComparer.OrdinalIgnoreCase))
            {
                if (offset == null || string.IsNullOrWhiteSpace(offset.DistrictName) || Math.Abs(offset.Offset) <= 0.001f)
                {
                    continue;
                }

                writer.WriteLine("[{0}]", BuildTerritoryDistrictReputationSectionName(offset.DistrictName));
                writer.WriteLine("DistrictName={0}", offset.DistrictName);
                writer.WriteLine("Offset={0}", FormatFloat(offset.Offset));
                writer.WriteLine();
            }
        }

        private static TerritoryPersistenceSnapshot ReadTerritorySnapshot(IniFile ini)
        {
            var snapshot = new TerritoryPersistenceSnapshot();
            if (ini == null)
            {
                return snapshot;
            }

            foreach (var section in ini.Sections)
            {
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                if (section.StartsWith("TerritorySite:", StringComparison.OrdinalIgnoreCase))
                {
                    var siteId = section.Substring("TerritorySite:".Length).Trim();
                    if (string.IsNullOrWhiteSpace(siteId))
                    {
                        continue;
                    }

                    snapshot.Sites.Add(new TerritorySiteSnapshot
                    {
                        SiteId = siteId,
                        ControlLevel = ParseTerritoryControlLevel(ini.GetString(section, "ControlLevel", TerritoryControlLevel.None.ToString())),
                        CrewAssigned = ini.GetBool(section, "CrewAssigned", false),
                        LoadRuns = ParseInt(ini.GetString(section, "LoadRuns", "0"), 0),
                        UnloadRuns = ParseInt(ini.GetString(section, "UnloadRuns", "0"), 0),
                        TotalDeliveries = ParseInt(ini.GetString(section, "TotalDeliveries", "0"), 0),
                        TotalDeliveredTons = ini.GetFloat(section, "TotalDeliveredTons", 0f),
                        TotalLoadedTons = ini.GetFloat(section, "TotalLoadedTons", 0f),
                        FranchiseLevel = ParseTerritoryFranchiseLevel(ini.GetString(section, "FranchiseLevel", TerritoryFranchiseLevel.None.ToString())),
                        LoaderCount = ParseInt(ini.GetString(section, "LoaderCount", "0"), 0),
                        MechanicCount = ParseInt(ini.GetString(section, "MechanicCount", "0"), 0),
                        GuardCount = ParseInt(ini.GetString(section, "GuardCount", "0"), 0),
                        ManagerCount = ParseInt(ini.GetString(section, "ManagerCount", "0"), 0),
                        Repossessions = ParseInt(ini.GetString(section, "Repossessions", "0"), 0),
                        NpcLoads = ParseInt(ini.GetString(section, "NpcLoads", "0"), 0),
                        NpcDeliveries = ParseInt(ini.GetString(section, "NpcDeliveries", "0"), 0),
                        LastCommodity = ini.GetString(section, "LastCommodity", string.Empty),
                        DepotSpecialization = ParseDepotSpecialization(ini.GetString(section, "DepotSpecialization", DepotSpecialization.None.ToString())),
                        CurrentWeekServiceDeliveries = ParseInt(ini.GetString(section, "CurrentWeekServiceDeliveries", "0"), 0),
                        CurrentWeekServiceTons = ini.GetFloat(section, "CurrentWeekServiceTons", 0f),
                        ServicePenaltySteps = ParseInt(ini.GetString(section, "ServicePenaltySteps", "0"), 0),
                        ServiceSuccessStreak = ParseInt(ini.GetString(section, "ServiceSuccessStreak", "0"), 0),
                        ServiceTargetMetLastWeek = ini.GetBool(section, "ServiceTargetMetLastWeek", false),
                        SiteOperatorAssigned = ini.GetBool(section, "SiteOperatorAssigned", false),
                        LastPassiveIncomeAmount = ini.GetFloat(section, "LastPassiveIncomeAmount", 0f),
                        LastPassiveIncomeWeekIndex = ParseInt(ini.GetString(section, "LastPassiveIncomeWeekIndex", "-1"), -1),
                        LastPassiveIncomeStatus = ini.GetString(section, "LastPassiveIncomeStatus", string.Empty),
                    });

                    continue;
                }

                if (section.StartsWith("TerritoryDistrict:", StringComparison.OrdinalIgnoreCase))
                {
                    var districtName = ini.GetString(section, "DistrictName", section.Substring("TerritoryDistrict:".Length).Trim());
                    if (string.IsNullOrWhiteSpace(districtName))
                    {
                        continue;
                    }

                    snapshot.Districts.Add(new TerritoryDistrictSnapshot
                    {
                        DistrictName = districtName,
                        LicenseStatus = ParseDistrictLicenseStatus(ini.GetString(section, "LicenseStatus", DistrictLicenseStatus.None.ToString())),
                        LicenseStrikeCount = ParseInt(ini.GetString(section, "LicenseStrikeCount", "0"), 0),
                        CurrentWeekActivityCount = ParseInt(ini.GetString(section, "CurrentWeekActivityCount", "0"), 0),
                        CurrentWeekActivityTons = ini.GetFloat(section, "CurrentWeekActivityTons", 0f),
                        CompetitivePressure = ini.GetFloat(section, "CompetitivePressure", 0f),
                        CompetitiveOpportunity = ini.GetFloat(section, "CompetitiveOpportunity", 0f),
                        ActiveCompetitionJobs = ParseInt(ini.GetString(section, "ActiveCompetitionJobs", "0"), 0),
                        VisibleCompetitionCount = ParseInt(ini.GetString(section, "VisibleCompetitionCount", "0"), 0),
                        CompetitiveTons = ini.GetFloat(section, "CompetitiveTons", 0f),
                        CompetitiveResponseCount = ParseInt(ini.GetString(section, "CompetitiveResponseCount", "0"), 0),
                        CompetitiveWinCount = ParseInt(ini.GetString(section, "CompetitiveWinCount", "0"), 0),
                    });

                    continue;
                }

                if (string.Equals(section, "TerritoryMeta", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.LastOperationsChargeWeekIndex = ParseInt(ini.GetString(section, "LastOperationsChargeWeekIndex", "-1"), -1);
                    snapshot.LastMaintenanceWeekIndex = ParseInt(ini.GetString(section, "LastMaintenanceWeekIndex", "-1"), -1);
                    continue;
                }

                if (section.StartsWith("TerritoryCorridor:", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.Corridors.Add(new TerritoryCorridorSnapshot
                    {
                        DistrictA = ini.GetString(section, "DistrictA", string.Empty),
                        DistrictB = ini.GetString(section, "DistrictB", string.Empty),
                        DeliveryCount = ParseInt(ini.GetString(section, "DeliveryCount", "0"), 0),
                        TotalDeliveredTons = ini.GetFloat(section, "TotalDeliveredTons", 0f),
                        RightLevel = ParseCorridorRightLevel(ini.GetString(section, "RightLevel", CorridorRightLevel.None.ToString())),
                        CurrentWeekDeliveryCount = ParseInt(ini.GetString(section, "CurrentWeekDeliveryCount", "0"), 0),
                        CurrentWeekDeliveredTons = ini.GetFloat(section, "CurrentWeekDeliveredTons", 0f),
                        DecayPressure = ini.GetFloat(section, "DecayPressure", 0f),
                    });

                    continue;
                }

                if (section.StartsWith("TerritoryDistrictReputation:", StringComparison.OrdinalIgnoreCase))
                {
                    var districtName = ini.GetString(section, "DistrictName", section.Substring("TerritoryDistrictReputation:".Length).Trim());
                    if (string.IsNullOrWhiteSpace(districtName))
                    {
                        continue;
                    }

                    snapshot.DistrictReputationOffsets.Add(new TerritoryDistrictReputationOffsetSnapshot
                    {
                        DistrictName = districtName,
                        Offset = ini.GetFloat(section, "Offset", 0f),
                    });
                }
            }

            return snapshot;
        }

        private static bool HasTerritoryData(TerritoryPersistenceSnapshot snapshot)
        {
            return snapshot != null
                && (snapshot.LastOperationsChargeWeekIndex >= 0
                    || snapshot.LastMaintenanceWeekIndex >= 0
                    || (snapshot.Sites != null && snapshot.Sites.Count > 0)
                    || (snapshot.Districts != null && snapshot.Districts.Count > 0)
                    || (snapshot.Corridors != null && snapshot.Corridors.Count > 0)
                    || HasTerritoryDistrictReputationOffsetData(snapshot));
        }

        private static bool HasTerritoryDistrictReputationOffsetData(TerritoryPersistenceSnapshot snapshot)
        {
            return snapshot != null
                && snapshot.DistrictReputationOffsets != null
                && snapshot.DistrictReputationOffsets.Any(offset => offset != null && !string.IsNullOrWhiteSpace(offset.DistrictName) && Math.Abs(offset.Offset) > 0.001f);
        }

        private static bool HasTerritoryCompetitionData(TerritoryPersistenceSnapshot snapshot)
        {
            return snapshot != null
                && snapshot.Districts != null
                && snapshot.Districts.Any(district => district != null
                    && (district.CompetitivePressure > 0.001f
                        || district.CompetitiveOpportunity > 0.001f
                        || district.ActiveCompetitionJobs > 0
                        || district.VisibleCompetitionCount > 0
                        || district.CompetitiveTons > 0.001f
                        || district.CompetitiveResponseCount > 0
                        || district.CompetitiveWinCount > 0));
        }

        private static string BuildTerritorySiteSectionName(string siteId)
        {
            return "TerritorySite:" + (siteId ?? string.Empty).Trim();
        }

        private static string BuildTerritoryDistrictSectionName(string districtName)
        {
            return "TerritoryDistrict:" + (districtName ?? string.Empty).Trim();
        }

        private static string BuildTerritoryDistrictReputationSectionName(string districtName)
        {
            return "TerritoryDistrictReputation:" + (districtName ?? string.Empty).Trim();
        }

        private static string BuildOwnedFleetSectionName(int index)
        {
            return "OwnedFleet:" + Math.Max(1, index).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildNpcContractSectionName(int contractId)
        {
            return "NpcContract:" + Math.Max(1, contractId).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildNpcWorldJobSectionName(int jobId)
        {
            return "NpcWorldJob:" + Math.Max(1, jobId).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildPropertyOfficeSectionName(string officeId)
        {
            return "PropertyOffice:" + (officeId ?? string.Empty).Trim();
        }

        private static string BuildPropertyOfficeObjectSectionName(string instanceId)
        {
            return "PropertyOfficeObject:" + (instanceId ?? string.Empty).Trim();
        }

        private static string BuildPropertyApartmentSectionName(string apartmentId)
        {
            return "PropertyApartment:" + (apartmentId ?? string.Empty).Trim();
        }

        private static string BuildPropertyCommercialVehicleSectionName(string assetId)
        {
            return "PropertyCommercialVehicle:" + (assetId ?? string.Empty).Trim();
        }

        private static string BuildPlayerContractSectionName(string contractId)
        {
            return "PlayerContract:" + (contractId ?? string.Empty).Trim();
        }

        private static string BuildPlayerContractCooldownSectionName(int index)
        {
            return "PlayerContractCooldown:" + Math.Max(1, index).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildPropertyPersonalVehicleSectionName(string assetId)
        {
            return "PropertyPersonalVehicle:" + (assetId ?? string.Empty).Trim();
        }

        private static string BuildBankOfferSectionName(string bankId)
        {
            return "BankOffer:" + (bankId ?? string.Empty).Trim();
        }

        private static string BuildSpecialMissionProgressSectionName(string missionId)
        {
            return "SpecialMissionProgress:" + (missionId ?? string.Empty).Trim();
        }

        private static string BuildSpecialMissionAvailabilitySectionName(string missionId)
        {
            return "SpecialMissionAvailability:" + (missionId ?? string.Empty).Trim();
        }

        private static string BuildTerritoryCorridorSectionName(string districtA, string districtB)
        {
            var left = districtA ?? string.Empty;
            var right = districtB ?? string.Empty;
            if (StringComparer.OrdinalIgnoreCase.Compare(left, right) > 0)
            {
                var swap = left;
                left = right;
                right = swap;
            }

            return "TerritoryCorridor:" + left + "|" + right;
        }

        private static TerritoryControlLevel ParseTerritoryControlLevel(string raw)
        {
            TerritoryControlLevel parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : TerritoryControlLevel.None;
        }

        private static TerritoryFranchiseLevel ParseTerritoryFranchiseLevel(string raw)
        {
            TerritoryFranchiseLevel parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : TerritoryFranchiseLevel.None;
        }

        private static CorridorRightLevel ParseCorridorRightLevel(string raw)
        {
            CorridorRightLevel parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : CorridorRightLevel.None;
        }

        private static DistrictLicenseStatus ParseDistrictLicenseStatus(string raw)
        {
            DistrictLicenseStatus parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : DistrictLicenseStatus.None;
        }

        private static DepotSpecialization ParseDepotSpecialization(string raw)
        {
            DepotSpecialization parsed;
            return Enum.TryParse(raw ?? string.Empty, true, out parsed) ? parsed : DepotSpecialization.None;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string FormatColor(Color value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2}",
                value.R,
                value.G,
                value.B);
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2}",
                FormatFloat(value.X),
                FormatFloat(value.Y),
                FormatFloat(value.Z));
        }

        private static float ParseFloat(string raw, float fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            float parsed;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static Color? ParseColor(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var parts = raw.Split(',');
            if (parts.Length != 3)
            {
                return null;
            }

            var red = Math.Max(0, Math.Min(255, ParseInt(parts[0], 0)));
            var green = Math.Max(0, Math.Min(255, ParseInt(parts[1], 0)));
            var blue = Math.Max(0, Math.Min(255, ParseInt(parts[2], 0)));
            return Color.FromArgb(red, green, blue);
        }

        private static int ParseInt(string raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            int parsed;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static bool ParseBoolValue(string raw, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            bool parsed;
            if (bool.TryParse(raw.Trim(), out parsed))
            {
                return parsed;
            }

            if (raw.Trim().Equals("1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (raw.Trim().Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fallback;
        }

        private static T? ParseOptionalEnum<T>(string raw)
            where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            T parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed)
                ? parsed
                : (T?)null;
        }

        private static Vector3 ParseVector3(string raw, Vector3 fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var parts = raw.Split(',');
            if (parts.Length != 3)
            {
                return fallback;
            }

            return new Vector3(
                ParseFloat(parts[0], fallback.X),
                ParseFloat(parts[1], fallback.Y),
                ParseFloat(parts[2], fallback.Z));
        }

        private static VehicleCargoType ParseVehicleCargoType(string raw, VehicleCargoType fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            VehicleCargoType parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static bool HasOwnedFleetData(OwnedFleetPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static bool HasNpcLogisticsData(NpcLogisticsPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static bool HasNpcWorldDispatchData(NpcLogisticsPersistenceSnapshot snapshot)
        {
            return snapshot != null
                && ((snapshot.WorldJobs != null && snapshot.WorldJobs.Count > 0)
                    || snapshot.DispatchPolicy != NpcWorldDispatchPolicy.Balanced
                    || !string.IsNullOrWhiteSpace(snapshot.PriorityCommodity)
                    || !string.IsNullOrWhiteSpace(snapshot.PriorityDistrict)
                    || snapshot.PremiumDispatchEnabled
                    || !snapshot.OfficeDeliveryNotificationsEnabled
                    || snapshot.CompletedWorldDispatches > 0
                    || snapshot.LastWorldEvaluationClockMinute >= 0);
        }

        private static bool HasSpecialMissionData(SpecialMissionPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }
        private static bool HasPropertyOwnershipData(PropertyOwnershipPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static bool HasFinanceData(CompanyFinancePersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static bool HasBankLoanData(BankLoanPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static bool HasPlayerStatisticsData(PlayerStatisticsPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static bool HasPhaseFourPlayerStatisticsData(PlayerStatisticsPersistenceSnapshot snapshot)
        {
            return snapshot != null
                && (snapshot.HighestPrestigeScore > 0.001f
                    || !string.IsNullOrWhiteSpace(snapshot.HighestDoctrineId)
                    || snapshot.HighestDoctrineTier > 0);
        }

        private static bool HasPlayerContractsData(PlayerContractsPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static bool HasGlobalMarketData(GlobalMarketPersistenceSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasData;
        }

        private static CompanyFinanceFlow ParseCompanyFinanceFlow(string raw, CompanyFinanceFlow fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            CompanyFinanceFlow parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static CompanyFinanceCategory ParseCompanyFinanceCategory(string raw, CompanyFinanceCategory fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            CompanyFinanceCategory parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static PlayerContractType ParsePlayerContractType(string raw, PlayerContractType fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            PlayerContractType parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static PlayerContractStatus ParsePlayerContractStatus(string raw, PlayerContractStatus fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            PlayerContractStatus parsed;
            return Enum.TryParse(raw.Trim(), true, out parsed) ? parsed : fallback;
        }

        private static IniFile LoadPersistenceIni(string filePath)
        {
            var resolvedPath = ResolveReadablePersistencePath(filePath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return null;
            }

            if (!resolvedPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return IniFile.Load(resolvedPath);
            }

            var document = XDocument.Load(resolvedPath);
            if (document.Root == null || !document.Root.Elements(PersistenceSectionElementName).Any())
            {
                var legacyPath = ResolveLegacyPersistencePath(resolvedPath);
                if (!string.IsNullOrWhiteSpace(legacyPath) && File.Exists(legacyPath))
                {
                    return IniFile.Load(legacyPath);
                }

                throw new InvalidDataException("Save file contains no persisted sections.");
            }

            return IniFile.LoadFromString(BuildLegacyIniContentFromXml(document));
        }

        private static string ResolveReadablePersistencePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            if (File.Exists(filePath))
            {
                return filePath;
            }

            var legacyPath = ResolveLegacyPersistencePath(filePath);
            if (!string.IsNullOrWhiteSpace(legacyPath) && File.Exists(legacyPath))
            {
                return legacyPath;
            }

            return string.Empty;
        }

        private static string ResolveLegacyPersistencePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            return Path.ChangeExtension(filePath, ".ini");
        }

        private static XDocument BuildPersistenceXmlDocument(
            IEnumerable<Industry> industries,
            IndustryPersistenceMetadata metadata,
            TerritoryPersistenceSnapshot territorySnapshot)
        {
            var iniContent = BuildLegacyIniPersistenceContent(industries, metadata, territorySnapshot);
            var sections = ParseLegacyIniSections(iniContent);

            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(
                    PersistenceRootElementName,
                    new XAttribute("format", "section-key-v1"),
                    sections
                        .Where(section => section != null && section.Entries.Count > 0)
                        .Select(section =>
                            new XElement(
                                PersistenceSectionElementName,
                                new XAttribute("name", section.Name ?? string.Empty),
                                section.Entries.Select(entry =>
                                    new XElement(
                                        PersistenceValueElementName,
                                        new XAttribute("key", entry.Key ?? string.Empty),
                                        entry.Value ?? string.Empty))))));
        }

        private static string BuildLegacyIniPersistenceContent(
            IEnumerable<Industry> industries,
            IndustryPersistenceMetadata metadata,
            TerritoryPersistenceSnapshot territorySnapshot)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                {
                    WriteLegacyIniPersistence(writer, industries, metadata, territorySnapshot);
                    writer.Flush();
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static List<PersistenceSectionDocument> ParseLegacyIniSections(string content)
        {
            var sections = new List<PersistenceSectionDocument>();
            if (string.IsNullOrWhiteSpace(content))
            {
                return sections;
            }

            PersistenceSectionDocument currentSection = null;
            var normalized = content
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            foreach (var rawLine in normalized.Split('\n'))
            {
                if (rawLine == null)
                {
                    continue;
                }

                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = new PersistenceSectionDocument(line.Substring(1, line.Length - 2).Trim());
                    sections.Add(currentSection);
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                if (currentSection == null)
                {
                    currentSection = new PersistenceSectionDocument("Global");
                    sections.Add(currentSection);
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = separatorIndex + 1 < line.Length
                    ? line.Substring(separatorIndex + 1).Trim()
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                currentSection.Entries.Add(new PersistenceValueDocument(key, value));
            }

            return sections;
        }

        private static string BuildLegacyIniContentFromXml(XDocument document)
        {
            if (document == null || document.Root == null || !string.Equals(document.Root.Name.LocalName, PersistenceRootElementName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Save file is not a valid LSOL state XML document.");
            }

            var builder = new StringBuilder();
            foreach (var sectionElement in document.Root.Elements(PersistenceSectionElementName))
            {
                var nameAttribute = sectionElement.Attribute("name");
                var sectionName = nameAttribute != null ? nameAttribute.Value : string.Empty;
                if (string.IsNullOrWhiteSpace(sectionName))
                {
                    continue;
                }

                builder.Append('[').Append(sectionName.Trim()).AppendLine("]");
                foreach (var valueElement in sectionElement.Elements(PersistenceValueElementName))
                {
                    var keyAttribute = valueElement.Attribute("key");
                    var key = keyAttribute != null ? keyAttribute.Value : string.Empty;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    builder.Append(key.Trim()).Append('=').Append(valueElement.Value ?? string.Empty).AppendLine();
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private sealed class PersistenceSectionDocument
        {
            public PersistenceSectionDocument(string name)
            {
                Name = name;
                Entries = new List<PersistenceValueDocument>();
            }

            public string Name { get; private set; }

            public List<PersistenceValueDocument> Entries { get; private set; }
        }

        private sealed class PersistenceValueDocument
        {
            public PersistenceValueDocument(string key, string value)
            {
                Key = key;
                Value = value;
            }

            public string Key { get; private set; }

            public string Value { get; private set; }
        }
    }

    public sealed class IndustryPersistenceLoadResult
    {
        public int RestoredCount { get; set; }
        public IndustryPersistenceMetadata Metadata { get; set; }
    }

    public sealed class IndustryPersistenceMetadata
    {
        public bool HasGameplayMetadata { get; set; }
        public float Profit { get; set; }
        public float StartingBalance { get; set; }
        public ModLanguage? Language { get; set; }
        public ColorblindMode? ColorblindMode { get; set; }
        public bool UseMetricSpeedDisplay { get; set; }
        public bool VehicleFuelDifficultyEnabled { get; set; }
        public bool CargoWeightPowerDifficultyEnabled { get; set; }
        public bool CargoDamageDifficultyEnabled { get; set; }
        public bool IndustryPricingDifficultyEnabled { get; set; }
        public bool LicensingDifficultyEnabled { get; set; }
        public bool CorridorRestrictionDifficultyEnabled { get; set; } = true;
        public bool ReputationDifficultyEnabled { get; set; } = true;
        public bool OfficeGarageLimitDifficultyEnabled { get; set; } = true;
        public bool OfficeNpcLimitDifficultyEnabled { get; set; }
        public EconomyDifficultyPreset EconomyDifficultyPreset { get; set; } = EconomyDifficultyPreset.Standard;
        public NpcWeeklyWageDifficulty NpcWeeklyWageDifficulty { get; set; } = NpcWeeklyWageDifficulty.Standard;
        public int NpcRouteLimit { get; set; } = 5;
        public bool DifficultySettingsLocked { get; set; }
        public TabletAnalyticsPersistenceSnapshot Analytics { get; set; }
        public GlobalMarketPersistenceSnapshot Market { get; set; }
        public OwnedFleetPersistenceSnapshot OwnedFleet { get; set; }
        public NpcLogisticsPersistenceSnapshot NpcLogistics { get; set; }
        public SpecialMissionPersistenceSnapshot SpecialMissions { get; set; }
        public PropertyOwnershipPersistenceSnapshot PropertyOwnership { get; set; }
        public CompanyFinancePersistenceSnapshot Finance { get; set; }
        public BankLoanPersistenceSnapshot BankLoans { get; set; }
        public PlayerStatisticsPersistenceSnapshot PlayerStatistics { get; set; }
        public PlayerContractsPersistenceSnapshot PlayerContracts { get; set; }
    }
}
