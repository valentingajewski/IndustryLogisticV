using System;
using System.Linq;
using GTA;
using LSOL;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class CargoTransferProfitContext
    {
        public CompanyFinanceCategory Category { get; set; }

        public string Description { get; set; }

        public int RouteContractId { get; set; }

        public string RouteLabel { get; set; }

        public string PlayerContractId { get; set; }

        public string ShipperKey { get; set; }

        public string DistrictName { get; set; }
    }

    public sealed class CargoTransferController
    {
        private readonly FleetManager _fleetManager;
        private readonly IndustryManager _industryManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly PlayerContractsManager _playerContractsManager;
        private readonly TerritoryManager _territoryManager;
        private readonly Action<Industry> _notifyIndustryOutputChanged;
        private readonly Action<string> _showStatus;

        private PendingTransfer _pendingTransfer;

        public CargoTransferController(
            FleetManager fleetManager,
            IndustryManager industryManager,
            GlobalMarketManager globalMarket,
            Action<string> showStatus,
            PlayerContractsManager playerContractsManager = null,
            TerritoryManager territoryManager = null,
            Action<Industry> notifyIndustryOutputChanged = null)
        {
            _fleetManager = fleetManager;
            _industryManager = industryManager;
            _globalMarket = globalMarket;
            _showStatus = showStatus;
            _playerContractsManager = playerContractsManager;
            _territoryManager = territoryManager;
            _notifyIndustryOutputChanged = notifyIndustryOutputChanged;
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
            catch (Exception ex)
            {
                _pendingTransfer.OnProgress = null;
                _showStatus(ModDiagnostics.FormatFailure("Transfer visual callback", ex));
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
            catch (Exception ex)
            {
                _showStatus(ModDiagnostics.FormatFailure("Transfer completion", ex));
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

            PlayerContractTransferContext contractContext = null;
            string contractMessage = string.Empty;
            var contractResolution = _playerContractsManager != null
                ? _playerContractsManager.ResolveLoadContract(industry, cargoVehicle, cargoState, selectedProduct, out contractContext, out contractMessage)
                : PlayerContractTransferResolution.None;
            if (contractResolution == PlayerContractTransferResolution.Blocked)
            {
                _showStatus(contractMessage);
                return;
            }

            var isContractLoad = contractResolution == PlayerContractTransferResolution.Allowed;
            if (!isContractLoad && !EnsureIndustryTransportPermit(industry))
            {
                return;
            }

            if (!CanTopUpCargoState(cargoState, selectedProduct, out var loadValidationMessage))
            {
                _showStatus(loadValidationMessage);
                return;
            }

            var requestedCapacity = Math.Max(0f, cargoState.FreeCapacityTons);
            var targetLoadTons = isContractLoad && _playerContractsManager != null
                ? _playerContractsManager.GetContractLoadCeiling(contractContext, cargoState)
                : ResolveLoadTargetTons(industry, selectedProduct, requestedCapacity);
            if (targetLoadTons <= 0.001f)
            {
                _showStatus(isContractLoad ? "Loading failed: contract cargo is no longer available." : "Loading failed: product unavailable.");
                return;
            }

            if (!_fleetManager.CanVehicleCarryCommodity(cargoVehicle, selectedProduct))
            {
                _showStatus(string.Format("This vehicle cannot carry {0}.", selectedProduct));
                return;
            }

            var productCargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            var shouldAnimateCrateDoors = CommodityCatalog.UsesDoorAnimation(productCargoType);
            var usesLooseVisual = CommodityCatalog.UsesLooseVisual(productCargoType);

            cargoType = CommodityCatalog.ResolveCargoType(cargoType, selectedProduct);

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

                            _showStatus(isContractLoad ? "Loading failed: contract cargo is no longer available." : "Loading failed: product unavailable.");
                            return;
                        }

                        var previousWeight = cargoState.WeightTons;
                        var previousCondition = Math.Max(0f, Math.Min(1f, cargoState.CargoCondition));
                        var previousLostTons = Math.Max(0f, cargoState.TotalLostTons);
                        cargoState.Commodity = selectedProduct;
                        cargoState.WeightTons += loaded;
                        cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
                        cargoState.CargoCondition = previousWeight <= 0.001f
                            ? 1f
                            : ((previousWeight * previousCondition) + loaded) / Math.Max(0.001f, cargoState.WeightTons);
                        cargoState.TotalLostTons = previousWeight <= 0.001f ? 0f : previousLostTons;
                        cargoState.LastTrackedRigHealth = 0f;
                        cargoState.SourceIndustryId = industry != null ? industry.Id : string.Empty;
                        cargoState.SourceDistrictName = industry != null ? industry.DistrictName : string.Empty;
                        _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        if (isContractLoad && _playerContractsManager != null)
                        {
                            _playerContractsManager.CommitContractLoad(contractContext, cargoVehicle, cargoState, loaded);
                        }
                        else if (_territoryManager != null)
                        {
                            _territoryManager.RegisterLoad(industry, selectedProduct, loaded, false);
                        }

                        NotifyIndustryOutputChanged(industry);

                        _showStatus(isContractLoad
                            ? string.Format("Loaded {0} {1} for the active contract.", ModFormatting.FormatTons(loaded), selectedProduct)
                            : string.Format("Loaded {0} {1}.", ModFormatting.FormatTons(loaded), selectedProduct));
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

                    var currentTons = targetLoadTons * ModMath.Clamp01(progress);
                    _pendingTransfer.Label = BuildLoadingTransferLabel(currentTons, targetLoadTons, selectedProduct);
                });
        }

        public void StartTabletUnloadTransfer(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            bool omegaOnly,
            Action beforeStart,
            Action<float, CargoTransferProfitContext> addProfit,
            Action<Industry, string, float, string, string, bool, bool> recordDeliveryProgress = null,
            Action<Industry, Vehicle, string, bool, bool> onUnloadCompleted = null)
        {
            if (cargoState.IsEmpty)
            {
                _showStatus("Vehicle is empty.");
                return;
            }

            PlayerContractTransferContext contractContext = null;
            string contractMessage = string.Empty;
            var contractResolution = _playerContractsManager != null
                ? _playerContractsManager.ResolveUnloadContract(industry, cargoVehicle, cargoState, out contractContext, out contractMessage)
                : PlayerContractTransferResolution.None;
            if (contractResolution == PlayerContractTransferResolution.Blocked)
            {
                _showStatus(contractMessage);
                return;
            }

            var isContractUnload = contractResolution == PlayerContractTransferResolution.Allowed;
            if (!isContractUnload && !EnsureIndustryTransportPermit(industry))
            {
                return;
            }

            if (omegaOnly && !cargoState.Commodity.Equals("Omega", StringComparison.OrdinalIgnoreCase))
            {
                _showStatus(string.Format("Vehicle cargo is {0}. Omega cargo required.", cargoState.Commodity));
                return;
            }

            if (!industry.AcceptsCommodity(cargoState.Commodity))
            {
                _showStatus(string.Format("This industry does not accept {0}.", cargoState.Commodity));
                return;
            }

            var commodity = cargoState.Commodity;
            var tonsToUnload = Math.Max(0f, cargoState.WeightTons);
            if (isContractUnload && contractContext != null && contractContext.Contract != null)
            {
                tonsToUnload = Math.Min(tonsToUnload, Math.Max(0f, contractContext.Contract.LoadedTons));
            }

            var targetUnloadTons = ResolveUnloadTargetTons(industry, commodity, tonsToUnload);
            if (targetUnloadTons <= 0.001f)
            {
                _showStatus("Unloading failed: destination storage full.");
                return;
            }

            var shouldAnimateCrateDoors = CommodityCatalog.UsesDoorAnimation(cargoState.CargoType)
                || CommodityCatalog.UsesDoorAnimation(commodity);

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            beforeStart();
            StartTransfer(
                BuildUnloadingTransferLabel(0f, targetUnloadTons, commodity),
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
                        var sourceIndustryId = cargoState.SourceIndustryId;
                        var sourceDistrictName = cargoState.SourceDistrictName;
                        var conditionRatio = ModMath.Clamp01(cargoState.CargoCondition);
                        if (isContractUnload && _playerContractsManager != null)
                        {
                            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                            var contractResult = _playerContractsManager.CommitContractUnload(contractContext, cargoVehicle, cargoState, accepted, Game.GameTime);
                            addProfit(contractResult.Payout, new CargoTransferProfitContext
                            {
                                Category = CompanyFinanceCategory.PlayerContract,
                                Description = string.Format(
                                    "Contract delivery of {0} to {1}",
                                    cargoState.Commodity ?? "cargo",
                                    industry != null ? industry.Name : "destination"),
                                RouteContractId = 0,
                                RouteLabel = contractResult.RouteLabel,
                                PlayerContractId = contractResult.PlayerContractId,
                                ShipperKey = contractResult.ShipperKey,
                                DistrictName = contractResult.DistrictName,
                            });

                            if (contractResult.ContractCompleted)
                            {
                                ClearCargoStateAndVisuals(cargoVehicle, cargoState);
                            }
                            else
                            {
                                _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                            }

                            _showStatus(contractResult.Message);
                            onUnloadCompleted?.Invoke(industry, cargoVehicle, commodity, contractResult.ContractCompleted, contractResult.ContractCompleted);
                        }
                        else
                        {
                            var baseRevenue = _industryManager.ComputeDeliveryProfit(industry, commodity, accepted, _globalMarket, Game.GameTime);
                            var revenue = baseRevenue * conditionRatio;
                            if (_territoryManager != null)
                            {
                                revenue = _territoryManager.AdjustDeliveryRevenue(industry, commodity, accepted, revenue);
                                _territoryManager.RegisterDelivery(industry, commodity, accepted, false, sourceIndustryId, sourceDistrictName);
                            }

                            addProfit(revenue, new CargoTransferProfitContext
                            {
                                Category = CompanyFinanceCategory.PlayerDelivery,
                                Description = industry != null && industry.IsWarehouse
                                    ? string.Format("Stored {0} at {1}", commodity, industry.Name)
                                    : string.Format("Player delivery of {0} to {1}", commodity, industry != null ? industry.Name : "destination"),
                                RouteContractId = 0,
                                RouteLabel = string.Empty,
                            });

                            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                            var completedDelivery = cargoState.WeightTons <= 0.001f;
                            recordDeliveryProgress?.Invoke(
                                industry,
                                commodity,
                                accepted,
                                sourceIndustryId,
                                sourceDistrictName,
                                completedDelivery,
                                completedDelivery && conditionRatio >= 0.999f);

                            if (completedDelivery)
                            {
                                ClearCargoStateAndVisuals(cargoVehicle, cargoState);
                            }
                            else
                            {
                                _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                            }

                            if (industry != null && industry.IsWarehouse)
                            {
                                _showStatus(string.Format(
                                    "Stored {0} {1} | Condition {2}",
                                    ModFormatting.FormatTons(accepted),
                                    commodity,
                                    ModFormatting.FormatPercent(conditionRatio * 100f)));
                            }
                            else
                            {
                                _showStatus(string.Format(
                                    "Unloaded {0} {1}. Profit {2} | Condition {3}",
                                    ModFormatting.FormatTons(accepted),
                                    commodity,
                                    ModFormatting.FormatSignedMoney(revenue),
                                    ModFormatting.FormatPercent(conditionRatio * 100f)));
                            }

                            onUnloadCompleted?.Invoke(industry, cargoVehicle, commodity, completedDelivery, false);
                        }
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

                    var currentTons = targetUnloadTons * ModMath.Clamp01(progress);
                    _pendingTransfer.Label = BuildUnloadingTransferLabel(currentTons, targetUnloadTons, commodity);
                });
        }

        public void StartMenuLoadTransfer(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            string selectedProduct,
            Action beforeStart)
        {
            PlayerContractTransferContext contractContext = null;
            string contractMessage = string.Empty;
            var contractResolution = _playerContractsManager != null
                ? _playerContractsManager.ResolveLoadContract(industry, cargoVehicle, cargoState, selectedProduct, out contractContext, out contractMessage)
                : PlayerContractTransferResolution.None;
            if (contractResolution == PlayerContractTransferResolution.Blocked)
            {
                _showStatus(contractMessage);
                return;
            }

            var isContractLoad = contractResolution == PlayerContractTransferResolution.Allowed;
            if (!isContractLoad && !EnsureIndustryTransportPermit(industry))
            {
                return;
            }

            if (!CanTopUpCargoState(cargoState, selectedProduct, out var loadValidationMessage))
            {
                _showStatus(loadValidationMessage);
                return;
            }

            var requestedCapacity = Math.Max(0f, cargoState.FreeCapacityTons);
            var targetLoadTons = isContractLoad && _playerContractsManager != null
                ? _playerContractsManager.GetContractLoadCeiling(contractContext, cargoState)
                : ResolveLoadTargetTons(industry, selectedProduct, requestedCapacity);
            if (targetLoadTons <= 0.001f)
            {
                _showStatus(isContractLoad ? "Loading failed: contract cargo is no longer available." : "Loading failed: product unavailable.");
                return;
            }

            if (!_fleetManager.CanVehicleCarryCommodity(cargoVehicle, selectedProduct))
            {
                _showStatus(string.Format("This vehicle cannot carry {0}.", selectedProduct));
                return;
            }

            var cargoType = cargoState.CargoType;
            var productCargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
            var shouldAnimateCrateDoors = CommodityCatalog.UsesDoorAnimation(productCargoType);
            var usesLooseVisual = CommodityCatalog.UsesLooseVisual(productCargoType);
            cargoType = CommodityCatalog.ResolveCargoType(cargoType, selectedProduct);

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

                            _showStatus(isContractLoad ? "Loading failed: contract cargo is no longer available." : "Loading failed: product unavailable.");
                            return;
                        }

                        var previousWeight = cargoState.WeightTons;
                        var previousCondition = Math.Max(0f, Math.Min(1f, cargoState.CargoCondition));
                        var previousLostTons = Math.Max(0f, cargoState.TotalLostTons);
                        cargoState.Commodity = selectedProduct;
                        cargoState.WeightTons += loaded;
                        cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(selectedProduct);
                        cargoState.CargoCondition = previousWeight <= 0.001f
                            ? 1f
                            : ((previousWeight * previousCondition) + loaded) / Math.Max(0.001f, cargoState.WeightTons);
                        cargoState.TotalLostTons = previousWeight <= 0.001f ? 0f : previousLostTons;
                        cargoState.LastTrackedRigHealth = 0f;
                        cargoState.SourceIndustryId = industry != null ? industry.Id : string.Empty;
                        cargoState.SourceDistrictName = industry != null ? industry.DistrictName : string.Empty;
                        _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                        if (isContractLoad && _playerContractsManager != null)
                        {
                            _playerContractsManager.CommitContractLoad(contractContext, cargoVehicle, cargoState, loaded);
                        }
                        else if (_territoryManager != null)
                        {
                            _territoryManager.RegisterLoad(industry, selectedProduct, loaded, false);
                        }

                        NotifyIndustryOutputChanged(industry);

                        _showStatus(isContractLoad
                            ? string.Format("Loaded {0} {1} for the active contract.", ModFormatting.FormatTons(loaded), selectedProduct)
                            : string.Format("Loaded {0} {1}.", ModFormatting.FormatTons(loaded), selectedProduct));
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

                    var currentTons = targetLoadTons * ModMath.Clamp01(progress);
                    _pendingTransfer.Label = BuildLoadingTransferLabel(currentTons, targetLoadTons, selectedProduct);
                });
        }

        public void StartMenuUnloadTransfer(
            Industry industry,
            Vehicle cargoVehicle,
            VehicleCargoState cargoState,
            Action beforeStart,
            Action<float, CargoTransferProfitContext> addProfit,
            Action<Industry, string, float, string, string, bool, bool> recordDeliveryProgress = null,
            Action<Industry, Vehicle, string, bool, bool> onUnloadCompleted = null)
        {
            PlayerContractTransferContext contractContext = null;
            string contractMessage = string.Empty;
            var contractResolution = _playerContractsManager != null
                ? _playerContractsManager.ResolveUnloadContract(industry, cargoVehicle, cargoState, out contractContext, out contractMessage)
                : PlayerContractTransferResolution.None;
            if (contractResolution == PlayerContractTransferResolution.Blocked)
            {
                _showStatus(contractMessage);
                return;
            }

            var isContractUnload = contractResolution == PlayerContractTransferResolution.Allowed;
            if (!isContractUnload && !EnsureIndustryTransportPermit(industry))
            {
                return;
            }

            var commodity = cargoState.Commodity;
            var tonsToUnload = Math.Max(0f, cargoState.WeightTons);
            if (isContractUnload && contractContext != null && contractContext.Contract != null)
            {
                tonsToUnload = Math.Min(tonsToUnload, Math.Max(0f, contractContext.Contract.LoadedTons));
            }

            var targetUnloadTons = ResolveUnloadTargetTons(industry, commodity, tonsToUnload);
            if (targetUnloadTons <= 0.001f)
            {
                _showStatus("Unloading failed: destination storage full.");
                return;
            }

            var conditionRatio = ModMath.Clamp01(cargoState.CargoCondition);
            var shouldAnimateCrateDoors = CommodityCatalog.UsesDoorAnimation(cargoState.CargoType)
                || CommodityCatalog.UsesDoorAnimation(commodity);

            if (shouldAnimateCrateDoors)
            {
                SetRearCargoDoors(cargoVehicle, true);
            }

            beforeStart();
            StartTransfer(
                BuildUnloadingTransferLabel(0f, targetUnloadTons, commodity),
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
                        var sourceIndustryId = cargoState.SourceIndustryId;
                        var sourceDistrictName = cargoState.SourceDistrictName;
                        var conditionRatio = ModMath.Clamp01(cargoState.CargoCondition);
                        if (isContractUnload && _playerContractsManager != null)
                        {
                            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                            var contractResult = _playerContractsManager.CommitContractUnload(contractContext, cargoVehicle, cargoState, accepted, Game.GameTime);
                            addProfit(contractResult.Payout, new CargoTransferProfitContext
                            {
                                Category = CompanyFinanceCategory.PlayerContract,
                                Description = string.Format(
                                    "Contract delivery of {0} to {1}",
                                    cargoState.Commodity ?? "cargo",
                                    industry != null ? industry.Name : "destination"),
                                RouteContractId = 0,
                                RouteLabel = contractResult.RouteLabel,
                                PlayerContractId = contractResult.PlayerContractId,
                                ShipperKey = contractResult.ShipperKey,
                                DistrictName = contractResult.DistrictName,
                            });

                            if (contractResult.ContractCompleted)
                            {
                                ClearCargoStateAndVisuals(cargoVehicle, cargoState);
                            }
                            else
                            {
                                _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                            }

                            _showStatus(contractResult.Message);
                            onUnloadCompleted?.Invoke(industry, cargoVehicle, commodity, contractResult.ContractCompleted, contractResult.ContractCompleted);
                        }
                        else
                        {
                            var revenue = _industryManager.ComputeDeliveryProfit(industry, commodity, accepted, _globalMarket, Game.GameTime);
                            if (_territoryManager != null)
                            {
                                revenue = _territoryManager.AdjustDeliveryRevenue(industry, commodity, accepted, revenue);
                                _territoryManager.RegisterDelivery(industry, commodity, accepted, false, sourceIndustryId, sourceDistrictName);
                            }

                            addProfit(revenue, new CargoTransferProfitContext
                            {
                                Category = CompanyFinanceCategory.PlayerDelivery,
                                Description = string.Format("Player delivery of {0} to {1}", commodity, industry != null ? industry.Name : "destination"),
                                RouteContractId = 0,
                                RouteLabel = string.Empty,
                            });

                            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - accepted);
                            var completedDelivery = cargoState.WeightTons <= 0.001f;
                            recordDeliveryProgress?.Invoke(
                                industry,
                                commodity,
                                accepted,
                                sourceIndustryId,
                                sourceDistrictName,
                                completedDelivery,
                                completedDelivery && conditionRatio >= 0.999f);

                            if (completedDelivery)
                            {
                                ClearCargoStateAndVisuals(cargoVehicle, cargoState);
                            }
                            else
                            {
                                _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
                            }

                            if (industry != null && industry.IsWarehouse)
                            {
                                _showStatus(string.Format("Stored {0} {1}", ModFormatting.FormatTons(accepted), commodity));
                            }
                            else
                            {
                                _showStatus(string.Format("Unloaded {0} {1}. Profit {2}", ModFormatting.FormatTons(accepted), commodity, ModFormatting.FormatSignedMoney(revenue)));
                            }

                            onUnloadCompleted?.Invoke(industry, cargoVehicle, commodity, completedDelivery, false);
                        }
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

                    var currentTons = targetUnloadTons * ModMath.Clamp01(progress);
                    _pendingTransfer.Label = BuildUnloadingTransferLabel(currentTons, targetUnloadTons, commodity);
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
                && industry.SortedAcceptedInputs != null
                && industry.SortedAcceptedInputs.Count > 1;
        }

        public static bool IsOmegaOnlyUnloadIndustry(Industry industry)
        {
            return industry != null
                && industry.SupportsOmegaBoost
                && industry.SortedAcceptedInputs != null
                && industry.SortedAcceptedInputs.Count == 1
                && industry.SortedAcceptedInputs.Contains("Omega");
        }

        private bool EnsureIndustryTransportPermit(Industry industry)
        {
            if (!_industryManager.RequiresContractorPermit(industry))
            {
                return true;
            }

            _showStatus(string.Format(
                "Purchase the contractor permit for {0} before transporting cargo to or from it.",
                industry.Name));
            return false;
        }

        private void NotifyIndustryOutputChanged(Industry industry)
        {
            if (_notifyIndustryOutputChanged == null || industry == null)
            {
                return;
            }

            try
            {
                _notifyIndustryOutputChanged(industry);
            }
            catch (Exception ex)
            {
                _showStatus(ModDiagnostics.FormatFailure("Industry output prop refresh", ex));
            }
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

        public void StartTimedTransfer(string label, int durationMs, Action beforeStart, Action complete, Func<float, string> progressLabelFactory = null)
        {
            beforeStart?.Invoke();
            StartTransfer(
                label,
                durationMs,
                complete,
                progress =>
                {
                    if (progressLabelFactory == null || _pendingTransfer == null)
                    {
                        return;
                    }

                    _pendingTransfer.Label = progressLabelFactory(ModMath.Clamp01(progress));
                });
        }

        internal static float ResolveLoadTargetTons(Industry industry, string commodity, float requestedTons)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity) || requestedTons <= 0f)
            {
                return 0f;
            }

            var available = Math.Max(0f, industry.GetStock(commodity));
            return Math.Min(requestedTons, available);
        }

        internal static float ResolveUnloadTargetTons(Industry industry, string commodity, float requestedTons)
        {
            if (industry == null || string.IsNullOrWhiteSpace(commodity) || requestedTons <= 0f)
            {
                return 0f;
            }

            var acceptedCapacity = Math.Max(0f, industry.GetMaxTransferTonsForCommodity(commodity));
            return Math.Min(Math.Max(0f, requestedTons), acceptedCapacity);
        }

        private static bool CanTopUpCargoState(VehicleCargoState cargoState, string selectedProduct, out string message)
        {
            message = string.Empty;
            if (cargoState == null)
            {
                message = "No cargo hold is available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedProduct))
            {
                message = "No product selected for loading.";
                return false;
            }

            if (cargoState.IsEmpty)
            {
                return true;
            }

            if (cargoState.FreeCapacityTons <= 0.001f)
            {
                message = "Vehicle cargo is already full.";
                return false;
            }

            if (!string.Equals(CommodityCatalog.Normalize(cargoState.Commodity), CommodityCatalog.Normalize(selectedProduct), StringComparison.OrdinalIgnoreCase))
            {
                message = string.Format("Vehicle already carries {0}. Mixed cargo is not supported.", cargoState.Commodity);
                return false;
            }

            return true;
        }

        internal static string BuildLoadingTransferLabel(float currentTons, float targetTons, string commodity)
        {
            return string.Format(
                "Loading {0} {1}...",
                ModFormatting.FormatRatio(Math.Max(0f, currentTons), Math.Max(0f, targetTons), "t"),
                commodity ?? string.Empty);
        }

        internal static string BuildUnloadingTransferLabel(float currentTons, float targetTons, string commodity)
        {
            return string.Format(
                "Unloading {0} {1}...",
                ModFormatting.FormatRatio(Math.Max(0f, currentTons), Math.Max(0f, targetTons), "t"),
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