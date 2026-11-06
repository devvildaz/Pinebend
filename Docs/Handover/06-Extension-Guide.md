# Extending the prototype

These are implementation checklists, not promises that a feature already exists. Keep changes scoped to the owner's requested work and preserve the human gameplay-test boundary.

## Add a car visual

Follow [Car-Models.md](Operations/Car-Models.md). Import a mesh-only body prefab using metres, +Z forward, +Y up. Separate optional wheel meshes use an X axle and are installed into the four existing sockets. Create a `Pinebend/Car visual model` ScriptableObject and assign it to each intended car's `TestCar.model` in the scene.

The adapter rejects prefabs containing Collider or Rigidbody components. Authors must also avoid cameras, listeners, controllers or other unwanted behavior; the adapter does not validate every component type. It hides previous visual children and instantiates the replacement under the sprung shell/wheel socket. Empty assignments preserve the generated hatchback visuals, including underbody.

This does not change vehicle dimensions or handling. A differently sized physics rig needs wheel/hull geometry, center of mass, recovery footprint, grid reservations and bot assumptions updated together. Model selection/download in a lobby is not implemented; visuals belong to scene slots. Distribute identical builds. `CircuitBuilder.Create` can erase scene model assignments.

## Add an item

1. Append to `RallyWeapons.Power`; preserve existing numeric IDs. Update Item capacity, reveal preview, display name and active/queued handling.
2. Extend `GameplayBalance` default and invalid-authoring fallback arrays, asset percentages and validation. Keep total 100 and identical odds for everyone.
3. Implement authority-only activation in `RallyWeapons.FireAt` and any authoritative simulation/cleanup. Spend charges only when activation succeeds.
4. If the effect outlives inventory, give it separate state/lifecycle as shield/turbo do. Decide recovery, destruction and race-end behavior explicitly.
5. Add bot eligibility/tactics in `RallyBot.UseItem`; avoid giving bots rules unavailable to humans.
6. Add network state or `RaceReplication` event handling and guest visuals. Do not let client visuals award damage or publish recursive events. Extend protocol compatibility/version as needed.
7. Update existing HUD text/reveal behavior and necessary functional cues without unsolicited UI redesign.
8. Add meaningful rule/serialization checks; compile/build, update docs, and give the human focused acceptance cases.

Current reveal cycling contains a literal item count; it must be updated alongside the enum. Check all enum casts, probability indexing, preview switches, ammo getters and network item DTO consumers. Adding a new damage type also requires deciding whether it consumes shield and whether force is suppressed.

## Tune balance

Edit `Assets/Pinebend/Resources/GameplayBalance.asset` for fixed odds, selected bot difficulty, shield seconds and bullet-hit budget. Tune profile curves/timing in `GameplayBalance.cs`; per-car base cruise remains in `RallyBot`. `CarPowerState` owns turbo and repair constants. `TestCar` owns most driving and recovery constants; `RallyWeapons` owns projectile, damage and explosion values.

Run `GameplayBalanceCheck.Check()` after changes. The cached Resources object is shared; do not create per-position overrides. There is no difficulty menu or per-session balance synchronization. All peers should use the same build/settings. The host still owns results, but guest presentation can disagree if code/assets differ.

## Add a track generation version

Preserve old recipe sampling. Introduce a new version dispatch in `TrackRecipe.Sample` and `Supported`, keep enum/file compatibility, and update the menu generation path. Test seeded JSON roundtrips and geometry across many seeds. The generator, road, spawn planner, gates, bots and map must agree on point order, count, widths and intersection semantics.

Do not silently replace version 2's bridge layouts with version 3 intersections. Current code contains hardcoded 128-point loops and four quarter-route gates. A new sample count requires auditing them. A recipe is not a mesh file; changing shared road/jump construction can alter old recipes even if centerline generation stays stable. For strict archival reproducibility, version those construction rules too.

## Evolve multiplayer

Keep simulation ownership independent of input ownership. Extract `PeerLink` transport operations and `PeerRace` connection creation behind explicit interfaces before integrating another transport. Preserve or replace the current ordered event contract intentionally. Add protocol checks for new fields, malformed messages and compatibility errors.

Prediction would require separating command capture, authoritative simulation and presentation in `TestCar`, tracking input acknowledgments and handling correction. The present interpolation cannot be relabeled prediction. Host migration/late joining would also require full world-state reconstruction for items, timers, craters and race state; current event-only history does not provide it.

## Refactor incrementally

Useful boundaries already exist in `CarPowerState`, `TrackRecipe`, `GameplayBalance`, `StartingGrid` and the network DTOs. The largest remaining couplings are `TestCar` (many responsibilities), `RallyWeapons` (simulation and presentation), string-based scene lookup and direct TCP construction in `PeerRace`.

Prefer focused extraction with behavior preserved, then relevant checks and a build. Introducing assembly definitions, a full DI framework, another UI system or a networking package is a separate architectural decision, not a prerequisite for the next small feature.
