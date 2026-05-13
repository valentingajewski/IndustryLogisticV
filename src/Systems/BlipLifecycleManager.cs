using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class BlipLifecycleManager
    {
        private readonly IndustryManager _industryManager;
        private readonly Func<IReadOnlyList<OfficeDefinition>> _getOfficeDefinitions;
        private readonly Func<string> _getActiveOfficeId;
        private readonly Func<IReadOnlyList<InteriorDefinition>> _getApartmentDefinitions;
        private readonly Func<string> _getActiveApartmentId;
        private readonly Func<Vector3> _getOfficeMarkerSeed;
        private readonly Func<Vector3> _getVehicleSpawnMarkerSeed;
        private readonly Vector3 _commercialDealershipMarker;
        private readonly Vector3 _personalDealershipMarker;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Func<Industry, Vector3> _getIndustryMarkerPosition;
        private readonly Func<Industry, bool> _isPetrolServiceStation;
        private readonly TerritoryManager _territoryManager;
        private readonly List<Blip> _officeBlips;
        private readonly List<Blip> _apartmentBlips;
        private readonly List<Blip> _industryBlips;

        private Blip _commercialDealershipBlip;
        private Blip _personalDealershipBlip;
        private Blip _vehicleSpawnBlip;

        public BlipLifecycleManager(
            IndustryManager industryManager,
            Func<IReadOnlyList<OfficeDefinition>> getOfficeDefinitions,
            Func<string> getActiveOfficeId,
            Func<IReadOnlyList<InteriorDefinition>> getApartmentDefinitions,
            Func<string> getActiveApartmentId,
            Func<Vector3> getOfficeMarkerSeed,
            Func<Vector3> getVehicleSpawnMarkerSeed,
            Vector3 commercialDealershipMarker,
            Vector3 personalDealershipMarker,
            Func<Vector3, Vector3> getGroundPosition,
            Func<Industry, Vector3> getIndustryMarkerPosition,
            Func<Industry, bool> isPetrolServiceStation,
            TerritoryManager territoryManager = null)
        {
            _industryManager = industryManager;
            _getOfficeDefinitions = getOfficeDefinitions;
            _getActiveOfficeId = getActiveOfficeId;
            _getApartmentDefinitions = getApartmentDefinitions;
            _getActiveApartmentId = getActiveApartmentId;
            _getOfficeMarkerSeed = getOfficeMarkerSeed;
            _getVehicleSpawnMarkerSeed = getVehicleSpawnMarkerSeed;
            _commercialDealershipMarker = commercialDealershipMarker;
            _personalDealershipMarker = personalDealershipMarker;
            _getGroundPosition = getGroundPosition;
            _getIndustryMarkerPosition = getIndustryMarkerPosition;
            _isPetrolServiceStation = isPetrolServiceStation;
            _territoryManager = territoryManager;
            _officeBlips = new List<Blip>();
            _apartmentBlips = new List<Blip>();
            _industryBlips = new List<Blip>();
        }

        public void Create()
        {
            Destroy();

            CreateOfficeBlips();
            CreateApartmentBlips();
            CreateDealershipBlips();
            _vehicleSpawnBlip = CreateStaticBlip(_getGroundPosition(ResolveVehicleSpawnMarkerSeed()), BlipSprite.Garage2, BlipColor.White, "Vehicle Spawn", 0.9f);

            for (int i = 0; i < _industryManager.Industries.Count; i++)
            {
                var industry = _industryManager.Industries[i];
                var isPetrolStation = _isPetrolServiceStation(industry);
                var sprite = ResolveIndustryBlipSprite(industry, isPetrolStation);
                var color = ResolveIndustryBlipColor(industry, isPetrolStation);
                var blip = CreateStaticBlip(_getIndustryMarkerPosition(industry), sprite, color, ResolveIndustryBlipName(industry), 0.85f);
                if (blip != null && blip.Exists())
                {
                    _industryBlips.Add(blip);
                }
            }
        }

        public void Refresh()
        {
            var offices = ResolveOfficeDefinitions();
            var expectedOfficeBlipCount = offices.Count > 0 ? offices.Count : 1;
            if (_officeBlips.Count != expectedOfficeBlipCount)
            {
                Create();
                return;
            }

            if (offices.Count == 0)
            {
                if (_officeBlips.Count == 0)
                {
                    Create();
                    return;
                }

                var officeBlip = _officeBlips[0];
                if (officeBlip == null || !officeBlip.Exists())
                {
                    Create();
                    return;
                }

                officeBlip.Position = _getGroundPosition(ResolveOfficeMarkerSeed());
                officeBlip.Color = BlipColor.Green;
                officeBlip.Name = "Logistics Office";
                officeBlip.Scale = 1.0f;
            }
            else
            {
                var activeOfficeId = ResolveActiveOfficeId();
                for (int i = 0; i < offices.Count; i++)
                {
                    var office = offices[i];
                    var blip = _officeBlips[i];
                    if (blip == null || !blip.Exists())
                    {
                        Create();
                        return;
                    }

                    var isActive = !string.IsNullOrWhiteSpace(activeOfficeId)
                        && string.Equals(office.OfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase);
                    blip.Position = _getGroundPosition(office.MarkerPosition);
                    blip.Color = isActive ? BlipColor.Green : BlipColor.White;
                    blip.Name = ResolveOfficeBlipName(office, isActive);
                    blip.Scale = isActive ? 1.0f : 0.9f;
                }
            }

            var apartments = ResolveApartmentDefinitions();
            if (_apartmentBlips.Count != apartments.Count)
            {
                Create();
                return;
            }

            var activeApartmentId = ResolveActiveApartmentId();
            for (int i = 0; i < apartments.Count; i++)
            {
                var apartment = apartments[i];
                var blip = _apartmentBlips[i];
                if (blip == null || !blip.Exists())
                {
                    Create();
                    return;
                }

                var isActive = !string.IsNullOrWhiteSpace(activeApartmentId)
                    && string.Equals(apartment.InteriorId, activeApartmentId, StringComparison.OrdinalIgnoreCase);
                blip.Position = _getGroundPosition(apartment.ExteriorPosition);
                blip.Color = isActive ? BlipColor.Blue : BlipColor.White;
                blip.Name = ResolveApartmentBlipName(apartment, isActive);
                blip.Scale = isActive ? 0.95f : 0.85f;
            }

            if (_commercialDealershipBlip == null || !_commercialDealershipBlip.Exists()
                || _personalDealershipBlip == null || !_personalDealershipBlip.Exists())
            {
                Create();
                return;
            }

            _commercialDealershipBlip.Position = _getGroundPosition(_commercialDealershipMarker);
            _personalDealershipBlip.Position = _getGroundPosition(_personalDealershipMarker);

            if (_vehicleSpawnBlip != null && _vehicleSpawnBlip.Exists())
            {
                _vehicleSpawnBlip.Position = _getGroundPosition(ResolveVehicleSpawnMarkerSeed());
            }

            var count = Math.Min(_industryBlips.Count, _industryManager.Industries.Count);
            for (int i = 0; i < count; i++)
            {
                var blip = _industryBlips[i];
                if (blip == null || !blip.Exists())
                {
                    continue;
                }

                var industry = _industryManager.Industries[i];
                var isPetrolStation = _isPetrolServiceStation(industry);
                blip.Position = _getIndustryMarkerPosition(industry);
                blip.Sprite = ResolveIndustryBlipSprite(industry, isPetrolStation);
                blip.Color = ResolveIndustryBlipColor(industry, isPetrolStation);
                blip.Name = ResolveIndustryBlipName(industry);
            }
        }

        public void Destroy()
        {
            for (int i = 0; i < _officeBlips.Count; i++)
            {
                var blip = _officeBlips[i];
                if (blip != null && blip.Exists())
                {
                    blip.Delete();
                }
            }

            for (int i = 0; i < _apartmentBlips.Count; i++)
            {
                var blip = _apartmentBlips[i];
                if (blip != null && blip.Exists())
                {
                    blip.Delete();
                }
            }

            if (_commercialDealershipBlip != null && _commercialDealershipBlip.Exists())
            {
                _commercialDealershipBlip.Delete();
            }

            if (_personalDealershipBlip != null && _personalDealershipBlip.Exists())
            {
                _personalDealershipBlip.Delete();
            }

            if (_vehicleSpawnBlip != null && _vehicleSpawnBlip.Exists())
            {
                _vehicleSpawnBlip.Delete();
            }

            for (int i = 0; i < _industryBlips.Count; i++)
            {
                var blip = _industryBlips[i];
                if (blip != null && blip.Exists())
                {
                    blip.Delete();
                }
            }

            _officeBlips.Clear();
            _apartmentBlips.Clear();
            _industryBlips.Clear();
            _commercialDealershipBlip = null;
            _personalDealershipBlip = null;
            _vehicleSpawnBlip = null;
        }

        private void CreateOfficeBlips()
        {
            var offices = ResolveOfficeDefinitions();
            if (offices.Count == 0)
            {
                var fallbackBlip = CreateStaticBlip(_getGroundPosition(ResolveOfficeMarkerSeed()), BlipSprite.Office, BlipColor.Green, "Logistics Office", 1.0f);
                if (fallbackBlip != null && fallbackBlip.Exists())
                {
                    _officeBlips.Add(fallbackBlip);
                }

                return;
            }

            var activeOfficeId = ResolveActiveOfficeId();
            for (int i = 0; i < offices.Count; i++)
            {
                var office = offices[i];
                var isActive = !string.IsNullOrWhiteSpace(activeOfficeId)
                    && string.Equals(office.OfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase);
                var blip = CreateStaticBlip(
                    _getGroundPosition(office.MarkerPosition),
                    BlipSprite.Office,
                    isActive ? BlipColor.Green : BlipColor.White,
                    ResolveOfficeBlipName(office, isActive),
                    isActive ? 1.0f : 0.9f);
                if (blip != null && blip.Exists())
                {
                    _officeBlips.Add(blip);
                }
            }
        }

        private void CreateApartmentBlips()
        {
            var apartments = ResolveApartmentDefinitions();
            var activeApartmentId = ResolveActiveApartmentId();
            for (int i = 0; i < apartments.Count; i++)
            {
                var apartment = apartments[i];
                var isActive = !string.IsNullOrWhiteSpace(activeApartmentId)
                    && string.Equals(apartment.InteriorId, activeApartmentId, StringComparison.OrdinalIgnoreCase);
                var blip = CreateStaticBlip(
                    _getGroundPosition(apartment.ExteriorPosition),
                    BlipSprite.Safehouse,
                    isActive ? BlipColor.Blue : BlipColor.White,
                    ResolveApartmentBlipName(apartment, isActive),
                    isActive ? 0.95f : 0.85f);
                if (blip != null && blip.Exists())
                {
                    _apartmentBlips.Add(blip);
                }
            }
        }

        private void CreateDealershipBlips()
        {
            _commercialDealershipBlip = CreateStaticBlip(
                _getGroundPosition(_commercialDealershipMarker),
                BlipSprite.Truck,
                BlipColor.Blue,
                "Commercial Dealership",
                0.95f);
            _personalDealershipBlip = CreateStaticBlip(
                _getGroundPosition(_personalDealershipMarker),
                BlipSprite.PersonalVehicleCar,
                BlipColor.Yellow,
                "Personal Vehicle Dealership",
                0.95f);
        }

        private IReadOnlyList<OfficeDefinition> ResolveOfficeDefinitions()
        {
            var offices = _getOfficeDefinitions != null ? _getOfficeDefinitions() : null;
            return offices != null
                ? offices.Where(office => office != null).ToList()
                : new OfficeDefinition[0];
        }

        private string ResolveActiveOfficeId()
        {
            return _getActiveOfficeId != null
                ? _getActiveOfficeId() ?? string.Empty
                : string.Empty;
        }

        private IReadOnlyList<InteriorDefinition> ResolveApartmentDefinitions()
        {
            var apartments = _getApartmentDefinitions != null ? _getApartmentDefinitions() : null;
            return apartments != null
                ? apartments.Where(apartment => apartment != null).ToList()
                : new InteriorDefinition[0];
        }

        private string ResolveActiveApartmentId()
        {
            return _getActiveApartmentId != null
                ? _getActiveApartmentId() ?? string.Empty
                : string.Empty;
        }

        private Vector3 ResolveOfficeMarkerSeed()
        {
            return _getOfficeMarkerSeed != null
                ? _getOfficeMarkerSeed()
                : Vector3.Zero;
        }

        private Vector3 ResolveVehicleSpawnMarkerSeed()
        {
            return _getVehicleSpawnMarkerSeed != null
                ? _getVehicleSpawnMarkerSeed()
                : Vector3.Zero;
        }

        private static string ResolveOfficeBlipName(OfficeDefinition office, bool isActive)
        {
            if (office == null)
            {
                return "Logistics Office";
            }

            return isActive
                ? office.DisplayName + " [Active]"
                : office.DisplayName;
        }

        private static string ResolveApartmentBlipName(InteriorDefinition apartment, bool isActive)
        {
            if (apartment == null)
            {
                return "Apartment";
            }

            var baseName = !string.IsNullOrWhiteSpace(apartment.InteriorIgName)
                ? apartment.InteriorIgName
                : apartment.DisplayName;
            return isActive
                ? baseName + " [Active]"
                : baseName;
        }

        private static Blip CreateStaticBlip(Vector3 position, BlipSprite sprite, BlipColor color, string name, float scale)
        {
            var blip = World.CreateBlip(position);
            if (blip == null || !blip.Exists())
            {
                return null;
            }

            blip.Sprite = sprite;
            blip.Color = color;
            blip.Name = name;
            blip.Scale = scale;
            blip.IsShortRange = false;
            blip.IsHiddenOnLegend = false;
            return blip;
        }

        private static BlipSprite ResolveIndustryBlipSprite(Industry industry, bool isPetrolStation)
        {
            if (isPetrolStation)
            {
                return BlipSprite.JerryCan;
            }

            return industry != null && industry.IsStore
                ? BlipSprite.Store
                : BlipSprite.Warehouse;
        }

        private BlipColor ResolveIndustryBlipColor(Industry industry, bool isPetrolStation)
        {
            if (industry == null)
            {
                return BlipColor.White;
            }

            if (industry.IsStarterHeadquarters)
            {
                return BlipColor.Green;
            }

            if (_territoryManager == null)
            {
                if (industry.IsOwned)
                {
                    return BlipColor.Green;
                }

                return industry.HasContractorPermit
                    ? BlipColor.Yellow
                    : BlipColor.Red;
            }

            var siteState = _territoryManager.GetSiteState(industry);
            if (siteState == null)
            {
                if (industry.IsOwned)
                {
                    return BlipColor.Green;
                }

                return industry.HasContractorPermit
                    ? BlipColor.Yellow
                    : BlipColor.Red;
            }

            if (siteState.ControlLevel == TerritoryControlLevel.Owned)
            {
                return BlipColor.Green;
            }

            if (siteState.ControlLevel == TerritoryControlLevel.Leased)
            {
                return BlipColor.Blue;
            }

            return industry.HasContractorPermit
                ? BlipColor.Yellow
                : BlipColor.Red;
        }

        private string ResolveIndustryBlipName(Industry industry)
        {
            if (_territoryManager == null || industry == null)
            {
                return industry != null ? industry.Name : string.Empty;
            }

            var siteState = _territoryManager.GetSiteState(industry);
            if (siteState == null)
            {
                return industry.Name;
            }

            if (industry.IsStarterHeadquarters)
            {
                return industry.Name + " [HQ]";
            }

            if (industry.IsDepotLike || industry.SiteRole == SiteRole.FleetYard)
            {
                var controlTag = siteState.ControlLevel == TerritoryControlLevel.Owned
                    ? "YARD"
                    : (siteState.ControlLevel == TerritoryControlLevel.Leased ? "LEASE" : "OPEN");
                return string.Format("{0} [{1}]", industry.Name, controlTag);
            }

            if (siteState.FranchiseLevel != TerritoryFranchiseLevel.None)
            {
                return string.Format("{0} [F{1}]", industry.Name, (int)siteState.FranchiseLevel);
            }

            if (siteState.IsOperational)
            {
                return industry.Name + " [LIVE]";
            }

            return industry.Name + " [SETUP]";
        }
    }
}