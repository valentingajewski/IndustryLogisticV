using System.Collections.Generic;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class PropertyOwnershipPersistenceSnapshot
    {
        public PropertyOwnershipPersistenceSnapshot()
        {
            Offices = new List<OfficeOwnershipPersistenceEntry>();
            OfficeObjects = new List<OfficeObjectPersistenceEntry>();
            Apartments = new List<ApartmentOwnershipPersistenceEntry>();
            CommercialVehicles = new List<OwnedCommercialVehiclePersistenceEntry>();
            PersonalVehicles = new List<OwnedPersonalVehiclePersistenceEntry>();
        }

        public string ActiveOfficeId { get; set; }

        public string ActiveApartmentId { get; set; }

        public int LastSuccessfulApartmentSleepMinute { get; set; } = -1;

        public List<OfficeOwnershipPersistenceEntry> Offices { get; }

        public List<OfficeObjectPersistenceEntry> OfficeObjects { get; }

        public List<ApartmentOwnershipPersistenceEntry> Apartments { get; }

        public List<OwnedCommercialVehiclePersistenceEntry> CommercialVehicles { get; }

        public List<OwnedPersonalVehiclePersistenceEntry> PersonalVehicles { get; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ActiveOfficeId)
                    || !string.IsNullOrWhiteSpace(ActiveApartmentId)
                    || LastSuccessfulApartmentSleepMinute >= 0
                    || Offices.Count > 0
                    || OfficeObjects.Count > 0
                    || Apartments.Count > 0
                    || CommercialVehicles.Count > 0
                    || PersonalVehicles.Count > 0;
            }
        }
    }

    public sealed class OfficeOwnershipPersistenceEntry
    {
        public string OfficeId { get; set; }

        public bool IsOwned { get; set; }

        public bool IsRented { get; set; }

        public bool IsAccessSuspended { get; set; }

        public float OutstandingRent { get; set; }

        public int LastChargedWeekIndex { get; set; } = -1;
    }

    public sealed class OfficeObjectPersistenceEntry
    {
        public string InstanceId { get; set; }

        public string OfficeId { get; set; }

        public int DefinitionId { get; set; }

        public bool IsPlaced { get; set; }

        public Vector3 Position { get; set; }

        public Vector3 Rotation { get; set; }

        public float StoredResourceAmount { get; set; }
    }

    public sealed class ApartmentOwnershipPersistenceEntry
    {
        public string InteriorId { get; set; }

        public bool IsOwned { get; set; }

        public bool IsRented { get; set; }

        public bool IsAccessSuspended { get; set; }

        public float OutstandingRent { get; set; }

        public int LastChargedWeekIndex { get; set; } = -1;
    }

    public sealed class OwnedCommercialVehiclePersistenceEntry
    {
        public string AssetId { get; set; }

        public string DisplayName { get; set; }

        public string PoweredModelName { get; set; }

        public string CargoModelName { get; set; }

        public bool HasSeparateCargoVehicle { get; set; }

        public float PurchasePrice { get; set; }

        public string AssignedOfficeId { get; set; }

        public bool IsRental { get; set; }

        public float DailyRent { get; set; }

        public int LastChargedDayIndex { get; set; } = -1;

        public bool InActiveGarage { get; set; }

        public bool IsDeployed { get; set; }

        public Vector3 PoweredPosition { get; set; }

        public float PoweredHeading { get; set; }

        public VehicleCargoType CargoType { get; set; }

        public float CapacityTons { get; set; }

        public string Commodity { get; set; }

        public float WeightTons { get; set; }

        public float CargoCondition { get; set; }

        public float TotalLostTons { get; set; }

        public string SourceIndustryId { get; set; }

        public string SourceDistrictName { get; set; }

        public float CurrentFuelLiters { get; set; }
    }

    public sealed class OwnedPersonalVehiclePersistenceEntry
    {
        public string AssetId { get; set; }

        public string DisplayName { get; set; }

        public string ModelName { get; set; }

        public string Category { get; set; }

        public float PurchasePrice { get; set; }

        public string AssignedApartmentId { get; set; }

        public bool IsDeployed { get; set; }

        public Vector3 Position { get; set; }

        public float Heading { get; set; }
    }
}