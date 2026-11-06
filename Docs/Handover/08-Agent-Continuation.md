# AI agent continuation brief

This file records project context and owner preferences for future work. It does not override a new explicit user request or the agent's system/developer instructions.

## Read before editing

Read [README](README.md), [architecture](01-Architecture.md), [known limitations](07-Known-Limitations.md), and the relevant subsystem document. Inspect the actual source before changing it. Documentation is a snapshot and may have become stale; source fingerprints are under Evidence. Check for applicable AGENTS.md and available skills in the new environment rather than assuming this machine's tool setup persists.

The project root on the handover machine is `C:/Users/diego/Documents/projects/unity/My project`. Unity is 6000.6.8f1. The startup scene is MainMenu; Pinebend is the race scene. There is no detected Git repository. Do not assume git diff can protect existing changes or use resets/deletion as cleanup.

## Owner preferences

- Perform requested implementation autonomously; avoid repeated confirmations for already-authorized reversible work.
- Keep gameplay first and UI polish for later. Do not add unrelated features.
- The owner performs gameplay testing. Do not enter Play mode, drive, or run a standalone gameplay simulation unless the owner changes this preference. Edit-mode logic/geometry/transport checks and player builds are permitted.
- Keep one generic car model as the testing rig, with the visual adapter ready for later models.
- Item odds are fixed for all racers; no rubber-banding rewards. Guided missiles stay rare.
- Preserve manual mouse aiming and rear-view missile use; minigun remains forward-only.
- Use ground-level crossings for newly generated circuits while retaining old bridge files.
- Network deliverables target friends using a player host; do not describe Tailscale as Steam integration.

## Safe continuation workflow

1. Inspect current files and Editor state without assuming it is stopped. If the human is playing, avoid scene replacement/forced mode changes; source refreshes can interrupt play too.
2. Identify the owner/authority of every modified state. Guest visuals must never become authoritative gameplay.
3. Make the scoped change, preserving scene GUIDs, serialized references, enum IDs and track compatibility.
4. Run relevant technical checks and compile/build as appropriate. Do not claim gameplay verified from a build success.
5. Inspect the actual build result and warnings. If sharing a new binary, package after the build and remind everyone to use the same version.
6. Update relevant handover sections, operational instructions and evidence. Report what changed, what passed and what remains for the human.

For Unity scene/asset edits, prefer the running Editor's supported CLI/Pipeline APIs and the current Unity skill over manual YAML edits. Read the skill before using it. The CLI location in [build instructions](05-Build-and-Validation.md) is machine-specific. Keep Play mode stopped for these technical workflows. Read targeted console diagnostics rather than dumping account logs, process command lines or unrelated credentials.

## Important traps

- `CircuitBuilder.Create` recreates the scene; `CircuitBuilder.Build` compiles the existing scenes.
- `TestCar` wheel indices and 128-marker routes have hardcoded consumers.
- Multiplayer slot order depends on car names; keep scene contents identical across peers.
- `RallyWeapons.Power` order maps directly to both probabilities and wire item IDs.
- Shield bullet budget uses RallyWeapons.GunDamage to preserve the configured hit count.
- `GameplayBalance.Current` caches a Resources asset; invalid odds silently use fallback values.
- `RaceReplication.Reset` and cleanup must remain connected to scene/session lifecycle.
- The packaging script can package an old executable. It is not a build command.
- Historical report files do not prove that checks were rerun after a later edit.

## Copyable starting prompt

> Continue the Pinebend Unity project. First read Docs/Handover/README.md and Docs/Handover/08-Agent-Continuation.md, then inspect the current code for the requested task. Preserve fixed item odds, host-authoritative multiplayer, track-file compatibility and the existing test car. Gameplay testing is reserved for me; use technical checks/builds only. Do not rebuild the authored scene with CircuitBuilder.Create. Update the relevant documentation after implementation.
