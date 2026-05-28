# LSOL Add-on Packages

LSOL add-ons live under `scripts/LSOL/LSOL_Addons/<package-id>/` inside the nested LSOL runtime root.

`scripts/LSOL/LSOL_Config` remains the active base runtime content root for loose XML content. `scripts/LSOL/LSOL_Addons` is the sibling runtime package root for packaged add-ons, including packaged mission packs under `content/missions`.

This first ecosystem pass is file-driven only. LSOL discovers package manifests, validates them, and then loads supported content directories. Third-party assemblies are not executed yet.

## Current Runtime Support

Supported manifest categories:

- `mission-pack`
- `content-pack`
- `preset-pack`
- `ui-pack`
- `module`
- `plugin`

Currently loaded capabilities:

- `content.missions`
- `content.resources`
- `content.sites`
- `content.vehicles`
- `content.officeObjects`

Recognized but not loaded yet:

- `ui.localization`
- `ui.theme`
- `module.missionType`
- `module.tabletApp`
- `Plugin` assembly metadata

## Runtime Layout

```text
scripts/
  LSOL.dll
  LSOL.ini
  LSOL/
    LSOL_Config/
      ...
    LSOL_Addons/
      some.author.package/
        addon.xml
        content/
          missions/
          resources/
          sites/
          vehicles/
          office-objects/
          ui/
            localization/
            themes/
        assets/
        bin/
```

## Minimal Manifest

```xml
<Addon id="sample.author.mission-pack" version="1.0.0" category="mission-pack" lsolApiVersion="1">
  <Metadata
    name="Sample Mission Pack"
    author="LSOL"
    description="Minimal packaged mission example for the LSOL add-on ecosystem."
    source="repository-example" />

  <Compatibility minLSOLVersion="1.0.0" maxTestedLSOLVersion="1.0.0" />

  <Dependencies />
  <Conflicts />

  <Capabilities>
    <Capability name="content.missions" />
  </Capabilities>

  <Content>
    <Directory type="missions" path="content/missions" />
  </Content>
</Addon>
```

## Manifest Rules

- `id`, `name`, `version`, `category`, `author`, `description`, `lsolApiVersion`, `minLSOLVersion`, and `maxTestedLSOLVersion` are required.
- `lsolApiVersion` is currently `1`.
- The current LSOL package compatibility baseline for this pass is `1.0.0`.
- `Dependencies` and `Conflicts` are optional sections but are validated when present.
- `Capabilities` should match the content the package actually ships.
- `Content/Directory` paths must stay inside the package folder.
- One bad add-on should only disable that package; it should not stop LSOL from loading the rest.

## Additive Fragment Rules

For `resources`, `sites`, `vehicles`, and `office-objects`, LSOL uses additive-only merge semantics:

- base `scripts/LSOL/LSOL_Config` content loads first
- add-on fragments load second
- new IDs are accepted
- duplicate IDs are rejected with validation warnings
- add-ons do not silently override base LSOL data

ID guidance:

- namespace package-owned IDs when possible
- keep site IDs stable
- keep vehicle IDs stable and avoid reusing existing model names
- keep office object numeric IDs unique

## Validation Behavior

LSOL surfaces validation warnings for:

- malformed manifests
- unsupported categories or capabilities
- missing content directories
- duplicate add-on IDs
- dependency and conflict failures
- version/API incompatibility
- duplicate additive fragment IDs
- unsupported mission types

## Example Package

The repository includes a minimal packaged mission example under `LSOL_Addons_examples/sample.author.mission-pack/`.

Copy that folder into `scripts/LSOL/LSOL_Addons/` to test the packaged mission path alongside the loose runtime mission path in `scripts/LSOL/LSOL_Config/missions`.