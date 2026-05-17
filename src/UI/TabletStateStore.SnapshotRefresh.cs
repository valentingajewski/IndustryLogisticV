using System;
using System.Linq;
using GTA;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.UI
{
    internal sealed partial class TabletStateStore
    {
        private sealed class SnapshotRefreshContext
        {
            public SnapshotRefreshContext(Vehicle poweredVehicle, Vehicle cargoVehicle, VehicleCargoState cargoState, bool hasVehicleContext, Industry nearestIndustry)
            {
                PoweredVehicle = poweredVehicle;
                CargoVehicle = cargoVehicle;
                CargoState = cargoState;
                HasVehicleContext = hasVehicleContext;
                NearestIndustry = nearestIndustry;
            }

            public Vehicle PoweredVehicle { get; }

            public Vehicle CargoVehicle { get; }

            public VehicleCargoState CargoState { get; }

            public bool HasVehicleContext { get; }

            public Industry NearestIndustry { get; }
        }

        private void RefreshAllSnapshotSlices(TabletStateSnapshot snapshot)
        {
            var context = CreateSnapshotRefreshContext();
            RefreshBalanceSlice(snapshot);
            RefreshStatusSlice(snapshot);
            RefreshCargoSlice(snapshot, context);
            RefreshNearestIndustrySlice(snapshot, context);
            RefreshNetworkSlice(snapshot);
            RefreshMarketSlice(snapshot, context);
        }

        private void RefreshDirtySnapshotSlices(TabletStateSnapshot snapshot)
        {
            var needsContext = _cargoDirty || _nearestIndustryDirty || _marketDirty;
            var context = needsContext ? CreateSnapshotRefreshContext() : null;

            if (_balanceDirty)
            {
                RefreshBalanceSlice(snapshot);
            }

            if (_statusDirty)
            {
                RefreshStatusSlice(snapshot);
            }

            if (_cargoDirty)
            {
                RefreshCargoSlice(snapshot, context);
            }

            if (_nearestIndustryDirty)
            {
                RefreshNearestIndustrySlice(snapshot, context);
            }

            if (_networkDirty)
            {
                RefreshNetworkSlice(snapshot);
            }

            if (_marketDirty)
            {
                RefreshMarketSlice(snapshot, context);
            }
        }

        private SnapshotRefreshContext CreateSnapshotRefreshContext()
        {
            var player = _getPlayer != null ? _getPlayer() : null;
            Vehicle poweredVehicle = null;
            Vehicle cargoVehicle = null;
            var hasVehicleContext = player != null
                && player.Exists()
                && _fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle);
            if (!hasVehicleContext)
            {
                poweredVehicle = null;
                cargoVehicle = null;
            }

            var cargoState = cargoVehicle != null && cargoVehicle.Exists()
                ? _fleetManager.GetOrCreateCargoState(cargoVehicle)
                : null;
            var nearestIndustry = _getNearestIndustry != null ? _getNearestIndustry() : null;
            return new SnapshotRefreshContext(poweredVehicle, cargoVehicle, cargoState, hasVehicleContext, nearestIndustry);
        }

        private void RefreshBalanceSlice(TabletStateSnapshot snapshot)
        {
            snapshot.Balance = _getProfit != null ? _getProfit() : 0f;
        }

        private void RefreshStatusSlice(TabletStateSnapshot snapshot)
        {
            snapshot.TransferInProgress = _hasPendingTransfer != null && _hasPendingTransfer();
            snapshot.StatusBanner = _getStatusBanner != null ? _getStatusBanner() ?? string.Empty : string.Empty;
            snapshot.ActiveNpcRouteCount = _npcLogisticsManager != null && _npcLogisticsManager.Contracts != null
                ? _npcLogisticsManager.Contracts.Count()
                : 0;
            snapshot.ControlledDistrictCount = _getControlledDistrictCount != null ? _getControlledDistrictCount() : 0;
            snapshot.ActiveCorridorCount = _getActiveCorridorCount != null ? _getActiveCorridorCount() : 0;
            snapshot.SecuredSupportSiteCount = _getSecuredSupportSiteCount != null ? _getSecuredSupportSiteCount() : 0;
        }

        private void RefreshCargoSlice(TabletStateSnapshot snapshot, SnapshotRefreshContext context)
        {
            snapshot.HasCargoVehicle = false;
            snapshot.CargoIsEmpty = false;
            snapshot.CargoVehicleName = string.Empty;
            snapshot.CargoType = VehicleCargoType.Unknown;
            snapshot.CargoCommodity = string.Empty;
            snapshot.CargoWeightTons = 0f;
            snapshot.CargoCapacityTons = 0f;
            snapshot.CargoCapacityRatio = 0f;
            snapshot.HasPoweredVehicle = false;
            snapshot.PoweredVehicleName = string.Empty;
            snapshot.FuelVehicleMatchesCargoVehicle = false;
            snapshot.FuelIsEmpty = false;
            snapshot.FuelCurrentLiters = 0f;
            snapshot.FuelCapacityLiters = 0f;
            snapshot.FuelRatio = 0f;

            if (context == null)
            {
                return;
            }

            if (context.CargoVehicle != null && context.CargoVehicle.Exists() && context.CargoState != null)
            {
                snapshot.HasCargoVehicle = true;
                snapshot.CargoVehicleName = context.CargoVehicle.DisplayName;
                snapshot.CargoType = context.CargoState.CargoType;
                snapshot.CargoCommodity = context.CargoState.IsEmpty ? "Empty" : context.CargoState.Commodity;
                snapshot.CargoIsEmpty = context.CargoState.IsEmpty;
                snapshot.CargoWeightTons = Math.Max(0f, context.CargoState.WeightTons);
                snapshot.CargoCapacityTons = Math.Max(0f, context.CargoState.CapacityTons);
                snapshot.CargoCapacityRatio = snapshot.CargoCapacityTons <= 0.001f
                    ? 0f
                    : ModMath.Clamp01(snapshot.CargoWeightTons / snapshot.CargoCapacityTons);
            }

            if (!context.HasVehicleContext)
            {
                return;
            }

            var fuelTelemetry = _vehicleFuelSystem.GetTelemetry(context.PoweredVehicle, context.CargoVehicle);
            if (fuelTelemetry == null)
            {
                return;
            }

            snapshot.HasPoweredVehicle = true;
            snapshot.PoweredVehicleName = context.PoweredVehicle.DisplayName;
            snapshot.FuelVehicleMatchesCargoVehicle = !fuelTelemetry.UsesSeparatePoweredVehicle;
            snapshot.FuelIsEmpty = fuelTelemetry.IsOutOfFuel;
            snapshot.FuelCurrentLiters = Math.Max(0f, fuelTelemetry.CurrentLiters);
            snapshot.FuelCapacityLiters = Math.Max(0f, fuelTelemetry.CapacityLiters);
            snapshot.FuelRatio = fuelTelemetry.FuelRatio;
        }

        private void RefreshNearestIndustrySlice(TabletStateSnapshot snapshot, SnapshotRefreshContext context)
        {
            snapshot.NearestIndustry = null;
            snapshot.HasNearestIndustry = false;
            snapshot.CanInteractWithNearestIndustry = false;
            snapshot.NearestIndustryName = string.Empty;
            snapshot.NearestIndustryDistance = float.MaxValue;
            snapshot.NearestIndustryInputs = Array.Empty<string>();
            snapshot.NearestIndustryOutputs = Array.Empty<string>();
            snapshot.NearestIndustryProductionRateTonsPerHour = 0f;
            snapshot.NearestIndustryUtilizationPercent = 0f;
            snapshot.NearestIndustryOmegaStorageTons = 0f;
            snapshot.NearestIndustryOmegaCapacityTons = 0f;
            snapshot.NearestIndustryOwnedForGameplay = false;
            snapshot.NearestIndustryRequiresPurchase = false;
            snapshot.NearestIndustryHasPermitForGameplay = false;
            snapshot.NearestIndustryRequiresPermit = false;
            snapshot.NearestIndustryProductionWarning = string.Empty;

            var nearestIndustry = context != null ? context.NearestIndustry : null;
            if (nearestIndustry == null)
            {
                return;
            }

            snapshot.NearestIndustry = nearestIndustry;
            snapshot.HasNearestIndustry = true;
            snapshot.NearestIndustryName = nearestIndustry.Name;
            snapshot.NearestIndustryInputs = nearestIndustry.SortedAcceptedInputs != null
                ? nearestIndustry.SortedAcceptedInputs.ToArray()
                : Array.Empty<string>();
            snapshot.NearestIndustryOutputs = nearestIndustry.SortedOutputs != null
                ? nearestIndustry.SortedOutputs.ToArray()
                : Array.Empty<string>();
            snapshot.NearestIndustryProductionRateTonsPerHour = nearestIndustry.CurrentOutputPerHourTons;
            snapshot.NearestIndustryUtilizationPercent = nearestIndustry.LastUtilizationPercent;
            snapshot.NearestIndustryOmegaStorageTons = nearestIndustry.OmegaStorage;
            snapshot.NearestIndustryOmegaCapacityTons = nearestIndustry.OmegaCapacityTons;
            snapshot.NearestIndustryOwnedForGameplay = _industryManager.IsIndustryOwnedForGameplay(nearestIndustry);
            snapshot.NearestIndustryRequiresPurchase = _industryManager.RequiresIndustryPurchase(nearestIndustry);
            snapshot.NearestIndustryHasPermitForGameplay = _industryManager.HasContractorPermitForGameplay(nearestIndustry);
            snapshot.NearestIndustryRequiresPermit = _industryManager.RequiresContractorPermit(nearestIndustry);
            snapshot.NearestIndustryProductionWarning = nearestIndustry.GetProductionWarning() ?? string.Empty;

            float distance;
            snapshot.CanInteractWithNearestIndustry = IsIndustryInRange(nearestIndustry, 4.8f, out distance);
            snapshot.NearestIndustryDistance = distance;
        }

        private void RefreshNetworkSlice(TabletStateSnapshot snapshot)
        {
            snapshot.IndustrySummaries = BuildLocationSummaries(ExternalLocationKind.Industry, TabletLocationFilters.IsProductionIndustry);
            snapshot.ConstructionSiteSummaries = BuildLocationSummaries(ExternalLocationKind.Industry, TabletLocationFilters.IsConstructionSite);
            snapshot.WarehouseSummaries = BuildLocationSummaries(ExternalLocationKind.Industry, TabletLocationFilters.IsWarehouse);
            snapshot.StoreSummaries = BuildLocationSummaries(ExternalLocationKind.Store);
            snapshot.GasStationSummaries = BuildLocationSummaries(ExternalLocationKind.GasStation);
        }

        private void RefreshMarketSlice(TabletStateSnapshot snapshot, SnapshotRefreshContext context)
        {
            snapshot.MarketMultiplier = _globalMarket.PriceMultiplier;
            snapshot.MarketHighlights = BuildMarketHighlights(context != null ? context.NearestIndustry : null, context != null ? context.CargoState : null);
            snapshot.MarketPrices = BuildMarketPrices();
        }

        private void MarkVolatileSlicesDirty()
        {
            _balanceDirty = true;
            _cargoDirty = true;
            _nearestIndustryDirty = true;
            _marketDirty = true;
            _statusDirty = true;
        }

        private bool HasSnapshotDirtyState()
        {
            return _balanceDirty
                || _cargoDirty
                || _nearestIndustryDirty
                || _marketDirty
                || _networkDirty
                || _statusDirty;
        }

        private void ClearSnapshotDirtyState()
        {
            _balanceDirty = false;
            _cargoDirty = false;
            _nearestIndustryDirty = false;
            _marketDirty = false;
            _networkDirty = false;
            _statusDirty = false;
        }
    }
}