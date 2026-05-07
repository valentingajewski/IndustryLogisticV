using System;
using System.Collections.Generic;
using GTA;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using WinForms = System.Windows.Forms;

namespace LSOL.UI
{
    public sealed class IndustryTabletController
    {
        private const int LoadOptionsRefreshIntervalMs = 250;

        private readonly IndustryTabletUi _industryTablet;
        private readonly FleetManager _fleetManager;
        private readonly IndustryManager _industryManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly Func<Industry, GTA.Math.Vector3> _getIndustryMarkerPosition;
        private readonly List<string> _cachedLoadOptions;
        private readonly Dictionary<string, string> _cachedLoadOptionSubtitles;

        private Industry _cachedLoadIndustry;
        private int _cachedLoadVehicleHandle;
        private int _lastLoadOptionsRefreshMs;
        private float _cachedLoadFreeCapacityTons;
        private VehicleCargoType _cachedLoadCargoType;
        private bool _hasCachedLoadOptions;

        public IndustryTabletController(
            FleetManager fleetManager,
            IndustryManager industryManager,
            GlobalMarketManager globalMarket,
            Func<Industry, GTA.Math.Vector3> getIndustryMarkerPosition)
        {
            _industryTablet = new IndustryTabletUi();
            _fleetManager = fleetManager;
            _industryManager = industryManager;
            _globalMarket = globalMarket;
            _getIndustryMarkerPosition = getIndustryMarkerPosition;
            _cachedLoadOptions = new List<string>();
            _cachedLoadOptionSubtitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _cachedLoadCargoType = VehicleCargoType.Unknown;
            _cachedLoadFreeCapacityTons = -1f;
            _lastLoadOptionsRefreshMs = int.MinValue;
        }

        public event Action<Industry> LoadRequested
        {
            add { _industryTablet.LoadRequested += value; }
            remove { _industryTablet.LoadRequested -= value; }
        }

        public event Action<Industry, string> LoadCommodityRequested
        {
            add { _industryTablet.LoadCommodityRequested += value; }
            remove { _industryTablet.LoadCommodityRequested -= value; }
        }

        public event Action<Industry> UnloadRequested
        {
            add { _industryTablet.UnloadRequested += value; }
            remove { _industryTablet.UnloadRequested -= value; }
        }

        public event Action<Industry, bool> UnloadModeRequested
        {
            add { _industryTablet.UnloadModeRequested += value; }
            remove { _industryTablet.UnloadModeRequested -= value; }
        }

        public event Action<Industry> IndustryPurchaseRequested
        {
            add { _industryTablet.IndustryPurchaseRequested += value; }
            remove { _industryTablet.IndustryPurchaseRequested -= value; }
        }

        public event Action<Industry, IndustryUpgradeModule> UpgradeModuleRequested
        {
            add { _industryTablet.UpgradeModuleRequested += value; }
            remove { _industryTablet.UpgradeModuleRequested -= value; }
        }

        public event Action<Industry> VehicleSpawnerRequested
        {
            add { _industryTablet.VehicleSpawnerRequested += value; }
            remove { _industryTablet.VehicleSpawnerRequested -= value; }
        }

        public bool IsOpen
        {
            get { return _industryTablet.IsOpen; }
        }

        public Industry ActiveIndustry
        {
            get { return _industryTablet.ActiveIndustry; }
        }

        public bool HandleKey(WinForms.Keys key, ControlBindings controls)
        {
            return _industryTablet.HandleKey(key, controls);
        }

        public bool TryOpen(Ped player, Industry nearestIndustry, float interactionDistance, Action beforeOpen, Action<string> showStatus)
        {
            if (player == null || !player.Exists())
            {
                return false;
            }

            if (nearestIndustry == null || player.Position.DistanceTo(_getIndustryMarkerPosition(nearestIndustry)) > interactionDistance)
            {
                showStatus("No industry marker in range.");
                return false;
            }

            ClearLoadOptionsCache(false);
            beforeOpen();
            _industryTablet.Open(nearestIndustry);
            return true;
        }

        public void Draw(Ped player, float profitBalance, VehicleCargoType fallbackCargoType, float interactionDistance, Action<string> showStatus)
        {
            if (!_industryTablet.IsOpen)
            {
                return;
            }

            if (player == null || !player.Exists())
            {
                Close();
                return;
            }

            var industry = _industryTablet.ActiveIndustry;
            if (industry == null)
            {
                Close();
                return;
            }

            if (player.Position.DistanceTo(_getIndustryMarkerPosition(industry)) > interactionDistance + 2.4f)
            {
                showStatus("Tablet signal lost. Move closer to the industry marker.");
                Close();
                return;
            }

            _industryTablet.UpdateProfitBalance(profitBalance);
            UpdateLoadOptions(player, industry, fallbackCargoType);
            _industryTablet.UpdateOwnershipState(
                _industryManager.IsIndustryOwnedForGameplay(industry),
                _industryManager.RequiresIndustryPurchase(industry),
                industry.IndustryPrice,
                industry.IndustryOwnerCut);
            _industryTablet.DrawAndHandleInput();
        }

        public void Close()
        {
            if (!_industryTablet.IsOpen)
            {
                return;
            }

            ClearLoadOptionsCache(false);
            _industryTablet.Close();
        }

        private void UpdateLoadOptions(Ped player, Industry industry, VehicleCargoType fallbackCargoType)
        {
            if (!_industryTablet.IsOpen || industry == null || player == null || !player.Exists())
            {
                ClearLoadOptionsCache(true);
                return;
            }

            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                ClearLoadOptionsCache(true);
                return;
            }

            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null || !cargoState.IsEmpty)
            {
                ClearLoadOptionsCache(true);
                return;
            }

            var cargoType = cargoState.CargoType;
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = fallbackCargoType;
            }

            var freeCapacityTons = Math.Max(0f, cargoState.FreeCapacityTons);
            var now = Game.GameTime;
            if (CanReuseCachedLoadOptions(industry, cargoVehicle.Handle, cargoType, freeCapacityTons, now))
            {
                return;
            }

            _industryManager.PopulateLoadableOutputs(industry, cargoType, _cachedLoadOptions);
            _cachedLoadOptions.RemoveAll(commodity => !_fleetManager.CanVehicleCarryCommodity(cargoVehicle, commodity));
            BuildLoadOptionSubtitles(industry, _cachedLoadOptions, freeCapacityTons, _cachedLoadOptionSubtitles);

            _cachedLoadIndustry = industry;
            _cachedLoadVehicleHandle = cargoVehicle.Handle;
            _cachedLoadCargoType = cargoType;
            _cachedLoadFreeCapacityTons = freeCapacityTons;
            _lastLoadOptionsRefreshMs = now;
            _hasCachedLoadOptions = true;

            _industryTablet.SetLoadOptions(_cachedLoadOptions, _cachedLoadOptionSubtitles);
        }

        private bool CanReuseCachedLoadOptions(Industry industry, int cargoVehicleHandle, VehicleCargoType cargoType, float freeCapacityTons, int now)
        {
            return _hasCachedLoadOptions
                && ReferenceEquals(_cachedLoadIndustry, industry)
                && _cachedLoadVehicleHandle == cargoVehicleHandle
                && _cachedLoadCargoType == cargoType
                && Math.Abs(_cachedLoadFreeCapacityTons - freeCapacityTons) < 0.05f
                && now - _lastLoadOptionsRefreshMs < LoadOptionsRefreshIntervalMs;
        }

        private void BuildLoadOptionSubtitles(Industry industry, List<string> loadOptions, float truckFreeCapacityTons, Dictionary<string, string> subtitles)
        {
            if (subtitles == null)
            {
                return;
            }

            subtitles.Clear();
            if (industry == null || loadOptions == null || loadOptions.Count == 0)
            {
                return;
            }

            var maxLoadTons = Math.Max(0f, truckFreeCapacityTons);
            for (int i = 0; i < loadOptions.Count; i++)
            {
                var commodity = loadOptions[i];
                if (string.IsNullOrWhiteSpace(commodity))
                {
                    continue;
                }

                var availableTons = Math.Max(0f, industry.GetStock(commodity));
                var loadableTons = Math.Min(availableTons, maxLoadTons);
                var unitPrice = Math.Max(0f, _globalMarket.GetUnitPrice(commodity));
                var cargoValue = loadableTons * unitPrice;

                subtitles[commodity.Trim()] = string.Format(
                    "Cargo value: ${0:0} ({1:0.0}t | ${2:0}/t)",
                    cargoValue,
                    loadableTons,
                    unitPrice);
            }
        }

        private void ClearLoadOptionsCache(bool updateUi)
        {
            if (!_hasCachedLoadOptions && _cachedLoadOptions.Count == 0 && _cachedLoadOptionSubtitles.Count == 0)
            {
                return;
            }

            _cachedLoadIndustry = null;
            _cachedLoadVehicleHandle = 0;
            _cachedLoadCargoType = VehicleCargoType.Unknown;
            _cachedLoadFreeCapacityTons = -1f;
            _lastLoadOptionsRefreshMs = int.MinValue;
            _hasCachedLoadOptions = false;
            _cachedLoadOptions.Clear();
            _cachedLoadOptionSubtitles.Clear();

            if (updateUi)
            {
                _industryTablet.SetLoadOptions(null);
            }
        }
    }
}