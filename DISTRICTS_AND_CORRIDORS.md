# Districts, Reputation, and Corridors

This guide explains the strategic territory system in LSOL in player terms.

If you open the company hub and see district influence, reputation labels, or corridor progress, this is the system behind it.

## The Short Version

- Every industry belongs to a district.
- Your work inside a district builds influence and reputation there.
- Cross-district deliveries also build corridor rights between districts.
- Stronger districts improve your company network and unlock more advanced logistics behavior.
- NPC logistics now also requires the district used by the route to be at least `Established`.

## What A District Is

A district is a region of the map, such as Port, Grand Senora, Downtown, or East Los Santos.

LSOL tracks a live strategic state for each district. That state is built from the sites inside it and from the corridors connected to it.

Each district keeps track of:

- how many sites it contains,
- how many of those sites you control,
- how many are operational,
- how many controlled depots you have there,
- how many service sites reached franchise status,
- how many route rights it gained from corridors,
- its influence score,
- its influence ratio,
- its reputation score,
- and its current reputation label.

## Influence: How District Strength Grows

Influence is the main measure of how established your company is in a district.

LSOL builds district influence from several sources:

- starter headquarters presence,
- leasing or owning sites,
- making sites operational,
- completed load and unload runs,
- total delivered tonnage,
- franchise progress at stores, gas stations, and construction sinks,
- depot staff and support presence,
- and corridor rights connected to that district.

In code, the district does not use a flat score target. Instead, the raw influence score is divided by a district target to get an `InfluenceRatio`.

That target grows with district size, so larger districts need more work to dominate than smaller ones.

## Important Influence Breakpoints

These breakpoints matter the most to players:

- `60% influence` means the district counts as controlled in the strategic overview.
- `60% influence` is also the point where the UI starts describing the district as established enough to support local fleet privileges and better delivery terms.
- `85% influence` is used for spawn-right checks on non-support sites that have a vehicle spawn position.

So if a district still feels weak, the first big target is usually `60%`, not `100%`.

## Reputation: The Label You See In The UI

Reputation is shown as a label, not just a raw number.

LSOL calculates the district label from this combined score:

```text
district score = (influence ratio * 100) + reputation score
```

The labels are:

- `Unknown`: below `28`
- `Emerging`: `28+`
- `Established`: `60+`
- `Anchored`: `90+`
- `Dominant`: `125+`

The reputation score itself mainly comes from:

- total deliveries,
- delivered tonnage,
- extra credit for construction-site work,
- and whether sites are operational.

Corridor rights also add a smaller amount of reputation to both connected districts.

## Why `Established` Matters

NPC logistics is currently tied to district maturity.

To begin hiring NPC routes involving a district, that district must be at least `Established`.

That means `Emerging` is not enough. If a district is still `Unknown` or `Emerging`, build it up with more deliveries, more active sites, and more corridor progress.

## Corridors: What They Represent

A corridor is the link between two different districts.

Whenever you complete a delivery from one district into another district, LSOL records progress on that corridor.

Same-district deliveries do not build corridor rights. Cross-district deliveries do.

Each corridor stores:

- the two connected districts,
- total cross-district delivery count,
- total cross-district delivered tonnage,
- and the current corridor-right level.

## Corridor Levels

There are three corridor levels above `None`:

- `Service Permit`: at least `2` deliveries or `30` tons
- `Corridor`: at least `5` deliveries or `90` tons
- `Priority`: at least `10` deliveries or `180` tons

You do not need to meet both numbers. Reaching either the delivery count threshold or the tonnage threshold is enough.

## What Corridors Actually Do

Corridors are not just visual progress. They affect gameplay in several ways.

### 1. They unlock cross-district NPC routes

If two industries are in different districts, NPC route creation checks whether that corridor has any rights unlocked.

No corridor rights means the cross-district NPC route is blocked.

### 2. They make both districts stronger

Each corridor level adds strategic weight to both connected districts:

- `+4 influence score` per corridor-right level
- `+2.5 reputation score` per corridor-right level

This means corridor work helps both ends of your network, not just the destination district.

### 3. They improve NPC cargo reliability

NPC loss ratio is reduced by `8%` per corridor level on that route.

In simple terms:

- better corridor level,
- better route quality,
- less NPC cargo loss.

## Site Control And District Growth

Districts do not only grow from deliveries. Site control matters too.

Support sites such as depots and yards can be:

- unsecured,
- leased,
- or owned.

Once secured, you can assign a crew and later expand staff with loaders, mechanics, guards, and managers.

That support network adds district strength and improves district support bonuses.

Operational sites also matter. A site that is purchased or permitted but never actually activated contributes less than one that is actively being used.

## A Practical Way To Grow A District

If you want to push a district from weak to useful, this order works well:

1. Buy the permit or ownership needed to access the local industries.
2. Run the first real supply or dispatch so the important sites become operational.
3. Make repeated deliveries in that district to build influence and reputation.
4. Secure a depot or yard if one is available, then assign crew.
5. Start linking that district to others with cross-district deliveries to unlock corridor rights.
6. Keep pushing until the district reaches `Established` if you want to use NPC logistics there.

## Common Questions

### Why can I deliver manually but still not hire an NPC there?

The usual blockers are:

- the industry still needs a contractor permit,
- the destination corridor is not unlocked yet,
- or the district is still below `Established`.

### Why does `60%` influence feel important even before the label says `Established`?

Because `60% influence` is already used by the strategic system as a major district-control breakpoint, even though the reputation label is calculated from a combined score and not from influence alone.

### Can a district be high influence but still not high reputation?

Yes. Influence and reputation are related, but they are not the same value. The final label uses both together.

## Rule Summary

- `Influence` measures how much strategic weight your company has in a district.
- `Reputation` turns district performance into the labels `Unknown`, `Emerging`, `Established`, `Anchored`, and `Dominant`.
- `Corridors` are built by cross-district deliveries.
- `Corridor rights` are required for cross-district NPC routes.
- `Established` is the current district gate for NPC logistics.

If you remember only one thing: run real deliveries first, then build corridors, then automate.