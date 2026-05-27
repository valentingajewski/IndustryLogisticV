# Installation And First Steps

This page is for players who want LSOL running correctly on a normal GTA V ScriptHook setup and want a clear first-session path once the mod loads.

## Requirements

You need:

- GTA V in a single-player mod setup
- ScriptHookV
- ScriptHookVDotNet v3
- `LemonUI.SHVDN3.dll`

If any of those are missing, LSOL will not load correctly.

## Recommended Install Layout

The safest install is to keep `LSOL.dll`, `LSOL.ini`, `LSOL_Config`, and `LSOL_Addons` together in GTA V's `scripts` folder.

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
      Offices.xml
      OfficeObjects.xml
      Resources.xml
      Sites.xml
      Vehicles.xml
      WorldNpcLogistics.xml
      missions/
    LSOL_Addons/
      some.author.package/
        addon.xml
        content/
          missions/
          resources/
          sites/
          vehicles/
          office-objects/
```

## What Goes Where

- Put the full `LSOL_Config` folder beside `LSOL.ini`.
- Put loose mission `.xml` files in `LSOL_Config/missions/`.
- Put packaged add-ons in `LSOL_Addons/<package-id>/`.
- Do not place live content in the repository root `configs/` or `missions/` folders. Those are legacy reference folders only.

## Supported `LSOL.ini` Locations

LSOL can resolve `LSOL.ini` from several places, but the recommended location is:

- `GTA V/scripts/LSOL.ini`

Keeping it beside `LSOL_Config` and `LSOL_Addons` avoids most path issues.

## First Launch Checklist

When the mod loads successfully, verify these basics:

- office markers appear in the world
- industry markers appear in the world
- `F7` opens Mod Control
- `F8` opens the Company Hub
- pressing `E` near an office, industry, dealership, bank, apartment, or mission prompt opens the expected interaction

If those do not work, your runtime folder layout is the first thing to check.

## Default Controls

- `F8`: Company Hub
- `F7`: Mod Control menu
- `F9`: Debug menu
- `E`: Interact with nearby systems
- `U`: Legacy upgrade shortcut
- `Arrow keys`: Move through menus and selectors
- `Enter`: Confirm
- `Backspace`: Go back or close

## Your First Play Session

Use this sequence if you want the cleanest start:

1. Launch GTA V with LSOL installed.
2. Press `F7` and open Saving Options.
3. Create a named save if you want a separate profile.
4. Pick the starting balance that matches the pace you want.
5. Visit an office marker and rent or buy an office.
6. Make that office your active office.
7. Visit the commercial dealership.
8. Buy a working truck or trailer setup for the cargo you want to move.
9. Drive to an industry marker.
10. Press `E` to open the industry tablet.
11. Load cargo, deliver it to a valid destination, and start building money and district presence.

## Common Early Problems

### The tablet does not open at an industry

Check these first:

- you are close enough to the marker
- you are pressing `E`
- the site is actually configured in `LSOL_Config`
- the runtime folder still contains `LSOL_Config` beside `LSOL.ini`

### Offices or dealerships do not appear

That usually means the runtime config root is missing or incomplete. Re-check that the full `LSOL_Config` folder is present beside `LSOL.ini`.

### A mission pack or add-on does not show up

Make sure you installed it into a live runtime path:

- loose missions go in `LSOL_Config/missions/`
- packaged add-ons go in `LSOL_Addons/<package-id>/`

Root `missions/` is not runtime-loaded.

## Read Next

- [INDUSTRY_SITES_AND_DELIVERIES.md](INDUSTRY_SITES_AND_DELIVERIES.md)
- [OFFICES_GARAGES_AND_SUPPORT_SITES.md](OFFICES_GARAGES_AND_SUPPORT_SITES.md)
- [SAVES_AND_PROFILES.md](SAVES_AND_PROFILES.md)