Special mission packs live in this folder.

Drop one `.ini` file per mission next to this README and LSOL will load it automatically on startup.

Current supported mission type:

- `handler_container_transfer`
- `trailer_delivery`

Required sections for `handler_container_transfer`:

- `[Meta]`
- `[Unlock]`
- `[Vehicle.DockHandler]`
- `[Vehicle.DockTug]`
- `[Vehicle.Trailer]`
- `[Vehicle.Truck]`
- `[Prop.Container]`
- `[Zone.LoadingBay]`
- `[Zone.TransferBay]`
- `[Zone.Destination]`

Required sections for `trailer_delivery`:

- `[Meta]`
- `[Vehicle.Trailer]`
- `[Zone.Destination]`

Optional sections for `trailer_delivery`:

- `[Unlock]`
- `[Zone.CleanupArea]`

Important fields:

- `[Meta] Id, Type, Name, Category, Summary, Description, Reward, Repeatable, RepeatCooldownInGameWeeks, RepeatCooldownInGameMonths`
- `[Unlock] Districts, MinInfluenceRatio`
- Each `Vehicle.*` section uses `Model, Position, Heading, Required`
- `Prop.Container` uses `ModelHash` or `Model`, plus `Position, Heading, AttachTargetRole, AttachOffset, AttachRotation, Required`
- Each `Zone.*` section uses `Position, Radius`

Notes for community authors:

- `Districts` must match the IDs from `configs/Districts.csv`, for example `Port` or `GrandSenora`.
- `RepeatCooldownInGameWeeks=1` means the mission can only be accepted once per in-game week after completion.
- `RepeatCooldownInGameMonths=1` means the mission can only be accepted once per in-game month after completion.
- `AttachOffset` is added to the trailer-bed center that LSOL computes at runtime. Start small and tune by testing in game.
- `AttachRotation` is relative to the trailer after attachment.
- Mission vehicles and props are cleaned up automatically when the mission is cancelled, failed, or completed.
- Active missions and completion counts are saved with the normal LSOL save system.

Use `port_container_handler.ini` and `quarry_heavy_machinery.ini` as the reference implementations for the first mission packs.