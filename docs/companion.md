# The companion (`CompanionFlight`, `DuelPlane`, `Tracer`)

The player does not fly a career level alone. One friendly fighter flies the level with them:
in formation while the film bars are up, and out in a **background dogfight** — a whole depth
layer behind the play plane — while the level is being played. It is theatre, not gameplay:
nothing in the background can hurt the player and the player cannot reach it.

## What it is configured with

Per level, on `CampaignDefinition` (`CampaignDefinition.cs`):

| Field | Default | Means |
| --- | --- | --- |
| `companion` | `false` | Whether this level flies with a wingman at all. Level 1 sets it `true`; level 2 does not. |
| `companionPlane` | `PlaneModels.Sopwith` | The wingman's model. Any `PlaneModelConfig` — the same registry the player and the enemy waves pick from. |
| `companionFoe` | `PlaneModels.Fokker` | The model of the plane it duels in the background. |

Custom battles never get one: `CampaignLevels.Custom` leaves the flag off, and
`CampaignLevelController.BeginCompanion` refuses on `CustomBattle.Requested` as well.

Nothing about the companion is in the level *script* (docs/campaign-scripts.md). The whole
sequence hangs off the cutscene state instead, so a level of any shape — three conversations
or seven — gets the same rhythm for free and no script has to mention it.

## The rhythm

`CampaignLevelController` already computes `Cinematic` every `LateUpdate`
(`LevelIntro` still running, or the film bars anywhere but fully down — docs/level-intro.md).
That one boolean drives everything, through `CompanionFlight.SetCinematic`:

| Cutscene state | Phase | What is on screen |
| --- | --- | --- |
| the intro, and every radio block | **Escort** | Both planes fly the same depth, in a tight parallel pair: the wingman sits **65 m ahead and 32 m above** — about one model length in front, clearly leading
without being out on its own — riding a slow sine up/down (and a slower one fore/aft, ±10 / ±12 m) so it breathes rather than sits glued in place. It is spawned straight onto that station, so at the fly-in the two enter the frame together and neither ever has to catch the other up. The wobble is small enough that the pair never close inside the 30 m bump reach on their own. |
| bars go **down** | **Peel** (1.6 s) | The wingman rolls onto a wing and slides 250 m back into the background layer, climbing 24° as it goes. A background foe enters from beyond the right edge at the same depth. |
| gameplay | **Duel** | The two of them fight, endlessly, until a cutscene ends it. |
| bars go **up** | **Rejoin** (four steps, below) | The background foe is killed on the spot and the wingman flies itself back to formation. The first radio line of the block is **held** until it is back — see "Coming back". |

Every cutscene repeats the cycle with a **fresh** foe, so a level never runs out of background
fight. The peel fires on the bars going down rather than on the first wave spawning: in level 1
that is the `task` op right after the opening conversation, so the split reads as "we're
separating" rather than as a reaction to enemies that are not there yet.

## Coming back

The wingman does not snap back into formation. The moment the bars go up the foe is killed, and
from there the return is flown as **four steps**, each one starting the instant the one before it
is finished — there is no timer holding one step open, and nothing is faded, lerped bodily or
teleported. Every step is a phase in `CompanionFlight` and a role on `DuelPlane`, so the plane is
flying its own flight model the whole way back.

| # | Phase | Role | Runs until | Cap |
| --- | --- | --- | --- | --- |
| 1 | `Align` | `Level` | The nose is within **10°** of straight and level (heading 0, the direction the player flies). Whatever the duel left it in — a break turn, a reposition arc, a dive — is flown out at the plane's own turn rate first. | 2.5 s |
| 2 | `Roll` | `Level` | The half roll is finished. `PlaneRoll.Flip` starts one **immediately** if the plane is on its back, instead of waiting out the 1 s inverted delay the spontaneous righting uses; if it is already upright there is nothing to do and the step costs a single frame. | 1.5 s |
| 3 | `Form` | `Form` | It is within **70 m** of the formation slot — i.e. it has climbed to station height *and* pulled ahead of the player. Still at duel depth: nothing has moved forward yet. | 8 s |
| 4 | `Close` | `Form` → `Escort` | The 250 m depth slide has run its **2.2 s**. Only now does the plane come forward to the play plane, and it does it already sitting in the slot, so the slide is the only motion on screen. | — |

Step 1 also freezes the depth where it is (`HoldDepth`), so a block of lines that starts while the
peel is still sliding outward does not finish going away before it turns round: it stops at
whatever depth it had reached and step 4 brings it back from there.

The caps are there so a script can never be wedged by a step that will not converge (the player
climbing away for the whole of step 3, say), not to pace the sequence. In practice each one ends
on its own condition well inside its cap.

### How step 3 flies

`Form` never steers *at* the station — a bearing to a point a few metres away swings through 180°
and puts a flick in exactly the moment the eye is on. It splits the job:

- **Height** is the heading. The nose is aimed at `atan2(rise, ahead)` where `ahead` is the X gap
  to the slot with a **130 m** floor, so the climb saturates at 45° when it is far below and
  flattens to level exactly as it arrives. The lookahead floor also means the bearing can never
  reverse: the plane never turns round to fly backwards at the slot.
- **Position along the run** is throttle. The commanded cruise is the *player's* cruise speed
  (`PlayerConfig.flySpeed`, 180 m/s — not the duel's 175) plus **0.55 per metre** of the gap,
  clamped to **+95 / −70 m/s**. Behind the slot it runs the player down; ahead of it — the common
  case, since the wingman is faster and step 1 flies it forward — it throttles back below the
  player and lets him catch up. At the slot the two speeds match by construction.

  The speed model eases onto that (drag, ~1.1 s), it is never assigned: `UpdateSpeed` takes the
  floor of its clamp separately from its target, so a raised cruise cannot snap the speed to it.
  `Form` is the one role that ignores the duel's pace trim ("Holding station is throttle", below) —
  it is already holding station on the player directly.

While it is forming up the wingman is held in the **escort** Y band (ground + 60 … top − 40)
rather than the duel band, both for the containment steer and the hard clamp — otherwise the duel
floor would fight the descent to a player flying low.

### The radio waits for it

`ICampaignScriptHost.CompanionReady` (`_wing == null || _wing.Formed`) is false for the whole of
the four steps. `CampaignScriptRunner.Say` blocks on it after the bars are in and before the
lead-in beat, so the first line of a block only starts typing once the wingman is back on the
wing — the block then runs normally, and later lines in the same block never wait again.

`CompanionFlight.Begin` runs *after* `LevelIntro` has parked the player off the left edge, so the
station it spawns onto is measured from the entry point: the wingman is already in formation when
the level opens and the pair fly in from the left together, in parallel. There is no join-up, no
lead to pay off and no stretch where one plane is flying at anything but cruise.

`StandDown` (level completed, crash, ditch, shot down) drops both planes into level flight with
the guns cold and releases their bounds, so they simply fly on out of frame.

## The background layer

`CompanionFlight.Depth` is **250 m** behind the play plane: play Z 100 → duel Z 350. With the
camera 420 m in front of the play plane, that is 670 m away, so the pair render at about **63 %**
of the player's size with no scaling anywhere — pure perspective. It also puts them behind the
cloud layer (Z 40–160, docs/clouds.md), so a cloud drifts in front of the duel now and then,
which is most of the depth cue.

### Staying on screen

The band the pair fight in is not a world rectangle — it is **the camera's own frustum, measured at
the duel depth**. `CampaignLevelController` hands `CompanionFlight.SetWindow` the camera *position*
and both half-view extents, and the flight scales them by `(CameraDistance + Depth) / CameraDistance`
= **1.595**: whatever the player's screen is, the visible half-width at Z 350 is that much wider than
at the play plane. Everything below is a fraction of that visible half-extent, so it holds at any
aspect ratio and any resolution.

| Bound | Value | Why |
| --- | --- | --- |
| X | camera − **0.72** … + **0.88** visible half-widths | Inside the frame with ~50 m to spare on the far side even for a 60 m model, and deliberately **asymmetric**: the fight sits forward of centre, in the half of the screen the player is flying into. |
| Y | camera ± **0.86** visible half-heights, intersected with ground + 140 … `WorldTop` − 110 | The world limits keep it off the terrain; the camera-relative half keeps it on the screen when the player is at the top or the bottom of the world. The old absolute band could sit 378 m above a low-flying player, which is the edge of the 386 m frustum — the wingman clipped off the top. |

The X band is not centred on the camera and neither is the fight: `StationX` is **0.12 half-widths
ahead** of it (≈ 80 m). That is the "slightly ahead" the pair are trimmed toward, so the duel leads
the player's eye rather than trailing off the back of the frame.

#### Holding station is throttle, not a fence

The old version had a constant 175 m/s cruise and a **catch-up hack**: cross into the last 100 m of
the left margin and the cruise jumped 1.45×. That only ever fired once the pair were already at the
edge, and a dogfighting plane's *forward* progress is well under its airspeed (a 55° break turn
makes 100 m/s of ground down a 175 m/s airframe), so against a player cruising at 180 — let alone
boosting at 234 — the fight sat pinned to the left margin and slid off it.

It is now a continuous trim, computed once per frame in `CompanionFlight.ApplyPace` and given to
**both** planes identically:

```
pace   = clamp(camera speed, 175, 280)          // measured, filtered at 3 /s
cruise = pace + clamp((StationX − centreX) × 0.35, −55, +90)
```

- `pace` is the **camera's** real X speed, so a boost or a dive is matched rather than reacted to.
- The trim is on the **pair's centre**, not on each plane. Both get the same number, so the duel's
  own geometry — the 190–380 m pass cycle, the break turns — is untouched; only the whole fight is
  translated back onto its station.
- `DuelPlane` eases onto the commanded cruise at **60 m/s²** (`SetPace` sets a target, `Fly` moves
  toward it), so the pace can never snap the speed the way the old catch-up's raised floor could.

The containment steering (`FlightSteering.Contain`, the same the scripted enemies use —
docs/campaign-scripts.md) and the 120 m/s hard Y clamp are still there behind it, but with the
pace doing the work they are a backstop rather than the mechanism. The hard clamp is also what
makes the escort→duel bound swap (the wingman may escort as low as ground + 60) invisible.

## The duel itself (`DuelPlane`)

One class flies both aircraft. It has **no rigidbody and no colliders** — `DuelPlane.Spawn`
destroys the ones `PlaneFactory` builds — so the background layer touches nothing: the player's
rounds and bombs pass straight through it, it never collides with terrain, and it costs no
physics. Movement is plain transform integration, but the *model* is the player's, not a turret's.

### It flies the player's flight model

`DuelPlane.Spawn` is handed the shared `PlayerConfig` asset, and takes its turn rate
(**120 °/s**), its turn easing (`turnResponsiveness / mass` = 5 / 2.5 = **2.0**), its dive
acceleration, its drag and its speed cap from it. Nothing about the way a background plane rotates
in the Z plane is tuned separately any more — change the asset and the wingman changes with it.
The one number it keeps for itself is `DuelPlane.CruiseSpeed`, **175 m/s** — and even that is only
the *floor* of the pace the duel is flown at ("Holding station is throttle", above): the commanded
cruise follows the camera so the fight keeps up with a boosting player and still has energy to
manoeuvre.

It is handed the **asset**, not the per-plane loadout the player flies (docs/flight-model.md).
The wingman is another pilot in another aeroplane; the garage stats belong to the plane the
player selected, so they stop at the player's cockpit. In practice that means picking the Dr.I
does not make the wingman turn harder — which is also what keeps the formation logic above,
tuned against these numbers, valid whatever the player picks.

So, exactly as in `CubeController.FixedUpdate`, a commanded turn rate is not applied — it is a
*target* the current rate approaches at `1 − e^(−2.0·dt)`, and speed follows the nose (diving adds
along the flight path, drag bleeds the excess back toward cruise). A plane that pulls up out of a
pass slows down and one that noses over accelerates. The lead-intercept aim reads this real speed,
not the cruise figure.

#### The pilot rolls out of its turns

What is *not* shared with the player is the thing holding the stick, and this is where the old
version went wrong. The player's commanded rate comes from a key: it is full deflection or nothing,
and a human lets go before the nose arrives. Copying that literally — `error / dt`, clamped —
saturates on any error over about 1.5° at 60 fps, so the plane turned at its maximum rate right up
to the target heading, arrived with a turn rate it then had to shed over 0.5 s, overshot, and did
the same thing back. The result was a permanent limit cycle: a background plane that visibly
wagged its nose instead of flying.

The fix is one term. The turn rate already in the airframe is worth `ω / 2.0` radians of extra
heading before it decays away, so the pilot steers on the error it will *still* have after
rolling out:

```
desiredRate = clamp((error − ω / turnResponse) × 5, ±maxRate)
```

Far from the target the bracket is large and it commands the full 180 °/s, exactly like a held key.
As the nose comes round, the rate it is already carrying eats the remaining error, the command
falls to zero on its own and the turn is flown out smoothly. There is no overshoot to correct and
therefore nothing to oscillate; the same figure is what stopped the background duel jerking between
its role headings.

#### In formation the nose is not steered at all

The roll-out pilot is right for the duel, where `Fly` integrates the position *from* the heading so
the nose is the flight path by construction. `HoldStation` is the other way round — the position is
driven by the station and the heading is read back off it — and steering toward that read-back
heading was wrong twice over. The pilot deliberately lags (that is what stops it oscillating), so
while the player rolled into a turn the wingman's fuselage stayed pointing where it had been while
the aircraft slid bodily around the corner: a plane crabbing sideways through the air, moving like
something being dragged rather than something flying.

So in `Escort` the nose is not commanded, it is **tracked**: `TrackHeading` sets the heading to the
direction the plane actually moved this frame, rate-limited to the same 180 °/s the airframe can
turn at. The player cannot out-turn that (the boost's 1.3× is refused during a cutscene), so in
practice the crab is exactly zero — the pair bank into a turn together and the wingman's fuselage
is always along its own path. The turn rate that drives the bank is recovered from the tracked
heading through an 8 /s filter, so the roll still comes from a real rate rather than a per-frame
difference, and a large correction (the tail of a rejoin) is still flown round at a plane's turn
rate instead of snapping.

Banking is rolled about the nose axis (`Euler(0,0,heading) * Euler(roll,0,0)`) from the depth
change and the current turn rate, eased at 4.5 — slower than the turn, so the roll trails the
manoeuvre rather than snapping with it. This is the only thing selling the third dimension in a
game that is otherwise flat, and the 250 m peel slide saturates the ±80° limit into a full
wing-over. The per-turn-rate factor is **0.22** against the old 0.42, chosen so that a full-rate
turn still banks the same ~40° it did when the rate cap was 95 °/s: raising the cap to the player's
180 was meant to change how the plane rotates in Z, not how far it rolls.

The bank shares its axis with the auto-righting half roll every plane now carries (`PlaneRoll`,
docs/flight-model.md), so the two add: `_bank` is the lean, `_roll.Angle` the 0°/180° flip that
keeps a duellist wheels-down whichever way it is flying. The bank is scaled by `cos(flip)` so it
still leans into the turn once the plane has rolled over. Both duellists qualify for the flip the
same way an enemy fighter does — turn rate below 15 % of the cap for a second — which in the duel
means the long straight repositioning legs right the plane while the hunting turns leave it alone.

### Roles, not AI

A dogfight where both planes hunt each other reads as two planes circling forever. Instead the
two are given **opposite roles**, swapped on a timer by `CompanionFlight`:

| Role | Behaviour |
| --- | --- |
| `Hunt` | Runs the pass cycle below. The only role that fires. |
| `Break` | The defensive turn: **across** the attacker rather than straight away from it — the away-heading rolled 55° to one side, with a 22° jink at 0.5 Hz on top. The side flips every ~2.4 s, so the quarry weaves in wide S-turns instead of running in a straight line. The flip is *blended*, at 1.6 per second: a hard swap of the sign asks for a 110° heading change in one frame, and even a pilot that rolls out cleanly answers that with a whip. Ramping it over ~1.2 s is the S-turn it was supposed to be. |
| `Cross` | Steers at a fixed point in the world, taken once when the role starts: 280 m along the bearing to the opponent, offset 110 m to one side. The two sides are given opposite signs, so the pair merge and pass instead of colliding. Inside 90 m of that point it stops steering and flies the merge straight through — a live target recomputed every frame swings through 180° as the two planes pass each other, which put a hard flick into the one moment of the duel the eye is actually following. Never fires. |

One plane hunts for **6–9 s**, then both go to `Cross` for **1.6 s**, then the roles swap and the
other one is on the offensive. The hand-over always happens through a head-on pass, which is the
readable moment.

`Escort`, `Peel`, `Level` and `Form` (the last two are the return, above) are four more roles on
the same class; `Idle` (fly level, guns cold, no bounds) is what `StandDown` leaves them in.

### The pass cycle — why they are not glued to each other's tail

Two planes of equal speed, one chasing and one fleeing straight, hold their separation forever:
that is a tail chase, and it is what a naive `Hunt` produces. Real fighters do not get a tail and
keep it; they make **passes**. `Hunt` is a three-state cycle inside the role
(`DuelPlane.TickEngagement`):

| State | Until | Flies |
| --- | --- | --- |
| `Approach` | range ≤ **190 m** | Lead pursuit on the intercept point, firing whenever the shot is on. |
| `Overshoot` | 1.1 s | A heading frozen at the moment of break-off, 26° off to a random side. It flies *past* — no tracking at all, which is what opens the range again. |
| `Reposition` | range ≥ **380 m**, or 3 s | A wide arc back: the bearing to the opponent, offset 40° to the overshoot's side. Curving back rather than fleeing and re-turning means no 180° about-face is ever spent. |

So the pair oscillate between roughly **190 m and 380 m** apart — at the duel's depth that is 14–28 %
of the screen width, always visibly two aeroplanes rather than one shape. The aim gate does the
rest: 40° off the bearing during `Reposition` is far outside the 12° firing cone, so the guns go
quiet between passes on their own.

Backing that up, `Separate` is a hard floor the roles cannot argue with: inside **130 m** a push
directly away from the opponent is blended into whatever heading the role asked for, weighted by
how deep inside that radius they are. The two models cannot pass through each other even on a
badly-timed merge — worth having, since nothing here has a collider to stop it.

### Shots

Fire is **cosmetic only**. `Tracer` is an emissive round with no rigidbody, no collider and no
damage — it flies a straight line for 1.3 s and destroys itself. Nothing in the background
duel can be hit by anything, and nothing it fires can hit the level. Rounds leave in bursts of
4–8 at 0.085 s spacing followed by a 0.9–1.9 s pause, and only when the hunter is inside 470 m
and aimed within 12°; a burst that has started is committed even if the aim drifts, which is
what a real burst does. Each shot draws the same `MuzzleFlash` the player and the enemies use,
and plays the shared shot clip at **0.045** volume against the enemy's 0.15 — audible as a
distant scrap, never as something demanding a response.

### Death and immortality

Neither background plane has health, a health bar, smoke damage states or `IDamageable`. They
cannot be shot down, cannot be bombed, and cannot hit the ground. The **only** thing that kills
the foe is `CompanionFlight.BeginAlign` — the cutscene trigger, the first step of the return —
and the wingman is never killed at all.

`DuelPlane.Kill` is the death: an `Explosion` at the spot, smoke and fire lit, and then a burning
38° dive away, spinning at 230 °/s and gaining 20 m/s².

### The wreck is never deleted on screen

The wreck used to be `Destroy`d on a **3 s timer**, or as soon as it sank 100 m below the duel
floor — both of which happen with the thing still in frame, so a burning aeroplane simply blinked
out of existence mid-dive. There is no timer any more and no floor test. `TickWreck` runs after
the position has been integrated and has exactly two exits:

| Exit | When | What happens |
| --- | --- | --- |
| **It lands** | The wreck is at or below the surface under it | `Explosion` + `GroundBlast` at the impact point, then removal. Over water (coast levels — the duel's Z 350 is past `SeaSurface.NearEdge`) it is a `WaterSplash` and no fireball, matching what a bomb does. |
| **It leaves** | Fully outside the camera frustum at the duel depth, by its own half-length + 60 m | Silent removal — there is nothing left to watch. |

Whichever comes first. In practice a wreck killed high leaves the frame; one killed low flies the
crash all the way in, which is the whole point: the fall reads as a fall.

The surface under it is the real terrain, not a constant. `Battlefield.Surface(x, z)` samples the
streamed terrain at the wreck's own position and resolves sea level for the coast, so the crash
lands on the hillside it is actually over. If no chunk is loaded there it falls back to the
`AiGroundY` the flight was constructed with.

The frustum the second test uses is the *uninset* one from "Staying on screen" —
`CompanionFlight.ApplyView` pushes it to both planes and to the wreck every frame, and it keeps
doing so after `StandDown`, so a wreck falling as the level ends still disappears at the right
moment rather than the moment the bounds are released.

## Bumping into it

While the wingman is at the play depth (within 40 m of Z 100 — the escort, and the first moments
of a peel) the player can fly into it. `CompanionFlight.CheckBump` is a distance test on the same
30 m reach `PlaneScrapes` uses for plane-to-plane contact, on a 0.5 s cooldown, called from
`CampaignLevelController.FixedUpdate` next to the scrape check.

It is deliberately **not** `CubeController.Scrape`, which costs 10 health outside a cutscene.
`CubeController.Bump` is the no-damage variant: the airframe shudder and the `OnScraped`
callback, which the controller already wires to a full camera shake, and nothing else. The
wingman does not react at all. There is no physics behind any of this — the companion has no
collider, so a bump is a distance test and a shake, never a push.

## What it is not

- It is **not** counted by the level script. The companion and its foe live outside
  `CampaignEnemies`, so `EnemiesAlive` never sees them and a `wave`/`waitclear` can never be
  wedged by the background fight.
- It is **not** a target for the scripted enemies either: their AI only knows the player's
  rigidbody, so a wave ignores the wingman even while it is flying formation at the play depth.
- It casts **no real-time shadow**: `ConfigureShadows` sets the URP shadow distance to
  `CameraDistance + 200` (620 m) and the duel layer sits at 670 m. Background aircraft losing
  their shadows is the correct look anyway.
