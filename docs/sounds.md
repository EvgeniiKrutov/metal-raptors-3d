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

## Clips

Copied from `metal-raptors/public/sounds` into `Assets/Resources/Sounds`:
`engine_idle`, `engine_throttle_1`, `engine_stutter`,
`ambient_wind`. Note that `/Assets/Resources` is gitignored, so these files —
like the existing bullet and explosion clips — are not tracked by the repo.

Base volumes: throttle 0.2, stutter 0.3, wind 0.35, enemy throttle 0.15 — all as
in the 2D library. Idle was raised from the 2D 0.7 to 0.95 so the idle bed sits
close enough to the revs layer that the crossfade is not heard as a dip.

## Player engine

Two looping voices, idle and throttle (revs), crossfaded over 0.7 s in both
directions — longer than the 2D 0.3 s, again to hide the seam. The plane is
considered to be working the engine when either

- `|AngularVelocity| > MaxTurnRate * 0.4` — a hard turn, or
- the nose is pitched above +30° — a climb.

`CubeController` exposes `Heading`, `AngularVelocity` and `MaxTurnRate` for this.
Once maneuvering stops, a 0.3 s grace period runs before dropping back to idle,
so rapid stick work does not chatter between the two loops.

Deviation from 2D: the crossfade runs both levels simultaneously instead of
fading the new layer in first and the old one out afterwards, and the throttle
source keeps looping silently at level 0 instead of being stopped, which avoids
a restart pop. The revs clip is picked at random from the two throttle files each
time revving starts (2D only ever used clip 1).

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
