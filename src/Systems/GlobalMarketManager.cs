using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.Systems
{
    public sealed class GlobalMarketManager
    {
        private const string DefaultCommodityKey = "__default__";
        private const float DefaultPriceMultiplier = 1f;
        private const float ScarcityIncreaseStep = 0.05f;
        private const float DeliveryPressureStep = 0.10f;
        private const int ScarcityIncreaseIntervalMs = 600000;
        private const int MinutesPerDay = 24 * 60;
        private const int MinutesPerWeek = 7 * MinutesPerDay;

        private static readonly Dictionary<string, float> BasePrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "Coal", 250f },
            { "Gravel", 260f },
            { "Ore", 320f },
            { "Water", 220f },
            { "Wood", 320f },
            { "Crops", 340f },
            { "Oil", 380f },
            { "Livestock", 420f },
            { "Recyclable", 180f },
            { "Fuel", 700f },
            { "Cement", 450f },
            { "Lumber", 520f },
            { "Paper", 550f },
            { "Plastic", 800f },
            { "Alcohol", 650f },
            { "Chemicals", 750f },
            { "Fabric", 700f },
            { "ProcessedFood", 850f },
            { "Meat", 900f },
            { "LiquidFertilizer", 600f },
            { "Steel", 900f },
            { "Alloy", 1100f },
            { "Metal", 850f },
            { "Bricks", 650f },
            { "Concrete", 700f },
            { "Beam", 950f },
            { "MechanicalParts", 1100f },
            { "Electronic", 1400f },
            { "Clothes", 1200f },
            { "Furniture", 1500f },
            { "Medicine", 2400f },
            { "TV", 3000f },
            { "Computer", 3800f },
            { "Vehicles", 5000f },
            { "Omega", 7000f },
        };

        private static readonly ScheduledMarketShockTemplate[] ScheduledShockTemplates =
        {
            new ScheduledMarketShockTemplate("fuel-squeeze", "Fuel Squeeze", new[] { "Fuel", "Oil", "Chemicals" }, 0.08f, 0.26f),
            new ScheduledMarketShockTemplate("construction-boom", "Construction Boom", new[] { "Cement", "Concrete", "Steel", "Bricks", "Beam" }, 0.07f, 0.24f),
            new ScheduledMarketShockTemplate("consumer-restock", "Consumer Restock", new[] { "ProcessedFood", "Clothes", "Furniture", "Electronic" }, 0.08f, 0.22f),
            new ScheduledMarketShockTemplate("medical-shortage", "Medical Shortage", new[] { "Medicine", "Chemicals", "ProcessedFood" }, 0.12f, 0.30f),
            new ScheduledMarketShockTemplate("tech-launch", "Tech Launch", new[] { "Electronic", "TV", "Computer", "MechanicalParts" }, 0.10f, 0.24f),
            new ScheduledMarketShockTemplate("auto-tender", "Auto Tender", new[] { "Vehicles", "Metal", "Alloy", "MechanicalParts" }, 0.09f, 0.23f),
            new ScheduledMarketShockTemplate("harvest-swing", "Harvest Swing", new[] { "Crops", "LiquidFertilizer", "Meat", "ProcessedFood" }, 0.08f, 0.20f),
        };

        private readonly Dictionary<string, float> _basePrices;
        private readonly Dictionary<string, CommodityMarketState> _commodityStates;
        private readonly Dictionary<string, TemporaryCommodityDemandShock> _temporaryDemandShocks;
        private readonly List<ScheduledMarketShockState> _activeScheduledShocks;
        private int _lastUpdatedGameTimeMs;
        private int _lastScheduledShockWeekIndex;
        private Func<int> _getCurrentInGameMinute;
        private Func<IEnumerable<TerritoryDistrictState>> _getDistrictStates;
        private string _pendingShockAnnouncement;

        public GlobalMarketManager(int startGameTimeMs, IReadOnlyDictionary<string, float> commodityBasePrices = null)
        {
            _basePrices = new Dictionary<string, float>(BasePrices, StringComparer.OrdinalIgnoreCase);
            if (commodityBasePrices != null)
            {
                foreach (var pair in commodityBasePrices)
                {
                    var commodity = Domain.CommodityCatalog.Normalize(pair.Key);
                    if (string.IsNullOrWhiteSpace(commodity) || pair.Value <= 0f)
                    {
                        continue;
                    }

                    _basePrices[commodity] = pair.Value;
                }
            }

            _commodityStates = new Dictionary<string, CommodityMarketState>(StringComparer.OrdinalIgnoreCase);
            _temporaryDemandShocks = new Dictionary<string, TemporaryCommodityDemandShock>(StringComparer.OrdinalIgnoreCase);
            _activeScheduledShocks = new List<ScheduledMarketShockState>();
            _lastScheduledShockWeekIndex = -1;
            _pendingShockAnnouncement = string.Empty;
            Reset(startGameTimeMs);
        }

        public void ConfigureShockContext(Func<int> getCurrentInGameMinute, Func<IEnumerable<TerritoryDistrictState>> getDistrictStates)
        {
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _getDistrictStates = getDistrictStates;
        }

        public float PriceMultiplier
        {
            get
            {
                if (_commodityStates.Count == 0)
                {
                    return DefaultPriceMultiplier;
                }

                float total = 0f;
                var count = 0;
                foreach (var pair in _commodityStates)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }

                    total += GetPriceMultiplier(pair.Key);
                    count += 1;
                }

                return count > 0
                    ? total / count
                    : DefaultPriceMultiplier;
            }
        }

        public void Update(int gameTimeMs)
        {
            RotateScheduledShocksIfNeeded();

            foreach (var pair in _commodityStates)
            {
                UpdateCommodityState(pair.Value, gameTimeMs);
            }

            _lastUpdatedGameTimeMs = gameTimeMs;
        }

        public void Reset(int currentGameTimeMs)
        {
            _commodityStates.Clear();
            _temporaryDemandShocks.Clear();
            _activeScheduledShocks.Clear();
            _lastUpdatedGameTimeMs = currentGameTimeMs;
            _lastScheduledShockWeekIndex = -1;
            _pendingShockAnnouncement = string.Empty;
            InitializeKnownCommodityStates();
        }

        public GlobalMarketPersistenceSnapshot CreatePersistenceSnapshot(int currentGameTimeMs)
        {
            var snapshot = new GlobalMarketPersistenceSnapshot();
            foreach (var pair in _commodityStates)
            {
                if (pair.Value == null || pair.Key.Equals(DefaultCommodityKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                snapshot.CommodityStates.Add(new GlobalMarketCommodityPersistenceEntry
                {
                    Commodity = pair.Key,
                    PriceMultiplier = Math.Max(DefaultPriceMultiplier, pair.Value.PriceMultiplier),
                    RemainingScarcityMs = Math.Max(0, pair.Value.NextScarcityIncreaseAtMs - currentGameTimeMs),
                });
            }

            return snapshot;
        }

        public void ApplyPersistenceSnapshot(GlobalMarketPersistenceSnapshot snapshot, int currentGameTimeMs)
        {
            Reset(currentGameTimeMs);
            if (snapshot == null || !snapshot.HasData)
            {
                return;
            }

            for (int i = 0; i < snapshot.CommodityStates.Count; i++)
            {
                var entry = snapshot.CommodityStates[i];
                var commodity = Domain.CommodityCatalog.Normalize(entry != null ? entry.Commodity : string.Empty);
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    continue;
                }

                var state = EnsureCommodityState(commodity);
                state.PriceMultiplier = Math.Max(DefaultPriceMultiplier, entry.PriceMultiplier);
                state.NextScarcityIncreaseAtMs = currentGameTimeMs + Math.Max(0, entry.RemainingScarcityMs);
            }
        }

        public void RegisterDelivery(int gameTimeMs)
        {
            foreach (var pair in _commodityStates)
            {
                ApplyDeliveryPressure(pair.Value, gameTimeMs);
            }

            _lastUpdatedGameTimeMs = gameTimeMs;
        }

        public void RegisterDelivery(string commodity, int gameTimeMs)
        {
            ApplyDeliveryPressure(EnsureCommodityState(commodity), gameTimeMs);
            _lastUpdatedGameTimeMs = gameTimeMs;
        }

        public float GetPriceMultiplier(string commodity)
        {
            var baseMultiplier = EnsureCommodityState(commodity).PriceMultiplier;
            return Math.Max(DefaultPriceMultiplier, baseMultiplier + GetTemporaryDemandShockBonus(commodity));
        }

        public void SetTemporaryDemandShock(string sourceId, string commodity, float bonusMultiplier)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            var normalizedCommodity = Domain.CommodityCatalog.Normalize(commodity);
            var key = sourceId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedCommodity) || bonusMultiplier <= 0f)
            {
                _temporaryDemandShocks.Remove(key);
                return;
            }

            _temporaryDemandShocks[key] = new TemporaryCommodityDemandShock
            {
                Commodity = normalizedCommodity,
                BonusMultiplier = Math.Max(0f, bonusMultiplier),
            };
        }

        public void ClearTemporaryDemandShock(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            _temporaryDemandShocks.Remove(sourceId.Trim());
        }

        public float GetUnitPrice(string commodity)
        {
            commodity = Domain.CommodityCatalog.Normalize(commodity);
            float basePrice;
            if (!_basePrices.TryGetValue(commodity ?? string.Empty, out basePrice))
            {
                basePrice = 400f;
            }

            return basePrice * GetPriceMultiplier(commodity);
        }

        public float GetSinkDemandMultiplier(string districtName, string commodity)
        {
            commodity = Domain.CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(commodity) || _activeScheduledShocks.Count <= 0)
            {
                return 1f;
            }

            float bonus = 0f;
            for (int i = 0; i < _activeScheduledShocks.Count; i++)
            {
                var shock = _activeScheduledShocks[i];
                if (shock == null
                    || shock.DistrictDemandMultiplier <= 0f
                    || !shock.ContainsCommodity(commodity)
                    || !shock.MatchesDistrict(districtName))
                {
                    continue;
                }

                bonus += shock.DistrictDemandMultiplier;
            }

            return Math.Max(1f, 1f + bonus);
        }

        public bool TryGetShockHighlightReason(string commodity, string districtName, out string reason)
        {
            reason = string.Empty;
            commodity = Domain.CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(commodity) || _activeScheduledShocks.Count <= 0)
            {
                return false;
            }

            var districtShock = _activeScheduledShocks.FirstOrDefault(shock => shock != null && shock.ContainsCommodity(commodity) && shock.MatchesDistrict(districtName));
            if (districtShock != null)
            {
                reason = string.Format("Shock: {0} in {1}", districtShock.Title, districtShock.DistrictName);
                return true;
            }

            var marketShock = _activeScheduledShocks.FirstOrDefault(shock => shock != null && shock.ContainsCommodity(commodity));
            if (marketShock == null)
            {
                return false;
            }

            reason = string.Format("Shock: {0}", marketShock.Title);
            return true;
        }

        public string GetCommodityShockSummary(string commodity)
        {
            commodity = Domain.CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(commodity) || _activeScheduledShocks.Count <= 0)
            {
                return string.Empty;
            }

            var shock = _activeScheduledShocks.FirstOrDefault(entry => entry != null && entry.ContainsCommodity(commodity));
            if (shock == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(shock.DistrictName))
            {
                return string.Format(
                    "{0} | {1} demand +{2:0}% | Market +{3:0}%",
                    shock.Title,
                    shock.DistrictName,
                    shock.DistrictDemandMultiplier * 100f,
                    shock.CommodityBonusMultiplier * 100f);
            }

            return string.Format("{0} | Market +{1:0}%", shock.Title, shock.CommodityBonusMultiplier * 100f);
        }

        public string ConsumePendingShockAnnouncement()
        {
            var result = _pendingShockAnnouncement ?? string.Empty;
            _pendingShockAnnouncement = string.Empty;
            return result;
        }

        private CommodityMarketState EnsureCommodityState(string commodity)
        {
            var key = NormalizeCommodityKey(commodity);
            CommodityMarketState state;
            if (_commodityStates.TryGetValue(key, out state))
            {
                return state;
            }

            state = new CommodityMarketState
            {
                PriceMultiplier = DefaultPriceMultiplier,
                NextScarcityIncreaseAtMs = _lastUpdatedGameTimeMs + ScarcityIncreaseIntervalMs,
            };

            _commodityStates[key] = state;
            return state;
        }

        private void InitializeKnownCommodityStates()
        {
            foreach (var commodity in _basePrices.Keys)
            {
                EnsureCommodityState(commodity);
            }

            foreach (var commodity in Domain.CommodityCatalog.GetKnownCommodities())
            {
                EnsureCommodityState(commodity);
            }
        }

        private static void ApplyDeliveryPressure(CommodityMarketState state, int gameTimeMs)
        {
            if (state == null)
            {
                return;
            }

            state.PriceMultiplier = Math.Max(DefaultPriceMultiplier, state.PriceMultiplier - DeliveryPressureStep);
            state.NextScarcityIncreaseAtMs = gameTimeMs + ScarcityIncreaseIntervalMs;
        }

        private static void UpdateCommodityState(CommodityMarketState state, int gameTimeMs)
        {
            if (state == null)
            {
                return;
            }

            while (gameTimeMs >= state.NextScarcityIncreaseAtMs)
            {
                state.PriceMultiplier += ScarcityIncreaseStep;
                state.NextScarcityIncreaseAtMs += ScarcityIncreaseIntervalMs;
            }
        }

        private static string NormalizeCommodityKey(string commodity)
        {
            var normalized = Domain.CommodityCatalog.Normalize(commodity);
            return string.IsNullOrWhiteSpace(normalized)
                ? DefaultCommodityKey
                : normalized;
        }

        private float GetTemporaryDemandShockBonus(string commodity)
        {
            commodity = Domain.CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(commodity) || _temporaryDemandShocks.Count <= 0)
            {
                return 0f;
            }

            float total = 0f;
            foreach (var pair in _temporaryDemandShocks)
            {
                if (pair.Value == null || !string.Equals(pair.Value.Commodity, commodity, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                total += Math.Max(0f, pair.Value.BonusMultiplier);
            }

            return total;
        }

        private void RotateScheduledShocksIfNeeded()
        {
            if (_getCurrentInGameMinute == null)
            {
                return;
            }

            var currentWeekIndex = GetWeekIndex(_getCurrentInGameMinute());
            if (currentWeekIndex == _lastScheduledShockWeekIndex)
            {
                return;
            }

            var shouldAnnounce = _lastScheduledShockWeekIndex >= 0;
            _lastScheduledShockWeekIndex = currentWeekIndex;
            ClearScheduledShocks();

            var newShocks = BuildScheduledShocks(currentWeekIndex);
            for (int i = 0; i < newShocks.Count; i++)
            {
                var shock = newShocks[i];
                if (shock == null)
                {
                    continue;
                }

                _activeScheduledShocks.Add(shock);
                for (int commodityIndex = 0; commodityIndex < shock.Commodities.Count; commodityIndex++)
                {
                    SetTemporaryDemandShock(
                        string.Format("scheduled:{0}:{1}", shock.SourceId, shock.Commodities[commodityIndex]),
                        shock.Commodities[commodityIndex],
                        shock.CommodityBonusMultiplier);
                }
            }

            _pendingShockAnnouncement = shouldAnnounce
                ? BuildShockAnnouncement(_activeScheduledShocks)
                : string.Empty;
        }

        private void ClearScheduledShocks()
        {
            for (int i = 0; i < _activeScheduledShocks.Count; i++)
            {
                var shock = _activeScheduledShocks[i];
                if (shock == null)
                {
                    continue;
                }

                for (int commodityIndex = 0; commodityIndex < shock.Commodities.Count; commodityIndex++)
                {
                    ClearTemporaryDemandShock(string.Format("scheduled:{0}:{1}", shock.SourceId, shock.Commodities[commodityIndex]));
                }
            }

            _activeScheduledShocks.Clear();
        }

        private List<ScheduledMarketShockState> BuildScheduledShocks(int currentWeekIndex)
        {
            var shocks = new List<ScheduledMarketShockState>();
            if (ScheduledShockTemplates.Length == 0)
            {
                return shocks;
            }

            var districtStates = _getDistrictStates != null
                ? (_getDistrictStates() ?? Enumerable.Empty<TerritoryDistrictState>()).Where(state => state != null && !string.IsNullOrWhiteSpace(state.DistrictName)).ToList()
                : new List<TerritoryDistrictState>();
            var activeDistrictStates = districtStates
                .Where(state => state.LicenseStatus == DistrictLicenseStatus.Active || state.LicenseStatus == DistrictLicenseStatus.Probation)
                .ToList();
            var districtPool = (activeDistrictStates.Count > 0 ? activeDistrictStates : districtStates)
                .Select(state => state.DistrictName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var territoryScale = activeDistrictStates.Count;
            var shockCount = territoryScale >= 3 ? 2 : 1;
            var usedTemplateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int offset = 0; offset < shockCount; offset++)
            {
                var template = ResolveShockTemplate(currentWeekIndex, offset, usedTemplateIds);
                if (template == null)
                {
                    continue;
                }

                usedTemplateIds.Add(template.Id);
                var districtName = districtPool.Count > 0
                    ? districtPool[PositiveModulo((currentWeekIndex * 5) + (offset * 3), districtPool.Count)]
                    : string.Empty;
                var commodityBonusMultiplier = template.CommodityBonusMultiplier + Math.Min(0.04f, territoryScale * 0.01f);
                var districtDemandMultiplier = string.IsNullOrWhiteSpace(districtName)
                    ? 0f
                    : template.DistrictDemandMultiplier + Math.Min(0.08f, territoryScale * 0.02f);

                shocks.Add(new ScheduledMarketShockState(
                    string.Format("{0}:{1}", currentWeekIndex, template.Id),
                    template.Title,
                    districtName,
                    template.Commodities,
                    commodityBonusMultiplier,
                    districtDemandMultiplier));
            }

            return shocks;
        }

        private static ScheduledMarketShockTemplate ResolveShockTemplate(int currentWeekIndex, int offset, ISet<string> usedTemplateIds)
        {
            if (ScheduledShockTemplates.Length == 0)
            {
                return null;
            }

            for (int attempt = 0; attempt < ScheduledShockTemplates.Length; attempt++)
            {
                var index = PositiveModulo((currentWeekIndex * 17) + (offset * 7) + (attempt * 5), ScheduledShockTemplates.Length);
                var template = ScheduledShockTemplates[index];
                if (template == null || (usedTemplateIds != null && usedTemplateIds.Contains(template.Id)))
                {
                    continue;
                }

                return template;
            }

            return ScheduledShockTemplates[PositiveModulo(currentWeekIndex + offset, ScheduledShockTemplates.Length)];
        }

        private static string BuildShockAnnouncement(IReadOnlyList<ScheduledMarketShockState> shocks)
        {
            if (shocks == null || shocks.Count == 0)
            {
                return string.Empty;
            }

            var summaries = shocks
                .Where(shock => shock != null)
                .Select(shock => string.IsNullOrWhiteSpace(shock.DistrictName)
                    ? shock.Title
                    : string.Format("{0} in {1}", shock.Title, shock.DistrictName))
                .ToArray();
            if (summaries.Length == 0)
            {
                return string.Empty;
            }

            return summaries.Length == 1
                ? string.Format("Market shock active: {0}.", summaries[0])
                : string.Format("Market shocks active: {0}.", string.Join(" | ", summaries));
        }

        private static int GetWeekIndex(int currentInGameMinute)
        {
            if (currentInGameMinute <= 0)
            {
                return 0;
            }

            return currentInGameMinute / MinutesPerWeek;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private sealed class CommodityMarketState
        {
            public float PriceMultiplier { get; set; }

            public int NextScarcityIncreaseAtMs { get; set; }
        }

        private sealed class TemporaryCommodityDemandShock
        {
            public string Commodity { get; set; }

            public float BonusMultiplier { get; set; }
        }

        private sealed class ScheduledMarketShockTemplate
        {
            public ScheduledMarketShockTemplate(string id, string title, IEnumerable<string> commodities, float commodityBonusMultiplier, float districtDemandMultiplier)
            {
                Id = id ?? string.Empty;
                Title = title ?? string.Empty;
                Commodities = (commodities ?? Array.Empty<string>())
                    .Select(Domain.CommodityCatalog.Normalize)
                    .Where(commodity => !string.IsNullOrWhiteSpace(commodity))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                CommodityBonusMultiplier = Math.Max(0f, commodityBonusMultiplier);
                DistrictDemandMultiplier = Math.Max(0f, districtDemandMultiplier);
            }

            public string Id { get; }

            public string Title { get; }

            public IReadOnlyList<string> Commodities { get; }

            public float CommodityBonusMultiplier { get; }

            public float DistrictDemandMultiplier { get; }
        }

        private sealed class ScheduledMarketShockState
        {
            private readonly HashSet<string> _commodities;

            public ScheduledMarketShockState(string sourceId, string title, string districtName, IEnumerable<string> commodities, float commodityBonusMultiplier, float districtDemandMultiplier)
            {
                SourceId = sourceId ?? string.Empty;
                Title = title ?? string.Empty;
                DistrictName = districtName ?? string.Empty;
                Commodities = (commodities ?? Array.Empty<string>())
                    .Select(Domain.CommodityCatalog.Normalize)
                    .Where(commodity => !string.IsNullOrWhiteSpace(commodity))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                _commodities = new HashSet<string>(Commodities, StringComparer.OrdinalIgnoreCase);
                CommodityBonusMultiplier = Math.Max(0f, commodityBonusMultiplier);
                DistrictDemandMultiplier = Math.Max(0f, districtDemandMultiplier);
            }

            public string SourceId { get; }

            public string Title { get; }

            public string DistrictName { get; }

            public IReadOnlyList<string> Commodities { get; }

            public float CommodityBonusMultiplier { get; }

            public float DistrictDemandMultiplier { get; }

            public bool ContainsCommodity(string commodity)
            {
                commodity = Domain.CommodityCatalog.Normalize(commodity);
                return !string.IsNullOrWhiteSpace(commodity) && _commodities.Contains(commodity);
            }

            public bool MatchesDistrict(string districtName)
            {
                return !string.IsNullOrWhiteSpace(DistrictName)
                    && !string.IsNullOrWhiteSpace(districtName)
                    && string.Equals(DistrictName, districtName.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public sealed class GlobalMarketPersistenceSnapshot
    {
        public GlobalMarketPersistenceSnapshot()
        {
            CommodityStates = new List<GlobalMarketCommodityPersistenceEntry>();
        }

        public List<GlobalMarketCommodityPersistenceEntry> CommodityStates { get; private set; }

        public bool HasData
        {
            get { return CommodityStates.Count > 0; }
        }
    }

    public sealed class GlobalMarketCommodityPersistenceEntry
    {
        public string Commodity { get; set; }

        public float PriceMultiplier { get; set; }

        public int RemainingScarcityMs { get; set; }
    }
}
