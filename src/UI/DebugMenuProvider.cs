using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class DebugMenuCallbacks
    {
        public Func<string> IndustryCaption { get; set; }
        public Func<string> IndustryDetail { get; set; }
        public Func<string> ResourceCaption { get; set; }
        public Func<string> ResourceDetail { get; set; }
        public Func<string> ResourceAmountCaption { get; set; }
        public Action SelectPreviousResourceAmount { get; set; }
        public Action SelectNextResourceAmount { get; set; }
        public Action SelectPreviousResource { get; set; }
        public Action SelectNextResource { get; set; }
        public Func<string> MoneyAmountCaption { get; set; }
        public Action SelectPreviousMoneyAmount { get; set; }
        public Action SelectNextMoneyAmount { get; set; }
        public Func<string> DistrictCaption { get; set; }
        public Func<string> DistrictDetail { get; set; }
        public Action SelectPreviousDistrict { get; set; }
        public Action SelectNextDistrict { get; set; }
        public Func<string> DistrictReputationAmountCaption { get; set; }
        public Action SelectPreviousDistrictReputationAmount { get; set; }
        public Action SelectNextDistrictReputationAmount { get; set; }
        public Func<string> DistrictStateCaption { get; set; }
        public Action SelectPreviousDistrictState { get; set; }
        public Action SelectNextDistrictState { get; set; }
        public Func<string> MissionBoardDetail { get; set; }
        public Action OpenMissionMenu { get; set; }
        public Func<float> SelectedMoneyAmount { get; set; }
        public Func<string> SelectedDistrictState { get; set; }
        public Func<float> SelectedDistrictReputationAmount { get; set; }
        public Action AddMoney { get; set; }
        public Action ApplyDistrictStateToAll { get; set; }
        public Action IncreaseDistrictReputation { get; set; }
        public Action DecreaseDistrictReputation { get; set; }
        public Action AddSelectedResourceToNearbyIndustry { get; set; }
        public Action DeleteResolvedVehicleCargo { get; set; }
        public Action EmptyResolvedVehicleFuelTank { get; set; }
        public Action FillResolvedVehicleFuelTank { get; set; }
        public Action DeleteCurrentVehicle { get; set; }
        public Action FillNearbyIndustryInputs { get; set; }
        public Action EmptyNearbyIndustryInputs { get; set; }
        public Action FillNearbyIndustryOutputs { get; set; }
        public Action EmptyNearbyIndustryOutputs { get; set; }
        public Action MultiplyNearbyIndustryProductionRate { get; set; }
        public Action CloseMenu { get; set; }
    }

    internal sealed class DebugMissionMenuCallbacks
    {
        public IEnumerable<SpecialMissionDefinition> Definitions { get; set; }
        public IEnumerable<SpecialMissionListing> Listings { get; set; }
        public Func<SpecialMissionDefinition, SpecialMissionListing, string> MissionCaptionFactory { get; set; }
        public Func<SpecialMissionDefinition, SpecialMissionListing, string> MissionDetailFactory { get; set; }
        public Action<string> TriggerMission { get; set; }
        public Action ReturnToDebugMenu { get; set; }
    }

    internal sealed class DebugMenuProvider
    {
        public void PopulateRootMenu(LemonMenu menu, DebugMenuCallbacks callbacks)
        {
            if (menu == null)
            {
                throw new ArgumentNullException("menu");
            }

            if (callbacks == null)
            {
                throw new ArgumentNullException("callbacks");
            }

            menu.Title = "Debug";
            menu.Subtitle = "ALT + W (Debugger attached)";

            menu.SetItems(BuildRootItems(callbacks));
        }

        internal IReadOnlyList<MenuItem> BuildRootItems(DebugMenuCallbacks callbacks)
        {
            if (callbacks == null)
            {
                throw new ArgumentNullException("callbacks");
            }

            return new[]
            {
                new MenuItem
                {
                    CaptionFactory = callbacks.IndustryCaption,
                    DetailFactory = callbacks.IndustryDetail,
                },
                new MenuItem
                {
                    CaptionFactory = callbacks.ResourceCaption,
                    DetailFactory = callbacks.ResourceDetail,
                    OnLeft = callbacks.SelectPreviousResource,
                    OnRight = callbacks.SelectNextResource,
                },
                new MenuItem
                {
                    CaptionFactory = callbacks.ResourceAmountCaption,
                    DetailFactory = () => "Used by the add-resource action.",
                    OnLeft = callbacks.SelectPreviousResourceAmount,
                    OnRight = callbacks.SelectNextResourceAmount,
                },
                new MenuItem
                {
                    CaptionFactory = callbacks.MoneyAmountCaption,
                    DetailFactory = () => "Used by the add-money action.",
                    OnLeft = callbacks.SelectPreviousMoneyAmount,
                    OnRight = callbacks.SelectNextMoneyAmount,
                },
                new MenuItem
                {
                    CaptionFactory = callbacks.DistrictCaption,
                    DetailFactory = callbacks.DistrictDetail,
                    OnLeft = callbacks.SelectPreviousDistrict,
                    OnRight = callbacks.SelectNextDistrict,
                },
                new MenuItem
                {
                    CaptionFactory = callbacks.DistrictReputationAmountCaption,
                    DetailFactory = () => "Used by the district reputation debug actions.",
                    OnLeft = callbacks.SelectPreviousDistrictReputationAmount,
                    OnRight = callbacks.SelectNextDistrictReputationAmount,
                },
                new MenuItem
                {
                    CaptionFactory = callbacks.DistrictStateCaption,
                    DetailFactory = () => "Used by the apply-all-district-state action.",
                    OnLeft = callbacks.SelectPreviousDistrictState,
                    OnRight = callbacks.SelectNextDistrictState,
                },
                new MenuItem
                {
                    IsSeparator = true,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Trigger missions",
                    DetailFactory = callbacks.MissionBoardDetail,
                    OnActivate = callbacks.OpenMissionMenu,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Add money",
                    DetailFactory = () => string.Format("Adds {0} to your current balance.", ModFormatting.FormatMoney(callbacks.SelectedMoneyAmount())),
                    OnActivate = callbacks.AddMoney,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Apply district state to all",
                    DetailFactory = () => string.Format("Forces all districts to show {0} using debug offsets.", callbacks.SelectedDistrictState()),
                    OnActivate = callbacks.ApplyDistrictStateToAll,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Increase district reputation",
                    DetailFactory = () => string.Format("Adds {0} reputation score to the selected district.", ModFormatting.FormatSignedNumber(callbacks.SelectedDistrictReputationAmount())),
                    OnActivate = callbacks.IncreaseDistrictReputation,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Decrease district reputation",
                    DetailFactory = () => string.Format("Applies {0} reputation score to the selected district.", ModFormatting.FormatSignedNumber(-callbacks.SelectedDistrictReputationAmount())),
                    OnActivate = callbacks.DecreaseDistrictReputation,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Add selected resource",
                    DetailFactory = () => "Adds the selected tonnage to the highlighted nearby industry resource.",
                    OnActivate = callbacks.AddSelectedResourceToNearbyIndustry,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Delete vehicle cargo",
                    DetailFactory = () => "Clears cargo and visuals from your current or nearest cargo vehicle.",
                    OnActivate = callbacks.DeleteResolvedVehicleCargo,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Empty fuel tank",
                    DetailFactory = () => "Sets the resolved powered vehicle fuel to 0L and applies the out-of-fuel state.",
                    OnActivate = callbacks.EmptyResolvedVehicleFuelTank,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Fill fuel tank",
                    DetailFactory = () => "Refills the resolved powered vehicle to max capacity and restores drivability.",
                    OnActivate = callbacks.FillResolvedVehicleFuelTank,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Delete current vehicle",
                    DetailFactory = () => "Deletes your current vehicle and its attached trailer if present.",
                    OnActivate = callbacks.DeleteCurrentVehicle,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Fill all inputs",
                    DetailFactory = () => "Fills every accepted input buffer for the nearby industry.",
                    OnActivate = callbacks.FillNearbyIndustryInputs,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Empty all inputs",
                    DetailFactory = () => "Clears every accepted input buffer for the nearby industry.",
                    OnActivate = callbacks.EmptyNearbyIndustryInputs,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Fill all outputs",
                    DetailFactory = () => "Fills every output buffer for the nearby industry.",
                    OnActivate = callbacks.FillNearbyIndustryOutputs,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Empty all outputs",
                    DetailFactory = () => "Clears every output buffer for the nearby industry.",
                    OnActivate = callbacks.EmptyNearbyIndustryOutputs,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Boost production x1000",
                    DetailFactory = () => "Multiplies the nearby industry's production rate by 1000.",
                    OnActivate = callbacks.MultiplyNearbyIndustryProductionRate,
                },
                new MenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = callbacks.CloseMenu,
                },
            };
        }

        public void PopulateMissionMenu(LemonMenu menu, DebugMissionMenuCallbacks callbacks)
        {
            if (menu == null)
            {
                throw new ArgumentNullException("menu");
            }

            if (callbacks == null)
            {
                throw new ArgumentNullException("callbacks");
            }

            menu.Title = "Trigger Missions";
            menu.Subtitle = "Force-start loaded community contracts";

            var items = new List<MenuItem>();
            var listingsById = (callbacks.Listings ?? Enumerable.Empty<SpecialMissionListing>())
                .Where(listing => listing != null && !string.IsNullOrWhiteSpace(listing.MissionId))
                .ToDictionary(listing => listing.MissionId, StringComparer.OrdinalIgnoreCase);
            var hasDefinitions = false;

            foreach (var definition in callbacks.Definitions ?? Enumerable.Empty<SpecialMissionDefinition>())
            {
                if (definition == null)
                {
                    continue;
                }

                hasDefinitions = true;
                SpecialMissionListing listing;
                listingsById.TryGetValue(definition.Id, out listing);
                var capturedDefinition = definition;
                var capturedListing = listing;

                items.Add(new MenuItem
                {
                    CaptionFactory = () => callbacks.MissionCaptionFactory(capturedDefinition, capturedListing),
                    DetailFactory = () => callbacks.MissionDetailFactory(capturedDefinition, capturedListing),
                    OnActivate = () => callbacks.TriggerMission(capturedDefinition.Id),
                });
            }

            if (!hasDefinitions)
            {
                items.Add(new MenuItem
                {
                    CaptionFactory = () => "No custom missions loaded",
                    DetailFactory = () => string.Format("Add XML mission packs to {0} and reload the mod.", RuntimeLayoutResolver.PreferredMissionDirectoryDisplayPath),
                });
            }

            items.Add(new MenuItem
            {
                CaptionFactory = () => "Back",
                OnActivate = callbacks.ReturnToDebugMenu,
            });

            menu.SetItems(items);
        }
    }
}