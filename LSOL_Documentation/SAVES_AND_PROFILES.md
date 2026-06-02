# Saves And Profiles

LSOL supports both a default save path and named save slots.

If you want one long-running company, the default path is enough. If you want separate careers, challenge runs, or difficulty setups, named saves are the right tool.

## Where Saves Live

Default save:

- `scripts/LSOL/LSOL.state.xml`

Named saves:

- `scripts/LSOL/LSOLSaves/<name>.state.xml`

Both live in the same runtime area as your active LSOL install. Legacy `<name>.state.ini` files can still be loaded if no XML save exists for that slot.

## How To Manage Saves

Open `F7` and go to Saving Options.

From there you can:

- create a new save
- load a save
- delete a save
- manually save the current game for an immediate checkpoint

## Named Save Autosave

Named saves now auto-update in the background during key career events such as completed deliveries, contract or mission state changes, weekly finance ticks, and periodic runtime checkpoints.

The Save game action still exists if you want to force an immediate write before swapping slots or testing something risky.

The default `LSOL.state.xml` path keeps its existing fallback role. Runtime autosave targets the active named save only.

![image](../doc_images/f7_save.JPG)

## Creating A New Save

When you create a named save, LSOL does more than just write a file.

It also:

- resets industries to their default world state
- asks for a starting balance
- stores the save as a fresh new profile
- seals the difficulty profile for that slot

The current starting-balance flow supports values from `-5000` to `100000` in `5000` steps.

![image](../doc_images/f7_new_save.JPG)

## Difficulty Locking

Once a named save is created or loaded, top-level difficulty settings stay tied to that profile.

If you want a different difficulty setup, create a new save instead of trying to repurpose an old one.

![image](../doc_images/f7_difficulty_option_message.JPG)

## What Persists

LSOL saves much more than cash and owned sites.

Persistent data includes things like:

- balance and general company state
- owned industries and site state
- office and apartment ownership or rental access
- commercial and personal vehicles
- NPC logistics contracts, payroll timing, and route state
- district and corridor progression
- doctrine, prestige, and HQ-related progress
- mission progress
- analytics and business-history data used by the hub

## Default Save Versus Named Save

Use the default save if you only want one company.

Use named saves if you want:

- separate career runs
- one easy and one hard profile
- a sandbox company and a serious progression company
- clean testing of different starting balances

## Read Next

- INSTALLATION_AND_FIRST_STEPS
- SUCCESSES_PRESTIGE_AND_DOCTRINE