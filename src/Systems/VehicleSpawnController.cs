using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
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

            if (_filterOrder.Count == 0)
            {
                _filterOrder = _fleetManager.Definitions
                    .Where(x => x != null && x.IsEnabled)
                    .Select(x => x.CargoType)
                    .Where(x => x != VehicleCargoType.Unknown && x != VehicleCargoType.Trailer)
                    .Distinct()
                    .ToList();
            }

            _tractorVehicles = _fleetManager.GetTractorDefinitions();
            _filteredVehicles = new List<VehicleDefinition>();
            if (_filterOrder.Count > 0 && !_filterOrder.Contains(defaultFilter))
            {
                defaultFilter = _filterOrder[0];
            }

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
                    return "None for this cargo filter";
                }

                return string.Format("{0}", _filteredVehicles[_selectedVehicleIndex]);
            }
        }

        public VehicleDefinition SelectedVehicleDefinition
        {
            get
            {
                return _filteredVehicles.Count == 0
                    ? null
                    : _filteredVehicles[_selectedVehicleIndex];
            }
        }

        public string CurrentTractorCaption
        {
            get
            {
                if (_filteredVehicles.Count == 0)
                {
                    return "n/a";
                }

                if (!_filteredVehicles[_selectedVehicleIndex].IsTrailer)
                {
                    return "auto (not needed)";
                }

                if (_tractorVehicles.Count == 0)
                {
                    return "unavailable";
                }

                return string.Format("{0}", _tractorVehicles[_selectedTractorIndex].ModelName);
            }
        }

        public VehicleDefinition SelectedTractorDefinition
        {
            get
            {
                var selectedVehicle = SelectedVehicleDefinition;
                if (selectedVehicle == null || !selectedVehicle.IsTrailer || _tractorVehicles.Count == 0)
                {
                    return null;
                }

                return _tractorVehicles[_selectedTractorIndex];
            }
        }

        public void ChangeFilter(int delta)
        {
            if (_filterOrder.Count == 0)
            {
                return;
            }

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
            return SpawnSelectedVehicle(getGroundPosition, _vehicleSpawnMarkerSeed, _vehicleSpawnHeading, out truck, out cargoVehicle, out message);
        }

        public bool SpawnSelectedVehicle(Func<Vector3, Vector3> getGroundPosition, Vector3 spawnPosition, float spawnHeading, out Vehicle truck, out Vehicle cargoVehicle, out string message)
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
                getGroundPosition(spawnPosition),
                spawnHeading,
                out truck,
                out cargoVehicle,
                out message);
        }
    }
}