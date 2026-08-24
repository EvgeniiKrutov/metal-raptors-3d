# Player flight model: constant speed, constant turn radius

Implemented in `CubeController`, tuned via `PlayerConfig`
(Assets/Resources/PlayerConfig.asset).

## Concept

`_speed = CruiseSpeed` — one line, evaluated every physics step. The plane flies
at a single speed no matter where its nose is pointing, and the only thing that
changes it is a skill.

This is a deliberate trade. The plane used to run a dive-energy model: gravity
along the flight path accelerated it nose-down, drag bled the excess off, and a
straight-down dive tended toward 280 m/s against a 180 m/s cruise. It read well
and it was the wrong model for this game. **Turn radius is `speed / turnRate`**,
so a speed that changed with pitch was a radius that changed with pitch: the
same held turn key described a different arc depending on how the plane had
entered it, and nothing on screen told the player which arc they were about to
fly. In a game whose whole difficulty is threading a plane past terrain and
through a gunsight, a turn you cannot predict is not a challenge, it is a coin
toss.

With a constant speed the radius is fixed too, and it is the *same* fixed radius
every time. You learn one arc and it is true for the rest of the game.

Speed is a single scalar along the heading. There is no air brake and no
throttle key — an earlier S / DownArrow brake (with its own recovery branch and
turn-rate bonus) was removed along with its four `PlayerConfig` fields.

## The one thing that changes it

The R boost (docs/boost.md), and it moves speed and turn rate **together**:

```
speed    = flySpeed      * boost      (CruiseSpeed)
turnRate = rotationSpeed * boost      (MaxTurnRate)
```

so `radius = speed / turnRate = flySpeed / rotationSpeed` — independent of the
boost. Boosting makes the plane cover the same arc faster, not a wider one. That
is what makes the skill safe to fire in a tight spot: it never widens the turn
you are already committed to.

## Tunables (PlayerConfig)

| Field | Default | Meaning |
|---|---|---|
| `flySpeed` | 180 (asset) | The plane's speed, full stop (m/s) |
| `rotationSpeed` | per plane | Turn rate (°/s); with `flySpeed` it fixes the radius |
| `turnResponsiveness` | asset | How fast the turn rate eases toward the commanded one |

`diveAcceleration`, `speedDrag` and `maxSpeedMultiplier` are no longer read by
`CubeController`. They stay on `PlayerConfig` because two other things still use
them: `PlaneLoadout` divides the garage's `maxSpeed` stat by `maxSpeedMultiplier`
to get `flySpeed` (see below), and `DuelPlane` — the background companion and its
foe — still flies the dive-energy model from the same asset. The enemy
**fighter** keeps its own copy of the model in `EnemyFighterConfig.asset`, so the
AI can still trade altitude for speed even though the player cannot.

Keep `bulletSpeed` (400) well above the plane's speed so rounds pull away from
it; at a constant 180 (234 under the 1.3 boost) there is plenty of room.

## The asset is a baseline, not the whole config

`PlayerConfig.asset` is loaded once per level and then **copied per plane**.
`PlaneLoadout.Build` instantiates it and overwrites the six fields the garage
shows as stat bars from the selected plane's `PlaneStats` (docs/garage.md):

| stat bar | `PlayerConfig` field | conversion |
|---|---|---|
| max speed | `flySpeed` | `maxSpeed / maxSpeedMultiplier` (see note) |
| rotation speed | `rotationSpeed` | direct |
| mass | `mass` | direct |
| fire rate | `fireRate` | `1 / fireRate` (bar is shots/s, field is seconds between) |
| damage | `damage` | direct |
| health | `health` | direct |

The `maxSpeed` conversion is kept as it was on purpose. Under the old model
`maxSpeed` was the plane's *dive* speed and `flySpeed` its cruise; now that the
plane only ever flies `flySpeed`, dividing by 1.6 still gives every plane the
speed it used to cruise at. That keeps the whole enemy balance intact — the
fighter's 180 m/s is documented as "the player's own cruise", and it still is —
and the garage bar stays honest as a *comparison* between planes, since every
plane is scaled by the same factor. Raise `flySpeed` by raising `maxSpeed`.

Everything else on the asset — `diveAcceleration`, `speedDrag`,
`maxSpeedMultiplier`, `turnResponsiveness`, `bulletSpeed`, every bomb and boost
field — is shared by every plane and is edited only in the asset. So the numbers
in the table above still describe the Camel exactly (its stat block is set to the
asset's own values); the Dr.I flies at 165 instead of 180.

It is a **copy** rather than an edit in place because `Resources.Load` hands back
the asset itself: writing the selected plane's numbers onto it would dirty
`PlayerConfig.asset` on disk in the editor and leak the last-flown plane's
handling into the next session.

## Scope

The player only. The enemy **fighter** still runs the old dive-energy model from
its own `EnemyFighterConfig.asset` (docs/enemies.md) — trading altitude for speed
is the fighter's whole identity, and unlike the player it is not the thing the
human has to aim — while the enemy **scout** flies a constant `flySpeed` as the
player now does. Neither is touched by the per-plane loadout: an enemy Albatros
and a player-selected Albatros have nothing in common but the model.
The companion wingman is handed the shared `PlayerConfig` rather than a loadout
(docs/companion.md). The shot-down fall is a separate mode (real rigidbody
gravity, see `CubeController.BeginFall`) and is untouched by this model.

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

## Auto-righting half roll (`PlaneRoll`)

A plane that flies "backwards" along the play plane is upside down, because the
only rotation the flight model applies is Z = heading: at heading π the body is
turned 180°, which points the nose the right way but puts the wheels up. Left
alone the plane cruises inverted for the rest of the run, which reads as a bug.

`PlaneRoll` fixes it with the manoeuvre a pilot would use — half a roll about
the nose axis, so the plane keeps its heading and comes out wheels-down. Every
plane owns one instance: the player (`CubeController`), each enemy fighter
(`EnemyController`) and both background duellists (`DuelPlane`, so the
companion and its foe, see docs/companion.md). Each controller multiplies the
roll into its `ApplyRotation` as `Euler(0,0,heading) * Euler(roll,0,0)` — the
local X axis is the nose axis once the heading rotation is applied.

The roll is deliberately unhurried, and its rate comes from the airframe rather
than from a fixed duration: `RollRateFraction` (0.6) of that plane's own
`rotationSpeed`, so a 180 °/s fighter rolls at 108 °/s and takes ~1.7 s to come
round. A plane that turns faster also rolls faster, which is what keeps the
manoeuvre reading as the same aircraft — a fixed half-second flip looked like a
snap the airframe could not have made.

### Which way is up

The model itself is baked either wheels-down-facing-right or, for `mirrored`
planes (every enemy, and the companion's foe), wheels-down-facing-left — see
`PlaneFactory.BuildPlaneModel`. So uprightness is
`cos(heading) * cos(roll)`, negated for a mirrored plane, and the plane is
inverted when that product is negative. Because the test reads the *current*
roll it is self-correcting: after a flip to 180° the same rule fires again the
next time the plane turns back the other way.

### When it fires

All three conditions must hold continuously for `InvertedDelay` (1 s):

- **Inverted**, per the test above.
- **Near-horizontal**: `|cos(heading)| >= LevelCos` (0.5), i.e. within 60° of
  level. Pointing straight up or straight down is neither wheels-up nor
  wheels-down, and rolling there reads as a random twitch; the plane waits
  until it levels out. This is also what stops a loop from triggering a flip
  halfway round — the inverted stretch of a loop is short and mostly steep.
- **Flying steady**: for the player, neither turn key held; for every AI plane,
  `PlaneRoll.Steady` — turn rate at or below `SteadyTurnFraction` (15 %) of the
  plane's own maximum. A fighter mid-turn keeps the orientation its manoeuvre
  gave it.

The delay is what makes a deliberate inverted pass still possible: keep turning,
or keep the nose steep, and the plane holds its attitude.

`PlaneRoll.Flip` is the way past the delay: it starts the same half roll on the
spot if — and only if — the plane is inverted by the same test and is not
already rolling. The wingman's return to formation uses it (docs/companion.md)
so that step of the sequence begins the frame the one before it ends instead of
waiting out a second of level flight first.

### The roll itself

Smoothstepped rotation from the current angle to ±180° over
`180 / (rotationSpeed * RollRateFraction)` seconds — the rate is captured when
the flip starts, so a boost part-way through does not speed it up mid-roll, and
a floor of 20 °/s keeps a badly configured plane from rolling forever. The
direction is chosen at random per flip so a formation of enemies doesn't roll in
lockstep. It is a pure aileron roll: heading, speed and position are untouched,
so it can never steer the plane or spoil a shot, and input keeps working
normally throughout — a turn pressed mid-roll steers as usual and the roll still
finishes. Once started the flip always completes; the timer only gates the
*start*. The angle is wrapped back into (-180°, 180°] on completion so repeated
flips don't accumulate.

The roll is applied to the plane's **body**, not just its model, so everything
mounted on the body rolls with it. That is what makes the fix more than
cosmetic: `PlaneBomber` releases from `-transform.up`, which on an inverted
plane pointed *upward* and threw bombs over the wing.

`DuelPlane` already had a roll of its own — the bank it leans into turns and
depth changes. The two share the axis, so they add, with the bank scaled by
`cos(flip)`: at 180° the bank is negated, which keeps it leaning into the turn
as seen by the camera rather than away from it, and scaling (rather than
flipping the sign at 90°) keeps that transition continuous through the roll.

Skipped while falling and while sinking — `PlaneFall` and `DuelPlane`'s fall
roll own the rotation there (the flip angle it left behind is still added in,
so it is carried through the fall rather than snapped away). Otherwise it runs everywhere the plane flies,
campaign cutscenes and the level intro included; during a cutscene the player
counts as flying steady, since a plane with no pilot input never blocks a flip.

## Shot-down fall (`PlaneFall`)

Zero health never destroys a plane outright on either side: the player and
every enemy fighter fall out of the sky, burning, and only explode where they
hit the ground (see docs/effects.md). Both `CubeController.BeginFall` and
`EnemyController.BeginFall` go through the shared `PlaneFall`, so the two sides
fall identically — and identically to the background duel's losing plane, which
is where the animation came from (`DuelPlane`, docs/companion.md).

It is a **diving barrel roll**, not a tumble: a pilot-less aircraft still has
wings, and what it does is fall away in a long rolling dive. Each falling plane
owns one `PlaneFall` instance holding its own heading, speed and roll angle:

- `Begin(rb, heading, speed)` takes the plane's live heading and speed and picks
  the dive target: `DiveDeg` (−38°) when the plane is flying right,
  `180 − DiveDeg` when it is flying left, so the nose always drops toward the
  ground rather than swinging through the vertical. It also zeroes the
  rigidbody's angular velocity — the roll is animated, not solved.
- `Step`, called from the falling branch of each controller's `FixedUpdate`,
  eases the heading toward that dive angle (`DiveResponse`, an exponential
  approach, so the nose *drops* over about a second instead of snapping down),
  adds `SpeedGain` (20 u/s²) to the speed, winds the roll on by
  `RollRateDeg` (230°/s), and drives the rigidbody's velocity straight along
  the heading.

The controllers read `Heading` and `Roll` back out and feed them into their
existing `ApplyRotation`, whose `Rz(heading) · Rx(roll)` form already puts the
roll on the nose axis — so the wreck rolls about its own length while it dives.
`PlaneRoll`'s flip angle (above) is added to the fall roll rather than replaced,
so a plane shot down mid-flip carries that attitude into the fall.

There is no gravity in it at all. The old fall integrated a scaled-up `Gravity`
(~15× 9.81, because the world is compressed relative to real scale) and bled off
the forward momentum with a `HorizontalDrag`, which produced a plane that stalled,
stopped and dropped. Driving the velocity along a diving heading instead means the
wreck keeps flying while it falls, and the speed *gain* rather than an acceleration
term is what reads as it running away downhill. Gravity stays off on every plane's
rigidbody, as it always has.

`DuelPlane` keeps its own kinematic implementation — it has no rigidbody — but its
four fall constants are now aliases of `PlaneFall`'s, so the background duel and
the gameplay planes cannot drift apart under a retune.

`PlaneFall.Timeout` is the enemy-side safety net: a wreck that has been
falling that long without an impact (its terrain chunk streamed away behind
the camera) is removed rather than left falling forever.

### The death cam

The player's wreck is followed all the way to the explosion. The camera was already
tracking the plane's transform — the body object outlives the model precisely so it
stays the follow target — but two things kept it from reading as a follow, and both
level controllers now switch them at `OnShotDown` (a `_playerFalling` flag that is
never cleared; the level is over either way):

- **The follow loosens.** `CamResponse` (8) drops to `FallCamResponse` (3.3), roughly
  a 0.3 s time constant instead of 0.125 s, so the camera visibly trails the diving
  wreck and settles onto it rather than staying servo-locked. It is still catching up
  as the plane hits the ground, which is what gives the impact its drift.
- **The floor drops.** `PositionCamera` clamps the camera above
  `CutRevealY + halfViewHeight` so the bottom of the frame never falls past the
  terrain's cut edge. While the player is falling that limit moves down to
  `WallBottomY` (−120 instead of −80) — the full depth of the cut wall
  `ProceduralTerrain.BuildCutWallMesh` actually draws, on the arena and on all three
  campaign terrains alike, which all place that wall at world y = 0. Those 40 units
  are the entire budget: below the wall's bottom edge there is nothing to see but sky
  under the ground, so the camera cannot follow a wreck further down than this no
  matter how it is tuned, and the impact is framed low by design.

Nothing resets the flag on the coast's ditch either, so a plane that sinks into the
sea keeps the same held shot.

One side effect worth knowing: the campaign's `Distance` readout stops counting once
the player is falling. The wreck flies forward as it dives, and without the gate a
shot-down player would be credited with the distance their own wreck coasted.

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
`FixedUpdate` and sweeps exactly **one** kind of pair: the player against each
enemy. It is a fuselage-core test — `HitboxRadius` is 15 m against a ~60 m model
span, so the two origins have to come within 30 m and only a real fuselage
overlap counts; a wingtip clipping a tail slips past. That tolerance is
deliberate: the player aims, and a core that reads as "I flew *into* him" is what
a rammed hit should feel like.

**Enemies do not damage each other by contact.** Two of them can fly through the
same piece of sky and both come out untouched. There was an enemy-against-enemy
sweep here once, and it was a mistake: no AI plane aims at another, so every
enemy-enemy contact is incidental — a wave converging on the player from
different angles crosses itself constantly — and turning that into damage meant
waves quietly killed themselves off-camera while the player watched. It also made
the fight unreadable, since the health you saw on a bar was not the health you
had put there. Enemies are the player's problem to solve, so only the player can
take them apart.

The sweep gates on **depth** as well as distance. Enemies normally share the
player's Z lane, but a scout mid-dodge (docs/enemies.md) is sliding out of it, so
an enemy more than `EnemyDepthDodge.ClearDepth` (35 m) from the player in Z never
scrapes. Past that distance the scout is already `OffPlane` and skipped outright;
the gate covers the stretch of the slide before it gets there, so the manoeuvre
cannot be punished by a collision it has visibly avoided.

A hit takes a scrape via `CubeController.Scrape` / `EnemyController.Scrape`,
which shaves off a fixed amount of health on both planes, shivers the model and
throws sparks, gated by a per-plane cooldown so one encounter (which can span
several frames of overlap) is a single hit. It also kicks the camera with a
short, decaying jitter applied on top of the normal follow smoothing (kept
separate so the jitter can't feed back into the follow itself). Ground contact is
a separate path (`OnCollisionEnter`): bullets never reach it (they are on their
own physics layer, below), and plane-plane contact can't reach it either since
those collisions are disabled — but the handler swallows a stray contact
defensively so it can never fail the level on its own.

## Bullets only hit the other side (`Bullet`)

**There is no friendly fire.** A round carries the side that fired it
(`Launch(..., fromEnemy)`) and damages only the opposite one: player fire hurts
enemies, enemy fire hurts the player, and an enemy round that crosses another
enemy passes straight through it — no damage, no impact, the round keeps flying.
`Hostile` is the whole rule, one line against `target is EnemyController`.

Rounds are on their own physics layer (`Bullet.Layer`, 12), and `BuildTemplate`
turns off collisions between it and `PlaneFactory.PlaneLayer` — and between it
and itself, so rounds don't knock each other about. So a bullet has **no physical
contact with any plane at all**, which is what lets a friendly round pass through
without deflecting off the plane it is not allowed to hurt. Damage is decided
entirely by an explicit swept sphere: `Bullet.FixedUpdate` runs a
`SphereCastNonAlloc` of `HitRadius` (3 m) along exactly this step's motion
(`linearVelocity * fixedDeltaTime`) against the plane layer, takes the nearest
*hostile* damageable that is not the shooter, and applies the damage.

Doing it by sweep rather than by contact also fixes an accuracy problem. A round
travels 8 m per physics step and the planes it is aimed at are thin convex mesh
colliders on scaled transforms, moving at up to 330 m/s themselves; continuous
detection is supposed to cover that but shots that plainly passed through a
fuselage were being dropped. A sweep along the step's own path cannot miss by
geometry, and a 3 m sphere instead of a 2.4 m cylinder is marginally more
generous — the right direction for a shot the player felt they had earned.

`OnCollisionEnter` still fires for terrain and anything else off the plane layer;
it just destroys the round. A `_spent` latch makes sure whichever path fires
first is the only one that counts.

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

A mirrored plane (it attacks from the right and flies left) driven by a state
machine originally ported from the sibling repo's `FighterPlane`. Since the
scout/fighter split it comes in two roles, each with its own config asset, its
own altitude band, its own reversal profile and one ability apiece — all of that
is **docs/enemies.md**, and only the shared core is described here. The scout
flies the constant speed below; the fighter runs the player's dive-energy model.

| State | Behaviour |
|---|---|
| `Attack` | Chase the player with lead-prediction aim (`PredictIntercept`) and shoot whenever the fire gate below opens. On timing out it breaks away into `Fly` only if the player is inside `maxFireRange` — a merge just happened and there is something to break away *from*. Otherwise it re-arms another attack run rather than disengaging from a fight it is not yet in. |
| `Fly` | Break away and reposition, weaving side to side over roughly where the player was, climbing to `flyPerchHeight` above the player (clamped inside the soft floor/ceiling band). Guns stay live for snap shots as the nose swings across the player. Times out back into `Attack`. |
| `Evade` | A single hard break: one heading, held for `evadeDuration`, `evadeBreakAngle` off the line directly away from the player — so the fighter crosses the attacker's gunsight instead of running down it — with `jitterAmplitude` of random wobble re-rolled `jitterHz` times a second so tracking fire keeps missing. The side of the break is picked for airspace, whichever of the two candidates has more room before the floor or ceiling, choosing randomly when they are comparable. Then straight back into `Attack`. |
| `Recover` | Entered whenever altitude drops below `minAltitudeMargin`, overriding every other state: climb hard at a fixed 70° until back above `safeAltitudeMargin`, then return to `Attack`. |
| `Return` | Entered when the fighter drifts off camera: fly straight back toward the player at `ReturnSpeedFactor` × cruise until it's on screen again, then resume `Attack`. The catch-up speed exists because cruise alone is below the player's, so a fighter that fell behind used to trail off screen for the rest of the level; it only applies while the fighter is off camera, where the speed-up cannot be seen. |
| `DiveClimb` / `DiveRun` / `DiveZoom` | The fighter's committed diving pass, docs/enemies.md. |

On-screen, a player who runs is chased by the **engagement boost** rather than by
cruise speed: past `engageRange` and moving away, the enemy's speed target becomes
the player's own speed × `engageFactor`, ignoring its configured `flySpeed`.

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

### The fire gate (`UpdateFiring` / `HasFiringSolution`)

Firing is decided by geometry, not by which state the fighter happens to be
in. Every state except `Return` (which only runs off camera, where a shot
would be invisible anyway) and the scripted `StandDown` may shoot, provided
the player is on camera and inside `maxFireRange`.

The gate itself is checked twice per fixed step, once against the lead point
from `PredictIntercept` and once against the player's *present* position;
either one opening is enough to pull the trigger. A candidate point passes if
the nose is within `fireAngleThreshold` of it — the ordinary deflection shot —
or, failing that, if the burst would still pass within `SnapWindowFactor`
target radii of it, capped at `SnapFireConeDeg` off the nose so the fighter
never sprays sideways. Because that second clause is a miss *distance*, it
opens the cone only where a plane-width miss is a real angle: at knife range
it allows a wide snap shot, at 400 m it is tighter than the base threshold and
changes nothing.

Both halves fix the same complaint — a fighter turning onto the player and
holding fire. Gating on the intercept point alone meant that in a close
crossing pass, where the lead angle exceeds `fireAngleThreshold` outright and
the limited turn rate leaves the nose trailing on the player rather than ahead
of them, the guns stayed cold with the player square in the gunsight. Silencing
the guns for the whole of `Fly` did the same thing for roughly a third of every
attack cycle.

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
