using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GTA;
using LSOL.Systems;
using LSOL.UI;

namespace LSOL
{
    public sealed partial class LSOLScript
    {
        private void ResetSaveSessionState()
        {
            _npcLogisticsManager.ClearAll();
            _fleetManager.DespawnOwnedFleet();
            _fleetManager.ClearAllStates();
            _vehicleFuelSystem.ClearAllStates();
            _cargoTransferController.ClearState();
            _barrierInteractionHandler.ClearState();
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
            if (File.Exists(filePath))
            {
                ShowStatus(Text(ModTextKey.DetailSaveExists));
                return;
            }

            _pendingSaveName = saveName;
            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            _pendingVehicleFuelDifficultyEnabled = _vehicleFuelDifficultyEnabled;
            _pendingCargoDamageDifficultyEnabled = _cargoDamageDifficultyEnabled;
            _pendingIndustryPricingDifficultyEnabled = _industryPricingDifficultyEnabled;
            _pendingLicensingDifficultyEnabled = _licensingDifficultyEnabled;
            _pendingEconomyDifficultyPreset = _economyDifficultyPreset;
            _pendingNpcWeeklyWageDifficulty = _npcWeeklyWageDifficulty;

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
            if (File.Exists(filePath))
            {
                ShowStatus(Text(ModTextKey.DetailSaveExists));
                return;
            }

            _vehicleFuelDifficultyEnabled = _pendingVehicleFuelDifficultyEnabled;
            _cargoDamageDifficultyEnabled = _pendingCargoDamageDifficultyEnabled;
            _industryPricingDifficultyEnabled = _pendingIndustryPricingDifficultyEnabled;
            _licensingDifficultyEnabled = _pendingLicensingDifficultyEnabled;
            _economyDifficultyPreset = _pendingEconomyDifficultyPreset;
            _npcWeeklyWageDifficulty = _pendingNpcWeeklyWageDifficulty;
            _difficultySettingsLocked = true;
            _industryStatePath = filePath;
            ApplyDifficultySettingsToSystems();
            ResetSaveSessionState();
            _industryManager.ResetIndustriesToDefaults();
            _territoryManager.Reset();
            _tabletStateStore.ResetAnalyticsState();
            _profit = GetSelectedStartingBalance();
            _currentStartingBalance = _profit;

            if (!TrySaveIndustryPersistenceToPath(filePath))
            {
                return;
            }

            var createdSaveName = _pendingSaveName;
            _pendingSaveName = string.Empty;
            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            _pendingVehicleFuelDifficultyEnabled = _vehicleFuelDifficultyEnabled;
            _pendingCargoDamageDifficultyEnabled = _cargoDamageDifficultyEnabled;
            _pendingIndustryPricingDifficultyEnabled = _industryPricingDifficultyEnabled;
            _pendingLicensingDifficultyEnabled = _licensingDifficultyEnabled;
            _pendingEconomyDifficultyPreset = _economyDifficultyPreset;
            _pendingNpcWeeklyWageDifficulty = _npcWeeklyWageDifficulty;
            ReturnToSavingOptionsMenu();
            ShowStatus(Text(ModTextKey.DetailSaveCreated, createdSaveName), 4000);
        }

        private void LoadNamedSave(NamedSaveEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
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
            if (!TryLoadIndustryPersistenceFromPath(entry.FilePath, true, out loadResult))
            {
                return;
            }

            _industryStatePath = entry.FilePath;
            ApplyLoadedPersistenceMetadata(loadResult.Metadata, true);
            ReturnToSavingOptionsMenu();
            ShowStatus(Text(ModTextKey.DetailSaveLoaded, entry.DisplayName), 4000);
        }

        private void DeleteNamedSave(NamedSaveEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
            {
                ShowStatus(Text(ModTextKey.DetailSelectedSaveMissing));
                RebuildSaveSlotsMenuItems();
                return;
            }

            var deletedActiveSave = PathsEqual(_industryStatePath, entry.FilePath);

            try
            {
                File.Delete(entry.FilePath);
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
                    _vehicleFuelDifficultyEnabled = false;
                    _cargoDamageDifficultyEnabled = true;
                    _industryPricingDifficultyEnabled = false;
                    _licensingDifficultyEnabled = false;
                    _language = ModLanguage.English;
                    _economyDifficultyPreset = EconomyDifficultyPreset.Standard;
                    _colorblindMode = ColorblindMode.Off;
                    _npcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Standard;
                    _difficultySettingsLocked = false;
                    ApplyPresentationSettings(false);
                    ApplyDifficultySettingsToSystems();
                    ResetSaveSessionState();
                    _industryManager.ResetIndustriesToDefaults();
                    _territoryManager.Reset();
                    _tabletStateStore.ResetAnalyticsState();
                    _profit = DefaultStartingBalance;
                    _currentStartingBalance = DefaultStartingBalance;
                }

                _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
                _pendingVehicleFuelDifficultyEnabled = _vehicleFuelDifficultyEnabled;
                _pendingCargoDamageDifficultyEnabled = _cargoDamageDifficultyEnabled;
                _pendingIndustryPricingDifficultyEnabled = _industryPricingDifficultyEnabled;
                _pendingLicensingDifficultyEnabled = _licensingDifficultyEnabled;
                _pendingEconomyDifficultyPreset = _economyDifficultyPreset;
                _pendingNpcWeeklyWageDifficulty = _npcWeeklyWageDifficulty;
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
            catch (NotSupportedException ex)
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
            return new IndustryPersistenceMetadata
            {
                Profit = _profit,
                StartingBalance = _currentStartingBalance,
                Language = _language,
                ColorblindMode = _colorblindMode,
                VehicleFuelDifficultyEnabled = _vehicleFuelDifficultyEnabled,
                CargoDamageDifficultyEnabled = _cargoDamageDifficultyEnabled,
                IndustryPricingDifficultyEnabled = _industryPricingDifficultyEnabled,
                LicensingDifficultyEnabled = _licensingDifficultyEnabled,
                EconomyDifficultyPreset = _economyDifficultyPreset,
                NpcWeeklyWageDifficulty = _npcWeeklyWageDifficulty,
                DifficultySettingsLocked = _difficultySettingsLocked,
                Analytics = _tabletStateStore.CreatePersistenceSnapshot(),
                OwnedFleet = _fleetManager.CreateOwnedFleetSnapshot(_vehicleFuelSystem),
                NpcLogistics = _npcLogisticsManager.CreatePersistenceSnapshot(),
            };
        }

        private void ApplyLoadedPersistenceMetadata(IndustryPersistenceMetadata metadata, bool lockDifficultySettings)
        {
            if (metadata != null && metadata.HasGameplayMetadata)
            {
                _profit = metadata.Profit;
                _currentStartingBalance = metadata.StartingBalance;
                _language = metadata.Language ?? ModLanguage.English;
                _vehicleFuelDifficultyEnabled = metadata.VehicleFuelDifficultyEnabled;
                _cargoDamageDifficultyEnabled = metadata.CargoDamageDifficultyEnabled;
                _industryPricingDifficultyEnabled = metadata.IndustryPricingDifficultyEnabled;
                _licensingDifficultyEnabled = metadata.LicensingDifficultyEnabled;
                _economyDifficultyPreset = metadata.EconomyDifficultyPreset;
                _colorblindMode = metadata.ColorblindMode ?? ColorblindMode.Off;
                _npcWeeklyWageDifficulty = metadata.NpcWeeklyWageDifficulty;
                _difficultySettingsLocked = lockDifficultySettings || metadata.DifficultySettingsLocked;
            }
            else
            {
                _language = ModLanguage.English;
                _industryPricingDifficultyEnabled = false;
                _licensingDifficultyEnabled = false;
                _economyDifficultyPreset = EconomyDifficultyPreset.Standard;
                _colorblindMode = ColorblindMode.Off;
                _npcWeeklyWageDifficulty = NpcWeeklyWageDifficulty.Standard;
                _difficultySettingsLocked = lockDifficultySettings;
            }

            ApplyPresentationSettings(false);
            _tabletStateStore.ApplyPersistenceSnapshot(metadata != null ? metadata.Analytics : null);

            _selectedStartingBalanceIndex = GetNearestStartingBalanceIndex(_currentStartingBalance);
            _pendingVehicleFuelDifficultyEnabled = _vehicleFuelDifficultyEnabled;
            _pendingCargoDamageDifficultyEnabled = _cargoDamageDifficultyEnabled;
            _pendingIndustryPricingDifficultyEnabled = _industryPricingDifficultyEnabled;
            _pendingLicensingDifficultyEnabled = _licensingDifficultyEnabled;
            _pendingEconomyDifficultyPreset = _economyDifficultyPreset;
            _pendingNpcWeeklyWageDifficulty = _npcWeeklyWageDifficulty;
            ApplyDifficultySettingsToSystems();
            _npcLogisticsManager.ApplyPersistenceSnapshot(metadata != null ? metadata.NpcLogistics : null);
            _fleetManager.RestoreOwnedFleet(
                metadata != null ? metadata.OwnedFleet : null,
                _vehicleFuelSystem,
                GetGroundPosition);
            _tabletStateStore.MarkAllDirty();
            RebuildModControlMenuItems();
            RebuildSavingOptionsMenuItems();
            RebuildDifficultyMenuItems();
            RebuildOptionsMenuItems();
        }

        private string ResolveConfigPath()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(assemblyDir, "LSOL.ini"),
                Path.Combine(BaseDirectory, "LSOL.ini"),
                Path.Combine(BaseDirectory, "scripts", "LSOL.ini"),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return candidates[0];
        }

        private string ResolveIndustryStatePath(string configPath)
        {
            var configDirectory = string.IsNullOrWhiteSpace(configPath)
                ? string.Empty
                : Path.GetDirectoryName(configPath) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                return Path.Combine(configDirectory, "LSOL.state.ini");
            }

            return Path.Combine(BaseDirectory, "LSOL.state.ini");
        }

        private string ResolveSavegamesDirectoryPath(string configPath)
        {
            var configDirectory = string.IsNullOrWhiteSpace(configPath)
                ? string.Empty
                : Path.GetDirectoryName(configPath) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                return Path.Combine(configDirectory, SavegamesDirectoryName);
            }

            return Path.Combine(BaseDirectory, SavegamesDirectoryName);
        }

        private List<NamedSaveEntry> GetAvailableNamedSaves()
        {
            if (!Directory.Exists(_savegamesDirectoryPath))
            {
                return new List<NamedSaveEntry>();
            }

            return Directory
                .GetFiles(_savegamesDirectoryPath, "*.state.ini")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new NamedSaveEntry(ExtractSaveDisplayName(path), path))
                .ToList();
        }

        private string BuildSaveSlotDetail(NamedSaveEntry entry)
        {
            var action = _saveSlotMenuAction == SaveSlotMenuAction.Load
                ? "Load this saved game."
                : "Delete this saved game.";

            if (entry == null)
            {
                return action;
            }

            if (PathsEqual(entry.FilePath, _industryStatePath))
            {
                action = "Currently active save. " + action;
            }

            try
            {
                var lastWriteTime = File.GetLastWriteTime(entry.FilePath);
                return string.Format("{0} Last updated {1:yyyy-MM-dd HH:mm}.", action, lastWriteTime);
            }
            catch
            {
                return action;
            }
        }

        private bool TryGetActiveNamedSave(out NamedSaveEntry activeSave)
        {
            activeSave = null;
            if (!IsNamedSavePath(_industryStatePath))
            {
                return false;
            }

            activeSave = new NamedSaveEntry(ExtractSaveDisplayName(_industryStatePath), _industryStatePath);
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
            return Path.Combine(_savegamesDirectoryPath, saveName + ".state.ini");
        }

        private bool IsNamedSavePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || PathsEqual(filePath, _defaultIndustryStatePath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            return filePath.EndsWith(".state.ini", StringComparison.OrdinalIgnoreCase)
                && PathsEqual(directory, _savegamesDirectoryPath);
        }

        private static string ExtractSaveDisplayName(string filePath)
        {
            var fileName = Path.GetFileName(filePath) ?? string.Empty;
            const string stateSuffix = ".state.ini";
            if (fileName.EndsWith(stateSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - stateSuffix.Length);
            }

            return Path.GetFileNameWithoutExtension(fileName);
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