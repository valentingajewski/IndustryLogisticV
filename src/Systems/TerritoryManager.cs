using System;
using System.Collections.Generic;
using System.Drawing;
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

    public enum DistrictLicenseStatus
    {
        None = 0,
        Active = 1,
        Probation = 2,
        Suspended = 3,
    }

    public enum DepotSpecialization
    {
        None = 0,
        Dispatch = 1,
        Maintenance = 2,
        Security = 3,
        Support = 4,
    }

    public sealed class TerritoryManager
    {
        private const int RepossessionCooldownMs = 60000;
        private const int MinutesPerWeek = 7 * 24 * 60;
        private const float ServiceSinkPassiveIncomeStockThresholdTons = 0.05f;

        private readonly IndustryManager _industryManager;
        private readonly Dictionary<string, Industry> _industriesById;
        private readonly Dictionary<string, DistrictConfig> _districtConfigsByName;
        private readonly Dictionary<string, TerritorySiteState> _sitesById;
        private readonly Dictionary<string, TerritoryDistrictState> _districtsByName;
        private readonly Dictionary<string, TerritoryCorridorState> _corridorsById;
        private readonly Dictionary<string, float> _districtReputationDebugOffsets;
        private Func<CompanyEndgameSummary> _getEndgameSummary;
        private Func<IEnumerable<NpcDistrictCompetitionSummary>> _getDistrictCompetitionSummaries;
        private Func<IEnumerable<NpcCorridorCompetitionSummary>> _getCorridorCompetitionSummaries;

        private int _lastRepossessionEvaluationMs;
        private int _lastOperationsChargeWeekIndex;
        private int _lastMaintenanceWeekIndex;
        private bool _corridorRestrictionEnabled;
        private bool _reputationEnabled;

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
            _lastOperationsChargeWeekIndex = -1;
            _lastMaintenanceWeekIndex = -1;
            _corridorRestrictionEnabled = true;
            _reputationEnabled = true;

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

        public IReadOnlyList<TerritoryDistrictEventState> GetActiveDistrictEvents()
        {
            return _districtsByName.Values
                .Where(state => state != null && state.ActiveEvent != null && state.ActiveEvent.HasData)
                .Select(state => state.ActiveEvent)
                .OrderByDescending(state => state.Severity)
                .ThenBy(state => state.DistrictName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public TerritoryDistrictEventState GetDistrictEvent(string districtName)
        {
            var districtState = GetDistrictState(districtName);
            return districtState != null && districtState.ActiveEvent != null && districtState.ActiveEvent.HasData
                ? districtState.ActiveEvent
                : null;
        }

        public TerritoryDistrictEventState GetDistrictEventById(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return null;
            }

            var normalized = eventId.Trim();
            return _districtsByName.Values
                .Where(state => state != null && state.ActiveEvent != null)
                .Select(state => state.ActiveEvent)
                .FirstOrDefault(state => string.Equals(state.EventId, normalized, StringComparison.OrdinalIgnoreCase));
        }

        internal Dictionary<string, PointF> GetDistrictCentroidsByName()
        {
            var centroids = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _districtConfigsByName)
            {
                var districtName = pair.Key;
                var districtConfig = pair.Value;
                if (string.IsNullOrWhiteSpace(districtName)
                    || districtConfig == null
                    || districtConfig.PolygonVertices == null
                    || districtConfig.PolygonVertices.Count == 0)
                {
                    continue;
                }

                var centroid = districtConfig.GetCentroid();
                centroids[districtName] = new PointF(centroid.X, centroid.Y);
            }

            return centroids;
        }

        public void RefreshState()
        {
            RefreshComputedState();
        }

        public void SetCorridorRestrictionEnabled(bool enabled)
        {
            _corridorRestrictionEnabled = enabled;
        }

        public void SetReputationEnabled(bool enabled)
        {
            _reputationEnabled = enabled;
        }

        internal void ConfigureEndgameContext(
            Func<CompanyEndgameSummary> getEndgameSummary,
            Func<IEnumerable<NpcDistrictCompetitionSummary>> getDistrictCompetitionSummaries,
            Func<IEnumerable<NpcCorridorCompetitionSummary>> getCorridorCompetitionSummaries)
        {
            _getEndgameSummary = getEndgameSummary;
            _getDistrictCompetitionSummaries = getDistrictCompetitionSummaries;
            _getCorridorCompetitionSummaries = getCorridorCompetitionSummaries;
        }

        public void Reset()
        {
            _industriesById.Clear();
            _sitesById.Clear();
            _corridorsById.Clear();
            _districtReputationDebugOffsets.Clear();
            _lastOperationsChargeWeekIndex = -1;
            _lastMaintenanceWeekIndex = -1;

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

        public bool HasServiceSiteOperatorAssigned(Industry industry)
        {
            var siteState = GetSiteState(industry);
            return siteState != null && siteState.SiteOperatorAssigned;
        }

        public bool IsServiceSinkStockedForPassiveIncome(Industry industry)
        {
            return IsServiceSinkStocked(industry);
        }

        public bool IsServiceSinkOperationalForPassiveIncome(Industry industry)
        {
            var siteState = GetSiteState(industry);
            return ShouldAwardServiceSinkPassiveIncome(industry, siteState);
        }

        public float GetServiceSiteWeeklyStaffingCost(Industry industry)
        {
            return ComputeServiceSiteWeeklyStaffingCost(industry);
        }

        public bool TrySetServiceSiteOperatorAssigned(Industry industry, bool assigned, out string result)
        {
            if (industry == null)
            {
                result = "No site selected.";
                return false;
            }

            if (!IsServiceSink(industry))
            {
                result = "Site operators are only used at owned stores and gas stations.";
                return false;
            }

            if (!industry.IsOwned)
            {
                result = string.Format("Buy {0} before assigning a site operator.", industry.Name);
                return false;
            }

            var siteState = GetSiteState(industry);
            if (siteState == null)
            {
                result = "Site state unavailable.";
                return false;
            }

            if (siteState.SiteOperatorAssigned == assigned)
            {
                result = assigned
                    ? string.Format("{0} already has a site operator assigned.", industry.Name)
                    : string.Format("{0} already has no site operator assigned.", industry.Name);
                return false;
            }

            siteState.SiteOperatorAssigned = assigned;
            RefreshComputedState();
            result = assigned
                ? string.Format("Assigned a site operator to {0}. Weekly wage {1:0}.", industry.Name, ComputeServiceSiteWeeklyStaffingCost(industry))
                : string.Format("Released the site operator at {0}.", industry.Name);
            return true;
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

        public bool HasActiveCorridorBetween(string districtA, string districtB)
        {
            var corridorState = GetCorridorState(districtA, districtB);
            return corridorState != null && corridorState.RightLevel != CorridorRightLevel.None;
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

        public int ApplyDistrictReputationDebugStateToAll(string reputationLabel)
        {
            float targetScore;
            if (!TryResolveDistrictDebugTargetScore(reputationLabel, out targetScore))
            {
                return 0;
            }

            var updatedDistrictCount = 0;
            foreach (var districtState in _districtsByName.Values)
            {
                if (districtState == null || string.IsNullOrWhiteSpace(districtState.DistrictName))
                {
                    continue;
                }

                var normalizedName = districtState.DistrictName.Trim();
                var currentOffset = GetDistrictReputationDebugOffset(normalizedName);
                var baseReputationScore = districtState.ReputationScore - currentOffset;
                var requiredOffset = targetScore - (districtState.InfluenceRatio * 100f) - baseReputationScore;
                if (Math.Abs(requiredOffset) <= 0.001f)
                {
                    _districtReputationDebugOffsets.Remove(normalizedName);
                }
                else
                {
                    _districtReputationDebugOffsets[normalizedName] = requiredOffset;
                }

                updatedDistrictCount += 1;
            }

            RefreshComputedState();
            return updatedDistrictCount;
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

        public TerritoryOperationsSummary GetOperationsSummary()
        {
            var summary = new TerritoryOperationsSummary
            {
                ControlledDistrictCount = GetControlledDistrictCount(),
            };
            var districtsByName = new Dictionary<string, TerritoryDistrictOperationsEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var districtState in _districtsByName.Values)
            {
                if (districtState == null || string.IsNullOrWhiteSpace(districtState.DistrictName))
                {
                    continue;
                }

                var administrationCost = GetDistrictAdministrationCost(districtState, DistrictHasStarterHeadquarters(districtState.DistrictName));
                if (administrationCost <= 0.01f && districtState.LicenseStatus != DistrictLicenseStatus.Suspended)
                {
                    continue;
                }

                var entry = EnsureDistrictOperationsEntry(districtsByName, districtState.DistrictName);
                entry.LicenseStatus = districtState.LicenseStatus;
                entry.LicenseActivityTons = Math.Max(0f, districtState.CurrentWeekActivityTons);
                entry.LicenseTargetTons = Math.Max(0f, districtState.RequiredWeeklyActivityTons);
                if (administrationCost > 0.01f)
                {
                    entry.AdministrationCost += administrationCost;
                    summary.ManagedDistrictCount += 1;
                    summary.ActiveLicensedDistrictCount += 1;
                    summary.CharterCost += administrationCost;
                    summary.WeeklyCost += administrationCost;
                }

                if (districtState.LicenseStatus == DistrictLicenseStatus.Probation)
                {
                    summary.ProbationDistrictCount += 1;
                }
                else if (districtState.LicenseStatus == DistrictLicenseStatus.Suspended)
                {
                    summary.SuspendedDistrictCount += 1;
                }
            }

            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null || string.IsNullOrWhiteSpace(siteState.DistrictName))
                {
                    continue;
                }

                var industry = ResolveIndustry(siteState.SiteId);
                if (industry == null)
                {
                    continue;
                }

                var entry = EnsureDistrictOperationsEntry(districtsByName, siteState.DistrictName);
                if (IsSupportSite(industry) && !industry.IsStarterHeadquarters && siteState.ControlLevel != TerritoryControlLevel.None)
                {
                    var supportSiteCost = GetSupportSiteOperationsCost(industry, siteState);
                    if (supportSiteCost > 0.01f)
                    {
                        entry.SupportSiteCost += supportSiteCost;
                        summary.SupportSiteCount += 1;
                        summary.InfrastructureCost += supportSiteCost;
                        summary.WeeklyCost += supportSiteCost;
                    }

                    var staffCost = GetSupportStaffOperationsCost(siteState);
                    var staffCount = GetTotalStaffCount(siteState);
                    if (staffCost > 0.01f && staffCount > 0)
                    {
                        entry.StaffCost += staffCost;
                        entry.StaffCount += staffCount;
                        summary.StaffCount += staffCount;
                        summary.InfrastructureCost += staffCost;
                        summary.WeeklyCost += staffCost;
                    }
                }

                if (IsServiceSink(industry))
                {
                    var franchiseCost = GetFranchiseOperationsCost(siteState.EffectiveFranchiseLevel);
                    if (franchiseCost > 0.01f)
                    {
                        entry.FranchiseCost += franchiseCost;
                        entry.FranchiseSites += 1;
                        summary.PremiumFranchiseCount += 1;
                        summary.InfrastructureCost += franchiseCost;
                        summary.WeeklyCost += franchiseCost;
                    }

                    if (IsServiceContractAtRisk(siteState))
                    {
                        entry.ServiceRiskCount += 1;
                        summary.AtRiskServiceSiteCount += 1;
                    }
                }
            }

            foreach (var corridorState in _corridorsById.Values)
            {
                if (corridorState == null || corridorState.RightLevel == CorridorRightLevel.None)
                {
                    continue;
                }

                var corridorCost = GetCorridorOperationsCost(corridorState.RightLevel);
                if (corridorCost <= 0.01f)
                {
                    continue;
                }

                var splitCost = corridorCost * 0.5f;
                EnsureDistrictOperationsEntry(districtsByName, corridorState.DistrictA).CorridorCost += splitCost;
                EnsureDistrictOperationsEntry(districtsByName, corridorState.DistrictB).CorridorCost += splitCost;
                summary.ActiveCorridorCount += 1;
                summary.InfrastructureCost += corridorCost;
                summary.WeeklyCost += corridorCost;

                if (IsCorridorAtRisk(corridorState))
                {
                    EnsureDistrictOperationsEntry(districtsByName, corridorState.DistrictA).CorridorRiskCount += 1;
                    EnsureDistrictOperationsEntry(districtsByName, corridorState.DistrictB).CorridorRiskCount += 1;
                    summary.AtRiskCorridorCount += 1;
                }
            }

            summary.ChargedDistrictCount = districtsByName.Values.Count(entry => entry != null && entry.TotalWeeklyCost > 0.01f);
            foreach (var entry in districtsByName.Values
                .Where(entry => entry != null && entry.TotalWeeklyCost > 0.01f)
                .OrderByDescending(entry => entry.TotalWeeklyCost)
                .ThenBy(entry => entry.DistrictName, StringComparer.OrdinalIgnoreCase))
            {
                summary.Districts.Add(entry);
            }

            return summary;
        }

        public bool IsDistrictLicensable(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName) || DistrictHasStarterHeadquarters(districtName))
            {
                return false;
            }

            var districtState = GetDistrictState(districtName);
            return districtState != null && GetReputationTier(districtState.ReputationLabel) >= 2;
        }

        public float GetDistrictLicenseEnrollmentCost(string districtName)
        {
            var districtState = GetDistrictState(districtName);
            if (districtState == null)
            {
                return 0f;
            }

            var cost = 18000f + (districtState.ControlledSites * 4000f) + (districtState.RouteRights * 1200f) + (districtState.ControlledDepots * 2500f);
            if (GetReputationTier(districtState.ReputationLabel) >= 3)
            {
                cost += 6000f;
            }

            return cost;
        }

        public bool TryAcquireDistrictLicense(string districtName, ref float profit, out float cost, out string result)
        {
            cost = 0f;
            result = string.Empty;
            if (string.IsNullOrWhiteSpace(districtName))
            {
                result = "Select a district first.";
                return false;
            }

            var districtState = GetDistrictState(districtName);
            if (districtState == null)
            {
                result = "District strategic data is unavailable.";
                return false;
            }

            if (DistrictHasStarterHeadquarters(districtName))
            {
                result = string.Format("{0} is already covered by headquarters operations.", districtName);
                return false;
            }

            if (!IsDistrictLicensable(districtName))
            {
                result = string.Format("{0} must reach Established before you can charter it.", districtName);
                return false;
            }

            if (districtState.LicenseStatus == DistrictLicenseStatus.Active || districtState.LicenseStatus == DistrictLicenseStatus.Probation)
            {
                result = string.Format("{0} already has an operating charter.", districtName);
                return false;
            }

            cost = GetDistrictLicenseEnrollmentCost(districtName);
            if (districtState.LicenseStatus == DistrictLicenseStatus.Suspended)
            {
                cost *= 0.7f;
            }

            if (profit < cost)
            {
                result = string.Format("Need {0} more to charter {1}.", ModFormatting.FormatMoney(cost - profit), districtName);
                return false;
            }

            profit -= cost;
            districtState.LicenseStatus = DistrictLicenseStatus.Active;
            districtState.LicenseStrikeCount = 0;
            RefreshComputedState();

            result = string.Format("Chartered {0} for {1}. Weekly compliance and licence fees now apply.", districtName, ModFormatting.FormatMoney(cost));
            return true;
        }

        public TerritoryOperationsChargeResult ProcessWeeklyOperationsCharges(int currentInGameMinute)
        {
            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            if (_lastOperationsChargeWeekIndex < 0)
            {
                _lastOperationsChargeWeekIndex = currentWeekIndex;
                return null;
            }

            var dueWeekCount = currentWeekIndex - _lastOperationsChargeWeekIndex;
            if (dueWeekCount <= 0)
            {
                return null;
            }

            _lastOperationsChargeWeekIndex = currentWeekIndex;
            var summary = GetOperationsSummary();
            if (summary == null || summary.WeeklyCost <= 0.01f)
            {
                return null;
            }

            return new TerritoryOperationsChargeResult
            {
                ChargeCount = dueWeekCount,
                WeeklyAmount = summary.WeeklyCost,
                TotalAmount = summary.WeeklyCost * dueWeekCount,
                Summary = summary,
            };
        }

        public TerritoryWeeklyMaintenanceResult ProcessWeeklyMaintenance(int currentInGameMinute)
        {
            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            if (_lastMaintenanceWeekIndex < 0)
            {
                _lastMaintenanceWeekIndex = currentWeekIndex;
                return null;
            }

            var dueWeekCount = currentWeekIndex - _lastMaintenanceWeekIndex;
            if (dueWeekCount <= 0)
            {
                return null;
            }

            var result = new TerritoryWeeklyMaintenanceResult();
            for (int weekIndex = _lastMaintenanceWeekIndex + 1; weekIndex <= currentWeekIndex; weekIndex++)
            {
                ProcessCompetitionPressure(result, weekIndex);
                ProcessDistrictLicenseMaintenance(result, weekIndex);
                ProcessServiceFranchiseMaintenance(result, weekIndex);
                ProcessCorridorMaintenance(result, weekIndex);
                ProcessDistrictEvents(result, weekIndex);
                ResetWeeklyTracking();
                result.ProcessedWeekCount += 1;
            }

            _lastMaintenanceWeekIndex = currentWeekIndex;
            RefreshComputedState();
            return result;
        }

        public int GetRemainingOperationsChargeMinutes(int currentInGameMinute)
        {
            var currentWeekIndex = GetWeekIndex(currentInGameMinute);
            var dueWeekIndex = _lastOperationsChargeWeekIndex < 0
                ? currentWeekIndex + 1
                : Math.Max(currentWeekIndex, _lastOperationsChargeWeekIndex + 1);
            return Math.Max(0, (dueWeekIndex * MinutesPerWeek) - Math.Max(0, currentInGameMinute));
        }

        public DepotSpecialization GetDepotSpecialization(Industry industry)
        {
            var siteState = GetSiteState(industry);
            return siteState != null ? siteState.DepotSpecialization : DepotSpecialization.None;
        }

        public bool TrySetDepotSpecialization(Industry industry, DepotSpecialization specialization, ref float profit, out float cost, out string result)
        {
            cost = 0f;
            result = string.Empty;
            if (industry == null || !IsSupportSite(industry))
            {
                result = "Specialization is only available for depots and yards.";
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
                result = "Assign a base crew before specializing this support site.";
                return false;
            }

            if (siteState.ControlLevel == TerritoryControlLevel.None && !industry.IsStarterHeadquarters)
            {
                result = "Secure the site before assigning a specialization.";
                return false;
            }

            if (siteState.DepotSpecialization == specialization)
            {
                result = string.Format("{0} is already set to {1}.", industry.Name, FormatDepotSpecialization(specialization));
                return false;
            }

            cost = siteState.DepotSpecialization == DepotSpecialization.None || specialization == DepotSpecialization.None
                ? 0f
                : GetDepotSpecializationSwapCost(industry);
            if (profit < cost)
            {
                result = string.Format("Need {0} more to refit {1}.", ModFormatting.FormatMoney(cost - profit), industry.Name);
                return false;
            }

            profit -= cost;
            siteState.DepotSpecialization = specialization;
            RefreshComputedState();

            result = specialization == DepotSpecialization.None
                ? string.Format("Cleared specialization at {0}.", industry.Name)
                : string.Format(
                    "Assigned {0} specialization at {1}{2}.",
                    FormatDepotSpecialization(specialization),
                    industry.Name,
                    cost > 0.01f ? string.Format(" for {0}", ModFormatting.FormatMoney(cost)) : string.Empty);
            return true;
        }

        public string GetDepotSpecializationEffectSummary(Industry industry)
        {
            var siteState = GetSiteState(industry);
            if (industry == null || siteState == null)
            {
                return string.Empty;
            }

            switch (siteState.DepotSpecialization)
            {
                case DepotSpecialization.Dispatch:
                    return "Dispatch: stronger delivery returns in this district and softer corridor upkeep targets.";
                case DepotSpecialization.Maintenance:
                    return "Maintenance: reduces route wear and loss exposure for district traffic.";
                case DepotSpecialization.Security:
                    return "Security: lowers cargo-loss pressure and helps corridors resist neglect.";
                case DepotSpecialization.Support:
                    return "Support: amplifies district support bonuses and stabilizes licensed territory.";
                default:
                    return "No specialization selected. Use left/right to assign a district role.";
            }
        }

        public bool IsDistrictEstablishedForNpc(string districtName)
        {
            if (!_reputationEnabled)
            {
                return true;
            }

            var districtState = GetDistrictState(districtName);
            return districtState != null && GetReputationTier(districtState.ReputationLabel) >= 2;
        }

        public int GetDistrictReputationTier(string districtName)
        {
            if (!_reputationEnabled)
            {
                return 4;
            }

            var districtState = GetDistrictState(districtName);
            return districtState != null
                ? GetReputationTier(districtState.ReputationLabel)
                : 0;
        }

        public string GetNpcDistrictRequirementSummary(string districtName)
        {
            var districtState = GetDistrictState(districtName);
            if (districtState == null)
            {
                return "District influence data is unavailable.";
            }

            if (!_reputationEnabled)
            {
                return string.Empty;
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

            var loadTons = Math.Max(0f, tons);
            siteState.LoadRuns += 1;
            siteState.TotalLoadedTons += loadTons;
            siteState.LastCommodity = CommodityCatalog.Normalize(commodity);
            if (viaNpc)
            {
                siteState.NpcLoads += 1;
            }

            var districtState = EnsureDistrictState(originIndustry.DistrictName);
            districtState.CurrentWeekActivityCount += 1;
            districtState.CurrentWeekActivityTons += loadTons;

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

            var deliveredTons = Math.Max(0f, tons);
            siteState.UnloadRuns += 1;
            siteState.TotalDeliveries += 1;
            siteState.TotalDeliveredTons += deliveredTons;
            siteState.LastCommodity = CommodityCatalog.Normalize(commodity);
            if (viaNpc)
            {
                siteState.NpcDeliveries += 1;
            }

            var districtState = EnsureDistrictState(destinationIndustry.DistrictName);
            districtState.CurrentWeekActivityCount += 1;
            districtState.CurrentWeekActivityTons += deliveredTons;
            if (districtState.CompetitivePressure > 0.001f)
            {
                var responseRelief = Math.Min(0.08f, deliveredTons * 0.004f);
                districtState.CompetitivePressure = Math.Max(0f, districtState.CompetitivePressure - responseRelief);
                districtState.CompetitiveOpportunity = Math.Max(0f, districtState.CompetitiveOpportunity - (responseRelief * 0.35f));
            }

            if (IsServiceSink(destinationIndustry))
            {
                siteState.FranchiseLevel = ResolveFranchiseLevel(siteState.TotalDeliveries, siteState.TotalDeliveredTons);
                siteState.CurrentWeekServiceDeliveries += 1;
                siteState.CurrentWeekServiceTons += deliveredTons;
            }

            ApplyDistrictEventDeliveryProgress(districtState, destinationIndustry, commodity, deliveredTons, viaNpc);

            var resolvedOriginDistrict = ResolveOriginDistrict(originIndustryId, originDistrictName);
            if (!string.IsNullOrWhiteSpace(resolvedOriginDistrict)
                && !string.IsNullOrWhiteSpace(destinationIndustry.DistrictName)
                && !string.Equals(resolvedOriginDistrict, destinationIndustry.DistrictName, StringComparison.OrdinalIgnoreCase))
            {
                var corridorState = GetOrCreateCorridorState(resolvedOriginDistrict, destinationIndustry.DistrictName);
                if (corridorState.CompetitivePressure > 0.001f)
                {
                    var responseRelief = Math.Min(0.10f, deliveredTons * 0.005f);
                    corridorState.CompetitivePressure = Math.Max(0f, corridorState.CompetitivePressure - responseRelief);
                    corridorState.CompetitiveOpportunity = Math.Max(0f, corridorState.CompetitiveOpportunity - (responseRelief * 0.3f));
                }

                corridorState.CurrentWeekDeliveryCount += 1;
                corridorState.CurrentWeekDeliveredTons += deliveredTons;
                if (!viaNpc)
                {
                    corridorState.DeliveryCount += 1;
                    corridorState.TotalDeliveredTons += deliveredTons;
                    corridorState.RightLevel = ResolveCorridorLevel(corridorState.DeliveryCount, corridorState.TotalDeliveredTons);
                }
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
            var endgame = GetEndgameSummary();
            var siteState = GetSiteState(destinationIndustry);
            if (siteState != null)
            {
                if (!siteState.IsOperational)
                {
                    multiplier *= 0.88f;
                }

                if (IsServiceSink(destinationIndustry))
                {
                    multiplier += 0.035f * (int)siteState.EffectiveFranchiseLevel;
                    if (siteState.ServiceTargetMetLastWeek)
                    {
                        multiplier += Math.Min(0.025f, siteState.ServiceSuccessStreak * 0.005f);
                    }

                    if (siteState.ServicePenaltySteps > 0)
                    {
                        multiplier -= Math.Min(0.045f, siteState.ServicePenaltySteps * 0.015f);
                    }
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
                multiplier += GetDistrictDispatchBonus(destinationIndustry.DistrictName);
                if (districtState.LicenseStatus == DistrictLicenseStatus.Active)
                {
                    multiplier += 0.05f;
                }
                else if (districtState.LicenseStatus == DistrictLicenseStatus.Probation)
                {
                    multiplier += 0.02f;
                }

                multiplier += Math.Min(0.12f, districtState.CompetitiveOpportunity * 0.12f);
            }

            if (endgame.ActiveDoctrine == CompanyDoctrine.Industrial
                && endgame.ActiveDoctrineEffectiveTier > 0
                && destinationIndustry.IsOwned
                && (destinationIndustry.IsWarehouse || destinationIndustry.RequiresPurchase))
            {
                multiplier += CompanyDoctrineSystem.GetIndustrialRevenueBonus(endgame.ActiveDoctrineEffectiveTier);
            }

            if (endgame.ActiveDoctrine == CompanyDoctrine.Service
                && endgame.ActiveDoctrineEffectiveTier > 0
                && IsServiceSink(destinationIndustry))
            {
                multiplier += CompanyDoctrineSystem.GetServiceRevenueBonus(endgame.ActiveDoctrineEffectiveTier);
            }

            if (endgame.HasLandmarkHeadquarters)
            {
                multiplier += 0.02f;
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
            var endgame = GetEndgameSummary();

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
                    if (corridorState.DecayPressure >= GetCorridorDecayThreshold(corridorState) * 0.75f)
                    {
                        adjusted *= 1.06f;
                    }
                }

                adjusted *= 1f - Math.Min(0.18f, GetDistrictSupportFactor(originIndustry.DistrictName) + GetDistrictSupportFactor(destinationIndustry.DistrictName));
                adjusted *= 1f - Math.Min(0.12f, GetDistrictMaintenanceBonus(originIndustry.DistrictName) + GetDistrictMaintenanceBonus(destinationIndustry.DistrictName));
                adjusted *= 1f - Math.Min(0.1f, GetDistrictSecurityBonus(originIndustry.DistrictName) + GetDistrictSecurityBonus(destinationIndustry.DistrictName));
                adjusted *= 1f + Math.Min(0.08f, (GetDistrictCompetitionPressureValue(originIndustry.DistrictName) + GetDistrictCompetitionPressureValue(destinationIndustry.DistrictName)) * 0.04f);

                if (endgame.ActiveDoctrine == CompanyDoctrine.Industrial && endgame.ActiveDoctrineEffectiveTier > 0)
                {
                    adjusted *= Math.Max(0.75f, 1f - CompanyDoctrineSystem.GetIndustrialLossMitigation(endgame.ActiveDoctrineEffectiveTier));
                }

                if (endgame.HasLandmarkHeadquarters)
                {
                    adjusted *= 0.97f;
                }

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

            var licensedDistrict = _districtsByName.Values
                .Where(x => x != null && (x.LicenseStatus == DistrictLicenseStatus.Active || x.LicenseStatus == DistrictLicenseStatus.Probation))
                .OrderByDescending(x => x.RequiredWeeklyActivityTons)
                .ThenByDescending(x => x.InfluenceRatio)
                .FirstOrDefault();
            if (licensedDistrict != null)
            {
                licensedDistrict.LicenseStatus = DistrictLicenseStatus.Suspended;
                licensedDistrict.LicenseStrikeCount = Math.Max(licensedDistrict.LicenseStrikeCount, 2);
                RefreshComputedState();

                if (showStatus != null)
                {
                    showStatus(string.Format("Financial pressure: the operating charter in {0} has been suspended.", licensedDistrict.DistrictName));
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
            snapshot.LastOperationsChargeWeekIndex = _lastOperationsChargeWeekIndex;
            snapshot.LastMaintenanceWeekIndex = _lastMaintenanceWeekIndex;

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
                    || siteState.Repossessions > 0
                    || siteState.DepotSpecialization != DepotSpecialization.None
                    || siteState.CurrentWeekServiceDeliveries > 0
                    || siteState.CurrentWeekServiceTons > 0.001f
                    || siteState.ServicePenaltySteps > 0
                    || siteState.ServiceSuccessStreak > 0
                    || siteState.ServiceTargetMetLastWeek
                    || siteState.SiteOperatorAssigned
                    || siteState.LastPassiveIncomeAmount > 0.001f
                    || siteState.LastPassiveIncomeWeekIndex >= 0
                    || !string.IsNullOrWhiteSpace(siteState.LastPassiveIncomeStatus);
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
                    DepotSpecialization = siteState.DepotSpecialization,
                    CurrentWeekServiceDeliveries = siteState.CurrentWeekServiceDeliveries,
                    CurrentWeekServiceTons = siteState.CurrentWeekServiceTons,
                    ServicePenaltySteps = siteState.ServicePenaltySteps,
                    ServiceSuccessStreak = siteState.ServiceSuccessStreak,
                    ServiceTargetMetLastWeek = siteState.ServiceTargetMetLastWeek,
                    SiteOperatorAssigned = siteState.SiteOperatorAssigned,
                    LastPassiveIncomeAmount = siteState.LastPassiveIncomeAmount,
                    LastPassiveIncomeWeekIndex = siteState.LastPassiveIncomeWeekIndex,
                    LastPassiveIncomeStatus = siteState.LastPassiveIncomeStatus,
                });
            }

            foreach (var districtState in _districtsByName.Values.OrderBy(x => x.DistrictName, StringComparer.OrdinalIgnoreCase))
            {
                if (districtState == null)
                {
                    continue;
                }

                var hasState = districtState.LicenseStatus != DistrictLicenseStatus.None
                    || districtState.LicenseStrikeCount > 0
                    || districtState.CurrentWeekActivityCount > 0
                    || districtState.CurrentWeekActivityTons > 0.001f
                    || districtState.CompetitivePressure > 0.001f
                    || districtState.CompetitiveOpportunity > 0.001f
                    || districtState.ActiveCompetitionJobs > 0
                    || districtState.ActiveCarrierCount > 0
                    || !string.IsNullOrWhiteSpace(districtState.DominantCarrierId)
                    || !string.IsNullOrWhiteSpace(districtState.DominantCarrierName)
                    || districtState.VisibleCompetitionCount > 0
                    || districtState.LastCompetitiveTons > 0.001f
                    || districtState.CompetitiveResponseCount > 0
                    || districtState.CompetitiveWinCount > 0
                    || (districtState.ActiveEvent != null && districtState.ActiveEvent.HasData);
                if (!hasState)
                {
                    continue;
                }

                snapshot.Districts.Add(new TerritoryDistrictSnapshot
                {
                    DistrictName = districtState.DistrictName,
                    LicenseStatus = districtState.LicenseStatus,
                    LicenseStrikeCount = districtState.LicenseStrikeCount,
                    CurrentWeekActivityCount = districtState.CurrentWeekActivityCount,
                    CurrentWeekActivityTons = districtState.CurrentWeekActivityTons,
                    CompetitivePressure = districtState.CompetitivePressure,
                    CompetitiveOpportunity = districtState.CompetitiveOpportunity,
                    ActiveCompetitionJobs = districtState.ActiveCompetitionJobs,
                    ActiveCarrierCount = districtState.ActiveCarrierCount,
                    DominantCarrierId = districtState.DominantCarrierId,
                    DominantCarrierName = districtState.DominantCarrierName,
                    VisibleCompetitionCount = districtState.VisibleCompetitionCount,
                    CompetitiveTons = districtState.LastCompetitiveTons,
                    CompetitiveResponseCount = districtState.CompetitiveResponseCount,
                    CompetitiveWinCount = districtState.CompetitiveWinCount,
                    ActiveEvent = districtState.ActiveEvent != null && districtState.ActiveEvent.HasData
                        ? new TerritoryDistrictEventSnapshot
                        {
                            EventId = districtState.ActiveEvent.EventId,
                            DistrictName = districtState.ActiveEvent.DistrictName,
                            CrisisType = districtState.ActiveEvent.CrisisType,
                            PreferredCommodity = districtState.ActiveEvent.PreferredCommodity,
                            Severity = districtState.ActiveEvent.Severity,
                            MarketPressureBonus = districtState.ActiveEvent.MarketPressureBonus,
                            ResponseTargetTons = districtState.ActiveEvent.ResponseTargetTons,
                            DeliveredReliefTons = districtState.ActiveEvent.DeliveredReliefTons,
                            ReliefDeliveryCount = districtState.ActiveEvent.ReliefDeliveryCount,
                            StartedWeekIndex = districtState.ActiveEvent.StartedWeekIndex,
                            EndsAtWeekIndex = districtState.ActiveEvent.EndsAtWeekIndex,
                            LastEscalatedWeekIndex = districtState.ActiveEvent.LastEscalatedWeekIndex,
                            TriggerSummary = districtState.ActiveEvent.TriggerSummary,
                            ImpactSummary = districtState.ActiveEvent.ImpactSummary,
                        }
                        : null,
                });
            }

            foreach (var corridorState in _corridorsById.Values.OrderBy(x => x.CorridorId, StringComparer.OrdinalIgnoreCase))
            {
                if (corridorState == null)
                {
                    continue;
                }

                var hasState = corridorState.DeliveryCount > 0
                    || corridorState.RightLevel != CorridorRightLevel.None
                    || corridorState.CurrentWeekDeliveryCount > 0
                    || corridorState.CurrentWeekDeliveredTons > 0.001f
                    || corridorState.DecayPressure > 0.001f
                    || corridorState.CompetitivePressure > 0.001f
                    || corridorState.CompetitiveOpportunity > 0.001f
                    || corridorState.ActiveCompetitionJobs > 0
                    || corridorState.ActiveCarrierCount > 0
                    || !string.IsNullOrWhiteSpace(corridorState.DominantCarrierId)
                    || !string.IsNullOrWhiteSpace(corridorState.DominantCarrierName)
                    || corridorState.VisibleCompetitionCount > 0
                    || corridorState.LastCompetitiveTons > 0.001f
                    || corridorState.CompetitiveWinCount > 0
                    || corridorState.ContestedWeekStreak > 0;
                if (!hasState)
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
                    CurrentWeekDeliveryCount = corridorState.CurrentWeekDeliveryCount,
                    CurrentWeekDeliveredTons = corridorState.CurrentWeekDeliveredTons,
                    DecayPressure = corridorState.DecayPressure,
                    CompetitivePressure = corridorState.CompetitivePressure,
                    CompetitiveOpportunity = corridorState.CompetitiveOpportunity,
                    ActiveCompetitionJobs = corridorState.ActiveCompetitionJobs,
                    ActiveCarrierCount = corridorState.ActiveCarrierCount,
                    DominantCarrierId = corridorState.DominantCarrierId,
                    DominantCarrierName = corridorState.DominantCarrierName,
                    VisibleCompetitionCount = corridorState.VisibleCompetitionCount,
                    CompetitiveTons = corridorState.LastCompetitiveTons,
                    CompetitiveWinCount = corridorState.CompetitiveWinCount,
                    ContestedWeekStreak = corridorState.ContestedWeekStreak,
                });
            }

            foreach (var debugOffset in _districtReputationDebugOffsets.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(debugOffset.Key) || Math.Abs(debugOffset.Value) <= 0.001f)
                {
                    continue;
                }

                snapshot.DistrictReputationOffsets.Add(new TerritoryDistrictReputationOffsetSnapshot
                {
                    DistrictName = debugOffset.Key,
                    Offset = debugOffset.Value,
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

            _lastOperationsChargeWeekIndex = snapshot.LastOperationsChargeWeekIndex;
            _lastMaintenanceWeekIndex = snapshot.LastMaintenanceWeekIndex;

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
                target.DepotSpecialization = source.DepotSpecialization;
                target.CurrentWeekServiceDeliveries = Math.Max(0, source.CurrentWeekServiceDeliveries);
                target.CurrentWeekServiceTons = Math.Max(0f, source.CurrentWeekServiceTons);
                target.ServicePenaltySteps = Math.Max(0, source.ServicePenaltySteps);
                target.ServiceSuccessStreak = Math.Max(0, source.ServiceSuccessStreak);
                target.ServiceTargetMetLastWeek = source.ServiceTargetMetLastWeek;
                target.SiteOperatorAssigned = source.SiteOperatorAssigned;
                target.LastPassiveIncomeAmount = Math.Max(0f, source.LastPassiveIncomeAmount);
                target.LastPassiveIncomeWeekIndex = source.LastPassiveIncomeWeekIndex;
                target.LastPassiveIncomeStatus = source.LastPassiveIncomeStatus ?? string.Empty;
            }

            for (int i = 0; i < snapshot.Districts.Count; i++)
            {
                var source = snapshot.Districts[i];
                if (source == null || string.IsNullOrWhiteSpace(source.DistrictName))
                {
                    continue;
                }

                var districtState = EnsureDistrictState(source.DistrictName);
                districtState.LicenseStatus = source.LicenseStatus;
                districtState.LicenseStrikeCount = Math.Max(0, source.LicenseStrikeCount);
                districtState.CurrentWeekActivityCount = Math.Max(0, source.CurrentWeekActivityCount);
                districtState.CurrentWeekActivityTons = Math.Max(0f, source.CurrentWeekActivityTons);
                districtState.CompetitivePressure = Math.Max(0f, source.CompetitivePressure);
                districtState.CompetitiveOpportunity = Math.Max(0f, source.CompetitiveOpportunity);
                districtState.ActiveCompetitionJobs = Math.Max(0, source.ActiveCompetitionJobs);
                districtState.ActiveCarrierCount = Math.Max(0, source.ActiveCarrierCount);
                districtState.DominantCarrierId = source.DominantCarrierId ?? string.Empty;
                districtState.DominantCarrierName = source.DominantCarrierName ?? string.Empty;
                districtState.VisibleCompetitionCount = Math.Max(0, source.VisibleCompetitionCount);
                districtState.LastCompetitiveTons = Math.Max(0f, source.CompetitiveTons);
                districtState.CompetitiveResponseCount = Math.Max(0, source.CompetitiveResponseCount);
                districtState.CompetitiveWinCount = Math.Max(0, source.CompetitiveWinCount);
                if (source.ActiveEvent != null && source.ActiveEvent.HasData)
                {
                    districtState.ActiveEvent = new TerritoryDistrictEventState
                    {
                        EventId = source.ActiveEvent.EventId ?? string.Empty,
                        DistrictName = string.IsNullOrWhiteSpace(source.ActiveEvent.DistrictName) ? source.DistrictName : source.ActiveEvent.DistrictName,
                        CrisisType = source.ActiveEvent.CrisisType,
                        PreferredCommodity = CommodityCatalog.Normalize(source.ActiveEvent.PreferredCommodity),
                        Severity = Clamp01(source.ActiveEvent.Severity),
                        MarketPressureBonus = Math.Max(0f, source.ActiveEvent.MarketPressureBonus),
                        ResponseTargetTons = Math.Max(0f, source.ActiveEvent.ResponseTargetTons),
                        DeliveredReliefTons = Math.Max(0f, source.ActiveEvent.DeliveredReliefTons),
                        ReliefDeliveryCount = Math.Max(0, source.ActiveEvent.ReliefDeliveryCount),
                        StartedWeekIndex = source.ActiveEvent.StartedWeekIndex,
                        EndsAtWeekIndex = source.ActiveEvent.EndsAtWeekIndex,
                        LastEscalatedWeekIndex = source.ActiveEvent.LastEscalatedWeekIndex,
                        TriggerSummary = source.ActiveEvent.TriggerSummary ?? string.Empty,
                        ImpactSummary = source.ActiveEvent.ImpactSummary ?? string.Empty,
                    };
                }
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
                corridorState.CurrentWeekDeliveryCount = Math.Max(0, source.CurrentWeekDeliveryCount);
                corridorState.CurrentWeekDeliveredTons = Math.Max(0f, source.CurrentWeekDeliveredTons);
                corridorState.DecayPressure = Math.Max(0f, source.DecayPressure);
                corridorState.CompetitivePressure = Math.Max(0f, source.CompetitivePressure);
                corridorState.CompetitiveOpportunity = Math.Max(0f, source.CompetitiveOpportunity);
                corridorState.ActiveCompetitionJobs = Math.Max(0, source.ActiveCompetitionJobs);
                corridorState.ActiveCarrierCount = Math.Max(0, source.ActiveCarrierCount);
                corridorState.DominantCarrierId = source.DominantCarrierId ?? string.Empty;
                corridorState.DominantCarrierName = source.DominantCarrierName ?? string.Empty;
                corridorState.VisibleCompetitionCount = Math.Max(0, source.VisibleCompetitionCount);
                corridorState.LastCompetitiveTons = Math.Max(0f, source.CompetitiveTons);
                corridorState.CompetitiveWinCount = Math.Max(0, source.CompetitiveWinCount);
                corridorState.ContestedWeekStreak = Math.Max(0, source.ContestedWeekStreak);
            }

            for (int i = 0; i < snapshot.DistrictReputationOffsets.Count; i++)
            {
                var source = snapshot.DistrictReputationOffsets[i];
                if (source == null || string.IsNullOrWhiteSpace(source.DistrictName) || Math.Abs(source.Offset) <= 0.001f)
                {
                    continue;
                }

                _districtReputationDebugOffsets[source.DistrictName.Trim()] = source.Offset;
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
                    siteState.RequiredWeeklyServiceTons = GetServiceContractTargetTons(industry);
                    siteState.EffectiveFranchiseLevel = ApplyFranchisePenalty(siteState.FranchiseLevel, siteState.ServicePenaltySteps);
                    siteState.ServiceContractStatus = BuildServiceContractStatus(industry, siteState);
                    siteState.PassiveIncomeStockReady = IsServiceSinkStocked(industry);
                    siteState.PassiveIncomeOperational = ShouldAwardServiceSinkPassiveIncome(industry, siteState);
                    siteState.PassiveIncomeStatus = BuildServiceSinkPassiveIncomeOperationalStatus(industry, siteState);
                }
                else
                {
                    siteState.RequiredWeeklyServiceTons = 0f;
                    siteState.EffectiveFranchiseLevel = TerritoryFranchiseLevel.None;
                    siteState.ServiceContractStatus = string.Empty;
                    siteState.PassiveIncomeStockReady = false;
                    siteState.PassiveIncomeOperational = false;
                    siteState.PassiveIncomeStatus = string.Empty;
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

            var competitionByDistrict = GetCompetitionSummariesByDistrict();
            var competitionByCorridor = GetCompetitionSummariesByCorridor();
            SyncDistrictCompetitionTelemetry(competitionByDistrict);
            SyncCorridorCompetitionTelemetry(competitionByCorridor);

            foreach (var corridorState in _corridorsById.Values)
            {
                corridorState.RequiredWeeklyDeliveredTons = GetCorridorWeeklyTargetTons(corridorState);
                corridorState.UpkeepStatus = BuildCorridorStatus(corridorState);
                corridorState.CompetitionStatus = BuildCorridorCompetitionStatus(corridorState);

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
                ApplyDistrictCorridorCompetitionContext(districtState);

                var target = GetDistrictInfluenceTarget(districtState.DistrictName, districtState.SiteCount);
                districtState.InfluenceRatio = target <= 0.001f
                    ? 0f
                    : Math.Max(0f, Math.Min(1.5f, districtState.InfluenceScore / target));

                if (!_reputationEnabled)
                {
                    districtState.ReputationScore = 0f;
                    districtState.ReputationLabel = "Disabled";
                    continue;
                }

                float debugOffset;
                if (_districtReputationDebugOffsets.TryGetValue(districtState.DistrictName ?? string.Empty, out debugOffset))
                {
                    districtState.ReputationScore += debugOffset;
                }

                districtState.ReputationLabel = ResolveReputationLabel(districtState.InfluenceRatio, districtState.ReputationScore);
                districtState.RequiredWeeklyActivityTons = GetDistrictLicenseTargetTons(districtState);
                districtState.WeeklyLicenseCost = GetDistrictAdministrationCost(districtState, DistrictHasStarterHeadquarters(districtState.DistrictName));
                districtState.CompetitionStatus = BuildDistrictCompetitionStatus(districtState);
                RefreshDistrictEventPresentation(districtState);
            }
        }

        private Dictionary<string, NpcDistrictCompetitionSummary> GetCompetitionSummariesByDistrict()
        {
            if (_getDistrictCompetitionSummaries == null)
            {
                return null;
            }

            return _getDistrictCompetitionSummaries()
                .Where(summary => summary != null && !string.IsNullOrWhiteSpace(summary.DistrictName))
                .GroupBy(summary => summary.DistrictName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        AggregateCarrierCompetitionMetadata(group, out var activeCarrierCount, out var dominantCarrierId, out var dominantCarrierName);
                        return new NpcDistrictCompetitionSummary
                        {
                            DistrictName = group.Key,
                            ActiveJobCount = group.Sum(entry => Math.Max(0, entry.ActiveJobCount)),
                            ActiveCarrierCount = activeCarrierCount,
                            DominantCarrierId = dominantCarrierId,
                            DominantCarrierName = dominantCarrierName,
                            VisibleConvoyCount = group.Sum(entry => Math.Max(0, entry.VisibleConvoyCount)),
                            CompetitiveTons = group.Sum(entry => Math.Max(0f, entry.CompetitiveTons)),
                            PressureScore = group.Sum(entry => Math.Max(0f, entry.PressureScore)),
                        };
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, NpcCorridorCompetitionSummary> GetCompetitionSummariesByCorridor()
        {
            if (_getCorridorCompetitionSummaries == null)
            {
                return null;
            }

            return _getCorridorCompetitionSummaries()
                .Where(summary => summary != null
                    && (!string.IsNullOrWhiteSpace(summary.CorridorId)
                        || (!string.IsNullOrWhiteSpace(summary.DistrictA)
                            && !string.IsNullOrWhiteSpace(summary.DistrictB)
                            && !string.Equals(summary.DistrictA, summary.DistrictB, StringComparison.OrdinalIgnoreCase))))
                .GroupBy(summary => !string.IsNullOrWhiteSpace(summary.CorridorId)
                        ? summary.CorridorId.Trim()
                        : BuildCorridorId(summary.DistrictA, summary.DistrictB),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var first = group.First();
                        var ordered = OrderDistrictPair(first != null ? first.DistrictA : string.Empty, first != null ? first.DistrictB : string.Empty);
                        AggregateCarrierCompetitionMetadata(group, out var activeCarrierCount, out var dominantCarrierId, out var dominantCarrierName);
                        return new NpcCorridorCompetitionSummary
                        {
                            CorridorId = group.Key,
                            DistrictA = ordered.Item1,
                            DistrictB = ordered.Item2,
                            ActiveJobCount = group.Sum(entry => Math.Max(0, entry.ActiveJobCount)),
                            ActiveCarrierCount = activeCarrierCount,
                            DominantCarrierId = dominantCarrierId,
                            DominantCarrierName = dominantCarrierName,
                            VisibleConvoyCount = group.Sum(entry => Math.Max(0, entry.VisibleConvoyCount)),
                            CompetitiveTons = group.Sum(entry => Math.Max(0f, entry.CompetitiveTons)),
                            PressureScore = group.Sum(entry => Math.Max(0f, entry.PressureScore)),
                        };
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        private static void AggregateCarrierCompetitionMetadata(
            IEnumerable<NpcDistrictCompetitionSummary> summaries,
            out int activeCarrierCount,
            out string dominantCarrierId,
            out string dominantCarrierName)
        {
            var carrierPressureById = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var carrierNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            activeCarrierCount = 0;
            dominantCarrierId = string.Empty;
            dominantCarrierName = string.Empty;
            if (summaries == null)
            {
                return;
            }

            foreach (var summary in summaries)
            {
                if (summary == null)
                {
                    continue;
                }

                if (summary.CarrierPressureById != null)
                {
                    foreach (var pair in summary.CarrierPressureById)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0.001f)
                        {
                            continue;
                        }

                        float current;
                        carrierPressureById.TryGetValue(pair.Key, out current);
                        carrierPressureById[pair.Key] = current + pair.Value;
                    }
                }

                if (summary.CarrierNamesById != null)
                {
                    foreach (var pair in summary.CarrierNamesById)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Key) || carrierNamesById.ContainsKey(pair.Key))
                        {
                            continue;
                        }

                        carrierNamesById[pair.Key] = pair.Value ?? string.Empty;
                    }
                }

                if (carrierPressureById.Count == 0 && summary.ActiveCarrierCount > activeCarrierCount)
                {
                    activeCarrierCount = Math.Max(0, summary.ActiveCarrierCount);
                    dominantCarrierId = summary.DominantCarrierId ?? string.Empty;
                    dominantCarrierName = summary.DominantCarrierName ?? string.Empty;
                }
            }

            if (carrierPressureById.Count > 0)
            {
                activeCarrierCount = carrierPressureById.Count;
                var dominant = carrierPressureById
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .First();
                dominantCarrierId = dominant.Key ?? string.Empty;
                dominantCarrierName = carrierNamesById.ContainsKey(dominantCarrierId)
                    ? (carrierNamesById[dominantCarrierId] ?? string.Empty)
                    : dominantCarrierId;
            }
        }

        private static void AggregateCarrierCompetitionMetadata(
            IEnumerable<NpcCorridorCompetitionSummary> summaries,
            out int activeCarrierCount,
            out string dominantCarrierId,
            out string dominantCarrierName)
        {
            var carrierPressureById = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var carrierNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            activeCarrierCount = 0;
            dominantCarrierId = string.Empty;
            dominantCarrierName = string.Empty;
            if (summaries == null)
            {
                return;
            }

            foreach (var summary in summaries)
            {
                if (summary == null)
                {
                    continue;
                }

                if (summary.CarrierPressureById != null)
                {
                    foreach (var pair in summary.CarrierPressureById)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0.001f)
                        {
                            continue;
                        }

                        float current;
                        carrierPressureById.TryGetValue(pair.Key, out current);
                        carrierPressureById[pair.Key] = current + pair.Value;
                    }
                }

                if (summary.CarrierNamesById != null)
                {
                    foreach (var pair in summary.CarrierNamesById)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Key) || carrierNamesById.ContainsKey(pair.Key))
                        {
                            continue;
                        }

                        carrierNamesById[pair.Key] = pair.Value ?? string.Empty;
                    }
                }

                if (carrierPressureById.Count == 0 && summary.ActiveCarrierCount > activeCarrierCount)
                {
                    activeCarrierCount = Math.Max(0, summary.ActiveCarrierCount);
                    dominantCarrierId = summary.DominantCarrierId ?? string.Empty;
                    dominantCarrierName = summary.DominantCarrierName ?? string.Empty;
                }
            }

            if (carrierPressureById.Count > 0)
            {
                activeCarrierCount = carrierPressureById.Count;
                var dominant = carrierPressureById
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .First();
                dominantCarrierId = dominant.Key ?? string.Empty;
                dominantCarrierName = carrierNamesById.ContainsKey(dominantCarrierId)
                    ? (carrierNamesById[dominantCarrierId] ?? string.Empty)
                    : dominantCarrierId;
            }
        }

        private void SyncDistrictCompetitionTelemetry(IDictionary<string, NpcDistrictCompetitionSummary> competitionByDistrict)
        {
            if (competitionByDistrict == null)
            {
                return;
            }

            foreach (var districtState in _districtsByName.Values)
            {
                if (districtState == null)
                {
                    continue;
                }

                NpcDistrictCompetitionSummary competition = null;
                var hasCompetition = competitionByDistrict != null
                    && competitionByDistrict.TryGetValue(districtState.DistrictName ?? string.Empty, out competition);
                if (!hasCompetition)
                {
                    competition = null;
                }

                districtState.ActiveCompetitionJobs = competition != null ? Math.Max(0, competition.ActiveJobCount) : 0;
                districtState.ActiveCarrierCount = competition != null ? Math.Max(0, competition.ActiveCarrierCount) : 0;
                districtState.DominantCarrierId = competition != null ? (competition.DominantCarrierId ?? string.Empty) : string.Empty;
                districtState.DominantCarrierName = competition != null ? (competition.DominantCarrierName ?? string.Empty) : string.Empty;
                districtState.VisibleCompetitionCount = competition != null ? Math.Max(0, competition.VisibleConvoyCount) : 0;
                districtState.LastCompetitiveTons = competition != null ? Math.Max(0f, competition.CompetitiveTons) : 0f;
            }
        }

        private void SyncCorridorCompetitionTelemetry(IDictionary<string, NpcCorridorCompetitionSummary> competitionByCorridor)
        {
            if (competitionByCorridor == null)
            {
                return;
            }

            foreach (var corridorState in _corridorsById.Values)
            {
                if (corridorState == null)
                {
                    continue;
                }

                corridorState.ActiveCompetitionJobs = 0;
                corridorState.ActiveCarrierCount = 0;
                corridorState.DominantCarrierId = string.Empty;
                corridorState.DominantCarrierName = string.Empty;
                corridorState.VisibleCompetitionCount = 0;
                corridorState.LastCompetitiveTons = 0f;
            }

            foreach (var pair in competitionByCorridor)
            {
                var competition = pair.Value;
                if (competition == null || string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var corridorState = GetOrCreateCorridorState(competition.DistrictA, competition.DistrictB);
                corridorState.ActiveCompetitionJobs = Math.Max(0, competition.ActiveJobCount);
                corridorState.ActiveCarrierCount = Math.Max(0, competition.ActiveCarrierCount);
                corridorState.DominantCarrierId = competition.DominantCarrierId ?? string.Empty;
                corridorState.DominantCarrierName = competition.DominantCarrierName ?? string.Empty;
                corridorState.VisibleCompetitionCount = Math.Max(0, competition.VisibleConvoyCount);
                corridorState.LastCompetitiveTons = Math.Max(0f, competition.CompetitiveTons);
            }
        }

        private void ApplyDistrictCorridorCompetitionContext(TerritoryDistrictState districtState)
        {
            if (districtState == null)
            {
                return;
            }

            districtState.ContestedCorridorCount = 0;
            districtState.CorridorHoldCount = 0;
            districtState.HottestCorridorPressure = 0f;
            districtState.HottestCorridorName = string.Empty;

            foreach (var corridorState in _corridorsById.Values)
            {
                if (corridorState == null
                    || (!string.Equals(corridorState.DistrictA, districtState.DistrictName, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(corridorState.DistrictB, districtState.DistrictName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (corridorState.CompetitivePressure >= 0.18f
                    || corridorState.ActiveCompetitionJobs > 0
                    || corridorState.ContestedWeekStreak > 0)
                {
                    districtState.ContestedCorridorCount += 1;
                }

                districtState.CorridorHoldCount += Math.Max(0, corridorState.CompetitiveWinCount);
                if (corridorState.CompetitivePressure > districtState.HottestCorridorPressure)
                {
                    districtState.HottestCorridorPressure = corridorState.CompetitivePressure;
                    districtState.HottestCorridorName = string.Equals(corridorState.DistrictA, districtState.DistrictName, StringComparison.OrdinalIgnoreCase)
                        ? corridorState.DistrictB
                        : corridorState.DistrictA;
                }
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
            var influenceThreshold = 0.85f;
            if (districtState != null)
            {
                if (districtState.LicenseStatus == DistrictLicenseStatus.Active)
                {
                    influenceThreshold = 0.72f;
                }
                else if (districtState.LicenseStatus == DistrictLicenseStatus.Probation)
                {
                    influenceThreshold = 0.78f;
                }
            }

            return districtState != null && districtState.InfluenceRatio >= influenceThreshold && ResolveOperationalState(industry, siteState);
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
            var districtState = GetDistrictState(districtName);
            if (districtState != null)
            {
                if (districtState.LicenseStatus == DistrictLicenseStatus.Active)
                {
                    support += 0.03f;
                }
                else if (districtState.LicenseStatus == DistrictLicenseStatus.Probation)
                {
                    support += 0.015f;
                }
            }

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
                if (siteState.DepotSpecialization == DepotSpecialization.Support)
                {
                    support += 0.025f + (siteState.ManagerCount * 0.01f);
                }
            }

            var endgame = GetEndgameSummary();
            if (endgame.ActiveDoctrine == CompanyDoctrine.Territorial)
            {
                support += CompanyDoctrineSystem.GetTerritorialSupportBonus(endgame.ActiveDoctrineEffectiveTier);
            }
            else if (endgame.ActiveDoctrine == CompanyDoctrine.Industrial)
            {
                support -= CompanyDoctrineSystem.GetIndustrialSupportPenalty(endgame.ActiveDoctrineEffectiveTier);
            }

            if (districtState != null)
            {
                support -= Math.Min(0.06f, districtState.CompetitivePressure * 0.06f);
                support += Math.Min(0.02f, districtState.CompetitiveOpportunity * 0.02f);
            }

            if (endgame.HasLandmarkHeadquarters)
            {
                support += 0.01f;
            }

            return Math.Max(0f, Math.Min(0.28f, support));
        }

        private float GetDistrictDispatchBonus(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return 0f;
            }

            var bonus = 0f;
            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null
                    || siteState.DepotSpecialization != DepotSpecialization.Dispatch
                    || siteState.ControlLevel == TerritoryControlLevel.None
                    || !siteState.CrewAssigned
                    || !string.Equals(siteState.DistrictName, districtName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bonus += 0.02f + (siteState.ManagerCount * 0.005f);
            }

            var districtState = GetDistrictState(districtName);
            if (districtState != null)
            {
                bonus += Math.Min(0.035f, districtState.CompetitiveOpportunity * 0.035f);
            }

            var endgame = GetEndgameSummary();
            if (endgame.ActiveDoctrine == CompanyDoctrine.Service)
            {
                bonus += CompanyDoctrineSystem.GetServiceOpportunityBonus(endgame.ActiveDoctrineEffectiveTier);
            }

            return Math.Max(0f, Math.Min(0.09f, bonus));
        }

        private float GetDistrictMaintenanceBonus(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return 0f;
            }

            var bonus = 0f;
            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null
                    || siteState.DepotSpecialization != DepotSpecialization.Maintenance
                    || siteState.ControlLevel == TerritoryControlLevel.None
                    || !siteState.CrewAssigned
                    || !string.Equals(siteState.DistrictName, districtName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bonus += 0.02f + (siteState.MechanicCount * 0.01f);
            }

            return Math.Max(0f, Math.Min(0.08f, bonus));
        }

        private float GetDistrictSecurityBonus(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return 0f;
            }

            var bonus = 0f;
            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null
                    || siteState.DepotSpecialization != DepotSpecialization.Security
                    || siteState.ControlLevel == TerritoryControlLevel.None
                    || !siteState.CrewAssigned
                    || !string.Equals(siteState.DistrictName, districtName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bonus += 0.02f + (siteState.GuardCount * 0.008f);
            }

            return Math.Max(0f, Math.Min(0.08f, bonus));
        }

        private TerritoryDistrictOperationsEntry EnsureDistrictOperationsEntry(IDictionary<string, TerritoryDistrictOperationsEntry> entries, string districtName)
        {
            var normalizedName = districtName ?? string.Empty;
            TerritoryDistrictOperationsEntry entry;
            if (entries.TryGetValue(normalizedName, out entry))
            {
                return entry;
            }

            var districtState = GetDistrictState(normalizedName);
            entry = new TerritoryDistrictOperationsEntry
            {
                DistrictName = normalizedName,
                ReputationLabel = districtState != null ? districtState.ReputationLabel ?? string.Empty : string.Empty,
            };
            entries[normalizedName] = entry;
            return entry;
        }

        private bool DistrictHasStarterHeadquarters(string districtName)
        {
            if (string.IsNullOrWhiteSpace(districtName))
            {
                return false;
            }

            return _industriesById.Values.Any(industry => industry != null
                && industry.IsStarterHeadquarters
                && string.Equals(industry.DistrictName, districtName, StringComparison.OrdinalIgnoreCase));
        }

        private float GetDistrictAdministrationCost(TerritoryDistrictState districtState, bool hasStarterHeadquarters)
        {
            if (districtState == null
                || hasStarterHeadquarters
                || (districtState.LicenseStatus != DistrictLicenseStatus.Active && districtState.LicenseStatus != DistrictLicenseStatus.Probation))
            {
                return 0f;
            }

            var cost = 2200f
                + (districtState.ControlledSites * 450f)
                + (districtState.RouteRights * 250f)
                + (districtState.ControlledDepots * 300f);
            var reputationTier = GetReputationTier(districtState.ReputationLabel);
            if (reputationTier >= 3)
            {
                cost += 1200f;
            }

            if (reputationTier >= 4)
            {
                cost += 1800f;
            }

            if (districtState.LicenseStatus == DistrictLicenseStatus.Probation)
            {
                cost *= 0.9f;
            }

            var endgame = GetEndgameSummary();
            if (endgame.ActiveDoctrine == CompanyDoctrine.Territorial)
            {
                cost *= 1f + CompanyDoctrineSystem.GetTerritorialOperationsCostPenalty(endgame.ActiveDoctrineEffectiveTier);
            }

            return cost;
        }

        private static float GetSupportSiteOperationsCost(Industry industry, TerritorySiteState siteState)
        {
            if (industry == null || siteState == null || siteState.ControlLevel == TerritoryControlLevel.None)
            {
                return 0f;
            }

            var tierRank = GetTierRank(industry.OwnershipTier);
            var cost = siteState.ControlLevel == TerritoryControlLevel.Owned
                ? 2200f + (tierRank * 450f)
                : 1400f + (tierRank * 300f);
            if (siteState.CrewAssigned)
            {
                cost += 850f;
            }

            return cost;
        }

        private static float GetSupportStaffOperationsCost(TerritorySiteState siteState)
        {
            if (siteState == null)
            {
                return 0f;
            }

            return (siteState.LoaderCount * 350f)
                + (siteState.MechanicCount * 525f)
                + (siteState.GuardCount * 425f)
                + (siteState.ManagerCount * 800f);
        }

        private float GetDistrictLicenseTargetTons(TerritoryDistrictState districtState)
        {
            var baseTarget = GetBaseDistrictLicenseTargetTons(districtState);
            if (baseTarget <= 0.01f)
            {
                return 0f;
            }

            return Math.Max(18f, baseTarget * (1f + GetDistrictEventActivityTargetModifier(districtState)));
        }

        private float GetBaseDistrictLicenseTargetTons(TerritoryDistrictState districtState)
        {
            if (districtState == null || DistrictHasStarterHeadquarters(districtState.DistrictName))
            {
                return 0f;
            }

            if (districtState.LicenseStatus == DistrictLicenseStatus.None && !IsDistrictLicensable(districtState.DistrictName))
            {
                return 0f;
            }

            var target = 22f
                + (districtState.ControlledSites * 8f)
                + (districtState.RouteRights * 6f)
                + (districtState.ControlledDepots * 10f);
            var reputationTier = GetReputationTier(districtState.ReputationLabel);
            if (reputationTier >= 3)
            {
                target += 18f;
            }

            var modifier = 1f;
            modifier -= Math.Min(0.12f, GetDistrictDispatchBonus(districtState.DistrictName) + (GetDistrictSupportFactor(districtState.DistrictName) * 0.35f));
            modifier += Math.Min(0.18f, districtState.CompetitivePressure * 0.18f);

            var endgame = GetEndgameSummary();
            if (endgame.ActiveDoctrine == CompanyDoctrine.Territorial)
            {
                modifier -= CompanyDoctrineSystem.GetTerritorialLicenseRelief(endgame.ActiveDoctrineEffectiveTier);
            }

            if (endgame.HasLandmarkHeadquarters)
            {
                modifier -= 0.03f;
            }

            return Math.Max(18f, target * Math.Max(0.7f, modifier));
        }

        private float GetServiceContractTargetTons(Industry industry)
        {
            var baseTarget = GetBaseServiceContractTargetTons(industry);
            if (baseTarget <= 0.01f)
            {
                return 0f;
            }

            return Math.Max(8f, baseTarget * (1f + GetDistrictEventServiceTargetModifier(industry)));
        }

        private float GetBaseServiceContractTargetTons(Industry industry)
        {
            if (industry == null || !IsServiceSink(industry))
            {
                return 0f;
            }

            var baseTarget = 8f + (GetTierRank(industry.OwnershipTier) * 4f);
            baseTarget += Math.Max(0f, industry.InputCapacityTons) * 0.08f;
            baseTarget += Math.Max(0f, industry.EmptyingRate) * 4f;

            if (industry.IsConstructionSink)
            {
                baseTarget += 18f;
            }
            else if (industry.IsGasStation)
            {
                baseTarget += 10f;
            }
            else if (industry.IsStore)
            {
                baseTarget += 6f;
            }

            var districtState = GetDistrictState(industry.DistrictName);
            if (districtState != null)
            {
                baseTarget *= 1f + Math.Min(0.15f, districtState.CompetitivePressure * 0.15f);
            }

            var endgame = GetEndgameSummary();
            if (endgame.ActiveDoctrine == CompanyDoctrine.Service)
            {
                baseTarget *= Math.Max(0.72f, 1f - CompanyDoctrineSystem.GetServiceTargetReduction(endgame.ActiveDoctrineEffectiveTier));
            }
            else if (endgame.ActiveDoctrine == CompanyDoctrine.Industrial)
            {
                baseTarget *= 1f + CompanyDoctrineSystem.GetIndustrialServiceTargetPenalty(endgame.ActiveDoctrineEffectiveTier);
            }

            return Math.Max(8f, baseTarget);
        }

        private void ProcessCompetitionPressure(TerritoryWeeklyMaintenanceResult result, int weekIndex)
        {
            var competitionByDistrict = GetCompetitionSummariesByDistrict();
            var competitionByCorridor = GetCompetitionSummariesByCorridor();
            SyncDistrictCompetitionTelemetry(competitionByDistrict);
            SyncCorridorCompetitionTelemetry(competitionByCorridor);
            ProcessCorridorCompetitionPressure(result, weekIndex, competitionByCorridor);

            foreach (var districtState in _districtsByName.Values)
            {
                ApplyDistrictCorridorCompetitionContext(districtState);
            }

            var endgame = GetEndgameSummary();
            foreach (var districtState in _districtsByName.Values)
            {
                if (districtState == null)
                {
                    continue;
                }

                NpcDistrictCompetitionSummary competition;
                if (competitionByDistrict != null)
                {
                    competitionByDistrict.TryGetValue(districtState.DistrictName ?? string.Empty, out competition);
                }
                else
                {
                    competition = null;
                }

                var previousPressure = districtState.CompetitivePressure;
                var ambientPressure = competition != null
                    ? Math.Min(0.45f, Math.Max(0f, competition.PressureScore))
                    : 0f;
                var corridorPressure = Math.Min(
                    0.24f,
                    (districtState.ContestedCorridorCount * 0.035f)
                        + (districtState.HottestCorridorPressure * 0.18f)
                        + Math.Min(0.08f, districtState.CorridorHoldCount * 0.01f));
                var footprintPressure = 0f;
                if (districtState.LicenseStatus == DistrictLicenseStatus.Active || districtState.LicenseStatus == DistrictLicenseStatus.Probation)
                {
                    footprintPressure += 0.04f;
                }

                if (districtState.InfluenceRatio >= 0.85f)
                {
                    footprintPressure += 0.06f;
                }
                else if (districtState.InfluenceRatio >= 0.65f)
                {
                    footprintPressure += 0.03f;
                }

                footprintPressure += Math.Min(0.08f, districtState.ControlledSites * 0.01f);

                var doctrinePressure = 0f;
                if (endgame.ActiveDoctrine == CompanyDoctrine.Territorial)
                {
                    doctrinePressure += CompanyDoctrineSystem.GetTerritorialCompetitionGrowthPenalty(endgame.ActiveDoctrineEffectiveTier) * 0.35f;
                }
                else if (endgame.ActiveDoctrine == CompanyDoctrine.Industrial)
                {
                    doctrinePressure -= 0.02f * endgame.ActiveDoctrineEffectiveTier;
                }
                else if (endgame.ActiveDoctrine == CompanyDoctrine.Service)
                {
                    doctrinePressure -= 0.01f * endgame.ActiveDoctrineEffectiveTier;
                }

                var activityTarget = districtState.RequiredWeeklyActivityTons > 0.01f
                    ? districtState.RequiredWeeklyActivityTons
                    : 18f;
                var activityRatio = activityTarget > 0.01f
                    ? Math.Min(1.5f, districtState.CurrentWeekActivityTons / activityTarget)
                    : 0f;
                var defensePressure = Math.Min(0.22f, activityRatio * 0.16f);
                defensePressure += Math.Min(0.10f, districtState.CorridorHoldCount * 0.025f);
                defensePressure += Math.Min(0.10f, GetDistrictSecurityBonus(districtState.DistrictName) + (GetDistrictSupportFactor(districtState.DistrictName) * 0.35f));
                if (endgame.HasLandmarkHeadquarters)
                {
                    defensePressure += 0.06f;
                }

                var nextPressure = Clamp01(previousPressure + ambientPressure + corridorPressure + footprintPressure + doctrinePressure - defensePressure);
                if (competition == null && districtState.ContestedCorridorCount <= 0)
                {
                    nextPressure = Math.Max(0f, nextPressure - 0.04f);
                }

                var nextOpportunity = Clamp01(
                    (ambientPressure * 0.55f)
                    + Math.Min(0.18f, districtState.ContestedCorridorCount * 0.025f)
                    + Math.Min(0.12f, districtState.HottestCorridorPressure * 0.12f)
                    + (Math.Max(0f, nextPressure - 0.22f) * 0.35f));
                if (endgame.ActiveDoctrine == CompanyDoctrine.Service)
                {
                    nextOpportunity = Clamp01(nextOpportunity + CompanyDoctrineSystem.GetServiceOpportunityBonus(endgame.ActiveDoctrineEffectiveTier));
                }

                if ((ambientPressure >= 0.14f || districtState.ContestedCorridorCount > 0) && districtState.CurrentWeekActivityTons > 0.001f)
                {
                    districtState.CompetitiveResponseCount += 1;
                }

                var heldGround = (ambientPressure + corridorPressure) >= 0.18f
                    && districtState.CurrentWeekActivityTons >= Math.Max(18f, activityTarget * 0.75f);
                if (heldGround)
                {
                    districtState.CompetitiveWinCount += 1;
                    result.CompetitiveWinCount += 1;
                    nextPressure = Math.Max(0f, nextPressure - (0.10f + Math.Min(0.08f, activityRatio * 0.06f)));
                    nextOpportunity = Clamp01(nextOpportunity + 0.08f);
                    result.Messages.Add(string.Format("Week {0}: {1} held its ground against outside carriers and opened fresh contract demand.", weekIndex, districtState.DistrictName));
                }
                else if (nextPressure >= 0.70f && districtState.CurrentWeekActivityTons + 0.25f < Math.Max(14f, activityTarget * 0.5f))
                {
                    result.Messages.Add(string.Format("Week {0}: competition tightened in {1}; local share is slipping.", weekIndex, districtState.DistrictName));
                }
                else if (previousPressure < 0.4f && nextPressure >= 0.4f)
                {
                    result.Messages.Add(string.Format("Week {0}: outside carrier traffic intensified around {1}.", weekIndex, districtState.DistrictName));
                }

                districtState.CompetitivePressure = nextPressure;
                districtState.CompetitiveOpportunity = nextOpportunity;
                districtState.ActiveCompetitionJobs = competition != null ? Math.Max(0, competition.ActiveJobCount) : 0;
                districtState.VisibleCompetitionCount = competition != null ? Math.Max(0, competition.VisibleConvoyCount) : 0;
                districtState.LastCompetitiveTons = competition != null ? Math.Max(0f, competition.CompetitiveTons) : 0f;
                districtState.CompetitionStatus = BuildDistrictCompetitionStatus(districtState);
                if (nextPressure >= 0.4f)
                {
                    result.HighCompetitionDistrictCount += 1;
                }
            }

            foreach (var corridorState in _corridorsById.Values)
            {
                if (corridorState == null)
                {
                    continue;
                }

                corridorState.RequiredWeeklyDeliveredTons = GetCorridorWeeklyTargetTons(corridorState);
                corridorState.UpkeepStatus = BuildCorridorStatus(corridorState);
                corridorState.CompetitionStatus = BuildCorridorCompetitionStatus(corridorState);
            }
        }

        private void ProcessCorridorCompetitionPressure(
            TerritoryWeeklyMaintenanceResult result,
            int weekIndex,
            IDictionary<string, NpcCorridorCompetitionSummary> competitionByCorridor)
        {
            foreach (var corridorState in _corridorsById.Values)
            {
                if (corridorState == null)
                {
                    continue;
                }

                NpcCorridorCompetitionSummary competition = null;
                var hasCompetition = competitionByCorridor != null
                    && competitionByCorridor.TryGetValue(corridorState.CorridorId ?? string.Empty, out competition);
                if (!hasCompetition)
                {
                    competition = null;
                }

                var previousPressure = corridorState.CompetitivePressure;
                var ambientPressure = competition != null
                    ? Math.Min(0.55f, Math.Max(0f, competition.PressureScore))
                    : 0f;
                var flowTarget = corridorState.RightLevel != CorridorRightLevel.None
                    ? (corridorState.RequiredWeeklyDeliveredTons > 0.01f
                        ? corridorState.RequiredWeeklyDeliveredTons
                        : GetCorridorWeeklyTargetTons(corridorState))
                    : Math.Max(16f, Math.Min(60f, (Math.Max(0f, corridorState.LastCompetitiveTons) * 1.2f) + 12f));
                var flowRatio = flowTarget > 0.01f
                    ? Math.Min(1.5f, corridorState.CurrentWeekDeliveredTons / flowTarget)
                    : 0f;

                var routePressure = corridorState.RightLevel != CorridorRightLevel.None
                    ? Math.Min(0.10f, (int)corridorState.RightLevel * 0.025f)
                    : 0f;
                var neglectPressure = 0f;
                if (corridorState.RightLevel != CorridorRightLevel.None)
                {
                    neglectPressure += Math.Min(0.18f, Math.Max(0f, 1f - flowRatio) * 0.16f);
                    neglectPressure += Math.Min(0.08f, corridorState.DecayPressure * 0.06f);
                }
                else if (ambientPressure > 0.001f)
                {
                    neglectPressure += 0.04f;
                }

                var defensePressure = 0f;
                if (corridorState.RightLevel != CorridorRightLevel.None)
                {
                    defensePressure += Math.Min(0.24f, flowRatio * 0.18f);
                }

                defensePressure += Math.Min(0.08f, GetDistrictSecurityBonus(corridorState.DistrictA) + GetDistrictSecurityBonus(corridorState.DistrictB));
                defensePressure += Math.Min(0.06f, (GetDistrictSupportFactor(corridorState.DistrictA) + GetDistrictSupportFactor(corridorState.DistrictB)) * 0.18f);

                var nextPressure = Clamp01(previousPressure + ambientPressure + routePressure + neglectPressure - defensePressure);
                if (competition == null)
                {
                    nextPressure = Math.Max(0f, nextPressure - 0.05f);
                }

                var nextOpportunity = Clamp01((ambientPressure * 0.45f) + (Math.Max(0f, nextPressure - 0.28f) * 0.45f));
                var meaningfulPressure = ambientPressure >= 0.16f
                    || nextPressure >= 0.35f
                    || corridorState.ActiveCompetitionJobs > 0;
                var heldGround = corridorState.RightLevel != CorridorRightLevel.None
                    && meaningfulPressure
                    && corridorState.CurrentWeekDeliveredTons >= Math.Max(14f, flowTarget * 0.7f);

                if (heldGround)
                {
                    corridorState.CompetitiveWinCount += 1;
                    corridorState.ContestedWeekStreak = Math.Min(8, corridorState.ContestedWeekStreak + 1);
                    nextPressure = Math.Max(0f, nextPressure - (0.12f + Math.Min(0.08f, flowRatio * 0.08f)));
                    nextOpportunity = Clamp01(nextOpportunity + 0.12f);
                    result.Messages.Add(string.Format(
                        "Week {0}: {1} / {2} held its lane against outside carriers.",
                        weekIndex,
                        corridorState.DistrictA,
                        corridorState.DistrictB));
                }
                else if (meaningfulPressure)
                {
                    corridorState.ContestedWeekStreak = Math.Min(8, corridorState.ContestedWeekStreak + 1);
                    if (corridorState.RightLevel != CorridorRightLevel.None && flowRatio < 0.6f)
                    {
                        corridorState.DecayPressure += flowRatio >= 0.35f ? 0.35f : 0.65f;
                    }

                    if (nextPressure >= 0.68f && corridorState.RightLevel != CorridorRightLevel.None)
                    {
                        result.Messages.Add(string.Format(
                            "Week {0}: outside carriers are contesting {1} / {2}.",
                            weekIndex,
                            corridorState.DistrictA,
                            corridorState.DistrictB));
                    }
                }
                else
                {
                    corridorState.ContestedWeekStreak = Math.Max(0, corridorState.ContestedWeekStreak - 1);
                }

                corridorState.CompetitivePressure = nextPressure;
                corridorState.CompetitiveOpportunity = nextOpportunity;
                corridorState.ActiveCompetitionJobs = competition != null ? Math.Max(0, competition.ActiveJobCount) : 0;
                corridorState.VisibleCompetitionCount = competition != null ? Math.Max(0, competition.VisibleConvoyCount) : 0;
                corridorState.LastCompetitiveTons = competition != null ? Math.Max(0f, competition.CompetitiveTons) : 0f;
                corridorState.CompetitionStatus = BuildCorridorCompetitionStatus(corridorState);
            }
        }

        private static TerritoryFranchiseLevel ApplyFranchisePenalty(TerritoryFranchiseLevel baseLevel, int penaltySteps)
        {
            var adjustedLevel = Math.Max(0, (int)baseLevel - Math.Max(0, penaltySteps));
            return (TerritoryFranchiseLevel)adjustedLevel;
        }

        private bool IsServiceContractAtRisk(TerritorySiteState siteState)
        {
            return siteState != null && siteState.ServicePenaltySteps > 0;
        }

        private bool ShouldAwardServiceSinkPassiveIncome(Industry industry, TerritorySiteState siteState)
        {
            return industry != null
            && siteState != null
                && industry.IsOwned
                && (industry.IsStore || industry.IsGasStation)
                && industry.WeeklyPassiveIncome > 0.01f
            && siteState.SiteOperatorAssigned
            && IsServiceSinkStocked(industry);
        }

        private bool IsCorridorAtRisk(TerritoryCorridorState corridorState)
        {
            return corridorState != null
                && corridorState.RightLevel != CorridorRightLevel.None
                && (corridorState.DecayPressure >= GetCorridorDecayThreshold(corridorState) * 0.75f
                    || corridorState.CompetitivePressure >= 0.55f
                    || (corridorState.CompetitivePressure >= 0.35f
                        && corridorState.CurrentWeekDeliveredTons + 0.25f < Math.Max(12f, corridorState.RequiredWeeklyDeliveredTons * 0.6f)));
        }

        private void ProcessDistrictLicenseMaintenance(TerritoryWeeklyMaintenanceResult result, int weekIndex)
        {
            foreach (var districtState in _districtsByName.Values)
            {
                if (districtState == null || districtState.LicenseStatus == DistrictLicenseStatus.None || DistrictHasStarterHeadquarters(districtState.DistrictName))
                {
                    continue;
                }

                if (districtState.LicenseStatus == DistrictLicenseStatus.Suspended)
                {
                    continue;
                }

                if (GetReputationTier(districtState.ReputationLabel) < 2 || districtState.InfluenceRatio < 0.5f)
                {
                    districtState.LicenseStatus = DistrictLicenseStatus.Suspended;
                    districtState.LicenseStrikeCount = Math.Max(2, districtState.LicenseStrikeCount);
                    result.SuspendedDistrictCount += 1;
                    result.Messages.Add(string.Format("Week {0}: {1} lost its charter after reputation slipped.", weekIndex, districtState.DistrictName));
                    continue;
                }

                var targetTons = districtState.RequiredWeeklyActivityTons > 0.01f
                    ? districtState.RequiredWeeklyActivityTons
                    : GetDistrictLicenseTargetTons(districtState);
                if (districtState.CurrentWeekActivityTons + 0.25f >= targetTons)
                {
                    if (districtState.LicenseStatus == DistrictLicenseStatus.Probation || districtState.LicenseStrikeCount > 0)
                    {
                        result.Messages.Add(string.Format("Week {0}: {1} restored charter compliance.", weekIndex, districtState.DistrictName));
                    }

                    districtState.LicenseStatus = DistrictLicenseStatus.Active;
                    districtState.LicenseStrikeCount = 0;
                    continue;
                }

                districtState.LicenseStrikeCount += 1;
                if (districtState.LicenseStrikeCount >= 2)
                {
                    districtState.LicenseStatus = DistrictLicenseStatus.Suspended;
                    result.SuspendedDistrictCount += 1;
                    result.Messages.Add(string.Format("Week {0}: {1} failed charter compliance and is now suspended.", weekIndex, districtState.DistrictName));
                }
                else
                {
                    districtState.LicenseStatus = DistrictLicenseStatus.Probation;
                    result.Messages.Add(string.Format("Week {0}: {1} is on charter probation. Activity {2:0}/{3:0} t.", weekIndex, districtState.DistrictName, districtState.CurrentWeekActivityTons, targetTons));
                }
            }
        }

        private void ProcessServiceFranchiseMaintenance(TerritoryWeeklyMaintenanceResult result, int weekIndex)
        {
            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null)
                {
                    continue;
                }

                var industry = ResolveIndustry(siteState.SiteId);
                if (industry == null || !IsServiceSink(industry))
                {
                    continue;
                }

                if (!industry.IsOwned)
                {
                    siteState.SiteOperatorAssigned = false;
                    siteState.ServicePenaltySteps = 0;
                    siteState.ServiceSuccessStreak = 0;
                    siteState.ServiceTargetMetLastWeek = false;
                    siteState.LastPassiveIncomeAmount = 0f;
                    siteState.LastPassiveIncomeWeekIndex = -1;
                    siteState.LastPassiveIncomeStatus = string.Empty;
                    continue;
                }

                if (siteState.SiteOperatorAssigned)
                {
                    result.ServiceSinkStaffingExpense += ComputeServiceSiteWeeklyStaffingCost(industry);
                }

                var awardedPassiveIncome = ShouldAwardServiceSinkPassiveIncome(industry, siteState);
                if (awardedPassiveIncome)
                {
                    result.ServiceSinkPassiveIncome += industry.WeeklyPassiveIncome;
                }

                siteState.LastPassiveIncomeAmount = awardedPassiveIncome ? industry.WeeklyPassiveIncome : 0f;
                siteState.LastPassiveIncomeWeekIndex = weekIndex;
                siteState.LastPassiveIncomeStatus = BuildServiceSinkPassiveIncomeWeeklyOutcome(industry, siteState, awardedPassiveIncome);

                var targetTons = siteState.RequiredWeeklyServiceTons > 0.01f
                    ? siteState.RequiredWeeklyServiceTons
                    : GetServiceContractTargetTons(industry);
                if (siteState.CurrentWeekServiceTons + 0.25f >= targetTons)
                {
                    if (!siteState.ServiceTargetMetLastWeek && siteState.ServicePenaltySteps > 0)
                    {
                        result.Messages.Add(string.Format("Week {0}: {1} recovered its local service contract.", weekIndex, industry.Name));
                    }

                    siteState.ServiceTargetMetLastWeek = true;
                    siteState.ServiceSuccessStreak += 1;
                    siteState.ServicePenaltySteps = Math.Max(0, siteState.ServicePenaltySteps - 1);
                    continue;
                }

                siteState.ServiceTargetMetLastWeek = false;
                siteState.ServiceSuccessStreak = 0;
                siteState.ServicePenaltySteps = Math.Min(2, siteState.ServicePenaltySteps + 1);
                result.MissedServiceContractCount += 1;
                result.Messages.Add(string.Format("Week {0}: {1} missed its service target ({2:0}/{3:0} t).", weekIndex, industry.Name, siteState.CurrentWeekServiceTons, targetTons));
            }
        }

        private void ProcessCorridorMaintenance(TerritoryWeeklyMaintenanceResult result, int weekIndex)
        {
            foreach (var corridorState in _corridorsById.Values)
            {
                if (corridorState == null || corridorState.RightLevel == CorridorRightLevel.None)
                {
                    continue;
                }

                var targetTons = corridorState.RequiredWeeklyDeliveredTons > 0.01f
                    ? corridorState.RequiredWeeklyDeliveredTons
                    : GetCorridorWeeklyTargetTons(corridorState);
                if (corridorState.CurrentWeekDeliveredTons + 0.25f >= targetTons)
                {
                    corridorState.DecayPressure = Math.Max(0f, corridorState.DecayPressure - 1f);
                    continue;
                }

                corridorState.DecayPressure += corridorState.CurrentWeekDeliveredTons >= (targetTons * 0.5f)
                    ? 0.5f
                    : 1f;
                var threshold = GetCorridorDecayThreshold(corridorState);
                if (corridorState.DecayPressure + 0.001f < threshold)
                {
                    continue;
                }

                corridorState.RightLevel -= 1;
                corridorState.DeliveryCount = Math.Min(corridorState.DeliveryCount, GetMinimumDeliveryCountForLevel(corridorState.RightLevel));
                corridorState.TotalDeliveredTons = Math.Min(corridorState.TotalDeliveredTons, GetMinimumDeliveredTonsForLevel(corridorState.RightLevel));
                corridorState.DecayPressure = Math.Max(0f, corridorState.DecayPressure - threshold);
                result.DegradedCorridorCount += 1;
                result.Messages.Add(string.Format(
                    "Week {0}: corridor upkeep slipped on {1} / {2}; rights fell to {3}.",
                    weekIndex,
                    corridorState.DistrictA,
                    corridorState.DistrictB,
                    corridorState.RightLevel == CorridorRightLevel.None ? "None" : corridorState.RightLevel.ToString()));
            }
        }

        private void ProcessDistrictEvents(TerritoryWeeklyMaintenanceResult result, int weekIndex)
        {
            var triggers = _districtsByName.Values
                .Where(district => district != null && !string.IsNullOrWhiteSpace(district.DistrictName))
                .Select(ResolveDistrictEventTrigger)
                .Where(trigger => trigger != null)
                .OrderByDescending(trigger => trigger.Score)
                .ThenBy(trigger => trigger.DistrictState != null ? trigger.DistrictState.DistrictName : string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null || trigger.DistrictState == null || trigger.DistrictState.ActiveEvent == null)
                {
                    continue;
                }

                RefreshDistrictEvent(trigger.DistrictState, trigger, result, weekIndex);
            }

            var activeCount = _districtsByName.Values.Count(district => district != null && district.ActiveEvent != null && district.ActiveEvent.HasData);
            for (int i = 0; i < triggers.Count && activeCount < 3; i++)
            {
                var trigger = triggers[i];
                if (trigger == null
                    || trigger.DistrictState == null
                    || trigger.DistrictState.ActiveEvent != null
                    || trigger.Type == DistrictCrisisType.None
                    || trigger.Score < 0.34f)
                {
                    continue;
                }

                StartDistrictEvent(trigger.DistrictState, trigger, result, weekIndex);
                activeCount += 1;
            }

            result.ActiveDistrictEventCount = _districtsByName.Values.Count(district => district != null && district.ActiveEvent != null && district.ActiveEvent.HasData);
        }

        private void ApplyDistrictEventDeliveryProgress(TerritoryDistrictState districtState, Industry destinationIndustry, string commodity, float deliveredTons, bool viaNpc)
        {
            var activeEvent = districtState != null ? districtState.ActiveEvent : null;
            if (activeEvent == null || deliveredTons <= 0.001f)
            {
                return;
            }

            var reliefFactor = GetDistrictEventCommodityReliefFactor(activeEvent, commodity);
            if (reliefFactor <= 0f && destinationIndustry != null)
            {
                if (activeEvent.CrisisType == DistrictCrisisType.ConstructionSurge && destinationIndustry.IsConstructionSink)
                {
                    reliefFactor = 0.40f;
                }
                else if (activeEvent.CrisisType == DistrictCrisisType.EmergencyRestock && IsServiceSink(destinationIndustry))
                {
                    reliefFactor = 0.25f;
                }
            }

            if (reliefFactor <= 0f)
            {
                return;
            }

            var reliefTons = deliveredTons * reliefFactor * (viaNpc ? 0.65f : 1f);
            if (reliefTons <= 0.01f)
            {
                return;
            }

            activeEvent.DeliveredReliefTons = Math.Min(activeEvent.ResponseTargetTons + 25f, activeEvent.DeliveredReliefTons + reliefTons);
            activeEvent.ReliefDeliveryCount += 1;
            activeEvent.Severity = Clamp01(activeEvent.Severity - Math.Min(0.18f, reliefTons * 0.005f));
            activeEvent.MarketPressureBonus = ResolveDistrictEventMarketPressureBonus(activeEvent.CrisisType, activeEvent.Severity);

            if (activeEvent.DeliveredReliefTons + 0.25f >= activeEvent.ResponseTargetTons)
            {
                districtState.ActiveEvent = null;
                return;
            }

            RefreshDistrictEventPresentation(districtState);
        }

        private DistrictEventTriggerDescriptor ResolveDistrictEventTrigger(TerritoryDistrictState districtState)
        {
            var descriptor = new DistrictEventTriggerDescriptor
            {
                DistrictState = districtState,
                Type = DistrictCrisisType.None,
                PreferredCommodity = string.Empty,
                TriggerSummary = string.Empty,
                ImpactSummary = string.Empty,
                ResponseTargetTons = 0f,
                Score = 0f,
            };

            if (districtState == null || string.IsNullOrWhiteSpace(districtState.DistrictName) || districtState.SiteCount <= 0)
            {
                return descriptor;
            }

            var serviceSiteCount = 0;
            var atRiskServiceSiteCount = 0;
            var gasStationCount = 0;
            var storeCount = 0;
            var constructionSiteCount = 0;
            var servicePenaltySteps = 0;

            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null || !string.Equals(siteState.DistrictName, districtState.DistrictName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var industry = ResolveIndustry(siteState.SiteId);
                if (industry == null)
                {
                    continue;
                }

                if (IsServiceSink(industry))
                {
                    serviceSiteCount += 1;
                    servicePenaltySteps += Math.Max(0, siteState.ServicePenaltySteps);
                    if (IsServiceContractAtRisk(siteState))
                    {
                        atRiskServiceSiteCount += 1;
                    }
                }

                if (industry.IsGasStation)
                {
                    gasStationCount += 1;
                }

                if (industry.IsStore)
                {
                    storeCount += 1;
                }

                if (industry.IsConstructionSink)
                {
                    constructionSiteCount += 1;
                }
            }

            var baseActivityTarget = GetBaseDistrictLicenseTargetTons(districtState);
            var activityRatio = baseActivityTarget > 0.01f
                ? Math.Min(1.5f, districtState.CurrentWeekActivityTons / baseActivityTarget)
                : 1f;
            var activityShortfall = Clamp01(1f - activityRatio);
            var operationalGapRatio = districtState.SiteCount > 0
                ? Clamp01((districtState.SiteCount - districtState.OperationalSites) / (float)districtState.SiteCount)
                : 0f;
            var licenseStress = districtState.LicenseStatus == DistrictLicenseStatus.Suspended
                ? 0.32f
                : (districtState.LicenseStatus == DistrictLicenseStatus.Probation ? 0.18f : 0f);
            var serviceRisk = Clamp01((servicePenaltySteps * 0.22f) + (atRiskServiceSiteCount * 0.10f));
            var competitionPressure = Clamp01(districtState.CompetitivePressure);
            var corridorPressure = Clamp01((districtState.ContestedCorridorCount * 0.10f) + (districtState.HottestCorridorPressure * 0.55f));
            var growthPressure = Clamp01(
                (constructionSiteCount * 0.18f)
                + (districtState.CompetitiveOpportunity * 0.45f)
                + (Math.Max(0f, activityRatio - 1f) * 0.30f)
                + Math.Min(0.12f, districtState.RouteRights * 0.02f));

            var fuelScore = Clamp01(
                (gasStationCount * 0.18f)
                + (serviceSiteCount * 0.06f)
                + (serviceRisk * 0.55f)
                + (activityShortfall * 0.14f));
            var constructionScore = Clamp01(
                (constructionSiteCount * 0.22f)
                + (growthPressure * 0.70f)
                + Math.Min(0.10f, districtState.ControlledDepots * 0.03f));
            var supplyScore = Clamp01(
                (competitionPressure * 0.58f)
                + (corridorPressure * 0.32f)
                + (operationalGapRatio * 0.24f)
                + Math.Min(0.12f, districtState.ActiveCompetitionJobs * 0.02f));
            var restockScore = Clamp01(
                (storeCount * 0.11f)
                + (serviceSiteCount * 0.05f)
                + (activityShortfall * 0.52f)
                + licenseStress
                + Math.Min(0.10f, operationalGapRatio * 0.18f));

            descriptor.Type = DistrictCrisisType.EmergencyRestock;
            descriptor.Score = restockScore;
            descriptor.TriggerSummary = string.Format(
                "Activity gap {0:0}% | Retail/service {1} | Charter {2}",
                activityShortfall * 100f,
                storeCount + serviceSiteCount,
                districtState.LicenseStatus);

            if (fuelScore > descriptor.Score)
            {
                descriptor.Type = DistrictCrisisType.FuelShortage;
                descriptor.Score = fuelScore;
                descriptor.TriggerSummary = string.Format(
                    "Fuel pressure {0:0}% | Gas sites {1} | At-risk stops {2}",
                    serviceRisk * 100f,
                    gasStationCount,
                    atRiskServiceSiteCount);
            }

            if (constructionScore > descriptor.Score)
            {
                descriptor.Type = DistrictCrisisType.ConstructionSurge;
                descriptor.Score = constructionScore;
                descriptor.TriggerSummary = string.Format(
                    "Build pressure {0:0}% | Construction sites {1} | Corridor rights {2}",
                    growthPressure * 100f,
                    constructionSiteCount,
                    districtState.RouteRights);
            }

            if (supplyScore > descriptor.Score)
            {
                descriptor.Type = DistrictCrisisType.SupplyDisruption;
                descriptor.Score = supplyScore;
                descriptor.TriggerSummary = string.Format(
                    "Competition {0:0}% | Contested lanes {1} | Operational gap {2:0}%",
                    competitionPressure * 100f,
                    districtState.ContestedCorridorCount,
                    operationalGapRatio * 100f);
            }

            descriptor.PreferredCommodity = ResolveDistrictEventPreferredCommodity(
                descriptor.Type,
                gasStationCount,
                storeCount,
                constructionSiteCount,
                serviceSiteCount);
            descriptor.ResponseTargetTons = ResolveDistrictEventResponseTargetTons(
                districtState,
                descriptor.Type,
                descriptor.Score,
                serviceSiteCount,
                storeCount,
                constructionSiteCount);
            descriptor.ImpactSummary = BuildDistrictEventImpactSummary(descriptor.Type);
            return descriptor;
        }

        private void StartDistrictEvent(TerritoryDistrictState districtState, DistrictEventTriggerDescriptor trigger, TerritoryWeeklyMaintenanceResult result, int weekIndex)
        {
            if (districtState == null || trigger == null || trigger.Type == DistrictCrisisType.None || trigger.Score < 0.01f)
            {
                return;
            }

            districtState.ActiveEvent = new TerritoryDistrictEventState
            {
                EventId = BuildDistrictEventId(districtState.DistrictName, weekIndex),
                DistrictName = districtState.DistrictName ?? string.Empty,
                CrisisType = trigger.Type,
                PreferredCommodity = CommodityCatalog.Normalize(trigger.PreferredCommodity),
                Severity = Clamp01(Math.Max(0.24f, trigger.Score)),
                MarketPressureBonus = ResolveDistrictEventMarketPressureBonus(trigger.Type, trigger.Score),
                ResponseTargetTons = Math.Max(8f, trigger.ResponseTargetTons),
                DeliveredReliefTons = 0f,
                ReliefDeliveryCount = 0,
                StartedWeekIndex = weekIndex,
                EndsAtWeekIndex = weekIndex + 1,
                LastEscalatedWeekIndex = -1,
                TriggerSummary = trigger.TriggerSummary ?? string.Empty,
                ImpactSummary = trigger.ImpactSummary ?? string.Empty,
            };
            RefreshDistrictEventPresentation(districtState);

            result.CreatedDistrictEventCount += 1;
            result.Messages.Add(string.Format(
                "Week {0}: {1} is active. {2}",
                weekIndex,
                districtState.ActiveEvent.Headline,
                districtState.ActiveEvent.ImpactSummary));
        }

        private void RefreshDistrictEvent(TerritoryDistrictState districtState, DistrictEventTriggerDescriptor trigger, TerritoryWeeklyMaintenanceResult result, int weekIndex)
        {
            var activeEvent = districtState != null ? districtState.ActiveEvent : null;
            if (activeEvent == null)
            {
                return;
            }

            if (activeEvent.DeliveredReliefTons + 0.25f >= activeEvent.ResponseTargetTons)
            {
                var headline = BuildDistrictEventHeadline(districtState != null ? districtState.DistrictName : string.Empty, activeEvent.CrisisType);
                if (districtState != null)
                {
                    districtState.ActiveEvent = null;
                }

                result.ResolvedDistrictEventCount += 1;
                result.Messages.Add(string.Format(
                    "Week {0}: {1} stabilized after {2:0} t of relief cargo.",
                    weekIndex,
                    headline,
                    Math.Max(0f, activeEvent.DeliveredReliefTons)));
                return;
            }

            var triggerScore = trigger != null ? trigger.Score : 0f;
            if (triggerScore < 0.14f && weekIndex >= activeEvent.EndsAtWeekIndex)
            {
                var headline = BuildDistrictEventHeadline(districtState != null ? districtState.DistrictName : string.Empty, activeEvent.CrisisType);
                if (districtState != null)
                {
                    districtState.ActiveEvent = null;
                }

                result.ResolvedDistrictEventCount += 1;
                result.Messages.Add(string.Format("Week {0}: {1} stabilized before it rolled over again.", weekIndex, headline));
                return;
            }

            if (trigger != null && trigger.Type != DistrictCrisisType.None && (trigger.Score >= activeEvent.Severity || weekIndex >= activeEvent.EndsAtWeekIndex))
            {
                activeEvent.CrisisType = trigger.Type;
                if (!string.IsNullOrWhiteSpace(trigger.PreferredCommodity))
                {
                    activeEvent.PreferredCommodity = CommodityCatalog.Normalize(trigger.PreferredCommodity);
                }
            }

            if (trigger != null)
            {
                activeEvent.TriggerSummary = trigger.TriggerSummary ?? string.Empty;
                activeEvent.ImpactSummary = trigger.ImpactSummary ?? string.Empty;
                activeEvent.ResponseTargetTons = Math.Max(activeEvent.ResponseTargetTons, trigger.ResponseTargetTons);
            }

            activeEvent.Severity = Clamp01(Math.Max(activeEvent.Severity * 0.72f, triggerScore));
            activeEvent.MarketPressureBonus = ResolveDistrictEventMarketPressureBonus(activeEvent.CrisisType, activeEvent.Severity);

            if (weekIndex >= activeEvent.EndsAtWeekIndex)
            {
                activeEvent.LastEscalatedWeekIndex = weekIndex;
                activeEvent.EndsAtWeekIndex = weekIndex + 1;
                activeEvent.ResponseTargetTons += Math.Max(6f, (trigger != null ? trigger.ResponseTargetTons : activeEvent.ResponseTargetTons) * 0.25f);
                activeEvent.Severity = Clamp01(Math.Max(activeEvent.Severity + 0.08f, triggerScore + 0.06f));
                activeEvent.MarketPressureBonus = ResolveDistrictEventMarketPressureBonus(activeEvent.CrisisType, activeEvent.Severity);
                result.EscalatedDistrictEventCount += 1;
                result.Messages.Add(string.Format(
                    "Week {0}: {1} rolled into another week. Relief {2:0}/{3:0} t.",
                    weekIndex,
                    BuildDistrictEventHeadline(districtState != null ? districtState.DistrictName : string.Empty, activeEvent.CrisisType),
                    Math.Max(0f, activeEvent.DeliveredReliefTons),
                    Math.Max(0f, activeEvent.ResponseTargetTons)));
            }

            RefreshDistrictEventPresentation(districtState);
        }

        private void RefreshDistrictEventPresentation(TerritoryDistrictState districtState)
        {
            var activeEvent = districtState != null ? districtState.ActiveEvent : null;
            if (activeEvent == null)
            {
                return;
            }

            activeEvent.DistrictName = districtState != null ? districtState.DistrictName ?? string.Empty : activeEvent.DistrictName ?? string.Empty;
            activeEvent.PreferredCommodity = CommodityCatalog.Normalize(activeEvent.PreferredCommodity);
            activeEvent.Headline = BuildDistrictEventHeadline(activeEvent.DistrictName, activeEvent.CrisisType);
            activeEvent.SeverityLabel = ResolveDistrictEventSeverityLabel(activeEvent.Severity);
            activeEvent.StatusText = string.Format(
                "{0} | Relief {1:0}/{2:0} t | Closes week {3}",
                activeEvent.SeverityLabel,
                Math.Max(0f, activeEvent.DeliveredReliefTons),
                Math.Max(0f, activeEvent.ResponseTargetTons),
                Math.Max(0, activeEvent.EndsAtWeekIndex + 1));
            if (string.IsNullOrWhiteSpace(activeEvent.ImpactSummary))
            {
                activeEvent.ImpactSummary = BuildDistrictEventImpactSummary(activeEvent.CrisisType);
            }
        }

        private float GetDistrictEventActivityTargetModifier(TerritoryDistrictState districtState)
        {
            var activeEvent = districtState != null ? districtState.ActiveEvent : null;
            if (activeEvent == null || activeEvent.CrisisType == DistrictCrisisType.None)
            {
                return 0f;
            }

            var severity = Clamp01(activeEvent.Severity);
            switch (activeEvent.CrisisType)
            {
                case DistrictCrisisType.FuelShortage:
                    return 0.10f + (severity * 0.10f);
                case DistrictCrisisType.ConstructionSurge:
                    return 0.12f + (severity * 0.14f);
                case DistrictCrisisType.SupplyDisruption:
                    return 0.08f + (severity * 0.10f);
                case DistrictCrisisType.EmergencyRestock:
                    return 0.12f + (severity * 0.12f);
                default:
                    return 0f;
            }
        }

        private float GetDistrictEventServiceTargetModifier(Industry industry)
        {
            if (industry == null || string.IsNullOrWhiteSpace(industry.DistrictName))
            {
                return 0f;
            }

            var activeEvent = GetDistrictEvent(industry.DistrictName);
            if (activeEvent == null || activeEvent.CrisisType == DistrictCrisisType.None)
            {
                return 0f;
            }

            var severity = Clamp01(activeEvent.Severity);
            switch (activeEvent.CrisisType)
            {
                case DistrictCrisisType.FuelShortage:
                    return industry.IsGasStation
                        ? 0.18f + (severity * 0.12f)
                        : 0.10f + (severity * 0.08f);
                case DistrictCrisisType.ConstructionSurge:
                    return industry.IsConstructionSink
                        ? 0.18f + (severity * 0.10f)
                        : 0f;
                case DistrictCrisisType.SupplyDisruption:
                    return 0.06f + (severity * 0.08f);
                case DistrictCrisisType.EmergencyRestock:
                    return industry.IsStore || industry.IsGasStation
                        ? 0.14f + (severity * 0.10f)
                        : 0.08f + (severity * 0.08f);
                default:
                    return 0f;
            }
        }

        private float GetDistrictEventCorridorTargetModifier(TerritoryCorridorState corridorState)
        {
            if (corridorState == null)
            {
                return 0f;
            }

            return Math.Min(
                0.32f,
                GetDistrictEventCorridorTargetModifier(corridorState.DistrictA)
                + GetDistrictEventCorridorTargetModifier(corridorState.DistrictB));
        }

        private float GetDistrictEventCorridorTargetModifier(string districtName)
        {
            var activeEvent = GetDistrictEvent(districtName);
            if (activeEvent == null || activeEvent.CrisisType == DistrictCrisisType.None)
            {
                return 0f;
            }

            var severity = Clamp01(activeEvent.Severity);
            switch (activeEvent.CrisisType)
            {
                case DistrictCrisisType.FuelShortage:
                    return 0.05f + (severity * 0.05f);
                case DistrictCrisisType.ConstructionSurge:
                    return 0.08f + (severity * 0.08f);
                case DistrictCrisisType.SupplyDisruption:
                    return 0.06f + (severity * 0.10f);
                case DistrictCrisisType.EmergencyRestock:
                    return 0.06f + (severity * 0.07f);
                default:
                    return 0f;
            }
        }

        private static string BuildDistrictEventId(string districtName, int weekIndex)
        {
            return string.Format("district_event_{0}_{1}", SanitizeEventIdPart(districtName), Math.Max(0, weekIndex));
        }

        private static string SanitizeEventIdPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "district";
            }

            var characters = value.Trim()
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character))
                .ToArray();
            return characters.Length > 0 ? new string(characters) : "district";
        }

        private static string BuildDistrictEventHeadline(string districtName, DistrictCrisisType crisisType)
        {
            var label = string.IsNullOrWhiteSpace(districtName) ? "District" : districtName.Trim();
            switch (crisisType)
            {
                case DistrictCrisisType.FuelShortage:
                    return string.Format("{0} fuel shortage", label);
                case DistrictCrisisType.ConstructionSurge:
                    return string.Format("{0} construction surge", label);
                case DistrictCrisisType.SupplyDisruption:
                    return string.Format("{0} supply disruption", label);
                case DistrictCrisisType.EmergencyRestock:
                    return string.Format("{0} emergency restock", label);
                default:
                    return string.Format("{0} live district event", label);
            }
        }

        private static string ResolveDistrictEventSeverityLabel(float severity)
        {
            severity = Clamp01(severity);
            if (severity >= 0.72f)
            {
                return "Critical";
            }

            if (severity >= 0.44f)
            {
                return "Active";
            }

            return "Watch";
        }

        private static string BuildDistrictEventImpactSummary(DistrictCrisisType crisisType)
        {
            switch (crisisType)
            {
                case DistrictCrisisType.FuelShortage:
                    return "Fuel stops and service lanes need relief cargo. Service demand stays elevated until coverage stabilizes.";
                case DistrictCrisisType.ConstructionSurge:
                    return "Build sites are pulling extra tonnage. Charter activity and corridor upkeep targets rise while the surge lasts.";
                case DistrictCrisisType.SupplyDisruption:
                    return "Competition and site outages are choking throughput. Corridor upkeep and district pressure stay elevated.";
                case DistrictCrisisType.EmergencyRestock:
                    return "Retail and service sinks are understocked. Dispatch demand stays elevated until relief cargo lands.";
                default:
                    return "District demand has turned volatile and needs operational relief.";
            }
        }

        private static float ResolveDistrictEventMarketPressureBonus(DistrictCrisisType crisisType, float severity)
        {
            severity = Clamp01(severity);
            switch (crisisType)
            {
                case DistrictCrisisType.FuelShortage:
                    return 0.14f + (severity * 0.24f);
                case DistrictCrisisType.ConstructionSurge:
                    return 0.10f + (severity * 0.18f);
                case DistrictCrisisType.SupplyDisruption:
                    return 0.08f + (severity * 0.16f);
                case DistrictCrisisType.EmergencyRestock:
                    return 0.12f + (severity * 0.20f);
                default:
                    return 0f;
            }
        }

        private static string ResolveDistrictEventPreferredCommodity(DistrictCrisisType crisisType, int gasStationCount, int storeCount, int constructionSiteCount, int serviceSiteCount)
        {
            switch (crisisType)
            {
                case DistrictCrisisType.FuelShortage:
                    return gasStationCount > 0 ? "Fuel" : (serviceSiteCount > 0 ? "Oil" : "Chemicals");
                case DistrictCrisisType.ConstructionSurge:
                    return constructionSiteCount > 0 ? "Concrete" : "Steel";
                case DistrictCrisisType.SupplyDisruption:
                    return constructionSiteCount > 0 ? "MechanicalParts" : "ProcessedFood";
                case DistrictCrisisType.EmergencyRestock:
                    return storeCount > 0 ? "ProcessedFood" : (gasStationCount > 0 ? "Fuel" : "Medicine");
                default:
                    return string.Empty;
            }
        }

        private static float ResolveDistrictEventResponseTargetTons(
            TerritoryDistrictState districtState,
            DistrictCrisisType crisisType,
            float score,
            int serviceSiteCount,
            int storeCount,
            int constructionSiteCount)
        {
            if (districtState == null)
            {
                return 0f;
            }

            var baseTarget = 10f
                + (serviceSiteCount * 4f)
                + (storeCount * 3f)
                + (constructionSiteCount * 5f)
                + Math.Min(10f, districtState.RouteRights * 1.8f)
                + Math.Min(8f, districtState.ControlledSites * 1.5f);

            switch (crisisType)
            {
                case DistrictCrisisType.ConstructionSurge:
                    baseTarget += 8f;
                    break;
                case DistrictCrisisType.SupplyDisruption:
                    baseTarget += 6f;
                    break;
                case DistrictCrisisType.FuelShortage:
                    baseTarget += 4f;
                    break;
            }

            return Math.Max(8f, baseTarget * (0.75f + Math.Min(0.70f, Clamp01(score))));
        }

        private static float GetDistrictEventCommodityReliefFactor(TerritoryDistrictEventState activeEvent, string commodity)
        {
            commodity = CommodityCatalog.Normalize(commodity);
            if (activeEvent == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            if (string.Equals(activeEvent.PreferredCommodity, commodity, StringComparison.OrdinalIgnoreCase))
            {
                return 1f;
            }

            var affinity = CommodityCatalog.GetEventResponseAffinity(activeEvent.PreferredCommodity, commodity);
            if (affinity <= 0f)
            {
                return 0f;
            }

            return Math.Max(0.22f, affinity * 0.90f);
        }

        private void ResetWeeklyTracking()
        {
            foreach (var districtState in _districtsByName.Values)
            {
                if (districtState == null)
                {
                    continue;
                }

                districtState.CurrentWeekActivityCount = 0;
                districtState.CurrentWeekActivityTons = 0f;
            }

            foreach (var siteState in _sitesById.Values)
            {
                if (siteState == null)
                {
                    continue;
                }

                siteState.CurrentWeekServiceDeliveries = 0;
                siteState.CurrentWeekServiceTons = 0f;
            }

            foreach (var corridorState in _corridorsById.Values)
            {
                if (corridorState == null)
                {
                    continue;
                }

                corridorState.CurrentWeekDeliveryCount = 0;
                corridorState.CurrentWeekDeliveredTons = 0f;
            }
        }

        private float GetCorridorWeeklyTargetTons(TerritoryCorridorState corridorState)
        {
            var baseTarget = GetBaseCorridorWeeklyTargetTons(corridorState);
            if (baseTarget <= 0.01f)
            {
                return 0f;
            }

            return Math.Max(14f, baseTarget * (1f + GetDistrictEventCorridorTargetModifier(corridorState)));
        }

        private float GetBaseCorridorWeeklyTargetTons(TerritoryCorridorState corridorState)
        {
            if (corridorState == null || corridorState.RightLevel == CorridorRightLevel.None)
            {
                return 0f;
            }

            var target = 0f;
            switch (corridorState.RightLevel)
            {
                case CorridorRightLevel.Priority:
                    target = 110f;
                    break;
                case CorridorRightLevel.Corridor:
                    target = 55f;
                    break;
                default:
                    target = 22f;
                    break;
            }

            var modifier = 1f;
            modifier -= Math.Min(0.16f, GetDistrictDispatchBonus(corridorState.DistrictA) + GetDistrictDispatchBonus(corridorState.DistrictB));
            modifier += Math.Min(0.18f, (GetDistrictCompetitionPressureValue(corridorState.DistrictA) + GetDistrictCompetitionPressureValue(corridorState.DistrictB)) * 0.09f);
            modifier += Math.Min(0.20f, corridorState.CompetitivePressure * 0.18f);
            modifier += Math.Min(0.08f, corridorState.ActiveCompetitionJobs * 0.015f);
            modifier -= Math.Min(0.05f, corridorState.CompetitiveOpportunity * 0.08f);

            var districtA = GetDistrictState(corridorState.DistrictA);
            var districtB = GetDistrictState(corridorState.DistrictB);
            if (districtA != null && districtA.LicenseStatus == DistrictLicenseStatus.Active)
            {
                modifier -= 0.04f;
            }

            if (districtB != null && districtB.LicenseStatus == DistrictLicenseStatus.Active)
            {
                modifier -= 0.04f;
            }

            var endgame = GetEndgameSummary();
            if (endgame.ActiveDoctrine == CompanyDoctrine.Territorial)
            {
                modifier -= CompanyDoctrineSystem.GetTerritorialLicenseRelief(endgame.ActiveDoctrineEffectiveTier) * 0.45f;
            }
            else if (endgame.ActiveDoctrine == CompanyDoctrine.Service)
            {
                modifier += CompanyDoctrineSystem.GetServiceCorridorTargetPenalty(endgame.ActiveDoctrineEffectiveTier);
            }

            if (endgame.HasLandmarkHeadquarters)
            {
                modifier -= 0.03f;
            }

            return Math.Max(14f, target * Math.Max(0.65f, modifier));
        }

        private float GetCorridorDecayThreshold(TerritoryCorridorState corridorState)
        {
            if (corridorState == null)
            {
                return 2f;
            }

            var threshold = 0f;
            switch (corridorState.RightLevel)
            {
                case CorridorRightLevel.Priority:
                    threshold = 1.5f;
                    break;
                case CorridorRightLevel.Corridor:
                    threshold = 2f;
                    break;
                default:
                    threshold = 2.5f;
                    break;
            }

            threshold += Math.Min(0.7f, GetDistrictSecurityBonus(corridorState.DistrictA) + GetDistrictSecurityBonus(corridorState.DistrictB));
            var endgame = GetEndgameSummary();
            if (endgame.ActiveDoctrine == CompanyDoctrine.Territorial)
            {
                threshold += CompanyDoctrineSystem.GetTerritorialCorridorResilience(endgame.ActiveDoctrineEffectiveTier);
            }
            else if (endgame.ActiveDoctrine == CompanyDoctrine.Service)
            {
                threshold -= CompanyDoctrineSystem.GetServiceCorridorTargetPenalty(endgame.ActiveDoctrineEffectiveTier) * 0.4f;
            }

            if (endgame.HasLandmarkHeadquarters)
            {
                threshold += 0.15f;
            }

            threshold -= Math.Min(0.45f, corridorState.CompetitivePressure * 0.35f);
            threshold += Math.Min(0.16f, corridorState.CompetitiveOpportunity * 0.12f);
            return Math.Max(0.85f, threshold);
        }

        private float GetDistrictCompetitionPressureValue(string districtName)
        {
            var districtState = GetDistrictState(districtName);
            return districtState != null
                ? Clamp01(districtState.CompetitivePressure)
                : 0f;
        }

        private string BuildDistrictCompetitionStatus(TerritoryDistrictState districtState)
        {
            if (districtState == null)
            {
                return string.Empty;
            }

            var carrierSummary = districtState.ActiveCarrierCount > 0
                ? string.Format(
                    " | Carriers {0}{1}",
                    Math.Max(0, districtState.ActiveCarrierCount),
                    !string.IsNullOrWhiteSpace(districtState.DominantCarrierName)
                        ? string.Format(" | Lead {0}", districtState.DominantCarrierName)
                        : string.Empty)
                : string.Empty;
            return string.Format(
                "Competition {0:0}% | Opportunity {1:0}% | Traffic {2} | Lanes {3} | Wins {4}{5}",
                Clamp01(districtState.CompetitivePressure) * 100f,
                Clamp01(districtState.CompetitiveOpportunity) * 100f,
                Math.Max(0, districtState.ActiveCompetitionJobs),
                Math.Max(0, districtState.ContestedCorridorCount),
                Math.Max(0, districtState.CompetitiveWinCount),
                carrierSummary);
        }

        private string BuildCorridorCompetitionStatus(TerritoryCorridorState corridorState)
        {
            if (corridorState == null)
            {
                return string.Empty;
            }

            string posture;
            if (corridorState.CompetitivePressure >= 0.65f)
            {
                posture = "Threatened";
            }
            else if (corridorState.CompetitivePressure >= 0.35f)
            {
                posture = "Contested";
            }
            else if (corridorState.CompetitiveOpportunity >= 0.28f && corridorState.CompetitiveWinCount > 0)
            {
                posture = "Holding";
            }
            else if (corridorState.ActiveCompetitionJobs > 0 || corridorState.ContestedWeekStreak > 0)
            {
                posture = corridorState.RightLevel == CorridorRightLevel.None ? "Open" : "Active";
            }
            else
            {
                posture = corridorState.RightLevel == CorridorRightLevel.None ? "Open" : "Calm";
            }

            var carrierSummary = corridorState.ActiveCarrierCount > 0
                ? string.Format(
                    " | Carriers {0}{1}",
                    Math.Max(0, corridorState.ActiveCarrierCount),
                    !string.IsNullOrWhiteSpace(corridorState.DominantCarrierName)
                        ? string.Format(" | Lead {0}", corridorState.DominantCarrierName)
                        : string.Empty)
                : string.Empty;
            return string.Format(
                "{0} | Competition {1:0}% | Opportunity {2:0}% | Traffic {3} | Holds {4}{5}",
                posture,
                Clamp01(corridorState.CompetitivePressure) * 100f,
                Clamp01(corridorState.CompetitiveOpportunity) * 100f,
                Math.Max(0, corridorState.ActiveCompetitionJobs),
                Math.Max(0, corridorState.CompetitiveWinCount),
                carrierSummary);
        }

        private CompanyEndgameSummary GetEndgameSummary()
        {
            return _getEndgameSummary != null
                ? (_getEndgameSummary() ?? new CompanyEndgameSummary())
                : new CompanyEndgameSummary();
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private string BuildServiceContractStatus(Industry industry, TerritorySiteState siteState)
        {
            if (industry == null || siteState == null || !IsServiceSink(industry))
            {
                return string.Empty;
            }

            if (!industry.IsOwned)
            {
                return ServiceStatusCatalog.ContractOpenMarket;
            }

            if (siteState.ServicePenaltySteps >= 2)
            {
                return ServiceStatusCatalog.ContractAtRisk;
            }

            if (siteState.ServicePenaltySteps == 1)
            {
                return ServiceStatusCatalog.ContractWatch;
            }

            if (siteState.ServiceTargetMetLastWeek)
            {
                return ServiceStatusCatalog.ContractSecured;
            }

            return ServiceStatusCatalog.ContractAwaitingWeeklyService;
        }

        private static bool IsServiceSinkStocked(Industry industry)
        {
            return industry != null && industry.GetInputStockTotal() > ServiceSinkPassiveIncomeStockThresholdTons;
        }

        private static float ComputeServiceSiteWeeklyStaffingCost(Industry industry)
        {
            return ServiceSiteEconomyPolicy.ComputeWeeklyStaffingCost(industry);
        }

        private string BuildServiceSinkPassiveIncomeOperationalStatus(Industry industry, TerritorySiteState siteState)
        {
            if (industry == null || siteState == null || !IsServiceSink(industry))
            {
                return string.Empty;
            }

            if (!industry.IsOwned)
            {
                return ServiceStatusCatalog.ContractOpenMarket;
            }

            if (!siteState.SiteOperatorAssigned)
            {
                return ServiceStatusCatalog.StatusNoStaffAssigned;
            }

            if (!IsServiceSinkStocked(industry))
            {
                return ServiceStatusCatalog.StatusUnderstocked;
            }

            if (siteState.ServicePenaltySteps >= 2)
            {
                return ServiceStatusCatalog.PassiveIncomeActiveAtRisk;
            }

            if (siteState.ServicePenaltySteps == 1)
            {
                return ServiceStatusCatalog.PassiveIncomeActiveWatch;
            }

            return ServiceStatusCatalog.PassiveIncomeActive;
        }

        private string BuildServiceSinkPassiveIncomeWeeklyOutcome(Industry industry, TerritorySiteState siteState, bool awardedPassiveIncome)
        {
            if (industry == null || siteState == null || !industry.IsOwned)
            {
                return string.Empty;
            }

            if (awardedPassiveIncome)
            {
                if (siteState.ServicePenaltySteps >= 2)
                {
                    return ServiceStatusCatalog.PassiveIncomePaidLastWeekAtRisk;
                }

                if (siteState.ServicePenaltySteps == 1)
                {
                    return ServiceStatusCatalog.PassiveIncomePaidLastWeekWatch;
                }

                return ServiceStatusCatalog.PassiveIncomePaidLastWeek;
            }

            if (!siteState.SiteOperatorAssigned)
            {
                return ServiceStatusCatalog.PassiveIncomeMissedLastWeekNoStaff;
            }

            if (!IsServiceSinkStocked(industry))
            {
                return ServiceStatusCatalog.PassiveIncomeMissedLastWeekUnderstocked;
            }

            return ServiceStatusCatalog.PassiveIncomeMissedLastWeek;
        }

        private string BuildCorridorStatus(TerritoryCorridorState corridorState)
        {
            if (corridorState == null)
            {
                return "Dormant";
            }

            if (corridorState.RightLevel == CorridorRightLevel.None)
            {
                return corridorState.ActiveCompetitionJobs > 0 || corridorState.CompetitivePressure >= 0.18f
                    ? "Open | Competition active"
                    : "Dormant";
            }

            var threshold = GetCorridorDecayThreshold(corridorState);
            if (corridorState.DecayPressure >= threshold * 0.75f)
            {
                return string.Format(
                    "At risk ({0:0}/{1:0} t){2}",
                    corridorState.CurrentWeekDeliveredTons,
                    corridorState.RequiredWeeklyDeliveredTons,
                    corridorState.CompetitivePressure >= 0.35f ? " | Contested" : string.Empty);
            }

            if (corridorState.DecayPressure > 0.01f)
            {
                return string.Format(
                    "Watch ({0:0}/{1:0} t){2}",
                    corridorState.CurrentWeekDeliveredTons,
                    corridorState.RequiredWeeklyDeliveredTons,
                    corridorState.ActiveCompetitionJobs > 0 ? " | Competition active" : string.Empty);
            }

            return string.Format(
                "Stable ({0:0}/{1:0} t){2}",
                corridorState.CurrentWeekDeliveredTons,
                corridorState.RequiredWeeklyDeliveredTons,
                corridorState.CompetitivePressure >= 0.35f ? " | Contested" : string.Empty);
        }

        private static float GetFranchiseOperationsCost(TerritoryFranchiseLevel franchiseLevel)
        {
            switch (franchiseLevel)
            {
                case TerritoryFranchiseLevel.Signature:
                    return 1600f;
                case TerritoryFranchiseLevel.Preferred:
                    return 700f;
                default:
                    return 0f;
            }
        }

        private static float GetCorridorOperationsCost(CorridorRightLevel rightLevel)
        {
            switch (rightLevel)
            {
                case CorridorRightLevel.Priority:
                    return 2500f;
                case CorridorRightLevel.Corridor:
                    return 1350f;
                case CorridorRightLevel.ServicePermit:
                    return 650f;
                default:
                    return 0f;
            }
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

        private static bool TryResolveDistrictDebugTargetScore(string reputationLabel, out float targetScore)
        {
            switch ((reputationLabel ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "DOMINANT":
                    targetScore = 130f;
                    return true;
                case "ESTABLISHED":
                    targetScore = 75f;
                    return true;
                case "EMERGING":
                    targetScore = 44f;
                    return true;
                case "UNKNOWN":
                    targetScore = 10f;
                    return true;
                default:
                    targetScore = 0f;
                    return false;
            }
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

        private static float GetDepotSpecializationSwapCost(Industry industry)
        {
            var tierRank = GetTierRank(industry != null ? industry.OwnershipTier : SiteOwnershipTier.Local);
            return 5500f + (tierRank * 1200f);
        }

        private static string FormatDepotSpecialization(DepotSpecialization specialization)
        {
            switch (specialization)
            {
                case DepotSpecialization.Dispatch:
                    return "Dispatch";
                case DepotSpecialization.Maintenance:
                    return "Maintenance";
                case DepotSpecialization.Security:
                    return "Security";
                case DepotSpecialization.Support:
                    return "Support";
                default:
                    return "General";
            }
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

        private static int GetTotalStaffCount(TerritorySiteState siteState)
        {
            return siteState == null
                ? 0
                : Math.Max(0, siteState.LoaderCount)
                    + Math.Max(0, siteState.MechanicCount)
                    + Math.Max(0, siteState.GuardCount)
                    + Math.Max(0, siteState.ManagerCount);
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

        private static int GetWeekIndex(int currentInGameMinute)
        {
            return currentInGameMinute <= 0 ? 0 : currentInGameMinute / MinutesPerWeek;
        }
    }

    public sealed class TerritoryOperationsSummary
    {
        public TerritoryOperationsSummary()
        {
            Districts = new List<TerritoryDistrictOperationsEntry>();
        }

        public float WeeklyCost { get; set; }
        public float CharterCost { get; set; }
        public float InfrastructureCost { get; set; }
        public int ControlledDistrictCount { get; set; }
        public int ChargedDistrictCount { get; set; }
        public int ManagedDistrictCount { get; set; }
        public int ActiveLicensedDistrictCount { get; set; }
        public int ProbationDistrictCount { get; set; }
        public int SuspendedDistrictCount { get; set; }
        public int ActiveCorridorCount { get; set; }
        public int AtRiskCorridorCount { get; set; }
        public int SupportSiteCount { get; set; }
        public int StaffCount { get; set; }
        public int PremiumFranchiseCount { get; set; }
        public int AtRiskServiceSiteCount { get; set; }
        public List<TerritoryDistrictOperationsEntry> Districts { get; private set; }
    }

    public sealed class TerritoryDistrictOperationsEntry
    {
        public string DistrictName { get; set; }
        public string ReputationLabel { get; set; }
        public float AdministrationCost { get; set; }
        public float SupportSiteCost { get; set; }
        public float StaffCost { get; set; }
        public float FranchiseCost { get; set; }
        public float CorridorCost { get; set; }
        public DistrictLicenseStatus LicenseStatus { get; set; }
        public float LicenseActivityTons { get; set; }
        public float LicenseTargetTons { get; set; }
        public int StaffCount { get; set; }
        public int FranchiseSites { get; set; }
        public int CorridorRiskCount { get; set; }
        public int ServiceRiskCount { get; set; }

        public float TotalWeeklyCost
        {
            get { return AdministrationCost + SupportSiteCost + StaffCost + FranchiseCost + CorridorCost; }
        }
    }

    public sealed class TerritoryOperationsChargeResult
    {
        public int ChargeCount { get; set; }
        public float WeeklyAmount { get; set; }
        public float TotalAmount { get; set; }
        public TerritoryOperationsSummary Summary { get; set; }
    }

    public sealed class TerritoryWeeklyMaintenanceResult
    {
        public TerritoryWeeklyMaintenanceResult()
        {
            Messages = new List<string>();
        }

        public int ProcessedWeekCount { get; set; }
        public int SuspendedDistrictCount { get; set; }
        public int DegradedCorridorCount { get; set; }
        public int MissedServiceContractCount { get; set; }
        public int HighCompetitionDistrictCount { get; set; }
        public int CompetitiveWinCount { get; set; }
        public int ActiveDistrictEventCount { get; set; }
        public int CreatedDistrictEventCount { get; set; }
        public int ResolvedDistrictEventCount { get; set; }
        public int EscalatedDistrictEventCount { get; set; }
        public float ServiceSinkPassiveIncome { get; set; }
        public float ServiceSinkStaffingExpense { get; set; }
        public List<string> Messages { get; private set; }
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
        public TerritoryFranchiseLevel EffectiveFranchiseLevel { get; set; }
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
        public DepotSpecialization DepotSpecialization { get; set; }
        public int CurrentWeekServiceDeliveries { get; set; }
        public float CurrentWeekServiceTons { get; set; }
        public float RequiredWeeklyServiceTons { get; set; }
        public int ServicePenaltySteps { get; set; }
        public int ServiceSuccessStreak { get; set; }
        public bool ServiceTargetMetLastWeek { get; set; }
        public string ServiceContractStatus { get; set; }
        public bool SiteOperatorAssigned { get; set; }
        public bool PassiveIncomeStockReady { get; set; }
        public bool PassiveIncomeOperational { get; set; }
        public string PassiveIncomeStatus { get; set; }
        public float LastPassiveIncomeAmount { get; set; }
        public int LastPassiveIncomeWeekIndex { get; set; } = -1;
        public string LastPassiveIncomeStatus { get; set; }
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
        public DistrictLicenseStatus LicenseStatus { get; set; }
        public int LicenseStrikeCount { get; set; }
        public int CurrentWeekActivityCount { get; set; }
        public float CurrentWeekActivityTons { get; set; }
        public float RequiredWeeklyActivityTons { get; set; }
        public float WeeklyLicenseCost { get; set; }
        public float CompetitivePressure { get; set; }
        public float CompetitiveOpportunity { get; set; }
        public int ActiveCompetitionJobs { get; set; }
        public int ActiveCarrierCount { get; set; }
        public string DominantCarrierId { get; set; }
        public string DominantCarrierName { get; set; }
        public int VisibleCompetitionCount { get; set; }
        public float LastCompetitiveTons { get; set; }
        public int CompetitiveResponseCount { get; set; }
        public int CompetitiveWinCount { get; set; }
        public int ContestedCorridorCount { get; set; }
        public int CorridorHoldCount { get; set; }
        public string HottestCorridorName { get; set; }
        public float HottestCorridorPressure { get; set; }
        public string CompetitionStatus { get; set; }
        public TerritoryDistrictEventState ActiveEvent { get; set; }

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
            RequiredWeeklyActivityTons = 0f;
            WeeklyLicenseCost = 0f;
            ContestedCorridorCount = 0;
            CorridorHoldCount = 0;
            HottestCorridorName = string.Empty;
            HottestCorridorPressure = 0f;
        }
    }

    public sealed class TerritoryDistrictEventState
    {
        public string EventId { get; set; }
        public string DistrictName { get; set; }
        public DistrictCrisisType CrisisType { get; set; }
        public string PreferredCommodity { get; set; }
        public float Severity { get; set; }
        public float MarketPressureBonus { get; set; }
        public float ResponseTargetTons { get; set; }
        public float DeliveredReliefTons { get; set; }
        public int ReliefDeliveryCount { get; set; }
        public int StartedWeekIndex { get; set; } = -1;
        public int EndsAtWeekIndex { get; set; } = -1;
        public int LastEscalatedWeekIndex { get; set; } = -1;
        public string TriggerSummary { get; set; }
        public string ImpactSummary { get; set; }
        public string Headline { get; set; }
        public string SeverityLabel { get; set; }
        public string StatusText { get; set; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(EventId)
                    || CrisisType != DistrictCrisisType.None
                    || !string.IsNullOrWhiteSpace(PreferredCommodity)
                    || ResponseTargetTons > 0.001f;
            }
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
        public int CurrentWeekDeliveryCount { get; set; }
        public float CurrentWeekDeliveredTons { get; set; }
        public float RequiredWeeklyDeliveredTons { get; set; }
        public float DecayPressure { get; set; }
        public string UpkeepStatus { get; set; }
        public float CompetitivePressure { get; set; }
        public float CompetitiveOpportunity { get; set; }
        public int ActiveCompetitionJobs { get; set; }
        public int ActiveCarrierCount { get; set; }
        public string DominantCarrierId { get; set; }
        public string DominantCarrierName { get; set; }
        public int VisibleCompetitionCount { get; set; }
        public float LastCompetitiveTons { get; set; }
        public int CompetitiveWinCount { get; set; }
        public int ContestedWeekStreak { get; set; }
        public string CompetitionStatus { get; set; }
    }

    public sealed class TerritoryPersistenceSnapshot
    {
        public TerritoryPersistenceSnapshot()
        {
            Sites = new List<TerritorySiteSnapshot>();
            Districts = new List<TerritoryDistrictSnapshot>();
            Corridors = new List<TerritoryCorridorSnapshot>();
            DistrictReputationOffsets = new List<TerritoryDistrictReputationOffsetSnapshot>();
        }

        public int LastOperationsChargeWeekIndex { get; set; } = -1;
        public int LastMaintenanceWeekIndex { get; set; } = -1;
        public List<TerritorySiteSnapshot> Sites { get; private set; }
        public List<TerritoryDistrictSnapshot> Districts { get; private set; }
        public List<TerritoryCorridorSnapshot> Corridors { get; private set; }
        public List<TerritoryDistrictReputationOffsetSnapshot> DistrictReputationOffsets { get; private set; }
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
        public DepotSpecialization DepotSpecialization { get; set; }
        public int CurrentWeekServiceDeliveries { get; set; }
        public float CurrentWeekServiceTons { get; set; }
        public int ServicePenaltySteps { get; set; }
        public int ServiceSuccessStreak { get; set; }
        public bool ServiceTargetMetLastWeek { get; set; }
        public bool SiteOperatorAssigned { get; set; }
        public float LastPassiveIncomeAmount { get; set; }
        public int LastPassiveIncomeWeekIndex { get; set; } = -1;
        public string LastPassiveIncomeStatus { get; set; }
    }

    public sealed class TerritoryDistrictSnapshot
    {
        public string DistrictName { get; set; }
        public DistrictLicenseStatus LicenseStatus { get; set; }
        public int LicenseStrikeCount { get; set; }
        public int CurrentWeekActivityCount { get; set; }
        public float CurrentWeekActivityTons { get; set; }
        public float CompetitivePressure { get; set; }
        public float CompetitiveOpportunity { get; set; }
        public int ActiveCompetitionJobs { get; set; }
        public int ActiveCarrierCount { get; set; }
        public string DominantCarrierId { get; set; }
        public string DominantCarrierName { get; set; }
        public int VisibleCompetitionCount { get; set; }
        public float CompetitiveTons { get; set; }
        public int CompetitiveResponseCount { get; set; }
        public int CompetitiveWinCount { get; set; }
        public TerritoryDistrictEventSnapshot ActiveEvent { get; set; }
    }

    public sealed class TerritoryDistrictEventSnapshot
    {
        public string EventId { get; set; }
        public string DistrictName { get; set; }
        public DistrictCrisisType CrisisType { get; set; }
        public string PreferredCommodity { get; set; }
        public float Severity { get; set; }
        public float MarketPressureBonus { get; set; }
        public float ResponseTargetTons { get; set; }
        public float DeliveredReliefTons { get; set; }
        public int ReliefDeliveryCount { get; set; }
        public int StartedWeekIndex { get; set; } = -1;
        public int EndsAtWeekIndex { get; set; } = -1;
        public int LastEscalatedWeekIndex { get; set; } = -1;
        public string TriggerSummary { get; set; }
        public string ImpactSummary { get; set; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(EventId)
                    || CrisisType != DistrictCrisisType.None
                    || !string.IsNullOrWhiteSpace(PreferredCommodity)
                    || ResponseTargetTons > 0.001f;
            }
        }
    }

    public sealed class TerritoryCorridorSnapshot
    {
        public string DistrictA { get; set; }
        public string DistrictB { get; set; }
        public int DeliveryCount { get; set; }
        public float TotalDeliveredTons { get; set; }
        public CorridorRightLevel RightLevel { get; set; }
        public int CurrentWeekDeliveryCount { get; set; }
        public float CurrentWeekDeliveredTons { get; set; }
        public float DecayPressure { get; set; }
        public float CompetitivePressure { get; set; }
        public float CompetitiveOpportunity { get; set; }
        public int ActiveCompetitionJobs { get; set; }
        public int ActiveCarrierCount { get; set; }
        public string DominantCarrierId { get; set; }
        public string DominantCarrierName { get; set; }
        public int VisibleCompetitionCount { get; set; }
        public float CompetitiveTons { get; set; }
        public int CompetitiveWinCount { get; set; }
        public int ContestedWeekStreak { get; set; }
    }

    public sealed class TerritoryDistrictReputationOffsetSnapshot
    {
        public string DistrictName { get; set; }
        public float Offset { get; set; }
    }

    internal sealed class DistrictEventTriggerDescriptor
    {
        public TerritoryDistrictState DistrictState { get; set; }
        public DistrictCrisisType Type { get; set; }
        public string PreferredCommodity { get; set; }
        public string TriggerSummary { get; set; }
        public string ImpactSummary { get; set; }
        public float ResponseTargetTons { get; set; }
        public float Score { get; set; }
    }
}