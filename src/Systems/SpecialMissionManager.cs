using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class SpecialMissionManager
    {
        private static readonly DateTime InGameEpoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private const int InGameMinutesPerDay = 24 * 60;
        private const int InGameMinutesPerWeek = 7 * InGameMinutesPerDay;
        private const float VehicleMarkerRadius = 4.25f;
        private const float VehicleMarkerHeight = 1.6f;
        private const float ObjectiveInteractDistance = 8f;

        private readonly TerritoryManager _territoryManager;
        private readonly FleetManager _fleetManager;
        private readonly Action<float> _awardProfit;
        private readonly Action<string, int> _showStatus;
        private readonly Action _markUiDirty;
        private readonly Func<int> _getCurrentInGameMinute;
        private readonly Dictionary<string, SpecialMissionDefinition> _definitionsById;
        private readonly Dictionary<string, int> _completionCounts;
        private readonly Dictionary<string, int> _lastCompletedInGameMinuteByMissionId;
        private readonly HashSet<string> _announcedAvailableMissionIds;
        private ActiveSpecialMissionRuntime _activeMission;
        private int _lastAvailabilityScanInGameMinute;

        public SpecialMissionManager(
            string configPath,
            TerritoryManager territoryManager,
            FleetManager fleetManager,
            Action<float> awardProfit,
            Action<string, int> showStatus,
            Action markUiDirty,
            Func<int> getCurrentInGameMinute)
        {
            _territoryManager = territoryManager;
            _fleetManager = fleetManager;
            _awardProfit = awardProfit;
            _showStatus = showStatus;
            _markUiDirty = markUiDirty;
            _getCurrentInGameMinute = getCurrentInGameMinute;
            _completionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _lastCompletedInGameMinuteByMissionId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _announcedAvailableMissionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _lastAvailabilityScanInGameMinute = -1;

            Catalog = SpecialMissionCatalog.Load(configPath);
            _definitionsById = Catalog.Definitions
                .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id))
                .GroupBy(definition => definition.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        public SpecialMissionCatalog Catalog { get; }

        public IEnumerable<SpecialMissionDefinition> Definitions
        {
            get { return _definitionsById.Values.OrderBy(definition => definition.Category).ThenBy(definition => definition.Name); }
        }

        public bool HasActiveMission
        {
            get { return _activeMission != null; }
        }

        public string ActiveMissionId
        {
            get { return _activeMission != null ? _activeMission.Definition.Id : string.Empty; }
        }

        public string ActiveMissionName
        {
            get { return _activeMission != null ? _activeMission.Definition.Name : string.Empty; }
        }

        public string ActiveObjective
        {
            get { return _activeMission != null ? _activeMission.CurrentObjective : string.Empty; }
        }

        public string ActiveObjectiveDetail
        {
            get { return _activeMission != null ? _activeMission.CurrentDetail : string.Empty; }
        }

        public bool HasDefinitions
        {
            get { return _definitionsById.Count > 0; }
        }

        public IReadOnlyDictionary<string, int> CompletionCounts
        {
            get { return _completionCounts; }
        }

        public SpecialMissionDefinition GetDefinition(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return null;
            }

            SpecialMissionDefinition definition;
            return _definitionsById.TryGetValue(missionId.Trim(), out definition)
                ? definition
                : null;
        }

        public int GetCompletionCount(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return 0;
            }

            int count;
            return _completionCounts.TryGetValue(missionId.Trim(), out count)
                ? count
                : 0;
        }

        public bool IsMissionUnlocked(SpecialMissionDefinition definition, out string detail)
        {
            detail = string.Empty;
            if (definition == null)
            {
                detail = "Mission definition unavailable.";
                return false;
            }

            if (_territoryManager == null || definition.Unlock == null || definition.Unlock.DistrictNames.Count == 0)
            {
                detail = "Available now.";
                return true;
            }

            var minimumRatio = definition.Unlock.MinInfluenceRatio;
            for (int i = 0; i < definition.Unlock.DistrictNames.Count; i++)
            {
                var districtName = definition.Unlock.DistrictNames[i];
                var state = _territoryManager.GetDistrictState(districtName);
                if (state != null && state.InfluenceRatio >= minimumRatio)
                {
                    detail = string.Format("Unlocked by {0} district progress.", districtName);
                    return true;
                }
            }

            detail = BuildUnlockProgressDetail(definition.Unlock);
            return false;
        }

        public bool CanAcceptMission(string missionId, out string detail)
        {
            var definition = GetDefinition(missionId);
            if (definition == null)
            {
                detail = "Mission definition unavailable.";
                return false;
            }

            if (!definition.Repeatable && GetCompletionCount(definition.Id) > 0)
            {
                detail = "This mission has already been completed on this save.";
                return false;
            }

            var availabilityDelayRemainingMinutes = GetAvailabilityDelayRemainingMinutes(definition);
            if (availabilityDelayRemainingMinutes > 0)
            {
                detail = BuildAvailabilityDelayDetail(definition, availabilityDelayRemainingMinutes);
                return false;
            }

            if (!IsMissionUnlocked(definition, out detail))
            {
                return false;
            }

            if (!MeetsMissionSpecificAvailabilityRequirements(definition, out detail))
            {
                return false;
            }

            var cooldownRemainingMinutes = GetRepeatCooldownRemainingMinutes(definition);
            if (cooldownRemainingMinutes > 0)
            {
                detail = BuildRepeatCooldownDetail(definition, cooldownRemainingMinutes);
                return false;
            }

            return true;
        }

        public IReadOnlyList<SpecialMissionListing> GetMissionListings()
        {
            var listings = new List<SpecialMissionListing>();
            foreach (var definition in Definitions)
            {
                var unlocked = IsMissionUnlocked(definition, out var availabilityDetail);
                var availabilityDelayRemainingMinutes = GetAvailabilityDelayRemainingMinutes(definition);
                if (availabilityDelayRemainingMinutes > 0)
                {
                    unlocked = false;
                    availabilityDetail = BuildAvailabilityDelayDetail(definition, availabilityDelayRemainingMinutes);
                }
                else if (unlocked && !MeetsMissionSpecificAvailabilityRequirements(definition, out availabilityDetail))
                {
                    unlocked = false;
                }

                var completionCount = GetCompletionCount(definition.Id);
                var cooldownRemainingMinutes = unlocked
                    ? GetRepeatCooldownRemainingMinutes(definition)
                    : 0;
                var isActive = _activeMission != null
                    && string.Equals(_activeMission.Definition.Id, definition.Id, StringComparison.OrdinalIgnoreCase);
                if (unlocked && cooldownRemainingMinutes > 0)
                {
                    availabilityDetail = BuildRepeatCooldownDetail(definition, cooldownRemainingMinutes);
                }
                else if (unlocked && !definition.Repeatable && completionCount > 0)
                {
                    availabilityDetail = "This mission has already been completed on this save.";
                }

                var canAccept = unlocked
                    && cooldownRemainingMinutes <= 0
                    && _activeMission == null
                    && (definition.Repeatable || completionCount <= 0);
                listings.Add(new SpecialMissionListing
                {
                    MissionId = definition.Id,
                    Category = definition.Category,
                    Name = definition.Name,
                    Summary = definition.Summary,
                    Description = definition.Description,
                    Reward = definition.Reward,
                    Repeatable = definition.Repeatable,
                    IsUnlocked = unlocked,
                    IsActive = isActive,
                    CanAccept = canAccept,
                    CompletionCount = completionCount,
                    RepeatCooldownInGameMinutes = definition.RepeatCooldownInGameMinutes,
                    RepeatCooldownInGameMonths = definition.RepeatCooldownInGameMonths,
                    RepeatCooldownRemainingMinutes = cooldownRemainingMinutes,
                    AvailabilityDetail = isActive
                        ? _activeMission.CurrentDetail
                        : availabilityDetail,
                    Objective = isActive
                        ? _activeMission.CurrentObjective
                        : string.Empty,
                });
            }

            return listings;
        }

        public bool TryAcceptMission(string missionId)
        {
            if (_activeMission != null)
            {
                ShowStatus(string.Format("Complete or cancel '{0}' before starting another mission.", _activeMission.Definition.Name));
                return false;
            }

            var definition = GetDefinition(missionId);
            if (definition == null)
            {
                ShowStatus("Mission definition unavailable.");
                return false;
            }

            if (!CanAcceptMission(missionId, out var detail))
            {
                ShowStatus(detail);
                return false;
            }

            var runtime = CreateRuntime(definition, null, out var error);
            if (runtime == null)
            {
                ShowStatus(string.IsNullOrWhiteSpace(error)
                    ? "Mission runtime could not be created."
                    : error);
                return false;
            }

            _activeMission = runtime;
            _activeMission.OnActivated(false);
            MarkUiDirty();
            ShowStatus(string.Format("Special mission accepted: {0}.", definition.Name), 4500);
            return true;
        }

        public bool TryForceStartMission(string missionId, out string detail)
        {
            detail = string.Empty;
            var definition = GetDefinition(missionId);
            if (definition == null)
            {
                detail = "Mission definition unavailable.";
                return false;
            }

            var replacedMissionName = _activeMission != null
                ? _activeMission.Definition.Name
                : string.Empty;
            if (_activeMission != null)
            {
                CleanupActiveMission();
            }

            var runtime = CreateRuntime(definition, null, out var error);
            if (runtime == null)
            {
                detail = string.IsNullOrWhiteSpace(error)
                    ? "Mission runtime could not be created."
                    : error;
                return false;
            }

            _activeMission = runtime;
            _activeMission.OnActivated(false);
            MarkUiDirty();
            detail = string.IsNullOrWhiteSpace(replacedMissionName)
                ? string.Format("Debug mission triggered: {0}.", definition.Name)
                : string.Format("Debug mission triggered: {0}. Replaced active mission {1}.", definition.Name, replacedMissionName);
            return true;
        }

        public bool CancelActiveMission(bool notifyPlayer = true)
        {
            if (_activeMission == null)
            {
                if (notifyPlayer)
                {
                    ShowStatus("No active special mission.");
                }

                return false;
            }

            var missionName = _activeMission.Definition.Name;
            CleanupActiveMission();
            if (notifyPlayer)
            {
                ShowStatus(string.Format("Cancelled special mission: {0}.", missionName), 4000);
            }

            return true;
        }

        public void Update(Ped player, int gameTime)
        {
            RefreshMissionAvailabilityAnnouncements();
            if (_activeMission == null)
            {
                return;
            }

            _activeMission.Update(player, gameTime);
        }

        public bool HandleInteract(Ped player)
        {
            return _activeMission != null && _activeMission.HandleInteract(player);
        }

        public void ResetState(bool clearProgress = true)
        {
            CleanupActiveMission();
            _lastAvailabilityScanInGameMinute = -1;
            if (clearProgress)
            {
                _completionCounts.Clear();
                _lastCompletedInGameMinuteByMissionId.Clear();
                _announcedAvailableMissionIds.Clear();
            }

            MarkUiDirty();
        }

        public void SetCompletionCount(string missionId, int completionCount)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return;
            }

            if (completionCount <= 0)
            {
                _completionCounts.Remove(missionId.Trim());
                return;
            }

            _completionCounts[missionId.Trim()] = completionCount;
        }

        public int GetLastCompletedInGameMinute(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return 0;
            }

            int clockMinute;
            return _lastCompletedInGameMinuteByMissionId.TryGetValue(missionId.Trim(), out clockMinute)
                ? clockMinute
                : 0;
        }

        public void SetLastCompletedInGameMinute(string missionId, int clockMinute)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return;
            }

            var key = missionId.Trim();
            if (clockMinute <= 0)
            {
                _lastCompletedInGameMinuteByMissionId.Remove(key);
                return;
            }

            _lastCompletedInGameMinuteByMissionId[key] = clockMinute;
        }

        public void IncrementCompletionCount(string missionId)
        {
            var current = GetCompletionCount(missionId);
            SetCompletionCount(missionId, current + 1);
        }

        public SpecialMissionPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new SpecialMissionPersistenceSnapshot();
            foreach (var pair in _completionCounts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
                {
                    continue;
                }

                snapshot.CompletedMissions.Add(new SpecialMissionCompletionSnapshot
                {
                    MissionId = pair.Key,
                    CompletionCount = pair.Value,
                    LastCompletedInGameMinute = GetLastCompletedInGameMinute(pair.Key),
                });
            }

            foreach (var missionId in _announcedAvailableMissionIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(missionId))
                {
                    continue;
                }

                snapshot.AvailableMissionAnnouncements.Add(new SpecialMissionAvailabilitySnapshot
                {
                    MissionId = missionId,
                });
            }

            if (_activeMission != null)
            {
                snapshot.ActiveMission = _activeMission.CreateSnapshot();
            }

            return snapshot.HasData ? snapshot : null;
        }

        public void ApplyPersistenceSnapshot(SpecialMissionPersistenceSnapshot snapshot)
        {
            ResetState(true);
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.CompletedMissions != null)
            {
                for (int i = 0; i < snapshot.CompletedMissions.Count; i++)
                {
                    var entry = snapshot.CompletedMissions[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.MissionId) || entry.CompletionCount <= 0)
                    {
                        continue;
                    }

                    var missionId = entry.MissionId.Trim();
                    _completionCounts[missionId] = entry.CompletionCount;
                    if (entry.LastCompletedInGameMinute > 0)
                    {
                        _lastCompletedInGameMinuteByMissionId[missionId] = entry.LastCompletedInGameMinute;
                    }
                }
            }

            if (snapshot.AvailableMissionAnnouncements != null)
            {
                for (int i = 0; i < snapshot.AvailableMissionAnnouncements.Count; i++)
                {
                    var entry = snapshot.AvailableMissionAnnouncements[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.MissionId))
                    {
                        continue;
                    }

                    _announcedAvailableMissionIds.Add(entry.MissionId.Trim());
                }
            }

            if (snapshot.ActiveMission != null && !string.IsNullOrWhiteSpace(snapshot.ActiveMission.MissionId))
            {
                var definition = GetDefinition(snapshot.ActiveMission.MissionId);
                var runtime = CreateRuntime(definition, snapshot.ActiveMission, out var error);
                if (runtime != null)
                {
                    _activeMission = runtime;
                    _activeMission.OnActivated(true);
                }
                else if (!string.IsNullOrWhiteSpace(error))
                {
                    ShowStatus(error, 4500);
                }
            }

            MarkUiDirty();
        }

        public void Shutdown()
        {
            CleanupActiveMission();
        }

        private string BuildUnlockProgressDetail(SpecialMissionUnlockRequirement unlock)
        {
            if (unlock == null || unlock.DistrictNames.Count == 0)
            {
                return "No unlock requirement configured.";
            }

            var minimumPercent = unlock.MinInfluenceRatio * 100f;
            var progress = new List<string>();
            for (int i = 0; i < unlock.DistrictNames.Count; i++)
            {
                var districtName = unlock.DistrictNames[i];
                var state = _territoryManager != null ? _territoryManager.GetDistrictState(districtName) : null;
                var percent = state != null ? state.InfluenceRatio * 100f : 0f;
                progress.Add(string.Format("{0} {1:0}%/{2:0}%", districtName, percent, minimumPercent));
            }

            return string.Join(" | ", progress.ToArray());
        }

        private int GetRepeatCooldownRemainingMinutes(SpecialMissionDefinition definition)
        {
            if (definition == null || (!definition.HasRepeatCooldown))
            {
                return 0;
            }

            if (GetCompletionCount(definition.Id) <= 0)
            {
                return 0;
            }

            var lastCompletedInGameMinute = GetLastCompletedInGameMinute(definition.Id);
            if (lastCompletedInGameMinute <= 0)
            {
                return 0;
            }

            if (definition.RepeatCooldownInGameMonths > 0)
            {
                var lastCompletedDate = ConvertInGameMinuteToDateTime(lastCompletedInGameMinute);
                var nextAvailableDate = lastCompletedDate.AddMonths(definition.RepeatCooldownInGameMonths);
                var currentDate = ConvertInGameMinuteToDateTime(GetCurrentInGameMinute());
                var remaining = (int)Math.Ceiling((nextAvailableDate - currentDate).TotalMinutes);
                return Math.Max(0, remaining);
            }

            var elapsedInGameMinutes = Math.Max(0, GetCurrentInGameMinute() - lastCompletedInGameMinute);
            return Math.Max(0, definition.RepeatCooldownInGameMinutes - elapsedInGameMinutes);
        }

        private int GetAvailabilityDelayRemainingMinutes(SpecialMissionDefinition definition)
        {
            if (definition == null || !definition.HasAvailabilityDelay)
            {
                return 0;
            }

            if (definition.AvailabilityDelayInGameMonths > 0)
            {
                var availableAt = ConvertInGameMinuteToDateTime(0).AddMonths(definition.AvailabilityDelayInGameMonths);
                var remainingMonthsMinutes = (int)Math.Ceiling((availableAt - ConvertInGameMinuteToDateTime(GetCurrentInGameMinute())).TotalMinutes);
                return Math.Max(0, remainingMonthsMinutes);
            }

            return Math.Max(0, definition.AvailabilityDelayInGameMinutes - GetCurrentInGameMinute());
        }

        private bool MeetsMissionSpecificAvailabilityRequirements(SpecialMissionDefinition definition, out string detail)
        {
            detail = string.Empty;
            if (definition == null)
            {
                detail = "Mission definition unavailable.";
                return false;
            }

            if (string.Equals(definition.Id, "quarry_heavy_machinery", StringComparison.OrdinalIgnoreCase)
                && (_territoryManager == null || !_territoryManager.HasActiveCorridorBetween("Port", "GrandSenora")))
            {
                detail = "Requires an active corridor between Port and GrandSenora.";
                return false;
            }

            return true;
        }

        private void RefreshMissionAvailabilityAnnouncements()
        {
            var currentInGameMinute = GetCurrentInGameMinute();
            if (currentInGameMinute == _lastAvailabilityScanInGameMinute)
            {
                return;
            }

            _lastAvailabilityScanInGameMinute = currentInGameMinute;
            var newlyAvailable = new List<string>();
            foreach (var definition in Definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                var isAvailable = CanAcceptMission(definition.Id, out _);
                if (isAvailable)
                {
                    if (_announcedAvailableMissionIds.Add(definition.Id.Trim()))
                    {
                        newlyAvailable.Add(definition.Name);
                    }
                }
                else
                {
                    _announcedAvailableMissionIds.Remove(definition.Id.Trim());
                }
            }

            if (newlyAvailable.Count == 1)
            {
                ShowStatus(string.Format("Special mission available: {0}.", newlyAvailable[0]), 4500);
            }
            else if (newlyAvailable.Count > 1)
            {
                ShowStatus(string.Format("Special missions available: {0}.", string.Join(", ", newlyAvailable.ToArray())), 5000);
            }
        }

        private string BuildAvailabilityDelayDetail(SpecialMissionDefinition definition, int remainingMinutes)
        {
            if (definition == null)
            {
                return string.Format("Available in {0}.", FormatMissionDuration(remainingMinutes));
            }

            return string.Format("Available in {0}.", FormatMissionDuration(remainingMinutes));
        }

        private static string FormatMissionDuration(int totalMinutes)
        {
            totalMinutes = Math.Max(0, totalMinutes);
            var days = totalMinutes / InGameMinutesPerDay;
            var remainingMinutes = totalMinutes % InGameMinutesPerDay;
            var hours = remainingMinutes / 60;
            var minutes = remainingMinutes % 60;
            var parts = new List<string>();

            if (days > 0)
            {
                parts.Add(string.Format("{0}d", days));
            }

            if (hours > 0)
            {
                parts.Add(string.Format("{0}h", hours));
            }

            if (minutes > 0 || parts.Count == 0)
            {
                parts.Add(string.Format("{0}m", minutes));
            }

            return string.Join(" ", parts.ToArray());
        }

        private int GetCurrentInGameMinute()
        {
            return _getCurrentInGameMinute != null
                ? Math.Max(0, _getCurrentInGameMinute())
                : 0;
        }

        private static DateTime ConvertInGameMinuteToDateTime(int totalMinutes)
        {
            return InGameEpoch.AddMinutes(Math.Max(0, totalMinutes));
        }

        private static string BuildRepeatCooldownDetail(SpecialMissionDefinition definition, int remainingMinutes)
        {
            var cooldownLabel = FormatRepeatCooldown(definition);
            if (remainingMinutes <= 0)
            {
                return string.IsNullOrWhiteSpace(cooldownLabel)
                    ? "Repeatable mission."
                    : string.Format("Repeatable mission with a {0} cooldown.", cooldownLabel);
            }

            return string.Format("Available again in {0} ({1} between runs).", FormatInGameDuration(remainingMinutes), cooldownLabel);
        }

        private static string FormatRepeatCooldown(SpecialMissionDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (definition.RepeatCooldownInGameMonths > 0)
            {
                var months = definition.RepeatCooldownInGameMonths;
                return months == 1
                    ? "1 in-game month"
                    : string.Format("{0} in-game months", months);
            }

            var cooldownMinutes = definition.RepeatCooldownInGameMinutes;
            if (cooldownMinutes <= 0)
            {
                return string.Empty;
            }

            if (cooldownMinutes % InGameMinutesPerWeek == 0)
            {
                var weeks = cooldownMinutes / InGameMinutesPerWeek;
                return weeks == 1
                    ? "1 in-game week"
                    : string.Format("{0} in-game weeks", weeks);
            }

            return FormatInGameDuration(cooldownMinutes);
        }

        private static string FormatInGameDuration(int totalMinutes)
        {
            totalMinutes = Math.Max(0, totalMinutes);
            var days = totalMinutes / InGameMinutesPerDay;
            var remainingMinutes = totalMinutes % InGameMinutesPerDay;
            var hours = remainingMinutes / 60;
            var minutes = remainingMinutes % 60;
            var parts = new List<string>();

            if (days > 0)
            {
                parts.Add(string.Format("{0}d", days));
            }

            if (hours > 0)
            {
                parts.Add(string.Format("{0}h", hours));
            }

            if (minutes > 0 || parts.Count == 0)
            {
                parts.Add(string.Format("{0}m", minutes));
            }

            return string.Join(" ", parts.ToArray());
        }

        private ActiveSpecialMissionRuntime CreateRuntime(
            SpecialMissionDefinition definition,
            ActiveSpecialMissionPersistenceSnapshot snapshot,
            out string error)
        {
            error = string.Empty;
            if (definition == null)
            {
                error = "Mission definition unavailable.";
                return null;
            }

            switch (definition.Type)
            {
                case SpecialMissionType.TrailerDelivery:
                    var trailerRuntime = new TrailerDeliveryRuntime(this, definition, snapshot);
                    error = trailerRuntime.InitializationError;
                    return string.IsNullOrWhiteSpace(error)
                        ? (ActiveSpecialMissionRuntime)trailerRuntime
                        : null;
                case SpecialMissionType.HandlerContainerTransfer:
                    var runtime = new HandlerContainerTransferRuntime(this, definition, snapshot);
                    error = runtime.InitializationError;
                    return string.IsNullOrWhiteSpace(error)
                        ? (ActiveSpecialMissionRuntime)runtime
                        : null;
                default:
                    error = string.Format("Mission type '{0}' is not implemented yet.", definition.Type);
                    return null;
            }
        }

        private void CleanupActiveMission()
        {
            if (_activeMission == null)
            {
                return;
            }

            _activeMission.Cleanup();
            _activeMission = null;
            ClearWaypoint();
            MarkUiDirty();
        }

        private void MarkUiDirty()
        {
            _markUiDirty?.Invoke();
        }

        private void ShowStatus(string message, int durationMs = 3000)
        {
            _showStatus?.Invoke(message, durationMs);
        }

        private void SetWaypoint(Vector3 position)
        {
            if (position == Vector3.Zero)
            {
                return;
            }

            Function.Call(Hash.SET_NEW_WAYPOINT, position.X, position.Y);
        }

        private static void ClearWaypoint()
        {
            Function.Call(Hash.SET_WAYPOINT_OFF);
        }

        private void HandleMissionCompleted(ActiveSpecialMissionRuntime runtime, string detail)
        {
            if (!ReferenceEquals(_activeMission, runtime) || runtime == null)
            {
                return;
            }

            IncrementCompletionCount(runtime.Definition.Id);
            SetLastCompletedInGameMinute(runtime.Definition.Id, GetCurrentInGameMinute());
            if (runtime.Definition.Reward > 0.001f)
            {
                _awardProfit?.Invoke(runtime.Definition.Reward);
            }

            var missionName = runtime.Definition.Name;
            CleanupActiveMission();
            var rewardDetail = runtime.Definition.Reward > 0.001f
                ? string.Format(" Earned ${0:0}.", runtime.Definition.Reward)
                : string.Empty;
            ShowStatus(string.Format("Completed {0}. {1}{2}", missionName, detail ?? string.Empty, rewardDetail).Trim(), 5000);
        }

        private void HandleMissionFailed(ActiveSpecialMissionRuntime runtime, string detail)
        {
            if (!ReferenceEquals(_activeMission, runtime) || runtime == null)
            {
                return;
            }

            var missionName = runtime.Definition.Name;
            CleanupActiveMission();
            ShowStatus(string.Format("Mission failed: {0}. {1}", missionName, detail ?? string.Empty).Trim(), 5000);
        }

        private abstract class ActiveSpecialMissionRuntime
        {
            protected ActiveSpecialMissionRuntime(SpecialMissionManager owner, SpecialMissionDefinition definition)
            {
                Owner = owner;
                Definition = definition;
                CurrentObjective = string.Empty;
                CurrentDetail = string.Empty;
                InitializationError = string.Empty;
            }

            protected SpecialMissionManager Owner { get; }

            public SpecialMissionDefinition Definition { get; }

            public string CurrentObjective { get; protected set; }

            public string CurrentDetail { get; protected set; }

            public string InitializationError { get; protected set; }

            public abstract int CurrentStageIndex { get; }

            public abstract void OnActivated(bool restoredFromSave);

            public abstract void Update(Ped player, int gameTime);

            public abstract bool HandleInteract(Ped player);

            public abstract ActiveSpecialMissionPersistenceSnapshot CreateSnapshot();

            public abstract void Cleanup();
        }

        private sealed class TrailerDeliveryRuntime : ActiveSpecialMissionRuntime
        {
            private const string TrailerRole = "Trailer";
            private const string DestinationZone = "Destination";
            private const string CleanupAreaZone = "CleanupArea";

            private readonly Dictionary<string, Vehicle> _vehicles;
            private TrailerDeliveryStage _stage;

            public TrailerDeliveryRuntime(
                SpecialMissionManager owner,
                SpecialMissionDefinition definition,
                ActiveSpecialMissionPersistenceSnapshot snapshot)
                : base(owner, definition)
            {
                _vehicles = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);
                _stage = TrailerDeliveryStage.CollectTrailer;
                TrySpawnTrailer();

                if (!string.IsNullOrWhiteSpace(InitializationError))
                {
                    Cleanup();
                    return;
                }

                if (snapshot != null)
                {
                    _stage = ParseStage(snapshot.StageIndex);
                    ApplyCheckpointLayout();
                }

                UpdateObjectiveText();
            }

            public override int CurrentStageIndex
            {
                get { return (int)_stage; }
            }

            public override void OnActivated(bool restoredFromSave)
            {
                Owner.MarkUiDirty();
                var waypoint = ResolveCurrentWaypoint();
                if (waypoint != Vector3.Zero)
                {
                    Owner.SetWaypoint(waypoint);
                }
                else
                {
                    ClearWaypoint();
                }

                if (restoredFromSave)
                {
                    Owner.ShowStatus(string.Format("Resumed special mission: {0}.", Definition.Name), 4500);
                }
            }

            public override void Update(Ped player, int gameTime)
            {
                var trailer = GetTrailer();
                if (trailer == null || !trailer.Exists())
                {
                    Owner.HandleMissionFailed(this, "The mission trailer is no longer available.");
                    return;
                }

                DrawObjectiveGuidance();

                switch (_stage)
                {
                    case TrailerDeliveryStage.CollectTrailer:
                        if (ResolveTowVehicle(trailer) != null)
                        {
                            AdvanceTo(TrailerDeliveryStage.DeliverTrailer, "Trailer connected. Deliver the heavy machinery to the quarry.");
                        }

                        break;
                    case TrailerDeliveryStage.DeliverTrailer:
                        if (IsVehicleInsideZone(trailer, DestinationZone))
                        {
                            AdvanceTo(TrailerDeliveryStage.LeaveQuarryArea, "Heavy machinery delivered. Leave the quarry area and the trailer will be cleared.");
                        }

                        break;
                    case TrailerDeliveryStage.LeaveQuarryArea:
                        if (IsPlayerOutsideCleanupArea(player))
                        {
                            Owner.HandleMissionCompleted(this, "Heavy machinery delivered to the quarry.");
                        }

                        break;
                }
            }

            public override bool HandleInteract(Ped player)
            {
                return false;
            }

            public override ActiveSpecialMissionPersistenceSnapshot CreateSnapshot()
            {
                return new ActiveSpecialMissionPersistenceSnapshot
                {
                    MissionId = Definition.Id,
                    StageIndex = (int)_stage,
                };
            }

            public override void Cleanup()
            {
                for (int i = 0; i < _vehicles.Count; i++)
                {
                    var vehicle = _vehicles.ElementAt(i).Value;
                    if (vehicle == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (vehicle.Exists())
                        {
                            vehicle.Delete();
                        }
                    }
                    catch
                    {
                    }
                }

                _vehicles.Clear();
            }

            private void TrySpawnTrailer()
            {
                if (!Definition.TryGetVehicle(TrailerRole, out var spawn))
                {
                    InitializationError = string.Format("Mission '{0}' is missing trailer spawn data.", Definition.Name);
                    return;
                }

                if (!Owner._fleetManager.TrySpawnVehicleByModelName(spawn.ModelName, spawn.Position, spawn.Heading, out var vehicle)
                    || vehicle == null
                    || !vehicle.Exists())
                {
                    InitializationError = string.Format("Could not spawn mission trailer '{0}'.", spawn.ModelName);
                    return;
                }

                vehicle.IsPersistent = true;
                _vehicles[TrailerRole] = vehicle;
            }

            private void ApplyCheckpointLayout()
            {
                if (_stage == TrailerDeliveryStage.LeaveQuarryArea)
                {
                    PositionTrailerAtZone(DestinationZone);
                }
            }

            private void AdvanceTo(TrailerDeliveryStage nextStage, string statusMessage)
            {
                _stage = nextStage;
                UpdateObjectiveText();
                var waypoint = ResolveCurrentWaypoint();
                if (waypoint != Vector3.Zero)
                {
                    Owner.SetWaypoint(waypoint);
                }
                else
                {
                    ClearWaypoint();
                }

                Owner.MarkUiDirty();
                if (!string.IsNullOrWhiteSpace(statusMessage))
                {
                    Owner.ShowStatus(statusMessage, 4500);
                }
            }

            private void UpdateObjectiveText()
            {
                switch (_stage)
                {
                    case TrailerDeliveryStage.CollectTrailer:
                        CurrentObjective = "Collect the heavy machinery trailer";
                        CurrentDetail = "Bring a suitable tractor, hook the armytrailer2, and prepare the quarry run.";
                        break;
                    case TrailerDeliveryStage.DeliverTrailer:
                        CurrentObjective = "Deliver heavy machinery to the quarry";
                        CurrentDetail = "Tow the machinery trailer into the quarry drop zone in Grand Senora.";
                        break;
                    case TrailerDeliveryStage.LeaveQuarryArea:
                        CurrentObjective = "Clear the quarry area";
                        CurrentDetail = "Once you leave the quarry range, the delivered trailer will be removed from the world.";
                        break;
                }
            }

            private void DrawObjectiveGuidance()
            {
                var trailer = GetTrailer();
                switch (_stage)
                {
                    case TrailerDeliveryStage.CollectTrailer:
                        DrawVehicleMarker(trailer, Color.FromArgb(195, 82, 196, 235));
                        break;
                    case TrailerDeliveryStage.DeliverTrailer:
                        DrawVehicleMarker(trailer, Color.FromArgb(195, 226, 187, 92));
                        DrawZoneMarker(DestinationZone, Color.FromArgb(175, 92, 208, 144));
                        break;
                    case TrailerDeliveryStage.LeaveQuarryArea:
                        DrawZoneMarker(CleanupAreaZone, Color.FromArgb(175, 226, 187, 92));
                        break;
                }
            }

            private Vector3 ResolveCurrentWaypoint()
            {
                switch (_stage)
                {
                    case TrailerDeliveryStage.CollectTrailer:
                        return GetTrailerPosition();
                    case TrailerDeliveryStage.DeliverTrailer:
                        return GetZonePosition(DestinationZone);
                    default:
                        return Vector3.Zero;
                }
            }

            private bool IsVehicleInsideZone(Vehicle vehicle, string zoneId)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return false;
                }

                if (!TryGetZone(zoneId, out var zone))
                {
                    return false;
                }

                return vehicle.Position.DistanceTo(zone.Position) <= zone.Radius;
            }

            private bool IsPlayerOutsideCleanupArea(Ped player)
            {
                if (player == null || !player.Exists())
                {
                    return false;
                }

                if (!TryGetZone(CleanupAreaZone, out var zone) && !TryGetZone(DestinationZone, out zone))
                {
                    return false;
                }

                return player.Position.DistanceTo(zone.Position) > zone.Radius;
            }

            private bool TryGetZone(string zoneId, out SpecialMissionZone zone)
            {
                zone = null;
                if (Definition.TryGetZone(zoneId, out zone))
                {
                    return true;
                }

                if (string.Equals(zoneId, CleanupAreaZone, StringComparison.OrdinalIgnoreCase))
                {
                    return Definition.TryGetZone(DestinationZone, out zone);
                }

                return false;
            }

            private void PositionTrailerAtZone(string zoneId)
            {
                var trailer = GetTrailer();
                if (trailer == null || !trailer.Exists())
                {
                    return;
                }

                if (!TryGetZone(zoneId, out var zone) || !Definition.TryGetVehicle(TrailerRole, out var spawn))
                {
                    return;
                }

                trailer.Position = zone.Position;
                trailer.Heading = spawn.Heading;
                TryPlaceVehicleOnGround(trailer);
            }

            private Vehicle GetTrailer()
            {
                Vehicle trailer;
                return _vehicles.TryGetValue(TrailerRole, out trailer)
                    ? trailer
                    : null;
            }

            private Vector3 GetTrailerPosition()
            {
                var trailer = GetTrailer();
                return trailer != null && trailer.Exists()
                    ? trailer.Position
                    : Vector3.Zero;
            }

            private Vector3 GetZonePosition(string zoneId)
            {
                return TryGetZone(zoneId, out var zone)
                    ? zone.Position
                    : Vector3.Zero;
            }

            private static Vehicle ResolveTowVehicle(Vehicle vehicle)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return null;
                }

                int attachedHandle;
                try
                {
                    attachedHandle = Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, vehicle.Handle);
                }
                catch
                {
                    return null;
                }

                if (attachedHandle <= 0 || attachedHandle == vehicle.Handle)
                {
                    return null;
                }

                var attachedVehicle = Entity.FromHandle(attachedHandle) as Vehicle;
                return attachedVehicle != null && attachedVehicle.Exists()
                    ? attachedVehicle
                    : null;
            }

            private static void TryPlaceVehicleOnGround(Vehicle vehicle)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return;
                }

                try
                {
                    Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
                }
                catch
                {
                }
            }

            private static void DrawVehicleMarker(Vehicle vehicle, Color color)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return;
                }

                World.DrawMarker(
                    MarkerType.Cylinder,
                    vehicle.Position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(VehicleMarkerRadius, VehicleMarkerRadius, VehicleMarkerHeight),
                    color,
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);
            }

            private void DrawZoneMarker(string zoneId, Color color)
            {
                if (!TryGetZone(zoneId, out var zone))
                {
                    return;
                }

                World.DrawMarker(
                    MarkerType.Cylinder,
                    zone.Position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(zone.Radius * 2f, zone.Radius * 2f, 2.1f),
                    color,
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);
            }

            private static TrailerDeliveryStage ParseStage(int rawStage)
            {
                if (rawStage < (int)TrailerDeliveryStage.CollectTrailer)
                {
                    return TrailerDeliveryStage.CollectTrailer;
                }

                if (rawStage > (int)TrailerDeliveryStage.LeaveQuarryArea)
                {
                    return TrailerDeliveryStage.LeaveQuarryArea;
                }

                return (TrailerDeliveryStage)rawStage;
            }

            private enum TrailerDeliveryStage
            {
                CollectTrailer = 0,
                DeliverTrailer = 1,
                LeaveQuarryArea = 2,
            }
        }

        private sealed class HandlerContainerTransferRuntime : ActiveSpecialMissionRuntime
        {
            private const string DockHandlerRole = "DockHandler";
            private const string DockTugRole = "DockTug";
            private const string TrailerRole = "Trailer";
            private const string TruckRole = "Truck";
            private const string ContainerRole = "Container";
            private const string LoadingBayZone = "LoadingBay";
            private const string TransferBayZone = "TransferBay";
            private const string DestinationZone = "Destination";

            private readonly Dictionary<string, Vehicle> _vehicles;
            private readonly Dictionary<string, Prop> _props;
            private HandlerContainerTransferStage _stage;
            private bool _containerPickedUp;
            private bool _containerLoaded;

            public HandlerContainerTransferRuntime(
                SpecialMissionManager owner,
                SpecialMissionDefinition definition,
                ActiveSpecialMissionPersistenceSnapshot snapshot)
                : base(owner, definition)
            {
                _vehicles = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);
                _props = new Dictionary<string, Prop>(StringComparer.OrdinalIgnoreCase);
                _stage = HandlerContainerTransferStage.EnterDockTug;
                TrySpawnEntities();

                if (!string.IsNullOrWhiteSpace(InitializationError))
                {
                    Cleanup();
                    return;
                }

                if (snapshot != null)
                {
                    _stage = ParseStage(snapshot.StageIndex);
                    _containerPickedUp = snapshot.HandlerContainerPickedUp;
                    _containerLoaded = snapshot.HandlerContainerLoaded;

                    // Mid-lift state cannot be restored reliably without storing live prop transforms.
                    if (_containerPickedUp && !_containerLoaded)
                    {
                        _containerPickedUp = false;
                    }

                    ApplyCheckpointLayout();
                }

                UpdateObjectiveText();
            }

            public override int CurrentStageIndex
            {
                get { return (int)_stage; }
            }

            public override void OnActivated(bool restoredFromSave)
            {
                Owner.MarkUiDirty();
                var waypoint = ResolveCurrentWaypoint();
                if (waypoint != Vector3.Zero)
                {
                    Owner.SetWaypoint(waypoint);
                }

                if (restoredFromSave)
                {
                    Owner.ShowStatus(string.Format("Resumed special mission: {0}.", Definition.Name), 4500);
                }
            }

            public override void Update(Ped player, int gameTime)
            {
                if (!ValidateRequiredEntities())
                {
                    Owner.HandleMissionFailed(this, "A required mission vehicle or prop is missing.");
                    return;
                }

                DrawObjectiveGuidance();

                switch (_stage)
                {
                    case HandlerContainerTransferStage.EnterDockTug:
                        if (IsPlayerInRoleVehicle(player, DockTugRole))
                        {
                            AdvanceTo(HandlerContainerTransferStage.AttachTrailerToDockTug, "Docktug acquired. Reverse into the trailer and hook it up.");
                        }

                        break;
                    case HandlerContainerTransferStage.AttachTrailerToDockTug:
                        if (IsVehicleTowingExpectedTrailer(GetVehicle(DockTugRole), GetVehicle(TrailerRole)))
                        {
                            AdvanceTo(HandlerContainerTransferStage.MoveTrailerToLoadingBay, "Trailer connected. Bring it into the loading bay.");
                        }

                        break;
                    case HandlerContainerTransferStage.MoveTrailerToLoadingBay:
                        if (IsVehicleTowingExpectedTrailer(GetVehicle(DockTugRole), GetVehicle(TrailerRole))
                            && IsVehicleInsideZone(GetVehicle(TrailerRole), LoadingBayZone))
                        {
                            AdvanceTo(HandlerContainerTransferStage.EnterDockHandler, "Trailer is in position. Switch to the dock handler.");
                        }

                        break;
                    case HandlerContainerTransferStage.EnterDockHandler:
                        if (IsPlayerInRoleVehicle(player, DockHandlerRole))
                        {
                            AdvanceTo(HandlerContainerTransferStage.LoadContainer, "Move the dock handler into position and lift the container onto the spreader.");
                        }

                        break;
                    case HandlerContainerTransferStage.LoadContainer:
                        if (!_containerPickedUp && IsContainerBeingCarriedByHandler())
                        {
                            _containerPickedUp = true;
                            UpdateObjectiveText();
                            var waypoint = ResolveCurrentWaypoint();
                            if (waypoint != Vector3.Zero)
                            {
                                Owner.SetWaypoint(waypoint);
                            }

                            Owner.MarkUiDirty();
                            Owner.ShowStatus("Container lifted. Move it over the trailer and press Interact to secure it.", 4500);
                        }

                        break;
                    case HandlerContainerTransferStage.LeaveLoadedTrailerAtTransferBay:
                        if (!IsVehicleTowingExpectedTrailer(GetVehicle(DockTugRole), GetVehicle(TrailerRole))
                            && IsVehicleInsideZone(GetVehicle(TrailerRole), TransferBayZone))
                        {
                            AdvanceTo(HandlerContainerTransferStage.EnterTruck, "Loaded trailer staged. Get into the road truck.");
                        }

                        break;
                    case HandlerContainerTransferStage.EnterTruck:
                        if (IsPlayerInRoleVehicle(player, TruckRole))
                        {
                            AdvanceTo(HandlerContainerTransferStage.AttachLoadedTrailerToTruck, "Truck ready. Hook the loaded trailer to the packer.");
                        }

                        break;
                    case HandlerContainerTransferStage.AttachLoadedTrailerToTruck:
                        if (IsVehicleTowingExpectedTrailer(GetVehicle(TruckRole), GetVehicle(TrailerRole)))
                        {
                            AdvanceTo(HandlerContainerTransferStage.DeliverLoadedTrailer, "Connection confirmed. Deliver the loaded trailer to Grand Senora.");
                        }

                        break;
                    case HandlerContainerTransferStage.DeliverLoadedTrailer:
                        if (IsVehicleTowingExpectedTrailer(GetVehicle(TruckRole), GetVehicle(TrailerRole))
                            && IsVehicleInsideZone(GetVehicle(TrailerRole), DestinationZone))
                        {
                            Owner.HandleMissionCompleted(this, "Container delivered to the Grand Senora consignee.");
                        }

                        break;
                }
            }

            public override bool HandleInteract(Ped player)
            {
                if (_stage != HandlerContainerTransferStage.LoadContainer)
                {
                    return false;
                }

                if (!_containerPickedUp)
                {
                    return false;
                }

                if (!CanPlaceContainerOnTrailer(player))
                {
                    return false;
                }

                if (!AttachContainerToTrailer())
                {
                    Owner.ShowStatus("Container load could not be secured. Reposition the handler and try again.");
                    return true;
                }

                _containerPickedUp = false;
                _containerLoaded = true;
                AdvanceTo(HandlerContainerTransferStage.LeaveLoadedTrailerAtTransferBay, "Container secured. Leave the loaded trailer in the transfer bay for the road truck.");
                return true;
            }

            public override ActiveSpecialMissionPersistenceSnapshot CreateSnapshot()
            {
                return new ActiveSpecialMissionPersistenceSnapshot
                {
                    MissionId = Definition.Id,
                    StageIndex = (int)_stage,
                    HandlerContainerPickedUp = _containerPickedUp,
                    HandlerContainerLoaded = _containerLoaded,
                };
            }

            public override void Cleanup()
            {
                foreach (var pair in _props)
                {
                    DeleteProp(pair.Value);
                }

                _props.Clear();

                foreach (var pair in _vehicles)
                {
                    DeleteVehicle(pair.Value);
                }

                _vehicles.Clear();
            }

            private void TrySpawnEntities()
            {
                TrySpawnVehicleRole(DockHandlerRole);
                TrySpawnVehicleRole(DockTugRole);
                TrySpawnVehicleRole(TrailerRole);
                TrySpawnVehicleRole(TruckRole);
                TrySpawnPropRole(ContainerRole);
            }

            private void TrySpawnVehicleRole(string roleId)
            {
                if (!Definition.TryGetVehicle(roleId, out var spawn))
                {
                    InitializationError = string.Format("Mission '{0}' is missing vehicle role '{1}'.", Definition.Name, roleId);
                    return;
                }

                if (!Owner._fleetManager.TrySpawnVehicleByModelName(spawn.ModelName, spawn.Position, spawn.Heading, out var vehicle)
                    || vehicle == null
                    || !vehicle.Exists())
                {
                    InitializationError = string.Format("Could not spawn mission vehicle '{0}'.", spawn.ModelName);
                    return;
                }

                vehicle.IsPersistent = true;
                _vehicles[roleId] = vehicle;
            }

            private void TrySpawnPropRole(string roleId)
            {
                if (!Definition.TryGetProp(roleId, out var spawn))
                {
                    InitializationError = string.Format("Mission '{0}' is missing prop role '{1}'.", Definition.Name, roleId);
                    return;
                }

                if (!TrySpawnProp(spawn, out var prop) || prop == null || !prop.Exists())
                {
                    InitializationError = string.Format("Could not spawn mission prop '{0}'.", roleId);
                    return;
                }

                prop.IsPersistent = true;
                _props[roleId] = prop;
            }

            private void ApplyCheckpointLayout()
            {
                switch (_stage)
                {
                    case HandlerContainerTransferStage.EnterDockTug:
                    case HandlerContainerTransferStage.AttachTrailerToDockTug:
                        _containerPickedUp = false;
                        ResetPropToSpawn();
                        break;
                    case HandlerContainerTransferStage.MoveTrailerToLoadingBay:
                        _containerPickedUp = false;
                        ResetPropToSpawn();
                        TryAttachRoleVehicles(DockTugRole, TrailerRole);
                        break;
                    case HandlerContainerTransferStage.EnterDockHandler:
                        _containerPickedUp = false;
                        PositionTrailerAtZone(LoadingBayZone);
                        TryAttachRoleVehicles(DockTugRole, TrailerRole);
                        ResetPropToSpawn();
                        break;
                    case HandlerContainerTransferStage.LoadContainer:
                        _containerPickedUp = false;
                        PositionTrailerAtZone(LoadingBayZone);
                        TryAttachRoleVehicles(DockTugRole, TrailerRole);
                        if (_containerLoaded)
                        {
                            AttachContainerToTrailer();
                        }
                        else
                        {
                            ResetPropToSpawn();
                        }

                        break;
                    case HandlerContainerTransferStage.LeaveLoadedTrailerAtTransferBay:
                    case HandlerContainerTransferStage.EnterTruck:
                    case HandlerContainerTransferStage.AttachLoadedTrailerToTruck:
                        PositionTrailerAtZone(TransferBayZone);
                        DetachVehicleFromTrailer(GetVehicle(DockTugRole));
                        if (_containerLoaded)
                        {
                            AttachContainerToTrailer();
                        }

                        break;
                    case HandlerContainerTransferStage.DeliverLoadedTrailer:
                        PositionTrailerAtZone(TransferBayZone);
                        DetachVehicleFromTrailer(GetVehicle(DockTugRole));
                        if (_containerLoaded)
                        {
                            AttachContainerToTrailer();
                        }

                        TryAttachRoleVehicles(TruckRole, TrailerRole);
                        break;
                }
            }

            private void AdvanceTo(HandlerContainerTransferStage nextStage, string statusMessage)
            {
                _stage = nextStage;
                if (_stage == HandlerContainerTransferStage.EnterDockHandler || _stage == HandlerContainerTransferStage.LoadContainer)
                {
                    PositionTrailerAtZone(LoadingBayZone);
                    TryAttachRoleVehicles(DockTugRole, TrailerRole);
                }
                else if (_stage == HandlerContainerTransferStage.LeaveLoadedTrailerAtTransferBay)
                {
                    PositionTrailerAtZone(TransferBayZone);
                }

                UpdateObjectiveText();
                var waypoint = ResolveCurrentWaypoint();
                if (waypoint != Vector3.Zero)
                {
                    Owner.SetWaypoint(waypoint);
                }

                Owner.MarkUiDirty();
                if (!string.IsNullOrWhiteSpace(statusMessage))
                {
                    Owner.ShowStatus(statusMessage, 4500);
                }
            }

            private void UpdateObjectiveText()
            {
                switch (_stage)
                {
                    case HandlerContainerTransferStage.EnterDockTug:
                        CurrentObjective = "Enter the docktug";
                        CurrentDetail = "Head to the tug staged on the Port of Los Santos quay and take control of it.";
                        break;
                    case HandlerContainerTransferStage.AttachTrailerToDockTug:
                        CurrentObjective = "Attach the flatbed trailer";
                        CurrentDetail = "Back the docktug into the trflat trailer and lock the coupling.";
                        break;
                    case HandlerContainerTransferStage.MoveTrailerToLoadingBay:
                        CurrentObjective = "Move the trailer to the loading bay";
                        CurrentDetail = "Drag the empty trailer into the marked loading area for container transfer.";
                        break;
                    case HandlerContainerTransferStage.EnterDockHandler:
                        CurrentObjective = "Enter the dock handler";
                        CurrentDetail = "Leave the tug staged and move over to the handler crane.";
                        break;
                    case HandlerContainerTransferStage.LoadContainer:
                        CurrentObjective = _containerPickedUp
                            ? "Load the container onto the trailer"
                            : "Pick up the container";
                        CurrentDetail = _containerPickedUp
                            ? "Drive the handler up to the staged trailer and press Interact to set the container down on it."
                            : "Use the handler controls to lift the container from the yard stack.";
                        break;
                    case HandlerContainerTransferStage.LeaveLoadedTrailerAtTransferBay:
                        CurrentObjective = "Leave the loaded trailer in the transfer bay";
                        CurrentDetail = "Detach the docktug and stage the loaded trailer so the road truck can take over.";
                        break;
                    case HandlerContainerTransferStage.EnterTruck:
                        CurrentObjective = "Enter the road truck";
                        CurrentDetail = "Walk over to the packer and prepare for the highway run to Grand Senora.";
                        break;
                    case HandlerContainerTransferStage.AttachLoadedTrailerToTruck:
                        CurrentObjective = "Attach the loaded trailer";
                        CurrentDetail = "Back the truck into the staged trailer and secure the coupling.";
                        break;
                    case HandlerContainerTransferStage.DeliverLoadedTrailer:
                        CurrentObjective = "Deliver the container";
                        CurrentDetail = "Drive the loaded trailer to the consignee in Grand Senora and stop in the marked drop zone.";
                        break;
                }
            }

            private void DrawObjectiveGuidance()
            {
                switch (_stage)
                {
                    case HandlerContainerTransferStage.EnterDockTug:
                        DrawVehicleMarker(GetVehicle(DockTugRole), Color.FromArgb(195, 82, 196, 235));
                        break;
                    case HandlerContainerTransferStage.AttachTrailerToDockTug:
                        DrawVehicleMarker(GetVehicle(TrailerRole), Color.FromArgb(195, 226, 187, 92));
                        break;
                    case HandlerContainerTransferStage.MoveTrailerToLoadingBay:
                        DrawZoneMarker(LoadingBayZone, Color.FromArgb(175, 104, 214, 158));
                        break;
                    case HandlerContainerTransferStage.EnterDockHandler:
                        DrawVehicleMarker(GetVehicle(DockHandlerRole), Color.FromArgb(195, 82, 196, 235));
                        break;
                    case HandlerContainerTransferStage.LoadContainer:
                        DrawZoneMarker(LoadingBayZone, Color.FromArgb(175, 226, 187, 92));
                        if (_containerPickedUp)
                        {
                            DrawVehicleMarker(GetVehicle(TrailerRole), Color.FromArgb(195, 226, 187, 92));
                            DrawPropMarker(GetProp(ContainerRole), Color.FromArgb(195, 226, 187, 92));
                            if (CanPlaceContainerOnTrailer(Game.Player.Character))
                            {
                                Screen.ShowHelpTextThisFrame("~y~[LSOL]~s~ Press Interact to load the container onto the trailer.");
                            }
                        }
                        else
                        {
                            DrawPropMarker(GetProp(ContainerRole), Color.FromArgb(195, 226, 187, 92));
                            if (IsPlayerInRoleVehicle(Game.Player.Character, DockHandlerRole))
                            {
                                Screen.ShowHelpTextThisFrame("~y~[LSOL]~s~ Use the handler controls to pick up the container.");
                            }
                        }

                        break;
                    case HandlerContainerTransferStage.LeaveLoadedTrailerAtTransferBay:
                        DrawZoneMarker(TransferBayZone, Color.FromArgb(175, 104, 214, 158));
                        break;
                    case HandlerContainerTransferStage.EnterTruck:
                        DrawVehicleMarker(GetVehicle(TruckRole), Color.FromArgb(195, 82, 196, 235));
                        break;
                    case HandlerContainerTransferStage.AttachLoadedTrailerToTruck:
                        DrawVehicleMarker(GetVehicle(TrailerRole), Color.FromArgb(195, 226, 187, 92));
                        break;
                    case HandlerContainerTransferStage.DeliverLoadedTrailer:
                        DrawZoneMarker(DestinationZone, Color.FromArgb(175, 92, 208, 144));
                        break;
                }
            }

            private Vector3 ResolveCurrentWaypoint()
            {
                switch (_stage)
                {
                    case HandlerContainerTransferStage.EnterDockTug:
                        return GetVehiclePosition(DockTugRole);
                    case HandlerContainerTransferStage.AttachTrailerToDockTug:
                        return GetVehiclePosition(TrailerRole);
                    case HandlerContainerTransferStage.MoveTrailerToLoadingBay:
                        return GetZonePosition(LoadingBayZone);
                    case HandlerContainerTransferStage.EnterDockHandler:
                        return GetVehiclePosition(DockHandlerRole);
                    case HandlerContainerTransferStage.LoadContainer:
                        return _containerPickedUp
                            ? GetVehiclePosition(TrailerRole)
                            : GetPropPosition(ContainerRole);
                    case HandlerContainerTransferStage.LeaveLoadedTrailerAtTransferBay:
                        return GetZonePosition(TransferBayZone);
                    case HandlerContainerTransferStage.EnterTruck:
                        return GetVehiclePosition(TruckRole);
                    case HandlerContainerTransferStage.AttachLoadedTrailerToTruck:
                        return GetVehiclePosition(TrailerRole);
                    case HandlerContainerTransferStage.DeliverLoadedTrailer:
                        return GetZonePosition(DestinationZone);
                    default:
                        return Vector3.Zero;
                }
            }

            private bool ValidateRequiredEntities()
            {
                foreach (var pair in Definition.Vehicles)
                {
                    if (!pair.Value.Required)
                    {
                        continue;
                    }

                    var vehicle = GetVehicle(pair.Key);
                    if (vehicle == null || !vehicle.Exists())
                    {
                        return false;
                    }
                }

                foreach (var pair in Definition.Props)
                {
                    if (!pair.Value.Required)
                    {
                        continue;
                    }

                    var prop = GetProp(pair.Key);
                    if (prop == null || !prop.Exists())
                    {
                        return false;
                    }
                }

                return true;
            }

            private Vehicle GetVehicle(string roleId)
            {
                Vehicle vehicle;
                return _vehicles.TryGetValue(roleId, out vehicle)
                    ? vehicle
                    : null;
            }

            private Prop GetProp(string roleId)
            {
                Prop prop;
                return _props.TryGetValue(roleId, out prop)
                    ? prop
                    : null;
            }

            private Vector3 GetVehiclePosition(string roleId)
            {
                var vehicle = GetVehicle(roleId);
                return vehicle != null && vehicle.Exists()
                    ? vehicle.Position
                    : Vector3.Zero;
            }

            private Vector3 GetZonePosition(string zoneId)
            {
                return Definition.TryGetZone(zoneId, out var zone)
                    ? zone.Position
                    : Vector3.Zero;
            }

            private bool IsPlayerInRoleVehicle(Ped player, string roleId)
            {
                var vehicle = GetVehicle(roleId);
                return player != null
                    && player.Exists()
                    && vehicle != null
                    && vehicle.Exists()
                    && player.CurrentVehicle != null
                    && player.CurrentVehicle.Exists()
                    && player.CurrentVehicle.Handle == vehicle.Handle;
            }

            private bool IsVehicleTowingExpectedTrailer(Vehicle truck, Vehicle trailer)
            {
                if (truck == null || !truck.Exists() || trailer == null || !trailer.Exists())
                {
                    return false;
                }

                var attachedTrailer = ResolveAttachedTrailer(truck);
                return attachedTrailer != null
                    && attachedTrailer.Exists()
                    && attachedTrailer.Handle == trailer.Handle;
            }

            private bool IsVehicleInsideZone(Vehicle vehicle, string zoneId)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return false;
                }

                if (!Definition.TryGetZone(zoneId, out var zone))
                {
                    return false;
                }

                return vehicle.Position.DistanceTo(zone.Position) <= zone.Radius;
            }

            private bool CanPlaceContainerOnTrailer(Ped player)
            {
                var dockHandler = GetVehicle(DockHandlerRole);
                var trailer = GetVehicle(TrailerRole);
                var container = GetProp(ContainerRole);
                return _containerPickedUp
                    && IsPlayerInRoleVehicle(player, DockHandlerRole)
                    && dockHandler != null
                    && dockHandler.Exists()
                    && trailer != null
                    && trailer.Exists()
                    && container != null
                    && container.Exists()
                    && IsVehicleInsideZone(trailer, LoadingBayZone)
                    && (container.Position.DistanceTo(trailer.Position) <= ObjectiveInteractDistance + 3.5f
                        || dockHandler.Position.DistanceTo(trailer.Position) <= ObjectiveInteractDistance);
            }

            private bool IsContainerBeingCarriedByHandler()
            {
                var dockHandler = GetVehicle(DockHandlerRole);
                var container = GetProp(ContainerRole);
                if (dockHandler == null || !dockHandler.Exists() || container == null || !container.Exists())
                {
                    return false;
                }

                int attachedHandle;
                try
                {
                    attachedHandle = Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO, container.Handle);
                }
                catch
                {
                    attachedHandle = 0;
                }

                if (attachedHandle == dockHandler.Handle)
                {
                    return true;
                }

                if (!Definition.TryGetProp(ContainerRole, out var spawn))
                {
                    return false;
                }

                return container.Position.DistanceTo(dockHandler.Position) <= 12f
                    && container.Position.Z > spawn.Position.Z + 2.5f;
            }

            private bool AttachContainerToTrailer()
            {
                var trailer = GetVehicle(TrailerRole);
                var container = GetProp(ContainerRole);
                if (trailer == null || !trailer.Exists() || container == null || !container.Exists())
                {
                    return false;
                }

                if (!Definition.TryGetProp(ContainerRole, out var spawn))
                {
                    return false;
                }

                Vector3 trailerMin;
                Vector3 trailerMax;
                Vector3 propMin;
                Vector3 propMax;
                trailer.Model.GetDimensions(out trailerMin, out trailerMax);
                container.Model.GetDimensions(out propMin, out propMax);

                var attachOffset = new Vector3(
                    ((trailerMin.X + trailerMax.X) * 0.5f) + spawn.AttachOffset.X,
                    ((trailerMin.Y + trailerMax.Y) * 0.5f) + spawn.AttachOffset.Y,
                    (trailerMax.Z - propMin.Z) + spawn.AttachOffset.Z);

                try
                {
                    container.AttachTo(trailer, attachOffset, spawn.AttachRotation);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private void ResetPropToSpawn()
            {
                var prop = GetProp(ContainerRole);
                if (prop == null || !prop.Exists() || !Definition.TryGetProp(ContainerRole, out var spawn))
                {
                    return;
                }

                prop.Detach();
                prop.Position = spawn.Position;
                prop.Heading = spawn.Heading;
                TryPlacePropOnGround(prop);
                EnsurePropIsDynamic(prop);
            }

            private Vector3 GetPropPosition(string roleId)
            {
                var prop = GetProp(roleId);
                return prop != null && prop.Exists()
                    ? prop.Position
                    : Vector3.Zero;
            }

            private void PositionTrailerAtZone(string zoneId)
            {
                if (!Definition.TryGetVehicle(TrailerRole, out var trailerSpawn)
                    || !Definition.TryGetZone(zoneId, out var zone))
                {
                    return;
                }

                var trailer = GetVehicle(TrailerRole);
                if (trailer == null || !trailer.Exists())
                {
                    return;
                }

                trailer.Position = zone.Position;
                trailer.Heading = trailerSpawn.Heading;
                TryPlaceVehicleOnGround(trailer);
            }

            private void TryAttachRoleVehicles(string tractorRoleId, string trailerRoleId)
            {
                var tractor = GetVehicle(tractorRoleId);
                var trailer = GetVehicle(trailerRoleId);
                if (tractor == null || !tractor.Exists() || trailer == null || !trailer.Exists())
                {
                    return;
                }

                Owner._fleetManager.TryAttachVehicleToTrailer(tractor, trailer, tractor.Heading);
            }

            private static Vehicle ResolveAttachedTrailer(Vehicle vehicle)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return null;
                }

                var towedVehicle = vehicle.TowedVehicle;
                if (towedVehicle != null && towedVehicle.Exists())
                {
                    return towedVehicle;
                }

                var trailerHandleArg = new OutputArgument();
                bool hasTrailer;
                try
                {
                    hasTrailer = Function.Call<bool>(Hash.GET_VEHICLE_TRAILER_VEHICLE, vehicle.Handle, trailerHandleArg);
                }
                catch
                {
                    return null;
                }

                if (!hasTrailer)
                {
                    return null;
                }

                int trailerHandle;
                try
                {
                    trailerHandle = trailerHandleArg.GetResult<int>();
                }
                catch
                {
                    return null;
                }

                if (trailerHandle <= 0)
                {
                    return null;
                }

                var entity = Entity.FromHandle(trailerHandle) as Vehicle;
                return entity != null && entity.Exists()
                    ? entity
                    : null;
            }

            private static void DetachVehicleFromTrailer(Vehicle vehicle)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return;
                }

                try
                {
                    Function.Call(Hash.DETACH_VEHICLE_FROM_TRAILER, vehicle.Handle);
                }
                catch
                {
                    // Detach is best-effort for checkpoint restoration.
                }
            }

            private static bool TrySpawnProp(SpecialMissionPropSpawn spawn, out Prop prop)
            {
                prop = null;
                if (spawn == null)
                {
                    return false;
                }

                var model = spawn.HasModelHash
                    ? new Model(spawn.ModelHash)
                    : new Model(spawn.ModelName);
                if (!model.IsInCdImage || !model.IsValid || !model.Request(1000))
                {
                    return false;
                }

                prop = World.CreateProp(model, spawn.Position, true, false);
                model.MarkAsNoLongerNeeded();
                if (prop == null || !prop.Exists())
                {
                    return false;
                }

                prop.Heading = spawn.Heading;
                TryPlacePropOnGround(prop);
                EnsurePropIsDynamic(prop);
                return true;
            }

            private static void EnsurePropIsDynamic(Prop prop)
            {
                if (prop == null || !prop.Exists())
                {
                    return;
                }

                try
                {
                    Function.Call(Hash.FREEZE_ENTITY_POSITION, prop.Handle, false);
                }
                catch
                {
                }

                try
                {
                    Function.Call(Hash.SET_ENTITY_DYNAMIC, prop.Handle, true);
                }
                catch
                {
                }

                try
                {
                    Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, prop.Handle, true);
                }
                catch
                {
                }

                try
                {
                    Function.Call(Hash.ACTIVATE_PHYSICS, prop.Handle);
                }
                catch
                {
                }
            }

            private static void TryPlaceVehicleOnGround(Vehicle vehicle)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return;
                }

                try
                {
                    Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
                }
                catch
                {
                    // Ground placement is best-effort only.
                }
            }

            private static void TryPlacePropOnGround(Prop prop)
            {
                if (prop == null || !prop.Exists())
                {
                    return;
                }

                try
                {
                    Function.Call(Hash.PLACE_OBJECT_ON_GROUND_PROPERLY, prop.Handle);
                }
                catch
                {
                    // Ground placement is optional.
                }
            }

            private static void DeleteVehicle(Vehicle vehicle)
            {
                if (vehicle == null)
                {
                    return;
                }

                try
                {
                    if (vehicle.Exists())
                    {
                        vehicle.Delete();
                    }
                }
                catch
                {
                    // Cleanup should not throw.
                }
            }

            private static void DeleteProp(Prop prop)
            {
                if (prop == null)
                {
                    return;
                }

                try
                {
                    if (prop.Exists())
                    {
                        prop.Delete();
                    }
                }
                catch
                {
                    // Cleanup should not throw.
                }
            }

            private static void DrawVehicleMarker(Vehicle vehicle, Color color)
            {
                if (vehicle == null || !vehicle.Exists())
                {
                    return;
                }

                World.DrawMarker(
                    MarkerType.Cylinder,
                    vehicle.Position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(VehicleMarkerRadius, VehicleMarkerRadius, VehicleMarkerHeight),
                    color,
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);
            }

            private void DrawZoneMarker(string zoneId, Color color)
            {
                if (!Definition.TryGetZone(zoneId, out var zone))
                {
                    return;
                }

                World.DrawMarker(
                    MarkerType.Cylinder,
                    zone.Position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(zone.Radius * 2f, zone.Radius * 2f, 2.1f),
                    color,
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);
            }

            private static void DrawPropMarker(Prop prop, Color color)
            {
                if (prop == null || !prop.Exists())
                {
                    return;
                }

                World.DrawMarker(
                    MarkerType.Cylinder,
                    prop.Position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(3.5f, 3.5f, 1.8f),
                    color,
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);
            }

            private static HandlerContainerTransferStage ParseStage(int rawStage)
            {
                if (rawStage < (int)HandlerContainerTransferStage.EnterDockTug)
                {
                    return HandlerContainerTransferStage.EnterDockTug;
                }

                if (rawStage > (int)HandlerContainerTransferStage.DeliverLoadedTrailer)
                {
                    return HandlerContainerTransferStage.DeliverLoadedTrailer;
                }

                return (HandlerContainerTransferStage)rawStage;
            }

            private enum HandlerContainerTransferStage
            {
                EnterDockTug = 0,
                AttachTrailerToDockTug = 1,
                MoveTrailerToLoadingBay = 2,
                EnterDockHandler = 3,
                LoadContainer = 4,
                LeaveLoadedTrailerAtTransferBay = 5,
                EnterTruck = 6,
                AttachLoadedTrailerToTruck = 7,
                DeliverLoadedTrailer = 8,
            }
        }
    }

    public sealed class SpecialMissionListing
    {
        public string MissionId { get; set; }

        public string Category { get; set; }

        public string Name { get; set; }

        public string Summary { get; set; }

        public string Description { get; set; }

        public float Reward { get; set; }

        public bool Repeatable { get; set; }

        public bool IsUnlocked { get; set; }

        public bool IsActive { get; set; }

        public bool CanAccept { get; set; }

        public int CompletionCount { get; set; }

        public int RepeatCooldownInGameMinutes { get; set; }

        public int RepeatCooldownInGameMonths { get; set; }

        public int RepeatCooldownRemainingMinutes { get; set; }

        public string AvailabilityDetail { get; set; }

        public string Objective { get; set; }
    }

    public sealed class SpecialMissionPersistenceSnapshot
    {
        public SpecialMissionPersistenceSnapshot()
        {
            CompletedMissions = new List<SpecialMissionCompletionSnapshot>();
            AvailableMissionAnnouncements = new List<SpecialMissionAvailabilitySnapshot>();
        }

        public List<SpecialMissionCompletionSnapshot> CompletedMissions { get; }

        public List<SpecialMissionAvailabilitySnapshot> AvailableMissionAnnouncements { get; }

        public ActiveSpecialMissionPersistenceSnapshot ActiveMission { get; set; }

        public bool HasData
        {
            get { return CompletedMissions.Count > 0 || AvailableMissionAnnouncements.Count > 0 || ActiveMission != null; }
        }
    }

    public sealed class SpecialMissionAvailabilitySnapshot
    {
        public string MissionId { get; set; }
    }

    public sealed class SpecialMissionCompletionSnapshot
    {
        public string MissionId { get; set; }

        public int CompletionCount { get; set; }

        public int LastCompletedInGameMinute { get; set; }
    }

    public sealed class ActiveSpecialMissionPersistenceSnapshot
    {
        public string MissionId { get; set; }

        public int StageIndex { get; set; }

        public bool HandlerContainerPickedUp { get; set; }

        public bool HandlerContainerLoaded { get; set; }
    }
}