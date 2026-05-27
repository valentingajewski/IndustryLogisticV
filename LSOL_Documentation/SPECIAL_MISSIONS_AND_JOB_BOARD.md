# Special Missions And Job Board

LSOL's mission board mixes built-in rotating contracts with optional mission-pack content.

That means you get a mission layer even if you never install a custom XML mission, and you can extend it later if you want more variety.

## What The Board Already Includes

The built-in rotating board can generate contracts such as:

- district crisis relief jobs
- weekly tenders
- priority line-haul runs

These are tied to your company state, district presence, corridor access, and fleet capability.

## Optional Mission Packs

On top of the built-in board, LSOL can load mission XML from two live runtime paths:

- `LSOL_Config/missions/*.xml`
- `LSOL_Addons/*/content/missions/*.xml`

Those mission packs load automatically on startup.

## Supported Mission Types

The current supported mission-pack types are:

- `handler_container_transfer`
- `trailer_delivery`

As a player, that means you can expect both standard trailer-style contracts and more involved cargo-handling jobs.

## What A Handler Container Mission Feels Like

Container-handler missions use a real two-step interaction flow:

1. lift the container onto the handler
2. move it to the trailer and press interact again to secure it

That makes them feel different from normal point-to-point hauling.

## What A Trailer Delivery Mission Feels Like

Trailer-delivery missions are more direct. They focus on bringing the correct trailer to the destination cleanly and reliably.

They work well when you want contract variety without the extra equipment handling of container jobs.

## Mission Progress Persists

Mission progress, active mission state, and completion counts save with the rest of your company.

That includes both built-in rotating content and loaded mission packs.

## Where To Install Extra Mission Content

Use only the live runtime paths:

- loose missions: `LSOL_Config/missions/`
- packaged missions: `LSOL_Addons/<package-id>/content/missions/`

The repository root `missions/` folder is legacy reference content only.

## Read Next

- [ADDONS_AND_MISSION_PACKS.md](ADDONS_AND_MISSION_PACKS.md)
- [SAVES_AND_PROFILES.md](SAVES_AND_PROFILES.md)