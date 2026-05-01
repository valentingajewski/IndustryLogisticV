using System;
using System.Collections.Generic;
using System.Linq;
using IndustryLogisticV.Config;
using IndustryLogisticV.Domain;

namespace IndustryLogisticV.Systems
{
    public static class RecipeRegistry
    {
        private static readonly Dictionary<string, RecipeTemplate[]> RegisteredRecipes = new Dictionary<string, RecipeTemplate[]>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "RecyclingCenter",
                new[]
                {
                    new RecipeTemplate(
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Recyclable", 1f },
                        },
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Metal", 0.25f },
                            { "Plastic", 0.25f },
                            { "Alloy", 0.25f },
                        }),
                }
            },
            {
                "OmegaFactory",
                new[]
                {
                    new RecipeTemplate(
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Coal", 2f },
                            { "Ore", 2f },
                        },
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Omega", 1f },
                        }),
                }
            },
            {
                "SmeltingFactory",
                new[]
                {
                    new RecipeTemplate(
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Ore", 2f },
                            { "Coal", 1f },
                        },
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Metal", 1f },
                            { "Alloy", 1f },
                        }),
                }
            },
            {
                "Refinery",
                new[]
                {
                    new RecipeTemplate(
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Oil", 2f },
                        },
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Plastic", 1f },
                            { "Fuel", 1f },
                        }),
                }
            },
            {
                "ProcessorFactory",
                new[]
                {
                    new RecipeTemplate(
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Plastic", 1f },
                            { "Alloy", 1f },
                        },
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Electronic", 1f },
                        }),
                }
            },
            {
                "ConsumerElectronicsFactory",
                new[]
                {
                    new RecipeTemplate(
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "Electronic", 2f },
                        },
                        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "TV", 1f },
                            { "Computer", 1f },
                            { "Recyclable", 0.25f },
                        }),
                }
            },
        };

        public static List<ProductionRecipe> BuildRecipes(IndustryConfig config, bool supportsOmegaBoost)
        {
            var recipes = new List<ProductionRecipe>();
            if (config == null)
            {
                return recipes;
            }

            var id = config.Id ?? string.Empty;
            RecipeTemplate[] templates;
            if (RegisteredRecipes.TryGetValue(id, out templates))
            {
                for (int i = 0; i < templates.Length; i++)
                {
                    recipes.Add(templates[i].CreateRecipe());
                }

                return recipes;
            }

            if (config.Outputs.Count == 0)
            {
                return recipes;
            }

            var effectiveInputs = config.Inputs
                .Where(x => !supportsOmegaBoost || !x.Equals("Omega", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (effectiveInputs.Count == 0)
            {
                var outputOnly = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                foreach (var output in config.Outputs)
                {
                    outputOnly[output] = 1f;
                }

                recipes.Add(new ProductionRecipe(
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    outputOnly));
                return recipes;
            }

            var genericInputs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < effectiveInputs.Count; i++)
            {
                genericInputs[effectiveInputs[i]] = 1f;
            }

            var genericOutputs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var output in config.Outputs)
            {
                genericOutputs[output] = 1f;
            }

            recipes.Add(new ProductionRecipe(genericInputs, genericOutputs));
            return recipes;
        }

        private sealed class RecipeTemplate
        {
            public RecipeTemplate(Dictionary<string, float> inputsTons, Dictionary<string, float> outputsTons)
            {
                InputsTons = inputsTons;
                OutputsTons = outputsTons;
            }

            private Dictionary<string, float> InputsTons { get; }
            private Dictionary<string, float> OutputsTons { get; }

            public ProductionRecipe CreateRecipe()
            {
                return new ProductionRecipe(
                    new Dictionary<string, float>(InputsTons, StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, float>(OutputsTons, StringComparer.OrdinalIgnoreCase));
            }
        }
    }
}