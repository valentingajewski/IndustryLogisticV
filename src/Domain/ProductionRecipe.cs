using System;
using System.Collections.Generic;

namespace LSOL.Domain
{
    public sealed class ProductionRecipe
    {
        public ProductionRecipe(
            Dictionary<string, float> inputsTons,
            Dictionary<string, float> outputsTons,
            string id = null,
            string displayName = null,
            int selectionPriority = 0,
            Dictionary<string, float> optionalInputWeights = null,
            HashSet<string> optionalInputs = null,
            HashSet<string> boostInputs = null)
        {
            InputsTons = inputsTons ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            OutputsTons = outputsTons ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Id = id;
            DisplayName = displayName;
            SelectionPriority = Math.Max(0, selectionPriority);
            OptionalInputWeights = optionalInputWeights ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            OptionalInputs = optionalInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            BoostInputs = boostInputs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int SelectionPriority { get; }

        public Dictionary<string, float> InputsTons { get; }
        public Dictionary<string, float> OutputsTons { get; }

        public Dictionary<string, float> OptionalInputWeights { get; }

        public HashSet<string> OptionalInputs { get; }

        public HashSet<string> BoostInputs { get; }

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
