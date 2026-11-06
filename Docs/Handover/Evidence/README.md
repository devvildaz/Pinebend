# Evidence snapshot

These are copies of the latest available reports. Balance/power and build reports were refreshed for the repair/minigun update; other reports retain their earlier results. Report timestamps and SHA-256 hashes are recorded in `source-manifest.json`. Source fingerprints identify the documented state in the absence of a Git commit.

| Copied file | Meaning |
|---|---|
| [Pinebend-build.txt](Pinebend-build.txt) | Latest Unity build result, errors/warnings and reported output bytes |
| [balance-check.txt](balance-check.txt) | Fixed probability intervals, decoy inventory JSON and shield checks |
| [power-check.txt](power-check.txt) | Timed power rules |
| [network-check.txt](network-check.txt) | Local protocol/loopback transport checks only |
| [layout-check.txt](layout-check.txt) | Seeded layout and intersection geometry checks |
| [grid-check.txt](grid-check.txt) | Grid reservations using synthetic ground probes |
| [source-manifest.json](source-manifest.json) | Hashed source/configuration/tool files and original report/artifact metadata |

The manifest includes C# files, Pinebend scenes and balance asset, asset metadata, package manifest/lock, project settings, tools and original project docs. It excludes Library, Temp, account logs, machine credentials and player binaries themselves. The player ZIP and executable are represented by path, length, source modification time (UTC) and SHA-256 only. Copying the handover folder does not copy the source project or binary deliverable.

Build warnings were investigated in the prior implementation task and attributed to installed AI inference/Sentis shaders. Only the build summary is preserved here, not the full console warning transcript. Human gameplay, VPN deployment and cross-machine multiplayer outcomes are not present in these reports.
