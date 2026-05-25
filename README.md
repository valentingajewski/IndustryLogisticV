# Los Santos Overloaded Logistics (LSOL)

LSOL turns GTA V into a logistics and industry management game.

You start small, move cargo between industries, earn money from deliveries, expand your fleet, buy property, automate routes with NPC drivers, and unlock more advanced company systems over time.

This README is written for players first. It explains what the mod does, how to install it, and how to get started without needing to read the code.

Additional player guide:

- [DISTRICTS_AND_CORRIDORS.md](DISTRICTS_AND_CORRIDORS.md) explains district influence, reputation labels, corridor progression, and why NPC logistics may still be gated in a region.

## What LSOL Adds To GTA V

LSOL adds a full business-management loop on top of GTA V free roam:

- A network of industries, depots, stores, warehouses, and gas stations across the map.
- Cargo trucks, trailers, fuel usage, and cargo condition.
- Office ownership and rental progression.
- Apartment ownership and personal vehicle storage.
- Commercial truck dealership and personal car dealership.
- Company management menus, a company hub, analytics screens, and industry menus.
- NPC logistics drivers that can run routes for you.
- Save files, named profiles, and persistent world/company progression.
- Optional custom special missions loaded from `LSOL_Config/missions/*.xml`.
- Manifest-based add-on packages loaded from `LSOL_Addons/*/addon.xml`, including packaged mission packs and additive content fragments for selected XML catalogs.

In short: you are building a logistics company inside GTA V.

## Main Gameplay Loop

The usual LSOL progression looks like this:

1. Rent or buy an office.
2. Buy a commercial truck or trailer combo from the commercial dealership.
3. Drive to an industry marker and use the industry tablet to load cargo.
4. Deliver that cargo to another site that accepts it.
5. Earn money.
6. Expand into better offices, more vehicles, owned industries, apartments, NPC drivers, and special missions.

The mod becomes deeper as you go. Early on it feels like trucking and delivery management. Later it becomes a wider company simulator with ownership, automation, analytics, mission content, and territory operations costs tied to the footprint you maintain.

## Features Overview

### Industries and Deliveries

Industries produce, consume, and transform commodities. Some sites are producers, some are processors, some are sinks such as stores or gas stations.

You use the industry tablet to:

- load cargo,
- unload cargo,
- buy industries when the rules require ownership,
- buy upgrade modules,
- review industry details.

### Offices

Offices are your commercial base of operations.

Each office has:

- its own location and map blip,
- a vehicle spawn point,
- a limited number of active commercial garage slots,
- a rental price or purchase price,
- a weekly rent/payment rule.

Your active office controls where your commercial vehicles are managed and where company trucks spawn from when retrieved from storage.

If rent is not paid, access to that property can be suspended until arrears are cleared.

### Commercial Vehicles

Commercial vehicles are bought from the commercial dealership.

The dealership supports:

- cargo filters,
- different truck/trailer combinations,
- vehicle pricing from the vehicle config,
- storage inside the active office garage,
- deployment and retrieval from office or industry flows.

Deployed office trucks also get their own truck blips on the map while you are outside them.

### Apartments and Personal Cars

Apartments work as your residential property system.

They allow you to:

- buy and activate a home,
- enter the apartment interior,
- unlock a personal garage,
- buy personal vehicles from the personal dealership,
- store and retrieve personal cars assigned to the active apartment.

Apartments also have ongoing rent/arrears rules.

### Fuel and Cargo Condition

LSOL can track fuel usage and cargo condition.

Depending on your save settings, you may need to manage:

- refueling,
- fuel capacity,
- out-of-fuel situations,
- cargo damage,
- lost cargo value.

This adds more planning to longer routes and heavier rigs.

### NPC Logistics

Once your company is established, you can hire NPC drivers to automate routes.

NPC logistics includes:

- driver tiers,
- route automation,
- upfront contract costs,
- recurring weekly wages,
- persistence across saves.

### Company Hub, Overview, and Analytics

The mod includes a company hub that lets you review your business instead of driving everywhere just to check progress.

You can view:

- company overview information,
- site and district summaries,
- analytics and trends,
- recurring territory obligations, district charters, corridor upkeep risk, and support-site specialization in the Budget, Analytics, and Company Map views,
- available mission content,
- current company state.

### Special Missions

LSOL now runs a rotating mission board with district crisis contracts, weekly tenders, and priority line-haul jobs, alongside special community mission packs loaded from `LSOL_Config/missions` and packaged add-ons under `LSOL_Addons/*/content/missions`.

These missions can add custom jobs such as:

- district crisis relief contracts,
- weekly tender slots tied to district standing,
- priority long-haul board runs,
- trailer deliveries,
- container handling jobs,
- heavy machinery transport-style tasks.

Mission progress also saves with the rest of your company.

### Late-Game Economic Pressure

As your company gets larger, LSOL now pushes back with additional management pressure instead of only scaling raw throughput.

This phase adds:

- rotating market shocks that temporarily lift prices and district demand for selected commodity groups,
- corporate overhead that scales with owned sites, fleet size, hired NPC crews, district licences, support sites, and corridor footprint,
- owned-fleet maintenance and inspection cycles with Maintenance Bays reducing wear and backlog,
- resale depreciation on commercial vehicles based on maintenance history and condition,
- warehouse storage condition that can reduce inventory value and slowly create spoilage or shrinkage on sensitive cargo,
- new budget and analytics visibility for overhead, maintenance, inventory losses, market shocks, and warehouse condition.

### Prestige, Doctrine, and Endgame Identity

Once your company is established, LSOL now adds a prestige layer so late-game play is about identity and resilience, not only bigger counters.

This phase adds:

- three strategic company doctrines that emerge from how you build the company: `Regional Backbone`, `Integrated Chain`, and `Client Priority`,
- doctrine bonuses and tradeoffs tied into district support, delivery returns, service targets, corridor upkeep, and route-loss exposure,
- a unique `Landmark HQ Annex` office object that acts as a company-wide capstone once placed at an owned office,
- district competition pressure driven by ambient outside-carrier traffic, with opportunity windows and defense wins tracked over time,
- new Successes tablet progress for doctrine posture, prestige score, HQ status, and endgame-oriented goals,
- district competition visibility inside the Analytics tablet and Company Map detail views.

## Requirements

You need a working GTA V ScriptHook setup.

Required base components:

- GTA V (single-player mod setup)
- ScriptHookV
- ScriptHookVDotNet v3
- LemonUI for SHVDN3

If those dependencies are missing, LSOL will not load correctly.

## Installation

### Recommended Player Install

1. Install ScriptHookV.
2. Install ScriptHookVDotNet v3.
3. Make sure `LemonUI.SHVDN3.dll` is available in your GTA V `scripts` setup.
4. Copy `LSOL.dll` into your GTA V `scripts` folder.
5. Put `LSOL.ini` next to the mod so the script can find it.
6. Copy the full `LSOL_Config` folder next to `LSOL.ini`. This is LSOL's active base runtime content root.
7. If you distribute loose runtime mission packs, place their `.xml` files under `LSOL_Config/missions`.
8. If you distribute packaged add-ons, place each package under `LSOL_Addons/<package-id>/` with its own `addon.xml` manifest.

### Supported `LSOL.ini` Locations

LSOL looks for `LSOL.ini` in these locations:

1. next to the compiled LSOL assembly,
2. GTA V root folder,
3. GTA V `scripts` folder.

Recommended location:

- `GTA V/scripts/LSOL.ini`

### Important Folder Layout

If `LSOL.ini` is in `GTA V/scripts/`, the mod expects this content beside it:

```text
GTA V/
  scripts/
    LSOL.dll
    LSOL.ini
    LemonUI.SHVDN3.dll
    LSOL_Config/
      Core.xml
      Dealership.xml
      Districts.xml
      HiringNPC.xml
      Interiors.xml
      Objects.xml
      Offices.xml
      Resources.xml
      Sites.xml
      Vehicles.xml
      WorldNpcLogistics.xml
      missions/
        README.md
        port_container_handler.xml
        quarry_heavy_machinery.xml
    LSOL_Addons/
      some.author.package/
        addon.xml
        content/
          missions/
            packaged_contract.xml
          resources/
            extra_resources.xml
          sites/
            extra_sites.xml
          vehicles/
            extra_vehicles.xml
          office-objects/
            extra_office_objects.xml
        assets/
        bin/
```

Notes:

- `LSOL_Config/Dealership.xml` is required for the personal vehicle dealership.
- `LSOL_Config` remains the base runtime content root for player-editable LSOL data.
- `LSOL_Addons` is the runtime add-on package root scanned next to `LSOL_Config`.
- Mission packs are loaded from both `LSOL_Config/missions/*.xml` and `LSOL_Addons/*/content/missions/*.xml`.
- Additive packaged content is currently supported for `resources`, `sites`, `vehicles`, and `office-objects`. Base LSOL content loads first, add-on fragments load second, and duplicate IDs are rejected instead of overridden.
- Valid packaged add-ons require `addon.xml` with API/version compatibility metadata, capabilities, and content directory declarations.
- Plugin metadata may be present in `addon.xml`, but LSOL does not load third-party DLLs in this version.
- Root `configs/` and root `missions/` are legacy reference folders and are not loaded at runtime.

## Add-on Packages

LSOL now supports manifest-based add-on packages rooted in `scripts/LSOL_Addons`.

Current packaged content support:

- `content.missions`
- `content.resources`
- `content.sites`
- `content.vehicles`
- `content.officeObjects`

Current package behavior:

- Each package must contain `addon.xml` at its root.
- Package discovery is deterministic and dependency-aware.
- Invalid manifests fail soft: one broken add-on does not stop LSOL from loading the rest.
- Missing dependencies, declared conflicts, duplicate add-on IDs, duplicate fragment IDs, and unsupported mission types are surfaced as validation warnings.
- Future-facing manifest metadata for UI/module/plugin tiers is accepted, but non-file capabilities are not loaded yet.

See `LSOL_Addons/README.md` for authoring details and `LSOL_Addons_examples/sample.author.mission-pack/` for a minimal packaged mission example.

## Controls

Default key bindings:

- `F8`: Company Hub / dashboard
- `F7`: Game Mod Control menu and saving options
- `F6`: Context panel
- `F9`: Debug menu
- `E`: Interact with property markers, industry tablets, and nearby gates/doors
- `U`: Legacy upgrade key
- `Up / Down`: Menu navigation
- `Left / Right`: Change values in menus
- `Enter`: Select / confirm
- `Backspace` or `Esc`: Go back / close

These keys can be changed through `LSOL.ini`.

## First-Time Setup For A New Player

If this is your first time using LSOL, do this:

1. Launch GTA V with the mod installed.
2. Open the `F7` Game Mod Control menu.
3. Go to Saving Options.
4. Create a named save if you want a clean profile.
5. Choose a starting balance that fits the kind of progression you want.
6. Visit an office blip on the map.
7. Rent or buy an office.
8. Visit the commercial dealership.
9. Buy your first working truck setup.
10. Start hauling cargo between industries.

If you want faster early progression, create a new save with a larger starting balance.

## How To Start Playing

### Step 1: Get An Office

You need an office to begin using the commercial business systems properly.

From the office menu you can:

- rent the office,
- purchase it permanently,
- activate it as your current company office,
- manage worker selection,
- access the commercial garage,
- hire NPC drivers.

### Step 2: Buy A Commercial Vehicle

Go to the commercial dealership blip.

There you can:

- filter by cargo type,
- choose the cargo body or trailer,
- choose a truck tractor when needed,
- see pricing while cycling vehicles,
- purchase the selected setup into your office garage.

### Step 3: Retrieve The Vehicle

Open your office menu and enter the commercial garage.

Vehicles can be:

- stored,
- deployed,
- moved to reserve,
- activated in the garage,
- deployed at an industry when the flow allows it.

### Step 4: Run Deliveries

Drive to an industry marker and press `E`.

Use the industry tablet to:

- load available cargo,
- unload cargo at the destination,
- review the site,
- purchase the site when required,
- buy upgrade modules.

### Step 5: Grow The Company

Once you are earning consistent money, expand into:

- better offices,
- more commercial vehicles,
- owned industries,
- apartments,
- personal vehicles,
- NPC routes,
- special missions.

## Menus Explained

### F7 Game Mod Control

Use this menu for management settings outside the world markers.

This is where you can usually access:

- saving options,
- difficulty settings,
- general mod options.

### F8 Company Hub

Use this to review your company from anywhere.

The company hub is the best place to:

- inspect business status,
- review locations,
- check analytics,
- browse mission content.

## Property Systems

### Office Rules

- Offices can be rented or purchased.
- One office is your active office.
- Active office capacity controls how many commercial vehicles can stay in the active garage.
- Missed payments can suspend access until arrears are paid.

### Apartment Rules

- Apartments must be purchased before they can be used.
- One apartment is your active home.
- Your active apartment controls your personal garage and home interior access.
- Apartments also use rent/arrears logic.

## Saving And Persistence

LSOL stores progression in `.state.ini` files.

### Default Save

If you do not use named saves, LSOL uses:

- `LSOL.state.ini`

This file is stored beside `LSOL.ini`.

### Named Saves

Named saves are stored in:

- `LSOLSaves/`

That folder is also created beside `LSOL.ini`.

From the save menu, you can:

- create a new save,
- load a save,
- delete a save,
- manually save the current game.

Named saves preserve gameplay state such as:

- profit,
- owned industries,
- offices and apartments,
- commercial and personal vehicles,
- NPC logistics,
- special mission progress,
- analytics history,
- difficulty settings tied to that save.

Creating a new named save resets the gameplay world and starts a fresh profile.

## Special Missions

The mission board now combines built-in rotating contracts with optional loose mission packs loaded from `LSOL_Config/missions/*.xml` and packaged mission add-ons loaded from `LSOL_Addons/*/content/missions/*.xml` next to `LSOL.ini`.

If no XML mission packs are installed, the mission board still surfaces generated district crisis jobs, weekly tenders, and priority runs once your company has enough territory presence and fleet capability.

The included mission documentation in `LSOL_Config/missions/README.md` is mainly for mission authors, but as a player you only need to know this:

- mission packs are `.xml` files,
- the rotating board also generates contracts from your district footprint, corridors, and fleet,
- they load automatically on startup,
- they can unlock new jobs and rewards,
- their progress is saved with your company.

## Building From Source

If you want to build LSOL yourself:

```powershell
dotnet build LSOL.csproj -c Release
```

Expected output:

- `bin/Release/net48/LSOL.dll`

This is mostly for modders and contributors. Normal players can use a packaged build instead.

## Troubleshooting

### The mod does not load

Check these first:

- ScriptHookV is installed correctly.
- ScriptHookVDotNet v3 is installed correctly.
- `LemonUI.SHVDN3.dll` is present.
- `LSOL.dll` is in the `scripts` folder.
- `LSOL.ini` is in a supported location.

### Industry tablet does not open

- Make sure you are close enough to an industry marker.
- Press `E` while on foot.
- Check whether your control bindings were changed in `LSOL.ini`.

### Property or dealership systems are missing

- Make sure the `LSOL_Config` folder is next to `LSOL.ini`.
- Make sure `LSOL_Config/Dealership.xml` exists if you want the personal vehicle dealership.
- If map blips seem wrong, confirm the external config files were copied with the mod.

### No special missions appear

- Make sure `LSOL_Config/missions` exists next to `LSOL.ini` if you are using the loose runtime mission path.
- Make sure any packaged mission add-ons are installed under `LSOL_Addons/<package-id>/` with a valid `addon.xml` and `content/missions` directory.
- Make sure your mission files contain valid `.xml` mission pack definitions.

### Build succeeds but the DLL is not updated

If you build from source while GTA V or another process is using the DLL, the file can be locked.

Close the game or any process holding the file, then build again.

## Recommended New Player Mindset

Do not try to understand every menu immediately.

Start with this small goal:

1. rent an office,
2. buy one truck,
3. learn one delivery route,
4. save your game,
5. expand from there.

LSOL is much easier to learn when treated as a small trucking business first and a full logistics empire second.

## Project Name

Internal project/build name:

- `LSOL`

Common name used in this README:

- Los Santos Overloaded Logistics
