# Company Map And Route Planner

The Company Map and Route Planner are LSOL's two best strategy surfaces once the company is larger than a few manual routes.

They are connected. The planner ranks lanes, and the map shows how those lanes sit inside the district and corridor network.

## Company Map

The Company Map is a metro-style network view of LSOL's districts.

It is built to show:

- district nodes
- corridor links
- district activity and influence state
- live corridor pressure
- planner overlays on top of the network

This is the fastest way to understand whether your company is growing in a coherent shape or just scattering deliveries randomly across the map.

## What You See On The Map

The map focuses on strategic signals instead of literal road geometry.

Expect to read:

- node size and emphasis by district importance
- corridor thickness and color as a cue for corridor strength or risk
- district detail cards that summarize current activity
- planner overlays that distinguish selected, recommended, live, underperforming, and blocked lanes

## Basic Controls

The map is designed around the same keyboard-driven flow as the rest of LSOL.

- use the arrow keys to move around district selections
- use `Enter` to drill into a district or planner detail
- use `Backspace` to return

## What The Detail Panel Is For

The detail side of the Company Map is there to explain why a district feels strong or weak.

It surfaces things like:

- summary state
- weekly activity
- hot or urgent signals
- corridor context

If you see a district underperforming, the detail card is where the map stops being decorative and starts being actionable.

## Route Planner

The Route Planner ranks candidate lanes for either player hauling or NPC automation.

It helps you compare:

- route value
- blockers
- availability
- commodity focus
- current live-contract overlap

That makes it useful both before you hire NPCs and after you already have a working fleet.

## Planner Filters And Handoffs

The planner supports selection and filtering around:

- sort mode
- lane availability
- commodity family

And it can hand routes off into:

- a Company Map view
- an NPC draft flow
- GPS-style route setup depending on the context

If you are not sure what to automate next, start in the planner, not in the hire menu.

## Best Way To Use Both Together

This sequence works well:

1. open the planner and look for a promising lane
2. inspect blockers if the lane is unavailable
3. send the lane to the Company Map
4. confirm the district and corridor picture actually supports the plan
5. then decide whether to drive it yourself or automate it

## Read Next

- [DISTRICTS_AND_CORRIDORS.md](DISTRICTS_AND_CORRIDORS.md)
- [NPC_LOGISTICS_AND_WORLD_DISPATCH.md](NPC_LOGISTICS_AND_WORLD_DISPATCH.md)