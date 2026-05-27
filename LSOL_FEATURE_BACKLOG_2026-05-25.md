# LSOL Feature Backlog and Prioritization Report

Generated from repo analysis on 2026-05-25.

## 1. Executive Summary

LSOL already has a much broader foundation than a normal trucking mod: hauling, industry ownership, offices and apartments, fleet fuel and maintenance, NPC route automation, ambient world dispatch, bank loans, district licensing, corridor rights, depot specialization, analytics, success tracking, generated contracts, authored special missions, prestige, doctrine, and a code-backed HQ endgame layer all exist in some form.

The highest-value backlog is not invent-brand-new pillars first. It is:

- Finish code-backed but content-missing systems.
- Expose hidden management loops that already run in the background.
- Repair the economy and content holes that make some chains structurally weak.
- Deepen the midgame around fleet operations, NPC dispatch, and territory obligations.
- Complete the late-game identity loop around doctrine, prestige, competition, and HQ.
- Expand the add-on ecosystem carefully from data packs toward safe UI and module surfaces.

Effort buckets used below:

- XS: data, UI, content, or visibility work on an existing code path.
- S: small multi-file enhancement inside an existing subsystem.
- M: meaningful feature deepening across 2-3 systems.
- L: major loop expansion or new management layer.
- XL: new architecture or a large multi-system feature family.

## 2. System Coverage Checklist

- Trucking, trailer handling, cargo loading and unloading, and owned fleet flow: Implemented and mature.
- Fuel, repairs, inspections, maintenance condition, and depreciation pressure: Implemented but under-surfaced.
- Industry production, site ownership, warehouses, stores, gas stations, and service sinks: Implemented with economy and data gaps.
- Offices, apartments, motels, garages, and object placement: Implemented with thin management depth.
- NPC logistics contracts, wages, tiers, and route persistence: Implemented and meaningful.
- Ambient world dispatch and diagnostics: Implemented but mostly hidden from the player.
- Banking and one-loan finance layer: Implemented but shallow.
- Districts, charters, corridors, support depots, and service targets: Implemented and strategically important.
- Quick jobs, freight market, crisis contracts, tenders, priority runs, and authored missions: Implemented but content breadth is still limited.
- Tablet apps, company map, analytics, budget, and overview surfaces: Implemented but missing drill-down and control surfaces.
- Success tracking, prestige, doctrine math, competition pressure, and HQ logic: Partially existing and needs completion.
- Save profiles and manifest-based add-ons: Implemented, but advanced extension surfaces are still partial.

## 3. Full Feature Backlog By Effort

### XS / Extra Small

- [Economy] Complete commodity base-price coverage and commodity-name parity. Player value: every lane prices correctly and analytics become trustworthy. Extends: global market pricing, resource catalog, existing rebalance work. Gameplay phase: Early-Mid. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.

- [Industry Network] Close dead-end chains by adding or fixing sinks for Medicine, Furniture, Vehicles, Paper, and Metal. Player value: more commodities become worth hauling end-to-end. Extends: sites catalog and existing rebalance notes. Gameplay phase: Early-Mid. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Progression and Endgame] Ship a base Landmark HQ Annex definition in the active office-object catalog. Player value: the HQ capstone becomes actually obtainable without external content. Extends: HQ office-object function, success goals, doctrine tier math. Gameplay phase: Late-Endgame. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.

- [Progression and Endgame] Add a doctrine, prestige, and HQ status card to existing tablet surfaces. Player value: players can finally understand why endgame bonuses are active or inactive. Extends: success tracker, doctrine state, tablet apps. Gameplay phase: Late-Endgame. Effort: XS. Confidence: High. Label: You are working in the LSOL GTA V mod repo. Implement this backlog item:

[Territory and Competition] Add a service-site weekly target and status panel.
Player value: store, gas station, and construction-service obligations stop being opaque.
Gameplay phase: Mid-Late
Effort target: XS
Label: Missing Surface on Existing System

Important repo grounding:
- This is primarily a UI/surface completion task, not a new territory system.
- The backend already tracks service-contract progress and passive-income state in src/Systems/TerritoryManager.cs.
- TerritorySiteState already contains:
  - CurrentWeekServiceDeliveries
  - CurrentWeekServiceTons
  - RequiredWeeklyServiceTons
  - ServicePenaltySteps
  - ServiceSuccessStreak
  - ServiceTargetMetLastWeek
  - ServiceContractStatus
  - SiteOperatorAssigned
  - PassiveIncomeStockReady
  - PassiveIncomeOperational
  - PassiveIncomeStatus
  - LastPassiveIncomeAmount
  - LastPassiveIncomeWeekIndex
  - LastPassiveIncomeStatus
- Weekly maintenance already updates those fields in ProcessServiceFranchiseMaintenance(...).
- TerritoryManager already exposes GetSiteState(industry), GetServiceSiteWeeklyStaffingCost(industry), IsServiceSinkStockedForPassiveIncome(industry), and IsServiceSinkOperationalForPassiveIncome(industry).
- Company Map already partially surfaces this data: src/UI/CompanyMapController.cs appends ServiceContractStatus and current/required weekly service tons in district/site detail text.
- TabletStateStore already surfaces some service-site state into TabletLocationSummary:
  - staffing
  - stock readiness
  - passive-income operational state
  - weekly passive income
  - weekly staffing cost
  - last passive income amount
  - passive-income status
  - recent payout status
  - contract status
- Industry tablet site detail already renders a service section in src/UI/TabletApps.cs, but it currently stops short of showing the actual weekly target progress, remaining tons, streak/penalty state, and explicit last-week contract outcome.
- This is the clearest missing-surface gap.

Important scope guard:
- Do not invent new territory math, new passive-income rules, or new persistence.
- Reuse the existing TerritorySiteState data instead of recomputing target tons in the UI.
- Do not duplicate the private target-calculation logic from TerritoryManager in the UI.
- Current operator/passive-income gameplay is explicitly for owned stores and gas stations. If construction-site obligations already come through the same existing TerritorySiteState fields, you may surface them too, but do not broaden gameplay rules or add new construction mechanics in this task.

Likely implementation direction:
- Prefer the smallest clean path:
  1. Extend TabletLocationSummary in src/UI/TabletStateStore.cs with the service-target fields that are already present on TerritorySiteState.
  2. Populate those fields when building owned service-site summaries from TerritoryManager.GetSiteState(industry).
  3. Update the existing service section in IndustryTabletApp inside src/UI/TabletApps.cs to add a real weekly target/status panel.
- Good candidate fields to add to TabletLocationSummary:
  - ServiceCurrentWeekDeliveries
  - ServiceCurrentWeekTons
  - ServiceRequiredWeeklyTons
  - ServicePenaltySteps
  - ServiceSuccessStreak
  - ServiceTargetMetLastWeek
- Keep the current service banner/income/staff/stock/operations layout, but add one compact info row or status block that makes the weekly contract legible.
- A good result would show:
  - current week tons vs required tons
  - remaining tons if the target is not yet met
  - current week delivery count
  - contract risk state
  - recovery/probation state via penalty steps or secured/watch/at-risk wording
  - whether last week was met or missed
  - recent passive-income payout/outcome if relevant
- Reuse existing status strings where they already exist:
  - ServiceContractStatus
  - PassiveIncomeStatus
  - LastPassiveIncomeStatus
- If target tons is zero or unavailable, hide the target row rather than showing nonsense.

UI conventions to preserve:
- Keep this inside the existing tablet/list-detail flow.
- Do not create a new app, new dashboard, or custom pagination.
- Do not overload overview list rows with too much text. The richer target/status detail should live in the site detail panel first.
- If you add any overview summary text, keep it concise and consistent with current line-2 status formatting.

Concrete gap to fill:
- Today the industry tablet service detail shows:
  - passive-income banner
  - weekly income
  - staffing
  - stock
  - operations
  - optional contract line
- It does not clearly show:
  - weekly target progress numbers
  - whether the target is already met this week
  - how close the site is to failure or recovery
  - streak/penalty context
  - a clear “last week outcome” line tied to the contract, not only passive income

Acceptance criteria:
- On an owned service site, the tablet detail clearly shows weekly service progress using existing territory state.
- The player can see current week tons vs required weekly tons.
- The player can tell whether the target is met, at risk, or missed.
- The player can see contract pressure/recovery state in a readable way.
- Existing passive-income, staff, and stock info still reads cleanly.
- No new backend rules, persistence, or balance changes are introduced.
- If construction sites already carry the same service-target state in existing data, they should surface it through the same display path; otherwise do not force new behavior.

Likely files:
- src/UI/TabletStateStore.cs
- src/UI/TabletApps.cs
- optionally src/UI/CompanyMapController.cs only if you need minor wording parity, but avoid widening scope
- tests/LSOL.Tests/UI/... for any new summary/UI-state test

Testing guidance:
- There is already backend coverage in tests/LSOL.Tests/Systems/TerritoryManagerPassiveIncomeTests.cs.
- CompanyMapControllerTests already seed CurrentWeekServiceTons in territory snapshots.
- Add a focused UI/state test for the new summary fields or rendered detail helper if practical.
- Prefer a narrow test around TabletStateStore service summary enrichment or a pure formatter/helper if you introduce one.
- Build with:
  - dotnet build LSOL.csproj -c Release

When finished, report:
- which tablet surface(s) now show weekly service target status
- which existing TerritorySiteState fields were surfaced
- whether construction-site obligations were already supported by existing data or intentionally left unchanged
- what you validated on Existing System.

- [Territory and Competition] Add a service-site weekly target and status panel. Player value: store, gas station, and construction-service obligations stop being opaque. Extends: territory service-target tracking and passive-income state. Gameplay phase: Mid-Late. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Territory and Competition] Add corridor and charter at-risk forecast notices before weekly maintenance resolves. Player value: players can prevent silent penalties and corridor decay. Extends: weekly maintenance results, corridor states, map summaries. Gameplay phase: Mid-Late. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Fleet and Service] Add an alert center for low fuel, overdue inspections, and poor condition. Player value: fleet failures become manageable instead of surprising. Extends: fuel telemetry, fleet maintenance summary, existing status notifications. Gameplay phase: All Phases. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Economy] Add a bank comparison screen with installment preview by bank and term. Player value: borrowing becomes strategic instead of blind. Extends: bank previews, supported terms, current offer snapshots. Gameplay phase: Early-Mid. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Properties and Facilities] Improve office-object catalog cards with function, capacity, office limit, and haul requirement. Player value: office modules become readable before purchase. Extends: office object definitions and purchase menu. Gameplay phase: Early-Mid. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Properties and Facilities] Surface diesel tank storage, refill state, and implied per-liter cost. Player value: office refuel becomes a real management tool. Extends: office refuel storage and fuel-delivery flow. Gameplay phase: Early-Mid. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [NPC Logistics] Show exact route-block reasons in hire and edit flows. Player value: the player sees whether the blocker is standing, corridor rights, stock, or vehicle compatibility. Extends: existing route validation. Gameplay phase: Midgame. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Contracts and Missions] Add board sorting and filtering for expiry, payout density, rig class, district, and commodity. Player value: the existing contract board becomes usable at scale. Extends: quick-job and freight-market listings. Gameplay phase: Early-Mid. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [UI and Analytics] Add inventory valuation drill-down by site and commodity. Player value: warehouse value stops being a black box. Extends: budget and analytics inventory valuation. Gameplay phase: Midgame. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [UI and Analytics] Add route profitability drill-down by NPC contract and contract family. Player value: bad routes become easy to spot and fix. Extends: budget tablet, analytics tablet, NPC finance history. Gameplay phase: Midgame. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Territory and Competition] Add depot specialization effect tooltips and summary text in the Company Map. Player value: specialization choice becomes informed instead of experimental. Extends: specialization captions and depot state. Gameplay phase: Mid-Late. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Contracts and Missions] Strengthen special-mission progress hints for cleanup, trailer, and container-transfer stages. Player value: authored missions feel fairer and less trial-and-error. Extends: current mission stage runtime text and zones. Gameplay phase: Midgame. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Add-ons and Authoring] Surface add-on validation warnings in a dedicated viewer. Player value: authors can diagnose pack issues without digging through logs. Extends: add-on catalog validation messages. Gameplay phase: All Phases. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Save and Profiles] Add save-profile preview metadata such as balance, active office, fleet count, date, and difficulty flags. Player value: restoring the right company becomes much easier. Extends: named save profiles and persistence metadata. Gameplay phase: All Phases. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Industry Network] Surface owner cut, passive-income readiness, and service-income readiness in site detail views. Player value: ownership decisions become legible. Extends: industry state, territory site state, site economics. Gameplay phase: Early-Mid. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.
- [Territory and Competition] Show district tender standing and district standing directly in map and contract surfaces. Player value: players know why tenders are unlocked or blocked. Extends: tender gating and district-state logic. Gameplay phase: Mid-Late. Effort: XS. Confidence: High. Label: Missing Surface on Existing System.

### S / Small

- [NPC Logistics] Build a world-dispatch dashboard with live job list and diagnostics. Player value: ambient dispatch becomes a controllable system instead of background noise. Extends: world jobs, dispatch diagnostics, tablet overviews. Gameplay phase: Mid-Late. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Properties and Facilities] Add a property portfolio dashboard for offices, apartments, motels, rent, arrears, and office assignment. Player value: all owned and rented properties become manageable from one place. Extends: property manager and existing menus. Gameplay phase: Early-Mid. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Fleet and Service] Add an active-vehicle fuel gauge and range estimate HUD element. Player value: route planning gets immediate on-road feedback. Extends: fuel telemetry. Gameplay phase: Early Game. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Fleet and Service] Turn the Maintenance Bay into a visible service workflow with inspection reset and cost preview. Player value: inspections and repairs become something the player plans around. Extends: maintenance condition, inspection backlog, repair bay flow. Gameplay phase: Midgame. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Territory and Competition] Add a territory obligations dashboard with weekly cost and target preview. Player value: charters, corridors, service targets, and support sites become understandable before the bill lands. Extends: operations summary and weekly maintenance math. Gameplay phase: Mid-Late. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Contracts and Missions] Add backhaul and urgent-shuttle families to the live contract board. Player value: more route variety without needing a new mission engine. Extends: contract-board generation. Gameplay phase: Early-Mid. Effort: S. Confidence: High. Label: Extension.
- [Contracts and Missions] Add a weekly-tender bid and eligibility preflight panel. Player value: tender content feels intentional rather than quietly gated. Extends: tender generation and district-standing checks. Gameplay phase: Mid-Late. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [NPC Logistics] Add route-template copy and duplicate tools. Player value: scaling up a successful lane stops being tedious. Extends: route definitions and NPC contract editor. Gameplay phase: Midgame. Effort: S. Confidence: High. Label: Extension.
- [Properties and Facilities] Add office layout presets and snap-to-yard placement helpers. Player value: office-object placement becomes faster and cleaner. Extends: object placement system. Gameplay phase: Early-Mid. Effort: S. Confidence: High. Label: Extension.
- [Industry Network] Add warehouse-condition and spoilage-risk visibility by site. Player value: sensitive inventory can be managed before it loses value. Extends: warehouse condition logic and sensitive commodity lists. Gameplay phase: Midgame. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Territory and Competition] Add a district heat map and weekly activity meter to the Company Map. Player value: expansion planning becomes strategic instead of guess-based. Extends: district influence, activity, and map snapshot caching. Gameplay phase: Mid-Late. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Economy] Add bank-offer history and a simple credit score. Player value: borrowing gains continuity across a career. Extends: offer snapshots and finance history. Gameplay phase: Early-Mid. Effort: S. Confidence: High. Label: Deepening.
- [UI and Analytics] Add a configurable alert-rules center for rent, contracts, fleet, and territory. Player value: the player can decide what the mod should warn about. Extends: status queue and due-time tracking. Gameplay phase: All Phases. Effort: S. Confidence: High. Label: Extension.
- [Territory and Competition] Add a support-depot crew-capacity planner. Player value: support-site and NPC scale become predictable before expansion. Extends: depot states, NPC-capacity modules, operations summary. Gameplay phase: Mid-Late. Effort: S. Confidence: Medium. Label: Missing Surface on Existing System.
- [Properties and Facilities] Expand existing-function office modules with larger fuel tanks, upgraded NPC cabins, and premium maintenance bays. Player value: offices scale with company size using systems already present. Extends: refuel, repair, and NPC office-object functions. Gameplay phase: Midgame. Effort: S. Confidence: High. Label: Extension.
- [Contracts and Missions] Add more authored missions using current runtimes: more trailer deliveries, more handler jobs, reefer pulls, tanker relays, and carrier hauls. Player value: immediate mission variety with low engine risk. Extends: existing mission XML schema and runtimes. Gameplay phase: Mid-Late. Effort: S. Confidence: High. Label: Extension.
- [Contracts and Missions] Group the mission board by authored, generated, crisis, tender, and priority content. Player value: the player can tell what type of work they are taking. Extends: mission categories and current board content. Gameplay phase: Midgame. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Add-ons and Authoring] Ship richer sample add-on packs for sites, vehicles, office objects, and missions. Player value: community authors get correct reference patterns fast. Extends: existing manifest-based add-on system. Gameplay phase: All Phases. Effort: S. Confidence: High. Label: Extension.
- [Fleet and Service] Add resale and depreciation previews in garage and budget surfaces. Player value: vehicle condition visibly affects business decisions. Extends: maintenance condition and sale refund logic. Gameplay phase: Midgame. Effort: S. Confidence: High. Label: Missing Surface on Existing System.
- [Territory and Competition] Add charter acquisition preview with cost, support bonus, and risk forecast. Player value: district expansion becomes a clear decision instead of a leap. Extends: license cost, support bonus, district state, and maintenance logic. Gameplay phase: Midgame. Effort: S. Confidence: High. Label: Missing Surface on Existing System.

### M / Medium

- [NPC Logistics] Add driver training and tier progression. Player value: hired NPCs become long-term assets rather than static contracts. Extends: existing tiers, wages, speed and loss multipliers. Gameplay phase: Mid-Late. Effort: M. Confidence: High. Label: Deepening.
- [NPC Logistics] Add multi-stop loop and backhaul routes for hired drivers. Player value: empty return legs become productive and route efficiency rises. Extends: existing multi-route contract structure. Gameplay phase: Mid-Late. Effort: M. Confidence: High. Label: Deepening.
- [Industry Network] Add warehouse specialization modes such as cold storage, secure storage, and high-throughput. Player value: warehouses become strategic assets instead of neutral buffers. Extends: warehouse condition, sensitive commodity groups, site metadata. Gameplay phase: Mid-Late. Effort: M. Confidence: High. Label: Deepening.
- [Industry Network] Add optional-input recipe weighting and per-site recipe variants. Player value: production chains become less flat and more believable. Extends: recipe model, output weights, rebalance roadmap. Gameplay phase: Early-Mid to Mid. Effort: M. Confidence: High. Label: Deepening.
- [Fleet and Service] Add remote tow and roadside service. Player value: out-of-fuel or damaged rigs create management decisions instead of dead ends. Extends: repair cost category, fuel failures, service UI, office services. Gameplay phase: Midgame. Effort: M. Confidence: High. Label: Extension.
- [Fleet and Service] Add separate wear layers for engine, tires, and brakes. Player value: fleet upkeep becomes richer than one condition number. Extends: maintenance condition, inspections, repair costs. Gameplay phase: Mid-Late. Effort: M. Confidence: High. Label: Deepening.
- [Contracts and Missions] Add shipper or district reputation for repeated reliable hauling. Player value: better work can be unlocked through consistency. Extends: district standing, contract completion state, finance history. Gameplay phase: Mid-Late. Effort: M. Confidence: Medium. Label: New Feature.
- [Territory and Competition] Add visible rival-carrier events using the existing competition pressure and opportunity layer. Player value: district competition becomes concrete instead of abstract. Extends: competition pressure, opportunity, and win tracking. Gameplay phase: Late Game. Effort: M. Confidence: High. Label: Deepening.
- [Progression and Endgame] Add a real HQ project flow with meaningful capstone bonuses. Player value: Landmark HQ becomes a true endgame build target. Extends: HQ function, prestige, doctrine, success goals. Gameplay phase: Endgame. Effort: M. Confidence: High. Label: Deepening.
- [Progression and Endgame] Add doctrine choice UI with live bonus and tradeoff explanation. Player value: endgame company identity becomes intentional and legible. Extends: doctrine system and endgame state. Gameplay phase: Late-Endgame. Effort: M. Confidence: High. Label: Missing Surface on Existing System.
- [Properties and Facilities] Add office expansion and satellite yards. Player value: the company can grow facilities without abandoning earlier properties. Extends: office ownership, garage capacity, active office logic. Gameplay phase: Mid-Late. Effort: M. Confidence: Medium. Label: New Feature.
- [Contracts and Missions] Add cargo-group-specific contract families such as reefer rescue, fuel relay, heavy haul, and vehicle-carrier work. Player value: commodity and rig diversity matter more. Extends: contract-board generation, mission generation, cargo groups, vehicle definitions. Gameplay phase: Mid-Late. Effort: M. Confidence: High. Label: Extension.
- [Territory and Competition] Add service-franchise recovery jobs and probation recovery events. Player value: missed service targets generate recoverable drama instead of passive punishment only. Extends: service penalties, crisis and tender generation, weekly maintenance states. Gameplay phase: Mid-Late. Effort: M. Confidence: High. Label: Deepening.
- [Add-ons and Authoring] Add fail-soft save recovery for removed add-on content. Player value: saves stay usable even when content packs disappear. Extends: add-on validation and persistence snapshots. Gameplay phase: All Phases. Effort: M. Confidence: High. Label: Missing Surface on Existing System.
- [Add-ons and Authoring] Add safe read-only add-on tablet cards. Player value: packs can expose analytics or content info without arbitrary runtime code. Extends: current tablet registration direction and recognized module surface. Gameplay phase: Authoring and Late. Effort: M. Confidence: Medium. Label: New Feature.
- [Economy] Integrate seasonal demand cycles and a market-shock calendar. Player value: planning around time windows becomes valuable. Extends: global market shocks, temporary demand shocks, analytics history. Gameplay phase: Mid-Late. Effort: M. Confidence: Medium. Label: Extension.
- [UI and Analytics] Add an operations recommendation engine for blocked chains, idle stock, loss-heavy routes, and at-risk service sites. Player value: the UI starts telling the player what to fix next. Extends: analytics, route profitability, world dispatch diagnostics, territory risk. Gameplay phase: All Phases. Effort: M. Confidence: High. Label: Extension.

### L / Large

- [Territory and Competition] Add a rival-company simulation at district and corridor level. Player value: competition becomes a living part of the map rather than pressure numbers only. Extends: competition pressure and opportunity, competition wins, ambient dispatch. Gameplay phase: Late-Endgame. Effort: L. Confidence: Medium. Label: New Feature.
- [Industry Network] Add real warehouse spoilage and shrinkage with financial consequences. Player value: inventory stewardship becomes a major management pillar. Extends: warehouse condition, sensitive commodities, budget categories. Gameplay phase: Mid-Late. Effort: L. Confidence: High. Label: Deepening.
- [UI and Analytics] Add a route planner and map optimizer for player and NPC lanes. Player value: network design becomes first-class gameplay. Extends: Company Map, route definitions, district and corridor eligibility, pricing data. Gameplay phase: Mid-Late. Effort: L. Confidence: Medium. Label: New Feature.
- [Territory and Competition] Add a dynamic district crisis and event system with operational consequences. Player value: districts feel alive and reactive. Extends: crisis types, market shocks, contract generation, territory state. Gameplay phase: Mid-Late. Effort: L. Confidence: High. Label: Deepening.
- [Properties and Facilities] Add richer office facility modules with ambient staff and interactive rooms. Player value: offices feel like active company spaces rather than menus and yards. Extends: office objects, placement, office ownership loop. Gameplay phase: Mid-Late. Effort: L. Confidence: Medium. Label: Extension.
- [Fleet and Service] Add load-based handling and power penalties using the existing vehicle-load service. Player value: rig choice and overloading finally matter on the road. Extends: vehicle-load service, cargo state, fuel and maintenance systems. Gameplay phase: Mid-Late. Effort: L. Confidence: High. Label: Deepening.
- [Economy] Add loan default, restructuring, and repossession systems. Player value: borrowing gains real stakes and recovery paths. Extends: bank loans, property ownership, fleet sale logic, finance history. Gameplay phase: Mid-Late. Effort: L. Confidence: Medium. Label: New Feature.
- [Contracts and Missions] Add multi-stage mission chains with graded outcomes and bonus objectives. Player value: mission content becomes more memorable and replayable. Extends: existing mission runtimes, checkpoints, and board systems. Gameplay phase: Mid-Late. Effort: L. Confidence: Medium. Label: Extension.
- [Add-ons and Authoring] Load theme and localization packages from the manifest ecosystem. Player value: LSOL becomes much friendlier to long-term community content and accessibility. Extends: already recognized but unloaded capabilities. Gameplay phase: All Phases. Effort: L. Confidence: High. Label: Missing Surface on Existing System.
- [Progression and Endgame] Externalize success and doctrine tables for balance passes and content packs. Player value: late-game progression becomes easier to extend and rebalance. Extends: hardcoded success and doctrine definitions. Gameplay phase: Late-Endgame. Effort: L. Confidence: Medium. Label: New Feature.
- [Properties and Facilities] Add apartment and residence customization with business-adjacent perks. Player value: residential ownership becomes more than storage and a spawn point. Extends: interiors, apartment ownership, and personal garage. Gameplay phase: Early-Mid to Late. Effort: L. Confidence: Medium. Label: New Feature.

### XL / Very Large

- [Add-ons and Authoring] Add a full safe add-on runtime for custom tablet apps, mission types, and approved modules. Player value: LSOL gets a durable ecosystem rather than only fixed core content. Extends: manifest layer, recognized module capabilities, and existing add-on catalog. Gameplay phase: Authoring and Endgame. Effort: XL. Confidence: Medium. Label: New Feature.
- [Progression and Endgame] Add prestige reset, legacy company bonuses, and a new-game-plus loop. Player value: endgame gets replay value beyond simple completion. Extends: prestige, doctrine, HQ, success, and save profiles. Gameplay phase: Endgame. Effort: XL. Confidence: Medium. Label: New Feature.
- [Economy] Overhaul supply-demand simulation with per-commodity semantics, substitutes, and sink elasticity. Player value: hauling decisions feel systemic instead of mostly data-authored. Extends: market pricing, recipes, sites, and commodity catalog. Gameplay phase: All Phases. Effort: XL. Confidence: Medium. Label: New Feature.
- [NPC Logistics] Add full personnel management for drivers and site staff. Player value: LSOL becomes a true company-simulation layer, not only route management. Extends: NPC tiers, service-site operators, support crews, and wage tracking. Gameplay phase: Mid-Late. Effort: XL. Confidence: Low-Medium. Label: New Feature.
- [Properties and Facilities] Add a full property interior customization editor. Player value: offices, apartments, and HQ become expressive long-term goals. Extends: placement system, interiors, office objects, persistence. Gameplay phase: All Phases. Effort: XL. Confidence: Medium. Label: New Feature.
- [Territory and Competition] Add a persistent AI carrier ecosystem with growing and shrinking rival networks. Player value: the map evolves even when the player is passive. Extends: competition pressure, corridors, district standing, ambient dispatch. Gameplay phase: Late-Endgame. Effort: XL. Confidence: Medium. Label: New Feature.
- [Contracts and Missions] Add a campaign-chapter dispatcher built on logistics missions and district progress. Player value: LSOL gains long-form authored progression without losing its sandbox. Extends: special missions, contracts, district standing, HQ milestones. Gameplay phase: Mid-Late. Effort: XL. Confidence: Low-Medium. Label: New Feature.
- [Territory and Competition] Add live world logistics events with district-wide consequences and recovery windows. Player value: late-game operations stay dynamic and reactive. Extends: market shocks, crisis contracts, service targets, and territory maintenance. Gameplay phase: Late-Endgame. Effort: XL. Confidence: Medium. Label: New Feature.

## 4. Feature Categories

- Economy and Market
  Covers price coverage, chain closure, bank tooling, seasonal demand, default and restructuring, and the eventual supply-demand overhaul. This is the best category for immediate ROI because LSOL already has a real finance and pricing core.

- Industry Network and Warehousing
  Covers sink fixes, service-site visibility, warehouse condition, specialization, recipe weighting, spoilage, and site-detail expansion. This category converts existing site breadth into stronger gameplay depth.

- Fleet and Service
  Covers fuel HUD, maintenance alerts, repair workflows, resale visibility, recovery and tow, multi-part wear, and load-based handling. This category makes the road-game loop feel less opaque and more mechanical.

- Properties and Facilities
  Covers portfolio management, office module scaling, placement helpers, office expansion, richer office spaces, apartments, and property customization. This category is important because LSOL already treats offices and homes as persistent company infrastructure.

- NPC Logistics and Dispatch
  Covers route-block reasons, dispatch dashboards, route templates, training, multi-stop routing, and personnel-scale features. This category deepens one of LSOL's most advanced existing systems.

- Territory and Competition
  Covers charters, corridors, service targets, depot specialization, heat maps, rival events, crisis systems, and persistent rival networks. This is the strongest late-game management category already present in the code.

- Contracts and Missions
  Covers board filters, new contract families, tender surfaces, authored mission expansion, mission grouping, mission chains, and long-form campaign dispatch. This category is the best content-expansion path because the board and mission runtimes already exist.

- Progression and Endgame
  Covers HQ content, prestige and doctrine visibility, HQ bonuses, doctrine choice, and prestige-reset systems. This category matters because LSOL already has the scaffolding but not the full player-facing payoff.

- UI and Analytics
  Covers drill-downs, heat maps, alert rules, planner tools, recommendation engines, and better dashboards. This category has unusually high value because many systems are already running but are hidden from the player.

- Add-ons and Authoring
  Covers validation, samples, fail-soft saves, theme and localization loading, safe tablet cards, externalized progression data, and eventually a safe runtime. This category determines whether LSOL can scale beyond core-authored content.

## 5. Important Rule For Partially Existing Features

- Extension means the system is already live and the backlog item broadens it with more content, more variants, or more options.
- Deepening means the system is already live and the backlog item adds new strategic states, consequences, or interactions to make it materially richer.
- Missing Surface on Existing System means the backend, data, or math already exists, but the player-facing content, UI, or active catalog support is incomplete.
- New Feature means there is no strong directly implemented version yet, even if adjacent systems make it a natural fit.

The key planning rule is: LSOL should prioritize Missing Surface on Existing System and Deepening over New Feature whenever player value is similar. This repo already has many systems that look missing only because they are hidden, thinly authored, or not surfaced cleanly.

## 6. Final Prioritization

1. Complete the economy data pass: commodity base-price coverage, name parity, and chain-end sink fixes.
   Reason: this improves almost every haul lane immediately and fixes structural weakness already documented in the repo.

2. Ship the Landmark HQ Annex in base content and add doctrine, prestige, and HQ visibility.
   Reason: the endgame is already coded deeply enough that this is a completion task, not a greenfield task.

3. Surface territory obligations, service-site targets, and corridor and charter risk before weekly penalties hit.
   Reason: charters, corridors, and service contracts already matter financially; the player just cannot read them well enough.

4. Deepen the existing contract board with stronger filters, tender visibility, and a few more generated families.
   Reason: quick jobs and freight market are already real; better surfacing produces immediate content density.

5. Add fleet alerting, a fuel HUD, and a visible maintenance and inspection workflow.
   Reason: fuel, inspections, and depreciation are already active pressure systems and need better player feedback.

6. Add property portfolio management and stronger office-module transparency.
   Reason: offices, apartments, and modules already form a company backbone, but the management layer is too fragmented.

7. Build a usable world-dispatch and NPC-operations dashboard, then add route templates.
   Reason: NPC logistics is already one of the strongest systems in LSOL and should be easier to scale.

8. Improve site detail views with inventory value, owner-cut visibility, passive-income state, and service readiness.
   Reason: this turns existing economic and territory math into readable choices.

9. Expand authored mission content and cargo-group-specific contract families before building new mission architecture.
   Reason: LSOL already has mission runtimes and a board; content-first expansion is cheaper and safer.

10. Add district heat maps, charter previews, and depot-specialization explanations.
    Reason: the territorial layer is already mid and late-game critical and deserves first-class visibility.

11. Add warehouse condition visibility now, then warehouse specialization and spoilage simulation next.
    Reason: the warehouse loop already has the beginnings of a quality and condition layer and can become a major midgame pillar.

12. Turn competition pressure into visible rival events before attempting full rival-company simulation.
    Reason: the math is already there, and visible events are the cheapest way to make the system feel real.

13. Improve add-on quality-of-life with validation UI, richer examples, and fail-soft save handling before bigger plugin ambitions.
    Reason: the data-pack ecosystem already exists and can deliver value without large runtime risk.

14. Expand office modules and office spaces using existing functions before building a full facility editor.
    Reason: module depth will pay off earlier than full property customization.

15. Delay full add-on runtime, prestige reset, full rival simulation, and deep economy overhaul until the missing-surface backlog is mostly complete.
    Reason: LSOL still has too many high-confidence, lower-risk completions left on the table.

## 7. Gap Detection

- HQ gap
  The HQ office-object function exists in code, progression logic, and README-level promises, but the active base office-object XML does not define an HQ module. This is the clearest code-backed content gap.

- Doctrine and prestige gap
  Doctrine, prestige, HQ state, and effective-tier math already influence late-game formulas, but the player-facing explanation and decision layer are still thin. This is a visibility and completion problem, not a missing concept problem.

- Contract-board gap
  Quick jobs and freight market are already implemented with expiries, filters, cooldowns, and supplied-vehicle logic, but their surface area still reads as thinner than the backend actually is.

- Service-site gap
  Service-site operator staffing, weekly service targets, passive income readiness, and penalties already exist, but the player lacks clean surfaces to monitor and recover them.

- Fleet-pressure gap
  Fuel usage, inspections, maintenance condition, repair bays, and depreciation already create pressure, but the player-facing warnings and drill-downs are still too weak.

- Economy data gap
  The repo's own rebalance document identifies incomplete commodity pricing and unfinished chain ends. This is a structural backlog item, not optional polish.

- Office-object catalog gap
  The function system supports Refuel, Repair, Npc, and Headquarters, but the base content only ships refuel, NPC cabin, maintenance, and decor. The runtime is ahead of the authored catalog.

- Ambient-dispatch gap
  World dispatch jobs, diagnostics, and policies exist, but much of the system is effectively hidden from ordinary play. This is a strong candidate for near-term UI work.

- Add-on capability gap
  Packaged missions, resources, sites, vehicles, and office objects are supported now, but localization, theming, tablet-app modules, mission-type modules, and plugin metadata are only recognized, not truly loaded.

- Authoring and documentation gap
  Apartments are active via interior and residential content rather than an obvious standalone apartment catalog, and the HQ promise outpaces the shipped base office-object catalog. Some player expectations are therefore being set by docs more clearly than by active content.

- Competition gap
  Competition pressure, opportunity, and wins exist in the systems layer, but there is no equally visible rival-actor layer. The game already tracks competition mathematically before it shows it dramatically.

- UI gap
  A large fraction of LSOL's backlog is not missing gameplay. It is existing gameplay with missing visibility, drill-down, content authoring, or control surfaces. That is the strongest overall conclusion from the repo scan.