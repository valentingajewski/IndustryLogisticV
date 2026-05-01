using System;
using GTA;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Systems
{
    public sealed class CargoTransferController
    {
        private readonly FleetManager _fleetManager;
        private readonly IndustryManager _industryManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly Action<string> _showStatus;

        private PendingTransfer _pendingTransfer;

        public CargoTransferController(
            FleetManager fleetManager,
            IndustryManager industryManager,
            GlobalMarketManager globalMarket,
            Action<string> showStatus)
        {
            _fleetManager = fleetManager;
            _industryManager = industryManager;
            _globalMarket = globalMarket;
            _showStatus = showStatus;
        }

        public bool HasPendingTransfer
        {
            get { return _pendingTransfer != null; }
        }

        public void ClearState()
        {
            _pendingTransfer = null;
        }

        public void Update(int now, Action<string, float> drawProgressBar)
        {
            if (_pendingTransfer == null)
            {
                return;
            }

            var elapsed = now - _pendingTransfer.StartMs;
            var progress = Math.Min(1f, elapsed / (float)_pendingTransfer.DurationMs);

            try
            {
                _pendingTransfer.OnProgress?.Invoke(progress);
            }
            catch (Exception)
            {
                _pendingTransfer.OnProgress = null;
                _showStatus("Transfer visual callback failed. Continuing without preview.");
            }

            drawProgressBar(_pendingTransfer.Label, progress);

            if (elapsed < _pendingTransfer.DurationMs)
            {
                return;
            }

            var completed = _pendingTransfer;
            _pendingTransfer = null;

            try
            {
                completed.OnComplete?.Invoke();
            }
            catch (Exception)
            {
                _showStatus("Transfer completion failed.");
            }
        }

        public void StartTabletLoadTransfer(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            VehicleCargoType cargoType,
            string selectedProduct,
            Action beforeStart)
        {
            if (string.IsNullOrWhiteSpace(selectedProduct))
            {
                _showStatus("No product selected for loading.");
                return;
            }

            var requestedCapacity = Math.Max(0.5f, cargoState.FreeCapacityTons);
            var targetLoadTons = ResolveLoadTargetTons(industry, selectedProduct, requestedCapacity);
            if (targetLoadTons <= 0.001f)
            {
                _showStatus("Loading failed: product unavailable.");
                return;
            }

            var shouldAnimateCrateDoors = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct) == VehicleCargoType.Crate;
            var usesLooseVisual = IsLooseVisualCommodity(selectedProduct);

            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            }

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            if (usesLooseVisual)
            {
                _fleetManager.ClearCargoVisuals(cargoState);
            }

            beforeStart();
            StartTransfer(
                BuildLoadingTransferLabel(0f, targetLoadTons, selectedProduct),
                2600,
                () =>
                {
                    try
                    {
                        float loaded;
                        if (!_industryManager.TryLoadCommodity(industry, cargoType, selectedProduct, targetLoadTons, out loaded))
                        {
                            if (usesLooseVisual)
                            {
                                _fleetManager.ClearCargoVisuals(cargoState);
                            }

                            _showStatus("Loading failed: product unavailable.");
                            return;
                        }

                        cargoState.Commodity = selectedProduct;
                        cargoState.WeightTons += loaded;
                        cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
                        cargoState.CargoCondition = 1f;
                        cargoState.TotalLostTons = 0f;
                        cargoState.LastTrackedRigHealth = 0f;
                        _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        _showStatus(string.Format("Loaded {0:0.0}t {1}.", loaded, selectedProduct));
                    }
                    finally
                    {
                        if (shouldAnimateCrateDoors)
                        {
                            SetRearCargoDoors(cargoVehicle, false);
                        }
                    }
                },
                progress =>
                {
                    if (_pendingTransfer == null)
                    {
                        return;
                    }

                    var currentTons = targetLoadTons * Clamp01(progress);
                    _pendingTransfer.Label = BuildLoadingTransferLabel(currentTons, targetLoadTons, selectedProduct);
                });
        }

        public void StartTabletUnloadTransfer(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            bool omegaOnly,
            Action beforeStart,
            Action<float> addProfit)
        {
            if (cargoState.IsEmpty)
            {
                _showStatus("Vehicle is empty.");
                return;
            }

            if (omegaOnly && !cargoState.Commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                _showStatus(string.Format("Vehicle cargo is {0}. Omega fluid required.", cargoState.Commodity));
                return;
            }

            if (!industry.AcceptsCommodity(cargoState.Commodity))
            {
                _showStatus(string.Format("This industry does not accept {0}.", cargoState.Commodity));
                return;
            }

            var tonsToUnload = cargoState.WeightTons;
            var commodity = cargoState.Commodity;
            var shouldAnimateCrateDoors = cargoState.CargoType == VehicleCargoType.Crate
                || CommodityCatalog.GetCargoTypeForCommodity(commodity) == VehicleCargoType.Crate;

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            beforeStart();
            StartTransfer(
                string.Format("Unloading {0:0.0}t {1}...", tonsToUnload, commodity),
                2800,
                () =>
                {
                    try
                    {
                        float accepted;
                        if (!_industryManager.TryUnload(industry, commodity, tonsToUnload, out accepted))
                        {
                            _showStatus("Unloading failed: destination storage full.");
                            return;
                        }

                        var baseRevenue = _industryManager.ComputeDeliveryProfit(industry, commodity, accepted, _globalMarket, Game.GameTime);
                        var conditionRatio = Clamp01(cargoState.CargoCondition);
                        var revenue = baseRevenue * conditionRatio;
                        addProfit(revenue);

                        cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                        if (cargoState.WeightTons <= 0.001f)
                        {
                            ClearCargoStateAndVisuals(cargoVehicle, cargoState);
                        }
                        else
                        {
                            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        }

                        _showStatus(string.Format(
                            "Unloaded {0:0.0}t {1}. Profit +${2:0} | Condition {3:0}%",
                            accepted,
                            commodity,
                            revenue,
                            conditionRatio * 100f));
                    }
                    finally
                    {
                        if (shouldAnimateCrateDoors)
                        {
                            SetRearCargoDoors(cargoVehicle, false);
                        }
                    }
                });
        }

        public void StartMenuLoadTransfer(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            string selectedProduct,
            Action beforeStart)
        {
            var requestedCapacity = Math.Max(0.5f, cargoState.FreeCapacityTons);
            var targetLoadTons = ResolveLoadTargetTons(industry, selectedProduct, requestedCapacity);
            if (targetLoadTons <= 0.001f)
            {
                _showStatus("Loading failed: product unavailable.");
                return;
            }

            var cargoType = cargoState.CargoType;
            var shouldAnimateCrateDoors = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct) == VehicleCargoType.Crate;
            var usesLooseVisual = IsLooseVisualCommodity(selectedProduct);
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            }

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            if (usesLooseVisual)
            {
                _fleetManager.ClearCargoVisuals(cargoState);
            }

            beforeStart();
            StartTransfer(
                BuildLoadingTransferLabel(0f, targetLoadTons, selectedProduct),
                2600,
                () =>
                {
                    try
                    {
                        float loaded;
                        if (!_industryManager.TryLoadCommodity(industry, cargoType, selectedProduct, targetLoadTons, out loaded))
                        {
                            if (usesLooseVisual)
                            {
                                _fleetManager.ClearCargoVisuals(cargoState);
                            }

                            _showStatus("Loading failed: product unavailable.");
                            return;
                        }

                        cargoState.Commodity = selectedProduct;
                        cargoState.WeightTons += loaded;
                        cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
                        _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        _showStatus(string.Format("Loaded {0:0.0}t {1}.", loaded, selectedProduct));
                    }
                    finally
                    {
                        if (shouldAnimateCrateDoors)
                        {
                            SetRearCargoDoors(cargoVehicle, false);
                        }
                    }
                },
                progress =>
                {
                    if (_pendingTransfer == null)
                    {
                        return;
                    }

                    var currentTons = targetLoadTons * Clamp01(progress);
                    _pendingTransfer.Label = BuildLoadingTransferLabel(currentTons, targetLoadTons, selectedProduct);
                });
        }

        public void StartMenuUnloadTransfer(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            Action beforeStart,
            Action<float> addProfit)
        {
            var tonsToUnload = cargoState.WeightTons;
            var commodity = cargoState.Commodity;
            var shouldAnimateCrateDoors = cargoState.CargoType == VehicleCargoType.Crate;

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            beforeStart();
            StartTransfer(
                string.Format("Unloading {0:0.0}t {1}...", tonsToUnload, commodity),
                2800,
                () =>
                {
                    try
                    {
                        float accepted;
                        if (!_industryManager.TryUnload(industry, commodity, tonsToUnload, out accepted))
                        {
                            _showStatus("Unloading failed: destination storage full.");
                            return;
                        }

                        var revenue = _industryManager.ComputeDeliveryProfit(industry, commodity, accepted, _globalMarket, Game.GameTime);
                        addProfit(revenue);

                        cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                        if (cargoState.WeightTons <= 0.001f)
                        {
                            ClearCargoStateAndVisuals(cargoVehicle, cargoState);
                        }
                        else
                        {
                            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        }

                        _showStatus(string.Format("Unloaded {0:0.0}t {1}. Profit +${2:0}", accepted, commodity, revenue));
                    }
                    finally
                    {
                        if (shouldAnimateCrateDoors)
                        {
                            SetRearCargoDoors(cargoVehicle, false);
                        }
                    }
                });
        }

        public void ClearCargoStateAndVisuals(Vehicle cargoVehicle, VehicleCargoState cargoState)
        {
            if (cargoState != null)
            {
                cargoState.ClearCargo();
                _fleetManager.ClearCargoVisuals(cargoState);
            }

            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                return;
            }

            var stateForVehicle = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (stateForVehicle == null || object.ReferenceEquals(stateForVehicle, cargoState))
            {
                return;
            }

            stateForVehicle.ClearCargo();
            _fleetManager.ClearCargoVisuals(stateForVehicle);
        }

        public static bool IndustryHasMultipleInputs(Industry industry)
        {
            return industry != null
                && industry.SupportsOmegaBoost
                && industry.Inputs != null
                && industry.Inputs.Count > 1;
        }

        public static bool IsOmegaOnlyUnloadIndustry(Industry industry)
        {
            return industry != null
                && industry.SupportsOmegaBoost
                && industry.Inputs != null
                && industry.Inputs.Count == 1
                && industry.Inputs.Contains("Omega");
        }

        private void StartTransfer(string label, int durationMs, Action complete, Action<float> onProgress = null)
        {
            _pendingTransfer = new PendingTransfer
            {
                Label = label,
                DurationMs = durationMs,
                StartMs = Game.GameTime,
                OnComplete = complete,
                OnProgress = onProgress,
            };
        }

        private static bool IsLooseVisualCommodity(string commodity)
        {
            var normalized = CommodityCatalog.Normalize(commodity);
            return normalized.Equals("Ore", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Coal", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Recyclable", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Recyclables", StringComparison.OrdinalIgnoreCase);
        }

        private static float ResolveLoadTargetTons(Industry industry, string commodity, float requestedTons)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity) || requestedTons <= 0f)
            {
                return 0f;
            }

            var available = Math.Max(0f, industry.GetStock(commodity));
            return Math.Min(requestedTons, available);
        }

        private static string BuildLoadingTransferLabel(float currentTons, float targetTons, string commodity)
        {
            return string.Format(
                "Loading {0:0.0}/{1:0.0}t {2}...",
                Math.Max(0f, currentTons),
                Math.Max(0f, targetTons),
                commodity ?? string.Empty);
        }

        private static void SetRearCargoDoors(Vehicle vehicle, bool open)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            ToggleDoor(vehicle, VehicleDoorIndex.BackLeftDoor, open);
            ToggleDoor(vehicle, VehicleDoorIndex.BackRightDoor, open);
            ToggleDoor(vehicle, VehicleDoorIndex.Trunk, open);
        }

        private static void ToggleDoor(Vehicle vehicle, VehicleDoorIndex doorIndex, bool open)
        {
            if (!vehicle.Doors.Contains(doorIndex))
            {
                return;
            }

            var door = vehicle.Doors[doorIndex];
            if (open)
            {
                door.Open(false, false);
            }
            else
            {
                door.Close(false);
            }
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }

        private sealed class PendingTransfer
        {
            public string Label { get; set; }
            public int StartMs { get; set; }
            public int DurationMs { get; set; }
            public Action OnComplete { get; set; }
            public Action<float> OnProgress { get; set; }
        }
    }
}