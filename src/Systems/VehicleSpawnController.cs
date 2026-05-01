using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Systems
{
    public sealed class VehicleSpawnController
    {
        private readonly FleetManager _fleetManager;
        private readonly Vector3 _vehicleSpawnMarkerSeed;
        private readonly float _vehicleSpawnHeading;
        private readonly List<VehicleCargoType> _filterOrder;
        private readonly List<VehicleDefinition> _tractorVehicles;

        private List<VehicleDefinition> _filteredVehicles;
        private int _selectedVehicleIndex;
        private int _selectedTractorIndex;

        public VehicleSpawnController(
            FleetManager fleetManager,
            Vector3 vehicleSpawnMarkerSeed,
            float vehicleSpawnHeading,
            IEnumerable<VehicleCargoType> filterOrder,
            VehicleCargoType defaultFilter)
        {
            _fleetManager = fleetManager;
            _vehicleSpawnMarkerSeed = vehicleSpawnMarkerSeed;
            _vehicleSpawnHeading = vehicleSpawnHeading;
            _filterOrder = filterOrder == null
                ? new List<VehicleCargoType>()
                : new List<VehicleCargoType>(filterOrder);
            _tractorVehicles = _fleetManager.GetTractorDefinitions();
            _filteredVehicles = new List<VehicleDefinition>();
            SelectedFilter = defaultFilter;
            RefreshFilteredVehicles();
        }

        public VehicleCargoType SelectedFilter { get; private set; }

        public string CurrentVehicleCaption
        {
            get
            {
                if (_filteredVehicles.Count == 0)
                {
                    return "Vehicle: none for this cargo filter";
                }

                return string.Format("Vehicle: {0}", _filteredVehicles[_selectedVehicleIndex]);
            }
        }

        public string CurrentTractorCaption
        {
            get
            {
                if (_filteredVehicles.Count == 0)
                {
                    return "Trailer Truck: n/a";
                }

                if (!_filteredVehicles[_selectedVehicleIndex].IsTrailer)
                {
                    return "Trailer Truck: auto (not needed)";
                }

                if (_tractorVehicles.Count == 0)
                {
                    return "Trailer Truck: unavailable";
                }

                return string.Format("Trailer Truck: {0}", _tractorVehicles[_selectedTractorIndex].ModelName);
            }
        }

        public void ChangeFilter(int delta)
        {
            var index = _filterOrder.IndexOf(SelectedFilter);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + delta + _filterOrder.Count) % _filterOrder.Count;
            SelectedFilter = _filterOrder[index];
            RefreshFilteredVehicles();
        }

        public void RefreshFilteredVehicles()
        {
            _filteredVehicles = _fleetManager.GetSpawnableForCargoType(SelectedFilter).ToList();
            _selectedVehicleIndex = 0;
        }

        public void ChangeVehicleSelection(int delta)
        {
            if (_filteredVehicles.Count == 0)
            {
                return;
            }

            _selectedVehicleIndex = (_selectedVehicleIndex + delta + _filteredVehicles.Count) % _filteredVehicles.Count;
        }

        public void ChangeTractorSelection(int delta)
        {
            if (_tractorVehicles.Count == 0)
            {
                return;
            }

            _selectedTractorIndex = (_selectedTractorIndex + delta + _tractorVehicles.Count) % _tractorVehicles.Count;
        }

        public bool SpawnSelectedVehicle(Func<Vector3, Vector3> getGroundPosition, out Vehicle truck, out Vehicle cargoVehicle, out string message)
        {
            truck = null;
            cargoVehicle = null;
            if (_filteredVehicles.Count == 0)
            {
                message = "No vehicle available in this cargo filter.";
                return false;
            }

            var selected = _filteredVehicles[_selectedVehicleIndex];
            VehicleDefinition tractor = null;
            if (selected.IsTrailer && _tractorVehicles.Count > 0)
            {
                tractor = _tractorVehicles[_selectedTractorIndex];
            }

            return _fleetManager.SpawnSelectedVehicle(
                selected,
                tractor,
                getGroundPosition(_vehicleSpawnMarkerSeed),
                _vehicleSpawnHeading,
                out truck,
                out cargoVehicle,
                out message);
        }
    }
}