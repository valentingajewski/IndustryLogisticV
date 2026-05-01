# Industry Logistic V - Player Guide

This guide explains only gameplay: how to play, where resources come from, and how the industry chain works.

## 1. Main Objective

Build profit by moving commodities through the logistics network:

1. Load resources at producing industries.
2. Deliver to processing industries.
3. Sell finished goods to stores, terminal, and petrol stations.
4. Upgrade industries to improve throughput.

## 2. Quick Start (First 5 Minutes)

1. Go to the Logistics Office marker (near the configured MainOffice location).
2. Press `F7` to open the Game Mod Control menu.
3. Set **Activate** to **On** (it starts Off by default).
4. At the office marker, press `E` to open the logistics menu.
5. Pick a cargo filter + vehicle and spawn it.
6. Drive to an industry marker and press `E` to open the industry tablet.
7. Load cargo, deliver it to the next step in the chain, unload, repeat.
8. While carrying cargo, watch the minimap-side cargo overview: collisions reduce cargo condition and can spill part of the load.

## 3. Default Controls

- `F7`: Open Game Mod Control menu (Activate, Industry persistence, mode, difficulty)
- `E`: Interact with office/industry terminal
- `E` (GateInteract): Open nearby gates/barriers
- `F8`: Dashboard overview
- `F6`: Context panel
- Menu navigation: `Up/Down/Left/Right`, `Enter`, `Backspace`

## 4. Cargo Types (Match Commodity to Vehicle)

- **Fluid**: Oil, Fuel, Omega
- **Crate**: Electronic, TV, Computer
- **Solid**: Metal, Alloy
- **Loose**: Ore, Coal, Plastic, Recyclable

Tip: if cargo type and vehicle type do not match, loading options will be limited or unavailable.

## 5. Where to Find Raw Resources

### Primary extraction nodes

- **Ore Loading Area** -> Ore
- **Coal Loading Area** -> Coal
- **Grand Senora Desert Oil Field** -> Oil
- **El Burro Heights Oil Field** -> Oil

### Core processing nodes

- **Palmer-Taylor Refinery**: Oil -> Plastic + Fuel
- **Cypress Flats Smelting Factory**: Ore + Coal -> Metal + Alloy
- **Cypress Flats Processor Factory**: Plastic + Alloy -> Electronic
- **Consumer Electronics Factory**: Electronic -> TV + Computer + Recyclable
- **La Puerta Recycling Center**: Recyclable -> Metal + Plastic + Alloy
- **Rancho Omega Factory**: Coal + Ore -> Omega

## 6. Delivery Targets (Profit Sinks)

- **Stores** (TV/Computer demand):
  - Davis Carson Store
  - Burton Mall
  - Sandy Shores Carson Store
  - Grapeseed Store
  - Paleto Store
- **Terminal**: accepts multiple commodities (bulk sink)
- **Petrol Stations**: accept Fuel and continuously drain stock over time (repeat demand)

Use `F8` dashboard to identify who needs what, and to monitor fill levels.

## 7. Industry Chain Examples

### Electronics Retail Chain

1. Ore + Coal -> **Metal/Alloy** at Smelting
2. Oil -> **Plastic** at Refinery
3. Plastic + Alloy -> **Electronic** at Processor
4. Electronic -> **TV/Computer** at Consumer Electronics Factory
5. Deliver TV/Computer to Stores

### Fuel Distribution Chain

1. Oil -> **Fuel** at Refinery
2. Deliver Fuel to Petrol Stations

### Omega Boost Support Chain

1. Coal + Ore -> **Omega** at Omega Factory
2. Deliver Omega to industries that support omega boost

### Recycling Loop

1. Recyclable -> **Metal/Plastic/Alloy** at Recycling Center
2. Feed those back into processing chains

## 8. Upgrades and Progression

At an industry tablet/menu, open upgrades to invest profit.

- Production, input/output storage, omega storage are industry-dependent.
- Input-only sink locations (for example stores) do not offer all module types.

Focus early upgrades on bottleneck processors (Refinery, Smelting, Processor, Consumer Electronics).

## 9. Practical Tips

- Start with short routes first (one producer -> one processor).
- Keep an eye on `F8` to avoid delivering to full destinations.
- Use `F6` when near an industry to inspect local stock and omega values.
- The cargo overview HUD appears next to the minimap whenever your truck or trailer carries a resource.
- Heavy vehicle damage reduces cargo condition and can destroy part of the load, so rough driving directly lowers final payout.
- Keep mod mechanics **On** in F7, otherwise production/interaction systems stay disabled.

## 10. Industry Persistence

Industry persistence lets your industry values continue between sessions.

- Open `F7` and set **Industry persistence** to **On**.
- When you log off / the script unloads, industry state is saved automatically.
- On next session, saved values are loaded automatically.
- This includes important industry progression values such as stock levels, omega storage, capacities, production rate, and upgrade levels.

If you set Industry persistence to Off, no save is written on logout.
