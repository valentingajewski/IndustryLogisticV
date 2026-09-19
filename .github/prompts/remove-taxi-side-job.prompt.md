---
description: "Remove the Taxi side job from LSOL end-to-end (system, host wiring, persistence, skills, localization, config, tests) without breaking any other feature"
agent: "agent"
---

# Remove the Taxi side job

Delete **every** trace of the `Taxi` side job from this repository (added 2026-09-18 as a full vertical
slice). After this task, a case-insensitive search for `taxi` must return **zero** code/config hits in
`src/`, `tests/`, `LSOL_Config/`, `configs/` and `missions/`.

Everything else must keep working bit-for-bit: Towing, Garbage, FoodDelivery, Bus, trucking, industry,
NPC logistics, offices, property, banking, districts and the tablet UI.

## Hard constraints

1. **Never delete shared infrastructure just because Taxi was a caller.**
   `src/Systems/SideJobConfigLoader.cs` is shared by Bus, Garbage and Taxi. Keep the file and every
   method that still has a non-taxi caller (`TryLoadDocument`, `ReadPosition`, `LoadDistricts`,
   `ResolveDistrictName`, `EnumerateJobVehicles`, …). Only delete a member if you have proven with
   `vscode_listCodeUsages` **and** a successful build that nothing else uses it. Adjust any doc comment
   that mentions Taxi (e.g. "passenger seats of the type-specific `seats` attribute (Taxi, Bus)").
2. **Do not renumber anything.**
   - `PlayerSkillId` keeps its explicit numeric values (`Trucking = 0`, `Bus = 5`, …). Deleting
     `Taxi = 4` leaves `Bus = 5` untouched — that is intended.
   - `persistenceVersion` in `IndustryPersistenceManager` is a max-ladder (`32` territory, `33` bus,
     `34` taxi, `35` food delivery). Remove only the taxi rung; leave `33` and `35` exactly as they are.
   - `JobVehicle` / `JobPoint` ids in the shipped XML are authored data. Delete taxi rows only; never
     renumber other jobs' ids.
3. **Never hand-edit build or deploy artifacts**: `bin/`, `obj/`, `TestResults/`, `LSOL_files/`
   (mirrored copies), `*-lscache`. They are regenerated.
4. **Do not touch save compatibility beyond staying compilable.** The user starts fresh saves, so no
   migration code is needed — but do not add code that throws or crashes when an old save still
   contains `[Taxi]` sections or unknown keys. Tolerating/ignoring unknown sections is correct.
5. **One change at a time, build as you go.** Do not leave the repo in a non-compiling state between
   layers.

## Step 0 — Discovery (do this first, and again at the end)

`grep_search` is known to produce **false negatives in this repo** (it previously returned zero hits for
a symbol used in 19 places, including in `tests/`). Do not trust a zero result. Use:

```powershell
Set-Location "d:\Personal folders\Programmation\Games\gtav_industry_mgnt"
Select-String -Path "src\**\*.cs","tests\**\*.cs","LSOL_Config\**\*.xml","configs\**\*.csv","configs\**\*.ini","missions\**\*.xml","LSOL_Documentation\**\*.md" -Pattern "taxi" -SimpleMatch | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
```

Also run `vscode_listCodeUsages` on `TaxiSideJobSystem`, `TaxiPersistenceSnapshot`, `TaxiJobId` and
`PlayerSkillId.Taxi` before removing each one.

Known touchpoints to confirm and clear (line numbers as of 2026-09-19 — re-grep, do not trust them):

| Area | Location |
| --- | --- |
| Runtime system | `src/Systems/TaxiSideJobSystem.cs` (delete whole file) |
| Host wiring | `src/LSOLScript.cs` |
| Persistence write/read | `src/Systems/IndustryPersistenceManager.cs` |
| Persistence session | `src/LSOLScript.Persistence.cs` |
| Progression | `src/Systems/PlayerSkillSystem.cs`, `src/UI/SkillsTabletApp.cs` |
| District bonus | `src/Systems/DistrictBonusCatalog.cs` |
| Localization | `src/UI/ModLocalization.Tablet.cs` |
| Shipped config | `LSOL_Config/SideJobs/JobCoordinates.xml`, `LSOL_Config/SideJobs/JobVehicles.xml` |
| Tests | `tests/LSOL.Tests/Systems/TaxiSideJobSystemTests.cs` + taxi refs in 4 other test files |

## Step 1 — Unwire the host (`src/LSOLScript.cs`)

Remove, in this order, every taxi member and call site:

- Field `_taxiSideJobSystem`; the `_pendingTaxiRestore` field.
- Constructor: the `new TaxiSideJobSystem(...)` block, its `AddProfit(CompanyFinanceCategory.OtherIncome, amount, "Taxi fares")` delegate, and its dependency arguments.
- The four LemonMenus `_taxiStandMenu`, `_taxiGarageMenu`, `_taxiDealershipMenu`, `_taxiDispatchMenu` (declarations, construction blocks, `Subtitle` strings, and every `.IsOpen` / `.Close()` / `.HandleKey(...)` / `.Draw()` reference, including the long `IsAnyMenuOpen` OR-chain).
- `OpenTaxiStandMenu`, `CloseTaxiJobMenus`, `ReloadTaxiStandConfig` (`"Reload stand config"` menu entry) and any debug-menu entry that calls them.
- The `<6 m` `TryGetNearestTaxiStand` interact block.
- `SetModMechanicsEnabled` → `_taxiSideJobSystem.SetModMechanicsEnabled(...)`.
- `SetJobEnabled` → `_taxiSideJobSystem.SetJobEnabled(_sideJobEnabled["Taxi"])`.
- `OnTick` → `_taxiSideJobSystem.Update(player, gameTime)`.
- `CancelActiveSideJobs` → the taxi branch.
- `_sideJobEnabled`: the `{ "Taxi", true }` entry **and** the hard-coded `jobIds` array in `RebuildSideJobsMenuItems()` (`new[] { "Towing", "Garbage", "Taxi", "FoodDelivery", "Bus" }`).
- Any taxi case in `GetSideJobDisplayName(...)` or similar switch statements.

## Step 2 — Persistence

`src/Systems/IndustryPersistenceManager.cs`:

- Delete `HasTaxiData`, `WriteTaxiSnapshot`, `ReadTaxiSnapshot` and the `TaxiPersistenceSnapshot`-based helpers.
- Delete the `persistenceVersion = 34;` rung.
- Delete the write branch (`WriteTaxiSnapshot(writer, metadata.Taxi)`), the read branch
  (`metadata.Taxi = ReadTaxiSnapshot(ini)`), the `HasTaxiData(...)` term in `HasGameplayMetadata`, and
  the `SavedGameMetadata.Taxi` property.

`src/LSOLScript.Persistence.cs`:

- Delete the `_pendingTaxiRestore = null;` reset, the `_taxiSideJobSystem.ResetState();` call in
  `ResetSaveSessionState`, the `_pendingTaxiRestore == null` term in the all-pending-null guard, and the
  whole taxi restore branch.

## Step 3 — Progression and UI

- `src/Systems/PlayerSkillSystem.cs`: remove `Taxi = 4` from the enum, the `SkillDefinition` entry
  (`HasLiveXpSource = true`) and the `PlayerSkillId.Taxi` entry in `GetAllSkillIds()`.
- `src/UI/SkillsTabletApp.cs`: remove the `case PlayerSkillId.Taxi: return "tablet.skills.name.taxi";` arm.
- `src/Systems/DistrictBonusCatalog.cs`: remove `TaxiJobId`, its entry in `JobIdList`, its
  display-name branch, and its `GetContributionPointsPerUnit` branch (returning `0.1f`). Fix the
  surrounding doc comment that mentions Taxi.
- `src/UI/ModLocalization.Tablet.cs`: delete **all** `sidejob.taxi.*` keys (≈29) and
  `tablet.skills.name.taxi` **in all 7 languages**. Leave `sidejob.bonus.districtBonus` and every other
  key untouched.

## Step 4 — Shipped config data

- `LSOL_Config/SideJobs/JobCoordinates.xml`: delete every `<JobPoint ... job="Taxi" ...>` (the 5
  `function="TaxiStand"` entries and all ~40 `function="FareDropoff"` entries) plus the `<!-- Taxi -->`
  header comment block and the `Taxi` rows in the file's legend comment.
- `LSOL_Config/SideJobs/JobVehicles.xml`: delete every `type="Taxi"` `<JobVehicle>` row (`Taxi`, `Limo`)
  plus the `<!-- Taxi -->` block and the `Taxi` legend rows. Keep `seats` documented if another job type
  still uses it.
- The files must remain **well-formed XML** (`[xml](Get-Content <file>)` must not throw) and the
  remaining rows keep their original ids.
- Check `configs/` and `missions/` for taxi rows as well.

## Step 5 — Tests

- Delete `tests/LSOL.Tests/Systems/TaxiSideJobSystemTests.cs`.
- Remove/repair taxi references in: `DistrictBonusCatalogTests.cs` (job-id list/count assertions),
  `PlayerSkillSystemTests.cs` (skill count/order/name), `FoodDeliverySideJobSystemTests.cs` (a comment
  comparing drop-offs to taxi), `CompanyMapDistrictBonusFormatterTests.cs`.
- Do not weaken unrelated assertions to make them pass — update the expected values to the new
  (taxi-free) truth.
- **Do not fix the pre-existing malformed `LSOL_Config/SideJobs/BusRoute.xml`.** It is already broken
  before your change and fails 6 bus tests; that failure set is the baseline, not a regression. Leave
  the file untouched and report it as a known pre-existing failure.

## Step 6 — Docs and comments

**Required, not optional.** Update every documentation file and code comment that lists or enumerates
the side jobs: `LSOL_Documentation/SPECIAL_MISSIONS_AND_JOB_BOARD.md`,
`LSOL_Documentation/SAVES_AND_PROFILES.md`, `LSOL_Documentation/README.md`, `LSOL_Addons/README.md`,
`LSOL_Addons_examples/**`, `repo_manifest.md`, and the header legends of both
`LSOL_Config/SideJobs/*.xml` files. No doc currently contains the literal word "Taxi", but several
enumerate side jobs generically — find those lists (grep for `Towing`, `Garbage`, `Bus`,
`FoodDelivery` as job-name enumerations) and keep them accurate. Also fix in-code comments that mention
the taxi job (e.g. in `FoodDeliverySideJobSystem.cs` and `SideJobConfigLoader.cs`).

## Definition of done

1. `dotnet build LSOL.csproj -c Release` → **0 warnings, 0 errors**.
2. `dotnet build LSOL.csproj -c Release -p:ShvdnVariant=Enhanced` → **0 warnings, 0 errors**.
3. `dotnet test tests/LSOL.Tests/LSOL.Tests.csproj` → no *new* failures. Pre-existing baseline is
   427 passed / 17 failed (11 known + 6 caused by the malformed `BusRoute.xml`). The taxi suite's
   passing tests disappear, so the total count drops; the failing set must not grow.
4. Final `Select-String` sweep for `taxi` returns no hits under `src/`, `tests/`, `LSOL_Config/`,
   `configs/`, `missions/`.
5. No other side job's behaviour changed: the side-jobs menu still lists Towing, Garbage, Food Delivery
   and Bus; the skills tablet still shows those four plus Trucking.

## Report back

- Table of **deleted files** and **modified files** with a one-line reason each.
- Exact build commands run and their warning/error counts.
- Test run: total / passed / failed, and the delta versus the baseline above.
- The final grep sweep output (proof of zero remaining taxi references).
- Anything you deliberately left in place, and why.
