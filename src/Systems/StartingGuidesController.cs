using System;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LSOL.Domain;
using LSOL.UI;
using Color = System.Drawing.Color;

namespace LSOL.Systems
{
    public sealed class StartingGuidesPersistenceSnapshot
    {
        public bool Enabled { get; set; }

        public bool IntroSequenceCompleted { get; set; }

        public int CurrentTaskIndex { get; set; }

        public bool IsCompleted { get; set; }

        public bool HasData
        {
            get
            {
                return Enabled
                    || IntroSequenceCompleted
                    || CurrentTaskIndex > 0
                    || IsCompleted;
            }
        }
    }

    internal sealed class StartingGuidesController
    {
        private enum IntroPhase
        {
            None = 0,
            FadeOut = 1,
            FadeIn = 2,
            Complete = 3,
        }

        private sealed class GuideStep
        {
            public GuideStep(string keyPrefix)
            {
                TitleKey = keyPrefix + ".title";
                InstructionKey = keyPrefix + ".instruction";
            }

            public string TitleKey { get; private set; }

            public string InstructionKey { get; private set; }
        }

        private const int IntroFadeDurationMs = 850;
        private const int InstructionDurationMs = 40000;
        private const int DeliveryTargetCount = 5;
        private const int CoalUnloadStepIndex = 8;
        private const int ReturnAfterFirstDeliveryStepIndex = 9;
        private const int StoreTruckStepIndex = 10;
        private const int TakeQuickJobStepIndex = 11;
        private const int CompleteQuickJobStepIndex = 12;
        private const int ReturnAfterQuickJobStepIndex = 13;
        private const int DeliveriesStepIndex = 14;
        private const int VisitBankStepIndex = 16;
        private const int CompleteMixerDeliveryStepIndex = 18;
        private const float ChecklistRowSpacing = 21f;
        private const float IndustryArrivalDistance = 28f;
        private const float OfficeArrivalDistance = 18f;
        private const float StartingGuideSpawnHeading = 90f;
        private const string PanelTitleKey = "startingGuides.panel.title";
        private const string PanelCompletedKey = "startingGuides.panel.complete";
        private const string DeliveriesProgressTitleKey = "startingGuides.step.completeDeliveries.progressTitle";
        private static readonly Vector3 StartingGuideSpawnPosition = new Vector3(-885.71f, -2580f, 13.83f);
        private static readonly string[] TipperKeywords = { "tipper", "tiptruck" };
        private static readonly string[] MixerKeywords = { "mixer" };
        private static readonly GuideStep[] Steps =
        {
            new GuideStep("startingGuides.step.rentOffice"),
            new GuideStep("startingGuides.step.claimFreeTipper"),
            new GuideStep("startingGuides.step.retrieveTipperOffice"),
            new GuideStep("startingGuides.step.setGpsQuarry"),
            new GuideStep("startingGuides.step.arriveQuarry"),
            new GuideStep("startingGuides.step.loadCoal"),
            new GuideStep("startingGuides.step.setGpsCementFactory"),
            new GuideStep("startingGuides.step.goCementFactory"),
            new GuideStep("startingGuides.step.unloadCoal"),
            new GuideStep("startingGuides.step.returnOffice"),
            new GuideStep("startingGuides.step.storeTruck"),
            new GuideStep("startingGuides.step.takeQuickJob"),
            new GuideStep("startingGuides.step.completeQuickJob"),
            new GuideStep("startingGuides.step.returnOffice"),
            new GuideStep("startingGuides.step.completeDeliveries"),
            new GuideStep("startingGuides.step.buyFirstPermit"),
            new GuideStep("startingGuides.step.visitBank"),
            new GuideStep("startingGuides.step.purchaseFreeMixer"),
            new GuideStep("startingGuides.step.completeMixerDelivery"),
        };

        private readonly IndustryManager _industryManager;
        private readonly PropertyManager _propertyManager;
        private readonly FleetManager _fleetManager;
        private readonly PlayerContractsManager _playerContractsManager;
        private readonly PlayerSuccessTracker _playerSuccessTracker;
        private readonly Func<Vector3, Vector3> _resolveGroundPosition;
        private readonly Func<Vector3> _getAirportTeleportPosition;
        private readonly Func<Industry, Vector3> _getIndustryMarkerPosition;
        private readonly Func<bool> _isMenuOpen;

        private StartingGuidesPersistenceSnapshot _snapshot;
        private IntroPhase _introPhase;
        private int _introPhaseStartedAtMs;
        private int _lastShownTaskIndex;
        private string _lastGpsIndustryId;
        private bool _bankVisited;
        private bool _coalUnloadCompleted;
        private bool _quickJobCompleted;
        private bool _mixerDeliveryCompleted;

        public StartingGuidesController(
            IndustryManager industryManager,
            PropertyManager propertyManager,
            FleetManager fleetManager,
            PlayerContractsManager playerContractsManager,
            PlayerSuccessTracker playerSuccessTracker,
            Func<Vector3, Vector3> resolveGroundPosition,
            Func<Vector3> getAirportTeleportPosition,
            Func<Industry, Vector3> getIndustryMarkerPosition,
            Func<bool> isMenuOpen)
        {
            _industryManager = industryManager;
            _propertyManager = propertyManager;
            _fleetManager = fleetManager;
            _playerContractsManager = playerContractsManager;
            _playerSuccessTracker = playerSuccessTracker;
            _resolveGroundPosition = resolveGroundPosition;
            _getAirportTeleportPosition = getAirportTeleportPosition;
            _getIndustryMarkerPosition = getIndustryMarkerPosition;
            _isMenuOpen = isMenuOpen;
            _snapshot = new StartingGuidesPersistenceSnapshot();
            _lastShownTaskIndex = -1;
            _lastGpsIndustryId = string.Empty;
        }

        public bool IsActive
        {
            get { return _snapshot != null && _snapshot.Enabled && !_snapshot.IsCompleted; }
        }

        public void Reset()
        {
            _snapshot = new StartingGuidesPersistenceSnapshot();
            ResetTransientState();
        }

        public void BeginNewSave(bool enabled)
        {
            _snapshot = enabled
                ? new StartingGuidesPersistenceSnapshot
                {
                    Enabled = true,
                    IntroSequenceCompleted = false,
                    CurrentTaskIndex = 0,
                    IsCompleted = false,
                }
                : new StartingGuidesPersistenceSnapshot();
            ResetTransientState();
        }

        public void ApplyPersistenceSnapshot(StartingGuidesPersistenceSnapshot snapshot)
        {
            _snapshot = CloneSnapshot(snapshot) ?? new StartingGuidesPersistenceSnapshot();
            ResetTransientState();
        }

        public StartingGuidesPersistenceSnapshot CreatePersistenceSnapshot()
        {
            return CloneSnapshot(_snapshot);
        }

        public void Update(Ped player, int gameTime)
        {
            if (!IsActive || player == null || !player.Exists())
            {
                return;
            }

            if (!_snapshot.IntroSequenceCompleted)
            {
                UpdateIntroSequence(player, gameTime);
                if (!_snapshot.IntroSequenceCompleted)
                {
                    return;
                }
            }

            AdvanceFromRuntimeState(player);
            ShowCurrentInstruction(gameTime);
        }

        public void Draw()
        {
            if (!IsActive || !_snapshot.IntroSequenceCompleted || (_isMenuOpen != null && _isMenuOpen()))
            {
                return;
            }

            var resolution = Screen.Resolution;
            var panelX = 22f;
            var panelY = 54f;
            var panelWidth = 350f;
            var panelHeight = 58f + (Steps.Length * ChecklistRowSpacing);
            var deliveryProgress = GetDeliveryProgress();

            DrawRect(resolution.Width, resolution.Height, panelX + 6f, panelY + 6f, panelWidth, panelHeight, Color.FromArgb(52, 0, 0, 0));
            DrawRect(resolution.Width, resolution.Height, panelX, panelY, panelWidth, panelHeight, Color.FromArgb(196, 14, 21, 30));
            DrawRect(resolution.Width, resolution.Height, panelX, panelY, panelWidth, 4f, Color.FromArgb(232, 222, 183, 92));

            DrawTextLine(resolution, LocalizedText.Get(PanelTitleKey), panelX + 14f, panelY + 10f, 0.39f, Color.FromArgb(240, 255, 255, 255), GTA.UI.Font.ChaletLondon, Alignment.Left);
            DrawTextLine(
                resolution,
                string.Format("{0}/{1}", Math.Min(Steps.Length, Math.Max(0, _snapshot.CurrentTaskIndex) + 1), Steps.Length),
                panelX + panelWidth - 16f,
                panelY + 10f,
                0.34f,
                Color.FromArgb(220, 196, 208, 220),
                GTA.UI.Font.ChaletLondon,
                Alignment.Right);

            for (int i = 0; i < Steps.Length; i++)
            {
                var rowY = panelY + 34f + (i * ChecklistRowSpacing);
                var completed = i < _snapshot.CurrentTaskIndex;
                var current = i == _snapshot.CurrentTaskIndex;
                var textColor = completed
                    ? Color.FromArgb(230, 202, 234, 208)
                    : current
                        ? Color.FromArgb(242, 255, 244, 214)
                        : Color.FromArgb(216, 194, 204, 212);

                DrawTextLine(
                    resolution,
                    string.Format("{0}. {1}", i + 1, GetStepTitle(i, deliveryProgress)),
                    panelX + 16f,
                    rowY,
                    current ? 0.31f : 0.29f,
                    textColor,
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left);
            }
        }

        public void NotifyIndustryGpsSet(string industryId)
        {
            _lastGpsIndustryId = industryId ?? string.Empty;
        }

        public void NotifyBankVisited()
        {
            _bankVisited = true;
        }

        public void NotifyUnloadCompleted(Industry destinationIndustry, Vehicle cargoVehicle, string commodity, bool completedDelivery, bool completedContract)
        {
            if (!IsActive)
            {
                return;
            }

            if (_snapshot.CurrentTaskIndex == CoalUnloadStepIndex
                && completedDelivery
                && MatchesIndustry(destinationIndustry, ResolveCementIndustry())
                && MatchesKeyword(commodity, "coal"))
            {
                _coalUnloadCompleted = true;
            }

            if (_snapshot.CurrentTaskIndex == CompleteQuickJobStepIndex && completedContract)
            {
                _quickJobCompleted = true;
            }

            if (_snapshot.CurrentTaskIndex == CompleteMixerDeliveryStepIndex && completedDelivery && IsMixerVehicle(cargoVehicle))
            {
                _mixerDeliveryCompleted = true;
            }
        }

        private void ResetTransientState()
        {
            _introPhase = IntroPhase.None;
            _introPhaseStartedAtMs = 0;
            _lastShownTaskIndex = -1;
            _lastGpsIndustryId = string.Empty;
            _bankVisited = false;
            _coalUnloadCompleted = false;
            _quickJobCompleted = false;
            _mixerDeliveryCompleted = false;
        }

        private void UpdateIntroSequence(Ped player, int gameTime)
        {
            switch (_introPhase)
            {
                case IntroPhase.None:
                    Function.Call(Hash.DO_SCREEN_FADE_OUT, IntroFadeDurationMs);
                    _introPhase = IntroPhase.FadeOut;
                    _introPhaseStartedAtMs = gameTime;
                    return;
                case IntroPhase.FadeOut:
                    if (!Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT)
                        && gameTime - _introPhaseStartedAtMs < IntroFadeDurationMs + 300)
                    {
                        return;
                    }

                    TeleportPlayerToAirport(player);
                    Function.Call(Hash.DO_SCREEN_FADE_IN, IntroFadeDurationMs);
                    _introPhase = IntroPhase.FadeIn;
                    _introPhaseStartedAtMs = gameTime;
                    return;
                case IntroPhase.FadeIn:
                    if (!Function.Call<bool>(Hash.IS_SCREEN_FADED_IN)
                        && gameTime - _introPhaseStartedAtMs < IntroFadeDurationMs + 300)
                    {
                        return;
                    }

                    _snapshot.IntroSequenceCompleted = true;
                    _introPhase = IntroPhase.Complete;
                    _lastShownTaskIndex = -1;
                    return;
                default:
                    _snapshot.IntroSequenceCompleted = true;
                    return;
            }
        }

        private void TeleportPlayerToAirport(Ped player)
        {
            var destination = StartingGuideSpawnPosition;

            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, destination.X, destination.Y, destination.Z);

            if (player == null || !player.Exists())
            {
                return;
            }

            if (player.IsInVehicle() && player.CurrentVehicle != null && player.CurrentVehicle.Exists())
            {
                player.CurrentVehicle.Position = destination;
                player.CurrentVehicle.Heading = StartingGuideSpawnHeading;
            }
            else
            {
                player.Position = destination;
            }

            player.Heading = StartingGuideSpawnHeading;

            var spawnedVehicle = SpawnStartingGuideVehicle(destination);
            if (spawnedVehicle != null && spawnedVehicle.Exists())
            {
                Function.Call(Hash.SET_PED_INTO_VEHICLE, player.Handle, spawnedVehicle.Handle, -1);
            }
        }

        private static Vehicle SpawnStartingGuideVehicle(Vector3 destination)
        {
            var model = new Model("emperor");
            if (!model.Request(500))
            {
                model.MarkAsNoLongerNeeded();
                return null;
            }

            var vehicle = World.CreateVehicle(model, destination, StartingGuideSpawnHeading);
            model.MarkAsNoLongerNeeded();
            if (vehicle == null || !vehicle.Exists())
            {
                return null;
            }

            vehicle.IsPersistent = false;
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, true, true, false);
            return vehicle;
        }

        private void AdvanceFromRuntimeState(Ped player)
        {
            switch (_snapshot.CurrentTaskIndex)
            {
                case 0:
                    if (HasOperationalOfficeAccess())
                    {
                        AdvanceStep();
                    }
                    break;
                case 1:
                    if (HasCommercialVehicle(TipperKeywords))
                    {
                        AdvanceStep();
                    }
                    break;
                case 2:
                    if (HasDeployedOfficeVehicle(TipperKeywords))
                    {
                        AdvanceStep();
                    }
                    break;
                case 3:
                    if (MatchesIndustryId(_lastGpsIndustryId, ResolveQuarryIndustry()))
                    {
                        AdvanceStep();
                    }
                    break;
                case 4:
                    if (IsPlayerNearIndustry(player, ResolveQuarryIndustry()))
                    {
                        AdvanceStep();
                    }
                    break;
                case 5:
                    if (HasCoalLoadedFromQuarry(player))
                    {
                        AdvanceStep();
                    }
                    break;
                case 6:
                    if (MatchesIndustryId(_lastGpsIndustryId, ResolveCementIndustry()))
                    {
                        AdvanceStep();
                    }
                    break;
                case 7:
                    if (IsPlayerNearIndustry(player, ResolveCementIndustry()))
                    {
                        AdvanceStep();
                    }
                    break;
                case 8:
                    if (_coalUnloadCompleted)
                    {
                        AdvanceStep();
                    }
                    break;
                case ReturnAfterFirstDeliveryStepIndex:
                    if (IsPlayerAtActiveOffice(player))
                    {
                        AdvanceStep();
                    }
                    break;
                case StoreTruckStepIndex:
                    if (HasStoredOfficeVehicle(TipperKeywords) && !HasDeployedOfficeVehicle(TipperKeywords))
                    {
                        AdvanceStep();
                    }
                    break;
                case TakeQuickJobStepIndex:
                    if (HasAcceptedQuickJob())
                    {
                        AdvanceStep();
                    }
                    break;
                case CompleteQuickJobStepIndex:
                    if (_quickJobCompleted)
                    {
                        AdvanceStep();
                    }
                    break;
                case ReturnAfterQuickJobStepIndex:
                    if (IsPlayerAtActiveOffice(player))
                    {
                        AdvanceStep();
                    }
                    break;
                case DeliveriesStepIndex:
                    if (GetDeliveryProgress() >= DeliveryTargetCount)
                    {
                        AdvanceStep();
                    }
                    break;
                case 15:
                    if (HasAnyContractorPermit())
                    {
                        AdvanceStep();
                    }
                    break;
                case VisitBankStepIndex:
                    if (_bankVisited)
                    {
                        AdvanceStep();
                    }
                    break;
                case 17:
                    if (HasCommercialVehicle(MixerKeywords))
                    {
                        AdvanceStep();
                    }
                    break;
                case CompleteMixerDeliveryStepIndex:
                    if (_mixerDeliveryCompleted)
                    {
                        AdvanceStep();
                    }
                    break;
            }
        }

        private void AdvanceStep()
        {
            _snapshot.CurrentTaskIndex = Math.Max(0, _snapshot.CurrentTaskIndex) + 1;
            _lastShownTaskIndex = -1;

            if (_snapshot.CurrentTaskIndex >= Steps.Length)
            {
                _snapshot.CurrentTaskIndex = Steps.Length - 1;
                _snapshot.IsCompleted = true;
                Screen.ShowSubtitle(LocalizedText.Get(PanelCompletedKey), 8000);
            }
        }

        private void ShowCurrentInstruction(int gameTime)
        {
            if (!IsActive)
            {
                return;
            }

            var currentIndex = Math.Max(0, Math.Min(Steps.Length - 1, _snapshot.CurrentTaskIndex));
            if (_lastShownTaskIndex == currentIndex)
            {
                return;
            }

            _lastShownTaskIndex = currentIndex;
            Screen.ShowSubtitle(LocalizedText.Get(Steps[currentIndex].InstructionKey), InstructionDurationMs);
        }

        private bool HasOperationalOfficeAccess()
        {
            if (_propertyManager == null)
            {
                return false;
            }

            string reason;
            return _propertyManager.CanUseCommercialSystems(out reason);
        }

        private bool HasCommercialVehicle(string[] keywords)
        {
            return (_propertyManager != null ? _propertyManager.CommercialVehicles : null)
                ?.Any(entry => EntryMatchesKeywords(entry, keywords)) == true;
        }

        private bool HasDeployedOfficeVehicle(string[] keywords)
        {
            var activeOfficeId = _propertyManager != null ? _propertyManager.ActiveOfficeId : string.Empty;
            return (_propertyManager != null ? _propertyManager.CommercialVehicles : null)
                ?.Any(entry => entry != null
                    && entry.IsDeployed
                    && string.Equals(entry.AssignedOfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase)
                    && EntryMatchesKeywords(entry, keywords)) == true;
        }

        private bool HasStoredOfficeVehicle(string[] keywords)
        {
            var activeOfficeId = _propertyManager != null ? _propertyManager.ActiveOfficeId : string.Empty;
            return (_propertyManager != null ? _propertyManager.CommercialVehicles : null)
                ?.Any(entry => entry != null
                    && !entry.IsDeployed
                    && string.Equals(entry.AssignedOfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase)
                    && EntryMatchesKeywords(entry, keywords)) == true;
        }

        private bool HasCoalLoadedFromQuarry(Ped player)
        {
            var quarry = ResolveQuarryIndustry();
            if (quarry == null || _fleetManager == null || player == null || !player.Exists())
            {
                return false;
            }

            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                return false;
            }

            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            return cargoState != null
                && !cargoState.IsEmpty
                && MatchesKeyword(cargoState.Commodity, "coal")
                && string.Equals(cargoState.SourceIndustryId, quarry.Id, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasAcceptedQuickJob()
        {
            return _playerContractsManager != null
                && _playerContractsManager.GetAcceptedContracts().Any(contract => contract != null && contract.Type == PlayerContractType.QuickJob);
        }

        private int GetTotalSuccessfulDeliveries()
        {
            var snapshot = _playerSuccessTracker != null ? _playerSuccessTracker.CreatePersistenceSnapshot() : null;
            return snapshot != null ? Math.Max(0, snapshot.TotalSuccessfulDeliveries) : 0;
        }

        private int GetDeliveryProgress()
        {
            return Math.Min(DeliveryTargetCount, GetTotalSuccessfulDeliveries());
        }

        private bool HasAnyContractorPermit()
        {
            return _industryManager != null
                && _industryManager.Industries.Any(industry => industry != null && industry.HasContractorPermit);
        }

        private bool IsPlayerNearIndustry(Ped player, Industry industry)
        {
            if (player == null || !player.Exists() || industry == null)
            {
                return false;
            }

            var markerPosition = _getIndustryMarkerPosition != null ? _getIndustryMarkerPosition(industry) : industry.Position;
            return player.Position.DistanceTo(markerPosition) <= IndustryArrivalDistance;
        }

        private bool IsPlayerAtActiveOffice(Ped player)
        {
            var office = _propertyManager != null ? _propertyManager.ActiveOffice : null;
            return player != null
                && player.Exists()
                && office != null
                && player.Position.DistanceTo(office.MarkerPosition) <= OfficeArrivalDistance;
        }

        private Industry ResolveQuarryIndustry()
        {
            return ResolveIndustryByKeyword("quarry");
        }

        private Industry ResolveCementIndustry()
        {
            return ResolveIndustryByKeyword("cement");
        }

        private Industry ResolveIndustryByKeyword(string keyword)
        {
            if (_industryManager == null || string.IsNullOrWhiteSpace(keyword))
            {
                return null;
            }

            return _industryManager.Industries.FirstOrDefault(industry => industry != null && (
                MatchesKeyword(industry.Id, keyword)
                || MatchesKeyword(industry.Name, keyword)
                || MatchesKeyword(industry.DistrictName, keyword)));
        }

        private bool MatchesIndustry(Industry left, Industry right)
        {
            return left != null
                && right != null
                && string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesIndustryId(string industryId, Industry industry)
        {
            return industry != null
                && !string.IsNullOrWhiteSpace(industryId)
                && string.Equals(industryId, industry.Id, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMixerVehicle(Vehicle cargoVehicle)
        {
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                return false;
            }

            OwnedCommercialVehiclePersistenceEntry entry;
            if (_propertyManager != null && _propertyManager.TryResolveCommercialVehicleRecord(cargoVehicle, out entry) && entry != null)
            {
                return EntryMatchesKeywords(entry, MixerKeywords);
            }

            return MatchesAnyKeyword(cargoVehicle.DisplayName, MixerKeywords);
        }

        private string GetStepTitle(int stepIndex, int deliveryProgress)
        {
            if (stepIndex < 0 || stepIndex >= Steps.Length)
            {
                return string.Empty;
            }

            if (stepIndex == DeliveriesStepIndex)
            {
                return LocalizedText.Format(DeliveriesProgressTitleKey, DeliveryTargetCount, deliveryProgress);
            }

            return LocalizedText.Get(Steps[stepIndex].TitleKey);
        }

        private static bool EntryMatchesKeywords(OwnedCommercialVehiclePersistenceEntry entry, string[] keywords)
        {
            return entry != null && (
                MatchesAnyKeyword(entry.DisplayName, keywords)
                || MatchesAnyKeyword(entry.PoweredModelName, keywords)
                || MatchesAnyKeyword(entry.CargoModelName, keywords));
        }

        private static bool MatchesAnyKeyword(string value, string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(value) || keywords == null)
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(keywords[i]) && normalized.Contains(keywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesKeyword(string value, string keyword)
        {
            return MatchesAnyKeyword(value, new[] { keyword });
        }

        private static StartingGuidesPersistenceSnapshot CloneSnapshot(StartingGuidesPersistenceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new StartingGuidesPersistenceSnapshot
            {
                Enabled = snapshot.Enabled,
                IntroSequenceCompleted = snapshot.IntroSequenceCompleted,
                CurrentTaskIndex = Math.Max(0, snapshot.CurrentTaskIndex),
                IsCompleted = snapshot.IsCompleted,
            };
        }

        private static PointF ToScriptTextCoords(Size resolution, float x, float y)
        {
            const float scriptWidth = 1280f;
            const float scriptHeight = 720f;
            return new PointF(
                x * (scriptWidth / resolution.Width),
                y * (scriptHeight / resolution.Height));
        }

        private static void DrawTextLine(Size resolution, string text, float x, float y, float scale, Color color, GTA.UI.Font font, Alignment alignment)
        {
            var coords = ToScriptTextCoords(resolution, x, y);
            var normalizedX = coords.X / 1280f;
            var normalizedY = coords.Y / 720f;
            var wrapEnd = alignment == Alignment.Right ? normalizedX : 1f;

            Function.Call(Hash.SET_TEXT_FONT, (int)font);
            Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_CENTRE, alignment == Alignment.Center);
            Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, alignment == Alignment.Right);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, wrapEnd);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 0);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text ?? string.Empty);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, normalizedX, normalizedY, 0);
        }

        private static void DrawRect(float screenWidth, float screenHeight, float x, float y, float width, float height, Color color)
        {
            var centerX = (x + (width * 0.5f)) / screenWidth;
            var centerY = (y + (height * 0.5f)) / screenHeight;
            var normalizedWidth = width / screenWidth;
            var normalizedHeight = height / screenHeight;
            Function.Call(Hash.DRAW_RECT, centerX, centerY, normalizedWidth, normalizedHeight, color.R, color.G, color.B, color.A);
        }
    }
}