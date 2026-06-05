using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
{
    public enum CommercialDealershipSection
    {
        TruckTractor = 0,
        Trailers = 1,
        TrucksVans = 2,
    }

    public sealed class VehicleSpawnController
    {
        private readonly FleetManager _fleetManager;
        private readonly Vector3 _vehicleSpawnMarkerSeed;
        private readonly float _vehicleSpawnHeading;
        private readonly List<VehicleCargoType> _filterOrder;
        private readonly List<CommercialDealershipSection> _commercialDealershipSectionOrder;
        private readonly Func<VehicleDefinition, bool> _isVehicleAvailable;

        private List<VehicleDefinition> _filteredVehicles;
        private List<VehicleDefinition> _tractorVehicles;
        private int _selectedVehicleIndex;
        private int _selectedTractorIndex;
        private bool _commercialDealershipSectionModeEnabled;

        public VehicleSpawnController(
            FleetManager fleetManager,
            Vector3 vehicleSpawnMarkerSeed,
            float vehicleSpawnHeading,
            IEnumerable<VehicleCargoType> filterOrder,
            VehicleCargoType defaultFilter,
            Func<VehicleDefinition, bool> isVehicleAvailable = null)
        {
            _fleetManager = fleetManager;
            _vehicleSpawnMarkerSeed = vehicleSpawnMarkerSeed;
            _vehicleSpawnHeading = vehicleSpawnHeading;
            _isVehicleAvailable = isVehicleAvailable;
            _filterOrder = filterOrder == null
                ? new List<VehicleCargoType>()
                : new List<VehicleCargoType>(filterOrder);
            _commercialDealershipSectionOrder = new List<CommercialDealershipSection>
            {
                CommercialDealershipSection.TruckTractor,
                CommercialDealershipSection.Trailers,
                CommercialDealershipSection.TrucksVans,
            };

            if (_filterOrder.Count == 0)
            {
                _filterOrder = _fleetManager.Definitions
                    .Where(x => x != null && x.IsEnabled)
                    .Select(x => x.CargoType)
                    .Where(x => x != VehicleCargoType.Unknown && x != VehicleCargoType.Trailer)
                    .Distinct()
                    .ToList();
            }

            _filteredVehicles = new List<VehicleDefinition>();
            _tractorVehicles = new List<VehicleDefinition>();
            SelectedCommercialDealershipSection = CommercialDealershipSection.TruckTractor;
            if (_filterOrder.Count > 0 && !_filterOrder.Contains(defaultFilter))
            {
                defaultFilter = _filterOrder[0];
            }

            SelectedFilter = defaultFilter;
            RefreshFilteredVehicles();
        }

        public VehicleCargoType SelectedFilter { get; private set; }

        public bool IsCommercialDealershipSectionModeEnabled
        {
            get { return _commercialDealershipSectionModeEnabled; }
        }

        public CommercialDealershipSection SelectedCommercialDealershipSection { get; private set; }

        public string CurrentCommercialDealershipSectionCaption
        {
            get { return GetCommercialDealershipSectionLabel(SelectedCommercialDealershipSection); }
        }

        public string CurrentVehicleCaption
        {
            get
            {
                var selectedVehicle = SelectedVehicleDefinition;
                if (selectedVehicle == null)
                {
                    return "None";
                }

                return GetVehicleLabel(selectedVehicle);
            }
        }

        public VehicleDefinition SelectedVehicleDefinition
        {
            get
            {
                return _selectedVehicleIndex < 0 || _selectedVehicleIndex >= _filteredVehicles.Count
                    ? null
                    : _filteredVehicles[_selectedVehicleIndex];
            }
        }

        public string CurrentTractorCaption
        {
            get
            {
                if (_tractorVehicles.Count == 0)
                {
                    return "unavailable";
                }

                var selectedVehicle = SelectedVehicleDefinition;
                if (selectedVehicle != null && !selectedVehicle.IsTrailer)
                {
                    return "auto (not needed)";
                }

                var selectedTractor = SelectedTractorDefinition;
                if (selectedTractor == null)
                {
                    return "None";
                }

                return GetVehicleLabel(selectedTractor);
            }
        }

        public VehicleDefinition SelectedTractorDefinition
        {
            get
            {
                return _selectedTractorIndex < 0 || _selectedTractorIndex >= _tractorVehicles.Count
                    ? null
                    : _tractorVehicles[_selectedTractorIndex];
            }
        }

        public bool HasAnySelection => SelectedVehicleDefinition != null || SelectedTractorDefinition != null;

        public void SetCommercialDealershipSectionMode(bool enabled)
        {
            _commercialDealershipSectionModeEnabled = enabled;
            RefreshFilteredVehicles();
        }

        public void ChangeCommercialDealershipSection(int delta)
        {
            if (!_commercialDealershipSectionModeEnabled || _commercialDealershipSectionOrder.Count == 0)
            {
                return;
            }

            var index = _commercialDealershipSectionOrder.IndexOf(SelectedCommercialDealershipSection);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + delta + _commercialDealershipSectionOrder.Count) % _commercialDealershipSectionOrder.Count;
            SelectedCommercialDealershipSection = _commercialDealershipSectionOrder[index];
            RefreshFilteredVehicles();
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
            var selectedVehicleModelName = SelectedVehicleDefinition != null
                ? SelectedVehicleDefinition.ModelName
                : string.Empty;
            var keepNoVehicleSelection = _selectedVehicleIndex < 0;
            var selectedTractorModelName = SelectedTractorDefinition != null
                ? SelectedTractorDefinition.ModelName
                : string.Empty;
            var keepNoTractorSelection = _selectedTractorIndex < 0;

            if (_commercialDealershipSectionModeEnabled)
            {
                _filteredVehicles = GetCommercialDealershipSectionVehicles()
                    .Where(IsVehicleAvailable)
                    .ToList();
                _tractorVehicles = new List<VehicleDefinition>();
            }
            else
            {
                _filteredVehicles = _fleetManager.GetSpawnableForCargoType(SelectedFilter)
                    .Where(IsVehicleAvailable)
                    .ToList();
                _tractorVehicles = _fleetManager.GetTractorDefinitions()
                    .Where(IsVehicleAvailable)
                    .ToList();
            }

            if (_filteredVehicles.Count == 0 || keepNoVehicleSelection)
            {
                _selectedVehicleIndex = -1;
            }
            else
            {
                var preservedIndex = _filteredVehicles.FindIndex(definition => string.Equals(definition.ModelName, selectedVehicleModelName, StringComparison.OrdinalIgnoreCase));
                _selectedVehicleIndex = preservedIndex >= 0 ? preservedIndex : 0;
            }

            if (_tractorVehicles.Count == 0 || keepNoTractorSelection)
            {
                _selectedTractorIndex = -1;
            }
            else
            {
                var preservedTractorIndex = _tractorVehicles.FindIndex(definition => string.Equals(definition.ModelName, selectedTractorModelName, StringComparison.OrdinalIgnoreCase));
                _selectedTractorIndex = preservedTractorIndex >= 0 ? preservedTractorIndex : 0;
            }
        }

        public void ChangeVehicleSelection(int delta)
        {
            var optionCount = _filteredVehicles.Count + 1;
            if (optionCount <= 1)
            {
                _selectedVehicleIndex = _filteredVehicles.Count > 0 ? 0 : -1;
                return;
            }

            var optionIndex = _selectedVehicleIndex >= 0
                ? _selectedVehicleIndex + 1
                : 0;
            optionIndex = (optionIndex + delta % optionCount + optionCount) % optionCount;
            _selectedVehicleIndex = optionIndex == 0 ? -1 : optionIndex - 1;
        }

        public void ChangeTractorSelection(int delta)
        {
            var optionCount = _tractorVehicles.Count + 1;
            if (optionCount <= 1)
            {
                _selectedTractorIndex = _tractorVehicles.Count > 0 ? 0 : -1;
                return;
            }

            var optionIndex = _selectedTractorIndex >= 0
                ? _selectedTractorIndex + 1
                : 0;
            optionIndex = (optionIndex + delta % optionCount + optionCount) % optionCount;
            _selectedTractorIndex = optionIndex == 0 ? -1 : optionIndex - 1;
        }

        public bool SpawnSelectedVehicle(Func<Vector3, Vector3> getGroundPosition, out Vehicle truck, out Vehicle cargoVehicle, out string message)
        {
            return SpawnSelectedVehicle(getGroundPosition, _vehicleSpawnMarkerSeed, _vehicleSpawnHeading, out truck, out cargoVehicle, out message);
        }

        public bool SpawnSelectedVehicle(Func<Vector3, Vector3> getGroundPosition, Vector3 spawnPosition, float spawnHeading, out Vehicle truck, out Vehicle cargoVehicle, out string message)
        {
            truck = null;
            cargoVehicle = null;

            var selected = SelectedVehicleDefinition;
            var tractor = SelectedTractorDefinition;
            if (selected != null && !selected.IsTrailer)
            {
                tractor = null;
            }

            if (selected == null && tractor == null)
            {
                message = _filteredVehicles.Count == 0
                    ? "No vehicle available in this cargo filter. Select a truck to spawn it on its own."
                    : "Select a truck and/or cargo vehicle.";
                return false;
            }

            return _fleetManager.SpawnSelectedVehicle(
                selected,
                tractor,
                getGroundPosition != null ? getGroundPosition(spawnPosition) : spawnPosition,
                spawnHeading,
                out truck,
                out cargoVehicle,
                out message);
        }

        private static string GetVehicleLabel(VehicleDefinition definition)
        {
            if (definition == null)
            {
                return "None";
            }

            return string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.ModelName
                : definition.DisplayName;
        }

        private bool IsVehicleAvailable(VehicleDefinition definition)
        {
            return definition != null && (_isVehicleAvailable == null || _isVehicleAvailable(definition));
        }

        private IEnumerable<VehicleDefinition> GetCommercialDealershipSectionVehicles()
        {
            switch (SelectedCommercialDealershipSection)
            {
                case CommercialDealershipSection.TruckTractor:
                    return _fleetManager.GetTractorDefinitions();
                case CommercialDealershipSection.Trailers:
                    return _fleetManager.Definitions.Where(definition => definition != null && definition.IsEnabled && definition.IsTrailer);
                case CommercialDealershipSection.TrucksVans:
                    return _fleetManager.Definitions.Where(definition => definition != null && definition.IsEnabled && definition.IsRigid);
                default:
                    return Enumerable.Empty<VehicleDefinition>();
            }
        }

        private static string GetCommercialDealershipSectionLabel(CommercialDealershipSection section)
        {
            switch (section)
            {
                case CommercialDealershipSection.TruckTractor:
                    return "Truck tractor";
                case CommercialDealershipSection.Trailers:
                    return "Trailers";
                case CommercialDealershipSection.TrucksVans:
                    return "Trucks/Vans";
                default:
                    return "Unknown";
            }
        }
    }
}