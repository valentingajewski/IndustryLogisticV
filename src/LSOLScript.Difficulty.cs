using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using GTA;
using LSOL.Systems;
using LSOL.UI;
using OfficeMenuItem = LSOL.UI.MenuItem;

namespace LSOL
{
    public sealed partial class LSOLScript
    {
        private enum DifficultyProfileTarget
        {
            Live = 0,
            Pending = 1,
        }

        private DifficultySettingsProfile CaptureLiveDifficultyProfile()
        {
            return new DifficultySettingsProfile
            {
                EconomyDifficultyPreset = _economyDifficultyPreset,
                NpcWeeklyWageDifficulty = _npcWeeklyWageDifficulty,
                VehicleFuelDifficultyEnabled = _vehicleFuelDifficultyEnabled,
                CargoWeightPowerDifficultyEnabled = _cargoWeightPowerDifficultyEnabled,
                CargoDamageDifficultyEnabled = _cargoDamageDifficultyEnabled,
                IndustryPricingDifficultyEnabled = _industryPricingDifficultyEnabled,
                LicensingDifficultyEnabled = _licensingDifficultyEnabled,
                CorridorRestrictionDifficultyEnabled = _corridorRestrictionDifficultyEnabled,
                ReputationDifficultyEnabled = _reputationDifficultyEnabled,
                OfficeGarageLimitDifficultyEnabled = _officeGarageLimitDifficultyEnabled,
                OfficeNpcLimitDifficultyEnabled = _officeNpcLimitDifficultyEnabled,
                NpcRouteLimit = _npcRouteLimit,
            };
        }

        private DifficultySettingsProfile CapturePendingDifficultyProfile()
        {
            return new DifficultySettingsProfile
            {
                EconomyDifficultyPreset = _pendingEconomyDifficultyPreset,
                NpcWeeklyWageDifficulty = _pendingNpcWeeklyWageDifficulty,
                VehicleFuelDifficultyEnabled = _pendingVehicleFuelDifficultyEnabled,
                CargoWeightPowerDifficultyEnabled = _pendingCargoWeightPowerDifficultyEnabled,
                CargoDamageDifficultyEnabled = _pendingCargoDamageDifficultyEnabled,
                IndustryPricingDifficultyEnabled = _pendingIndustryPricingDifficultyEnabled,
                LicensingDifficultyEnabled = _pendingLicensingDifficultyEnabled,
                CorridorRestrictionDifficultyEnabled = _pendingCorridorRestrictionDifficultyEnabled,
                ReputationDifficultyEnabled = _pendingReputationDifficultyEnabled,
                OfficeGarageLimitDifficultyEnabled = _pendingOfficeGarageLimitDifficultyEnabled,
                OfficeNpcLimitDifficultyEnabled = _pendingOfficeNpcLimitDifficultyEnabled,
                NpcRouteLimit = _pendingNpcRouteLimit,
            };
        }

        private DifficultySettingsProfile CaptureDifficultyProfile(DifficultyProfileTarget target)
        {
            return target == DifficultyProfileTarget.Pending
                ? CapturePendingDifficultyProfile()
                : CaptureLiveDifficultyProfile();
        }

        private void SetLiveDifficultyProfile(DifficultySettingsProfile profile)
        {
            profile = profile ?? DifficultySettingsProfile.CreateDefault();
            _economyDifficultyPreset = profile.EconomyDifficultyPreset;
            _npcWeeklyWageDifficulty = profile.NpcWeeklyWageDifficulty;
            _vehicleFuelDifficultyEnabled = profile.VehicleFuelDifficultyEnabled;
            _cargoWeightPowerDifficultyEnabled = profile.CargoWeightPowerDifficultyEnabled;
            _cargoDamageDifficultyEnabled = profile.CargoDamageDifficultyEnabled;
            _industryPricingDifficultyEnabled = profile.IndustryPricingDifficultyEnabled;
            _licensingDifficultyEnabled = profile.LicensingDifficultyEnabled;
            _corridorRestrictionDifficultyEnabled = profile.CorridorRestrictionDifficultyEnabled;
            _reputationDifficultyEnabled = profile.ReputationDifficultyEnabled;
            _officeGarageLimitDifficultyEnabled = profile.OfficeGarageLimitDifficultyEnabled;
            _officeNpcLimitDifficultyEnabled = profile.OfficeNpcLimitDifficultyEnabled;
            _npcRouteLimit = ClampNpcRouteLimit(profile.NpcRouteLimit);
        }

        private void SetPendingDifficultyProfile(DifficultySettingsProfile profile)
        {
            profile = profile ?? DifficultySettingsProfile.CreateDefault();
            _pendingEconomyDifficultyPreset = profile.EconomyDifficultyPreset;
            _pendingNpcWeeklyWageDifficulty = profile.NpcWeeklyWageDifficulty;
            _pendingVehicleFuelDifficultyEnabled = profile.VehicleFuelDifficultyEnabled;
            _pendingCargoWeightPowerDifficultyEnabled = profile.CargoWeightPowerDifficultyEnabled;
            _pendingCargoDamageDifficultyEnabled = profile.CargoDamageDifficultyEnabled;
            _pendingIndustryPricingDifficultyEnabled = profile.IndustryPricingDifficultyEnabled;
            _pendingLicensingDifficultyEnabled = profile.LicensingDifficultyEnabled;
            _pendingCorridorRestrictionDifficultyEnabled = profile.CorridorRestrictionDifficultyEnabled;
            _pendingReputationDifficultyEnabled = profile.ReputationDifficultyEnabled;
            _pendingOfficeGarageLimitDifficultyEnabled = profile.OfficeGarageLimitDifficultyEnabled;
            _pendingOfficeNpcLimitDifficultyEnabled = profile.OfficeNpcLimitDifficultyEnabled;
            _pendingNpcRouteLimit = ClampNpcRouteLimit(profile.NpcRouteLimit);
        }

        private void SyncPendingDifficultyProfileFromLive()
        {
            SetPendingDifficultyProfile(CaptureLiveDifficultyProfile());
        }

        private bool IsDifficultyMutationLocked(DifficultyProfileTarget target)
        {
            return target == DifficultyProfileTarget.Live && _difficultySettingsLocked;
        }

        private bool EnsureDifficultyMutationAllowed(DifficultyProfileTarget target)
        {
            if (!IsDifficultyMutationLocked(target))
            {
                return true;
            }

            ShowDifficultySettingsLockedStatus();
            return false;
        }

        private void ApplyDifficultyProfileFromActions(DifficultyProfileTarget target, DifficultySettingsProfile profile)
        {
            if (target == DifficultyProfileTarget.Pending)
            {
                SetPendingDifficultyProfile(profile);
                RebuildNewSaveSetupMenuItems();
                return;
            }

            SetLiveDifficultyProfile(profile);
            ApplyDifficultySettingsToSystems();
            RebuildDifficultyMenuItems();
        }

        private void ReturnToDifficultySourceMenu(DifficultyProfileTarget target)
        {
            _difficultyTemplateMenu.Close();
            _difficultyActionsMenu.Close();
            if (target == DifficultyProfileTarget.Pending)
            {
                RebuildNewSaveSetupMenuItems();
                _newSaveSetupMenu.Open();
                return;
            }

            RebuildDifficultyMenuItems();
            _difficultyMenu.Open();
        }

        private OfficeMenuItem CreateDifficultyActionsEntry(DifficultyProfileTarget target)
        {
            return new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.RowDifficultyTemplatesActions),
                DetailFactory = () => CurrentDifficultyTemplateActionsDetail(target),
                OnActivate = () => OpenDifficultyActionsMenu(target),
            };
        }

        private List<OfficeMenuItem> BuildDifficultyMenuRootItems(DifficultyProfileTarget target)
        {
            var items = new List<OfficeMenuItem>();
            foreach (var kind in DifficultyMenuLayout.BuildRootPrefix(target == DifficultyProfileTarget.Pending))
            {
                items.Add(CreateDifficultyRootMenuItem(target, kind));
            }

            return items;
        }

        private OfficeMenuItem CreateDifficultyRootMenuItem(DifficultyProfileTarget target, DifficultyRootMenuEntryKind kind)
        {
            switch (kind)
            {
                case DifficultyRootMenuEntryKind.StartingBalance:
                    return new OfficeMenuItem
                    {
                        CaptionFactory = CurrentStartingBalanceCaption,
                        DetailFactory = () => Text(ModTextKey.DetailNewSaveStartingBalance),
                        OnLeft = () => ChangeStartingBalanceSelection(-1),
                        OnRight = () => ChangeStartingBalanceSelection(1),
                    };
                case DifficultyRootMenuEntryKind.EnableAll:
                    return CreateDifficultyBulkActionMenuItem(target, true);
                case DifficultyRootMenuEntryKind.DisableAll:
                    return CreateDifficultyBulkActionMenuItem(target, false);
                case DifficultyRootMenuEntryKind.Templates:
                    return CreateDifficultyActionsEntry(target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private OfficeMenuItem CreateDifficultyBulkActionMenuItem(DifficultyProfileTarget target, bool enabled)
        {
            return new OfficeMenuItem
            {
                CaptionFactory = () => Text(enabled ? ModTextKey.RowDifficultyEnableAll : ModTextKey.RowDifficultyDisableAll),
                DetailFactory = () => CurrentDifficultyBulkActionDetail(target, enabled),
                OnActivate = () => ApplyDifficultyBulkAction(target, enabled),
            };
        }

        private string CurrentDifficultyTemplateActionsDetail(DifficultyProfileTarget target)
        {
            return IsDifficultyMutationLocked(target)
                ? Text(ModTextKey.DetailDifficultyTemplatesActionsLocked)
                : Text(ModTextKey.DetailDifficultyTemplatesActions);
        }

        private string CurrentDifficultyBulkActionDetail(DifficultyProfileTarget target, bool enabled)
        {
            if (IsDifficultyMutationLocked(target))
            {
                return Text(ModTextKey.DetailDifficultyBulkActionBlockedLocked);
            }

            return Text(enabled ? ModTextKey.DetailDifficultyEnableAll : ModTextKey.DetailDifficultyDisableAll);
        }

        private List<OfficeMenuItem> BuildDifficultyMenuCoreItems(DifficultyProfileTarget target)
        {
            var items = new List<OfficeMenuItem>();
            foreach (var descriptor in DifficultySettingsCatalog.AllSettings)
            {
                items.Add(CreateDifficultyMenuItem(target, descriptor.Kind));
            }

            return items;
        }

        private OfficeMenuItem CreateDifficultyMenuItem(DifficultyProfileTarget target, DifficultySettingKind kind)
        {
            switch (kind)
            {
                case DifficultySettingKind.EconomyDifficultyPreset:
                    return target == DifficultyProfileTarget.Pending
                        ? new OfficeMenuItem
                        {
                            CaptionFactory = CurrentPendingEconomyDifficultyPresetCaption,
                            DetailFactory = CurrentPendingEconomyDifficultyPresetDetail,
                            OnLeft = () => ChangePendingEconomyDifficultyPreset(-1),
                            OnRight = () => ChangePendingEconomyDifficultyPreset(1),
                            OnActivate = () => ChangePendingEconomyDifficultyPreset(1),
                        }
                        : new OfficeMenuItem
                        {
                            CaptionFactory = CurrentEconomyDifficultyPresetCaption,
                            DetailFactory = CurrentEconomyDifficultyPresetDetail,
                            OnLeft = () => ChangeEconomyDifficultyPreset(-1),
                            OnRight = () => ChangeEconomyDifficultyPreset(1),
                            OnActivate = () => ChangeEconomyDifficultyPreset(1),
                        };
                case DifficultySettingKind.NpcWeeklyWageDifficulty:
                    return target == DifficultyProfileTarget.Pending
                        ? new OfficeMenuItem
                        {
                            CaptionFactory = CurrentPendingNpcWeeklyWageDifficultyCaption,
                            DetailFactory = CurrentPendingNpcWeeklyWageDifficultyDetail,
                            OnLeft = () => ChangePendingNpcWeeklyWageDifficulty(-1),
                            OnRight = () => ChangePendingNpcWeeklyWageDifficulty(1),
                            OnActivate = () => ChangePendingNpcWeeklyWageDifficulty(1),
                        }
                        : new OfficeMenuItem
                        {
                            CaptionFactory = CurrentNpcWeeklyWageDifficultyCaption,
                            DetailFactory = CurrentNpcWeeklyWageDifficultyDetail,
                            OnLeft = () => ChangeNpcWeeklyWageDifficulty(-1),
                            OnRight = () => ChangeNpcWeeklyWageDifficulty(1),
                            OnActivate = () => ChangeNpcWeeklyWageDifficulty(1),
                        };
                case DifficultySettingKind.VehicleFuelDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowVehicleFuel, () => Text(ModTextKey.DetailVehicleFuel), () => _pendingVehicleFuelDifficultyEnabled, TogglePendingVehicleFuelSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowVehicleFuel, () => Text(ModTextKey.DetailVehicleFuel), () => _vehicleFuelDifficultyEnabled, ToggleVehicleFuelSetting);
                case DifficultySettingKind.CargoWeightPowerDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowCargoWeightPower, () => Text(ModTextKey.DetailCargoWeightPower), () => _pendingCargoWeightPowerDifficultyEnabled, TogglePendingCargoWeightPowerSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowCargoWeightPower, () => Text(ModTextKey.DetailCargoWeightPower), () => _cargoWeightPowerDifficultyEnabled, ToggleCargoWeightPowerSetting);
                case DifficultySettingKind.CargoDamageDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowCargoDamage, () => Text(ModTextKey.DetailCargoDamage), () => _pendingCargoDamageDifficultyEnabled, TogglePendingCargoDamageSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowCargoDamage, () => Text(ModTextKey.DetailCargoDamage), () => _cargoDamageDifficultyEnabled, ToggleCargoDamageSetting);
                case DifficultySettingKind.IndustryPricingDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowIndustryPriceMechanic, () => Text(ModTextKey.DetailIndustryPriceMechanic), () => _pendingIndustryPricingDifficultyEnabled, TogglePendingIndustryPricingSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowIndustryPriceMechanic, () => Text(ModTextKey.DetailIndustryPriceMechanic), () => _industryPricingDifficultyEnabled, ToggleIndustryPricingSetting);
                case DifficultySettingKind.LicensingDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowLicensingSystem, () => Text(ModTextKey.DetailLicensingSystem), () => _pendingLicensingDifficultyEnabled, TogglePendingLicensingSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowLicensingSystem, () => Text(ModTextKey.DetailLicensingSystem), () => _licensingDifficultyEnabled, ToggleLicensingSetting);
                case DifficultySettingKind.CorridorRestrictionDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowCorridorRestriction, () => Text(ModTextKey.DetailCorridorRestriction), () => _pendingCorridorRestrictionDifficultyEnabled, TogglePendingCorridorRestrictionSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowCorridorRestriction, () => Text(ModTextKey.DetailCorridorRestriction), () => _corridorRestrictionDifficultyEnabled, ToggleCorridorRestrictionSetting);
                case DifficultySettingKind.ReputationDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowReputationSystem, () => Text(ModTextKey.DetailReputationSystem), () => _pendingReputationDifficultyEnabled, TogglePendingReputationSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowReputationSystem, () => Text(ModTextKey.DetailReputationSystem), () => _reputationDifficultyEnabled, ToggleReputationSetting);
                case DifficultySettingKind.OfficeGarageLimitDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowOfficeGarageLimit, () => Text(ModTextKey.DetailOfficeGarageLimit), () => _pendingOfficeGarageLimitDifficultyEnabled, TogglePendingOfficeGarageLimitSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowOfficeGarageLimit, () => Text(ModTextKey.DetailOfficeGarageLimit), () => _officeGarageLimitDifficultyEnabled, ToggleOfficeGarageLimitSetting);
                case DifficultySettingKind.OfficeNpcLimitDifficultyEnabled:
                    return target == DifficultyProfileTarget.Pending
                        ? CreateDifficultyCheckboxMenuItem(ModTextKey.RowOfficeNpcLimit, CurrentPendingOfficeNpcLimitDetail, () => _pendingOfficeNpcLimitDifficultyEnabled, TogglePendingOfficeNpcLimitSetting)
                        : CreateDifficultyCheckboxMenuItem(ModTextKey.RowOfficeNpcLimit, CurrentOfficeNpcLimitDetail, () => _officeNpcLimitDifficultyEnabled, ToggleOfficeNpcLimitSetting);
                case DifficultySettingKind.NpcRouteLimit:
                    return target == DifficultyProfileTarget.Pending
                        ? new OfficeMenuItem
                        {
                            CaptionFactory = CurrentPendingNpcRouteLimitCaption,
                            DetailFactory = CurrentNpcRouteLimitDetail,
                            OnLeft = () => ChangePendingNpcRouteLimit(-1),
                            OnRight = () => ChangePendingNpcRouteLimit(1),
                            OnActivate = () => ChangePendingNpcRouteLimit(1),
                        }
                        : new OfficeMenuItem
                        {
                            CaptionFactory = CurrentNpcRouteLimitCaption,
                            DetailFactory = CurrentNpcRouteLimitDetail,
                            OnLeft = () => ChangeNpcRouteLimit(-1),
                            OnRight = () => ChangeNpcRouteLimit(1),
                            OnActivate = () => ChangeNpcRouteLimit(1),
                        };
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private OfficeMenuItem CreateDifficultyCheckboxMenuItem(string captionKey, Func<string> detailFactory, Func<bool> checkboxFactory, Action onActivate)
        {
            return new OfficeMenuItem
            {
                CaptionFactory = () => Text(captionKey),
                DetailFactory = detailFactory,
                CheckboxStateFactory = checkboxFactory,
                OnActivate = onActivate,
            };
        }

        private void OpenDifficultyActionsMenu(DifficultyProfileTarget target)
        {
            _activeDifficultyProfileTarget = target;
            _difficultyTemplateMenu.Close();
            if (target == DifficultyProfileTarget.Pending)
            {
                _newSaveSetupMenu.Close();
            }
            else
            {
                _difficultyMenu.Close();
            }

            RebuildDifficultyActionsMenuItems();
            _difficultyActionsMenu.Open();
        }

        private void RebuildDifficultyActionsMenuItems()
        {
            _difficultyActionsMenu.Title = Text(ModTextKey.MenuDifficultyActionsTitle);
            _difficultyActionsMenu.Subtitle = Text(ModTextKey.MenuDifficultyActionsSubtitle);

            var items = new List<OfficeMenuItem>();
            foreach (var kind in DifficultyMenuLayout.BuildTemplateMenuActions())
            {
                items.Add(CreateDifficultyTemplateMenuActionItem(kind));
            }

            _difficultyActionsMenu.SetItems(items);
        }

        private string CurrentDifficultyLoadTemplateDetail()
        {
            return IsDifficultyMutationLocked(_activeDifficultyProfileTarget)
                ? Text(ModTextKey.DetailDifficultyTemplateBlockedLocked)
                : Text(ModTextKey.DetailDifficultyLoadTemplate);
        }

        private OfficeMenuItem CreateDifficultyTemplateMenuActionItem(DifficultyTemplateMenuEntryKind kind)
        {
            switch (kind)
            {
                case DifficultyTemplateMenuEntryKind.SaveTemplate:
                    return new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.RowDifficultySaveTemplate),
                        DetailFactory = () => Text(ModTextKey.DetailDifficultySaveTemplate),
                        OnActivate = SaveCurrentDifficultyTemplate,
                    };
                case DifficultyTemplateMenuEntryKind.LoadTemplate:
                    return new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.RowDifficultyLoadTemplate),
                        DetailFactory = CurrentDifficultyLoadTemplateDetail,
                        OnActivate = OpenDifficultyTemplateMenu,
                    };
                case DifficultyTemplateMenuEntryKind.Back:
                    return new OfficeMenuItem
                    {
                        CaptionFactory = () => Text(ModTextKey.CommonBack),
                        OnActivate = ReturnToDifficultySourceMenu,
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private void ReturnToDifficultySourceMenu()
        {
            ReturnToDifficultySourceMenu(_activeDifficultyProfileTarget);
        }

        private void ReturnToDifficultyActionsMenu()
        {
            _difficultyTemplateMenu.Close();
            RebuildDifficultyActionsMenuItems();
            _difficultyActionsMenu.Open();
        }

        private void ApplyDifficultyBulkAction(DifficultyProfileTarget target, bool enabled)
        {
            if (!EnsureDifficultyMutationAllowed(target))
            {
                return;
            }

            var profile = CaptureDifficultyProfile(target);
            profile.ApplyBooleanBulkState(enabled);
            ApplyDifficultyProfileFromActions(target, profile);
            ReturnToDifficultySourceMenu(target);
        }

        private void SaveCurrentDifficultyTemplate()
        {
            var rawName = Game.GetUserInput(WindowTitle.EnterMessage60, string.Empty, MaxSaveNameLength);
            if (string.IsNullOrWhiteSpace(rawName))
            {
                ShowStatus(Text(ModTextKey.DetailDifficultyTemplateSaveCancelled));
                return;
            }

            var name = rawName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowStatus(Text(ModTextKey.DetailDifficultyTemplateNameInvalid));
                return;
            }

            DifficultyTemplateSaveResult result;
            if (!TrySaveDifficultyTemplate(name, CaptureDifficultyProfile(_activeDifficultyProfileTarget), out result))
            {
                return;
            }

            ShowStatus(
                Text(
                    result == DifficultyTemplateSaveResult.Overwritten
                        ? ModTextKey.DetailDifficultyTemplateOverwritten
                        : ModTextKey.DetailDifficultyTemplateSaved,
                    name),
                4000);
            RebuildDifficultyActionsMenuItems();
        }

        private void OpenDifficultyTemplateMenu()
        {
            if (!EnsureDifficultyMutationAllowed(_activeDifficultyProfileTarget))
            {
                return;
            }

            List<DifficultySettingsTemplateEntry> templates;
            if (!TryLoadDifficultyTemplates(out templates))
            {
                return;
            }

            BuildDifficultyTemplateMenuItems(templates);
            _difficultyActionsMenu.Close();
            _difficultyTemplateMenu.Open();
        }

        private void BuildDifficultyTemplateMenuItems(List<DifficultySettingsTemplateEntry> templates)
        {
            templates = templates ?? new List<DifficultySettingsTemplateEntry>();
            _difficultyTemplateMenu.Title = Text(ModTextKey.MenuDifficultyTemplateLoadTitle);
            _difficultyTemplateMenu.Subtitle = Text(ModTextKey.MenuDifficultyTemplateLoadSubtitle);

            var items = new List<OfficeMenuItem>();
            if (templates.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => Text(ModTextKey.RowNoDifficultyTemplates),
                    DetailFactory = () => Text(ModTextKey.DetailDifficultyNoTemplatesFound),
                });
            }
            else
            {
                for (int i = 0; i < templates.Count; i++)
                {
                    var template = templates[i];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => template.Name,
                        DetailFactory = () => BuildDifficultyTemplateDetail(template),
                        OnActivate = () => LoadDifficultyTemplate(template),
                    });
                }
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => Text(ModTextKey.CommonBack),
                OnActivate = ReturnToDifficultyActionsMenu,
            });

            _difficultyTemplateMenu.SetItems(items);
        }

        private string BuildDifficultyTemplateDetail(DifficultySettingsTemplateEntry template)
        {
            if (template == null || template.Profile == null)
            {
                return string.Empty;
            }

            var summary = DifficultySettingsSummaryFormatter.BuildPreviewLine(template.Profile, false);
            var flags = DifficultySettingsSummaryFormatter.BuildPreviewFlags(template.Profile);
            return flags.Count == 0
                ? summary
                : string.Format("{0}\nFlags {1}", summary, string.Join(", ", flags.ToArray()));
        }

        private void LoadDifficultyTemplate(DifficultySettingsTemplateEntry template)
        {
            if (template == null || template.Profile == null)
            {
                return;
            }

            if (!EnsureDifficultyMutationAllowed(_activeDifficultyProfileTarget))
            {
                return;
            }

            ApplyDifficultyProfileFromActions(_activeDifficultyProfileTarget, template.Profile);
            ReturnToDifficultySourceMenu();
            ShowStatus(Text(ModTextKey.DetailDifficultyTemplateLoaded, template.Name), 4000);
        }

        private bool TryLoadDifficultyTemplates(out List<DifficultySettingsTemplateEntry> templates)
        {
            templates = new List<DifficultySettingsTemplateEntry>();

            try
            {
                templates = _difficultyTemplateStore.LoadTemplates();
                return true;
            }
            catch (IOException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load difficulty templates", ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load difficulty templates", ex));
            }
            catch (ArgumentException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load difficulty templates", ex));
            }
            catch (InvalidDataException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load difficulty templates", ex));
            }
            catch (NotSupportedException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load difficulty templates", ex));
            }
            catch (XmlException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Load difficulty templates", ex));
            }

            return false;
        }

        private bool TrySaveDifficultyTemplate(string name, DifficultySettingsProfile profile, out DifficultyTemplateSaveResult result)
        {
            result = DifficultyTemplateSaveResult.Created;

            try
            {
                result = _difficultyTemplateStore.SaveTemplate(name, profile);
                return true;
            }
            catch (IOException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save difficulty template", ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save difficulty template", ex));
            }
            catch (ArgumentException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save difficulty template", ex));
            }
            catch (InvalidDataException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save difficulty template", ex));
            }
            catch (NotSupportedException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save difficulty template", ex));
            }
            catch (XmlException ex)
            {
                ShowStatus(ModDiagnostics.FormatFailure("Save difficulty template", ex));
            }

            return false;
        }
    }
}