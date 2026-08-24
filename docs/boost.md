# Engine boost (R)

The player's third input after the gun and the bomb: **R** runs the engine hard for a few
seconds. Everything about the plane gets faster — it turns tighter *and* covers more ground —
so unlike the removed air brake (docs/flight-model.md) it is an escape and a chase tool rather
than a maneuvering trade.

| Script | Role |
| --- | --- |
| `PlaneBoost.cs` | The R key, the duration, the cooldown. Lives on the player's body next to `PlaneShooter` and `PlaneBomber`. |
| `WingStreaks.cs` | The two wingtip streaks. Shared — the fighter's diving pass mounts the same component in red (docs/enemies.md). |
| `CooldownSquare.cs` | The HUD square, shared with the bomb. |

## Configuration

Three fields on `PlayerConfig` (`Assets/Resources/PlayerConfig.asset`):

| Field | Default | Meaning |
| --- | --- | --- |
| `boostMultiplier` | 1.3 | Applied to **both** the turn rate and the speed band. |
| `boostDuration` | 3 | Seconds the boost runs. |
| `boostCooldown` | 8 | Seconds before R works again, counted from the moment the boost *ends*. |

So the cycle is 3 s on, 8 s off — R comes back 11 s after it was pressed. There is no way to
cancel a boost early and no way to stack two.

## What it changes

`PlaneBoost` drives `CubeController.SetBoost`, which moves an internal factor between 1 and
`boostMultiplier`. Two things read it:

- `MaxTurnRate` — `rotationSpeed × factor`, so 120 °/s becomes 156 °/s.
- `CruiseSpeed` — `flySpeed × factor`, and since that is now the plane's only speed
  (docs/flight-model.md), the boost simply moves it: 180 → 234 m/s.

Because both scale by the same factor, the **turn radius is unchanged** by a boost —
`speed / turnRate` cancels it out. The plane flies the same arc, faster. That is what makes the
boost safe to press in a tight spot instead of a way to widen yourself into the ground.

The factor is *eased* rather than switched (`BoostResponse`, 3.5/s, snapping to the target
within 0.001), so the plane surges and settles instead of teleporting into the new speed. The
boost is engaged the instant R is pressed, but the plane needs about a second to be fully on it,
and the same again to come back down after the three seconds are up.

Nothing else scales. Turn *responsiveness*, mass, dive acceleration and drag are untouched, so a
boosted plane still feels like the same aircraft.

## Gating

Identical to the bomb's (docs/bombs.md): no boost while the pause menu or the briefing is open,
none during the campaign fly-in or while the cinematic bars are showing, and `LevelIntro` /
`StopWeapons` `Stop()` and `Resume()` it alongside the gun and the bomber. A boost already
running is *not* interrupted by a radio line starting — only new presses are refused. `Stop()`
mid-boost ends it and starts the cooldown.

The cooldown does not tick while the component is stopped (a disabled `MonoBehaviour` runs no
`Update`), so an intro or a pause never eats into it; a radio line does let it tick, since the
game is running.

## The wingtip streaks (`WingStreaks`)

Two `TrailRenderer`s, one per wingtip, emitting only while the boost runs. They are the reason
the boost reads on screen at all — the speed change alone is easy to miss on a scrolling map.

The tips come from `PlaneFactory.WingTipsLocal`, which takes the model's combined renderer
bounds and returns the two extremes **in Z** (the wings run into and out of the screen in this
2.5D view), at the bounds' centre in X and Y. Each streak is then pulled back along −X by 12 %
of the semi-span so it starts behind the leading edge rather than on it. Being children of the
plane body, they follow its heading for free.

Each trail is 2.1 units wide at the plane and lives 0.55 s, on a
`Universal Render Pipeline/Unlit` material at 0.8 alpha made transparent through
`UIFactory.MakeTransparent`. The colour is a caller's choice (`Mount`'s optional `tint`) but
both users take the default white: the player's boost and the enemy fighter's diving pass
(docs/enemies.md) put up the same streak, so on screen it reads as *a plane running its engine
hard*, whoever is flying it. `alignment = View` keeps the ribbon facing the camera, so it reads
the same from any bank angle.

The width is a *curve*, not a straight `startWidth`/`endWidth` taper: full width at the plane,
still 0.8 of it at 60 % of the way down the tail, then falling to zero at the tip. A plain
linear taper spends the whole streak thinning out, which is what made the old 1.4-wide, 0.28-alpha
version read as a faint hair; holding the width and collapsing it late gives a clean streak that
still ends in a point, and is what lets the width itself stay modest. The taper — not a colour gradient — is also the only fade available here:
URP's Unlit shader ignores vertex colour, so a `TrailRenderer` gradient would have no effect on
it.

Ending the boost only clears `emitting`; the existing tail lives out its 0.55 s and disappears
on its own.

## Sound

The engine gets a third looping voice in `PlayerEngineVoice` (docs/sounds.md): the same
`engine_throttle_1` clip played at **1.35× pitch**, faded in over 0.18 s while boosting and out
again afterwards. The idle and throttle beds duck to 0.35 underneath it, so the high revs
dominate without the engine dropping out. The voice follows `CubeController.Boosting`, which
tracks the *target* factor, so the sound arrives on the keypress rather than trailing the eased
speed.

## HUD

A `CooldownSquare` directly under the bomb's in the HUD's action column, labelled `R` on desktop
and `BOOST` on touch. It is hollow with a white outline while the boost is running or ready, and
fades to a ghost outline walked by a clock hand and a border arc running 0 → 1 together over the
8-second cooldown. The
square is pressable, through `HudPressRelay` on to `PlaneBoost.Request()` — the same method the `R`
key calls. See docs/hud.md.
