# Level intro (`LevelIntro`, `CinematicBars`)

A campaign level no longer drops the player mid-air with the controls live. It opens as a scene:
the map sits still, the plane flies in from off the left edge, and the first radio call arrives
between two black cinematic bars. Control changes hands during the fly-in, so the intro costs
about three seconds and never takes the stick away once the shooting can start.

## The order of things

| Beat | What the player sees |
| --- | --- |
| briefing | The pre-level page (docs/level-briefing.md) freezes the game at `Time.timeScale = 0`, so nothing below has started yet. The level is silent — the engine has not been armed (docs/sounds.md). |
| fly-in | The frame is static and carries no HUD at all. The plane enters from the left at cruise speed, straight and level; nothing responds to the keyboard. |
| cue | The script starts. Its first `say` block raises the bars over ~0.5 s. |
| freeze | With the bars in, the fly-in stops where it is and the picture blurs over 0.55 s (docs/cutscenes.md). |
| lead-in | The bars hold empty for 0.55 s — the beat that makes the line feel spoken rather than pasted. |
| radio | The line types itself into the bottom bar (docs/campaign-scripts.md), over a still frame. |
| thaw | The last line closes: the picture sharpens, the plane resumes its entry, the bars leave. |
| hand-over | The stick, the guns, the bomb and the boost come back. Shortly after, the plane passes the camera's hold point and the map finally starts to scroll. |
| HUD | The bars lower at the end of the opening conversation and the HUD appears for the first time. |

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
coroutine and then destroys itself. It touches only `CubeController.SetControlled(false)` (which
makes the plane ignore the keyboard while it still flies its normal flight model), the
`Stop`/`Resume` pair on each of `PlaneShooter`, `PlaneBomber` and `PlaneBoost`, and the callback
that starts the script. The intro runs for custom battles too; there it is only the fly-in, since
a battle with no script has nothing to cue — and with no conversation to wait for, the HUD appears
at the hand-over.

## The bars (`CinematicBars`)

Two solid black bars, **150 px** tall each at the 1920×1080 reference resolution, pinned to the top
and bottom edges. They leave a 780 px window — about 2.46∶1, the letterbox of a widescreen film.
They slide in and out together by animating their height from 0 over **0.5 s** on a `SmoothStep`
curve, on `CutscenePause.Delta` (docs/cutscenes.md) — unscaled, so they keep sliding while they are
themselves stopping the game, but still frozen mid-slide by the pause menu like everything else.

They live on a nested canvas inside the HUD canvas with `overrideSorting` at order **150** — above
the HUD, below the pause/fail/completed overlay (200) and the briefing (300). Nesting them in the
HUD rather than giving them a root canvas is what makes the existing overlays hide them: the menus
deactivate the whole HUD object.

Nothing else in the game raises the bars; today they are the dialogue's frame, and `DialogueBar`
owns the instance.

## Cutscene mode

The level is **in a cutscene** whenever `LevelIntro` is still running or the bars are anywhere but
fully down — `CampaignLevelController.Cinematic`, evaluated fresh every `LateUpdate`. The bars half
of it is `CinematicBars.AnyShowing`, a static sweep over the live bar components that reads true
from the first frame of the slide-in to the last frame of the slide-out, so the two half-second
slides count as well as the held pose. Three things change while it is true.

A dialogue block additionally **stops and blurs the game** between the bars arriving and the bars
leaving (docs/cutscenes.md); everything below applies to the slides at either end, where the game
is still moving, and is simply moot in between.

### The HUD is not on screen (`HudCurtain`)

`HudCurtain` is a component on the HUD canvas with one idempotent method, `Set(bool)`. Closing it
deactivates every child of the canvas **except the cinematic bars** — those are nested in the HUD
canvas (see above) and hiding them would take the dialogue with them — and remembers exactly what
it turned off; opening it turns exactly those back on. Nothing else is restored, so an absent
searchlight indicator does not reappear.

The curtain starts **closed**, in `BuildHud`, before the fly-in begins. So the player never sees
the HUD flash up behind the entering plane: the frame is empty until the intro has handed over
*and* the opening conversation has finished, at which point `Set(!Cinematic)` opens it for the
first time. It closes again for every later radio line.

A widget created while the curtain is closed would otherwise be born visible, so a spawner such as
`WarnIncoming` hands the new object to `HudCurtain.Adopt`, which hides it and adds it to the
restore list.

The pause and fail overlays still deactivate the whole HUD object, which composes with the curtain
without either knowing about the other: the root going inactive hides everything, and the curtain
still holds its own children when the root comes back.

### The ground stops being lethal

There is no barrier above the ground. The plane flies exactly as it always does, straight into the
dirt if the player insists — it just cannot die there, and the touchdown is **not** resolved by
Unity's physics. `CampaignLevelController` feeds `CubeController.SetCinematic`, and the transition
does two things.

**Plane–ground contacts are switched off, not caught.** Terrain lives on its own layer,
`ProceduralTerrain.GroundLayer` (11) — set on every `Terrain.CreateTerrainGameObject` in both
terrain builders and on Level 1/2's flat ground slab — so `PlaneScrapes.SetGroundCollisions(false)`
can disable **8 vs 11** in the layer matrix for the length of the cutscene, and re-enable it after.
This is the fix for the visible symptom: with collisions on, the rigidbody kept being pushed out of
the terrain while `FixedUpdate` overwrote its velocity from the heading every step, and the two
fought each other into a fast tremble. Nothing else in the matrix moves — bullets (0), props (9)
and bombs (10) all still meet terrain and the plane exactly as before. Enemy fighters share layer 8
and therefore also pass through terrain during a cutscene, which never comes up: their own flight
model floors them at `AiGroundY` well above it.

**The deck is authored instead.** With no collision to stop it, `CubeController` finds the ground
itself: one downward `Physics.Raycast` per step, masked to `GroundLayer` alone, from 500 units
above the plane. If the plane is below the hit point plus `GroundSkim` (14 units — roughly the
model resting on its belly) it is clamped to exactly that height and its downward velocity is
zeroed, the same shape as the ceiling clamp. The plane slides along the surface under full control
and can pull away whenever the player likes. No hit means no clamp, so a gap in the streamed
terrain simply does nothing.

Each clamped step calls `Scrape()`, which in cinematic mode skips both `TakeDamage` and the sparks
and keeps only the airframe shudder (`ShakeEffect`) and the `OnScraped` callback — which the
controller already wires to a full `_camShake`. Its existing 0.5-second `CollisionCooldown` is what
turns a continuous grind into a shake every half second rather than one every physics step.

`OnCollisionEnter` still routes to `Scrape()` while cinematic rather than exploding, but that path
is now a backstop for anything solid that is *not* terrain; the ground never reaches it.

This whole arrangement replaces an earlier hard floor 60 units above the terrain, which stopped the
player from descending at all during a radio line. Being unable to fly where you want read worse
than being unable to die there.

The coast's **ditching** check goes with it: `LateUpdate` only calls `Ditch()` outside a cutscene,
so dropping toward the waterline during a radio line is survivable in the same way.

Bullets, bombs and enemy fire are **not** affected; only the ground and the sea are. Nothing here
applies outside a cutscene, where hitting terrain kills as it always has (docs/flight-model.md).
Both level controllers call `SetGroundCollisions(true)` at `Start` next to
`DisablePlanePlaneCollisions`, because the layer matrix is a project-wide setting that outlives the
scene — a level that ended mid-cutscene would otherwise hand the next one a plane that flies
through hills.

### Bomb and boost are refused

`PlaneBomber` ignores H and `PlaneBoost` ignores R while the bars show, and both report not-ready
to a HUD that is not being drawn anyway. Their cooldowns freeze with the rest of the game while a
line is up and tick only through the slides, so a cutscene costs the player nothing, and a boost
already running is left alone to finish. The gun is *not* gated — only the two things that can hurt the player who used
them.
