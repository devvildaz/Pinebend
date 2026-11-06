# Pinebend multiplayer combat playtest

## What this build implements

Two to eight human drivers race three laps with car collisions, all ten power-ups, health, shields, damage, death/recovery, shared pickups, smoke, oil, mines and road craters. The host simulates every car and combat result. Guests send controls and receive car snapshots at 30 Hz plus ordered world events. Track recipes are sent automatically by the host.

This is an initial host-authoritative implementation, not a verified internet playtest. Guest driving and aiming respond after a network round trip; there is no client prediction or lag compensation yet. A close, stable host connection matters. Bots remain single-player only. Everyone must use this exact new ZIP: the combat protocol is incompatible with the old ghost-racing builds.

## Extract and share

Send Pinebend-Windows-Playtest.zip to every friend. Extract the entire archive into a new folder on each Windows PC. Keep Pinebend.exe, its data/runtime files, Friend-Playtest.md and MultiplayerSetup together. The host plays using the same executable. No Unity installation is needed by players.

## Different homes, including CGNAT

Use Tailscale for this prototype. It provides private addresses and can relay connections when a direct route cannot be established. The game still connects directly to the host's VPN address; no router port forwarding is required for this path. Relayed paths may add latency. [Connection types](https://tailscale.com/docs/reference/connection-types).

1. Everyone runs **MultiplayerSetup/Install-VPN.cmd**, or installs from the [official Windows download](https://tailscale.com/download/windows). Open Tailscale and sign in on each PC. The helper installs the client only; it does not sign in, share devices, or change your account. [Windows setup](https://tailscale.com/docs/install/windows).
2. The host opens the Tailscale administration Machines page and shares the hosting PC with each friend. Friends accept the sharing invitation using their own accounts. Alternatively, join the same private Tailscale network using an invitation. Do not share account passwords. [Machine sharing instructions](https://tailscale.com/docs/features/sharing).
3. The host right-clicks **MultiplayerSetup/Host-Setup.cmd > Run as administrator**. It adds a Windows Firewall rule for this Pinebend.exe and TCP port 7777, allowing inbound connections from Tailscale's IPv4 range. It prints the host's VPN IPv4 address when signed in. It does not disable the firewall or open other applications. If the executable moves to another folder, rerun this helper.
4. The host opens **Pinebend > Multiplayer > Host**, chooses a track, and clicks **Open host lobby**. Keep the lobby open. Give friends the printed VPN address (usually 100.x.x.x) and port 7777.
5. Each friend runs **MultiplayerSetup/Join-Check.cmd** and enters the host address. A successful result means the TCP port is reachable; it does not test gameplay. Then choose **Multiplayer > Join** in the game and enter that same address and port.
6. The host waits for the correct driver count and clicks **Start race**. Every driver loads the same track. The camera introduction and countdown begin after all drivers reach the grid. Drive at GO.

The helpers do not create sharing invitations or configure Tailscale access policies. If custom network policies are in use, allow the invited friends to reach the host on TCP 7777. VPN installation, sign-in, account sharing, firewall permission and cross-home connectivity must be completed on the actual test PCs.

## Same local network

A VPN is optional on the same LAN. On the host, open PowerShell as administrator in MultiplayerSetup and run:

```powershell
.\Connection.ps1 -Mode Host -ConfigureFirewall -Lan
```

This allows the host program/port from the local subnet as well as the VPN range. Use the host's LAN IPv4 address from ipconfig, not 127.0.0.1. Guests use Join-Check.cmd with that address, then join through the game menu. The loopback address 127.0.0.1 only reaches the same computer.

For a different port, use `-Port NUMBER` in Connection.ps1 for both setup/check commands and enter that port in the game. The .cmd shortcuts use 7777.

## Controls and useful test cases

W/S or Up/Down: throttle/brake/reverse. A/D or Left/Right: steer. Space: handbrake. R: recovery. M: local/full map. Hold C or LB/L1: rear camera. Esc leaves the race.

Left mouse / LT launches the active missile or activates/drops another item. Right mouse / RT fires the minigun; for a guided missile it selects the target beneath the sight, followed by left mouse / LT to launch. Manual missiles aim through the current camera, including rear view. The minigun fires forward. Carry one active item and one queued item.

Try these across two computers first, then add more players:

- Compare the track, grid, countdown and lap progress. Bump cars and verify both players see the collision.
- Contest the same pickup; only the host grants it. Compare inventory/ammo and the queued reveal.
- Fire both missile types forward and backward; compare hit damage, explosion force and craters. Verify a shield blocks a blast or five minigun hits.
- Trigger a mine, pass through smoke, and drive over oil. Mines persist until triggered. Oil expires after 60 seconds or five car entries. Smoke has no damage or impulse.
- Use turbo and repair; verify the effects appear on the other machine. Turbo lasts three seconds; repair restores up to 35 health; shield lasts up to five seconds.
- Wreck and recover on a slope, then check that both clients show the same rejoin. Verify finishes are recorded by the host while remaining drivers continue.

The host must keep the application open. A disconnect or sustained connection failure ends the race for everyone. Reopen the lobby afterward. Joining a race already underway, reconnecting into it and transferring host ownership are not implemented. A backgrounded game keeps networking active, but releases that player's controls.

## Troubleshooting and cleanup

Run `Connection.ps1 -Mode Status` for a read-only VPN check. If Join-Check fails, check both VPN logins, accepted sharing, the host's open lobby, matching port and executable firewall rule. If TCP is reachable but joining fails, extract the same latest ZIP on every PC. For VPN route details use `tailscale ping HOST_ADDRESS` or `tailscale status`; see the [CLI reference](https://tailscale.com/docs/reference/tailscale-cli).

To remove the helper's firewall rule later, run PowerShell as administrator:

```powershell
.\Connection.ps1 -Mode RemoveFirewall -Port 7777
```

This removes only the named Pinebend rule. Device sharing can be revoked separately in Tailscale.

## Build another deliverable

Open the project in Unity 6000.6.8f1 and stop Play mode. Choose **Pinebend > Build Windows player**. Check Builds/Pinebend-build.txt, then run Tools/Package-Playtest.ps1. This packages the runtime, guide and setup helpers into Builds/Pinebend-Windows-Playtest.zip. Send all testers the new ZIP together.

## Balance and decoy flares

Item odds are fixed for every driver, independent of position or whether the driver is a bot: missile 18%, minigun 16%, mine 10%, guided missile 3%, smoke 8%, oil 8%, shield 10%, turbo 12%, repair 10%, decoy flares 5%. One decoy pickup supplies one burst, activated with left mouse / LT through the existing inventory.

Decoys eject six harmless aircraft-style flares for three seconds. An incoming guided missile targeting that car can divert to a visible flare within 40 metres in its forward seeker cone. Once diverted it will not reacquire the car; if the flare expires it continues along its last heading. Manual missiles and gunfire are unaffected. This is not invulnerability: deploying too late can still allow a hit or a nearby missile explosion. Flares themselves cause no damage, force or road deformation. The host decides diversion in multiplayer and replicates the burst and missile path. Everyone must use this updated build.

The initial shield tuning is five seconds, one blast or five minigun hits. In the Unity project, Assets/Pinebend/Resources/GameplayBalance.asset exposes fixed item percentages, shield duration/hits, and Easy/Normal/Hard bot difficulty. Normal remains the default. Profiles adjust planned speeds, corner approach, aiming lead and item reaction times, while all cars retain the same driving physics. The game menu has no new difficulty selector yet. Gameplay balance remains for human testing.

## Repair and minigun balance update

Repair packs can be activated at 100 health to clear the active slot and promote the reserve; health remains capped at 100. The minigun now has 30 rounds at 10 damage each, with the same firing interval. Shields still absorb five standard gun hits. All players need the updated PB-RALLY/7-BALANCE build. Test full-health repair with and without a queued item.
