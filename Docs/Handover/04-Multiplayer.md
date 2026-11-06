# Multiplayer and connection handover

## What exists

One player hosts a **TCP, host-authoritative** race for 2–8 human drivers. This is player-hosted networking, not a peer mesh, Steamworks connection or Unity Relay session. The host simulates every car, collision, pickup, roll, projectile, damage result, recovery, crater and finish gate. Guests send intent and render snapshots. Bots are disabled in multiplayer.

For CGNAT friend tests the intended connection path is Tailscale: an external private-network route supplies connectivity to the host's address. The game itself has no NAT traversal, relay allocation, account authentication, invitations or matchmaking. Setup instructions are copied into [Operations/Friend-Playtest.md](Operations/Friend-Playtest.md). These helpers are implemented; actual VPN installation/sign-in/sharing and cross-home play remain user work.

## Authority boundaries

- `PeerRace.Authority`: true in solo or on the host. Gate all gameplay mutations here.
- `PeerRace.IsLocal(car)`: the input/camera owner on the current machine.
- `PeerRace.IsProxy(car)`: all client-side cars, including the locally controlled car. Guests disable wheel simulation and render authoritative poses.
- `TestCar.CaptureNetwork` / `ApplyNetwork`: state boundary for transforms, inventory, health, progress and visual effects.
- `RaceReplication`: ordered world changes and visual prop lifecycle. Guest effects must not recursively publish events or apply damage.

Never accept client claims for position, health, damage, pickup awards, crater edits or lap completion. Existing input validation is prototype-level and does not make this a hardened public service. TCP has no application-layer authentication or encryption here; private-network access is the intended deployment boundary.

## Wire contract

Current handshake: **PB-RALLY/7-BALANCE**. Text frames are UTF-8, newline-delimited; message payloads use `JsonUtility` DTOs from `RaceProtocol`.

| Message | Direction | Meaning |
|---|---|---|
| Protocol literal | Guest → host | Exact version check |
| WELCOME / ERROR | Host → guest | Handshake acceptance or incompatibility reason |
| START + JSON | Host → guest | Assigned slot, driver count and TrackRecipe |
| READY | Guest → host | Grid loaded and safely placed |
| GREEN | Host → guests | Begin camera introduction/countdown; not immediate GO |
| INPUT + JSON | Guest → host | Steering, pedal, handbrake, aim, gun/rear flags, sequence and launch/recovery counters |
| WORLD + JSON | Host → guests | All car snapshots plus authoritative GO flag |
| EVENT + JSON | Host → guests | Spawn/move/remove props and gameplay visual/world changes |

Input carries a monotonically increasing sequence; host ignores older sequence values. Launch and recovery counters preserve button presses across sends. Inputs require finite values, steering/pedal within ±1, bounded aim and target slot -1 through 7. Host rechecks guided target identity, range, direction and visibility. Manual missile aim farther than 350 m from the car is rejected.

Snapshots include slot, pose/velocity, health, lap/gear/finish place/progress/timers, hidden/respawn state, shield/turbo/oil/drift/handbrake flags, visual steering/slip and active/queued inventory with reveal time remaining. Guests interpolate toward the newest pose; large corrections and respawns snap. There is no local prediction, rollback or lag compensation.

Event kinds include `rocket`, `mine`, `smoke`, `oil`, `decoys`, `move`, `remove`, `pickup`, `crater`, `burst`, `shot`, `repair`. IDs identify replicated props. Only rockets receive ongoing movement events. Decoys replicate one burst origin/rotation/inherited velocity; each machine animates its six flares, while the host alone selects a missile's lure. Temporary timing is receipt-relative, not a synchronized simulation clock.

## Transport and session lifecycle

`PeerLink` uses `TcpClient`, `NetworkStream`, `NoDelay`, a concurrent outgoing queue and an asynchronous writer. The Unity main thread pumps available input; each read call processes at most eight 8192-byte chunks. Limits: 16,384 bytes per frame and 524,288 pending outbound bytes. Oversize frames, stalled writers and socket errors fail the session rather than growing an unbounded queue. Reliable TCP ordering simplifies events but can delay all later data behind a lost packet.

`PeerRace` publishes inputs and car snapshots at approximately 30 Hz. Socket connection timeout is 15 s, handshake timeout 8 s, and initial grid readiness timeout 60 s. Host neutralizes guest controls after 0.5 s without input; sustained loss of input or host snapshots for 8 s during driving ends the session. A disconnect in the lobby removes that guest; a failure during a race ends it rather than handing over authority.

The host stops listening when starting. Every player loads `Pinebend`, applies the same recipe and configures name-sorted slots. Once all grids are ready, host sends GREEN and starts the shared-length introduction/countdown. WORLD.green releases client gameplay after local countdown conditions are met. Multiplayer finish order is host-owned; a finished driver brakes while others continue, unlike solo's final pause.

## Deployment helpers

`Tools/MultiplayerSetup/Connection.ps1` supports Status, Host, Join, Install and RemoveFirewall. CMD shortcuts wrap installation, host setup and join reachability checks. Default TCP port is 7777. The installation helper invokes the Tailscale installer; it does not create accounts or shares. Host configuration creates a scoped executable/port firewall rule for Tailscale IPv4 addresses; `-Lan` also permits LocalSubnet. It never disables the firewall. Moving the executable requires updating the rule. Join tests TCP reachability, not game correctness.

`Tools/Package-Playtest.ps1` ships these helpers and the friend guide with the player. Everyone should extract a fresh copy of the same ZIP. See the operational guide for user steps, custom ports and cleanup. No firewall/VPN changes were performed as part of this handover.

## Steam and future transports

Steam P2P is **not implemented**. Tailscale changes routing only; it does not supply a Steam API or reusable Steam integration. Existing gameplay authority and DTO boundaries are reusable, but `PeerRace` currently creates TCP listeners/clients directly. A transport migration should first separate connection/session APIs from simulation, then add Steam identities, lobby/invite lifecycle and Steam message transport. Keep reliable world-event ordering and define snapshot delivery semantics explicitly. Steam deployment requirements must be checked against then-current official documentation when implementing them.

There is currently no client prediction, reconnect, host migration, late join, dedicated server, matchmaking, Steam integration, transport abstraction or verified internet gameplay result. Treat these as future work, not switches that can be enabled.
