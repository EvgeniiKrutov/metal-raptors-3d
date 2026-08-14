# Level intro (`LevelIntro`, `CinematicBars`)

A campaign level no longer drops the player mid-air with the controls live. It opens as a scene:
the map sits still, the plane flies in from off the left edge, and the first radio call arrives
between two black cinematic bars. Control changes hands during the fly-in, so the intro costs
about three seconds and never takes the stick away once the shooting can start.

## The order of things

| Beat | What the player sees |
| --- | --- |
| briefing | The pre-level page (docs/level-briefing.md) freezes the game at `Time.timeScale = 0`, so nothing below has started yet. |
| fly-in | The frame is static. The plane enters from the left at cruise speed, straight and level; nothing responds to the keyboard. |
| cue | The script starts. Its first `say` block raises the bars over ~0.5 s. |
| lead-in | The bars hold empty for 0.55 s — the beat that makes the line feel spoken rather than pasted. |
| radio | The line types itself into the bottom bar (docs/campaign-scripts.md). |
| hand-over | The stick and the guns come back. Shortly after, the plane passes the camera's hold point and the map finally starts to scroll. |

## Why the map holds still

There is no camera lock. `PositionCamera` already parks the camera at `StartX` and only ever
ratchets its X forward (`Mathf.Max(_camBasePos.x, …)`), so a plane that is *behind* `StartX`
cannot pull the frame: the map stays put on its own until the plane reaches the middle of the
screen. The intro exploits that by spawning the plane off the left edge instead of at `StartX`.

The one thing that has to be switched off is the **no-turning-back wall**. It rides the camera's
left view edge and teleports the plane back to it (`CubeController.SetLeftWall`), which is exactly
where the plane is flying in from, so `CampaignLevelController` stops feeding it while
`LevelIntro.Active`. The wall is armed on the first `LateUpdate` after the hand-over, by which
point the plane is well inside the frame.

Distance needs no special case either: `_furthestX` starts at `StartX` and the whole intro happens
at negative X, so the HUD reads `0 m` until the player is flying the level for real.

## Geometry

All distances are in metres, measured from `StartX` (the camera's hold point), and scale with the
half view width — a narrower window means a shorter entry.

| Mark | Where | 16:9 (half view ≈ 431 m) | Reached at 120 m/s |
| --- | --- | --- | --- |
| entry | left view edge − 90 | −521 | 0.0 s |
| cue | −0.55 × half view | −237 | ≈ 2.4 s |
| hand-over | −0.30 × half view | −129 | ≈ 3.3 s |
| hold point | `StartX` — the map starts scrolling | 0 | ≈ 4.3 s |

`LevelIntro` is a component added to the level controller that watches those two marks in a
coroutine and then destroys itself. It touches three things and nothing else:
`CubeController.SetControlled(false)` (which makes the plane ignore the keyboard while it still
flies its normal flight model), `PlaneShooter.Stop`/`Resume`, and the callback that starts the
script. The intro runs for custom battles too; there it is only the fly-in, since a battle with no
script has nothing to cue.

## The bars (`CinematicBars`)

Two solid black bars, **150 px** tall each at the 1920×1080 reference resolution, pinned to the top
and bottom edges. They leave a 780 px window — about 2.46∶1, the letterbox of a widescreen film.
They slide in and out together by animating their height from 0 over **0.5 s** on a `SmoothStep`
curve, on *scaled* time, so the pause menu freezes them mid-slide like everything else.

They live on a nested canvas inside the HUD canvas with `overrideSorting` at order **150** — above
the HUD, below the pause/fail/completed overlay (200) and the briefing (300). Nesting them in the
HUD rather than giving them a root canvas is what makes the existing overlays hide them: the menus
deactivate the whole HUD object.

Being above the HUD, the top bar **covers the top HUD row** while it is up — title, health bar,
light indicator, distance and the current task. That is the point (it reads as a cutscene), but it
is also an authoring rule: a `task` line belongs *after* a block of radio lines, not before it, or
the objective appears behind a bar. `CampaignScriptRunner` enforces the same thing from its side —
any op that is not `say` lowers the bars first and waits out the slide.

Nothing else in the game raises the bars; today they are the dialogue's frame, and `DialogueBar`
owns the instance.
