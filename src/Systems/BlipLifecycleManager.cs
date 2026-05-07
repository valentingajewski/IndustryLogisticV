using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class BlipLifecycleManager
    {
        private readonly IndustryManager _industryManager;
        private readonly Vector3 _mainOfficeMarkerSeed;
        private readonly Vector3 _vehicleSpawnMarkerSeed;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Func<Industry, Vector3> _getIndustryMarkerPosition;
        private readonly Func<Industry, bool> _isPetrolServiceStation;
        private readonly TerritoryManager _territoryManager;
        private readonly List<Blip> _industryBlips;

        private Blip _officeBlip;
        private Blip _vehicleSpawnBlip;

        public BlipLifecycleManager(
            IndustryManager industryManager,
            Vector3 mainOfficeMarkerSeed,
            Vector3 vehicleSpawnMarkerSeed,
            Func<Vector3, Vector3> getGroundPosition,
            Func<Industry, Vector3> getIndustryMarkerPosition,
            Func<Industry, bool> isPetrolServiceStation,
            TerritoryManager territoryManager = null)
        {
            _industryManager = industryManager;
            _mainOfficeMarkerSeed = mainOfficeMarkerSeed;
            _vehicleSpawnMarkerSeed = vehicleSpawnMarkerSeed;
            _getGroundPosition = getGroundPosition;
            _getIndustryMarkerPosition = getIndustryMarkerPosition;
            _isPetrolServiceStation = isPetrolServiceStation;
            _territoryManager = territoryManager;
            _industryBlips = new List<Blip>();
        }

        public void Create()
        {
            Destroy();

            _officeBlip = CreateStaticBlip(_getGroundPosition(_mainOfficeMarkerSeed), BlipSprite.Office, BlipColor.Blue, "Logistics Office", 1.0f);
            _vehicleSpawnBlip = CreateStaticBlip(_getGroundPosition(_vehicleSpawnMarkerSeed), BlipSprite.Garage2, BlipColor.White, "Vehicle Spawn", 0.9f);

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
            if (_officeBlip != null && _officeBlip.Exists())
            {
                _officeBlip.Position = _getGroundPosition(_mainOfficeMarkerSeed);
            }

            if (_vehicleSpawnBlip != null && _vehicleSpawnBlip.Exists())
            {
                _vehicleSpawnBlip.Position = _getGroundPosition(_vehicleSpawnMarkerSeed);
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
            if (_officeBlip != null && _officeBlip.Exists())
            {
                _officeBlip.Delete();
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

            _industryBlips.Clear();
            _officeBlip = null;
            _vehicleSpawnBlip = null;
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
            if (_territoryManager == null || industry == null)
            {
                return isPetrolStation
                    ? BlipColor.Yellow
                    : (industry != null && industry.IsSink ? BlipColor.Yellow : BlipColor.Green);
            }

            var siteState = _territoryManager.GetSiteState(industry);
            var districtState = _territoryManager.GetDistrictState(industry.DistrictName);
            if (siteState == null)
            {
                return isPetrolStation
                    ? BlipColor.Yellow
                    : (industry.IsSink ? BlipColor.Yellow : BlipColor.Green);
            }

            if (industry.IsStarterHeadquarters || (industry.IsDepotLike && siteState.ControlLevel == TerritoryControlLevel.Owned))
            {
                return BlipColor.Blue;
            }

            if (industry.IsDepotLike && siteState.ControlLevel == TerritoryControlLevel.Leased)
            {
                return BlipColor.White;
            }

            if (siteState.IsOperational && districtState != null && districtState.InfluenceRatio >= 0.6f)
            {
                return BlipColor.Green;
            }

            if (siteState.FranchiseLevel >= TerritoryFranchiseLevel.Preferred || isPetrolStation || industry.IsStore)
            {
                return BlipColor.Yellow;
            }

            return BlipColor.Red;
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