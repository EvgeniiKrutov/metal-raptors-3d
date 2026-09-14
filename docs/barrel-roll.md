# Barrel roll (B)

The player's fourth input after the gun, the bomb and the boost: **B** spins the plane through a
full 360° about its nose axis, and for the second and a half that takes — **plus half a second of
straight flight afterwards** — **nothing that is aimed at the plane can touch it**. It is the defensive counterpart to the boost — the boost is how you
leave a fight, the roll is how you survive the burst you are already in.

| Script | Role |
| --- | --- |
| `PlaneBarrelRoll.cs` | The B key, the pad button, the cooldown, the streaks. Lives on the player's body next to `PlaneShooter`, `PlaneBomber` and `PlaneBoost`. |
| `CubeController.cs` | The roll itself and the evasion it grants. |
| `WingStreaks.cs` | The two white wingtip streaks — the same component the boost and the enemy fighter's diving pass use (docs/boost.md). |
| `CooldownSquare.cs` | The HUD square, shared with the bomb and the boost (docs/hud.md). |

## Configuration

Two fields on `PlayerConfig` (`Assets/Resources/PlayerConfig.asset`):

| Field | Default | Meaning |
| --- | --- | --- |
| `rollRateMultiplier` | 2 | Multiple of `rotationSpeed` the roll spins at. |
| `rollGrace` | 0.5 | Seconds the evasion lasts **after** the spin itself has finished. |
| `rollCooldown` | 6 | Seconds before B works again, counted from the moment the spin *ends*. |

**The duration is not authored — it is derived.** The roll is 360° at
`rotationSpeed × rollRateMultiplier`, so at the Camel's 120 °/s and a multiplier of 2 it spins at
240 °/s and takes **1.5 s**. The cycle is therefore 1.5 s on, 6 s off: B comes back 7.5 s after it
was pressed.

That is deliberate rather than a convenience. `rotationSpeed` is a **per-plane** stat written by
`PlaneLoadout.Build` from the garage selection, not the shared asset's figure, so a plane that
turns harder also rolls faster and spends less time doing it. The roll is the aircraft's own
handling, not a fixed animation bolted on top of it.

`rollGrace` is the one number that does **not** scale with the plane, and that is the point: a
faster-rolling machine would otherwise be protected for less time than a slower one, which is
backwards. The grace is a flat half second of level flight on the way out, so the total
invulnerable window is **2.0 s** on the Camel and never shorter than `rollGrace` on anything.

The cooldown still counts from the end of the **spin**, not the end of the grace, so the cycle is
unchanged at 7.5 s.

## The roll itself (`CubeController`)

`BeginBarrelRoll(rateDeg)` starts it and refuses while one is already running, while the plane is
falling, or before the plane is active. It is three fields and a countdown:

```
_barrelLeft  seconds remaining  (360 / rate)
_barrelRate  degrees per second
_barrelAngle what ApplyRotation adds this frame
```

`AdvanceBarrelRoll` runs in `FixedUpdate` next to the flight model and simply integrates
`_barrelAngle += _barrelRate · dt`, then **zeroes it on the last frame** rather than wrapping.
Zeroing is what makes the roll a true 360°: the plane comes out of it in exactly the attitude it
went in, whichever way up that was, so the roll never quietly flips the aircraft.

`ApplyRotation` sums it with the other two roll sources — the auto-righting half roll and the
death spin — into the single nose-axis term:

```
roll = _roll.Angle + _barrelAngle + (_fall?.Roll ?? 0)
transform.rotation = Euler(0, 0, heading) * Euler(roll, 0, 0)
```

**The auto-righting flip is suppressed for the duration** (`if (!BarrelRolling) _roll.Tick(…)`).
Without that the two would fight: `PlaneRoll` decides the plane is inverted by testing
`cos(angle)` (docs/flight-model.md), and a barrel roll passes through inverted twice on its way
round. It would start a half roll into the middle of the barrel roll and leave the plane upside
down at the end of it. Suppressing the tick also holds `PlaneRoll`'s own inverted timer at
whatever it was, so a plane that was genuinely inverted before the roll is still righted after it.

Being shot down clears the roll outright (`BeginFall`), so the death spin is never added to a
barrel roll already in progress.

## Evasion

`CubeController.Evading` is true for the whole roll **and for `rollGrace` seconds after it**, and
**two call sites read it**:

| Site | Effect |
| --- | --- |
| `Bullet.Hostile` | An enemy round does not consider a rolling player a valid target. |
| `PlaneScrapes.Check` | The whole player/enemy proximity pass is skipped. |

Doing it in `Hostile` rather than in `TakeDamage` matters twice over. First, `TakeDamage` is the
one `IDamageable` entry every damage source funnels through — guarding it would also make the
player immune to **flak and bomb blasts**, which is wrong: those are area detonations, and rolling
through one should not save you. Guarding the bullet's target test leaves them alone.

Second, it reads better. `Hostile` is consulted during the bullet's sphere-cast, *before* a target
is picked, so a round that finds a rolling plane simply keeps flying — it passes visibly through
the aircraft rather than stopping dead in mid-air the way a swallowed hit would.

`PlaneScrapes.Check` skips the pair rather than only sparing the player, so a plane brushed
mid-roll takes nothing either. They did not collide; nobody should pay for it.

### The grace, and why it is not a second roll

The grace is a plain countdown (`_evadeGrace`) armed on the frame the spin finishes and ticked
down by `AdvanceBarrelRoll` on every frame the plane is *not* rolling. It deliberately does **not**
extend `BarrelRolling`, which stays tied to the spin alone — so the aircraft comes out of the
rotation, the streaks stop emitting and the cooldown starts, all on time, while the plane is still
briefly untouchable.

That split is what makes the window read correctly without any extra HUD. A `TrailRenderer` tail
lives **0.55 s** after `emitting` goes false (docs/boost.md), so the streaks the roll leaves behind
fade out over almost exactly the half second the grace lasts: the player is protected for as long
as they can still see the white ribbons behind their wings, and vulnerable again the moment they
are gone. Nothing enforces that — it falls out of the two numbers being the same — so **`rollGrace`
and `WingStreaks.Life` should be kept in step if either is retuned**.

Being shot down clears the grace along with the roll.

**Terrain is untouched by any of this.** The ground collision is a physics contact on
`CubeController`, not a damage test, so a barrel roll flown into a hillside kills the player
exactly as it always did.

## The streaks

The same white `WingStreaks` the boost puts up (docs/boost.md) — two wingtip `TrailRenderer`s,
emitting for the duration of the roll. Reusing them is the point: on screen a streak means *this
plane is being flown hard*, and it should not matter which button caused it.

`WingStreaks` grew two things to make one set of trails serve two abilities on the same aircraft:

- **`Mount` reuses an existing set** on the body when the tint matches, instead of building a
  second pair of trail renderers at the same wingtips. Two overlapping ribbons read as one
  thicker, more opaque ribbon, which is a visual artefact rather than a feature. The tint check
  keeps the enemy fighter's red streaks (docs/enemies.md) from ever being handed out to a white
  caller.
- **`SetEmitting(source, on)` counts sources.** Boost and roll each pass `this`, and the trails
  emit while *any* source wants them to. Without it, a boost expiring in the middle of a roll
  would take the roll's streaks down with it. The old one-argument `SetEmitting(bool)` still
  works and is what the enemy fighter — the only single-source user — calls.

## Sound

No new clip and no new voice: the roll raises the **boost's** high-rev bed, the `engine_throttle_1`
loop at 1.35× pitch, for the duration (docs/sounds.md). `PlayerEngineVoice` used to follow
`CubeController.Boosting`; it now follows `CubeController.HighRevs`, which is `Boosting ||
BarrelRolling`.

**The revs are the only thing the roll borrows from the boost.** Speed and turn rate are untouched
— `_boostTarget` is never moved — so B is not a second R and the two cannot be confused by feel.
The high revs are the pilot hauling the aeroplane round, not the engine being opened up.

## Gating

Identical to the bomb's and the boost's: no roll while the pause menu or the briefing is open,
none during the campaign fly-in or while the cinematic bars are showing, and `LevelIntro` /
`StopWeapons` `Stop()` and `Resume()` it alongside the gun, the bomber and the boost. The cooldown
does not tick while the component is stopped.

A roll already in progress is **not** cancelled by `Stop()` — the component drops its streaks and
starts the cooldown, and the aircraft finishes the rotation it is in over the remaining fraction of
a second. Snapping a rolling plane back to level would be a visible pop for the sake of a beat and
a half.

## Input

| | Binding |
| --- | --- |
| Keyboard | `B` |
| Gamepad | east button |
| Touch / mouse | the `ROLL` square |

The pad binding is the roll's alone; the bomb and the boost still read no gamepad.

## HUD

A `CooldownSquare` third in the action column, under the bomb and the boost, labelled `B` on
desktop and `ROLL` on touch. It is hollow white while the roll runs or is ready, and fades to a
ghost outline walked by a clock hand and a border arc over the 6-second cooldown, exactly as the
boost's does. It is pressable through `HudPressRelay` on to `PlaneBarrelRoll.Request()` — the same
method the key calls. The desktop hint line gained `B to roll`.

**It is the fifth square in the worst case**, on a night level played on touch: bomb, boost, roll,
fire, light. At the touch pitch of 152 units that column runs about 860 of the canvas's ~978, which
still clears the bottom hint — but it is now the constraint on adding a sixth (docs/hud.md).
