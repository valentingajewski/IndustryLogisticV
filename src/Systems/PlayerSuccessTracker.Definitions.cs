using System;
using System.Collections.Generic;

namespace LSOL.Systems
{
    internal sealed partial class PlayerSuccessTracker
    {
        private static IReadOnlyList<PlayerSuccessDefinition> BuildDefinitions()
        {
            return new List<PlayerSuccessDefinition>
            {
                CreateFloatThresholdDefinition("first_paycheck", "First Paycheck", "Earn your first $10,000.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdFirstPaycheck, BuildMoneyProgress),
                CreateFloatThresholdDefinition("small_operator", "Small Operator", "Reach $100,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdSmallOperator, BuildMoneyProgress),
                CreateFloatThresholdDefinition("millionaire_hauler", "Millionaire Hauler", "Reach $1,000,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdMillionaireHauler, BuildMoneyProgress),
                CreateFloatThresholdDefinition("logistics_tycoon", "Logistics Tycoon", "Reach $10,000,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdLogisticsTycoon, BuildMoneyProgress),
                CreateFloatThresholdDefinition("multimillion_company", "MultiMillion Company", "Reach $100,000,000 company balance.", tracker => tracker._snapshot.HighestCompanyBalanceEver, MoneyThresholdMultiMillionCompany, BuildMoneyProgress),
                CreateFloatThresholdDefinition("self_made", "Self-Made", "Accumulate $1,000,000 without recruiting any NPC.", tracker => tracker._snapshot.HighestCompanyBalanceBeforeFirstNpcHire, MoneyThresholdMillionaireHauler, BuildMoneyProgress),
                CreateIntThresholdDefinition("first_delivery", "First Delivery", "Complete your first delivery.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdFirstDelivery, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateIntThresholdDefinition("road_veteran", "Road Veteran", "Complete 100 deliveries.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdRoadVeteran, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateIntThresholdDefinition("road_legend", "Road Legend", "Complete 1,000 deliveries.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdRoadLegend, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateIntThresholdDefinition("endless_freight", "Endless Freight", "Complete 10,000 deliveries.", tracker => tracker._snapshot.TotalSuccessfulDeliveries, DeliveryThresholdEndlessFreight, (current, target) => BuildCountProgress(current, target, "deliveries")),
                CreateFloatThresholdDefinition("heavy_hauler", "Heavy Hauler", "Transport a total of 100t of resources.", tracker => tracker._snapshot.TotalTransportedTons, TonnageThresholdHeavyHauler, BuildTonnageProgress),
                CreateFloatThresholdDefinition("industrial_backbone", "Industrial Backbone", "Transport a total of 1,000t of resources.", tracker => tracker._snapshot.TotalTransportedTons, TonnageThresholdIndustrialBackbone, BuildTonnageProgress),
                CreateFloatThresholdDefinition("supply_chain_king", "Supply Chain King", "Transport a total of 10,000t of resources.", tracker => tracker._snapshot.TotalTransportedTons, TonnageThresholdSupplyChainKing, BuildTonnageProgress),
                CreateStateDefinition("first_office", "First Office", "Buy or rent your first office.", tracker => tracker.GetOfficeAccessCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOfficeAccessCount()), 1, "office"), tracker => tracker.GetOfficeAccessCount() > 0 ? 1f : 0f),
                CreateStateDefinition("company_footprint", "Company Footprint", "Own an office.", tracker => tracker.GetOwnedOfficeCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedOfficeCount()), 1, "owned office"), tracker => tracker.GetOwnedOfficeCount() > 0 ? 1f : 0f),
                CreateStateDefinition("empire", "Empire", "Own the port office.", tracker => tracker.OwnsPortOffice(), tracker => tracker.OwnsPortOffice() ? "1/1 port office owned" : "0/1 port office owned", tracker => tracker.OwnsPortOffice() ? 1f : 0f),
                CreateStateDefinition("home_base", "Home Base", "Buy or rent your first apartment.", tracker => tracker.GetApartmentAccessCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetApartmentAccessCount()), 1, "apartment"), tracker => tracker.GetApartmentAccessCount() > 0 ? 1f : 0f),
                CreateStateDefinition("fleet_owner", "Fleet Owner", "Own your first commercial vehicle.", tracker => tracker.GetOwnedCommercialVehicleCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedCommercialVehicleCount()), 1, "vehicle"), tracker => tracker.GetOwnedCommercialVehicleCount() > 0 ? 1f : 0f),
                CreateCustomDefinition("full_garage", "Full Garage", "Fill an office garage to its vehicle capacity.", tracker =>
                {
                    var fill = tracker.GetBestOwnedGarageFillState();
                    if (fill.Capacity <= 0)
                    {
                        return PlayerSuccessEvaluation.Incomplete(0f, "No owned office garage capacity.");
                    }

                    return new PlayerSuccessEvaluation(fill.Current >= fill.Capacity, fill.ProgressRatio, string.Format("{0}/{1} garage slots filled", fill.Current, fill.Capacity));
                }),
                CreateStateDefinition("company_fleet", "Company Fleet", "Own 10 commercial vehicles.", tracker => tracker.GetOwnedCommercialVehicleCount() >= FleetThresholdCompanyFleet, tracker => BuildCountProgress(tracker.GetOwnedCommercialVehicleCount(), FleetThresholdCompanyFleet, "owned vehicles"), tracker => BuildRatio(tracker.GetOwnedCommercialVehicleCount(), FleetThresholdCompanyFleet)),
                CreateStateDefinition("mechanized_empire", "Mechanized Empire", "Own a diversified fleet with at least one truck for several different cargo roles.", tracker => tracker.GetDistinctOwnedFleetRoles() >= FleetRoleThresholdMechanizedEmpire, tracker => BuildCountProgress(tracker.GetDistinctOwnedFleetRoles(), FleetRoleThresholdMechanizedEmpire, "cargo roles"), tracker => BuildRatio(tracker.GetDistinctOwnedFleetRoles(), FleetRoleThresholdMechanizedEmpire)),
                CreateStateDefinition("first_employee", "First Employee", "Recruit your first NPC driver.", tracker => tracker._snapshot.HasEverHiredNpc, tracker => tracker._snapshot.HasEverHiredNpc ? "1/1 hired NPC" : "0/1 hired NPC", tracker => tracker._snapshot.HasEverHiredNpc ? 1f : 0f),
                CreateStateDefinition("dispatcher", "Dispatcher", "Have 5 active NPCs.", tracker => tracker.GetCurrentNpcDriverCount() >= NpcThresholdDispatcher, tracker => BuildCountProgress(tracker.GetCurrentNpcDriverCount(), NpcThresholdDispatcher, "active NPCs"), tracker => BuildRatio(tracker.GetCurrentNpcDriverCount(), NpcThresholdDispatcher)),
                CreateStateDefinition("logistics_manager", "Logistics Manager", "Have multiple NPC routes running at the same time.", tracker => tracker.GetCurrentNpcRouteCount() >= ActiveNpcRoutesThreshold, tracker => BuildCountProgress(tracker.GetCurrentNpcRouteCount(), ActiveNpcRoutesThreshold, "active routes"), tracker => BuildRatio(tracker.GetCurrentNpcRouteCount(), ActiveNpcRoutesThreshold)),
                CreateFloatThresholdDefinition("hands_off_income", "Hands-Off Income", "Earn a large amount only from NPC deliveries.", tracker => tracker._snapshot.CumulativeNpcDeliveryIncome, NpcIncomeThresholdHandsOffIncome, BuildMoneyProgress),
                CreateStateDefinition("entrepreneur", "Entrepreneur", "Own your first industry.", tracker => tracker.GetOwnedActualIndustryCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedActualIndustryCount()), 1, "industry"), tracker => tracker.GetOwnedActualIndustryCount() > 0 ? 1f : 0f),
                CreateStateDefinition("industrialist", "Industrialist", "Owns 5 industries.", tracker => tracker.GetOwnedActualIndustryCount() >= IndustryThresholdIndustrialist, tracker => BuildCountProgress(tracker.GetOwnedActualIndustryCount(), IndustryThresholdIndustrialist, "industries"), tracker => BuildRatio(tracker.GetOwnedActualIndustryCount(), IndustryThresholdIndustrialist)),
                CreateStateDefinition("established_industrialist", "Established Industrialist", "Owns 15 industries", tracker => tracker.GetOwnedActualIndustryCount() >= IndustryThresholdEstablishedIndustrialist, tracker => BuildCountProgress(tracker.GetOwnedActualIndustryCount(), IndustryThresholdEstablishedIndustrialist, "industries"), tracker => BuildRatio(tracker.GetOwnedActualIndustryCount(), IndustryThresholdEstablishedIndustrialist)),
                CreateCustomDefinition("industrial_master", "Industrial Master", "Owns all industry", tracker =>
                {
                    var total = tracker.GetPurchasableActualIndustryCount();
                    if (total <= 0)
                    {
                        return PlayerSuccessEvaluation.Incomplete(0f, "No purchasable industries configured.");
                    }

                    var owned = tracker.GetOwnedActualIndustryCount();
                    return new PlayerSuccessEvaluation(owned >= total, BuildRatio(owned, total), string.Format("{0}/{1} purchasable industries", owned, total));
                }),
                CreateStateDefinition("vertical_integration", "Vertical Integration", "Own both a producer and a consumer in the same supply chain.", tracker => tracker.HasOwnedProducerConsumerMatch(), tracker => tracker.HasOwnedProducerConsumerMatch() ? "1/1 matching supply chain" : "0/1 matching supply chain", tracker => tracker.HasOwnedProducerConsumerMatch() ? 1f : 0f),
                CreateStateDefinition("supply_network", "Supply Network", "Own 1 industry in 3 districts.", tracker => tracker.GetOwnedIndustryDistrictCount() >= 3, tracker => BuildCountProgress(tracker.GetOwnedIndustryDistrictCount(), 3, "districts"), tracker => BuildRatio(tracker.GetOwnedIndustryDistrictCount(), 3)),
                CreateStateDefinition("warehouse_master", "Warehouse Master", "Buy your first warehouse.", tracker => tracker.GetOwnedWarehouseCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedWarehouseCount()), 1, "warehouse"), tracker => tracker.GetOwnedWarehouseCount() > 0 ? 1f : 0f),
                CreateStateDefinition("fuel_baron", "Fuel Baron", "Own your first gas station.", tracker => tracker.GetOwnedGasStationCount() > 0, tracker => BuildCountProgress(Math.Min(1, tracker.GetOwnedGasStationCount()), 1, "gas station"), tracker => tracker.GetOwnedGasStationCount() > 0 ? 1f : 0f),
                CreateStateDefinition("port_authority", "Port Authority", "Buy the Port Terminal.", tracker => tracker.OwnsPortTerminal(), tracker => tracker.OwnsPortTerminal() ? "1/1 Port Terminal owned" : "0/1 Port Terminal owned", tracker => tracker.OwnsPortTerminal() ? 1f : 0f),
                CreateStateDefinition("clean_run", "Clean Run", "Complete a delivery without cargo damage.", tracker => tracker._snapshot.TotalSuccessfulCleanDeliveries >= 1, tracker => BuildCountProgress(Math.Min(1, tracker._snapshot.TotalSuccessfulCleanDeliveries), 1, "clean delivery"), tracker => tracker._snapshot.TotalSuccessfulCleanDeliveries >= 1 ? 1f : 0f),
                CreateIntThresholdDefinition("perfectionist", "Perfectionist", "Complete 25 deliveries with no cargo damage.", tracker => tracker._snapshot.TotalSuccessfulCleanDeliveries, CleanDeliveryThresholdPerfectionist, (current, target) => BuildCountProgress(current, target, "clean deliveries")),
                CreateIntThresholdDefinition("emergency_services", "Emergency Services", "Use a recovery/refuel/service system 15 times.", tracker => tracker._snapshot.TotalEmergencyServiceUsages, EmergencyServiceThreshold, (current, target) => BuildCountProgress(current, target, "service uses")),
                CreateIntThresholdDefinition("special_contractor", "Special Contractor", "Complete your first special mission.", tracker => tracker._snapshot.TotalSpecialMissionsCompleted, SpecialMissionThresholdFirst, (current, target) => BuildCountProgress(current, target, "missions")),
                CreateIntThresholdDefinition("regular_contractor", "Regular Contractor", "Complete 10 special missions.", tracker => tracker._snapshot.TotalSpecialMissionsCompleted, SpecialMissionThresholdRegular, (current, target) => BuildCountProgress(current, target, "missions")),
                CreateIntThresholdDefinition("expert_contractor", "Expert Contractor", "Complete 50 special missions.", tracker => tracker._snapshot.TotalSpecialMissionsCompleted, SpecialMissionThresholdExpert, (current, target) => BuildCountProgress(current, target, "missions")),
                CreateFloatThresholdDefinition("reputation_built", "Reputation Built", "Reach a 250 reputation score in one district.", tracker => tracker.GetBestDistrictReputationScore(), ReputationThresholdBuilt, BuildReputationProgress),
                CreateStateDefinition("statewide_operator", "Statewide Operator", "Reach 100 reputation in 3 districts.", tracker => tracker.GetDistrictCountAtReputation(ReputationThresholdStatewide) >= ReputationDistrictThresholdStatewide, tracker => BuildCountProgress(tracker.GetDistrictCountAtReputation(ReputationThresholdStatewide), ReputationDistrictThresholdStatewide, "districts"), tracker => BuildRatio(tracker.GetDistrictCountAtReputation(ReputationThresholdStatewide), ReputationDistrictThresholdStatewide)),
                CreateCustomDefinition("worldwide_operator", "Worldwide Operator", "Reach dominant reputation in all districts.", tracker =>
                {
                    var total = tracker.GetTotalDistrictCount();
                    if (total <= 0)
                    {
                        return PlayerSuccessEvaluation.Incomplete(0f, "No districts configured.");
                    }

                    var dominant = tracker.GetDominantDistrictCount();
                    return new PlayerSuccessEvaluation(dominant >= total, BuildRatio(dominant, total), string.Format("{0}/{1} dominant districts", dominant, total));
                }),
                CreateIntThresholdDefinition("solo_grinder", "Solo Grinder", "Complete 100 deliveries before hiring any NPC.", tracker => tracker._snapshot.DeliveriesBeforeFirstNpcHire, DeliveryThresholdSoloGrinder, (current, target) => BuildCountProgress(current, target, "deliveries before first NPC")),
                CreateFloatThresholdDefinition("no_debt_needed", "No Debt Needed", "Reach $1,000,000 without taking a loan", tracker => tracker._snapshot.HighestCompanyBalanceBeforeFirstLoan, MoneyThresholdMillionaireHauler, BuildMoneyProgress),
                CreateFloatThresholdDefinition("freight_specialist", "Freight Specialist", "Deliver a large amount of one specific commodity type.", tracker => tracker.GetBestCommodityTonnage(), CommodityThresholdFreightSpecialist, BuildTonnageProgress),
                CreateStateDefinition("diversified_carrier", "Diversified Carrier", "Successfully transport many different resource types.", tracker => tracker.GetDistinctDeliveredCommodityCount() >= CommodityThresholdDiversifiedCarrier, tracker => BuildCountProgress(tracker.GetDistinctDeliveredCommodityCount(), CommodityThresholdDiversifiedCarrier, "resource types"), tracker => BuildRatio(tracker.GetDistinctDeliveredCommodityCount(), CommodityThresholdDiversifiedCarrier)),
                CreateStateDefinition("lsol_founder", "LSOL Founder", "Own an office, an apartment, at least one industry, a warehouse, and a working fleet at the same time.", tracker => tracker.GetFounderProgressCount() >= FounderRequirementCount, tracker => BuildCountProgress(tracker.GetFounderProgressCount(), FounderRequirementCount, "founder requirements"), tracker => BuildRatio(tracker.GetFounderProgressCount(), FounderRequirementCount)),
                CreateStateDefinition("megalomaniac", "Megalomaniac", "Complete the following Success: \"Endless Freight\", \"Self-Made\", \"Worldwide Operator\" and \"Industrial Master\"", tracker => tracker.GetMegalomaniacProgressCount() >= MegalomaniacRequirementCount, tracker => BuildCountProgress(tracker.GetMegalomaniacProgressCount(), MegalomaniacRequirementCount, "required Successes"), tracker => BuildRatio(tracker.GetMegalomaniacProgressCount(), MegalomaniacRequirementCount)),
            };
        }

        private static PlayerSuccessDefinition CreateFloatThresholdDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, float> currentValueFactory,
            float threshold,
            Func<float, float, string> progressTextFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker =>
            {
                var current = Math.Max(0f, currentValueFactory != null ? currentValueFactory(tracker) : 0f);
                return new PlayerSuccessEvaluation(current >= threshold, BuildRatio(current, threshold), progressTextFactory != null ? progressTextFactory(current, threshold) : string.Empty);
            });
        }

        private static PlayerSuccessDefinition CreateIntThresholdDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, int> currentValueFactory,
            int threshold,
            Func<int, int, string> progressTextFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker =>
            {
                var current = Math.Max(0, currentValueFactory != null ? currentValueFactory(tracker) : 0);
                return new PlayerSuccessEvaluation(current >= threshold, BuildRatio(current, threshold), progressTextFactory != null ? progressTextFactory(current, threshold) : string.Empty);
            });
        }

        private static PlayerSuccessDefinition CreateStateDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, bool> isCompleteFactory,
            Func<PlayerSuccessTracker, string> progressTextFactory,
            Func<PlayerSuccessTracker, float> progressRatioFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker =>
            {
                var isComplete = isCompleteFactory != null && isCompleteFactory(tracker);
                var progressText = progressTextFactory != null ? progressTextFactory(tracker) : string.Empty;
                var ratio = progressRatioFactory != null ? Clamp01(progressRatioFactory(tracker)) : (isComplete ? 1f : 0f);
                return new PlayerSuccessEvaluation(isComplete, ratio, progressText);
            });
        }

        private static PlayerSuccessDefinition CreateCustomDefinition(
            string id,
            string name,
            string description,
            Func<PlayerSuccessTracker, PlayerSuccessEvaluation> evaluationFactory)
        {
            return new PlayerSuccessDefinition(id, name, description, tracker => evaluationFactory != null ? evaluationFactory(tracker) : PlayerSuccessEvaluation.Incomplete(0f, string.Empty));
        }

        private sealed class PlayerSuccessDefinition
        {
            public PlayerSuccessDefinition(string id, string name, string description, Func<PlayerSuccessTracker, PlayerSuccessEvaluation> evaluate)
            {
                Id = NormalizeSuccessId(id);
                Name = name ?? string.Empty;
                Description = description ?? string.Empty;
                Evaluate = evaluate;
            }

            public string Id { get; }

            public string Name { get; }

            public string Description { get; }

            public Func<PlayerSuccessTracker, PlayerSuccessEvaluation> Evaluate { get; }
        }

        private sealed class PlayerSuccessEvaluation
        {
            public PlayerSuccessEvaluation(bool isComplete, float progressRatio, string progressText)
            {
                IsComplete = isComplete;
                ProgressRatio = progressRatio;
                ProgressText = progressText ?? string.Empty;
            }

            public bool IsComplete { get; }

            public float ProgressRatio { get; }

            public string ProgressText { get; }

            public static PlayerSuccessEvaluation Incomplete(float progressRatio, string progressText)
            {
                return new PlayerSuccessEvaluation(false, progressRatio, progressText);
            }
        }
    }
}