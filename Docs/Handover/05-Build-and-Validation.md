# Build, validation and release procedure

## Open and build

Use Unity 6000.6.8f1 with Windows build support and the project's locked packages. Open the root project, allow imports/compilation to finish, then open `Assets/Pinebend/MainMenu.unity`. Resolve compile errors before building. Preserve `.meta` files; do not copy only C# source.

In the Editor, choose **Pinebend → Build Windows player**. This calls `Pinebend.Editor.CircuitBuilder.Build()`, building `MainMenu.unity` followed by `Pinebend.unity` for `StandaloneWindows64`, with `BuildOptions.None`. It writes `Builds/Pinebend/Pinebend.exe` plus required data/runtime files and `Builds/Pinebend-build.txt`. It throws if Unity's result is not Succeeded.

Do **not** call `CircuitBuilder.Create()` to build an existing project. Create is scene generation and can overwrite scene assignments. Save intended scene edits before building; this build helper is not a complete release pipeline with version stamping or dirty-state management.

For the current machine's Unity CLI, run from the project root with the Editor already open and Play mode stopped:

```powershell
$unityCli = Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
& $unityCli status
& $unityCli command eval --caller plugin --skill unity-cli 'return new {playing=UnityEditor.EditorApplication.isPlaying,compilationFailed=UnityEditor.EditorUtility.scriptCompilationFailed};' --json
& $unityCli command eval --caller plugin --skill unity-cli 'Pinebend.Editor.CircuitBuilder.Build(); return System.IO.File.ReadAllText("Builds/Pinebend-build.txt");' --json
```

The CLI is a local automation convenience, not required for human development. Its executable may be elsewhere on another machine. Agents should read the available Unity CLI skill and check CLI command/version support before use. Target this project explicitly if multiple Editors are open. Do not launch another Editor against the same open project just to build.

## Existing edit-mode checks

These are callable static C# checks, **not** a Unity Test Runner suite and not gameplay simulations. They throw on failure and write plain text reports. Run only those relevant to a change; opening a report does not rerun it.

| Method (`Pinebend.Editor.` prefix) | Report in `Builds/` | Scope and limitation |
|---|---|---|
| `GameplayBalanceCheck.Check()` | `balance-check.txt` | Exact odds over 10,000 interval samples, boundary, decoy item JSON; invokes CarPowerCheck |
| `CarPowerCheck.Check()` | `power-check.txt` | Shield budget/blast/expiry/no stacking, repair cap and clearing; no physics |
| `PeerNetworkCheck.Check()` | `network-check.txt` | Input validation, eight-car JSON, fragmented/ordered TCP and frame limits; localhost only |
| `CircuitGenerationCheck.Check()` | `layout-check.txt` | 256 loops, 64 crossings, spacing, deterministic recipe roundtrip, junction geometry |
| `StartingGridCheck.Check()` | `grid-check.txt` | 256 eight-car reservations, obstruction/determinism/failure; synthetic terrain probe |
| `MenuPreviewCheck.Open(width,height)` | No automatic pass report | Editor preview window for visual layout inspection |

Example for a balance change:

```powershell
& $unityCli command eval --caller plugin --skill unity-cli 'Pinebend.Editor.GameplayBalanceCheck.Check(); return System.IO.File.ReadAllText("Builds/balance-check.txt");' --json
```

`CircuitGenerationCheck.CheckAndBuild()` also builds; use Check alone when a build is not intended. The network check opens local loopback sockets but does not launch a race or test VPN connectivity.

## Historical evidence at handover

Copied reports are under [Evidence](Evidence/README.md), with SHA-256 fingerprints and source modification metadata. They record the latest runs of each check. Balance/power checks and the build were refreshed for the repair/minigun update; layout/grid/network reports remain from earlier work, not a fresh full-suite run.

- Latest build: Succeeded, 0 errors, 0 warnings, 134,292,932 reported bytes.
- Balance: all 10,000 distribution intervals match authored odds; decoy inventory, shield checks and full-health repair queue promotion passed.
- Grid: 256 eight-car grids, minimum center separation 5.20 m with synthetic probes.
- Layout: 256 loops and 64 level intersections, maximum crossing height mismatch 0.00000 m in the tested set.
- Protocol: eight-car snapshot 5285 bytes, fragmented TCP reassembly and 96 ordered mixed frames passed.

The prior build's warning investigation identified the 510 warnings in installed AI inference/Sentis ConvGeneric/ConvTranspose/GridSample/Pad/Scatter/SliceSet shaders. The summary report does not contain the raw warning log. Recheck actual warnings on future builds; do not describe the project as warning-free. The subsequent repair/minigun update rebuilt successfully with zero warnings; the copied build report now reflects that update.

## Package and distribute

After a successful player build, run from the root:

```powershell
& ./Tools/Package-Playtest.ps1
Get-FileHash -Algorithm SHA256 ./Builds/Pinebend-Windows-Playtest.zip
```

The script checks that the executable exists, copies `Docs/Friend-Playtest.md` and `Tools/MultiplayerSetup`, then compresses the build into `Builds/Pinebend-Windows-Playtest.zip`, excluding debug/backup directories by name. **It does not compile or verify build freshness.** Inspect Unity's build report first. It overwrites the ZIP; archive older approved releases separately if needed. Handover documents are not automatically included in the player ZIP.

For a developer transfer, provide `Assets` with metadata, `Packages`, `ProjectSettings`, `Tools`, `Docs` and `PINEBEND.md`. `Library`, `Temp`, `Logs`, IDE output and user-specific state are generated/local and normally unnecessary. A source-control provider was not configured by this task; no Git repository was detected. Do not publish credentials, account logs or machine-specific VPN data with the project.

## Human acceptance checklist — not executed by the agent

Record build hash, track file/seed/version, mode, driver count and reproduction steps for each issue.

| Area | Human test |
|---|---|
| Driving | Acceleration, reverse, service brake, repeated handbrake drifts, jumps and landing control |
| Placement | Eight-car starts on each family; recovery on slopes, near rivals and beside deep craters |
| Race flow | Introduction, 3-2-1-GO lock, ordered forward gates, three laps, restart/menu |
| Map | Corner car-forward projection, all markers, rear view, full north-up map, crossings, resized window |
| Inventory | Empty start, active+one reserve, both reveals, full inventory, cooldown and promotion |
| Weapons | Manual front/rear aim, guided lock, forward gun, friendly/self blast damage and shield impulse block |
| Effects | Smoke blindness, oil five entries/60 s, persistent mine, turbo/recovery clear, repair at full health |
| Decoys | Early/late use, obstruction/cone, no reacquisition, manual missiles unaffected |
| Bots | Easy/Normal/Hard pace, corner/jump behavior, overtaking, item tactics and getting unstuck |
| Network | Two physical PCs first, then 2–8 people; contested boxes, car collisions, every item, craters, death, finish |
| Failures | Wrong build, unreachable host, disconnect in lobby/race, background focus loss, slow connection |

Successful build/check reports do not replace these tests. The owner's explicit preference is to perform gameplay testing personally.
