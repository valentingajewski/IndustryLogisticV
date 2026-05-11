using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public enum TerritoryControlLevel
    {
        None = 0,
        Leased = 1,
        Owned = 2,
    }

    public enum TerritoryFranchiseLevel
    {
        None = 0,
        Serviced = 1,
        Preferred = 2,
        Signature = 3,
    }

    public enum CorridorRightLevel
    {
        None = 0,
        ServicePermit = 1,
        Corridor = 2,
        Priority = 3,
    }

    public enum DepotStaffRole
    {
        Loader = 0,
        Mechanic = 1,
        Guard = 2,
        Manager = 3,
    }

    public sealed class TerritoryManager
    {
        private const int RepossessionCooldownMs = 60000;

        private readonly IndustryManager _industryManager;
        private readonly Dictionary<string, Industry> _industriesById;
        private readonly Dictionary<string, DistrictConfig> _districtConfigsByName;
        private readonly Dictionary<string, TerritorySiteState> _sitesById;
        private readonly Dictionary<string, TerritoryDistrictState> _districtsByName;
        private readonly Dictionary<string, TerritoryCorridorState> _corridorsById;
        private readonly Dictionary<string, float> _districtReputationDebugOffsets;

        private int _lastRepossessionEvaluationMs;
        private bool _corridorRestrictionEnabled;

        public TerritoryManager(ModConfig config, IndustryManager industryManager)
        {
            _industryManager = industryManager;
            _industriesById = new Dictionary<string, Industry>(StringComparer.OrdinalIgnoreCase);
            _districtConfigsByName = new Dictionary<string, DistrictConfig>(StringComparer.OrdinalIgnoreCase);
            _sitesById = new Dictionary<string, TerritorySiteState>(StringComparer.OrdinalIgnoreCase);
            _districtsByName = new Dictionary<string, TerritoryDistrictState>(StringComparer.OrdinalIgnoreCase);
            _corridorsById = new Dictionary<string, TerritoryCorridorState>(StringComparer.OrdinalIgnoreCase);
            _districtReputationDebugOffsets = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _lastRepossessionEvaluationMs = int.MinValue;
            _corridorRestrictionEnabled = true;

            if (config != null && config.DistrictConfigs != null)
            {
                foreach (var pair in config.DistrictConfigs)
                {
                    var district = pair.Value;
                    if (district == null || string.IsNullOrWhiteSpace(district.Name))
                    {
                        continue;
                    }

                    _districtConfigsByName[district.Name] = district;
                    EnsureDistrictState(district.Name);
                }
            }

            Reset();
        }

        public IEnumerable<TerritorySiteState> SiteStates
        {
            get { return _sitesById.Values; }
        }

        public IEnumerable<TerritoryDistrictState> DistrictStates
        {
            get { return _districtsByName.Values; }
        }

        public IEnumerable<TerritoryCorridorState> CorridorStates
        {
            get { return _corridorsById.Values; }
        }

        public void RefreshState()
        {
            RefreshComputedState();
        }

        public void SetCorridorRestrictionEnabled(bool enabled)
        {
            _corridorRestrictionEnabled = enabled;
        }

        public void Reset()
        {
            _industriesById.Clear();
            _sitesById.Clear();
            _corridorsById.Clear();
            _districtReputationDebugOffsets.Clear();

            if (_industryManager != null && _industryManager.Industries != null)
            {
                for (int i = 0; i < _industryManager.Industries.Count; i++)
                {
                    var industry = _industryManager.Industries[i];
                    if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
                    {
                        continue;
                    }

                    _industriesById[industry.Id] = industry;
                    EnsureDistrictState(industry.DistrictName);

                    var siteState = new TerritorySiteState
                    {
                        SiteId = industry.Id,
                        DistrictName = industry.DistrictName ?? string.Empty,
                        ControlLevel = ResolveDefaultControlLevel(industry),
                        CrewAssigned = industry.IsStarterHeadquarters,
                        LoaderCount = industry.IsStarterHeadquarters ? 1 : 0,
                        MechanicCount = industry.IsStarterHeadquarters ? 1 : 0,
                        GuardCount = industry.IsStarterHeadquarters ? 1 : 0,
                        ManagerCount = industry.IsStarterHeadquarters ? 1 : 0,
                    };

                    _sitesById[siteState.SiteId] = siteState;
                }
            }

            RefreshComputedState();
        }

        public TerritorySiteState GetSiteState(Industry industry)
        {
            if (industry == null || string.IsNullOrWhiteSpace(industry.Id))
            {
                return null;
            }

            TerritorySiteState siteState;
            return _sitesById.TryGetValue(industry.Id, out siteState)
                ? siteState
                : null;
        }

        public TerritoryDistrictState GetDistrictState(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return null;
            }

            TerritoryDistrictState districtState;
            return _districtsByName.TryGetValue(districtName, out districtState)
                ? districtState
                : null;
        }

        public TerritoryCorridorState GetCorridorState(string districtA, string districtB)
        {
            if (string.IsNullOrWhiteSpace(districtA) || string.IsNullOrWhiteSpace(districtB) || string.Equals(districtA, districtB, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            TerritoryCorridorState corridorState;
            return _corridorsById.TryGetValue(BuildCorridorId(districtA, districtB), out corridorState)
                ? corridorState
                : null;
        }

        public float GetDistrictReputationDebugOffset(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return 0f;
            }

            float offset;
            return _districtReputationDebugOffsets.TryGetValue(districtName.Trim(), out offset)
                ? offset
                : 0f;
        }

        public void AdjustDistrictReputationDebug(string districtName, float delta)
        {
            if (string.IsNullOrWhiteSpace(districtName) || Math.Abs(delta) <= 0.001f)
            {
                return;
            }

            var normalized = districtName.Trim();
            var updated = GetDistrictReputationDebugOffset(normalized) + delta;
            if (Math.Abs(updated) <= 0.001f)
            {
                _districtReputationDebugOffsets.Remove(normalized);
            }
            else
            {
                _districtReputationDebugOffsets[normalized] = updated;
            }

            RefreshComputedState();
        }

        public IEnumerable<Industry> GetDepotIndustries()
        {
            return _industriesById.Values
                .Where(IsSupportSite)
                .OrderBy(industry => industry.DistrictName)
                .ThenBy(industry => industry.Name);
        }

        public int GetControlledDistrictCount()
        {
            return _districtsByName.Values.Count(x => x != null && x.InfluenceRatio >= 0.6f);
        }

        public int GetActiveCorridorCount()
        {
            return _corridorsById.Values.Count(x => x != null && x.RightLevel != CorridorRightLevel.None);
        }

        public bool IsDistrictEstablishedForNpc(string districtName)
        {
            var districtState = GetDistrictState(districtName);
            return districtState != null && GetReputationTier(districtState.ReputationLabel) >= 2;
        }

        public string GetNpcDistrictRequirementSummary(string districtName)
        {
            var districtState = GetDistrictState(districtName);
            if (districtState == null)
            {
                return "District influence data is unavailable.";
            }

            if (IsDistrictEstablishedForNpc(districtName))
            {
                return string.Empty;
            }

            var label = string.IsNullOrWhiteSpace(districtState.ReputationLabel)
                ? "Unknown"
                : districtState.ReputationLabel.Trim();
            return string.Format(
                "{0} is currently {1}. Reach Established before assigning NPC routes there.",
                districtState.DistrictName,
                label);
        }

        public string GetActivationSummary(Industry industry)
        {
            return GetActivationSummary(industry, true);
        }

        public string GetNpcActivationSummary(Industry industry)
        {
            return GetActivationSummary(industry, false);
        }

        public bool IsAutomationReady(Industry industry)
        {
            return IsAutomationReady(industry, true);
        }

        public bool IsNpcAutomationReady(Industry industry)
        {
            return IsAutomationReady(industry, false);
        }

        public bool CanCreateNpcRoute(Industry originIndustry, Industry destinationIndustry, out string reason)
        {
            return CanCreateNpcRoute(originIndustry, destinationIndustry, out reason, true);
        }

        public bool CanCreateNpcRouteWithPermits(Industry originIndustry, Industry destinationIndustry, out string reason)
        {
            reason = string.Empty;
            if (originIndustry == null || destinationIndustry == null)
            {
                reason = "Route endpoints are incomplete.";
                return false;
            }

            if (!HasCorridorAccess(originIndustry.DistrictName, destinationIndustry.DistrictName))
            {
                reason = string.Format(
                    "The corridor between {0} and {1} is not licensed yet. Establish it with player deliveries first.",
                    originIndustry.DistrictName,
                    destinationIndustry.DistrictName);
                return false;
            }

            return true;
        }

        private string GetActivationSummary(Industry industry, bool requireOwnership)
        {
            var siteState = GetSiteState(industry);
            return siteState != null
                ? BuildActivationSummary(industry, siteState, requireOwnership)
                : "No strategic state";
        }

        private bool IsAutomationReady(Industry industry, bool requireOwnership)
        {
            var siteState = GetSiteState(industry);
            return siteState != null && ResolveOperationalState(industry, siteState, requireOwnership);
        }

        private bool CanCreateNpcRoute(Industry originIndustry, Industry destinationIndustry, out string reason, bool requireOwnership)
        {
            reason = string.Empty;
            if (originIndustry == null || destinationIndustry == null)
            {
                reason = "Route endpoints are incomplete.";
                return false;
            }

            if (!IsAutomationReady(originIndustry, requireOwnership))
            {
                reason = string.Format("{0} is not operational yet. {1}.", originIndustry.Name, GetActivationSummary(originIndustry, requireOwnership));
                return false;
            }

            if (!IsAutomationReady(destinationIndustry, requireOwnership))
            {
                reason = string.Format("{0} is not operational yet. {1}.", destinationIndustry.Name, GetActivationSummary(destinationIndustry, requireOwnership));
                return false;
            }

            if (!HasCorridorAccess(originIndustry.DistrictName, destinationIndustry.DistrictName))
            {
                reason = string.Format(
                    "The corridor between {0} and {1} is not licensed yet. Establish it with player deliveries first.",
                    originIndustry.DistrictName,
                    destinationIndustry.DistrictName);
                return false;
            }

            return true;
        }

        public float GetNextDepotControlCost(Industry industry)
        {
            var siteState = GetSiteState(industry);
            if (industry == null || siteState == null)
            {
                return 0f;
            }

            return siteState.ControlLevel == TerritoryControlLevel.None
                ? GetDepotLeaseCost(industry)
                : (siteState.ControlLevel == TerritoryControlLevel.Leased ? GetDepotPurchaseCost(industry) : 0f);
        }

        public float GetCrewAssignmentCostPreview(Industry industry)
        {
            return industry == null ? 0f : GetCrewAssignmentCost(industry);
        }

        public float GetHireStaffCostPreview(Industry industry, DepotStaffRole staffRole)
        {
            var siteState = GetSiteState(industry);
            return siteState == null ? 0f : GetHireStaffCost(industry, staffRole, GetStaffCount(siteState, staffRole));
        }

        public int GetStaffCount(Industry industry, DepotStaffRole staffRole)
        {
            var siteState = GetSiteState(industry);
            return siteState == null ? 0 : GetStaffCount(siteState, staffRole);
        }

        public int GetMaxStaffCount(Industry industry, DepotStaffRole staffRole)
        {
            return ResolveMaxStaffCount(industry, staffRole);
        }

        public float GetDistrictSupportBonus(string districtName)
        {
            return GetDistrictSupportFactor(districtName);
        }

        public bool CanSpawnCompanyVehicleAt(Industry industry, out string reason)
        {
            reason = string.Empty;
            if (industry == null)
            {
                reason = "No industry selected.";
                return false;
            }

            if (!industry.VehicleSpawnPosition.HasValue)
            {
                reason = "No vehicle pad is configured here.";
                return false;
            }

            var siteState = GetSiteState(industry);
            if (siteState == null)
            {
                reason = "Strategic site data is unavailable.";
                return false;
            }

            if (siteState.HasSpawnRights)
            {
                return true;
            }

            if (IsSupportSite(industry))
            {
                if (siteState.ControlLevel == TerritoryControlLevel.None)
                {
                    reason = "Secure this depot or yard before deploying fleet here.";
                    return false;
                }

                reason = "Assign depot crew before local fleet deployment.";
                return false;
            }

            reason = "District influence is too weak for local fleet deployment here.";
            return false;
        }

        public bool TryAcquireDepot(Industry industry, ref float profit, out float cost, out string result)
        {
            cost = 0f;
            result = string.Empty;
            if (industry == null || !IsSupportSite(industry))
            {
                result = "This site is not a depot or fleet yard.";
                return false;
            }

            var siteState = GetSiteState(industry);
            if (siteState == null)
            {
                result = "Strategic site data is unavailable.";
                return false;
            }

            if (industry.IsStarterHeadquarters || siteState.ControlLevel == TerritoryControlLevel.Owned)
            {
                result = string.Format("{0} is already secured.", industry.Name);
                return false;
            }

            cost = siteState.ControlLevel == TerritoryControlLevel.None
                ? GetDepotLeaseCost(industry)
                : GetDepotPurchaseCost(industry);
            if (profit < cost)
            {
                result = string.Format("Need {0} more to secure {1}.", ModFormatting.FormatMoney(cost - profit), industry.Name);
                return false;
            }

            profit -= cost;
            siteState.ControlLevel = siteState.ControlLevel == TerritoryControlLevel.None
                ? TerritoryControlLevel.Leased
                : TerritoryControlLevel.Owned;
            RefreshComputedState();

            result = siteState.ControlLevel == TerritoryControlLevel.Owned
                ? string.Format("Purchased {0} for {1}.", industry.Name, ModFormatting.FormatMoney(cost))
                : string.Format("Leased {0} for {1}.", industry.Name, ModFormatting.FormatMoney(cost));
            return true;
        }

        public bool TryAssignCrew(Industry industry, ref float profit, out float cost, out string result)
        {
            cost = 0f;
            result = string.Empty;
            if (industry == null || !IsSupportSite(industry))
            {
                result = "Crew assignment is only available for depots and yards.";
                return false;
            }

            var siteState = GetSiteState(industry);
            if (siteState == null)
            {
                result = "Strategic site data is unavailable.";
                return false;
            }

            if (siteState.ControlLevel == TerritoryControlLevel.None && !industry.IsStarterHeadquarters)
            {
                result = "Secure the site before assigning a crew.";
                return false;
            }

            if (siteState.CrewAssigned)
            {
                result = string.Format("{0} already has an operating crew.", industry.Name);
                return false;
            }

            cost = GetCrewAssignmentCost(industry);
            if (profit < cost)
            {
                result = string.Format("Need {0} more to assign a crew.", ModFormatting.FormatMoney(cost - profit));
                return false;
            }

            profit -= cost;
            siteState.CrewAssigned = true;
            siteState.LoaderCount = Math.Max(siteState.LoaderCount, 1);
            siteState.MechanicCount = Math.Max(siteState.MechanicCount, 1);
            siteState.GuardCount = Math.Max(siteState.GuardCount, 1);
            siteState.ManagerCount = Math.Max(siteState.ManagerCount, 1);
            RefreshComputedState();

            result = string.Format("Assigned an operating crew to {0} for {1}.", industry.Name, ModFormatting.FormatMoney(cost));
            return true;
        }

        public bool TryHireDepotStaff(Industry industry, DepotStaffRole staffRole, ref float profit, out float cost, out string result)
        {
            cost = 0f;
            result = string.Empty;
            if (industry == null || !IsSupportSite(industry))
            {
                result = "Additional staff can only be hired for depots and yards.";
                return false;
            }

            var siteState = GetSiteState(industry);
            if (siteState == null)
            {
                result = "Strategic site data is unavailable.";
                return false;
            }

            if (!siteState.CrewAssigned)
            {
                result = "Assign a base crew before expanding the depot staff.";
                return false;
            }

            if (siteState.ControlLevel == TerritoryControlLevel.None && !industry.IsStarterHeadquarters)
            {
                result = "Secure the site before hiring staff.";
                return false;
            }

            var currentCount = GetStaffCount(siteState, staffRole);
            var maxCount = ResolveMaxStaffCount(industry, staffRole);
            if (currentCount >= maxCount)
            {
                result = string.Format("{0} already has the maximum {1} staff.", industry.Name, staffRole.ToString().ToLowerInvariant());
                return false;
            }

            cost = GetHireStaffCost(industry, staffRole, currentCount);
            if (profit < cost)
            {
                result = string.Format("Need {0} more to hire a {1}.", ModFormatting.FormatMoney(cost - profit), staffRole.ToString().ToLowerInvariant());
                return false;
            }

            profit -= cost;
            SetStaffCount(siteState, staffRole, currentCount + 1);
            RefreshComputedState();

            result = string.Format(
                "Hired a {0} for {1} at {2}.",
                staffRole.ToString().ToLowerInvariant(),
                ModFormatting.FormatMoney(cost),
                industry.Name);
            return true;
        }

        public void RegisterLoad(Industry originIndustry, string commodity, float tons, bool viaNpc)
        {
            if (originIndustry == null || tons <= 0.001f)
            {
                return;
            }

            var siteState = GetSiteState(originIndustry);
            if (siteState == null)
            {
                return;
            }

            siteState.LoadRuns += 1;
            siteState.TotalLoadedTons += Math.Max(0f, tons);
            siteState.LastCommodity = CommodityCatalog.Normalize(commodity);
            if (viaNpc)
            {
                siteState.NpcLoads += 1;
            }

            RefreshComputedState();
        }

        public void RegisterDelivery(Industry destinationIndustry, string commodity, float tons, bool viaNpc, string originIndustryId, string originDistrictName)
        {
            if (destinationIndustry == null || tons <= 0.001f)
            {
                return;
            }

            var siteState = GetSiteState(destinationIndustry);
            if (siteState == null)
            {
                return;
            }

            siteState.UnloadRuns += 1;
            siteState.TotalDeliveries += 1;
            siteState.TotalDeliveredTons += Math.Max(0f, tons);
            siteState.LastCommodity = CommodityCatalog.Normalize(commodity);
            if (viaNpc)
            {
                siteState.NpcDeliveries += 1;
            }

            if (IsServiceSink(destinationIndustry))
            {
                siteState.FranchiseLevel = ResolveFranchiseLevel(siteState.TotalDeliveries, siteState.TotalDeliveredTons);
            }

            var resolvedOriginDistrict = ResolveOriginDistrict(originIndustryId, originDistrictName);
            if (!string.IsNullOrWhiteSpace(resolvedOriginDistrict)
                && !string.IsNullOrWhiteSpace(destinationIndustry.DistrictName)
                && !string.Equals(resolvedOriginDistrict, destinationIndustry.DistrictName, StringComparison.OrdinalIgnoreCase))
            {
                var corridorState = GetOrCreateCorridorState(resolvedOriginDistrict, destinationIndustry.DistrictName);
                corridorState.DeliveryCount += 1;
                corridorState.TotalDeliveredTons += Math.Max(0f, tons);
                corridorState.RightLevel = ResolveCorridorLevel(corridorState.DeliveryCount, corridorState.TotalDeliveredTons);
            }

            RefreshComputedState();
        }

        public float AdjustDeliveryRevenue(Industry destinationIndustry, string commodity, float deliveredTons, float baseRevenue)
        {
            if (destinationIndustry == null || baseRevenue <= 0.001f)
            {
                return baseRevenue;
            }

            var multiplier = 1f;
            var siteState = GetSiteState(destinationIndustry);
            if (siteState != null)
            {
                if (!siteState.IsOperational)
                {
                    multiplier *= 0.88f;
                }

                if (IsServiceSink(destinationIndustry))
                {
                    multiplier += 0.035f * (int)siteState.FranchiseLevel;
                }

                if (siteState.ControlLevel == TerritoryControlLevel.Leased)
                {
                    multiplier += 0.04f;
                }
                else if (siteState.ControlLevel == TerritoryControlLevel.Owned)
                {
                    multiplier += 0.08f;
                }
            }

            var districtState = GetDistrictState(destinationIndustry.DistrictName);
            if (districtState != null)
            {
                multiplier += 0.08f * Math.Min(1f, districtState.InfluenceRatio);
                if (districtState.InfluenceRatio >= 0.6f)
                {
                    multiplier += 0.04f;
                }

                multiplier += Math.Min(0.08f, GetDistrictSupportFactor(destinationIndustry.DistrictName));
            }

            if (!string.IsNullOrWhiteSpace(commodity) && commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase) && destinationIndustry.IsConstructionSink)
            {
                multiplier += 0.02f;
            }

            return baseRevenue * Math.Max(0.55f, multiplier);
        }

        public float AdjustNpcLossRatio(Industry originIndustry, Industry destinationIndustry, float baseLossRatio)
        {
            var adjusted = Math.Max(0f, baseLossRatio);

            if (originIndustry != null && destinationIndustry != null)
            {
                if (string.Equals(originIndustry.DistrictName, destinationIndustry.DistrictName, StringComparison.OrdinalIgnoreCase))
                {
                    adjusted *= 0.88f;
                }

                var corridorState = GetCorridorState(originIndustry.DistrictName, destinationIndustry.DistrictName);
                if (corridorState != null)
                {
                    adjusted *= 1f - (0.08f * (int)corridorState.RightLevel);
                }

                adjusted *= 1f - Math.Min(0.18f, GetDistrictSupportFactor(originIndustry.DistrictName) + GetDistrictSupportFactor(destinationIndustry.DistrictName));

                if (!IsNpcAutomationReady(originIndustry) || !IsNpcAutomationReady(destinationIndustry))
                {
                    adjusted *= 1.15f;
                }
            }

            return Math.Max(0f, Math.Min(0.95f, adjusted));
        }

        public void OnIndustryAccessChanged(Industry industry)
        {
            if (industry == null)
            {
                return;
            }

            RefreshComputedState();
        }

        public void EvaluateFinancialPressure(float profit, int gameTimeMs, Action<string> showStatus)
        {
            if (gameTimeMs - _lastRepossessionEvaluationMs < RepossessionCooldownMs)
            {
                return;
            }

            _lastRepossessionEvaluationMs = gameTimeMs;
            if (profit >= 0f)
            {
                return;
            }

            var leasedDepot = _sitesById.Values
                .Where(siteState => siteState != null && siteState.ControlLevel == TerritoryControlLevel.Leased)
                .Select(siteState => new { Site = siteState, Industry = ResolveIndustry(siteState.SiteId) })
                .Where(x => x.Industry != null && IsSupportSite(x.Industry) && !x.Industry.IsStarterHeadquarters)
                .OrderByDescending(x => GetSupportSiteScore(x.Industry, x.Site))
                .FirstOrDefault();
            if (leasedDepot != null)
            {
                leasedDepot.Site.ControlLevel = TerritoryControlLevel.None;
                leasedDepot.Site.CrewAssigned = false;
                leasedDepot.Site.LoaderCount = 0;
                leasedDepot.Site.MechanicCount = 0;
                leasedDepot.Site.GuardCount = 0;
                leasedDepot.Site.ManagerCount = 0;
                leasedDepot.Site.Repossessions += 1;
                RefreshComputedState();

                if (showStatus != null)
                {
                    showStatus(string.Format("Financial pressure: lease lost on {0}.", leasedDepot.Industry.Name));
                }

                return;
            }

            var franchiseSite = _sitesById.Values
                .Where(siteState => siteState != null && siteState.FranchiseLevel != TerritoryFranchiseLevel.None)
                .Select(siteState => new { Site = siteState, Industry = ResolveIndustry(siteState.SiteId) })
                .Where(x => x.Industry != null && IsServiceSink(x.Industry))
                .OrderByDescending(x => (int)x.Site.FranchiseLevel)
                .ThenByDescending(x => x.Site.TotalDeliveredTons)
                .FirstOrDefault();
            if (franchiseSite != null)
            {
                franchiseSite.Site.FranchiseLevel -= 1;
                franchiseSite.Site.Repossessions += 1;
                RefreshComputedState();

                if (showStatus != null)
                {
                    showStatus(string.Format("Financial pressure: franchise share slipped at {0}.", franchiseSite.Industry.Name));
                }

                return;
            }

            var corridorState = _corridorsById.Values
                .Where(x => x != null && x.RightLevel != CorridorRightLevel.None)
                .OrderByDescending(x => (int)x.RightLevel)
                .ThenByDescending(x => x.TotalDeliveredTons)
                .FirstOrDefault();
            if (corridorState != null)
            {
                corridorState.RightLevel -= 1;
                corridorState.DeliveryCount = Math.Min(corridorState.DeliveryCount, GetMinimumDeliveryCountForLevel(corridorState.RightLevel));
                corridorState.TotalDeliveredTons = Math.Min(corridorState.TotalDeliveredTons, GetMinimumDeliveredTonsForLevel(corridorState.RightLevel));
                RefreshComputedState();

                if (showStatus != null)
                {
                    showStatus(string.Format("Financial pressure: corridor rights slipped on {0} / {1}.", corridorState.DistrictA, corridorState.DistrictB));
                }
            }
        }

        public TerritoryPersistenceSnapshot CreateSnapshot()
        {
            var snapshot = new TerritoryPersistenceSnapshot();

            foreach (var siteState in _sitesById.Values.OrderBy(x => x.SiteId, StringComparer.OrdinalIgnoreCase))
            {
                if (siteState == null)
                {
                    continue;
                }

                var hasState = siteState.ControlLevel != TerritoryControlLevel.None
                    || siteState.CrewAssigned
                    || siteState.LoadRuns > 0
                    || siteState.UnloadRuns > 0
                    || siteState.TotalDeliveries > 0
                    || siteState.TotalDeliveredTons > 0.001f
                    || siteState.TotalLoadedTons > 0.001f
                    || siteState.LoaderCount > 0
                    || siteState.MechanicCount > 0
                    || siteState.GuardCount > 0
                    || siteState.ManagerCount > 0
                    || siteState.FranchiseLevel != TerritoryFranchiseLevel.None
                    || siteState.Repossessions > 0;
                if (!hasState)
                {
                    continue;
                }

                snapshot.Sites.Add(new TerritorySiteSnapshot
                {
                    SiteId = siteState.SiteId,
                    ControlLevel = siteState.ControlLevel,
                    CrewAssigned = siteState.CrewAssigned,
                    LoadRuns = siteState.LoadRuns,
                    UnloadRuns = siteState.UnloadRuns,
                    TotalDeliveries = siteState.TotalDeliveries,
                    TotalDeliveredTons = siteState.TotalDeliveredTons,
                    TotalLoadedTons = siteState.TotalLoadedTons,
                    FranchiseLevel = siteState.FranchiseLevel,
                    LoaderCount = siteState.LoaderCount,
                    MechanicCount = siteState.MechanicCount,
                    GuardCount = siteState.GuardCount,
                    ManagerCount = siteState.ManagerCount,
                    Repossessions = siteState.Repossessions,
                    NpcLoads = siteState.NpcLoads,
                    NpcDeliveries = siteState.NpcDeliveries,
                    LastCommodity = siteState.LastCommodity,
                });
            }

            foreach (var corridorState in _corridorsById.Values.OrderBy(x => x.CorridorId, StringComparer.OrdinalIgnoreCase))
            {
                if (corridorState == null || corridorState.DeliveryCount <= 0)
                {
                    continue;
                }

                snapshot.Corridors.Add(new TerritoryCorridorSnapshot
                {
                    DistrictA = corridorState.DistrictA,
                    DistrictB = corridorState.DistrictB,
                    DeliveryCount = corridorState.DeliveryCount,
                    TotalDeliveredTons = corridorState.TotalDeliveredTons,
                    RightLevel = corridorState.RightLevel,
                });
            }

            return snapshot;
        }

        public void ApplySnapshot(TerritoryPersistenceSnapshot snapshot)
        {
            Reset();
            if (snapshot == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Sites.Count; i++)
            {
                var source = snapshot.Sites[i];
                if (source == null || string.IsNullOrWhiteSpace(source.SiteId))
                {
                    continue;
                }

                TerritorySiteState target;
                if (!_sitesById.TryGetValue(source.SiteId, out target) || target == null)
                {
                    continue;
                }

                target.ControlLevel = source.ControlLevel;
                target.CrewAssigned = source.CrewAssigned;
                target.LoadRuns = Math.Max(0, source.LoadRuns);
                target.UnloadRuns = Math.Max(0, source.UnloadRuns);
                target.TotalDeliveries = Math.Max(0, source.TotalDeliveries);
                target.TotalDeliveredTons = Math.Max(0f, source.TotalDeliveredTons);
                target.TotalLoadedTons = Math.Max(0f, source.TotalLoadedTons);
                target.FranchiseLevel = source.FranchiseLevel;
                target.LoaderCount = Math.Max(0, source.LoaderCount);
                target.MechanicCount = Math.Max(0, source.MechanicCount);
                target.GuardCount = Math.Max(0, source.GuardCount);
                target.ManagerCount = Math.Max(0, source.ManagerCount);
                target.Repossessions = Math.Max(0, source.Repossessions);
                target.NpcLoads = Math.Max(0, source.NpcLoads);
                target.NpcDeliveries = Math.Max(0, source.NpcDeliveries);
                target.LastCommodity = CommodityCatalog.Normalize(source.LastCommodity);
            }

            for (int i = 0; i < snapshot.Corridors.Count; i++)
            {
                var source = snapshot.Corridors[i];
                if (source == null || string.IsNullOrWhiteSpace(source.DistrictA) || string.IsNullOrWhiteSpace(source.DistrictB))
                {
                    continue;
                }

                var corridorState = GetOrCreateCorridorState(source.DistrictA, source.DistrictB);
                corridorState.DeliveryCount = Math.Max(0, source.DeliveryCount);
                corridorState.TotalDeliveredTons = Math.Max(0f, source.TotalDeliveredTons);
                corridorState.RightLevel = source.RightLevel;
            }

            RefreshComputedState();
        }

        private void RefreshComputedState()
        {
            foreach (var districtState in _districtsByName.Values)
            {
                districtState.Reset();
            }

            foreach (var siteState in _sitesById.Values)
            {
                var industry = ResolveIndustry(siteState.SiteId);
                if (industry == null)
                {
                    continue;
                }

                var districtState = EnsureDistrictState(industry.DistrictName);
                siteState.DistrictName = industry.DistrictName ?? string.Empty;
                siteState.HasSpawnRights = ResolveSpawnRights(industry, siteState);
                siteState.IsOperational = ResolveOperationalState(industry, siteState);
                siteState.ActivationSummary = BuildActivationSummary(industry, siteState);
                if (IsServiceSink(industry))
                {
                    siteState.FranchiseLevel = ResolveFranchiseLevel(siteState.TotalDeliveries, siteState.TotalDeliveredTons);
                }

                districtState.SiteCount += 1;
                if (siteState.ControlLevel != TerritoryControlLevel.None)
                {
                    districtState.ControlledSites += 1;
                }

                if (siteState.IsOperational)
                {
                    districtState.OperationalSites += 1;
                }

                if (IsSupportSite(industry) && siteState.ControlLevel != TerritoryControlLevel.None)
                {
                    districtState.ControlledDepots += 1;
                }

                if (siteState.FranchiseLevel != TerritoryFranchiseLevel.None)
                {
                    districtState.FranchiseSites += 1;
                }

                districtState.InfluenceScore += ComputeSiteInfluence(industry, siteState);
                districtState.ReputationScore += ComputeSiteReputation(industry, siteState);
            }

            foreach (var corridorState in _corridorsById.Values)
            {
                var districtA = EnsureDistrictState(corridorState.DistrictA);
                var districtB = EnsureDistrictState(corridorState.DistrictB);
                var routeValue = (int)corridorState.RightLevel;

                districtA.RouteRights += routeValue;
                districtB.RouteRights += routeValue;
                districtA.InfluenceScore += routeValue * 4f;
                districtB.InfluenceScore += routeValue * 4f;
                districtA.ReputationScore += routeValue * 2.5f;
                districtB.ReputationScore += routeValue * 2.5f;
            }

            foreach (var districtState in _districtsByName.Values)
            {
                var target = GetDistrictInfluenceTarget(districtState.DistrictName, districtState.SiteCount);
                districtState.InfluenceRatio = target <= 0.001f
                    ? 0f
                    : Math.Max(0f, Math.Min(1.5f, districtState.InfluenceScore / target));

                float debugOffset;
                if (_districtReputationDebugOffsets.TryGetValue(districtState.DistrictName ?? string.Empty, out debugOffset))
                {
                    districtState.ReputationScore += debugOffset;
                }

                districtState.ReputationLabel = ResolveReputationLabel(districtState.InfluenceRatio, districtState.ReputationScore);
            }
        }

        private TerritoryDistrictState EnsureDistrictState(string districtName)
        {
            var normalizedName = districtName ?? string.Empty;
            TerritoryDistrictState districtState;
            if (_districtsByName.TryGetValue(normalizedName, out districtState))
            {
                return districtState;
            }

            districtState = new TerritoryDistrictState
            {
                DistrictName = normalizedName,
            };
            _districtsByName[normalizedName] = districtState;
            return districtState;
        }

        private TerritoryCorridorState GetOrCreateCorridorState(string districtA, string districtB)
        {
            var corridorId = BuildCorridorId(districtA, districtB);
            TerritoryCorridorState corridorState;
            if (_corridorsById.TryGetValue(corridorId, out corridorState))
            {
                return corridorState;
            }

            var ordered = OrderDistrictPair(districtA, districtB);
            corridorState = new TerritoryCorridorState
            {
                CorridorId = corridorId,
                DistrictA = ordered.Item1,
                DistrictB = ordered.Item2,
            };
            _corridorsById[corridorId] = corridorState;
            return corridorState;
        }

        private Industry ResolveIndustry(string siteId)
        {
            if (string.IsNullOrWhiteSpace(siteId))
            {
                return null;
            }

            Industry industry;
            return _industriesById.TryGetValue(siteId, out industry)
                ? industry
                : null;
        }

        private string ResolveOriginDistrict(string originIndustryId, string originDistrictName)
        {
            if (!string.IsNullOrWhiteSpace(originDistrictName))
            {
                return originDistrictName;
            }

            var originIndustry = ResolveIndustry(originIndustryId);
            return originIndustry != null ? originIndustry.DistrictName : string.Empty;
        }

        private static TerritoryControlLevel ResolveDefaultControlLevel(Industry industry)
        {
            return industry != null && industry.IsStarterHeadquarters
                ? TerritoryControlLevel.Owned
                : TerritoryControlLevel.None;
        }

        private bool ResolveSpawnRights(Industry industry, TerritorySiteState siteState)
        {
            if (industry == null || siteState == null || !industry.VehicleSpawnPosition.HasValue)
            {
                return false;
            }

            if (industry.IsStarterHeadquarters)
            {
                return true;
            }

            if (IsSupportSite(industry))
            {
                return siteState.ControlLevel != TerritoryControlLevel.None && siteState.CrewAssigned;
            }

            var districtState = GetDistrictState(industry.DistrictName);
            return districtState != null && districtState.InfluenceRatio >= 0.85f && ResolveOperationalState(industry, siteState);
        }

        private bool ResolveOperationalState(Industry industry, TerritorySiteState siteState)
        {
            return ResolveOperationalState(industry, siteState, true);
        }

        private bool ResolveOperationalState(Industry industry, TerritorySiteState siteState, bool requireOwnership)
        {
            if (industry == null || siteState == null)
            {
                return false;
            }

            if (industry.IsStarterHeadquarters)
            {
                return true;
            }

            if (IsSupportSite(industry))
            {
                return siteState.ControlLevel != TerritoryControlLevel.None && siteState.CrewAssigned;
            }

            if (requireOwnership && _industryManager.RequiresIndustryPurchase(industry) && !industry.IsOwned)
            {
                return false;
            }

            if (_industryManager.RequiresContractorPermit(industry) && !industry.HasContractorPermit)
            {
                return false;
            }

            if (RequiresInboundActivation(industry))
            {
                return siteState.UnloadRuns > 0 || siteState.TotalDeliveries > 0;
            }

            if (RequiresOutboundActivation(industry))
            {
                return siteState.LoadRuns > 0;
            }

            return true;
        }

        private string BuildActivationSummary(Industry industry, TerritorySiteState siteState)
        {
            return BuildActivationSummary(industry, siteState, true);
        }

        private string BuildActivationSummary(Industry industry, TerritorySiteState siteState, bool requireOwnership)
        {
            if (industry == null || siteState == null)
            {
                return "No strategic state";
            }

            if (industry.IsStarterHeadquarters)
            {
                return "Starter headquarters online";
            }

            if (IsSupportSite(industry))
            {
                if (siteState.ControlLevel == TerritoryControlLevel.None)
                {
                    return "Secure site control";
                }

                if (!siteState.CrewAssigned)
                {
                    return "Assign depot crew";
                }

                return "Operational support base";
            }

            if (requireOwnership && _industryManager.RequiresIndustryPurchase(industry) && !industry.IsOwned)
            {
                return "Purchase site ownership";
            }

            if (_industryManager.RequiresContractorPermit(industry) && !industry.HasContractorPermit)
            {
                return "Purchase contractor permit";
            }

            if (RequiresInboundActivation(industry) && siteState.UnloadRuns <= 0)
            {
                return industry.IsConstructionSink
                    ? "Make first build delivery"
                    : "Make first supply run";
            }

            if (RequiresOutboundActivation(industry) && siteState.LoadRuns <= 0)
            {
                return "Run first dispatch";
            }

            return "Operational";
        }

        private static bool RequiresInboundActivation(Industry industry)
        {
            return industry != null
                && (industry.IsSink
                    || industry.Inputs.Count > 0);
        }

        private static bool RequiresOutboundActivation(Industry industry)
        {
            return industry != null
                && !RequiresInboundActivation(industry)
                && industry.Outputs.Count > 0;
        }

        private bool HasCorridorAccess(string districtA, string districtB)
        {
            if (string.IsNullOrWhiteSpace(districtA) || string.IsNullOrWhiteSpace(districtB))
            {
                return true;
            }

            if (string.Equals(districtA, districtB, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!_corridorRestrictionEnabled)
            {
                return true;
            }

            var corridorState = GetCorridorState(districtA, districtB);
            return corridorState != null && corridorState.RightLevel != CorridorRightLevel.None;
        }

        private float ComputeSiteInfluence(Industry industry, TerritorySiteState siteState)
        {
            var baseWeight = GetTierWeight(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            var influence = 0f;

            if (industry != null && industry.IsStarterHeadquarters)
            {
                influence += 18f;
            }

            if (siteState.ControlLevel == TerritoryControlLevel.Leased)
            {
                influence += baseWeight * 0.85f;
            }
            else if (siteState.ControlLevel == TerritoryControlLevel.Owned)
            {
                influence += baseWeight * 1.2f;
            }

            if (siteState.IsOperational)
            {
                influence += baseWeight * 0.7f;
            }

            influence += Math.Min(12f, (siteState.LoadRuns + siteState.UnloadRuns) * 0.6f);
            influence += Math.Min(18f, siteState.TotalDeliveredTons * 0.06f);
            influence += (int)siteState.FranchiseLevel * 3.5f;
            influence += (siteState.ManagerCount * 1.4f) + (siteState.LoaderCount * 0.75f) + (siteState.MechanicCount * 1.15f) + (siteState.GuardCount * 0.9f);
            return influence;
        }

        private float ComputeSiteReputation(Industry industry, TerritorySiteState siteState)
        {
            var reputation = 0f;
            reputation += Math.Min(10f, siteState.TotalDeliveries * 0.55f);
            reputation += Math.Min(8f, siteState.TotalDeliveredTons * 0.03f);

            if (industry != null && industry.IsConstructionSink)
            {
                reputation += siteState.TotalDeliveries * 0.4f;
            }

            if (siteState.IsOperational)
            {
                reputation += 3f;
            }

            return reputation;
        }

        private float GetDistrictSupportFactor(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return 0f;
            }

            var support = 0f;
            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null || !string.Equals(siteState.DistrictName, districtName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var industry = ResolveIndustry(siteState.SiteId);
                if (industry == null || !IsSupportSite(industry) || siteState.ControlLevel == TerritoryControlLevel.None)
                {
                    continue;
                }

                support += siteState.LoaderCount * 0.01f;
                support += siteState.MechanicCount * 0.015f;
                support += siteState.GuardCount * 0.008f;
                support += siteState.ManagerCount * 0.02f;
            }

            return Math.Max(0f, Math.Min(0.18f, support));
        }

        private float GetDistrictInfluenceTarget(string districtName, int siteCount)
        {
            var baseline = 24f + (siteCount * 12f);
            DistrictConfig districtConfig;
            if (_districtConfigsByName.TryGetValue(districtName ?? string.Empty, out districtConfig) && districtConfig != null)
            {
                baseline += Math.Max(0, districtConfig.PolygonVertices.Count - 4) * 2f;
            }

            return baseline;
        }

        private static string ResolveReputationLabel(float influenceRatio, float reputationScore)
        {
            var score = influenceRatio * 100f + reputationScore;
            if (score >= 125f)
            {
                return "Dominant";
            }

            if (score >= 90f)
            {
                return "Anchored";
            }

            if (score >= 60f)
            {
                return "Established";
            }

            if (score >= 28f)
            {
                return "Emerging";
            }

            return "Unknown";
        }

        private static int GetReputationTier(string reputationLabel)
        {
            switch ((reputationLabel ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "DOMINANT":
                    return 4;
                case "ANCHORED":
                    return 3;
                case "ESTABLISHED":
                    return 2;
                case "EMERGING":
                    return 1;
                default:
                    return 0;
            }
        }

        private static TerritoryFranchiseLevel ResolveFranchiseLevel(int deliveryCount, float deliveredTons)
        {
            if (deliveryCount >= 12 || deliveredTons >= 120f)
            {
                return TerritoryFranchiseLevel.Signature;
            }

            if (deliveryCount >= 6 || deliveredTons >= 45f)
            {
                return TerritoryFranchiseLevel.Preferred;
            }

            if (deliveryCount >= 2 || deliveredTons >= 10f)
            {
                return TerritoryFranchiseLevel.Serviced;
            }

            return TerritoryFranchiseLevel.None;
        }

        private static CorridorRightLevel ResolveCorridorLevel(int deliveryCount, float deliveredTons)
        {
            if (deliveryCount >= 10 || deliveredTons >= 180f)
            {
                return CorridorRightLevel.Priority;
            }

            if (deliveryCount >= 5 || deliveredTons >= 90f)
            {
                return CorridorRightLevel.Corridor;
            }

            if (deliveryCount >= 2 || deliveredTons >= 30f)
            {
                return CorridorRightLevel.ServicePermit;
            }

            return CorridorRightLevel.None;
        }

        private static int GetMinimumDeliveryCountForLevel(CorridorRightLevel rightLevel)
        {
            switch (rightLevel)
            {
                case CorridorRightLevel.Priority:
                    return 10;
                case CorridorRightLevel.Corridor:
                    return 5;
                case CorridorRightLevel.ServicePermit:
                    return 2;
                default:
                    return 0;
            }
        }

        private static float GetMinimumDeliveredTonsForLevel(CorridorRightLevel rightLevel)
        {
            switch (rightLevel)
            {
                case CorridorRightLevel.Priority:
                    return 180f;
                case CorridorRightLevel.Corridor:
                    return 90f;
                case CorridorRightLevel.ServicePermit:
                    return 30f;
                default:
                    return 0f;
            }
        }

        private static bool IsSupportSite(Industry industry)
        {
            return industry != null && (industry.IsDepotLike || industry.SiteRole == SiteRole.FleetYard || industry.IsStarterHeadquarters);
        }

        private static bool IsServiceSink(Industry industry)
        {
            return industry != null && (industry.IsStore || industry.IsGasStation || industry.IsConstructionSink);
        }

        private static float GetTierWeight(SiteOwnershipTier tier)
        {
            switch (tier)
            {
                case SiteOwnershipTier.Starter:
                    return 6f;
                case SiteOwnershipTier.Local:
                    return 8f;
                case SiteOwnershipTier.Regional:
                    return 10f;
                case SiteOwnershipTier.Expansion:
                    return 13f;
                case SiteOwnershipTier.Core:
                    return 16f;
                case SiteOwnershipTier.Monopoly:
                    return 20f;
                default:
                    return 8f;
            }
        }

        private static int GetTierRank(SiteOwnershipTier tier)
        {
            switch (tier)
            {
                case SiteOwnershipTier.Starter:
                    return 0;
                case SiteOwnershipTier.Local:
                    return 1;
                case SiteOwnershipTier.Regional:
                    return 2;
                case SiteOwnershipTier.Expansion:
                    return 3;
                case SiteOwnershipTier.Core:
                    return 4;
                case SiteOwnershipTier.Monopoly:
                    return 5;
                default:
                    return 1;
            }
        }

        private static float GetDepotLeaseCost(Industry industry)
        {
            var tierRank = GetTierRank(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            var baseCost = 12000f + (tierRank * 7000f);
            if (industry != null && industry.SiteRole == SiteRole.FleetYard)
            {
                baseCost *= 1.15f;
            }

            return baseCost;
        }

        private static float GetDepotPurchaseCost(Industry industry)
        {
            var tierRank = GetTierRank(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            var baseCost = 52000f + (tierRank * 22000f);
            if (industry != null && industry.SiteRole == SiteRole.FleetYard)
            {
                baseCost *= 1.2f;
            }

            return baseCost;
        }

        private static float GetCrewAssignmentCost(Industry industry)
        {
            var tierRank = GetTierRank(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            return 8000f + (tierRank * 2500f);
        }

        private static float GetHireStaffCost(Industry industry, DepotStaffRole staffRole, int currentCount)
        {
            var tierRank = GetTierRank(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            var baseCost = 0f;
            switch (staffRole)
            {
                case DepotStaffRole.Loader:
                    baseCost = 1800f;
                    break;
                case DepotStaffRole.Mechanic:
                    baseCost = 2600f;
                    break;
                case DepotStaffRole.Guard:
                    baseCost = 2200f;
                    break;
                default:
                    baseCost = 4200f;
                    break;
            }

            return baseCost + (tierRank * 350f) + (currentCount * (baseCost * 0.45f));
        }

        private static int ResolveMaxStaffCount(Industry industry, DepotStaffRole staffRole)
        {
            var tierRank = GetTierRank(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            switch (staffRole)
            {
                case DepotStaffRole.Manager:
                    return 1 + (tierRank >= 3 ? 1 : 0);
                case DepotStaffRole.Mechanic:
                    return 2 + (tierRank / 2);
                case DepotStaffRole.Guard:
                    return 2 + (tierRank / 2);
                default:
                    return 3 + tierRank;
            }
        }

        private static int GetStaffCount(TerritorySiteState siteState, DepotStaffRole staffRole)
        {
            switch (staffRole)
            {
                case DepotStaffRole.Loader:
                    return siteState.LoaderCount;
                case DepotStaffRole.Mechanic:
                    return siteState.MechanicCount;
                case DepotStaffRole.Guard:
                    return siteState.GuardCount;
                default:
                    return siteState.ManagerCount;
            }
        }

        private static void SetStaffCount(TerritorySiteState siteState, DepotStaffRole staffRole, int value)
        {
            switch (staffRole)
            {
                case DepotStaffRole.Loader:
                    siteState.LoaderCount = value;
                    break;
                case DepotStaffRole.Mechanic:
                    siteState.MechanicCount = value;
                    break;
                case DepotStaffRole.Guard:
                    siteState.GuardCount = value;
                    break;
                default:
                    siteState.ManagerCount = value;
                    break;
            }
        }

        private static float GetSupportSiteScore(Industry industry, TerritorySiteState siteState)
        {
            var score = GetTierWeight(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            score += siteState.LoaderCount + siteState.MechanicCount + siteState.GuardCount + siteState.ManagerCount;
            if (siteState.CrewAssigned)
            {
                score += 6f;
            }

            return score;
        }

        private static string BuildCorridorId(string districtA, string districtB)
        {
            var ordered = OrderDistrictPair(districtA, districtB);
            return ordered.Item1 + "|" + ordered.Item2;
        }

        private static Tuple<string, string> OrderDistrictPair(string districtA, string districtB)
        {
            var left = districtA ?? string.Empty;
            var right = districtB ?? string.Empty;
            return StringComparer.OrdinalIgnoreCase.Compare(left, right) <= 0
                ? Tuple.Create(left, right)
                : Tuple.Create(right, left);
        }
    }

    public sealed class TerritorySiteState
    {
        public string SiteId { get; set; }
        public string DistrictName { get; set; }
        public TerritoryControlLevel ControlLevel { get; set; }
        public bool CrewAssigned { get; set; }
        public int LoadRuns { get; set; }
        public int UnloadRuns { get; set; }
        public int TotalDeliveries { get; set; }
        public float TotalDeliveredTons { get; set; }
        public float TotalLoadedTons { get; set; }
        public TerritoryFranchiseLevel FranchiseLevel { get; set; }
        public int LoaderCount { get; set; }
        public int MechanicCount { get; set; }
        public int GuardCount { get; set; }
        public int ManagerCount { get; set; }
        public int Repossessions { get; set; }
        public int NpcLoads { get; set; }
        public int NpcDeliveries { get; set; }
        public string LastCommodity { get; set; }
        public bool HasSpawnRights { get; set; }
        public bool IsOperational { get; set; }
        public string ActivationSummary { get; set; }
    }

    public sealed class TerritoryDistrictState
    {
        public string DistrictName { get; set; }
        public int SiteCount { get; set; }
        public int ControlledSites { get; set; }
        public int OperationalSites { get; set; }
        public int ControlledDepots { get; set; }
        public int FranchiseSites { get; set; }
        public int RouteRights { get; set; }
        public float InfluenceScore { get; set; }
        public float InfluenceRatio { get; set; }
        public float ReputationScore { get; set; }
        public string ReputationLabel { get; set; }

        public void Reset()
        {
            SiteCount = 0;
            ControlledSites = 0;
            OperationalSites = 0;
            ControlledDepots = 0;
            FranchiseSites = 0;
            RouteRights = 0;
            InfluenceScore = 0f;
            InfluenceRatio = 0f;
            ReputationScore = 0f;
            ReputationLabel = string.Empty;
        }
    }

    public sealed class TerritoryCorridorState
    {
        public string CorridorId { get; set; }
        public string DistrictA { get; set; }
        public string DistrictB { get; set; }
        public int DeliveryCount { get; set; }
        public float TotalDeliveredTons { get; set; }
        public CorridorRightLevel RightLevel { get; set; }
    }

    public sealed class TerritoryPersistenceSnapshot
    {
        public TerritoryPersistenceSnapshot()
        {
            Sites = new List<TerritorySiteSnapshot>();
            Corridors = new List<TerritoryCorridorSnapshot>();
        }

        public List<TerritorySiteSnapshot> Sites { get; private set; }
        public List<TerritoryCorridorSnapshot> Corridors { get; private set; }
    }

    public sealed class TerritorySiteSnapshot
    {
        public string SiteId { get; set; }
        public TerritoryControlLevel ControlLevel { get; set; }
        public bool CrewAssigned { get; set; }
        public int LoadRuns { get; set; }
        public int UnloadRuns { get; set; }
        public int TotalDeliveries { get; set; }
        public float TotalDeliveredTons { get; set; }
        public float TotalLoadedTons { get; set; }
        public TerritoryFranchiseLevel FranchiseLevel { get; set; }
        public int LoaderCount { get; set; }
        public int MechanicCount { get; set; }
        public int GuardCount { get; set; }
        public int ManagerCount { get; set; }
        public int Repossessions { get; set; }
        public int NpcLoads { get; set; }
        public int NpcDeliveries { get; set; }
        public string LastCommodity { get; set; }
    }

    public sealed class TerritoryCorridorSnapshot
    {
        public string DistrictA { get; set; }
        public string DistrictB { get; set; }
        public int DeliveryCount { get; set; }
        public float TotalDeliveredTons { get; set; }
        public CorridorRightLevel RightLevel { get; set; }
    }
}