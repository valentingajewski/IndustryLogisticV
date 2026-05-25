# LSOL Add-on Ecosystem Analysis

## Executive Summary

LSOL already has one real, non-invasive extension surface: community XML mission packs loaded from `scripts/LSOL_Config/missions/*.xml`. That surface is file-based, auto-discovered, documented for creators, and explicitly mentioned in the player-facing README. Much of the rest of LSOL is also data-driven, but in a different way: runtime content such as sites, resources, vehicles, districts, offices, interiors, banks, office objects, and dealership data is loaded from `LSOL_Config/*.xml` through `ModConfig`, `ExternalConfigCatalog`, and `XmlConfigImport`. NPC hiring tiers and world-dispatch tuning are also externalized to `LSOL_Config` XML, but they are consumed directly by `NpcLogisticsManager` and `NpcWorldDispatchConfigLoader` rather than the main catalog pipeline. Those files make LSOL highly configurable, but they are still monolithic base catalogs loaded from fixed filenames, not isolated add-on packages.

The hard boundary today is runtime logic. Mission type recognition is hardcoded, mission execution is hardcoded, tablet apps are registered in core code, success definitions are compiled into the mod, localization and theme data are compiled into the mod, office object behavior is limited to a fixed enum, and NPC logistics behavior remains implemented inside core managers even though some of its tuning is externalized. No external DLL or plugin loader is present in the repository.

LSPDFR's ecosystem reference suggests a layered model, not a single plugin bucket. Its site separates the core mod from community categories such as Essential Mods, Vehicle Models, Vehicle Skins, Scripts & Plugins, Character, Audio, Visual & Data File, and Misc. Inside Scripts & Plugins, listings are further distinguishable by tags such as `lspdfr plugin`, `lspdfr callout`, `rph`, `scripthookvdotnet`, and `open source`. The important lesson for LSOL is not "load arbitrary DLLs first". The lesson is that a healthy ecosystem supports multiple extension tiers: pure content packs, tuning/data packs, focused gameplay modules, and only then advanced code plugins.

The recommended path for LSOL is therefore a hybrid ecosystem with strict layering:

1. Keep the current `LSOL_Config` and mission-pack behavior fully backward compatible.
2. Add a manifest-based `LSOL_Addons` package layer.
3. Support additive content packs first by merging fragment files for catalogs that are already data-driven.
4. Expand the declarative mission model before introducing code-based mission types.
5. Expose registry-based extension points for tablet apps, analytics panels, success definitions, and dispatch templates only after the package layer is stable.
6. Treat managed DLL/plugins as an advanced, opt-in tier, not the foundation of the ecosystem.

That approach is the safest fit for LSOL's current architecture, for ScriptHookVDotNet constraints, and for end-user reliability.

## Current LSOL Extension Surfaces

### 1. Runtime content root and file ownership

LSOL resolves a runtime directory near the assembly, the GTA `scripts` folder, or the base directory, and then treats `LSOL_Config` under that location as the base runtime content root while scanning sibling `LSOL_Addons` packages as a separate runtime package root. This matters because it means LSOL already thinks in terms of stable runtime-owned content locations, which is a useful anchor for the add-on system.

Repo-grounded evidence:

- `LSOLScript.Persistence.ResolveRuntimeDirectory()` searches candidate directories for `LSOL_Config`.
- `LSOLScript.Persistence.ResolveConfigDirectory()` returns `<runtime>/LSOL_Config`.
- `README.md` states that `LSOL_Config` is the base runtime content root, `LSOL_Addons` is the runtime add-on package root, and root `configs/` and root `missions/` are legacy reference folders and are not loaded at runtime.

### 2. Core XML-driven catalogs already in use

`ModConfig.Load()` calls `XmlConfigImport.LoadCoreConfig()` and `ExternalConfigCatalog.Load()`. `ExternalConfigCatalog.Load()` then populates multiple catalogs through dedicated XML loaders. Today, LSOL already reads runtime data from these XML files through the main catalog pipeline:

- `Core.xml`
- `Resources.xml`
- `Objects.xml`
- `Districts.xml`
- `Sites.xml`
- `Vehicles.xml`
- `Banks.xml`
- `Offices.xml`
- `OfficeObjects.xml`
- `Interiors.xml`
- `Dealership.xml`

Outside that main catalog pipeline, LSOL also reads:

- `HiringNPC.xml`
- `WorldNpcLogistics.xml`

This means LSOL is already highly data-driven in these areas:

- Commodity groups and base prices
- Cargo prop aliases
- District polygons
- Site and industry metadata
- Commercial vehicle definitions
- Bank locations and loan rules
- Office definitions
- Office object definitions
- Apartment/interior definitions
- Personal dealership content
- NPC tier tuning
- Ambient world-dispatch tuning

### 3. Mission definitions already support auto-discovered external content

`SpecialMissionCatalog.Load()` resolves the loose runtime mission path under `LSOL_Config/missions`, scans packaged mission directories under `LSOL_Addons/*/content/missions`, and parses each file into a `SpecialMissionDefinition`. `LSOL_Config/missions/README.md` documents the loose-path workflow for community authors, and `README.md` presents both mission runtime paths as supported player-facing features.

Current external mission support includes:

- One XML file per mission
- Automatic startup discovery
- Mission metadata, rewards, repeat rules, and unlock requirements
- Authored vehicle spawns, props, and zones
- Save/load support for mission progress and completion counts

### 4. Internal registry-like patterns that could evolve later

LSOL already contains some internal structures that are close to future extension points, even though they are not externally discoverable yet:

- `TabletShellController` uses an internal `ITabletApp` registry.
- `LSOLScript` registers tablet apps explicitly at startup.
- Mission definitions are already represented as domain objects and runtime instances are created from them.
- Office objects already use a catalog of definitions plus runtime behavior.

These internal patterns are important because they reduce the amount of architectural invention needed later. LSOL does not yet expose add-on registries, but it already uses registry-like shapes internally.

## Existing Community-Authoring Opportunities Already Present

### What LSOL users can already extend today without touching existing LSOL files

Runtime mission content now has two extension surfaces that meet the requirement of "without touching existing LSOL files":

- New XML mission files under `scripts/LSOL_Config/missions`
- New packaged mission add-ons under `scripts/LSOL_Addons/<package-id>/content/missions`

Both are true drop-in extension surfaces because authors can add new files and LSOL discovers them automatically.

### What is already data-driven but not yet add-on friendly

Several LSOL systems are configurable through external XML, but they are not currently community-pack-friendly in the LSPDFR sense because each catalog is loaded from a fixed base filename, not from additive fragments:

- New sites require changing `Sites.xml`
- New commodities require changing `Resources.xml`
- New commercial vehicles require changing `Vehicles.xml`
- New office objects require changing `OfficeObjects.xml`
- New offices require changing `Offices.xml`
- New interiors require changing `Interiors.xml`
- New banks require changing `Banks.xml`
- New personal dealership entries require changing `Dealership.xml`
- NPC tier changes require changing `HiringNPC.xml`
- Ambient dispatch tuning changes require changing `WorldNpcLogistics.xml`

So these systems are externally authored, but they are still base content, not self-contained add-ons.

### Content types already close to community-pack-friendly

The closest categories to true future add-on packs are:

1. Mission packs
2. Site/content packs
3. Commodity/resource packs
4. Vehicle/content packs
5. Office object packs
6. Economy tuning packs
7. UI localization/theme packs

The reason these are close is simple: LSOL already reads their inputs from files. The missing piece is packaging, discovery, merge behavior, and validation.

## Current Hardcoded Limits

### 1. Mission type support is hardcoded

The current community-authored mission XML surface supports two documented mission types:

- `handler_container_transfer`
- `trailer_delivery`

`SpecialMissionCatalog.ParseMissionType()` contains explicit string handling for those XML type names, and `LSOL_Config/missions/README.md` only documents those two external mission types.

`SpecialMissionManager.CreateRuntime()` then dispatches runtime creation through a `switch` over `SpecialMissionType` and creates concrete runtime classes for:

- `DynamicCargoDeliveryRuntime`
- `TrailerDeliveryRuntime`
- `HandlerContainerTransferRuntime`

Implication:

- Community authors can create more missions of supported types.
- Community authors cannot define new mission mechanics or a new mission type without core code changes.

### 2. Dynamic cargo missions are generated inside core code

`SpecialMissionManager` builds generated contracts in code and marks them as `SpecialMissionType.DynamicCargoDelivery` with `IsGenerated = true`. This is an important distinction:

- Dynamic cargo delivery exists in LSOL.
- It is not a community-authored mission type under the current XML schema.

### 3. Tablet app extensibility is internal only

`TabletShellController` contains an app registry and an internal `ITabletApp` interface, but apps are registered manually in `LSOLScript` with explicit `RegisterApp(...)` calls.

Implication:

- LSOL already has the right architectural shape for UI modules.
- It does not yet auto-discover or externally register them.

### 4. Successes and doctrine progression are compiled into the mod

`PlayerSuccessTracker` holds a static `Definitions = BuildDefinitions()`, and the actual success catalog is defined in `PlayerSuccessTracker.Definitions.cs`.

Implication:

- Successes are not data-driven today.
- Community-authored achievement packs are not possible without a new definition/registry layer.

### 5. Localization and theme data are compiled into the mod

`ModLocalization.BuildTranslations()` contains the translation table in code, and `AccessibilityThemeService` contains the color palettes in code.

Implication:

- LSOL supports multiple languages and theme modes.
- It does not yet support external translation packs or theme packs.

### 6. Office object behavior is limited to a fixed function enum

`OfficeObjectDefinition` is data-driven, but `OfficeObjectFunction` is a fixed enum:

- `Decorative`
- `Refuel`
- `Repair`
- `Npc`
- `Headquarters`

Implication:

- Community office object packs using existing functions are architecturally close.
- New office object behaviors are not possible without new hooks or plugin APIs.

### 7. NPC logistics behavior is implemented in core code

`NpcLogisticsManager` loads driver tiers from `HiringNPC.xml` and world-dispatch tuning from `WorldNpcLogistics.xml`, but the actual route behavior, scoring, dispatch generation, diagnostics, and world simulation live inside the manager.

Implication:

- Tuning is external.
- Behavior modules are not.

### 8. Catalog loading is still fixed-file and monolithic

`XmlConfigImport.LoadDocument()` loads specific filenames from `LSOL_Config` and returns validation messages if they are missing. It does not scan directories of fragment files for the main catalogs.

Implication:

- The project is data-driven.
- It is not yet package-merge-driven.

### 9. No external DLL/plugin loader is present

A repository search found mission file scanning and standard native interop, but no LSOL-specific assembly/plugin discovery path.

Implication:

- There is no current foundation for third-party managed plugins.
- Any DLL add-on model would be new architecture, not a hidden existing feature.

## Comparison With LSPDFR's Ecosystem Model

LSPDFR's ecosystem reference is useful because it shows how a mature mod ecosystem separates community contribution types instead of forcing every extension into one bucket.

### What the LSPDFR reference shows

The fetched LCPDFR/LSPDFR pages show that the GTA V mod area is organized into visible categories such as:

- Essential Mods
- Vehicle Models
- Vehicle Skins
- Scripts & Plugins
- Character
- Audio
- Visual & Data File
- Misc

Within `Scripts & Plugins`, listings are further differentiated by tags and metadata such as:

- `lspdfr plugin`
- `lspdfr callout`
- `rph`
- `scripthookvdotnet`
- `open source`

The site also exposes discoverability patterns that matter:

- clear category separation
- creator attribution
- versioned listings
- latest/featured/trending views
- tag-based filtering
- large-scale browsing independent of the base mod

### Why that matters for LSOL

LSOL should not think in terms of "base mod plus arbitrary DLLs".

LSOL should think in terms of an ecosystem taxonomy such as:

- Mission Packs
- Content Packs
- Economy / Preset Packs
- UI / Localization Packs
- Feature Modules
- Advanced Plugins

That layered model matches LSOL's current architecture better because LSOL already has strong file-authored content, but only limited external behavior hooks.

### LSOL-specific lesson from LSPDFR

The most useful thing to copy from the LSPDFR ecosystem is the separation between:

- pure content or data packs
- focused gameplay feature packs
- deep runtime plugins

That separation allows LSOL to grow safely. End users can install simple content packs with low risk, while advanced developers can later target higher-power extension surfaces.

## Candidate Add-on Models

| Model | What it enables | Strengths | Limits | Assessment |
| --- | --- | --- | --- | --- |
| Mission packs only | More contracts on the existing board | Already proven, safest, no loader redesign needed | Too narrow for a broader LSOL ecosystem | Good first surface, not sufficient as final ecosystem |
| Content packs only | Sites, vehicles, commodities, offices, props | Fits LSOL's XML-heavy architecture | Cannot extend runtime behavior or new mechanics | Essential layer, but not enough alone |
| Config packs / presets | Economy rebalance, NPC wages, dispatch tuning, UI palettes | Very low risk, easy for players | Mostly tuning, not new gameplay | Useful supporting layer |
| DLL / script plugins only | Deep behavior extensions | Highest power | Highest fragility, no sandbox, steep API burden | Should be last, not first |
| Hybrid ecosystem | Missions, content packs, presets, UI packs, later plugins | Best fit for LSOL's mixed data/code architecture | Requires explicit layering and validation | Recommended |

## Recommended Architecture

### Recommended answer to "What should LSOL support?"

LSOL should support a hybrid ecosystem, but in staged layers:

1. Mission packs
2. Content packs
3. Config / preset packs
4. UI / localization packs
5. Registry-based feature modules
6. Optional managed plugins only when justified

This is the safest and most maintainable answer because it lets LSOL exploit what is already working today instead of forcing the project to jump immediately into arbitrary code loading.

### Layer 0: Preserve the current base content model

Keep loading:

- `LSOL_Config/*.xml`
- `LSOL_Config/missions/*.xml`

unchanged.

This ensures existing installs, guides, mission packs, and player expectations remain valid.

### Layer 1: Add manifest-based add-on packages

Introduce a new runtime sibling folder, for example:

- `scripts/LSOL_Addons/`

Each add-on becomes a self-contained package with:

- a manifest
- declared category/capabilities
- its own content folders
- optional assets
- optional future plugin DLL

This becomes LSOL's equivalent of the conceptual separation visible in the LSPDFR ecosystem: the base mod remains separate from community packages.

### Layer 2: Support additive content fragments before code plugins

For catalogs that are already file-authored, LSOL should move from:

- one fixed base file per catalog

to:

- base file plus zero or more add-on fragments merged in deterministic order

This is the most valuable architectural change LSOL can make in the short to medium term.

### Layer 3: Expose registry-based extension points

Once package discovery and validation are stable, LSOL can expose controlled registries for:

- mission handlers
- tablet apps
- analytics cards
- success definitions
- dispatch rule providers
- economy modifier providers

This is the right middle layer between pure XML content and arbitrary plugins.

### Layer 4: Optional managed plugin API

Only after the previous layers are stable should LSOL consider code plugins. Even then, the plugin tier should be:

- narrow
- versioned
- capability-declared
- opt-in
- fail-soft when possible

The goal is not to make every extension a DLL. The goal is to reserve DLLs for features that truly cannot remain declarative.

## Proposed Folder Structure

### Recommended runtime layout

```text
GTA V/
  scripts/
    LSOL.dll
    LSOL.ini
    LSOL_Config/
      Core.xml
      Districts.xml
      Objects.xml
      Offices.xml
      OfficeObjects.xml
      Resources.xml
      Sites.xml
      Vehicles.xml
      HiringNPC.xml
      WorldNpcLogistics.xml
      Interiors.xml
      Dealership.xml
      Banks.xml
      missions/
        README.md
        legacy_community_mission.xml
    LSOL_Addons/
      author.port-expansion/
        addon.xml
        content/
          missions/
            port_hazmat_contract.xml
          resources/
            cold_chain_resources.xml
          sites/
            port_expansion_sites.xml
          vehicles/
            heavy_haul_vehicles.xml
          office-objects/
            depot_equipment.xml
          ui/
            localization/
              en.xml
              fr.xml
            themes/
              port_hub.xml
        assets/
        bin/
          Author.PortExpansion.dll
```

### Why this structure fits LSOL

- It keeps the existing `LSOL_Config` model intact.
- It avoids placing community content into the same base files LSOL ships.
- It gives each add-on a natural packaging boundary.
- It leaves room for future code plugins without requiring them for all add-ons.

### Initial scan recommendation

The first implementation step should not scan every possible folder immediately. The first step should scan:

- `LSOL_Config/missions/*.xml` (active loose runtime path)
- `LSOL_Addons/*/content/missions/*.xml` (new package model)

Then LSOL can grow fragment scanning for other content types later.

## Proposed Manifest / Package Model

### Recommended format

LSOL is already XML-first at runtime, and the repository uses `XDocument` heavily. For that reason, an XML manifest is the lowest-friction choice for a first implementation.

Recommended manifest file:

- `addon.xml`

### Recommended manifest fields

Each add-on manifest should include:

- `id`: unique stable identifier, ideally namespaced like `author.package`
- `name`: display name
- `version`: add-on version
- `category`: high-level category such as `mission-pack`, `content-pack`, `preset-pack`, `ui-pack`, `module`, `plugin`
- `author`
- `description`
- `website` or `source`
- `lsolApiVersion`: add-on framework version, separate from LSOL's game version
- `minLSOLVersion`
- `maxTestedLSOLVersion`
- dependency list
- conflict list
- declared capabilities
- declared content directories
- optional plugin assembly metadata for future advanced add-ons

### Example manifest

```xml
<Addon id="acme.port-expansion" version="1.2.0" category="content-pack" lsolApiVersion="1">
  <Metadata
    name="Port Expansion"
    author="Acme Mods"
    description="Adds new port-side sites, commodities, and contracts." />

  <Compatibility minLSOLVersion="1.0.0" maxTestedLSOLVersion="1.4.0" />

  <Dependencies>
    <Dependency id="core" minVersion="1.0.0" />
  </Dependencies>

  <Capabilities>
    <Capability name="content.resources" />
    <Capability name="content.sites" />
    <Capability name="content.vehicles" />
    <Capability name="content.missions" />
  </Capabilities>

  <Content>
    <Directory type="resources" path="content/resources" />
    <Directory type="sites" path="content/sites" />
    <Directory type="vehicles" path="content/vehicles" />
    <Directory type="missions" path="content/missions" />
  </Content>

  <Plugin assembly="bin/Acme.PortExpansion.dll" entryType="Acme.PortExpansion.Addon" enabledByDefault="false" />
</Addon>
```

### Versioning recommendation

Use two separate version tracks:

- LSOL mod version compatibility
- LSOL add-on API version compatibility

That separation matters because LSOL may be able to preserve add-on package compatibility across multiple mod versions even when internal implementation changes.

## Proposed API / Registry Surfaces

### File-driven registries first

LSOL should first expose registries that remain file-authored:

#### Catalog fragment registry

For add-on fragment merging of:

- districts
- resources
- sites
- vehicles
- offices
- office objects
- interiors
- banks
- dealership vehicles

#### Mission definition registry

For mission XML discovery from both the loose runtime mission path and packaged add-ons.

#### Localization registry

For external translation tables keyed by existing LSOL text keys and future add-on-defined keys.

#### Theme registry

For palette variants keyed by existing color roles.

#### Economy preset registry

For balancing packs that override well-defined scalar values without redefining whole catalogs.

### Registry-based module surfaces next

After the package layer is stable, LSOL could expose controlled registries such as:

#### Mission type handler registry

Maps a mission `type` string to a runtime factory instead of a hardcoded `switch`.

#### Tablet app / tablet card registry

Allows externally registered pages, cards, or app tiles. The current `ITabletApp` pattern is a strong internal starting point for this.

#### Analytics provider registry

Allows add-ons to supply additional derived dashboards or data cards without taking over the whole tablet shell.

#### Success definition registry

Allows threshold-based or declarative success packs.

#### Dispatch rule / job template registry

Allows add-ons to contribute new candidate job templates or scoring modifiers to world dispatch without fully replacing the NPC logistics system.

#### Economy modifier registry

Allows controlled modifiers for commodity prices, payout rules, wages, or upkeep formulas.

### Managed plugin API only for advanced cases

If LSOL eventually adds managed plugins, the API should be narrow and explicit. Likely useful advanced surfaces include:

- add-on lifecycle hooks
- save loaded / save reset hooks
- delivery completed event hooks
- mission accepted / mission completed events
- district state changed notifications
- tablet page registration
- mission type handler registration
- analytics card registration

The API should prefer read-only access to game state by default, with explicit service interfaces for mutations.

## Mission Ecosystem Recommendations

### Keep current mission packs fully supported

Existing mission support is already the strongest LSOL ecosystem feature. It should remain valid exactly as-is.

That means preserving support for:

- `LSOL_Config/missions/*.xml`
- `handler_container_transfer`
- `trailer_delivery`

### Add packaged mission pack discovery first

The smallest viable first step toward a real ecosystem is:

1. add an `LSOL_Addons` package scanner
2. require a manifest per package
3. scan packaged mission files in `content/missions`
4. preserve active `LSOL_Config/missions` support unchanged

This gives LSOL a real package model without forcing wider architectural changes immediately.

### Expand the mission schema before adding plugin mission types

The safest medium-term move is not immediate arbitrary code execution. It is expanding the declarative mission model to cover more mission structures.

Examples of mission features that could remain file-driven:

- multi-step objective sequences
- timers and deadlines
- failure conditions
- conditional rewards
- optional briefing text and debriefing text
- reusable route templates
- NPC companions or escorts with predefined roles
- cargo counts, weight targets, or delivery quotas

This could cover many "new mission type" requests without requiring DLLs.

### Support mission type packs later through a registry

Once LSOL has a mission type handler registry, the current hardcoded switch can be replaced by built-in registrations for existing mission types plus optional third-party registrations.

Recommended order:

1. built-in mission handlers are moved into the same registry LSOL will expose later
2. manifest capability `module.missionType` is added
3. third-party mission type handlers become possible only in the advanced tier

### Suggested mission categories for community packs

Practical LSOL mission-pack categories could include:

- district crisis packs
- tender packs
- heavy haul packs
- port logistics packs
- warehouse overflow packs
- construction support packs
- agricultural route packs
- late-game prestige contract packs

## Feature-Pack / Module Recommendations

### Mission packs

Assessment: `Already possible now`

This is LSOL's current best ecosystem surface and should remain the entry point for community creators.

### New mission type packs

Assessment: `Requires medium architectural work`, then sometimes `Requires full plugin framework`

If LSOL expands the declarative mission schema well, some mission-type growth can stay file-driven. Truly novel mechanics still require code hooks.

### New site/content packs

Assessment: `Possible with small core extension`

Why: the site model is already externalized in `Sites.xml`; what is missing is additive fragment loading and merge validation.

### Commodity/resource packs

Assessment: `Possible with small core extension`

Why: commodities and cargo groups are already loaded from `Resources.xml`; LSOL needs additive merge behavior, load ordering, and duplicate validation.

### Vehicle/content packs

Assessment: `Possible with small core extension`

Why: commercial vehicle definitions are already externalized in `Vehicles.xml`.

### Office object packs

Assessment:

- `Possible with small core extension` for new objects using existing functions
- `Requires medium architectural work` or `Requires full plugin framework` for brand-new object behaviors

Why: object definitions are data-driven, but behavior is limited to a fixed enum.

### NPC logistics behavior modules

Assessment: `Requires full plugin framework`

Why: tuning is external, but route behavior and dispatch logic are core-owned.

### Analytics/dashboard modules

Assessment:

- `Requires medium architectural work` for declarative or read-only dashboard cards
- `Requires full plugin framework` for fully custom interactive analytics apps

### Economy modifiers or presets

Assessment: `Possible with small core extension`

Why: the relevant data already exists in XML files, but it needs package isolation and explicit override semantics.

### World-dispatch job packs

Assessment:

- `Requires medium architectural work` for data-driven job templates
- `Requires full plugin framework` for new dispatch algorithms

### Success/achievement packs

Assessment:

- `Requires medium architectural work` for declarative thresholds and state checks
- `Requires full plugin framework` for custom scripted conditions

### UI theme or localization packs

Assessment: `Possible with small core extension`

Why: the translation keys and palette roles already exist, but their data is compiled into code instead of being externalized.

## Recommendation Matrix

| Add-on category | Current reality | Best near-term target | Required work |
| --- | --- | --- | --- |
| Mission packs | Supported now through `LSOL_Config/missions/*.xml` and packaged `LSOL_Addons/*/content/missions/*.xml` | Continue supporting both runtime paths | Already possible now |
| New mission type packs | Not supported | Registry-backed mission handlers after mission DSL growth | Requires medium architectural work, sometimes full plugin framework |
| New site/content packs | Data-driven but monolithic | Additive site fragments | Possible with small core extension |
| Commodity/resource packs | Data-driven but monolithic | Additive resource fragments | Possible with small core extension |
| Vehicle/content packs | Data-driven but monolithic | Additive vehicle fragments | Possible with small core extension |
| Office object packs | Data-driven within fixed functions | Additive object fragments using known functions | Possible with small core extension |
| Tablet app extensions | Internal registry only | External app/card registry | Requires medium architectural work |
| NPC logistics behavior modules | Tuning only, behavior is hardcoded | Explicit dispatch/route provider API | Requires full plugin framework |
| Analytics/dashboard modules | Hardcoded apps | Read-only dashboard card providers first | Requires medium architectural work |
| Economy modifiers or presets | Replacement-style tuning only | Explicit preset or override packs | Possible with small core extension |
| World-dispatch job packs | Hardcoded generation logic | Data-driven job templates first | Requires medium architectural work |
| Success/achievement packs | Hardcoded definitions | Declarative success definitions | Requires medium architectural work |
| UI theme packs | Hardcoded palette tables | External palette packs | Possible with small core extension |
| Localization packs | Hardcoded translation tables | External translation packs | Possible with small core extension |

## UI / Tablet / Menu Extension Opportunities

### Why UI extensibility is realistic in LSOL

UI extension is realistic because LSOL already has:

- a tablet shell
- app IDs
- page routes
- a state store
- an internal app registry

That is a better starting point than building a UI extension system from scratch.

### Recommended staged UI extension model

#### Stage 1: Declarative dashboard cards

Allow add-ons to contribute read-only dashboard cards that reference known `TabletStateStore` metrics.

Examples:

- district heat summaries
- fleet maintenance summaries
- mission-pack promotional tiles
- economy preset summaries

This is much safer than letting add-ons inject arbitrary UI code immediately.

#### Stage 2: External tablet apps/pages

Expose a controlled version of the current `ITabletApp` registration path so add-ons can register a new app or page.

Recommended limit for first release:

- apps should use existing shell components
- apps should declare app IDs, titles, routes, and capability requirements
- apps should consume stable read-only state APIs

#### Stage 3: Advanced UI modules

Reserve full custom renderers or interactive workflows for the advanced plugin tier.

### Theme packs and localization packs

These are strong candidates for early non-code add-ons because they map cleanly to current LSOL internals:

- `ModLocalization` already has a string-key model
- `AccessibilityTheme` already has color-role keys

The main work is simply moving authored values into external files and deciding merge precedence.

## Config/Data-Pack Opportunities

### New site/content packs

This is one of the most natural LSOL add-on categories because `Sites.xml` already carries rich site metadata:

- IDs
- district assignment
- ownership tier
- economy presets
- vehicle spawns
- worker/gate/display settings
- commodity flow data

Needed change: additive site fragments plus duplicate/reference validation.

### Commodity/resource packs

Also highly practical because `Resources.xml` already defines:

- cargo groups
- commodity membership
- base prices

Needed change: additive resource-group loading before sites and vehicles are merged.

### Vehicle/content packs

Commercial vehicle definitions are already file-driven. This makes heavy-haul packs, refrigeration packs, specialized fleet packs, and progression packs realistic once additive loading exists.

### Office object packs

These are practical in two tiers:

- `Tier A`: more decorative, refuel, repair, NPC-capacity, or HQ-style objects using current function types
- `Tier B`: new function-bearing objects such as security consoles, dispatch desks, training bays, or analytics terminals, which would need new hooks

### Economy modifiers or presets

Economy packs are especially realistic because LSOL already externalizes many balance inputs:

- production and storage values in `Sites.xml`
- commodity base prices in `Resources.xml`
- NPC wages in `HiringNPC.xml`
- world dispatch tuning in `WorldNpcLogistics.xml`
- core scalar settings in `Core.xml`

The best packaging model here is not arbitrary merge-by-default. It is likely one of:

- named preset packs where one is active at a time
- explicit override packs with clear priority and conflict warnings

### World-dispatch job packs

This category is plausible, but not immediately. A good medium-term path is to make world-dispatch job templates data-driven while keeping the dispatch engine in core code.

That would allow community-authored packs such as:

- seasonal agricultural demand surges
- port congestion jobs
- emergency service replenishment packs
- district-specific shortage templates

## Backward Compatibility Strategy

Backward compatibility should be treated as a design constraint, not an afterthought.

### 1. Preserve current runtime roots

Keep:

- `LSOL_Config/*.xml`
- `LSOL_Config/missions/*.xml`

fully supported.

### 2. Load add-ons after base content

The safest merge model is:

1. base LSOL content loads first
2. packaged add-ons load second
3. deterministic ordering is applied across add-ons

This keeps existing installs working even when no add-ons are present.

### 3. Use additive-only merge semantics first

For v1 of an add-on ecosystem, LSOL should avoid silent overrides.

Recommended v1 rule:

- new IDs are accepted
- duplicate IDs are rejected with validation errors

This is simpler, safer, and easier for creators to reason about.

### 4. Make new schema fields optional

When LSOL expands mission or catalog schemas, new elements should be optional so existing community mission files and existing `LSOL_Config` files remain valid.

### 5. Preserve current mission type strings

Keep support for current mission identifiers and mission attributes even if the internal runtime moves to a handler registry later.

### 6. Gracefully handle missing add-ons in saves

If a save references content from a removed add-on, LSOL should prefer fail-soft behavior:

- mark missing content as unavailable
- skip broken mission entries
- preserve the rest of the save
- surface a validation warning to the player

## Validation / Security / Stability Considerations

### File-driven add-ons

For content packs and mission packs, LSOL should validate:

- manifest shape
- semantic version compatibility
- duplicate IDs
- dependencies and conflicts
- references to districts, commodities, offices, sites, and object functions
- unsupported mission types
- unsupported capability declarations

Load failures should be per-add-on, not fatal to the entire mod whenever possible.

### Load-order concerns

LSOL will need deterministic phase ordering. A sensible order is:

1. manifests
2. districts and resources
3. sites, vehicles, offices, banks, interiors, dealership, office objects
4. missions
5. localization and themes
6. optional plugin assemblies

Within a phase, order should be dependency-driven first, then explicit priority, then stable lexical fallback.

### ID namespacing

To avoid collisions, add-ons should be encouraged or required to namespace identifiers. Examples:

- `acme.port.hazmat_delivery_01`
- `acme.coldchain.frozen_food`
- `acme.portexpansion.site_terminal_02`

This matters especially for:

- mission IDs
- site IDs
- office object IDs
- vehicle IDs if LSOL treats them as package-level identities later

### Managed plugins are not sandboxable in any strong sense

If LSOL ever supports DLL plugins, they should be treated as trusted code. Under .NET Framework and ScriptHookVDotNet, LSOL should not promise hard sandboxing.

What LSOL can realistically provide instead:

- explicit capability disclosure
- compatibility checks
- safe mode / disable-all-addons boot mode
- per-add-on enable/disable flags
- load logs and validation reports
- API version gating

### User safety and support burden

The biggest reason not to start with DLL plugins is support burden. A purely declarative content pack is much easier to validate, debug, and explain to users than a third-party assembly that can crash gameplay or break saves.

## Suggested Phased Roadmap

### Phase 1: Package-aware mission add-ons

Smallest viable first step toward a real LSOL ecosystem:

1. Introduce `LSOL_Addons/*/addon.xml`
2. Scan `content/missions/*.xml`
3. Preserve the current loose runtime mission path at `LSOL_Config/missions/*.xml`
4. Add manifest/version/dependency validation
5. Surface validation results to the player or log

This creates the package system without widening scope too early.

### Phase 2: Additive catalog fragments

Add support for fragment directories such as:

- `content/resources`
- `content/sites`
- `content/vehicles`
- `content/offices`
- `content/office-objects`
- `content/interiors`
- `content/banks`
- `content/dealership`
- `content/districts`

Recommended rule: additive-only merges first.

### Phase 3: Mission DSL growth and handler registry refactor

Expand mission schema and move built-in mission handlers behind a registry so the core itself uses the same extension mechanism it may later expose.

### Phase 4: UI, localization, and theme packs

Externalize:

- translation tables
- palette definitions
- read-only dashboard cards

This gives LSOL safer community UI extensibility before interactive code plugins.

### Phase 5: Registry-based feature modules

Expose controlled registries for:

- tablet apps/pages
- analytics modules
- success definitions
- world-dispatch templates
- economy modifier providers

### Phase 6: Optional managed plugin tier

Only after the previous layers are stable, add advanced code plugins with:

- manifest-declared capability model
- explicit version compatibility
- optional per-plugin enablement
- documented narrow API surface

## Concrete Example Add-on Types

| Add-on type | Example | Practical path |
| --- | --- | --- |
| Mission pack | `Port Crisis Contracts Pack` | Already possible now if it stays within current supported mission types |
| New mission type pack | `Multi-stop refrigerated convoy pack` | Mission DSL expansion first, plugin handler later if needed |
| New site/content pack | `Blaine Agriculture Expansion` | Additive `resources` + `sites` fragments |
| Commodity/resource pack | `Cold Chain Commodities Pack` | Additive `resources` fragments |
| Vehicle/content pack | `Heavy Haul Fleet Pack` | Additive `vehicles` fragments |
| Office object pack | `Depot Equipment Pack` | Additive `office-objects` fragments using current functions |
| Tablet app extension | `Fleet Maintenance Dashboard` | External tablet app/card registry |
| NPC logistics behavior module | `Cold-chain routing priority module` | Advanced registry or plugin tier |
| Analytics/dashboard module | `Corridor Profit Heatmap` | Analytics provider registry, possibly plugin-backed |
| Economy modifier or preset | `Hardcore Logistics Overhead Preset` | Preset/override pack |
| World-dispatch job pack | `Storm Response Dispatch Pack` | Data-driven dispatch templates first |
| Success/achievement pack | `Port Authority Success Pack` | Declarative success definition registry |
| UI theme pack | `Industrial Gray Tablet Theme` | External palette pack |
| Localization pack | `French Logistics Localization` | External translation pack |

## Final Recommendation

LSOL should evolve into a hybrid ecosystem, but the ecosystem should be content-first and registry-driven before it becomes plugin-driven.

The practical recommendation is:

1. Treat packaged mission support as the first official add-on tier.
2. Add a manifest-based `LSOL_Addons` package model.
3. Keep `LSOL_Config` and existing mission packs fully backward compatible.
4. Make data-driven catalogs additive before attempting advanced plugin behavior.
5. Expand the mission DSL before introducing custom mission-type DLLs.
6. Expose internal registry patterns, especially tablet apps, as controlled extension points over time.
7. Externalize themes, localization, and preset-style balance data as safer early ecosystem categories.
8. Keep NPC behavior modules, advanced analytics modules, and deep UI modules in a later advanced tier.
9. Treat managed plugins as trusted, opt-in, high-power add-ons with clear version and capability declarations.
10. Build discoverability around package metadata and categories so LSOL can eventually support the same layered community contribution patterns that make the LSPDFR ecosystem usable.

If LSOL follows that path, it can support community-created missions, content expansions, balance packs, UI packs, and eventually advanced feature modules without forcing creators to overwrite base LSOL files or pushing end users directly into the most fragile extension model.