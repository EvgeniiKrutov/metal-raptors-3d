# Player flight model: cruise + dive energy

Implemented in `CubeController` (`UpdateSpeed`), tuned via `PlayerConfig`
(Assets/Resources/PlayerConfig.asset).

## Concept

The plane flies at constant throttle. The engine is strong enough that the plane
never stalls — it can fly a full loop and never drops below cruise speed. But it
is not a constant *speed*: pointing the nose at the ground trades altitude for
airspeed, so dives are faster than cruise.

Speed is a single scalar along the heading, and there is **one** branch: the
cruise/dive model below. There is no air brake and no throttle key — an earlier
S / DownArrow brake (with its own recovery branch and turn-rate bonus) was
removed along with its four `PlayerConfig` fields. The only speed the player
commands directly is the R boost (docs/boost.md).

## Cruise and dive

Speed changes by three terms each physics step:

1. **Gravity along the flight path**: `-sin(heading) * diveAcceleration`.
   Nose-down (sin < 0) accelerates the plane; nose-up decelerates it, which
   bleeds off dive speed on the way back up (a dive-then-zoom-climb roughly
   conserves that energy).
2. **Drag on the excess**: `(speed - cruise) * speedDrag` is shed per second,
   so extra speed also decays in level flight instead of persisting forever.
3. **Clamp**: speed never exceeds `cruise * maxSpeedMultiplier` and never drops
   below `cruise` — the "no stall" rule, now a hard floor rather than an
   effective one.

`cruise` is `flySpeed` scaled by the boost factor, which is 1 outside a boost;
both the floor and the cap ride it, so boosting shifts the whole speed band up
rather than just raising the ceiling.

The drag term gives a natural terminal dive speed before the hard cap:
`cruise + diveAcceleration * |sin(heading)| / speedDrag`.

## Tunables (PlayerConfig)

| Field | Default | Meaning |
|---|---|---|
| `flySpeed` | 180 (asset) | Cruise speed and the guaranteed minimum (m/s) |
| `diveAcceleration` | 90 | Gravity pull along the path at straight-down (m/s²) |
| `speedDrag` | 0.9 | Fraction of excess speed shed per second |
| `maxSpeedMultiplier` | 1.6 | Hard cap as a multiple of cruise |

With the defaults: a straight-down dive tends toward 180 + 90/0.9 = 280 m/s
(capped at 288), a 45° dive toward ≈ 250 m/s, and after levelling out the
excess halves roughly every 0.8 s. Keep `bulletSpeed` (400) well above the
cap so rounds still pull away from the plane in a dive — under boost the cap
rises to 374, which is still clear of it.

## Scope

Player only. Enemies (`EnemyController`) keep their constant `flySpeed`. The
shot-down fall is a separate mode (real rigidbody gravity, see
`CubeController.BeginFall`) and is untouched by this model.

## Soft side boundaries (`FlightSteering`)

Headings are radians in the XY play-plane, `+Y (up) = π/2`, for the player and
every enemy alike. The two sides keep to the world box by different means.

**The player** uses `FlightSteering.EdgeSteer`: while the plane is inside the
edge-margin band at a side and its heading still carries it toward that edge,
the desired turn rate is forced to bank it back toward the centre, scaled by
how deep the plane has pushed into the band (0 at the inner lip, full rate at
the edge). Outside the bands the pilot's own input rate passes through
unchanged. Campaign mode replaces this with a hard left wall instead (see
docs/campaign.md).

**Enemies** use `FlightSteering.Contain`, which limits the *heading the AI
asks for* rather than the turn rate it gets. Penetration into each band (both
sides, the ceiling, and the soft altitude floor) becomes a push vector, which
is added to the desired direction with enough weight that a full-depth
intrusion dominates it; the result is re-normalised back into a heading. Deep
in the band the commanded heading therefore always points back into the box,
and in a corner it points diagonally inward.

An AI plane cannot use the rate-forcing version. `EdgeSteer` fights the pilot
rather than replacing its goal, and the two balance exactly at ±90°: a fighter
that wants to keep going right while pinned against the right-hand band settles
nose-up and stays there. Nose-up plus the ceiling clamp below is a dead stop —
the fighter freezes in the top corner with zero velocity and no state that can
end it, which is what used to strand fighters at the top of the screen until
the player flew far enough away to push them off camera (off camera is
`Return`, the only state that could break the deadlock). `Contain` has no such
equilibrium: the push is a *direction*, so the commanded heading always carries
the plane inward and the fighter flies out of the band under its own power.

## Ceiling

The permanent horizontal limit is the ceiling (`_ceilingY`, set at `Initialize`):
`CubeController.FixedUpdate` zeroes the velocity's Y component when the plane is
at it and still climbing, then clamps the position. There is deliberately **no
floor** — nothing stops the player flying into the ground, that is the game.

`EnemyController` keeps the same clamp, but for the AI it is only a backstop:
`Contain` starts pitching the nose down a fixed margin below the ceiling, so a
fighter reaches the clamp already turning away instead of pressing into it
nose-up (which zeroes *both* velocity components, since a vertical heading has
no X component left to slide on). Enemies also get a soft floor there, spanning
`minAltitudeMargin` to `safeAltitudeMargin`, which leans the nose up before the
hard `Recover` pull-up is needed — the fighter now mostly skims the low band
instead of repeatedly yanking into a 70° climb out of it.

During a campaign cutscene a second clamp of the same shape appears, but at the
*actual terrain surface* rather than at a fixed height: plane–ground collisions
are disabled and the ground is found by a downward raycast instead, so the plane
skims the dirt under full control with no physics response to fight. See
docs/level-intro.md.

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
by a state machine originally ported from the sibling repo's `FighterPlane`:

| State | Behaviour |
|---|---|
| `Attack` | Chase the player with lead-prediction aim (`PredictIntercept`); guns fire once the nose is within the aim threshold of the intercept point. On timing out it breaks away into `Fly` only if the player is inside `maxFireRange` — a merge just happened and there is something to break away *from*. Otherwise it re-arms another attack run rather than disengaging from a fight it is not yet in. |
| `Fly` | Break away and reposition, weaving side to side over roughly where the player was, climbing to `flyPerchHeight` above the player (clamped inside the soft floor/ceiling band). Guns silent. Times out back into `Attack`. |
| `Evade` | A single hard break: one heading, held for `evadeDuration`, `evadeBreakAngle` off the line directly away from the player — so the fighter crosses the attacker's gunsight instead of running down it — with `jitterAmplitude` of random wobble re-rolled `jitterHz` times a second so tracking fire keeps missing. The side of the break is picked for airspace, whichever of the two candidates has more room before the floor or ceiling, choosing randomly when they are comparable. Then straight back into `Attack`. |
| `Recover` | Entered whenever altitude drops below `minAltitudeMargin`, overriding every other state: climb hard at a fixed 70° until back above `safeAltitudeMargin`, then return to `Attack`. |
| `Return` | Entered when the fighter drifts off camera: fly straight back toward the player at `ReturnSpeedFactor` × cruise until it's on screen again, then resume `Attack`. The catch-up speed exists because cruise alone is below the player's, so a fighter that fell behind used to trail off screen for the rest of the level; it only applies while the fighter is off camera, where the speed-up cannot be seen. |

### What provokes an evade

Two things, both gated by `evadeCooldown` seconds of attacking after the
previous evade ends:

- **Taking a hit** while in `Attack` or `Fly` (`TakeDamage`). A plane-to-plane
  scrape (`Scrape`, see above) applies damage directly without provoking one,
  so a bump keeps the fighter flying its current run.
- **Being tracked** (`UnderThreat`): the player is within `threatRange`, their
  nose is within `threatCone` of the fighter, and they sit more than
  `threatTailAngle` off the fighter's own nose. The last clause is what keeps
  the AI aggressive — only a genuine tail chase makes it break; a head-on merge
  is flown through and fought, because in a head-on both sides are equally
  exposed and flinching just hands over the pass.

The cooldown is what stops evasion from swallowing the fight. Without it a
player who lands a hit every couple of seconds keeps the fighter in evasion
permanently, and the fighter never shoots back — which, together with the
corner freeze described under "Soft side boundaries", is what made the old AI
harmless once the shooting started. The old evade was also enormous: two full
2π corkscrews at the fighter's turn rate, about 7 seconds of not attacking per
hit, and its middle jitter phase was in practice dead code — the phase timer
started at `EnterEvade` and had always expired by the time the first roll
finished, so the phase lasted a single frame.

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
