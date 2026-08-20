# Engine sounds

`SoundSystem` (Assets/Scripts/SoundSystem.cs) is the runtime audio mixer for the
level scenes. It is a port of the 2D game's `src/game/systems/SoundSystem.ts`
(sibling repo `metal-raptors`), including its tuning values from
`src/game/config/data/sounds.json`.

`SoundSystem.Begin(player, enemies)` creates a `SoundSystem` GameObject and owns
every engine `AudioSource` on it. Sources live on the system object rather than
on the planes so a voice can keep fading after the plane it belonged to has been
destroyed. `LevelController` passes its live enemy list (the same `List` instance
it mutates, read every frame); `CampaignLevelController` passes `null` — that
scene has no enemies yet.

## Arming

`Begin(..., silent: true)` builds the system but creates **no sources at all** and
skips its whole `Update`; `Arm()` builds them and starts the clocks. This is how a
campaign level stays silent behind its briefing page: the world, the plane and the
HUD are all constructed under `Time.timeScale = 0` while the player reads, and
starting the engine there would have it droning under a static page (the system
runs on *unscaled* time, so freezing the game would not have muted it). The
campaign controller passes `silent: HasBriefing` and arms the system from the
briefing's dismissal callback, at the black frame of the fade
(docs/level-briefing.md). A custom battle and the standalone Level 1/2 scenes have
no briefing to dismiss and start armed, as before.

Arming is one-way and idempotent; nothing ever puts the system back to silent.

## Clips

Copied from `metal-raptors/public/sounds` into `Assets/Resources/Sounds`:
`engine_idle`, `engine_throttle_1`, `engine_stutter`,
`ambient_wind`. Note that `/Assets/Resources/Sounds` is gitignored, so these files —
like the existing bullet and explosion clips — are not tracked by the repo
(docs/conventions.md).

One effect has no clip at all: the supply-crate pickup chime is synthesised at runtime with
`AudioClip.Create` and cached, rather than adding another wav (docs/supply-drops.md).

Base volumes: throttle 0.2, stutter 0.3, wind 0.35, enemy throttle 0.15 — all as
in the 2D library. Idle was raised from the 2D 0.7 to 0.95 so the idle bed sits
close enough to the revs layer that the crossfade is not heard as a dip.

## Player engine

Three looping voices: idle, throttle (revs) and boost. Idle and throttle are
crossfaded over 0.7 s in both
directions — longer than the 2D 0.3 s, again to hide the seam. The plane is
considered to be working the engine when either

- `|AngularVelocity| > MaxTurnRate * 0.4` — a hard turn, or
- the nose is pitched above +30° — a climb.

`CubeController` exposes `Heading`, `AngularVelocity` and `MaxTurnRate` for this.
Once maneuvering stops, a 0.3 s grace period runs before dropping back to idle,
so rapid stick work does not chatter between the two loops.

### High revs (boost)

The third voice is the **same `engine_throttle_1` clip at 1.35× pitch**, not a
separate recording — pitching the loop we already load is what a hard-running
engine sounds like, and it costs no new asset in a gitignored folder. It fades in
over 0.18 s whenever `CubeController.Boosting` is true and out again when the R
boost ends (docs/boost.md), at base volume 0.3.

While it is up, idle and throttle duck to **35%** so the high revs sit on top
without the engine dropping out from under them. The duck multiplies the shared
bed, so pause, spawn and retire fades still compose on it, and the idle/revs
maneuver logic is untouched — boosting into a hard turn still selects the revs
layer, just quieter.

`Boosting` follows the boost's *target* factor rather than the eased one, so the
sound lands on the keypress instead of ramping in behind the speed. It also means
the layer cuts on the frame the boost expires, while the plane is still slowing
down — which reads as the engine being pulled back, and is the reason the fade
out exists at all.

An earlier air brake ducked these same layers by `BrakeAmount`; both the brake
and the duck are gone (docs/flight-model.md).

Deviation from 2D: the crossfade runs both levels simultaneously instead of
fading the new layer in first and the old one out afterwards, and the throttle
source keeps looping silently at level 0 instead of being stopped, which avoids
a restart pop. The revs clip is picked at random from `ThrottleClipPaths` each
time a voice is built; only `engine_throttle_1` is in that list today, so the
random pick is a no-op until a second file is copied in. The boost layer always
takes clip 1 explicitly, so its pitched-up loop cannot end up being a different
recording from the bed underneath it.

## Enemy engines

Revs only — no idle layer and no maneuver logic, per design. Each enemy's loop
fades in over 0.6 s and is retired with a 0.3 s fade when the enemy dies or drops
out of the level's list. The retire fade is its own constant, so lengthening the
idle/revs crossfade does not drag out engine cut-offs.

Distance attenuation is manual (`spatialBlend` stays at 0, matching the shot and
explosion sounds): full volume within 320 units, linear fade to silence at 900,
and only the three nearest enemies are audible at all. The 2D thresholds (700 /
2000) were scaled by the ratio of the two games' camera view widths — the 3D
camera sees roughly 860 world units across against 1920 in 2D.

## Stutter

`CubeController.OnDamaged` fires on every hit; the level controllers forward it
to `ReportPlayerDamaged`, which plays `engine_stutter` once when the player is
alive and at or below 30% health. It will not retrigger while an earlier stutter
is still playing.

## Pause and game over

While `GameMenu.IsOpen` (pause menu, `Time.timeScale = 0`), every source on the
system object fades out over 0.12 s and is then `Pause`d; closing the menu
`UnPause`s and fades back in. All audio timing uses `Time.unscaledDeltaTime`,
since Unity audio ignores `timeScale`.

`EnterGameOver` — called on crash, shot down, and level completed — fades all
engine voices out over 0.3 s and stops the pause handling; the wind bed is
stopped when the Failed/Completed menu appears. This mirrors the 2D behaviour.
