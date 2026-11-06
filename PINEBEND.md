# Pinebend

Developer and AI-agent continuation documentation: [Docs/Handover/README.md](Docs/Handover/README.md).

An eight-car rally prototype for Unity 6000.6.8f1. Open Assets/Pinebend/Pinebend.unity to adjust the car or circuit. The original SampleScene remains in the project.

The scene contains a player and seven bot drivers using the same hatchback and four-wheel physics. The road markings follow a closed D-shaped spline around a 726 m lap on deformable gravel; the grass is usable runoff and has the same grip as the road. The start gantry and three intermediate timing gates record forward laps.

## Driving

W / Up accelerates. S / Down brakes while moving forward and reverses at low speed. A/D or the arrow keys steer. Tap or hold Space while steering to apply the rear handbrake. Release it and countersteer to catch the slide. S / Down is the service brake. R or the top gamepad face button requests a 0.55-second track recovery. Staying off the road for three seconds also triggers recovery. Escape returns to the main menu.

## Items

Mystery boxes appear in two rows of three. Each box rolls two missiles, 30 minigun rounds, one proximity mine, one smoke bomb, one oil spill, one shield, one turbo, one repair kit, one decoy flare burst, or a rare guided missile (3% chance). Carry one active item and one reserve. Using the last round promotes the reserve.

Each car has 100 health. A near missile blast deals up to 40 damage (including to its launcher), and each minigun hit deals 10. A wreck returns to the track after 1.35 seconds at full health. Lap time keeps running. Both recoveries give a 25 km/h rolling rejoin. Wreck smoke obscures passing drivers for up to four seconds. Missiles, mines and wrecks can repeatedly deepen craters in the gravel. With a guided missile, aim at a rival and press right mouse or RT to lock, then left mouse or LT to launch. Drop a mine with left mouse or LT; it arms after 0.75 seconds and stays until triggered or the race ends. Smoke bombs use the same input, drop behind the car and obscure vision for 10 seconds without damage, force or terrain deformation. Oil spills reduce grip while crossing and for 2.5 seconds afterwards, and expire after 60 seconds or five car entries; lingering on the spill counts once. Both new items are included in the active/reserve reveal animation and bot item use.

## Build

In the editor, choose Pinebend > Build Windows player. The executable and its data are written to Builds/Pinebend; the result is written to Builds/Pinebend-build.txt. Keep the output folder together when sharing it.






## Tracks and sessions

MainMenu is the startup scene. Both modes share the versioned .pbrtrack library in the application data Tracks folder. A file records the generator version, seed, name and built-in track flag. Version 1 tracks use the original radial generator. Version 2 stores a seed and layout family, samples a closed spline at even intervals, and gives each overpass separate upper and lower driving surfaces. Version 3 uses ground-level Crossroads in place of generated overpasses; the junction has one continuous destructible surface, cleared curbs and a flat approach. Existing version 2 bridge files still load unchanged. The menu preview fits its reserved image area; narrow windows stack the controls and scroll. Direct multiplayer supports 2–8 human drivers in a host-authoritative combat race. See Docs/Friend-Playtest.md for deployment, hosting, and joining. See Docs/Car-Models.md for adding a visual model. Single-player races finish after three laps.




Rear view: hold C or LB/L1. Manual missile aim and guided target selection use the active chase camera.

## Starting grid and race introduction

All active cars receive reserved grid slots before their controllers initialize. Rows are placed seven metres apart along the road (not by marker index), with terrain footprint probes, obstacle checks and conservative separation between cars. Blocked positions are skipped; if a full safe grid cannot be placed, the race stays locked and offers track selection.

A 3.5-second camera sweep shows the starting grid and blends into the local chase camera, followed by an animated 3-2-1-GO countdown and audio signals. Suspension can settle while horizontal movement, steering, items, recovery and timing gates are held until GO. The multiplayer host begins this sequence after every connected driver reports loaded.

## Defensive and movement items

Left mouse / LT activates the current shield, turbo or repair item. Shield lasts up to five seconds and absorbs one missile/mine blast (including its impulse), or five minigun hits. A blast consumes any remaining shield. Turbo lasts three seconds, multiplies forward wheel torque by 1.8 and raises the engine speed limit from 41 to 52 m/s. It requires throttle and ground contact; braking and handbraking still work, and oil still reduces grip. Expiry does not instantly clamp velocity. Repair restores 35 health, capped at 100, and can be consumed at full health to clear the slot and promote the reserve. Existing shield/turbo effects cannot be stacked or refreshed early. Recovery or destruction clears both effects.

Effects persist after the active item is consumed and the queued item is promoted. A cyan ring indicates shield, exhaust streaks indicate turbo thrust, and a short green ring confirms repair. No HUD redesign is included. Bots repair at 65 health or below, or to clear a queued item, react to nearby missile/mine/minigun threats with shields, boost on clear straights while grounded, check line of sight before firing and drop traps for following cars. All items are also host-authoritative in multiplayer.

## Multiplayer implementation

Protocol PB-RALLY/7-BALANCE sends controls to one player-host. The host runs all WheelColliders, collisions, item rolls, projectile impacts, damage, recovery and finish gates. Guests render 30 Hz authoritative car/inventory/effect snapshots; their hulls remain queryable for aiming but do not simulate physics. Ordered events replicate projectile and hazard lifecycle, pickup cooldowns, smoke, repair flashes, shots and crater edits. TCP writes use a bounded background queue and never wait on the Unity main thread. Stale guest inputs are neutralized; sustained connection failures end the race.

Guests currently have interpolation but no local prediction or lag compensation, so responsiveness depends on round-trip latency. No mid-race joins, reconnect, host migration or dedicated server. CGNAT friend tests use Tailscale private networking, with setup/check/firewall helpers shipped in the ZIP. No VPN account or firewall configuration is performed automatically by the game. See Docs/Friend-Playtest.md.

## Balance and decoy flares

Item odds are fixed for every driver, independent of position or whether the driver is a bot: missile 18%, minigun 16%, mine 10%, guided missile 3%, smoke 8%, oil 8%, shield 10%, turbo 12%, repair 10%, decoy flares 5%. One decoy pickup supplies one burst, activated with left mouse / LT through the existing inventory.

Decoys eject six harmless aircraft-style flares for three seconds. An incoming guided missile targeting that car can divert to a visible flare within 40 metres in its forward seeker cone. Once diverted it will not reacquire the car; if the flare expires it continues along its last heading. Manual missiles and gunfire are unaffected. This is not invulnerability: deploying too late can still allow a hit or a nearby missile explosion. Flares themselves cause no damage, force or road deformation. The host decides diversion in multiplayer and replicates the burst and missile path. Everyone must use this updated build.

The initial shield tuning is five seconds, one blast or five minigun hits. In the Unity project, Assets/Pinebend/Resources/GameplayBalance.asset exposes fixed item percentages, shield duration/hits, and Easy/Normal/Hard bot difficulty. Normal remains the default. Profiles adjust planned speeds, corner approach, aiming lead and item reaction times, while all cars retain the same driving physics. The game menu has no new difficulty selector yet. Gameplay balance remains for human testing.

## Bot/recovery update

Bots now request handbrake turn-in on tight grounded corners, releasing when drifting or oiled. Manual recovery is accepted only off-route, overturned (up Y < 0.45), or moving below about 1.4 m/s for 2.5 seconds while applying throttle/reverse. Death respawn and off-route automatic recovery remain. Compilation and Windows build passed with zero errors and warnings; the playtest ZIP includes this update. Human handling validation remains pending.
