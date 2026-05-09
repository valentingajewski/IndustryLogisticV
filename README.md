# Los Santos Overloaded Logistics (LSOL)

LSOL turns GTA V into a logistics and industry management game.

You start small, move cargo between industries, earn money from deliveries, expand your fleet, buy property, automate routes with NPC drivers, and unlock more advanced company systems over time.

This README is written for players first. It explains what the mod does, how to install it, and how to get started without needing to read the code.

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
- Optional custom special missions loaded from the `missions` folder.

In short: you are building a logistics company inside GTA V.

## Main Gameplay Loop

The usual LSOL progression looks like this:

1. Rent or buy an office.
2. Buy a commercial truck or trailer combo from the commercial dealership.
3. Drive to an industry marker and use the industry tablet to load cargo.
4. Deliver that cargo to another site that accepts it.
5. Earn money.
6. Expand into better offices, more vehicles, owned industries, apartments, NPC drivers, and special missions.

The mod becomes deeper as you go. Early on it feels like trucking and delivery management. Later it becomes a wider company simulator with ownership, automation, analytics, and mission content.

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
- available mission content,
- current company state.

### Special Missions

LSOL supports special community mission packs loaded from the `missions` folder.

These missions can add custom jobs such as:

- trailer deliveries,
- container handling jobs,
- heavy machinery transport-style tasks.

Mission progress also saves with the rest of your company.

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
6. Copy the full `configs` folder next to `LSOL.ini`.
7. If you want special missions, copy the `missions` folder next to `LSOL.ini`.

### Supported `LSOL.ini` Locations

LSOL looks for `LSOL.ini` in these locations:

1. next to the compiled LSOL assembly,
2. GTA V root folder,
3. GTA V `scripts` folder.

Recommended location:

- `GTA V/scripts/LSOL.ini`

### Important Folder Layout

If `LSOL.ini` is in `GTA V/scripts/`, the mod expects these folders beside it:

```text
GTA V/
  scripts/
    LSOL.dll
    LSOL.ini
    LemonUI.SHVDN3.dll
    configs/
      dealership.xml
      Districts.csv
      Offices.csv
      Interiors.csv
      Vehicles.csv
      ...
    missions/
      README.md
      port_container_handler.ini
      quarry_heavy_machinery.ini
```

Notes:

- `configs/dealership.xml` is required for the personal vehicle dealership.
- The loose `configs` folder is where LSOL reads player-editable data from.
- Mission packs are loaded from the `missions` folder next to `LSOL.ini`.

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

### F6 Context Panel

Use this while driving or working.

It shows useful live information such as:

- current cargo,
- vehicle fuel,
- nearby industry information.

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

Mission packs are loaded from the `missions` folder next to `LSOL.ini`.

If no mission packs are installed, the company hub mission area will simply show no mission content.

The included mission documentation in `missions/README.md` is mainly for mission authors, but as a player you only need to know this:

- mission packs are `.ini` files,
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

- Make sure the `configs` folder is next to `LSOL.ini`.
- Make sure `configs/dealership.xml` exists if you want the personal vehicle dealership.
- If map blips seem wrong, confirm the external config files were copied with the mod.

### No special missions appear

- Make sure the `missions` folder is next to `LSOL.ini`.
- Make sure it contains valid `.ini` mission pack files.

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
