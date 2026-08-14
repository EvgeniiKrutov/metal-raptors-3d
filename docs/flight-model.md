# Player flight model: cruise + dive energy

Implemented in `CubeController` (`UpdateSpeed`), tuned via `PlayerConfig`
(Assets/Resources/PlayerConfig.asset).

## Concept

The plane flies at constant throttle. The engine is strong enough that the plane
never stalls — it can fly a full loop and never drops below cruise speed. But it
is not a constant *speed*: pointing the nose at the ground trades altitude for
airspeed, so dives are faster than cruise.

Speed is a single scalar along the heading; each physics step it changes by
three terms:

1. **Gravity along the flight path**: `-sin(heading) * diveAcceleration`.
   Nose-down (sin < 0) accelerates the plane; nose-up decelerates it, which
   bleeds off dive speed on the way back up (a dive-then-zoom-climb roughly
   conserves that energy).
2. **Drag on the excess**: `(speed - flySpeed) * speedDrag` is shed per second,
   so extra speed also decays in level flight instead of persisting forever.
3. **Clamp**: speed never drops below `flySpeed` (the throttle floor — this is
   the "no stall" rule) and never exceeds `flySpeed * maxSpeedMultiplier`.

The drag term gives a natural terminal dive speed before the hard cap:
`flySpeed + diveAcceleration * |sin(heading)| / speedDrag`.

## Tunables (PlayerConfig)

| Field | Default | Meaning |
|---|---|---|
| `flySpeed` | 180 (asset) | Cruise speed and the guaranteed minimum (m/s) |
| `diveAcceleration` | 90 | Gravity pull along the path at straight-down (m/s²) |
| `speedDrag` | 0.9 | Fraction of excess speed shed per second |
| `maxSpeedMultiplier` | 1.6 | Hard cap as a multiple of `flySpeed` |

With the defaults: a straight-down dive tends toward 180 + 90/0.9 = 280 m/s
(capped at 288), a 45° dive toward ≈ 250 m/s, and after levelling out the
excess halves roughly every 0.8 s. Keep `bulletSpeed` (400) well above the
cap so rounds still pull away from the plane in a dive.

## Scope

Player only. Enemies (`EnemyController`) keep their constant `flySpeed`. The
shot-down fall is a separate mode (real rigidbody gravity, see
`CubeController.BeginFall`) and is untouched by this model.

## Soft side boundaries (`FlightSteering`)

Shared by the player and every enemy fighter — headings are radians in the XY
play-plane, `+Y (up) = π/2` for both. While a plane is inside the edge-margin
band at a side and its heading still carries it toward that edge,
`FlightSteering.EdgeSteer` forces the desired turn rate to bank it back
toward the centre; the forcing scales with how deep the plane has pushed into
the band (0 at the inner lip, full rate at the edge), so a shallow intrusion
gets a nudge and a deep one gets turned hard before it ever leaves the world.
Outside the bands the caller's own input rate passes through unchanged.
Campaign mode replaces this with a hard left wall instead (see
docs/campaign.md).

## Shot-down fall (`PlaneFall`)

Zero health never destroys a plane outright on either side: the player and
every enemy fighter fall out of the sky, burning, and only explode where they
hit the ground (see docs/effects.md). Both `CubeController.BeginFall` and
`EnemyController.BeginFall` go through the shared static `PlaneFall`, so the
two sides fall identically:

- `Begin` kicks the nose down (`InitialDrop`) and sets a random tumble spin
  about Z — the only rotation axis a plane's constraints leave free.
- `Step`, called from the falling branch of each controller's `FixedUpdate`,
  integrates `Gravity` into the rigidbody's velocity itself and bleeds the
  leftover forward momentum (`HorizontalDrag`), so the plane pitches into a
  dive instead of gliding sideways down.

`Gravity` (~15× Unity's default 9.81) is scaled up because the world is
compressed relative to real scale — a plane spans ~60 units in a 700 m arena,
so real gravity would read as a slow, weightless drift. It is integrated by
hand rather than by switching `useGravity` on: every plane spawns with gravity
off and the earlier player-only version had to override the *global*
`Physics.gravity` to get the scaled-up pull, which is scene-wide state for
what is a per-plane effect — and now that enemies fall too, more than one
plane can be falling at once.

`PlaneFall.Timeout` is the enemy-side safety net: a wreck that has been
falling that long without an impact (its terrain chunk streamed away behind
the camera) is removed rather than left falling forever.

## Collision & damage (plane-to-plane scrapes)

Planes never physically collide with each other. Both level controllers call
`PlaneScrapes.DisablePlanePlaneCollisions` on startup to disable self-collision
on the shared plane physics layer, because two script-driven rigidbodies that
overwrite their own velocity every step can never settle a real contact —
they'd jam and judder. This replaces an earlier per-pair
`Physics.IgnoreCollision` that could be defeated by the timing of
runtime-created colliders, which was letting contacts through and ram-exploding
both planes.

Both halves — the layer opt-out and the scrape sweep below — live in the shared
static `PlaneScrapes` helper precisely because they are one mechanism: a
controller that sets up one without the other falls back to real physics
contacts, and `EnemyController.OnCollisionEnter` then ram-explodes every
fighter the player touches. `CampaignLevelController` had exactly that gap
(neither half wired up) while `LevelController` carried a private copy of both,
so campaign and custom battles ram-killed enemies on contact while the player
flew on unscathed — the player's own handler already skipped `EnemyController`
contacts. Note the layer opt-out is global Unity state that survives scene
loads, so playing a skirmish first used to mask the campaign bug.

`PlaneScrapes.Check` runs every physics step from each controller's
`FixedUpdate` and sweeps two kinds of pair, with **two different hit tests**.

**Player against an enemy** is a fuselage-core test: `HitboxRadius` is 15 m
against a ~60 m model span, so the two origins have to come within 30 m and
only a real fuselage overlap counts — a wingtip clipping a tail slips past.
That tolerance is deliberate: the player aims, and a core that reads as
"I flew *into* him" is what a rammed hit should feel like.

**Enemy against enemy** is an exact test — a cheap origin-distance broad phase
at the pair's mean model span, then `Physics.ComputePenetration` on the two
planes' real convex hulls (`EnemyController.Hitbox`, convex by construction in
`PlaneFactory.AddPlaneCollider`), so a scrape lands exactly when the two models
actually intersect. The fuselage core is the *wrong* test here and was why
enemies flew through each other unharmed while the player's rams worked: no AI
plane ever aims at another, so two fighters converging on the player cross at
an angle and their origins rarely close the last 30 m, even as their silhouettes
plainly collide. Hulls have no such blind spot. If either collider is missing or
already disabled the pair falls back to the fuselage core.

Either way the pair takes a scrape via `CubeController.Scrape` /
`EnemyController.Scrape`, which shaves off a fixed amount of health on both
planes, shivers the model and throws sparks, gated by a per-plane cooldown so
one encounter (which can span several frames of overlap) is a single hit — an
enemy pair that stays interlocked therefore grinds itself down 10 points every
0.5 s until one of them falls. A scrape the player is part of also kicks the
camera with a short, decaying jitter applied on top of the normal follow
smoothing (kept separate so the jitter can't feed back into the follow itself).
Ground contact is a separate
path (`OnCollisionEnter`): bullets never reach it (they already apply damage
via `TakeDamage`), and plane-plane contact normally can't reach it either since
collisions are disabled — but the handler swallows a stray contact
defensively so it can never fail the level on its own.

Both planes share one more threshold: below a fixed fraction of health they
start trailing damage smoke (`SmokeTrail.Arm`, idempotent, see
docs/effects.md) — the same threshold for the player and every enemy fighter,
so both start smoking at the same relative damage.

Both level controllers also push the active URP asset's runtime
`shadowDistance` out to cover the full camera-to-play-plane depth (~420 m)
plus margin — the asset ships with a 50 m default, far short of where the
camera actually sits, so without this the plane would never cast a visible
shadow. This is set at runtime so the shared RP asset used by every other
scene is left untouched.

## Enemy AI (`EnemyController`)

A mirrored Fokker Dr.1 (it attacks from the right and flies left) flying the
same constant-speed physics as the player, tuned via `EnemyConfig` and driven
by a state machine ported 1:1 from the sibling repo's `FighterPlane`:

| State | Behaviour |
|---|---|
| `Attack` | Chase the player with lead-prediction aim (`PredictIntercept`); guns fire once the nose is within the aim threshold of the intercept point. Times out into `Fly`. |
| `Fly` | Break away toward high altitude, weaving side to side over roughly where the player was. Guns silent. Times out back into `Attack`. |
| `Evade` | Entered by taking a hit while attacking or flying: a full corkscrew roll (heading forced ~π off to a randomly picked side, tracked by accumulated turn until a full 2π closes it), then a `Jitter` phase — a dash away from the player with the flee heading re-randomized `jitterHz` times a second so aimed fire keeps missing — then an `Unroll` back into `Attack`. |
| `Recover` | Entered whenever altitude drops below `minAltitudeMargin`, overriding every other state: climb hard at a fixed 70° until back above `safeAltitudeMargin`, then return to `Attack`. |
| `Return` | Entered when the fighter drifts off camera: fly straight back toward the player until it's on screen again, then resume `Attack`. |

Being *shot* (`TakeDamage`) knocks an `Attack`/`Fly` fighter into `Evade`; a
plane-to-plane scrape (`Scrape`, see above) applies damage directly without
provoking an evade, so a bump keeps the fighter flying its current run. The
same shared `FlightSteering.EdgeSteer` boundary that steers the player away
from the world edges overrides the AI's desired turn rate near an edge too.

`PredictIntercept` is a two-pass iterative lead: it estimates the time a
round would take to reach the player's current straight-line-extrapolated
position, then re-estimates from where that gives, converging quickly enough
in two passes for the plane's turn rates.

A world-space health bar (dark backplate + emissive fill, left-anchored pivot
scaled by the health fraction) floats above the fighter, deliberately outside
its hierarchy so the fighter's banking never tilts it. Below a shared health
threshold both sides start trailing damage smoke (see "Collision & damage"
above); at zero health the fighter stops flying the AI entirely and falls as a
burning wreck (`PlaneFall`, above), exploding on impact via the same
`Explosion` / removal-delay sequence as the player's crash (docs/effects.md).
The kill is reported to the level the moment the fall starts, not when the
wreck lands, so `IsAlive`, the level's enemy list and the campaign's wave
pacing all treat a falling fighter as already dead — its engine voice cuts
out, it can't be damaged or scraped again, and the next wave doesn't wait on
the wreck.
