using System;
using System.Collections.Generic;

namespace LSOL.Domain
{
    public sealed class ProductionRecipe
    {
        public ProductionRecipe(Dictionary<string, float> inputsTons, Dictionary<string, float> outputsTons)
        {
            InputsTons = inputsTons ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            OutputsTons = outputsTons ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, float> InputsTons { get; }
        public Dictionary<string, float> OutputsTons { get; }

        public float GetMaxCyclesFromInputs(Dictionary<string, float> storageTons)
        {
            if (InputsTons.Count == 0)
            {
                return float.MaxValue;
            }

            float max = float.MaxValue;
            foreach (var pair in InputsTons)
            {
                if (pair.Value <= 0f)
                {
                    continue;
                }

                float available;
                if (!storageTons.TryGetValue(pair.Key, out available))
                {
                    return 0f;
                }

                var allowed = available / pair.Value;
                if (allowed < max)
                {
                    max = allowed;
                }
            }

            return max;
        }
    }
}
