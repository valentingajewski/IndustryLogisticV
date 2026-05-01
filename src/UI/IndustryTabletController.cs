using System;
using System.Collections.Generic;
using GTA;
using IndustryLogisticV.Config;
using IndustryLogisticV.Domain;
using IndustryLogisticV.Systems;
using WinForms = System.Windows.Forms;

namespace IndustryLogisticV.UI
{
    public sealed class IndustryTabletController
    {
        private readonly IndustryTabletUi _industryTablet;
        private readonly FleetManager _fleetManager;
        private readonly IndustryManager _industryManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly Func<Industry, GTA.Math.Vector3> _getIndustryMarkerPosition;

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

        public event Action<Industry, IndustryUpgradeModule> UpgradeModuleRequested
        {
            add { _industryTablet.UpgradeModuleRequested += value; }
            remove { _industryTablet.UpgradeModuleRequested -= value; }
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
            _industryTablet.DrawAndHandleInput();
        }

        public void Close()
        {
            if (!_industryTablet.IsOpen)
            {
                return;
            }

            _industryTablet.Close();
        }

        private void UpdateLoadOptions(Ped player, Industry industry, VehicleCargoType fallbackCargoType)
        {
            if (!_industryTablet.IsOpen || industry == null || player == null || !player.Exists())
            {
                _industryTablet.SetLoadOptions(new List<string>());
                return;
            }

            Vehicle driverVehicle;
            var cargoVehicle = _fleetManager.ResolveCargoVehicle(player, out driverVehicle);
            if (cargoVehicle == null || !cargoVehicle.Exists())
            {
                _industryTablet.SetLoadOptions(new List<string>());
                return;
            }

            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null || !cargoState.IsEmpty)
            {
                _industryTablet.SetLoadOptions(new List<string>());
                return;
            }

            var cargoType = cargoState.CargoType;
            if (cargoType == VehicleCargoType.Unknown || cargoType == VehicleCargoType.Trailer)
            {
                cargoType = fallbackCargoType;
            }

            var loadOptions = _industryManager.GetLoadableOutputs(industry, cargoType);
            var loadOptionSubtitles = BuildLoadOptionSubtitles(industry, loadOptions, cargoState.FreeCapacityTons);
            _industryTablet.SetLoadOptions(loadOptions, loadOptionSubtitles);
        }

        private Dictionary<string, string> BuildLoadOptionSubtitles(Industry industry, List<string> loadOptions, float truckFreeCapacityTons)
        {
            var subtitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (industry == null || loadOptions == null || loadOptions.Count == 0)
            {
                return subtitles;
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

            return subtitles;
        }
    }
}