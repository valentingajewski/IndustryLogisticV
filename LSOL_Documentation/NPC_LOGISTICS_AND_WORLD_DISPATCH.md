# NPC Logistics And World Dispatch

NPC logistics is how LSOL shifts from a hands-on hauling game into a company-management game.

You do not need it early, but once your network is real, it becomes one of the biggest force multipliers in the mod.

## What NPC Logistics Does

Hired NPC drivers can run recurring cargo routes for you.

The system includes:

- driver tiers
- upfront contract costs
- recurring weekly wages
- route automation
- persistence across saves
- driver progression over time

## What You Need Before It Works Well

NPC automation is gated by the real state of your company.

The most common blockers are:

- the relevant district is still below `Established`
- the route crosses districts without corridor rights
- the site still needs contractor permit access
- your office fleet is not in a good state for assignment

The best way to unlock NPC logistics is still the same old rule: haul manually first, automate second.

You will also need a Construction Site Cabin object which can be purchased in the active office object list.

## First Working NPC Setup

The first reliable NPC setup is usually:

1. keep one office active and make sure commercial systems are usable there
2. buy a Construction Site Cabin from that active office's object list
3. keep a compatible truck or tractor-plus-trailer combination available in the active office garage
4. prove the route manually first so you know the sites, cargo, and demand are real
5. save enough money for both the upfront contract fee and the recurring wage

If the hire screen says that no route is available, assume there is a real blocker instead of a hidden random failure.

![image](../doc_images/office_npc_cabine.JPG)

![image](../doc_images/npc_cabin.JPG)

![image](../doc_images/npc_menu.JPG)

## Driver Tiers

Driver tiers load from the hiring config and define the fundamentals of a contract:

- cargo-loss tendency
- driving speed and overall route quality
- pricing multiplier
- weekly wage

![image](../doc_images/npc_level.JPG)

Driver tiers load from `scripts/LSOL/LSOL_Config/HiringNPC.xml`.

The tier you hire is the base contract tier, but drivers also develop effective strength over time from real work. The UI can show that training progress, so a long-serving driver can perform above the raw starting tier that you originally bought.

## Costs

NPC logistics has two money drains you should plan for:

- a one-time contract fee when you hire or rework a route
- an ongoing weekly wage while the contract is active

If you automate a weak route too early, wages can eat the margin before the route becomes useful.

Editing a contract is cheaper than building a full new chain, but route churn is still not free.

## Common Hire-Screen Blockers

The most common reasons a route is rejected are:

- the district still has not reached `Established`
- the route crosses districts without the corridor rights to support it
- a site still lacks the required permit access
- the active office does not currently have commercial systems access
- the truck you want is already reserved by another NPC contract

## Multi-Route Contracts

LSOL supports more than one route per hired NPC contract.

That matters because it lets you build drivers into long-term company assets instead of treating every commodity chain as a separate isolated hire. Office-garage vehicle assignment also follows the route logic more closely now, so mixed-resource chains can keep route-specific truck reservations instead of forcing one rigid vehicle choice across every leg.

![image](../doc_images/npc_route1.JPG)
![image](../doc_images/npc_route2.JPG)

## World Dispatch

`NpcLogisticsManager` also runs an ambient world-dispatch layer.

This is different from your hired drivers.

World dispatch creates background jobs such as:

- shortage response
- overflow relief
- internal balancing work
- rival freight activity
- optional visible convoys

![image](../doc_images/ambiant_fuel.JPG)
![image](../doc_images/ambiant_npc.JPG)

These jobs are meant to stabilize the economy and create world motion. They use lighter simulation rules than your hired contracts, so you can still see background freight activity before every player-facing NPC-route blocker is cleared.

They do not replace manual hauling as your main income source.

## Important World-Dispatch Rule

Ambient world-dispatch jobs intentionally do not pay the player directly.

They use small capped tonnage, timing delays, and optional premium-dispatch fees so the economy feels alive without trivializing the rest of the game.

They also do not build district or corridor progression for you. Ambient traffic is background support, not a shortcut around company progression.

## Premium Dispatch And Policy Controls

The dispatch layer can expose player policy choices such as:

- changing dispatch priority
- focusing a commodity or district
- enabling premium dispatch

Those controls are useful once you care about how background traffic supports or defends the shape of your network.

In practice, these controls live on the `F8` Company Hub Dispatch flow under `World Dispatch`.

That page lets you:

- change the ambient policy between shortage, overflow, and value-oriented behavior
- bias ambient jobs toward one commodity
- bias support traffic toward one district cluster
- toggle premium dispatch for faster policy-matching response at an extra service cost
- open `Dispatch Diagnostics` to review recent ambient pipeline events and failures

The live runtime tuning for this layer comes from `scripts/LSOL/LSOL_Config/WorldNpcLogistics.xml`.

## Good NPC Timing

You usually get the best results from NPC hires when:

1. the district already works manually
2. the corridor is unlocked if the route crosses districts
3. the route has stable demand
4. you can afford both the hire fee and the wage
5. your office fleet is maintained instead of barely holding together

## Read Next

- [DISTRICTS_AND_CORRIDORS.md](DISTRICTS_AND_CORRIDORS.md)
- [COMPANY_MAP_AND_ROUTE_PLANNER.md](COMPANY_MAP_AND_ROUTE_PLANNER.md)
- [SAVES_AND_PROFILES.md](SAVES_AND_PROFILES.md)