# Pinebend developer handover

Snapshot: 2026-11-04. Intended for a human developer or an AI agent continuing this Unity project. Paths in these documents are relative to the project root unless linked otherwise. This folder documents the current implementation; it is not a replacement for the Unity source project.

## Start here

Pinebend is a Windows arcade rally combat prototype: one generic hatchback rig, solo races against seven bots, closed procedural tracks, jumps, deformable road, ten temporary items, and an initial host-authoritative multiplayer implementation for 2–8 people. Races are three laps. Gameplay and multiplayer testing belong to the human owner.

1. Open the project with Unity **6000.6.8f1** and Windows build support.
2. Open `Assets/Pinebend/MainMenu.unity` for the normal entry point. `Assets/Pinebend/Pinebend.unity` contains the authored race rig and original oval.
3. Read [architecture](01-Architecture.md), then the relevant system document below.
4. Read [verification and building](05-Build-and-Validation.md) before changing or distributing anything.
5. Read [known limitations and next work](07-Known-Limitations.md). Successful compilation is not evidence that driving, balance, or online play feels correct.

## Contents

| Document | Purpose |
|---|---|
| [01 — Architecture](01-Architecture.md) | Source map, initialization, authority, scene contracts |
| [02 — Gameplay and balance](02-Gameplay-and-Balance.md) | Controls, driving, bots, health, inventory, all item values |
| [03 — Tracks, spawning and maps](03-Tracks-Spawning-and-Maps.md) | Recipes, compatibility, intersections, jumps, craters, safe placement |
| [04 — Multiplayer](04-Multiplayer.md) | Connection model, wire messages, replication, CGNAT, Steam boundary |
| [05 — Build and validation](05-Build-and-Validation.md) | Editor/build commands, packaging, evidence, human test matrix |
| [06 — Extension guide](06-Extension-Guide.md) | Adding an item, model, track version or transport safely |
| [07 — Known limitations](07-Known-Limitations.md) | What is implemented, what remains unverified, proposed priorities |
| [08 — AI agent continuation](08-Agent-Continuation.md) | Owner constraints and a continuation checklist |
| [Friend playtest instructions](Operations/Friend-Playtest.md) | Existing player-facing deployment and VPN guide, copied at handover |
| [Car model instructions](Operations/Car-Models.md) | Existing mesh adapter instructions, copied at handover |
| [Evidence](Evidence/README.md) | Historical check reports, build result, source and artifact fingerprints |

## Current delivery state

The most recent Windows player build succeeded: **0 errors, 0 warnings**, 134,292,932 bytes reported by Unity. This build includes full-health repair consumption and the 30-round, 10-damage minigun. Edit-mode inventory and shield checks passed; no game was launched. Earlier builds reported AI inference/Sentis shader warnings.

Deliverable: `Builds/Pinebend-Windows-Playtest.zip`. Extract the complete ZIP on every player's Windows PC. The binary protocol is **PB-RALLY/7-BALANCE**; everyone should use the same exact ZIP. No Steam transport is installed. Tailscale connectivity helpers are packaged, but VPN accounts, device sharing, firewall setup, and real cross-home gameplay have not been validated by the agent.

No Git repository was detected in this workspace during handover. There is therefore no verified commit identifier; [source fingerprints](Evidence/source-manifest.json) identify the files documented here. Keep `Assets` including `.meta` files, `Packages`, `ProjectSettings`, `Tools`, and `Docs` when transferring the project. A player ZIP alone cannot be used to continue development.

## Owner decisions to preserve

- Prioritize gameplay mechanics and maintainable code; visual/HUD polish is a later phase.
- Keep the generic test-car rig; new visual models should use the existing adapter.
- **Item probabilities are fixed for everyone**, including bots, regardless of race position.
- Guided missiles remain rare. Manual missiles are manually aimed; minigun fire stays aligned with the car's front.
- Crossroads should use ground-level intersections for new tracks. Preserve old bridge-track compatibility.
- Leave driving, balancing and multiplayer playtests to the human. Technical checks and builds are allowed.

Update these documents and evidence after future changes. The operational copies are snapshots; their source documents remain `Docs/Friend-Playtest.md` and `Docs/Car-Models.md`.
