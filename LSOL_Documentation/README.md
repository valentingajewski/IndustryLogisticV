# LSOL Player Documentation

LSOL turns GTA V into a logistics, property, and company-management game.

You start with a small operation, move cargo between industries, build district presence, expand into offices and apartments, automate routes with hired NPCs, and eventually manage a much larger network with budgets, doctrine, prestige, missions, and add-on content.

This folder is the player-first documentation set. It is organized by the systems you actually use in game instead of by source files or config catalogs.

## Start Here

If you are new to LSOL, read these in order:

1. [INSTALLATION_AND_FIRST_STEPS.md](INSTALLATION_AND_FIRST_STEPS.md)
2. [INDUSTRY_SITES_AND_DELIVERIES.md](INDUSTRY_SITES_AND_DELIVERIES.md)
3. [OFFICES_GARAGES_AND_SUPPORT_SITES.md](OFFICES_GARAGES_AND_SUPPORT_SITES.md)
4. [COMMERCIAL_VEHICLES_FUEL_AND_CARGO.md](COMMERCIAL_VEHICLES_FUEL_AND_CARGO.md)
5. [DISTRICTS_AND_CORRIDORS.md](DISTRICTS_AND_CORRIDORS.md)
6. [NPC_LOGISTICS_AND_WORLD_DISPATCH.md](NPC_LOGISTICS_AND_WORLD_DISPATCH.md)

## Quick Start

The shortest reliable opening sequence is:

1. Install LSOL with `LSOL_Config` beside `LSOL.ini`.
2. Launch GTA V and open the `F7` Mod Control menu.
3. Create a save if you want a clean named profile.
4. Visit an office marker and rent or buy your first office.
5. Visit the commercial dealership and buy a starter truck or trailer setup.
6. Drive to an industry marker and press `E` to open the industry tablet.
7. Load cargo, deliver it, and start building money and district influence.

## Runtime Content Roots

These folder roles matter when you install content:

- `LSOL_Config/` is the active base runtime content root.
- `LSOL_Addons/` is the active packaged add-on root.
- `LSOL_Config/missions/` is the active loose mission-pack folder.
- Root `configs/` and root `missions/` are legacy reference folders and are not runtime-loaded.

If you only remember one rule: place live content beside `LSOL.ini`, not in the repository's legacy reference folders.

## Default Controls

These are the shipped defaults:

- `F8`: Company Hub
- `F7`: Mod Control menu
- `F9`: Debug menu
- `E`: Interact with industry tablets, property markers, dealerships, banks, office-object facilities, mission prompts, and apartment or garage interactions
- `U`: Legacy upgrade shortcut used by older interaction flows
- `Up / Down / Left / Right`: Menu navigation and selectors
- `Enter`: Confirm or drill into a selection
- `Backspace`: Back out of a menu or close a page

## Documentation Map

- [INSTALLATION_AND_FIRST_STEPS.md](INSTALLATION_AND_FIRST_STEPS.md): install layout, first launch, controls, and your first playable session
- [INDUSTRY_SITES_AND_DELIVERIES.md](INDUSTRY_SITES_AND_DELIVERIES.md): site types, permits, ownership, tablets, upgrades, and delivery flow
- [DISTRICTS_AND_CORRIDORS.md](DISTRICTS_AND_CORRIDORS.md): district growth, reputation, charters, support sites, and corridor progression
- [OFFICES_GARAGES_AND_SUPPORT_SITES.md](OFFICES_GARAGES_AND_SUPPORT_SITES.md): offices, commercial garages, office objects, depots, and support-site specialization
- [COMMERCIAL_VEHICLES_FUEL_AND_CARGO.md](COMMERCIAL_VEHICLES_FUEL_AND_CARGO.md): truck buying, deployment, fuel, cargo condition, maintenance, and resale pressure
- [APARTMENTS_AND_PERSONAL_VEHICLES.md](APARTMENTS_AND_PERSONAL_VEHICLES.md): apartment ownership or rental, personal garages, motels, and personal cars
- [NPC_LOGISTICS_AND_WORLD_DISPATCH.md](NPC_LOGISTICS_AND_WORLD_DISPATCH.md): hiring drivers, route automation, district and corridor blockers, and ambient dispatch
- [COMPANY_HUB_AND_MANAGEMENT_APPS.md](COMPANY_HUB_AND_MANAGEMENT_APPS.md): what the F7 and F8 interfaces do and when to use each one
- [COMPANY_MAP_AND_ROUTE_PLANNER.md](COMPANY_MAP_AND_ROUTE_PLANNER.md): metro-style district map, planner overlays, route ranking, and map handoffs
- [SPECIAL_MISSIONS_AND_JOB_BOARD.md](SPECIAL_MISSIONS_AND_JOB_BOARD.md): built-in rotating contracts and optional mission-pack content
- [SAVES_AND_PROFILES.md](SAVES_AND_PROFILES.md): default saves, named slots, save files, and what persists
- [SUCCESSES_PRESTIGE_AND_DOCTRINE.md](SUCCESSES_PRESTIGE_AND_DOCTRINE.md): late-game identity, HQ progression, and doctrine-driven play
- [ADDONS_AND_MISSION_PACKS.md](ADDONS_AND_MISSION_PACKS.md): how players install loose mission packs and packaged add-ons safely

## Authoring References

These two files stay outside this folder because they are primarily for content authors:

- [../LSOL_Addons/README.md](../LSOL_Addons/README.md)
- [../LSOL_Config/missions/README.md](../LSOL_Config/missions/README.md)