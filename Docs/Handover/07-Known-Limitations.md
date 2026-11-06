# Known limitations and next work

This distinguishes confirmed implementation constraints from unverified risks. It is not a report that every prior user bug remains unfixed.

## Implemented but awaiting human validation

- Latest balance changes: fixed item odds, bot profiles, five-second/five-hit shield, decoy flares, full-health repair consumption and the 30-round/10-damage minigun compile and pass rule checks. Their game feel has not been agent-tested.
- Multiplayer authority, snapshots, combat replication, readiness, failure handling and friend setup helpers exist. Local transport/serialization checks passed. Real two-PC/VPN races and 2–8-player stability have not been verified by the agent.
- Earlier spawn overlap, below-road recovery, map projection, responsive preview and crossing fixes are in code. Geometry/reservation checks cover specific cases; human driving is still needed to catch regressions and untested seeds.

## Confirmed limitations

| Area | Current limitation |
|---|---|
| Input | Gamepad aim/items/rear/recovery only; no gamepad driving/handbrake/menu mapping |
| Multiplayer feel | Guest input waits for host simulation; interpolation only, no prediction/lag compensation |
| Connectivity | TCP host address required; Tailscale is external; no built-in NAT traversal/Steam/Relay/lobbies |
| Session resilience | No late join, reconnect, host migration or dedicated server; race disconnect ends session |
| Trust | No game-layer authentication/encryption or full hostile-peer validation; private friend network scope |
| Compatibility | Protocol identifies version, not a full content hash; same exact ZIP required |
| Difficulty | Asset-selectable profile; no menu selection or multiplayer bot support |
| Persistence | Track recipes and connection preferences only; no live race/crater save or progression |
| Models | Mesh adapter only; no garage, per-user selection/download or variable chassis specification |
| Road damage | Mesh/collider rebuilt for each crater; no chunking, debris simulation or baked damage persistence |
| Maps | Flat XZ route footprint; no altitude/depth information |
| Architecture | Large coupled MonoBehaviours, global state and scene-name dependencies remain |
| UI | Functional IMGUI prototype; no final art/accessibility/localization/rebinding pass |
| Delivery | Windows player only has a verified build report; no CI/release versioning pipeline |
| Source history | No Git repository detected; no verified commit/diff baseline |

## Review risks inferred from code

These are follow-up candidates, not gameplay failures reproduced during handover:

- Very deep craters, steep approaches or congested roads may exhaust recovery candidates and prolong recovery. The global Y=-4 recovery condition and finite crater probe distance deserve a long-session stress test.
- Bots plan from undeformed route markers and do not have a full crater/hazard route planner. Difficulty may vary significantly by seed, jump and damage pattern.
- Static caches/registries and dynamically created objects need scene-cycle checks, especially if Enter Play Mode/domain-reload settings change.
- Repeated mesh recooking, CPU map rasterization, transient effects and growing permanent-mine counts may affect performance. Measure before optimizing.
- TCP ordered delivery can cause visible delays under packet loss even though writes are asynchronous. Bounded queues fail sessions when peers cannot keep up.
- Imported recipe validation is bounded but not a general content sandbox; host-sent snapshots/events are only partially validated. Public matchmaking would need a dedicated robustness/security pass.
- Exact old-track reproducibility depends on shared generator and road construction code, not just the version field. Test retained fixtures before changing either.

## Suggested sequence, pending owner priorities

1. Human acceptance on the latest ZIP, starting with two machines and all item types; record seeds/build hashes and network conditions.
2. Address demonstrated spawn/map/combat regressions before adding more systems. Preserve existing technical checks.
3. Complete gamepad driving through an input abstraction if controller testing is requested.
4. For online responsiveness, design client prediction/reconciliation and transport boundaries before a Steam integration.
5. Extract vehicle simulation/presentation and scene configuration incrementally as features demand.
6. Configure source control, repeatable release naming and automated technical checks with the owner's chosen provider.
7. Perform HUD/art/audio polish when the owner moves beyond mechanics.

Do not interpret this list as authorization to implement all proposed work. No additional feature work was performed in this documentation task.

## Latest bot/recovery build

Bot driving now outputs handbrake for tight grounded turns; TestCar consumes that command. Manual recovery is gated by off-route, overturned, or 2.5 seconds immobilized with driving input. The Editor was subsequently stopped; compilation and Windows build passed with zero errors and warnings. The playtest ZIP and fingerprints are updated. Gameplay validation remains for the human.
