# LSOL Repository Manifest

Tracked repository files indexed for LLM ingestion. Hidden gameplay or covert systems are summarized only at a high level.

## Root

- .gitignore: Git ignore rules for local outputs, caches, and runtime artifacts.
- ~$Ideas_Fix.xlsx: Temporary Office lock sidecar for Ideas_Fix.xlsx.
- dockhandlerMission_infos.txt: Design notes for the dock container-handler mission.
- ECONOMY_REBALANCE_2026-05-17.md: Economy rebalance notes for the 2026-05-17 tuning pass.
- gtav_industry_mgnt.sln: Visual Studio solution that hosts the LSOL codebase.
- Ideas_Fix.xlsx: Working spreadsheet of feature ideas, fixes, and balancing notes.
- LSOL.csproj: Main .NET Framework project file for the LSOL gameplay mod.
- LSOL.csproj.lscache: Language-service cache generated for the main project file.
- LSOL.ini: Runtime INI that bootstraps core LSOL settings.
- LSOL_ADDON_ECOSYSTEM_ANALYSIS.md: Analysis of the addon ecosystem, packaging model, and extension surface.
- LSOL_files.rar: Archived LSOL export bundle kept as a packaged snapshot.

## configs

- configs/apartments.xml: Legacy XML apartment catalog retained for compatibility or migration.
- configs/Banks.csv: Legacy CSV bank catalog used by migration and compatibility tooling.
- configs/dealership.xml: Legacy XML dealership catalog retained for compatibility or migration.
- configs/Districts.csv: Legacy CSV district catalog used by migration and compatibility tooling.
- configs/GasStations.ini: Legacy INI gas-station configuration.
- configs/HiringNPC.ini: Legacy INI staffing and NPC hiring configuration.
- configs/IndusModules.csv: Legacy CSV industry-upgrade module catalog.
- configs/Industries.ini: Legacy INI industry catalog and production defaults.
- configs/Interiors.csv: Legacy CSV interior catalog for owned spaces.
- configs/Motel.csv: Legacy CSV motel catalog.
- configs/Objects.ini: Legacy INI object catalog for spawned LSOL props.
- configs/OfficeModules.csv: Legacy CSV office-object and workstation catalog.
- configs/Offices.csv: Legacy CSV office catalog.
- configs/README.md: Reference for legacy config formats and migration expectations.
- configs/Resources.ini: Legacy INI commodity and resource catalog.
- configs/Sites.csv: Legacy CSV site catalog for industries, depots, and service points.
- configs/Stores.ini: Legacy INI store catalog.
- configs/Vehicles.csv: Legacy CSV vehicle catalog for the commercial fleet.
- configs/Warehouse.ini: Legacy INI warehouse configuration and storage defaults.
- configs/WorldNpcLogistics.ini: Legacy INI routes and world-dispatch data for NPC logistics.

## LSOL_Addons

- LSOL_Addons/README.md: Developer guide for LSOL addon package structure and loading.

### LSOL_Addons_examples/sample.author.mission-pack

- LSOL_Addons_examples/sample.author.mission-pack/addon.xml: Sample addon manifest for a mission-pack extension.

##### LSOL_Addons_examples/sample.author.mission-pack/content/missions

- LSOL_Addons_examples/sample.author.mission-pack/content/missions/sample_quarry_delivery.xml: Example addon mission definition used as an authoring reference.

## LSOL_Config

- LSOL_Config/Banks.xml: XML bank catalog used by the runtime config loader.
- LSOL_Config/Core.xml: Core XML settings for controls, balance, and global runtime options.
- LSOL_Config/Dealership.xml: XML dealership catalog for commercial vehicle sales.
- LSOL_Config/Districts.xml: XML district and corridor definitions for territory-aware systems.
- LSOL_Config/HiringNPC.xml: XML hiring tables for NPC worker and logistics staffing.
- LSOL_Config/Interiors.xml: XML interior catalog for owned apartments and business spaces.
- LSOL_Config/Motels.xml: XML motel catalog for low-tier housing and save locations.
- LSOL_Config/Objects.xml: XML world-object catalog for spawned LSOL props.
- LSOL_Config/OfficeObjects.xml: XML office-object catalog for furniture and workstation placement.
- LSOL_Config/Offices.xml: XML office catalog for purchasable or rentable company hubs.
- LSOL_Config/Resources.xml: XML commodity/resource catalog for production and trading systems.
- LSOL_Config/Sites.xml: XML site catalog for industries, depots, and service locations.
- LSOL_Config/Vehicles.xml: XML vehicle catalog for owned commercial fleet units.
- LSOL_Config/VehiclesObjects.xml: XML attachment layouts for cargo visuals on supported vehicles.
- LSOL_Config/WorldNpcLogistics.xml: XML routes and world-dispatch data for ambient NPC logistics.

### LSOL_Config/missions

- LSOL_Config/missions/quarry_heavy_machinery.xml: Sample heavy-machinery mission authored in the LSOL mission schema.
- LSOL_Config/missions/README.md: Reference for the LSOL mission XML schema and authoring rules.

## LSOL_Documentation

- LSOL_Documentation/ADDONS_AND_MISSION_PACKS.md: Documentation for addon loading, mission packs, and extension authoring.
- LSOL_Documentation/APARTMENTS_AND_PERSONAL_VEHICLES.md: Guide to apartment ownership and personal vehicle storage.
- LSOL_Documentation/COMMERCIAL_VEHICLES_FUEL_AND_CARGO.md: Guide to commercial vehicles, fuel usage, and cargo handling.
- LSOL_Documentation/COMPANY_HUB_AND_MANAGEMENT_APPS.md: Guide to the company hub and management-tablet apps.
- LSOL_Documentation/COMPANY_MAP_AND_ROUTE_PLANNER.md: Guide to the company map, route planning, and forecasting views.
- LSOL_Documentation/DISTRICTS_AND_CORRIDORS.md: Guide to districts, corridors, and territorial operations.
- LSOL_Documentation/INDUSTRY_SITES_AND_DELIVERIES.md: Guide to industries, sites, deliveries, and supply loops.
- LSOL_Documentation/INSTALLATION_AND_FIRST_STEPS.md: Installation guide and first-session onboarding notes.
- LSOL_Documentation/NPC_LOGISTICS_AND_WORLD_DISPATCH.md: Guide to NPC logistics, ambient dispatch, and route automation.
- LSOL_Documentation/OFFICES_GARAGES_AND_SUPPORT_SITES.md: Guide to offices, garages, and support-site ownership.
- LSOL_Documentation/README.md: Top-level documentation hub for LSOL systems and setup.
- LSOL_Documentation/SAVES_AND_PROFILES.md: Guide to save data, career profiles, and restore behavior.
- LSOL_Documentation/SPECIAL_MISSIONS_AND_JOB_BOARD.md: Guide to special missions, contracts, and job-board flow.
- LSOL_Documentation/SUCCESSES_PRESTIGE_AND_DOCTRINE.md: Guide to successes, prestige progression, and doctrine bonuses.

### LSOL_Documentation/Industries

- LSOL_Documentation/Industries/Industries.md: Reference for supported industry types and their gameplay roles.
- LSOL_Documentation/Industries/README.md: Index page for industry-focused documentation.

### LSOL_Documentation/Sites

- LSOL_Documentation/Sites/GasStations.md: Reference for gas-station gameplay and service-site behavior.
- LSOL_Documentation/Sites/OperationalSites.md: Reference for operational sites, depots, and logistics stops.
- LSOL_Documentation/Sites/README.md: Index page for site-focused documentation.

### LSOL_Documentation/Stores

- LSOL_Documentation/Stores/README.md: Index page for store-focused documentation.
- LSOL_Documentation/Stores/Stores.md: Reference for store-related data and gameplay roles.

## missions

- missions/port_container_handler.xml: Port container-handler mission definition.
- missions/README.md: Reference for standalone mission definition files.

## scripthookdotnet

- scripthookdotnet/LemonUI.SHVDN3.dll: LemonUI runtime dependency for ScriptHookVDotNet3 menus.
- scripthookdotnet/ScriptHookV.dll: Native ScriptHookV runtime dependency.
- scripthookdotnet/ScriptHookVDotNet.asi: ScriptHookVDotNet loader used by GTA V to start managed scripts.
- scripthookdotnet/ScriptHookVDotNet.pdb: Debug symbols for the packaged ScriptHookVDotNet runtime.
- scripthookdotnet/ScriptHookVDotNet3.dll: Primary managed GTA runtime API used by LSOL.
- scripthookdotnet/ScriptHookVDotNet3.pdb: Debug symbols for the packaged ScriptHookVDotNet3 runtime.

## src

- src/LSOLScript.AlertRules.cs: Alert-rule evaluation and notification wiring for runtime events.
- src/LSOLScript.Banking.cs: Banking-specific script glue between UI, systems, and state.
- src/LSOLScript.cs: Main script entry point, lifecycle loop, and shared runtime state.
- src/LSOLScript.Difficulty.cs: Difficulty-profile script glue and runtime tuning hooks.
- src/LSOLScript.Persistence.cs: Persistence glue for saving, loading, and state restoration.
- src/LSOLScript.Properties.cs: Property-management script glue for owned spaces and vehicles.
- src/ModUtilities.cs: Shared GTA/SHVDN helper utilities used across the mod.
- src/ShvdnRuntimeCompatibility.cs: Compatibility shim for ScriptHookVDotNet API differences.

### src/Config

- src/Config/ControlBindings.cs: Models and readers for configurable control bindings.
- src/Config/CoreXmlConfig.cs: Loader for the core XML configuration document.
- src/Config/DistrictConfig.cs: Configuration models for districts and related map metadata.
- src/Config/ExternalConfigCatalog.cs: Registry that locates and stages external LSOL config files.
- src/Config/IndustryConfig.cs: Configuration models for industries, recipes, and upgrade modules.
- src/Config/IniFile.cs: Lightweight INI parser used by legacy config loaders.
- src/Config/LsolAddonCatalog.cs: Discovery and catalog layer for LSOL addon packages.
- src/Config/ModConfig.cs: Aggregated runtime configuration model consumed by the mod.
- src/Config/RuntimeLayoutResolver.cs: Resolves runtime file layout and config lookup paths.
- src/Config/SpecialMissionCatalog.cs: Catalog loader for special mission definitions.
- src/Config/XmlConfigImport.cs: XML import helpers and compatibility conversion logic.

### src/Domain

- src/Domain/BankDefinition.cs: Domain definition for banks, offers, and lending terms.
- src/Domain/CommodityCatalog.cs: Domain catalog of tradable commodities and related metadata.
- src/Domain/CommodityEconomySemantics.cs: Domain semantics for commodity pricing, supply, and demand.
- src/Domain/DealershipVehicleDefinition.cs: Domain definition for dealership vehicle entries.
- src/Domain/Industry.cs: Domain model for owned industries, production, and upgrades.
- src/Domain/IndustryUpgradeModule.cs: Domain model for industry upgrade modules and effects.
- src/Domain/InteriorDefinition.cs: Domain definition for interiors used by LSOL properties.
- src/Domain/MotelDefinition.cs: Domain definition for motels and related rental data.
- src/Domain/OfficeDefinition.cs: Domain definition for offices and company hubs.
- src/Domain/OfficeFacilityAnchorDefinition.cs: Domain definition for office placement anchors.
- src/Domain/OfficeObjectDefinition.cs: Domain definition for office furniture and workstation objects.
- src/Domain/ProductionRecipe.cs: Domain model for production inputs, outputs, and timing.
- src/Domain/ServiceSiteEconomyPolicy.cs: Domain rules for service-site pricing and local economics.
- src/Domain/SiteMetadata.cs: Domain metadata for industries, depots, and service sites.
- src/Domain/SpecialMissionDefinition.cs: Domain definition for special-mission metadata and objectives.
- src/Domain/VehicleCargoState.cs: Domain model for current vehicle cargo state and capacity use.
- src/Domain/VehicleCargoType.cs: Domain model for cargo categories and handling semantics.
- src/Domain/VehicleDefinition.cs: Domain definition for commercial vehicles and fleet stats.
- src/Domain/VehicleObjectLayoutDefinition.cs: Domain definition for cargo-prop attachment layouts on vehicles.

### src/Properties

- src/Properties/AssemblyInfo.cs: Assembly metadata for the LSOL build.

### src/Systems

- src/Systems/AmbientWorldDispatchText.cs: Text helpers for ambient world dispatch.
- src/Systems/BankCreditStandingCalculator.cs: Calculator for bank credit standing.
- src/Systems/BankLoanManager.cs: Core system that manages bank loan.
- src/Systems/BlipLifecycleManager.cs: Core system that manages blip lifecycle.
- src/Systems/CargoTransferController.cs: Controller for cargo transfer.
- src/Systems/CompanyDoctrineSystem.cs: High-level company-doctrine system for strategic progression bonuses.
- src/Systems/CompanyFinanceTracker.cs: Tracker for company finance.
- src/Systems/DifficultySettingsCatalog.cs: Catalog for difficulty settings.
- src/Systems/DifficultySettingsProfile.cs: Profile model for difficulty settings.
- src/Systems/DifficultySettingsTemplateStore.cs: Store for difficulty settings template.
- src/Systems/FleetManager.cs: Core system that manages fleet.
- src/Systems/GlobalMarketManager.cs: Core system that manages global market.
- src/Systems/IndustryManager.cs: Core system that manages industry.
- src/Systems/IndustryOutputPropManager.cs: Core system that manages industry output prop.
- src/Systems/IndustryPersistenceManager.cs: Core system that manages industry persistence.
- src/Systems/IndustryRefuelService.cs: Service for industry refuel.
- src/Systems/NpcLogisticsManager.cs: Core system that manages NPC logistics.
- src/Systems/NpcLogisticsPersistenceMapper.cs: Mapper for NPC logistics persistence.
- src/Systems/NpcWorldDispatchModels.cs: Shared models for ambient world-dispatch and NPC logistics messaging.
- src/Systems/OfficeObjectManager.cs: Core system that manages office object.
- src/Systems/PlayerContractsManager.cs: Core system that manages player contracts.
- src/Systems/PlayerSuccessTracker.cs: Tracker for player success.
- src/Systems/PlayerSuccessTracker.Definitions.cs: Success-definition tables and milestone metadata for progression tracking.
- src/Systems/PropertyManager.cs: Core system that manages property.
- src/Systems/PropertyPersistenceSnapshots.cs: Persistence snapshot models for owned-property state.
- src/Systems/RuntimePersistenceSnapshots.cs: Persistence snapshot models shared across runtime subsystems.
- src/Systems/SpecialMissionManager.cs: Core system that manages special mission.
- src/Systems/TerritoryManager.cs: High-level territorial-operations system for rivalry, pressure, and passive gains.
- src/Systems/VehicleFuelSystem.cs: Core system for vehicle fuel.
- src/Systems/VehicleLoadPowerService.cs: Service for vehicle load power.
- src/Systems/VehicleSpawnController.cs: Controller for vehicle spawn.
- src/Systems/WarehouseStorageRiskSnapshot.cs: Snapshot model for warehouse-risk telemetry and storage pressure.
- src/Systems/WorkerSpawnController.cs: Controller for worker spawn.

### src/UI

- src/UI/AccessibilityTheme.cs: Accessibility-focused theme tokens for LSOL UI screens.
- src/UI/AlertRulesEvaluator.cs: UI evaluator for alert rules.
- src/UI/AnalyticsTabletApp.cs: Tablet or menu app for analytics tablet.
- src/UI/BankOfferComparisonFormatter.cs: UI formatter for bank offer comparison.
- src/UI/BudgetFleetResaleFormatter.cs: UI formatter for budget fleet resale.
- src/UI/BudgetTabletApp.cs: Tablet or menu app for budget tablet.
- src/UI/CommodityStatEntry.cs: UI entry model for commodity stat.
- src/UI/CompanyMapController.cs: UI controller for company map.
- src/UI/CompanyMapDepotSpecializationFormatter.cs: UI formatter for company map depot specialization.
- src/UI/CompanyMapForecastFormatter.cs: UI formatter for company map forecast.
- src/UI/DebugMenuProvider.cs: UI provider for debug menu.
- src/UI/DifficultyMenuLayout.cs: UI layout for difficulty menu.
- src/UI/DifficultySettingsSummaryFormatter.cs: UI formatter for difficulty settings summary.
- src/UI/IndustryStatisticsPanelRenderer.cs: UI renderer for industry statistics panel.
- src/UI/IndustryStatisticsSnapshot.cs: UI snapshot model for industry statistics.
- src/UI/IndustryTabletController.cs: UI controller for industry tablet.
- src/UI/IndustryTabletUi.cs: UI module for industry tablet UI.
- src/UI/InGameClockHudFormatter.cs: UI formatter for in game clock HUD.
- src/UI/LemonMenu.cs: LemonUI integration layer for LSOL menu screens.
- src/UI/ModLocalization.cs: Localization helpers and text resources for LSOL UI.
- src/UI/NpcLogisticsController.cs: UI controller for NPC logistics.
- src/UI/NpcRouteProfitabilityFormatter.cs: UI formatter for NPC route profitability.
- src/UI/OfficeFuelManagementFormatter.cs: UI formatter for office fuel management.
- src/UI/OfficeObjectCatalogFormatter.cs: UI formatter for office object catalog.
- src/UI/OverviewMenuController.cs: UI controller for overview menu.
- src/UI/PropertyPortfolioModels.cs: UI data models for property portfolio.
- src/UI/PropertyPortfolioTabletApp.cs: Tablet or menu app for property portfolio tablet.
- src/UI/RoutePlannerModels.cs: UI data models for route planner.
- src/UI/SaveProfilePreviewFormatter.cs: UI formatter for save profile preview.
- src/UI/SimpleMenu.cs: Menu layer for simple.
- src/UI/SuccessesTabletApp.cs: Tablet or menu app for successes tablet.
- src/UI/TabletAnalyticsHistory.cs: UI history model for tablet analytics.
- src/UI/TabletApps.cs: UI module for tablet apps.
- src/UI/TabletChartRenderer.cs: UI renderer for tablet chart.
- src/UI/TabletDeadlineFormatter.cs: UI formatter for tablet deadline.
- src/UI/TabletElements.cs: UI module for tablet elements.
- src/UI/TabletEndgameStatusFormatter.cs: UI formatter for tablet endgame status.
- src/UI/TabletFleetAlertFormatter.cs: UI formatter for tablet fleet alert.
- src/UI/TabletLocationEconomicsFormatter.cs: UI formatter for tablet location economics.
- src/UI/TabletLocationFilters.cs: UI module for tablet location filters.
- src/UI/TabletServiceSiteStatusFormatter.cs: UI formatter for tablet service site status.
- src/UI/TabletShellController.cs: UI controller for tablet shell.
- src/UI/TabletStateStore.cs: UI state store for tablet state.
- src/UI/TabletStateStore.SnapshotRefresh.cs: Tablet-state refresh logic that rebuilds cached UI snapshots.
- src/UI/VehicleFuelHudFormatter.cs: UI formatter for vehicle fuel HUD.

### tests/LSOL.Tests

- tests/LSOL.Tests/LSOL.Tests.csproj: Unit-test project file for the LSOL test suite.
- tests/LSOL.Tests/LSOL.Tests.csproj.lscache: Language-service cache generated for the LSOL test project.

#### tests/LSOL.Tests/Config

- tests/LSOL.Tests/Config/ModConfigIniControlOverridesTests.cs: Unit tests for mod config INI control overrides configuration behavior.
- tests/LSOL.Tests/Config/ModConfigXmlLoadingTests.cs: Unit tests for mod config XML loading configuration behavior.
- tests/LSOL.Tests/Config/RuntimeLayoutResolverTests.cs: Unit tests for runtime layout resolver configuration behavior.

#### tests/LSOL.Tests/Systems

- tests/LSOL.Tests/Systems/AmbientWorldDispatchTextTests.cs: Unit tests for ambient world dispatch text system behavior.
- tests/LSOL.Tests/Systems/BankCreditStandingCalculatorTests.cs: Unit tests for bank credit standing calculator system behavior.
- tests/LSOL.Tests/Systems/BankLoanManagerTests.cs: Unit tests for bank loan manager system behavior.
- tests/LSOL.Tests/Systems/CargoTransferControllerTests.cs: Unit tests for cargo transfer controller system behavior.
- tests/LSOL.Tests/Systems/DifficultySettingsProfileTests.cs: Unit tests for difficulty settings profile system behavior.
- tests/LSOL.Tests/Systems/DifficultySettingsTemplateStoreTests.cs: Unit tests for difficulty settings template store system behavior.
- tests/LSOL.Tests/Systems/FleetManagerCargoVisualModelSelectionTests.cs: Unit tests for fleet manager cargo visual model selection system behavior.
- tests/LSOL.Tests/Systems/GlobalMarketManagerTests.cs: Unit tests for global market manager system behavior.
- tests/LSOL.Tests/Systems/IndustryManagerConfigSemanticsTests.cs: Unit tests for industry manager config semantics system behavior.
- tests/LSOL.Tests/Systems/IndustryManagerWarehouseStoragePressureTests.cs: Unit tests for industry manager warehouse storage pressure system behavior.
- tests/LSOL.Tests/Systems/IndustryPersistenceManagerPropertyOwnershipTests.cs: Unit tests for industry persistence manager property ownership system behavior.
- tests/LSOL.Tests/Systems/IndustryPersistenceManagerRoundTripTests.cs: Unit tests for industry persistence manager round trip system behavior.
- tests/LSOL.Tests/Systems/NpcLogisticsManagerAmbientDispatchTests.cs: Unit tests for NPC logistics manager ambient dispatch system behavior.
- tests/LSOL.Tests/Systems/NpcLogisticsManagerPersistenceTests.cs: Unit tests for NPC logistics manager persistence system behavior.
- tests/LSOL.Tests/Systems/OfficeObjectAmbientStaffResolverTests.cs: Unit tests for office object ambient staff resolver system behavior.
- tests/LSOL.Tests/Systems/OfficeObjectBaseCatalogTests.cs: Unit tests for office object base catalog system behavior.
- tests/LSOL.Tests/Systems/OfficeObjectPlacementResolverTests.cs: Unit tests for office object placement resolver system behavior.
- tests/LSOL.Tests/Systems/PlayerContractsManagerTests.cs: Unit tests for player contracts manager system behavior.
- tests/LSOL.Tests/Systems/PlayerSuccessTrackerDeliveryProgressTests.cs: Unit tests for player success tracker delivery progress system behavior.
- tests/LSOL.Tests/Systems/PlayerSuccessTrackerEndgameTests.cs: Unit tests for player success tracker endgame system behavior.
- tests/LSOL.Tests/Systems/PropertyManagerApartmentOwnershipTests.cs: Unit tests for property manager apartment ownership system behavior.
- tests/LSOL.Tests/Systems/PropertyManagerCommercialVehiclePurchaseTests.cs: Unit tests for property manager commercial vehicle purchase system behavior.
- tests/LSOL.Tests/Systems/PropertyManagerCommercialVehicleResaleTests.cs: Unit tests for property manager commercial vehicle resale system behavior.
- tests/LSOL.Tests/Systems/PropertyManagerOfficeRentalTests.cs: Unit tests for property manager office rental system behavior.
- tests/LSOL.Tests/Systems/SpecialMissionManagerRestoreTests.cs: Unit tests for special mission manager restore system behavior.
- tests/LSOL.Tests/Systems/TerritoryManagerCompetitionTests.cs: Unit tests for territory manager competition system behavior.
- tests/LSOL.Tests/Systems/TerritoryManagerPassiveIncomeTests.cs: Unit tests for territory manager passive income system behavior.
- tests/LSOL.Tests/Systems/VehicleFuelRangeEstimatorTests.cs: Unit tests for vehicle fuel range estimator system behavior.

#### tests/LSOL.Tests/TestSupport

- tests/LSOL.Tests/TestSupport/TestWorkspace.cs: Shared test-workspace helper used by config and system tests.

#### tests/LSOL.Tests/UI

- tests/LSOL.Tests/UI/AlertRulesEvaluatorTests.cs: Unit tests for alert rules evaluator UI behavior.
- tests/LSOL.Tests/UI/BankOfferComparisonFormatterTests.cs: Unit tests for bank offer comparison formatter UI behavior.
- tests/LSOL.Tests/UI/BudgetFleetResaleFormatterTests.cs: Unit tests for budget fleet resale formatter UI behavior.
- tests/LSOL.Tests/UI/CompanyMapControllerTests.cs: Unit tests for company map controller UI behavior.
- tests/LSOL.Tests/UI/CompanyMapControllerVisualHelperTests.cs: Unit tests for company map controller visual helper UI behavior.
- tests/LSOL.Tests/UI/CompanyMapDepotSpecializationFormatterTests.cs: Unit tests for company map depot specialization formatter UI behavior.
- tests/LSOL.Tests/UI/CompanyMapForecastFormatterTests.cs: Unit tests for company map forecast formatter UI behavior.
- tests/LSOL.Tests/UI/DebugMenuProviderTests.cs: Unit tests for debug menu provider UI behavior.
- tests/LSOL.Tests/UI/DifficultyMenuLayoutTests.cs: Unit tests for difficulty menu layout UI behavior.
- tests/LSOL.Tests/UI/InGameClockHudFormatterTests.cs: Unit tests for in game clock HUD formatter UI behavior.
- tests/LSOL.Tests/UI/NpcRouteProfitabilityFormatterTests.cs: Unit tests for NPC route profitability formatter UI behavior.
- tests/LSOL.Tests/UI/OfficeFuelManagementFormatterTests.cs: Unit tests for office fuel management formatter UI behavior.
- tests/LSOL.Tests/UI/OfficeMarkerMenuTests.cs: Unit tests for office marker menu UI behavior.
- tests/LSOL.Tests/UI/OfficeObjectCatalogFormatterTests.cs: Unit tests for office object catalog formatter UI behavior.
- tests/LSOL.Tests/UI/PropertyPortfolioTabletAppTests.cs: Unit tests for property portfolio tablet app UI behavior.
- tests/LSOL.Tests/UI/SaveProfilePreviewFormatterTests.cs: Unit tests for save profile preview formatter UI behavior.
- tests/LSOL.Tests/UI/SuccessesTabletAppTests.cs: Unit tests for successes tablet app UI behavior.
- tests/LSOL.Tests/UI/TabletBudgetFleetResaleSummaryTests.cs: Unit tests for tablet budget fleet resale summary UI behavior.
- tests/LSOL.Tests/UI/TabletEndgameStatusFormatterTests.cs: Unit tests for tablet endgame status formatter UI behavior.
- tests/LSOL.Tests/UI/TabletFleetAlertFormatterTests.cs: Unit tests for tablet fleet alert formatter UI behavior.
- tests/LSOL.Tests/UI/TabletLocationEconomicsFormatterTests.cs: Unit tests for tablet location economics formatter UI behavior.
- tests/LSOL.Tests/UI/TabletLocationFiltersTests.cs: Unit tests for tablet location filters UI behavior.
- tests/LSOL.Tests/UI/TabletLocationRowFormattingTests.cs: Unit tests for tablet location row formatting UI behavior.
- tests/LSOL.Tests/UI/TabletNpcRouteDrilldownTests.cs: Unit tests for tablet NPC route drilldown UI behavior.
- tests/LSOL.Tests/UI/TabletPropertyPortfolioSummaryTests.cs: Unit tests for tablet property portfolio summary UI behavior.
- tests/LSOL.Tests/UI/TabletRoutePlannerTests.cs: Unit tests for tablet route planner UI behavior.
- tests/LSOL.Tests/UI/TabletServiceSiteStatusFormatterTests.cs: Unit tests for tablet service site status formatter UI behavior.
- tests/LSOL.Tests/UI/TabletServiceSiteSummaryTests.cs: Unit tests for tablet service site summary UI behavior.
- tests/LSOL.Tests/UI/TabletStateStoreDirtyRefreshTests.cs: Unit tests for tablet state store dirty refresh UI behavior.
- tests/LSOL.Tests/UI/TabletUpcomingBillsTests.cs: Unit tests for tablet upcoming bills UI behavior.
- tests/LSOL.Tests/UI/TabletWarehouseRiskFormatterTests.cs: Unit tests for tablet warehouse risk formatter UI behavior.
- tests/LSOL.Tests/UI/VehicleFuelHudFormatterTests.cs: Unit tests for vehicle fuel HUD formatter UI behavior.