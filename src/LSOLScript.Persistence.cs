using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using GTA;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;

namespace LSOL
{
    public sealed partial class LSOLScript
    {
        private void CancelActiveRefuelDispatchForShutdown()
        {
            if (_industryRefuelService != null)
            {
                _industryRefuelService.CancelActiveDispatch();
            }
        }

        private void ResetSaveSessionState()
        {
            DisableCruiseControl(false);
            _pendingOwnedFleetRestore = null;
            _pendingPropertyRestore = null;
            _pendingSpecialMissionRestore = null;
            _specialMissionManager.ResetState();
            _globalMarket.Reset(Game.GameTime);
            _npcLogisticsManager.ClearAll();
            _playerContractsManager.ClearAll();
            _propertyManager.ResetState();
            _fleetManager.DespawnOwnedFleet();
            _fleetManager.ClearAllStates();
            _vehicleFuelSystem.ClearAllStates();
            _vehicleLoadPowerService.ClearAllStates();
            CancelActiveRefuelDispatchForShutdown();
            _officeObjectManager.Cleanup();
            _cargoTransferController.ClearState();
            _financeTracker.Clear();
            _bankLoanManager.ApplyPersistenceSnapshot(null, GetCurrentInGameWeekMinute());
            _playerSuccessTracker.ResetForNewSave(_profit);
            SyncPlayerSuccessBalance(false);
            ResetAlertRuleRuntimeState(Game.GameTime, true);
            _tabletStateStore.MarkAllDirty();
        }

        private void RestorePendingWorldState()
        {
            if (_pendingOwnedFleetRestore == null && _pendingPropertyRestore == null && _pendingSpecialMissionRestore == null)
            {
                return;
            }

            var ownedFleetSnapshot = _pendingOwnedFleetRestore;
            var propertySnapshot = _pendingPropertyRestore;
            var specialMissionSnapshot = _pendingSpecialMissionRestore;
            _pendingOwnedFleetRestore = null;
            _pendingPropertyRestore = null;
            _pendingSpecialMissionRestore = null;

            if (propertySnapshot != null && propertySnapshot.HasData)
            {
                _propertyManager.RestoreWorldState(
                    _fleetManager,
                    _vehicleFuelSystem,
                    GetGroundPosition);
            }
            else
            {
                _fleetManager.RestoreOwnedFleet(
                    ownedFleetSnapshot,
                    _vehicleFuelSystem,
                    GetGroundPosition);
            }

            _specialMissionManager.ApplyPersistenceSnapshot(specialMissionSnapshot);
            ReevaluatePlayerSuccesses(false);
            _tabletStateStore.MarkAllDirty();
        }

        private void PromptForNewSave()
        {
            var rawName = Game.GetUserInput(WindowTitle.EnterMessage60, string.Empty, MaxSaveNameLength);
            if (string.IsNullOrWhiteSpace(rawName))
            {
                ShowStatus(Text(ModTextKey.DetailSaveCreationCancelled));
                return;
            }

            var saveName = SanitizeSaveName(rawName);
            if (string.IsNullOrWhiteSpace(saveName))
            {
                ShowStatus(Text(ModTextKey.DetailSaveNameInvalid));
                return;
            }

            var filePath = BuildNamedSavePath(saveName);
            if (PersistencePathExists(filePath))
            {
                ShowStatus(Text(ModTextKey.DetailSaveExists));
                return;
            }

            _pendingSaveName = saveName;
            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            SyncPendingDifficultyProfileFromLive();

            _savingOptionsMenu.Close();
            RebuildNewSaveSetupMenuItems();
            _newSaveSetupMenu.Open();
        }

        private void FinalizeNewSave()
        {
            if (string.IsNullOrWhiteSpace(_pendingSaveName))
            {
                ShowStatus(Text(ModTextKey.DetailNoPendingSaveName));
                return;
            }

            var filePath = BuildNamedSavePath(_pendingSaveName);
            if (PersistencePathExists(filePath))
            {
                ShowStatus(Text(ModTextKey.DetailSaveExists));
                return;
            }

            SetLiveDifficultyProfile(CapturePendingDifficultyProfile());
            _difficultySettingsLocked = true;
            _industryStatePath = filePath;
            ApplyDifficultySettingsToSystems();
            ResetSaveSessionState();
            _industryManager.ResetIndustriesToDefaults();
            _territoryManager.Reset();
            _tabletStateStore.ResetAnalyticsState();
            _profit = GetSelectedStartingBalance();
            _currentStartingBalance = _profit;
            _playerSuccessTracker.ResetForNewSave(_profit);
            SyncPlayerSuccessBalance(false);

            if (!TrySaveIndustryPersistenceToPath(filePath))
            {
                return;
            }

            var createdSaveName = _pendingSaveName;
            _pendingSaveName = string.Empty;
            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            SyncPendingDifficultyProfileFromLive();
            ReturnToSavingOptionsMenu();
            ShowStatus(Text(ModTextKey.DetailSaveCreated, createdSaveName), 4000);
        }

        private void LoadNamedSave(NamedSaveEntry entry)
        {
            _pendingDeleteSavePath = null;

            var loadPath = ResolveExistingSavePath(entry);
            if (entry == null || string.IsNullOrWhiteSpace(loadPath) || !File.Exists(loadPath))
            {
                ShowStatus(Text(ModTextKey.DetailSelectedSaveMissing));
                RebuildSaveSlotsMenuItems();
                return;
            }

            if (_industryPersistenceEnabled)
            {
                TrySaveIndustryPersistence();
            }

            IndustryPersistenceLoadResult loadResult;
            if (!TryLoadIndustryPersistenceFromPath(loadPath, true, out loadResult))
            {
                return;
            }

            _industryStatePath = entry.FilePath;
            ApplyLoadedPersistenceMetadata(loadResult.Metadata, true);
            ReturnToSavingOptionsMenu();
            ShowStatus(Text(ModTextKey.DetailSaveLoaded, entry.DisplayName), 4000);
        }

        private void ConfirmOrDeleteNamedSave(NamedSaveEntry entry)
        {
            var existingPath = ResolveExistingSavePath(entry);
            if (entry == null || string.IsNullOrWhiteSpace(existingPath) || !File.Exists(existingPath))
            {
                _pendingDeleteSavePath = null;
                ShowStatus(Text(ModTextKey.DetailSelectedSaveMissing));
                RebuildSaveSlotsMenuItems();
                return;
            }

            if (!PathsEqual(_pendingDeleteSavePath, entry.FilePath))
            {
                _pendingDeleteSavePath = entry.FilePath;
                RebuildSaveSlotsMenuItems();
                ShowStatus(string.Format("Press Enter again to delete {0}.", entry.DisplayName), 4000);
                return;
            }

            DeleteNamedSave(entry);
        }

        private void DeleteNamedSave(NamedSaveEntry entry)
        {
            _pendingDeleteSavePath = null;

            var existingPath = ResolveExistingSavePath(entry);
            if (entry == null || string.IsNullOrWhiteSpace(existingPath) || !File.Exists(existingPath))
            {
                ShowStatus(Text(ModTextKey.DetailSelectedSaveMissing));
                RebuildSaveSlotsMenuItems();
                return;
            }

            var deletedActiveSave = PathsEqual(_industryStatePath, entry.FilePath)
                || PathsEqual(_industryStatePath, existingPath);

            try
            {
                if (File.Exists(entry.FilePath))
                {
                    File.Delete(entry.FilePath);
                }

                if (!PathsEqual(existingPath, entry.FilePath) && File.Exists(existingPath))
                {
                    File.Delete(existingPath);
                }
            }
            catch (IOException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Delete selected save", ex));
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Delete selected save", ex));
                return;
            }
            catch (ArgumentException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Delete selected save", ex));
                return;
            }
            catch (InvalidDataException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Delete selected save", ex));
                return;
            }
            catch (NotSupportedException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Delete selected save", ex));
                return;
            }

            if (deletedActiveSave)
            {
                _industryStatePath = _defaultIndustryStatePath;

                IndustryPersistenceLoadResult loadResult;
                if (TryLoadIndustryPersistenceFromPath(_defaultIndustryStatePath, false, out loadResult))
                {
                    ApplyLoadedPersistenceMetadata(loadResult.Metadata, false);
                }
                else
                {
                    _language = ModLanguage.English;
                    _colorblindMode = ColorblindMode.Off;
                    _useMetricSpeedDisplay = false;
                    SetLiveDifficultyProfile(DifficultySettingsProfile.CreateDefault());
                    _difficultySettingsLocked = false;
                    ApplyPresentationSettings(false);
                    ApplyDifficultySettingsToSystems();
                    ResetSaveSessionState();
                    _industryManager.ResetIndustriesToDefaults();
                    _territoryManager.Reset();
                    _tabletStateStore.ResetAnalyticsState();
                    _profit = DefaultStartingBalance;
                    _currentStartingBalance = DefaultStartingBalance;
                    _playerSuccessTracker.ResetForNewSave(_profit);
                    SyncPlayerSuccessBalance(false);
                }

                _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
                SyncPendingDifficultyProfileFromLive();
            }

            RebuildSaveSlotsMenuItems();
            RebuildSavingOptionsMenuItems();
            RebuildModControlMenuItems();
            ShowStatus(Text(ModTextKey.DetailSaveDeleted, entry.DisplayName), 4000);
        }

        private void SaveCurrentNamedGame()
        {
            NamedSaveEntry activeSave;
            if (!TryGetActiveNamedSave(out activeSave))
            {
                ShowStatus(Text(ModTextKey.DetailCreateOrLoadNamedSave));
                return;
            }

            if (!TrySaveIndustryPersistenceToPath(activeSave.FilePath))
            {
                return;
            }

            RebuildSavingOptionsMenuItems();
            ShowStatus(Text(ModTextKey.DetailSaveSaved, activeSave.DisplayName), 4000);
        }

        private bool TryLoadIndustryPersistence(bool notifyWhenNoData)
        {
            IndustryPersistenceLoadResult loadResult;
            if (!TryLoadIndustryPersistenceFromPath(_industryStatePath, notifyWhenNoData, out loadResult))
            {
                return false;
            }

            ApplyLoadedPersistenceMetadata(loadResult.Metadata, IsNamedSavePath(_industryStatePath));

            if (loadResult.RestoredCount > 0)
            {
                ShowStatus(Text(ModTextKey.DetailLoadSavedState, loadResult.RestoredCount), 4000);
            }
            else
            {
                ShowStatus(Text(ModTextKey.DetailLoadSavedSettings), 4000);
            }

            return true;
        }

        private void TrySaveIndustryPersistence()
        {
            if (!_industryPersistenceEnabled)
            {
                return;
            }

            TrySaveIndustryPersistenceToPath(_industryStatePath);
        }

        private bool TryLoadIndustryPersistenceFromPath(string filePath, bool notifyWhenNoData, out IndustryPersistenceLoadResult loadResult)
        {
            loadResult = null;

            try
            {
                ResetSaveSessionState();
                loadResult = IndustryPersistenceManager.LoadWithMetadata(filePath, _industryManager.Industries, _territoryManager);
                if (loadResult.RestoredCount > 0 || (loadResult.Metadata != null && loadResult.Metadata.HasGameplayMetadata))
                {
                    return true;
                }

                if (notifyWhenNoData)
                {
                    ShowStatus(Text(ModTextKey.DetailNoSavedState));
                }
            }
            catch (IOException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load industry persistence data", ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load industry persistence data", ex));
            }
            catch (ArgumentException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load industry persistence data", ex));
            }
            catch (InvalidDataException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load industry persistence data", ex));
            }
            catch (NotSupportedException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load industry persistence data", ex));
            }
            catch (XmlException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load industry persistence data", ex));
            }

            return false;
        }

        private bool TrySaveIndustryPersistenceToPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowStatus(Text(ModTextKey.DetailNoSavePath));
                return false;
            }

            try
            {
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                IndustryPersistenceManager.Save(filePath, _industryManager.Industries, BuildCurrentPersistenceMetadata(), _territoryManager.CreateSnapshot());
                return true;
            }
            catch (IOException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save industry persistence data", ex));
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save industry persistence data", ex));
                return false;
            }
            catch (ArgumentException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save industry persistence data", ex));
                return false;
            }
            catch (NotSupportedException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save industry persistence data", ex));
                return false;
            }
        }

        private IndustryPersistenceMetadata BuildCurrentPersistenceMetadata()
        {
            var metadata = new IndustryPersistenceMetadata
            {
                Profit = _profit,
                StartingBalance = _currentStartingBalance,
                Language = _language,
                ColorblindMode = _colorblindMode,
                UseMetricSpeedDisplay = _useMetricSpeedDisplay,
                DifficultySettingsLocked = _difficultySettingsLocked,
                Analytics = _tabletStateStore.CreatePersistenceSnapshot(),
                Market = _globalMarket.CreatePersistenceSnapshot(Game.GameTime),
                OwnedFleet = _fleetManager.CreateOwnedFleetSnapshot(_vehicleFuelSystem),
                PropertyOwnership = _propertyManager.CreateSnapshot(_fleetManager, _vehicleFuelSystem),
                NpcLogistics = _npcLogisticsManager.CreatePersistenceSnapshot(),
                SpecialMissions = _specialMissionManager.CreatePersistenceSnapshot(),
                Finance = _financeTracker.CreatePersistenceSnapshot(),
                BankLoans = _bankLoanManager.CreatePersistenceSnapshot(),
                PlayerStatistics = _playerSuccessTracker.CreatePersistenceSnapshot(),
                PlayerContracts = _playerContractsManager.CreatePersistenceSnapshot(),
                AlertRules = EnsureAlertRules(),
            };
            CaptureLiveDifficultyProfile().ApplyToMetadata(metadata);
            return metadata;
        }

        private void ApplyLoadedPersistenceMetadata(IndustryPersistenceMetadata metadata, bool lockDifficultySettings)
        {
            if (metadata != null && metadata.HasGameplayMetadata)
            {
                _profit = metadata.Profit;
                _currentStartingBalance = metadata.StartingBalance;
                _language = metadata.Language ?? ModLanguage.English;
                _useMetricSpeedDisplay = metadata.UseMetricSpeedDisplay;
                SetLiveDifficultyProfile(DifficultySettingsProfile.FromMetadata(metadata));
                _colorblindMode = metadata.ColorblindMode ?? ColorblindMode.Off;
                _difficultySettingsLocked = lockDifficultySettings || metadata.DifficultySettingsLocked;
            }
            else
            {
                _language = ModLanguage.English;
                _useMetricSpeedDisplay = false;
                SetLiveDifficultyProfile(DifficultySettingsProfile.CreateDefault());
                _colorblindMode = ColorblindMode.Off;
                _difficultySettingsLocked = lockDifficultySettings;
            }

            ApplyPresentationSettings(false);
            _financeTracker.ApplyPersistenceSnapshot(metadata != null ? metadata.Finance : null);
            _bankLoanManager.ApplyPersistenceSnapshot(metadata != null ? metadata.BankLoans : null, GetCurrentInGameWeekMinute());
            _globalMarket.ApplyPersistenceSnapshot(metadata != null ? metadata.Market : null, Game.GameTime);
            _tabletStateStore.ApplyPersistenceSnapshot(metadata != null ? metadata.Analytics : null);
            _playerSuccessTracker.ApplyPersistenceSnapshot(metadata != null ? metadata.PlayerStatistics : null, _profit);
            SyncPlayerSuccessBalance(false);

            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            SyncPendingDifficultyProfileFromLive();
            _alertRules = metadata != null && metadata.AlertRules != null
                ? metadata.AlertRules
                : new AlertRulesPersistenceSnapshot();
            ResetAlertRuleRuntimeState(Game.GameTime, true);
            ApplyDifficultySettingsToSystems();
            var ownedFleetSnapshot = metadata != null ? metadata.OwnedFleet : null;
            var propertySnapshot = metadata != null ? metadata.PropertyOwnership : null;
            var specialMissionSnapshot = metadata != null ? metadata.SpecialMissions : null;
            if ((propertySnapshot == null || !propertySnapshot.HasData) && ownedFleetSnapshot != null && ownedFleetSnapshot.HasData)
            {
                propertySnapshot = _propertyManager.CreateLegacyMigrationSnapshot(ownedFleetSnapshot, GetCurrentInGameWeekMinute());
                if (propertySnapshot != null && propertySnapshot.HasData)
                {
                    ownedFleetSnapshot = null;
                }
            }

            _propertyManager.ApplySnapshot(propertySnapshot, GetCurrentInGameWeekMinute());
            _npcLogisticsManager.ApplyPersistenceSnapshot(metadata != null ? metadata.NpcLogistics : null);
            _playerContractsManager.ApplyPersistenceSnapshot(metadata != null ? metadata.PlayerContracts : null);
            if (_isConstructing)
            {
                _pendingOwnedFleetRestore = ownedFleetSnapshot != null && ownedFleetSnapshot.HasData
                    ? ownedFleetSnapshot
                    : null;
                _pendingPropertyRestore = propertySnapshot != null && propertySnapshot.HasData
                    ? propertySnapshot
                    : null;
                _pendingSpecialMissionRestore = specialMissionSnapshot != null && specialMissionSnapshot.HasData
                    ? specialMissionSnapshot
                    : null;
            }
            else
            {
                _pendingOwnedFleetRestore = null;
                _pendingPropertyRestore = null;
                _pendingSpecialMissionRestore = null;
                if (propertySnapshot != null && propertySnapshot.HasData)
                {
                    _propertyManager.RestoreWorldState(
                        _fleetManager,
                        _vehicleFuelSystem,
                        GetGroundPosition);
                }
                else
                {
                    _fleetManager.RestoreOwnedFleet(
                        ownedFleetSnapshot,
                        _vehicleFuelSystem,
                        GetGroundPosition);
                }

                _specialMissionManager.ApplyPersistenceSnapshot(specialMissionSnapshot);
            }

            ReevaluatePlayerSuccesses(false);

            _tabletStateStore.MarkAllDirty();
            RebuildModControlMenuItems();
            RebuildSavingOptionsMenuItems();
            RebuildDifficultyMenuItems();
            RebuildOptionsMenuItems();
        }

        private string ResolveRuntimeDirectory()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var scriptsDirectory = Path.Combine(BaseDirectory, "scripts");
            var candidates = new[]
            {
                assemblyDir,
                scriptsDirectory,
                BaseDirectory,
            }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                var configDirectory = Path.Combine(candidates[i], "LSOL_Config");
                if (Directory.Exists(configDirectory))
                {
                    return candidates[i];
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (Directory.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return BaseDirectory;
        }

        private string ResolveConfigDirectory()
        {
            return Path.Combine(ResolveRuntimeDirectory(), "LSOL_Config");
        }

        private string ResolveIndustryStatePath()
        {
            return Path.Combine(ResolveRuntimeDirectory(), "LSOL.state.xml");
        }

        private string ResolveSavegamesDirectoryPath()
        {
            return Path.Combine(ResolveRuntimeDirectory(), SavegamesDirectoryName);
        }

        private List<NamedSaveEntry> GetAvailableNamedSaves()
        {
            if (!Directory.Exists(_savegamesDirectoryPath))
            {
                return new List<NamedSaveEntry>();
            }

            return Directory
                .GetFiles(_savegamesDirectoryPath, "*.state.xml")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new NamedSaveEntry(ExtractSaveDisplayName(path), path, path))
                .Concat(
                    Directory
                        .GetFiles(_savegamesDirectoryPath, "*.state.ini")
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .Select(path => new NamedSaveEntry(
                            ExtractSaveDisplayName(path),
                            BuildNamedSavePath(ExtractSaveDisplayName(path)),
                            path))
                        .Where(entry => !string.IsNullOrWhiteSpace(entry.DisplayName)))
                .GroupBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string BuildSaveSlotDetail(NamedSaveEntry entry)
        {
            var action = BuildSaveSlotAction(entry);
            if (entry == null)
            {
                return action;
            }

            DateTime? lastWriteTime;
            IndustryPersistenceMetadata metadata;
            return TryLoadSaveProfilePreview(entry, out lastWriteTime, out metadata)
                ? SaveProfilePreviewFormatter.BuildDetail(
                    action,
                    lastWriteTime,
                    metadata,
                    _propertyManager != null ? _propertyManager.Offices : null)
                : action;
        }

        private string BuildSaveSlotAction(NamedSaveEntry entry)
        {
            var action = _saveSlotMenuAction == SaveSlotMenuAction.Load
                ? "Load this saved game."
                : "Delete this saved game.";

            if (entry == null)
            {
                return action;
            }

            if (_saveSlotMenuAction == SaveSlotMenuAction.Delete && PathsEqual(_pendingDeleteSavePath, entry.FilePath))
            {
                action = "Press Enter again to confirm deletion.";
            }

            return PathsEqual(entry.FilePath, _industryStatePath)
                ? "Currently active save. " + action
                : action;
        }

        private static bool TryLoadSaveProfilePreview(
            NamedSaveEntry entry,
            out DateTime? lastWriteTime,
            out IndustryPersistenceMetadata metadata)
        {
            lastWriteTime = null;
            metadata = null;

            var existingPath = ResolveExistingSavePath(entry);
            if (string.IsNullOrWhiteSpace(existingPath) || !File.Exists(existingPath))
            {
                return false;
            }

            try
            {
                lastWriteTime = File.GetLastWriteTime(existingPath);
            }
            catch
            {
                lastWriteTime = null;
            }

            try
            {
                var result = IndustryPersistenceManager.LoadWithMetadata(existingPath, Array.Empty<Industry>());
                metadata = result != null ? result.Metadata : null;
            }
            catch
            {
                metadata = null;
            }

            return lastWriteTime.HasValue || metadata != null;
        }

        private bool TryGetActiveNamedSave(out NamedSaveEntry activeSave)
        {
            activeSave = null;
            if (!IsNamedSavePath(_industryStatePath))
            {
                return false;
            }

            var displayName = ExtractSaveDisplayName(_industryStatePath);
            var canonicalPath = BuildNamedSavePath(displayName);
            activeSave = new NamedSaveEntry(displayName, canonicalPath, ResolveExistingPersistencePath(canonicalPath));
            return true;
        }

        private string GetCurrentSaveLabel()
        {
            NamedSaveEntry activeSave;
            return TryGetActiveNamedSave(out activeSave) ? activeSave.DisplayName : Text(ModTextKey.ValueDefaultAutosave);
        }

        private float GetSelectedStartingBalance()
        {
            if (StartingBalanceOptions.Length == 0)
            {
                return DefaultStartingBalance;
            }

            if (_selectedStartingBalanceIndex < 0 || _selectedStartingBalanceIndex >= StartingBalanceOptions.Length)
            {
                _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(DefaultStartingBalance);
            }

            return StartingBalanceOptions[_selectedStartingBalanceIndex];
        }

        private int GetNearestStartingBalanceIndex(float value)
        {
            if (StartingBalanceOptions.Length == 0)
            {
                return 0;
            }

            var bestIndex = 0;
            var bestDistance = Math.Abs(StartingBalanceOptions[0] - value);
            for (int i = 1; i < StartingBalanceOptions.Length; i++)
            {
                var distance = Math.Abs(StartingBalanceOptions[i] - value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private string BuildNamedSavePath(string saveName)
        {
            return Path.Combine(_savegamesDirectoryPath, saveName + ".state.xml");
        }

        private bool IsNamedSavePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)
                || PathsEqual(filePath, _defaultIndustryStatePath)
                || PathsEqual(filePath, ResolveLegacyPersistencePath(_defaultIndustryStatePath)))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            return (filePath.EndsWith(".state.xml", StringComparison.OrdinalIgnoreCase)
                    || filePath.EndsWith(".state.ini", StringComparison.OrdinalIgnoreCase))
                && PathsEqual(directory, _savegamesDirectoryPath);
        }

        private static string ExtractSaveDisplayName(string filePath)
        {
            var fileName = Path.GetFileName(filePath) ?? string.Empty;
            const string xmlStateSuffix = ".state.xml";
            if (fileName.EndsWith(xmlStateSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - xmlStateSuffix.Length);
            }

            const string legacyStateSuffix = ".state.ini";
            if (fileName.EndsWith(legacyStateSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - legacyStateSuffix.Length);
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static string ResolveLegacyPersistencePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            return Path.ChangeExtension(filePath, ".ini");
        }

        private static string ResolveExistingPersistencePath(string filePath)
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
            return File.Exists(legacyPath) ? legacyPath : filePath;
        }

        private static string ResolveExistingSavePath(NamedSaveEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return ResolveExistingPersistencePath(string.IsNullOrWhiteSpace(entry.LoadPath) ? entry.FilePath : entry.LoadPath);
        }

        private static bool PersistencePathExists(string filePath)
        {
            return !string.IsNullOrWhiteSpace(ResolveExistingPersistencePath(filePath))
                && (File.Exists(filePath) || File.Exists(ResolveLegacyPersistencePath(filePath)));
        }

        private static string SanitizeSaveName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var filteredCharacters = rawName
                .Trim()
                .Where(character => !invalidCharacters.Contains(character))
                .ToArray();

            var sanitized = new string(filteredCharacters).Trim();
            return sanitized.Length > MaxSaveNameLength
                ? sanitized.Substring(0, MaxSaveNameLength).Trim()
                : sanitized;
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}