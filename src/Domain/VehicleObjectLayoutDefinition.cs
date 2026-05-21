using System;
using System.Collections.Generic;
using GTA.Math;

namespace LSOL.Domain
{
    public enum VehicleObjectPlacementMode
    {
        Default = 0,
        Grid = 1,
        CenteredSingle = 2,
    }

    public sealed class VehicleObjectLayoutDefinition
    {
        public VehicleObjectLayoutDefinition()
        {
            CommodityOverrides = new List<VehicleCommodityObjectLayoutDefinition>();
            LooseCargoVisuals = new List<VehicleLooseCargoVisualDefinition>();
        }

        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public string ObjectKey { get; set; }
        public Vector3 CenterOffset { get; set; }
        public int MaxLine { get; set; }
        public int MaxRow { get; set; }
        public bool IsEnabled { get; set; }
        public VehicleObjectPlacementMode PlacementMode { get; set; }
        public List<VehicleCommodityObjectLayoutDefinition> CommodityOverrides { get; private set; }
        public List<VehicleLooseCargoVisualDefinition> LooseCargoVisuals { get; private set; }

        public bool HasUsableGrid
        {
            get { return MaxLine > 0 && MaxRow > 0; }
        }

        public VehicleCommodityObjectLayoutDefinition FindCommodityOverride(string commodity)
        {
            if (CommodityOverrides == null || CommodityOverrides.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < CommodityOverrides.Count; i++)
            {
                var candidate = CommodityOverrides[i];
                if (candidate != null && candidate.MatchesCommodity(commodity))
                {
                    return candidate;
                }
            }

            return null;
        }

        public VehicleLooseCargoVisualDefinition FindLooseCargoVisual(string commodity, VehicleCargoType cargoType)
        {
            if (LooseCargoVisuals == null || LooseCargoVisuals.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < LooseCargoVisuals.Count; i++)
            {
                var candidate = LooseCargoVisuals[i];
                if (candidate != null && candidate.MatchesCommodity(commodity, cargoType))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    public sealed class VehicleCommodityObjectLayoutDefinition
    {
        public string Commodity { get; set; }
        public string ObjectKey { get; set; }
        public Vector3? CenterOffset { get; set; }
        public int? MaxLine { get; set; }
        public int? MaxRow { get; set; }
        public VehicleObjectPlacementMode PlacementMode { get; set; }

        public bool MatchesCommodity(string commodity)
        {
            return !string.IsNullOrWhiteSpace(Commodity)
                && string.Equals(CommodityCatalog.Normalize(Commodity), CommodityCatalog.Normalize(commodity), StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class VehicleLooseCargoVisualDefinition
    {
        public VehicleLooseCargoVisualDefinition()
        {
            Commodities = new List<string>();
            MaxPropCount = 1;
            SpreadX = 0.5f;
            SpreadY = 0.35f;
            YawJitterDegrees = 12f;
            PitchJitterDegrees = 2f;
            RollJitterDegrees = 2f;
            IsEnabled = true;
        }

        public string ObjectKey { get; set; }
        public List<string> Commodities { get; private set; }
        public VehicleCargoType CargoType { get; set; }
        public Vector3? CenterOffset { get; set; }
        public int MaxPropCount { get; set; }
        public float SpreadX { get; set; }
        public float SpreadY { get; set; }
        public float YawJitterDegrees { get; set; }
        public float PitchJitterDegrees { get; set; }
        public float RollJitterDegrees { get; set; }
        public bool IsEnabled { get; set; }

        public bool MatchesCommodity(string commodity, VehicleCargoType cargoType)
        {
            if (!IsEnabled)
            {
                return false;
            }

            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            for (int i = 0; i < Commodities.Count; i++)
            {
                if (string.Equals(CommodityCatalog.Normalize(Commodities[i]), normalizedCommodity, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return CargoType != VehicleCargoType.Unknown && CargoType == cargoType;
        }
    }
}