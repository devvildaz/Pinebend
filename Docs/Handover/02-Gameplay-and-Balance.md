# Gameplay and balance

## Controls

| Action | Keyboard/mouse | Implemented gamepad equivalent |
|---|---|---|
| Accelerate / brake / reverse | W/S or Up/Down | Not mapped in `TestCar.Update` yet |
| Steer | A/D or Left/Right | Not mapped yet |
| Handbrake | Space | Not mapped yet |
| Aim | Mouse position | Right stick moves the sight |
| Launch / activate / drop item | Left mouse press | Left trigger press |
| Minigun | Hold right mouse | Hold right trigger |
| Select guided target | Right mouse press while holding guided item | Right trigger press |
| Rear view | Hold C | Hold left shoulder |
| Recover | R | North/top face button |
| Full map | M toggle | Not mapped yet |
| Return to menu | Escape | Not mapped yet |

Gamepad support is intentionally described as partial. The sight/weapon interface is ready for it, but the game is not fully controller-playable. Minigun fire always follows the car's front even in rear view; manual aim and guided selection use the active camera. Losing application focus neutralizes local driving/weapon input.

## Driving, health and recovery

The rig uses a 1080 kg Rigidbody and four WheelColliders. The default wheelbase is 2.3 m, wheel-center track width 1.72 m, and tire radius 0.36 m. These are construction defaults in `CircuitBuilder`, not a general vehicle configuration system. Existing scenes may have serialized overrides.

`TestCar.FixedUpdate` applies speed-dependent steering, torque, service braking and rear handbraking. Rear grip drops toward 0.32 during handbrake slides and returns at 1.65 units/second. Rear handbrake torque defaults to 1800. Yaw assist supports turn-in and catching the slide. Wheel poses, body roll, chase-camera travel-angle blending and dust provide drift feedback. Ground contact still comes from Unity wheel physics. Normal forward engine limit is 41 m/s; reverse limit is 9 m/s. The displayed gear is inferred from speed, not a simulated gearbox.

Cars have 100 health. Missile/mine blasts deal up to 40 damage with distance falloff, so two maximum-damage blasts leave 20 health. A third can destroy the car. Gun hits deal 10. Blasts can damage the launcher. Shields suppress accepted blast damage and impulse through the `TakeDamage` return value.

Wrecks hide the car, disable its colliders, emit an explosion visual and obscuring smoke, and wait 1.35 seconds. Manual recovery waits 0.55 seconds. Timers continue to penalize the driver. Successful return uses a safe road candidate and starts at 7 m/s (~25 km/h). Destruction restores health to 100; voluntary recovery does **not** refill nonzero health. Shield/turbo clear when recovery starts, oil clears when placement succeeds. Inventory is not explicitly cleared by recovery.

Automatic recovery triggers after three seconds off track or below world Y=-4. Bots also request recovery after three seconds stalled/overturned/badly misaligned. Recovery can wait longer than its nominal delay if a clear, safe road location is unavailable. Details: [spawning](03-Tracks-Spawning-and-Maps.md).

## Inventory and exact current odds

Cars begin without an item. Two rows of three mystery boxes grant one active item and at most one queued item. Each item has a 0.85-second reveal animation; using the last charge promotes the queue. A full inventory cannot consume another box. Boxes respawn after eight seconds. Host authority owns rolls and collection in multiplayer.

The fixed percentages below apply equally to every driver; there is no position weighting or bot-specific lottery.

| Enum index | Item | Probability | Pickup contents / behavior |
|---|---|---:|---|
| 0 | Missile | 18% | Two manually aimed missiles |
| 1 | Minigun | 16% | 30 rounds at 10 damage, forward-only hitscan |
| 2 | Mine | 10% | One proximity mine, arms after 0.75 s; no lifetime timeout |
| 3 | Guided | 3% | One missile, manually select a rival before launch |
| 4 | Smoke | 8% | One smoke bomb, 10 s obscurity, no damage/impulse/crater |
| 5 | Oil | 8% | One spill; expires after 60 s or five car entries |
| 6 | Shield | 10% | Up to 5 s, one blast or five standard minigun hits |
| 7 | Turbo | 12% | 3 s, forward torque ×1.8, engine limit 52 m/s |
| 8 | Repair | 10% | Restore up to 35 health, capped at 100 |
| 9 | Decoy | 5% | One burst of six flares lasting 3 s |

The enum order is also the probability-array and network inventory order. Do not reorder it casually. `Resources/GameplayBalance.asset` sets difficulty, shield duration/hits and item percentages. Percentages must have one entry per enum and total 100. Invalid authoring falls back to the default array in `GameplayBalance.Roll`; update the fallback when adding items.

## Combat and effect details

Missiles travel at 90 m/s with a 210 m travel budget. Guided steering turns toward a selected target (or a decoy). Explosion radius is 11 m, with full 40 damage up to 3 m and falloff outward. Current impulse uses `AddExplosionForce(2600, ..., 11, 0.55, Impulse)`. Changing impulse requires human testing for control loss and terrain interaction. Mines trigger within 2.4 m of an eligible racer after arming and use the explosion path. They can affect their owner and remain until triggered or the scene ends.

Oil sets longitudinal and lateral grip multipliers to 0.22 while crossing, with a 2.5-second tail. Remaining inside the same spill counts as one entry. Wreck smoke lasts up to four seconds; smoke bombs last ten. Smoke obscurity affects the player's view and bot attack decisions; it is not damage.

Shield uses a damage budget of `shieldGunHits * RallyWeapons.GunDamage`, currently 50. Any explosion consumes the remaining shield and is blocked. For non-explosion damage, the entire incoming hit is blocked even if it exceeds the remaining budget; overflow does not pass through. The budget tracks the shared gun-damage constant, preserving the configured number of hits. Reusing a still-active shield or turbo is rejected without spending that item. Repair can be consumed at full health; health stays capped at 100 and the reserve is promoted. Timed effects outlive their inventory slot, so the reserve can be promoted while the effect continues.

Turbo requires forward throttle and ground contact for thrust; braking, handbrake and oil still apply. Expiry does not instantly reduce velocity. Shield has a cyan ring, turbo has exhaust streaks and repair has a green flash. These are functional cues, not a final HUD design.

Decoy flares are harmless visual objects with colliders disabled. A guided missile targeting their owner can divert to a visible flare within 40 m and its forward seeker cone (dot product > 0.15). Once diverted, it drops the car target permanently. After the flare expires, it continues on its last heading; near the flare it detonates normally. A nearby detonation can still hurt the deploying car. Manual missiles and gunfire ignore decoys. Host authority chooses diversion; guests receive the burst and missile path.

## Bot difficulty

Solo currently has seven bots; multiplayer disables bots. All use the same driving physics as the player, without rubber-banding or position-based item rewards.

| Profile | Cruise multiplier | Corner-planning grip | Tactical reaction interval | Missile cooldown | Manual shot lead factor |
|---|---:|---:|---:|---:|---:|
| Easy | 0.89 | 7.8 | 0.42–0.65 s | 3.2 s | 0.35 |
| Normal (default) | 1.00 | 9.2 | 0.24–0.38 s | 2.3 s | 0.70 |
| Hard | 1.08 | 10.2 | 0.16–0.27 s | 1.8 s | 1.00 |

These profile numbers are code properties; the asset selects the profile. Base `RallyBot.cruisingSpeed` is per car; planned non-boost cruise is capped at 40.5 m/s. The planner looks ahead through curves, brakes before bends, offsets lanes to pass, ignores vertically separated blockers, reduces corner speed while oiled and holds straight steering in flight. It repairs at health ≤65 or to clear a queued item, shields against threats, boosts on clear grounded straights, uses decoys against nearby guided threats and drops traps for following racers. Human testing must determine whether these settings deliver the intended difficulty.
