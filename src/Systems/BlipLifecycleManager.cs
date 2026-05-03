using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Systems
{
    public sealed class BlipLifecycleManager
    {
        private readonly IndustryManager _industryManager;
        private readonly Vector3 _mainOfficeMarkerSeed;
        private readonly Vector3 _vehicleSpawnMarkerSeed;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Func<Industry, Vector3> _getIndustryMarkerPosition;
        private readonly Func<Industry, bool> _isPetrolServiceStation;
        private readonly List<Blip> _industryBlips;

        private Blip _officeBlip;
        private Blip _vehicleSpawnBlip;

        public BlipLifecycleManager(
            IndustryManager industryManager,
            Vector3 mainOfficeMarkerSeed,
            Vector3 vehicleSpawnMarkerSeed,
            Func<Vector3, Vector3> getGroundPosition,
            Func<Industry, Vector3> getIndustryMarkerPosition,
            Func<Industry, bool> isPetrolServiceStation)
        {
            _industryManager = industryManager;
            _mainOfficeMarkerSeed = mainOfficeMarkerSeed;
            _vehicleSpawnMarkerSeed = vehicleSpawnMarkerSeed;
            _getGroundPosition = getGroundPosition;
            _getIndustryMarkerPosition = getIndustryMarkerPosition;
            _isPetrolServiceStation = isPetrolServiceStation;
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
                var color = isPetrolStation
                    ? BlipColor.Yellow
                    : (industry.IsSink ? BlipColor.Yellow : BlipColor.Green);
                var blip = CreateStaticBlip(_getIndustryMarkerPosition(industry), sprite, color, industry.Name, 0.85f);
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
                blip.Position = _getIndustryMarkerPosition(industry);
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
    }
}