# Add-ons And Mission Packs

This page is for players installing extra LSOL content, not for authors building it.

The important thing is understanding which folders are actually live at runtime.

## Live Runtime Paths

LSOL currently loads extra content from two places under `scripts/LSOL/`:

- `scripts/LSOL/LSOL_Config/` for base loose XML content
- `scripts/LSOL/LSOL_Addons/` for packaged add-ons with `addon.xml`

If a pack is not in one of those runtime roots, LSOL will not use it.

## Loose Mission Packs

Loose mission packs are simple `.xml` files placed in:

- `scripts/LSOL/LSOL_Config/missions/`

This is the easiest way to add standalone mission content.

## Packaged Add-ons

Packaged add-ons live in:

- `scripts/LSOL/LSOL_Addons/<package-id>/`

Each package needs its own `addon.xml` manifest.

Current packaged content support includes:

- missions
- resources
- sites
- vehicles
- office objects

That means add-ons can do more than just add contracts. They can extend parts of the wider company ecosystem too.

## What Happens If An Add-on Is Bad

Package loading is designed to fail soft.

In practice:

- one broken add-on should only disable that package
- valid packages can still load
- manifest problems, conflicts, duplicates, or unsupported capabilities are treated as validation issues instead of total startup failure when possible

## Legacy Folders To Ignore

These repository folders are not runtime-loaded:

- root `configs/`
- root `missions/`

They are archive and migration references only.

## Where To Read The Author Docs

If you are installing content made by someone else, you usually do not need the author docs.

If you do want the full file-format rules, read:

- ../LSOL_Addons/README.md
- ../LSOL_Config/missions/README.md

## Read Next

- SPECIAL_MISSIONS_AND_JOB_BOARD
- INSTALLATION_AND_FIRST_STEPS