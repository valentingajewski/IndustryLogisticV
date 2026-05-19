Legacy runtime mission packs still load from `scripts/LSOL_Config/missions`.

Packaged mission add-ons also load from `scripts/LSOL_Addons/<package-id>/content/missions` when the package contains a valid `addon.xml` manifest.

The mission XML schema below is unchanged for both locations.

These XML mission packs supplement LSOL's built-in rotating contract board. District crises, weekly tenders, and generated priority runs do not require XML files.

Author one `.xml` file per mission in either supported folder and LSOL will load it automatically on startup.

Current supported mission type:

- `handler_container_transfer`
- `trailer_delivery`

Required XML blocks for `handler_container_transfer`:

- `<Mission ...>` root metadata attributes
- `<Unlock ...>`
- `<Vehicles><Vehicle role="DockHandler" ... /></Vehicles>`
- `<Vehicles><Vehicle role="DockTug" ... /></Vehicles>`
- `<Vehicles><Vehicle role="Trailer" ... /></Vehicles>`
- `<Vehicles><Vehicle role="Truck" ... /></Vehicles>`
- `<Props><Prop role="Container" ... /></Props>`
- `<Zones><Zone id="LoadingBay" ... /></Zones>`
- `<Zones><Zone id="TransferBay" ... /></Zones>`
- `<Zones><Zone id="Destination" ... /></Zones>`

Required XML blocks for `trailer_delivery`:

- `<Mission ...>` root metadata attributes
- `<Vehicles><Vehicle role="Trailer" ... /></Vehicles>`
- `<Zones><Zone id="Destination" ... /></Zones>`

Optional XML blocks for `trailer_delivery`:

- `<Unlock ...>`
- `<Zones><Zone id="CleanupArea" ... /></Zones>`

Important fields:

- `<Mission>` uses attributes `id, type, name, category, summary, description, reward, repeatable, repeatCooldownInGameWeeks, repeatCooldownInGameMonths, availabilityDelayInGameWeeks, availabilityDelayInGameMonths`
- `<Unlock>` uses `districts` and `minInfluenceRatio`
- Each `<Vehicle>` uses `role, model, x, y, z, heading, required`
- `Container` props use `modelHash` or `model`, plus `x, y, z, heading, attachTargetRole, attachOffset, attachRotation, required`
- Each `<Zone>` uses `id, x, y, z, radius`

Notes for community authors:

- District names must match the entries from `scripts/LSOL_Config/Districts.xml`, for example `Port` or `GrandSenora`.
- `RepeatCooldownInGameWeeks=1` means the mission can only be accepted once per in-game week after completion.
- `RepeatCooldownInGameMonths=1` means the mission can only be accepted once per in-game month after completion.
- `AttachOffset` is added to the trailer-bed center that LSOL computes at runtime. Start small and tune by testing in game.
- `AttachRotation` is relative to the trailer after attachment.
- Mission vehicles and props are cleaned up automatically when the mission is cancelled, failed, or completed.
- Active missions and completion counts are saved with the normal LSOL save system.

Use `port_container_handler.xml` and `quarry_heavy_machinery.xml` in `scripts/LSOL_Config/missions` as the legacy reference implementations, or copy the sample packaged add-on from `LSOL_Addons_examples/sample.author.mission-pack/` into `scripts/LSOL_Addons/`.