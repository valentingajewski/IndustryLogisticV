using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.Domain
{
    public sealed class CommodityEconomySemantics
    {
        public CommodityEconomySemantics()
        {
            DemandClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Substitutes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            SinkElasticity = -1f;
            ScarcitySensitivity = -1f;
            SinkPreferenceWeight = -1f;
            EventResponseAffinity = -1f;
            Volatility = -1f;
            Perishability = -1f;
        }

        public string SubstituteFamily { get; set; }

        public HashSet<string> DemandClasses { get; set; }

        public Dictionary<string, float> Substitutes { get; set; }

        public float SinkElasticity { get; set; }

        public float ScarcitySensitivity { get; set; }

        public float SinkPreferenceWeight { get; set; }

        public float EventResponseAffinity { get; set; }

        public float Volatility { get; set; }

        public float Perishability { get; set; }

        public CommodityEconomySemantics Clone()
        {
            return new CommodityEconomySemantics
            {
                SubstituteFamily = SubstituteFamily,
                DemandClasses = new HashSet<string>(DemandClasses ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                Substitutes = new Dictionary<string, float>(Substitutes ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                SinkElasticity = SinkElasticity,
                ScarcitySensitivity = ScarcitySensitivity,
                SinkPreferenceWeight = SinkPreferenceWeight,
                EventResponseAffinity = EventResponseAffinity,
                Volatility = Volatility,
                Perishability = Perishability,
            };
        }

        public bool HasConfiguredValues
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SubstituteFamily)
                    || (DemandClasses != null && DemandClasses.Count > 0)
                    || (Substitutes != null && Substitutes.Count > 0)
                    || SinkElasticity >= 0f
                    || ScarcitySensitivity >= 0f
                    || SinkPreferenceWeight >= 0f
                    || EventResponseAffinity >= 0f
                    || Volatility >= 0f
                    || Perishability >= 0f;
            }
        }

        public static CommodityEconomySemantics CreateResolved(
            float sinkElasticity,
            float scarcitySensitivity,
            float sinkPreferenceWeight,
            float eventResponseAffinity,
            float volatility,
            float perishability,
            string substituteFamily,
            IEnumerable<string> demandClasses,
            IEnumerable<KeyValuePair<string, float>> substitutes)
        {
            var result = new CommodityEconomySemantics
            {
                SinkElasticity = sinkElasticity,
                ScarcitySensitivity = scarcitySensitivity,
                SinkPreferenceWeight = sinkPreferenceWeight,
                EventResponseAffinity = eventResponseAffinity,
                Volatility = volatility,
                Perishability = perishability,
                SubstituteFamily = substituteFamily ?? string.Empty,
            };

            if (demandClasses != null)
            {
                result.DemandClasses = new HashSet<string>(
                    demandClasses.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()),
                    StringComparer.OrdinalIgnoreCase);
            }

            if (substitutes != null)
            {
                foreach (var pair in substitutes)
                {
                    var commodity = CommodityCatalog.Normalize(pair.Key);
                    if (string.IsNullOrWhiteSpace(commodity) || pair.Value <= 0f)
                    {
                        continue;
                    }

                    result.Substitutes[commodity] = pair.Value;
                }
            }

            return result;
        }
    }

    public sealed class CommodityAffinityEntry
    {
        public CommodityAffinityEntry(string commodity, float affinity)
        {
            Commodity = CommodityCatalog.Normalize(commodity);
            Affinity = Math.Max(0f, Math.Min(1f, affinity));
        }

        public string Commodity { get; private set; }

        public float Affinity { get; private set; }
    }
}