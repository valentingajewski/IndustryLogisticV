---
description: "Use when: implementing the LSOL towing side job (tow truck, damaged vehicle spawns, garage delivery, towing XP + cash reward)."
agent: "agent"
argument-hint: "Optional tweaks: spawn density, reward values, tow mechanic"
---

# Implement LSOL Towing Side Job

Add a self-contained **towing** side job: the player drives a tow truck, recovers damaged
vehicles spawned around the map, and delivers them to a garage for **cash + Towing XP**.

Reuse the existing skill system — do NOT create a new progression store.

## Existing anchors (reuse, don't duplicate)

- Skill system — `src/Systems/PlayerSkillSystem.cs`
  - `PlayerSkillId.Towing`, `AddXp(id, amount, PlayerSkillXpSource.Player)`, `GetLevel(id)`, `GetBonusMultiplier(id)`.
  - XP is **player-source only** (`AddXp` ignores non-player sources).
- Job vehicle catalog — `LSOL_Config/SideJobs/JobVehicles.xml`
  - `type="TowTruck"` entries with `model`, `unlockLevel`, `price`, `dailyRent`, `fuelCapacityLiters`, `towCapacityTons`.
- Job coordinates — `LSOL_Config/SideJobs/JobCoordinates.xml`
  - `JobPoint` with `job="TowTruck"` (function `TowDropoff`) holds the dropoff and the tow-truck spawn position (`x y z heading`).
- Vehicle catalog — `LSOL_Config/Vehicles.xml` (source list for damaged-vehicle spawns).
- Player feedback — use the mod's standard top-left help/status box (above the minimap), not a new HUD element.

## 1. Config / data model

1. `LSOL_Config/Vehicles.xml`: add a `VehicleWeight` attribute (tons, range **1–3**) to **every vehicle except bikes**.
   - Bikes carry no weight and are excluded from damaged-vehicle spawning.
2. `JobVehicles.xml`: towing entries already exist; verify each `TowTruck` has `unlockLevel` (skill gate) and `towCapacityTons` (max towable weight).
3. `JobCoordinates.xml`: `TowTruck` JobPoints define dropoff + tow-truck spawn. Support **multiple** garages — more dropoff points will be added later, so always resolve the nearest dropoff dynamically; never hard-code a single point.

## 2. Tow-truck spawn (player-facing gate)

- Spawn/claim the tow truck at a `TowTruck` JobPoint.
- **Gate 1 — skill:** if `PlayerSkillSystem.GetLevel(PlayerSkillId.Towing) < towVehicle.unlockLevel`, the player cannot spawn that tow truck (show why).
- **Gate 2 — capacity:** the tow truck's `towCapacityTons` limits which vehicles it can tow (see §4).

## 3. Damaged vehicle spawning (world)

- Damaged vehicles spawn **from time to time**, next to the road, with a **damaged engine** (engine health low, smoking/damaged state).
- Spawn source = `Vehicles.xml`, **excluding bikes**, weighted to keep a **balanced mix of light and heavy** vehicles — do not flood the map with only heavy or only light.
- **Density:** max **2–4** damaged vehicles active at once; refresh the set **every few minutes**. Never a wall of blips — the player should take one or two and come back later.
- Respect the concurrent cap + respawn cooldown; despawn stale, untouched spawns.
- Blip for each damaged vehicle uses the **`tow_truck`** icon.

## 4. Tow interaction

- Player drives the tow truck to a damaged vehicle and tows it.
- **Tow mechanic:** attach the damaged vehicle to the tow truck with a **semi-physical offset** (entity attach behind the truck at a fixed offset); it follows the truck. Do NOT implement real trailer/hitch physics.
- **GPS:** the moment a damaged vehicle is attached, start a GPS route to the **nearest** `TowDropoff` point. More dropoff points will be added later — always resolve the **nearest** dropoff dynamically, never hard-code a single garage.
- **Weight check:** if the damaged vehicle's `VehicleWeight` **exceeds** the tow truck's `towCapacityTons`, refuse the tow and show a **message in the left box above the minimap** (e.g. "Vehicle too heavy for this tow truck").
- Weight must be **visible** wherever the player inspects a damaged vehicle (map-view vehicle selection) so they can pick a compatible target.

## 5. Delivery + reward

- Deliver the towed vehicle to a `TowDropoff` garage.
- On successful delivery:
  - Award **Towing XP** via `PlayerSkillSystem.AddXp(PlayerSkillId.Towing, xp, PlayerSkillXpSource.Player)` — **75–150 XP**, scaled by vehicle weight.
  - Award **cash** — **$500–$1,500**, scaled by vehicle weight (heavier = more), using the mod's existing `AddProfit(...)` path so the finance tracker records it.
- Apply the existing Towing payout bonus (`GetBonusMultiplier`) consistently with other jobs.

## 6. Balance constraint (hard requirement)

Towing must **NOT** be the most profitable job in the mod — it is a **side job**.
Tune cash + XP so towing income per hour stays clearly below core trucking/commodity deliveries.
Prefer modest, weight-scaled payouts over large flat rewards.

## 7. Blips

| Target | Icon |
|--------|------|
| Tow dropoff garage | `radar_arena_work` |
| Damaged vehicle | `tow_truck` |

Resolve these via the mod's blip system (`BlipLifecycleManager`). If the `BlipSprite` enum lacks
these names, fall back to the raw sprite hash/name lookup (the SHVDN `Hash` enum can omit valid entries).

## 8. Persistence

- Persist active tow state (current damaged-vehicle set / cooldowns / player's active tow) in a save section following the existing persistence pattern (see `IndustryPersistenceManager` + `LSOLScript.Persistence.cs`).
- Legacy saves with no towing section must load safely.

## 9. Acceptance checklist

- [ ] `VehicleWeight` (1–3 t) present on every non-bike vehicle in `Vehicles.xml`; bikes excluded from spawning.
- [ ] Damaged vehicles spawn occasionally near roads, damaged engine, `tow_truck` blip; **2–4 active, refreshed every few minutes**, balanced weight mix.
- [ ] Dropoff garage blip uses `radar_arena_work`.
- [ ] Tow truck cannot spawn below its `unlockLevel`.
- [ ] Towing a vehicle above the truck's `towCapacityTons` is refused and shows a left-top message.
- [ ] Vehicle weight is visible in map-view vehicle selection.
- [ ] Towing uses a semi-physical offset attach (towed vehicle follows the truck).
- [ ] Attaching a damaged vehicle sets GPS to the **nearest** dropoff — dynamic, supports multiple/later-added garages.
- [ ] Delivery awards Towing XP (**75–150**, weight-scaled) + cash (**$500–$1,500**, weight-scaled), recorded in the finance tracker.
- [ ] Towing income is clearly below core delivery income.
- [ ] Towing state persists and legacy saves load safely.

## 10. Out of scope

- Adding new job types other than towing.
- Changing the trucking/commodity economy.
- New HUD elements (use the existing top-left box).
