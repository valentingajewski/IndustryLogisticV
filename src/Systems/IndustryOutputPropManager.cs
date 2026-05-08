using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class IndustryOutputPropManager
    {
        private const float DefaultActivationRange = 250f;
        private const float StockThresholdTons = 0.001f;
        private const float SlotPadding = 0.05f;

        private readonly IReadOnlyList<Industry> _industries;
        private readonly Dictionary<string, List<Prop>> _propsByIndustryId;
        private readonly float _activationRange;

        private Vector3 _lastPlayerPosition;
        private bool _hasPlayerPosition;

        public IndustryOutputPropManager(IReadOnlyList<Industry> industries, float activationRange = DefaultActivationRange)
        {
            _industries = industries;
            _activationRange = Math.Max(50f, activationRange);
            _propsByIndustryId = new Dictionary<string, List<Prop>>(StringComparer.OrdinalIgnoreCase);
        }

        public void Update(Vector3 playerPosition)
        {
            _lastPlayerPosition = playerPosition;
            _hasPlayerPosition = true;

            if (_industries == null)
            {
                DestroyAll();
                return;
            }

            for (int i = 0; i < _industries.Count; i++)
            {
                ReconcileIndustry(_industries[i], true);
            }
        }

        public void RefreshIndustry(Industry industry)
        {
            ReconcileIndustry(industry, _hasPlayerPosition);
        }

        public void DestroyAll()
        {
            foreach (var pair in _propsByIndustryId)
            {
                DestroyProps(pair.Value);
            }

            _propsByIndustryId.Clear();
        }

        private void ReconcileIndustry(Industry industry, bool enforceRange)
        {
            var industryKey = GetIndustryKey(industry);
            if (string.IsNullOrWhiteSpace(industryKey))
            {
                return;
            }

            if (!ShouldDisplayIndustryOutput(industry) || (enforceRange && !IsWithinActivationRange(industry)))
            {
                DestroyIndustry(industryKey);
                return;
            }

            var desiredCount = CalculateDesiredPropCount(industry);
            if (desiredCount <= 0)
            {
                DestroyIndustry(industryKey);
                return;
            }

            List<Prop> props;
            if (!_propsByIndustryId.TryGetValue(industryKey, out props))
            {
                props = new List<Prop>();
                _propsByIndustryId[industryKey] = props;
            }

            if (ContainsInvalidProp(props))
            {
                DestroyIndustry(industryKey);
                props = new List<Prop>();
                _propsByIndustryId[industryKey] = props;
            }

            while (props.Count > desiredCount)
            {
                var lastIndex = props.Count - 1;
                DeleteProp(props[lastIndex]);
                props.RemoveAt(lastIndex);
            }

            if (props.Count >= desiredCount)
            {
                return;
            }

            Model model;
            Vector3 anchor;
            Vector3 modelMin;
            Vector3 modelMax;
            Vector3 right;
            Vector3 backward;
            float slotSpacingX;
            float slotSpacingY;
            float heading;
            int maxLineCount;
            if (!TryPreparePlacement(industry, out model, out anchor, out modelMin, out modelMax, out right, out backward, out slotSpacingX, out slotSpacingY, out heading, out maxLineCount))
            {
                DestroyIndustry(industryKey);
                return;
            }

            try
            {
                for (int slotIndex = props.Count; slotIndex < desiredCount; slotIndex++)
                {
                    var prop = SpawnProp(model, anchor, modelMin, modelMax, right, backward, slotSpacingX, slotSpacingY, heading, slotIndex, maxLineCount, industry.DisplayObjectsAtGroundLevel);
                    if (prop == null || !prop.Exists())
                    {
                        continue;
                    }

                    props.Add(prop);
                }
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
            }

            if (props.Count == 0)
            {
                _propsByIndustryId.Remove(industryKey);
            }
        }

        private bool ShouldDisplayIndustryOutput(Industry industry)
        {
            return industry != null
                && industry.SpawnedVehiclePosition.HasValue
                && industry.SpawnedVehiclePosition.Value != Vector3.Zero
                && industry.DisplayObjectModelHash.HasValue
                && industry.DisplayObjectModelHash.Value > 0
                && industry.MaxDisplayObjectLine.HasValue
                && industry.MaxDisplayObjectLine.Value > 0
                && industry.MaxDisplayObjectRow.HasValue
                && industry.MaxDisplayObjectRow.Value > 0
                && industry.Outputs != null
                && industry.Outputs.Count > 0
                && !industry.IsSink
                && industry.OutputCapacityTons > StockThresholdTons;
        }

        private bool IsWithinActivationRange(Industry industry)
        {
            if (!_hasPlayerPosition || industry == null || !industry.SpawnedVehiclePosition.HasValue)
            {
                return true;
            }

            return industry.SpawnedVehiclePosition.Value.DistanceTo(_lastPlayerPosition) <= _activationRange;
        }

        private int CalculateDesiredPropCount(Industry industry)
        {
            if (industry == null)
            {
                return 0;
            }

            var currentOutput = Math.Max(0f, industry.GetOutputStockTotal());
            var maxOutput = Math.Max(0f, industry.OutputCapacityTons);
            var maxProps = Math.Max(0, (industry.MaxDisplayObjectLine ?? 0) * (industry.MaxDisplayObjectRow ?? 0));
            if (currentOutput <= StockThresholdTons || maxOutput <= StockThresholdTons || maxProps <= 0)
            {
                return 0;
            }

            if (currentOutput >= maxOutput)
            {
                return maxProps;
            }

            var fillRatio = Math.Max(0f, Math.Min(1f, currentOutput / maxOutput));
            if (fillRatio <= 0f)
            {
                return 0;
            }

            return Math.Max(1, Math.Min(maxProps, (int)Math.Ceiling(fillRatio * maxProps)));
        }

        private bool TryPreparePlacement(
            Industry industry,
            out Model model,
            out Vector3 anchor,
            out Vector3 modelMin,
            out Vector3 modelMax,
            out Vector3 right,
            out Vector3 backward,
            out float slotSpacingX,
            out float slotSpacingY,
            out float heading,
            out int maxLineCount)
        {
            model = new Model(industry.DisplayObjectModelHash ?? 0);
            anchor = industry != null && industry.SpawnedVehiclePosition.HasValue ? industry.SpawnedVehiclePosition.Value : Vector3.Zero;
            modelMin = Vector3.Zero;
            modelMax = Vector3.Zero;
            right = Vector3.Zero;
            backward = Vector3.Zero;
            slotSpacingX = 0f;
            slotSpacingY = 0f;
            heading = industry != null && industry.SpawnedVehicleHeading.HasValue ? industry.SpawnedVehicleHeading.Value : 0f;
            maxLineCount = industry != null ? Math.Max(1, industry.MaxDisplayObjectLine ?? 1) : 1;

            if (industry == null || !TryRequestModel(model, 500))
            {
                return false;
            }

            model.GetDimensions(out modelMin, out modelMax);
            var sizeX = Math.Max(0.1f, modelMax.X - modelMin.X);
            var sizeY = Math.Max(0.1f, modelMax.Y - modelMin.Y);
            slotSpacingX = sizeX + SlotPadding;
            slotSpacingY = sizeY + SlotPadding;

            var forward = HeadingToDirection(heading);
            right = new Vector3(forward.Y, -forward.X, 0f);
            backward = new Vector3(-forward.X, -forward.Y, 0f);
            return true;
        }

        private Prop SpawnProp(
            Model model,
            Vector3 anchor,
            Vector3 modelMin,
            Vector3 modelMax,
            Vector3 right,
            Vector3 backward,
            float slotSpacingX,
            float slotSpacingY,
            float heading,
            int slotIndex,
            int maxLineCount,
            bool placeOnGround)
        {
            var column = slotIndex % maxLineCount;
            var row = slotIndex / maxLineCount;
            var centerOffsetX = (modelMin.X + modelMax.X) * 0.5f;
            var centerOffsetY = (modelMin.Y + modelMax.Y) * 0.5f;
            var basePosition = anchor
                + (right * ((column * slotSpacingX) - centerOffsetX))
                + (backward * ((row * slotSpacingY) - centerOffsetY))
                + new Vector3(0f, 0f, -modelMin.Z);

            if (placeOnGround)
            {
                basePosition += new Vector3(0f, 0f, Math.Max(1f, (modelMax.Z - modelMin.Z) + 0.5f));
            }

            var prop = World.CreateProp(model, basePosition, true, false);
            if (prop == null || !prop.Exists())
            {
                return null;
            }

            prop.IsPersistent = true;
            prop.Heading = heading;

            if (placeOnGround)
            {
                TryPlacePropOnGround(prop);
                prop.Heading = heading;
            }

            Function.Call(Hash.FREEZE_ENTITY_POSITION, prop.Handle, true);
            return prop;
        }

        private static void TryPlacePropOnGround(Prop prop)
        {
            if (prop == null || !prop.Exists())
            {
                return;
            }

            try
            {
                Function.Call<bool>(Hash.PLACE_OBJECT_ON_GROUND_PROPERLY, prop.Handle);
            }
            catch
            {
                // Ground snap is optional; leave the prop at its configured Z if the native fails.
            }
        }

        private static bool ContainsInvalidProp(List<Prop> props)
        {
            if (props == null)
            {
                return false;
            }

            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                if (prop == null || !prop.Exists())
                {
                    return true;
                }
            }

            return false;
        }

        private void DestroyIndustry(string industryKey)
        {
            if (string.IsNullOrWhiteSpace(industryKey))
            {
                return;
            }

            List<Prop> props;
            if (!_propsByIndustryId.TryGetValue(industryKey, out props))
            {
                return;
            }

            DestroyProps(props);
            _propsByIndustryId.Remove(industryKey);
        }

        private static void DestroyProps(List<Prop> props)
        {
            if (props == null)
            {
                return;
            }

            for (int i = 0; i < props.Count; i++)
            {
                DeleteProp(props[i]);
            }

            props.Clear();
        }

        private static void DeleteProp(Prop prop)
        {
            if (prop == null)
            {
                return;
            }

            try
            {
                if (!prop.Exists())
                {
                    return;
                }

                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, prop.Handle, true, true);
                prop.Delete();

                if (prop.Exists())
                {
                    prop.IsVisible = false;
                    var position = prop.Position;
                    prop.Position = new Vector3(position.X, position.Y, position.Z - 250f);
                    prop.Delete();
                }
            }
            catch
            {
                // Keep cleanup resilient: one bad prop handle must not block deleting remaining props.
            }
        }

        private static string GetIndustryKey(Industry industry)
        {
            if (industry == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(industry.Id))
            {
                return industry.Id;
            }

            if (!string.IsNullOrWhiteSpace(industry.LegacyKey))
            {
                return industry.LegacyKey;
            }

            return industry.Name ?? string.Empty;
        }

        private static bool TryRequestModel(Model model, int timeoutMs)
        {
            if (!model.IsInCdImage || !model.IsValid)
            {
                return false;
            }

            return model.Request(timeoutMs);
        }

        private static Vector3 HeadingToDirection(float heading)
        {
            var radians = heading * (float)Math.PI / 180f;
            return new Vector3((float)-Math.Sin(radians), (float)Math.Cos(radians), 0f);
        }
    }
}