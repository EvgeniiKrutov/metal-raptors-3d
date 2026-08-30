# Enemy roles: scout and fighter

Implemented in `EnemyController`, split by `EnemyConfig.role`. Two assets carry the
numbers — `Assets/Resources/EnemyScoutConfig.asset` and `EnemyFighterConfig.asset` —
loaded through `EnemyConfigs.Load` by `CampaignEnemies` (campaign and custom battle) and
`LevelController` (the fixed Level scenes). `DuelPlane`, the companion's scripted foe, is a
separate AI and is not affected by any of this.

## The two fights

The roles exist to make the same vertical axis carry two different fights.

* **Scout** — lives low, in a corridor that rides the terrain contour at a safe height. You have
  to come down to fight it, which puts the terrain, not the enemy, in charge of *your* margins —
  the scout keeps its own. It turns one way faster than the other, and it can roll away into the
  background, out of your plane of fire, the moment you line it up.
* **Fighter** — lives high and converts altitude into speed, diving through you and zooming
  back out of reach. It only ever meets you on the way through. Its reversal is a wide loop:
  expensive in time and free in energy.

A player who parks on the deck is safe from the fighter's dive (it bottoms out well above the
terrain) but swarmed by scouts; a player who parks high is safe from the scouts until they
climb, and is the fighter's preferred meal.

## Which plane flies which role

`PlaneModelConfig.enemyRole`, so campaign scripts keep naming planes and nothing else:

| Model | Role |
| --- | --- |
| `albatros` — Albatros D.III | Fighter |
| `fokker` — Fokker Dr.I | Scout |
| `sopwith` — Sopwith Camel | Fighter (the default; never spawned as an enemy today) |

The Dr.I is a stand-in. The scout is written for a Fokker Eindecker; when that model lands it
takes the role and the Dr.I goes back to being a fighter.

## Altitude bands

`AltitudeBands` splits the vertical space — `groundY` (the terrain's *maximum* height, 85 m on
land, `SeaSurface.Level` 22 m on the Flanders coast) up to the enemy's ceiling — into three:

| Band | Fraction | Verdun (85 → 620) |
| --- | --- | --- |
| Deck | 0 – 15% | 85 – 165 |
| Mid | 15 – 55% | 165 – 379 |
| High | 55 – 100% | 379 – 620 |

Each role is *held* inside its home band by `EnemyController.Contain`, which feeds the band's
floor and roof to the same `FlightSteering.Contain` the player's world bounds use. The roof push
starts 40% of the corridor height below the roof, capped at 80 m; the scout's **floor** push is
not scaled that way — it is fixed at `safeAltitudeMargin − minAltitudeMargin` (70 m), because over
high ground the corridor is only ~74 m tall and a proportional margin would collapse to 30 m,
less than one step of travel at chase speed.

A role leaves its band only during a declared manoeuvre. The fighter's practical roof is
`ceilingY − 130`, so it flies 379 – 490.

**The scout's band is terrain-relative, not a fraction.** The fractions above are fixed slices of
the world, and the deck slice is only 80 m tall — less than the scout's own turn *radius*
(160 m/s ÷ 88 °/s ≈ 104 m), so a scout held inside it could not complete a turn without flying
into the ground. Instead the scout's corridor rides the contour under it: floor at
`contour + safeAltitudeMargin` (220 m), roof at `contour + deckCeilingMargin` (380 m) but never
above the mid/high boundary, so it stays out of the fighter's airspace. On Verdun that is 240 – 379
over a valley and 305 – 379 over a hilltop: the floor rises and falls with the terrain while the
roof holds the line against the fighter. Corridor height is no longer what keeps the scout safe —
the turn-direction check below is — so the squeeze over high ground is not a problem the way it
was when the band was a fixed 80 m slice.

Spawning matches: `EnemyConfigs.SpawnBand` gives each spawner the role's altitude range —
`groundY + 220 …` the mid/high boundary for scouts, the high band for fighters — so a wave arrives
already in position.

## Skills wait for the first appearance

A wave spawns off the right edge of the frame (`CampaignEnemies.SpawnPoint`: `SpawnAhead` 110 m
past the edge, plus 90 m of stagger per plane), so every enemy flies for a second or two before
the player can see it. **Nothing role-specific may run in that window.** `_appeared` latches the
first physics step on which `IsOnCamera` is true, and until then `CanDodge`, `WantsDive` and
`WantsReversal` all refuse:

| Refused before the first appearance | Why it could fire off screen |
| --- | --- |
| the scout's depth dodge | `TickDodge` runs before the state machine's camera branch, and `UnderAim`'s cone reaches 600 m — a scout just past the edge is inside it |
| the fighter's diving pass | it could otherwise commit on the frame it appears |
| the fighter's loop reversal | same |

Only *starting* a manoeuvre is gated. The cooldowns keep counting, so an enemy that has been in
the air a while is not also made to wait once it does appear — it just cannot spend the wait
performing. The ordinary flying, the guns and the containment are all untouched; this is about
the declared moves, which exist to be *read*, and a move the player never saw is a move that
only ever felt like a cheat.

## Scout

160 m/s cruising and 256 flat out, 88 °/s to the left and 57 °/s to the right, 137.5 health,
6 damage a round. **Both of those numbers sit under the whole garage**: 256 is below the Dr.I's
264, the slowest plane the player can buy, and 88 is below the Albatros's 104 °/s, the widest
turner of the three. Whatever the player flies, they can out-run a scout and out-turn a scout —
the fight is winnable by flying, not only by shooting, and every escape and every break the AI
below offers is one the airframe can actually take. It is the pressure role, so its gun discipline is
loose and its runs are long — where the fighter arrives, hurts and leaves, the scout is simply
always shooting at you:

| | Scout | Fighter |
| --- | --- | --- |
| `fireRate` | 0.18 s | 0.20 s |
| `fireAngleThreshold` | 22° | 14° |
| `maxFireRange` | 560 | 500 |
| `attackDuration` / `flyDuration` | 5.5 s / 0.7 s | 3.5 s / 1.6 s |
| `evadeDuration` / `evadeCooldown` | 0.85 s / 6.5 s | 1.3 s / 3.0 s |
| `threatRange` / `threatTailAngle` | 340 m / 115° | 420 m / 95° |
| `engageRange` | 380 m | 450 m |

Longer runs, shorter break-aways, shorter and rarer evades: the scout spends most of an engagement
pointed at you, and its 6 damage a round is what keeps that survivable.

### How the pressure was raised

The scout was the pressure role on paper and a fairly polite one in the air: it broke off every
4.5 s, evaded off almost any round that landed, and took a 5.5 s depth dodge on top of that. The
tuning below leans on **how much of the fight it spends pointed at you**, and deliberately not on
its gun — `fireRate`, `maxFireRange`, `bulletSpeed` and the 6 damage a round are all
untouched, so a second spent under its guns costs exactly what it did before. There are simply more
of them.

- **Longer runs, shorter breaks.** `attackDuration` 4.5 → **5.5** s, `flyDuration` 1.0 → **0.7** s.
  The attack/break cycle goes from 82 % attacking to 89 %, and at 0.7 s the break-away is barely a
  repositioning swing rather than a lull you can use.
- **It flinches less.** `evadeCooldown` 4.5 → **6.5** s and `evadeDuration` 1.0 → **0.85** s, so a
  hit is less likely to buy a break turn and the break is shorter when it does. `threatRange`
  420 → **340** m and `threatTailAngle` 95 → **115**° narrow `UnderThreat` to a gun genuinely on
  its six at close range, instead of anyone pointing roughly its way from half the screen out.
  The same narrowing lets it hold a tail run-down through fire it used to break off from.
- **It commits to the chase sooner.** `engageRange` 450 → **380** m, so the catch-up speed arms
  while the player is still on screen rather than after they have already opened the range. The
  256 cap is unchanged, so a player at full throttle still gets away — it just starts trying
  earlier.
- **It gives up on you slower.** `pressDelay` 3 → **2** s: park above its corridor and it starts
  climbing after two seconds, not three.
- **Its one defensive move costs it less gun time.** `dodgeHold` 2.5 → **1.6** s shortens the
  depth dodge from 5.5 s to 4.6 s. The move is kept — it is the scout's signature — but the
  stretch where it is neither hittable nor shooting is 16 % shorter, and `dodgeCooldown` still
  holds it to one per ~19 s.

`health` 125 → **137.5** (+10 %) pays for the extra exposure: a scout that spends 89 % of its cycle
nose-on is a scout in your own gunsight far more often, and the old number would have made the more
aggressive plane the shorter-lived one. Per-level `enemyHealthScale` is a multiplier on this base,
so every campaign level inherits the +10 %.

Its aim point is clamped into its corridor at the floor but allowed `AimReach` (150 m) *above* the
roof, so a player flying higher still draws the nose up into a climbing shot. Containment holds
the altitude; only the aim elevates.

### Terrain following, at a safe height

The scout is the only enemy that samples the ground under itself: a downward raycast on
`ProceduralTerrain.GroundLayer` each physics step (`TickDeck`), the same probe the player uses in
`CubeController.GroundUnder`. Everything about its altitude is then measured against that contour
rather than against the flat conservative floor:

| | Metres above the contour |
| --- | --- |
| `minAltitudeMargin` — abort everything and climb at 70° | 150 |
| `safeAltitudeMargin` — corridor floor, and where the climb ends | 220 |
| `deckCeilingMargin` — corridor roof, capped at the mid/high boundary | 380 |

The scout is a low-flying enemy, not a suicidal one. Four things keep it off the terrain: the
soft push of `Contain` as it nears the corridor floor, the turn-direction choice below, the hard
`Recover` climb below `minAltitudeMargin`, and `KeepNoseUp` as a last-ditch net. Its aim point is
clamped at the corridor floor by `ClampToBand` on top of that, so it never *steers* at a player
flying below it.

`KeepNoseUp` is the only heading clamp, and it is deliberately placed where it should never fire:
**below the corridor floor**, where the scout has no business being at all, it refuses a target
heading with a downward component and levels it off. An earlier version clamped inside the
corridor, against `noseDownMargin`, and that read as clunky — the scout flew visibly pinned to a
horizontal. Against the corridor floor it is invisible in normal play and only shows up when
something else has already gone wrong. Everything else the scout does still goes through the same
eased steering as every other plane; what the turn choice changes is not *how far* it may turn but
*which way*.

### Choosing which way to turn (`ChooseTurn`)

A turn in a side-scroller costs altitude: half of it is spent pointing downward, and at 160 m/s the
scout's turn radius is 104 m one way and 160 m the other, so a reversal taken the wrong way round
drops it 208 – 320 m — straight through the corridor floor and into the ground.

So before committing to a turn of more than 30°, the scout works out whether it can survive it.
`TurnClear` runs a short forward simulation of the arc — 2 s at 0.15 s steps, using the *actual*
asymmetric turn rate at each simulated heading, so the wide slow right turn is modelled as wide and
slow — and probes the terrain under every sample. The arc is flown at the speed the plane is
*actually* making (`FlightSpeed` — cruise, the engagement boost, or the `Return` catch-up,
whichever is highest), not at the configured `flySpeed`, and at the matching boosted turn rate
(`TurnBoost`, below), so the arc it probes is the one the plane will really fly. A scout chasing a
boosting Camel is doing 256 m/s, not 160; before the turn rate was boosted with it, that arc was
nearly twice as wide and sank nearly twice as deep as the cruise figure — which is precisely how a
scout that had just clawed its way back into frame flew itself into the ground.
The simulated turn rate also ramps in on the same
`turnResponsiveness / mass` curve the real steering uses, so the ~0.3 s of near-straight flight at
the start of a turn — where the plane is deepest — is part of the model. Then:

1. Take the **shortest** way round if the simulation clears `safeAltitudeMargin` the whole way.
2. Otherwise take the **long way**, which sweeps up and over instead of down and under.
3. If neither clears, **climb first**: the target heading becomes 55° up in the current direction
   of travel until it has `TurnClimbGain` (60 m) more than the corridor floor, then it re-picks.

A chosen direction is sticky. Once the long way is committed, `SteerToHeading` adds a full turn to
the heading error so the plane keeps going that way instead of `Mathf.DeltaAngle` quietly snapping
it back to the shortcut, and the re-check every `TurnCheckInterval` (0.35 s) only asks whether the
*committed* direction is still safe — it will not switch back opportunistically and reverse the
turn mid-sweep. The commitment clears when the error falls under 30°.

The simulation probes against the **corridor floor**, not the emergency line: an arc has to stay
inside the airspace the scout is supposed to occupy, not merely miss the ground. Probing against
`minAltitudeMargin` made "this turn is clear" and "abort everything and climb" the same condition,
which left no margin at all. It also ignores the containment push that would in practice be
lifting the nose, so it errs toward calling an arc unsafe. That is the right bias: the cost of a
false alarm is a longer way round, and the cost of a miss is a crater.

**None of this touches the guns.** The choice only changes which way the nose travels, never
whether the scout may fire, and `UpdateFiring` is independent of it — so the long way round is
also a long sweep of the nose across the sky, and the snap-fire window (`SnapFireConeDeg`, 26°)
catches the player as it passes. The scout shoots while climbing out of the both-blocked case
too.

If the raycast misses (unstreamed chunk, open water on the coast) it falls back to the flat
`groundY`, which is the conservative answer.

### Off camera

An enemy that falls off the camera enters `Return` and flies back at `1.35 ×` cruise — unless it is
recovering from the ground or running the fighter's diving pass, both of which are declared
manoeuvres and are left alone to finish. `Return` used
to switch off everything role-specific: it aimed at the player's raw position rather than through
`ClampToBand`, and it skipped `ChooseTurn` entirely. It also **preempted `Recover`** — ground
avoidance releases at `minAltitudeMargin` while `Recover` only exits at `safeAltitudeMargin`, so on
camera there is 70 m of hysteresis, but off camera the scout was thrown straight back into `Return`
the instant it crossed the lower line, nose back down at a player below it. The result was a
sawtooth on the emergency line that the steering lag turned into a crater, always out of sight
behind the camera.

That was harmless while every enemy measured its floor against the flat `groundY` (85 m, the
terrain *maximum*) — a fighter sawtoothing at 235 m absolute is nowhere near ground that tops out
at 85. Putting the scout's corridor on the contour moved the emergency line down onto the terrain
and made it lethal.

Now `Return` is entered only when the scout is not already recovering, so the climb to
`safeAltitudeMargin` finishes wherever it happens; `Return` aims through `ClampToBand` like
`Attack` does; and `ChooseTurn` stays live in it. Only `Recover` skips the turn choice, because it
is already a declared climb. Firing is unchanged — `UpdateFiring` is still gated on `Return` alone,
so a scout climbing out off camera shoots exactly as it did before.

### Asymmetric turn — slow to the right

Rotary-engine torque, as a rule rather than as flavour. **Swinging the nose to the right is the
weak side**, at `turnBias` (0.65) of the full `rotationSpeed`; swinging it to the left runs at the
full 88 °/s. Both sides are slower than every plane in the garage — the widest-turning of those is
the Albatros at 104 °/s — so a player who turns always wins the turn; which way the scout breaks
only decides by how much. `TurnLimitAt` decides which by asking whether the turn is increasing the nose's
x-component (`-sin(heading) × sign(turnRate)`), so it is the *direction the nose is heading
toward* that is penalised, not a fixed rotational sign — and the penalty scales with that same
term, biting hardest through the vertical and vanishing in level flight, where no turn is either
left or right yet.

**Pulling up out of a dive is exempt.** That term is symmetric about the horizon, so it applied
just as hard to a nose-down scout hauling its nose back to level as to one pushing it further
down — and since the penalty scales with pitch, it bit hardest exactly where the plane was
steepest and had least room. The result was a ratchet: the scout could enter a dive at the full
88 °/s and could only come out of it at 61, which is how it flew itself into the ground out of the
evade repertoire. `TurnLimitAt` now returns the full rate whenever the nose is below the horizon
and the turn is raising it (`sin(heading) < 0 && cos(heading) × sign(turnRate) >= 0`), the `>= 0`
so that the exemption still holds at exactly vertical, where the raising term is instantaneously
zero. Nothing else moves: level flight was never penalised, the slow sweep over the top is
untouched, and pushing the nose *down* from a climb still pays the full bias.

Since a wave attacks from the right flying left, the turn a scout most needs is the one back to
the right to chase a player who has run past it. That is the slow one. It is the scout's
exploitable weak spot, and the engagement boost below is what stops it from being a free escape —
as far as its 256 cap allows, which against a player at full throttle is not far.

This asymmetry *is* the scout's reversal cost — there is no separate manoeuvre for it. A scout
turning around simply takes longer one way than the other, through ordinary steering. It also
feeds the terrain check below: the slow side's turn radius is half again as wide (160 m against
104 m), so it is the side more likely to be refused when the ground is close.

### Depth dodge

The scout's one special manoeuvre, and its "reloading" ability (`EnemyDepthDodge`): it breaks 120 m
*away from the camera*, out of the plane your bullets travel in, flies there for a couple of
seconds, then comes back. The flight path in X/Y is untouched — it moves in Z only, which is why it
survived the manoeuvres that drove the heading, and its AI carries on flying, turning and steering
normally the whole time. At a camera distance of 420 m it sits at about three quarters of its size
while it is out there: far enough to read as a different plane of flight, close enough to stay part
of the same fight.

**It breaks on your aim, not on your hits.** `CanDodge` is checked every physics step and needs
four things at once: it has been on camera at least once (above), `dodgeCooldown` expired,
health at or below `dodgeHealthFraction`, and `UnderAim` — the player holding the trigger with
the scout inside the fire cone. That cone is an
*area*, not the bullet line: `dodgeAimCone` (22° either side of the player's nose) out to
`dodgeAimRange` (600 m), so it widens with distance the way a torch beam does. `PlaneShooter`
publishes `Firing`, a 0.25 s latch on the trigger being held, which is what makes a single tap
register across a physics step.

`dodgeHealthFraction` is **1**: the scout may break at full health. The gate used to be 50%, and
between that and the trigger sitting in `ApplyDamage` — a hit had to land while the scout was
*already* under half — the manoeuvre almost never ran. At level 1's 62-health scaling the round
that crosses a 50% gate is usually the second-to-last one a scout ever takes. A scout has too
little health for its signature move to be gated behind losing most of it, so the health condition
is kept only as a knob (drop it below 1 to make the break a wounded-animal tell again).

**The change of depth is flown, not slid.** Each leg is three consecutive movements — roll onto the
wing, *then* change depth, *then* roll level and fly straight — and the return runs the same three
in the same order, banked the other way. Nothing overlaps: while it is rolling the depth is fixed,
and while the depth is changing the bank is fixed. `EnemyDepthDodge.Step` is a seven-phase run:

| # | Phase | Z | Bank | Seconds |
| --- | --- | --- | --- | --- |
| 1 | roll onto the wing | held in lane | 0 → 75 | `dodgeRoll` 0.35 |
| 2 | slide out | lane → +120 | 75 | `dodgeOut` 0.8 |
| 3 | roll level | held at +120 | 75 → 0 | `dodgeRoll` 0.35 |
| 4 | fly straight | +120 | 0 | `dodgeHold` 1.6 |
| 5 | roll onto the other wing | held at +120 | 0 → −75 | `dodgeRoll` 0.35 |
| 6 | slide back | +120 → lane | −75 | `dodgeBack` 0.8 |
| 7 | roll level | held in lane | −75 → 0 | `dodgeRoll` 0.35 |

4.6 s in all, then `dodgeCooldown` (14 s) counted from the moment it returns. Every phase is eased
with the same `SmoothStep`, so each movement starts and ends at rest and the joins between them are
seamless — the plane never snaps from one to the next. `ApplyRotation` adds `Bank` to
`PlaneRoll.Angle`, the same local-X roll the companion banks on; negate `dodgeBank` in the asset to
roll it the other way.

Mechanically the rigidbody's `FreezePositionZ` is cleared for the duration and the Z is driven to
the dodge's curve; it is restored and the plane snapped back to its lane when the dodge ends — a
snap that lands on the lane it is already sitting in, because phase 7 ends there.

Once displaced more than `EnemyDepthDodge.ClearDepth` (35 m, half a plane) it is `OffPlane`:
`TakeDamage` and `Scrape` both refuse, so bombs and mid-air collisions miss it as cleanly as
bullets do. It does not fire while dodging — its own rounds are Z-frozen at its own depth and
could not reach you anyway.

**Cutting it short still flies it home (`Release`).** Something else in the AI can want the dodge
over before phase 7 — the run-down lock is the one that does. `Cancel` is the hard version: it
drops `Z` and `Bank` to zero on the spot, and `EnemyController.ReturnToPlane` teleports the
rigidbody with them. From the far lane that is 120 m of depth and 75° of bank gone in a single
step, which reads as the plane snapping *toward the camera* and jumping a size — the same
manoeuvre, ruined at the end. `Release` replaces it with the last three phases of the ordinary
return, re-timed from wherever the plane actually is: roll onto the other wing from its current
bank, slide back over `dodgeBack × (remaining depth ÷ dodgeDepth)`, roll level. Cut short at the
far lane it is the full 1.5 s; cut short halfway out it is proportionally shorter; cut short
before it has left the lane at all it is only the wings coming level. `Active` stays true
throughout, so the Z is still driven by velocity and the dodge finishes through the normal
`TickDodge` path — cooldown set, constraint restored, and the snap that restores it is a no-op
because the plane flew itself back to the lane first. The hard `CancelDodge` is now reached only from
`BeginFall`, where the plane is already dead and — since `TakeDamage` refuses while `OffPlane` —
inside `ClearDepth` of the lane anyway.

**The dodge and the ordinary evade do not stack.** `TakeDamage` skips `EnterEvade` while a dodge is
running, so a round that lands in the first phases — before `ClearDepth` (35 m) makes it
untouchable, about 0.65 s in — does not also kick off a break turn. The ordinary evade is otherwise unchanged: same
`threatRange` trigger, same `evadeDuration` and `evadeCooldown` — what it *flies* now comes from
the repertoire below.

The cost is symmetrical, which is what keeps it fair: the 4.6 s manoeuvre is 4.6 s in which the
scout cannot be hit *and* cannot shoot, on a ~19 s cycle. You lose the kill you had lined up; it
loses a quarter of its gun time.

### Climbing when ignored

A scout that cannot reach the player — out of `maxFireRange`, or the player more than 80 m above
its corridor roof — counts the seconds. After `pressDelay` (2 s) its roof is raised to the top of
the **mid** band (379 m) for `pressDuration` (10 s); its floor stays on the contour, so it climbs
to meet the player rather than teleporting up a band. It drops back early once it closes to 60% of
its firing range. So parking high buys you a lull, not immunity.

## Fighter

160 m/s, 75 °/s, 130 health, a shot every 0.20 s, 6 damage. It used to cruise at the player's
own 180 and turn at 105 °/s; it is now the widest-turning thing in the air, with a **122 m turn
radius** (`v / ω` — 160 ÷ 1.31 rad/s) against the scout's 104 m one way and 160 m the other, and
the player's 86 m at cruise. It cannot follow you round a corner and it is not supposed to try:
a level runaway is answered by the engagement boost below, and a turning fight by the loop
reversal or a dive, never by matching your arc. Dropping the cruise under the player's 180 also
means a Camel flying level away from it is genuinely leaving, which is what pushes the fighter
onto the vertical instead of onto your tail.

### Dive energy

The fighter runs the dive-energy model — gravity along the flight path (`diveAcceleration` 90),
drag on the excess (`speedDrag` 0.9), a floor at cruise and a cap at `maxSpeedMultiplier` (1.6),
so 160 to 256 m/s — from its own `EnemyFighterConfig.asset`. The cap still clears the player's
boosted 234, so the one thing a slower fighter has not lost is the ability to run you down on
the way through. Altitude really is energy for it; the
diving pass below is that model being spent, not a scripted speed multiplier. The scout keeps a
flat constant speed.

The **player no longer flies this model** (docs/flight-model.md): a constant speed is a constant
turn radius, and the human is the one who has to predict their own arc. So the fighter's dive is
now something only it can do, which is the point of the role — it is the enemy that owns the
vertical, and the player answers it with position rather than by out-diving it.

### The diving pass — the fighter's skill

The whole manoeuvre is one declared skill with a visual tell: **two white wingtip streaks**
(`WingStreaks`, the same component and the same white as the player's boost — docs/boost.md)
light up the moment it commits and stay lit until the pass is over. They are the only warning
you get, and they are on for the climb as well as the dive, so the tell arrives before the
danger does.

**It only runs against a low player.** `WantsDive` asks whether the *player* is within
`diveTriggerHeight` (180 m) of the ground — 265 m on Verdun — not whether the fighter already
has a height advantage, because the climb is part of the skill. Stay up in the high fight and
the fighter never runs it: you get the ordinary attack / break-away / evade cycle and nothing
else. Come down and you are on its board. The other gates: the range is inside
`maxFireRange × 1.6` (800 m), `diveCooldown` (10 s) has expired, and the fighter has been on
camera at least once since it spawned (above).

| # | Phase | Flies to | Ends when |
| --- | --- | --- | --- |
| 1 | `DiveClimb`, ≤ `diveClimbSeconds` 3 s | the top corner **away** from the player | it is within `DiveCornerReach` (90 m) of that corner |
| 2 | *wingover* | — | the 180° loop finishes (1.5 s) |
| 3 | `DiveRun`, ≤ `diveRunSeconds` 6 s | **the player**, then on to the opposite bottom corner, firing all the way | it reaches that corner |
| 4 | `DiveZoom`, ≤ 3 s | back up at 70° | it is in the high band again |

Then `EndDive` charges the 10 s cooldown and puts the streaks out.

**The corners are the screen's, not the world's.** `CameraBounds` reads the view rectangle at
the plane's own depth (`ViewportToWorldPoint`), and both corners are pulled in from it by
`DiveCornerInset` (70 m):

- **x** — the camera's left or right edge, clamped to the enemy window `CampaignEnemies`
  already maintains, so the pass spans the picture the player is actually looking at, at any
  aspect ratio, and can never be set up off screen. Containment uses the same bounds while
  diving (`DiveSideMargin` 40 m instead of the usual 90 m edge margin), so the fighter is not
  fighting its own corridor on the way to a corner it was told to reach.
- **top y** — the top of the view, capped by `ceilingY − ManoeuvreTopMargin` (580 m on Verdun).
  Containment during the climb still uses that world roof rather than the screen, so a fighter
  that is *already* above the top of the frame — which is normal, its band is 379 – 490 and the
  camera sits low when the player is low — is not shoved back down; it simply slides out to the
  corner and starts the run from there.
- **bottom y** — `minAltitudeMargin + ManoeuvreFloorLift` (160 + 40 = 200 m above the ground, 285 m
  on Verdun): a bit above the line ground avoidance defends, which is the whole reason that line
  is there. Note this is the *emergency* margin and not `safeAltitudeMargin` (260 → 345 m),
  which sits above the entire low fight and would have left the pass flying over the player it
  is aimed at.

The corner line is the pass's *shape* — the top corner it starts from and the bottom corner it
leaves through are both the camera's business rather than numbers in the asset, and the length
is always the full width of the frame. What the fighter actually flies between them is aimed at
the player; see below.

**The wingover is the loop reversal, borrowed.** Turning ~180° at the top with ordinary steering
would take 2.4 s at 75 °/s and drift half the screen; `EnterDiveRun` clears `_reversalCooldown`
and `WantsReversal` is allowed in `DiveRun`, so the fighter flicks over the top on `EnemyLoop`'s
1.5 s constant-rate arc instead — at the dive's own speed that is a ~110 m radius, so it may
clip a couple of hundred metres above the top of the frame for under a second before the nose
comes down. It holds its fire through the loop — a plane spraying a full
circle at the top of the screen reads as noise, not as a threat — and opens up again as the nose
comes down.

**The run is aimed at you, not at the corner.** `DiveRunAim` returns the player's own position
while the fighter is still short of them in the run direction (`PastTargetX`), clamped so the
nose never comes up and never goes below the bottom line; only once it has passed them does the
aim become the far bottom corner it exits through. Because the player is much closer than that
corner, the nose goes *down*, hard — 20 – 40° instead of the corner line's 12 — and the pass
reads as a dive at you that carries on across the screen, rather than a line that happens to
sweep past. Two things sharpen it further while `Diving` is up: the steering limit is
`DiveTurnFactor` (1.7 ×) of the fighter's ordinary 75 °/s, so the nose actually snaps onto the
new aim instead of easing onto it, and the speed floor is `diveSpeedMultiplier` (1.5 ×
`flySpeed` = 240 m/s, capped by `maxSpeedMultiplier` at 256) for the whole manoeuvre — climb
included, so even the set-up is quick. The 108 – 115 m turn radius that comes out of those two is
*tighter* than its 122 m cruise radius despite the extra speed. When the pass ends the floor
drops back to cruise and `speedDrag` bleeds the energy off, so it does not carry the dive speed
into the next turning fight.

**It fires down the whole run**, not at a firing solution: `UpdateFiring` skips the range check,
the aim cone and the intercept while `DiveRun` is up, and shoots at `fireRate` along its nose,
which is by then pointed at you. It does not re-aim for the guns — the nose is set by the flight
path — so the danger is still the *line* it is flying, and getting out of that line is the
answer. The one gate left is that the fighter itself must be on camera.

**A declared manoeuvre now finishes.** An enemy that leaves the camera is normally thrown into
`Return`; `Diving` is exempt from that, the way `Recover` already was. That exemption is what
makes the skill possible at all. The old pass climbed at a fixed 62° toward `ceilingY − 40`
(580 m) with no reference to the camera, and 580 m is above the top of the screen whenever the
player is low enough to trigger a dive — so every pass was thrown into `Return` a fraction of a
second into its climb, and every abort charged the full 10 s cooldown. The dive triggered
constantly and never once ran. Now the corners keep it in frame by construction, and the brief
excursions that remain (the wingover, a fighter starting the run from above the top edge) are
left alone to finish.

### Loop reversal

The fighter's reversal is a declared manoeuvre too (`EnemyLoop`): a constant-rate 180° through
the vertical over `loopSeconds` (1.5 s), at unchanged speed. It triggers on a
heading change of `reversalAngle` (120°) or more, with `ReversalCooldown` (2.5 s) stopping it from
chaining; the scout has no equivalent. At 75 °/s the loop is now the *only* quick way it has of
turning around — 120°/s through the loop against 75 °/s of ordinary steering, a 76 m radius
against 122 — so the manoeuvre is no longer an alternative to a hard turn, it is the hard turn.
That is also why the diving pass borrows it for the wingover at the top. It
is hard to punish — it keeps all its energy — but it takes a while and it is legible, so it is a
beat you can reposition into rather than a corner you get turned in.

## Taking your six (both roles)

`Attack` is a lead pursuit onto an *intercept* point — it aims where you will be, which makes it
a slashing head-on attacker and means a player flying straight and level is repeatedly passed
rather than hunted. `AiState.Tail` is the answer: once an enemy is already behind you and you
are not threatening it, it stops intercepting and settles into your six.

`Tail` is the punishment for disengaging. A player who turns and fights is answered by the
slashing attack, the loop reversal and the diving pass; a player who simply flies away gets
something on their six that closes the range, comes to their altitude and shoots.

**Getting there.** `WantsTail` needs the enemy `_appeared`, not diving, not standing down,
currently in `Attack` or `Fly`, inside `maxFireRange × 1.4`, not `UnderThreat`,
and — the real test — `TailOffAngle() <= 75°`. That angle is between the **reverse** of your
velocity and the vector from you to the enemy, so it is literally "how far off my six are
they": 0° is directly astern, 180° is directly ahead. An enemy that is already behind you
latches on; one merging head-on does not, and keeps its slashing attack.

The reverse matters. Measured against your velocity instead, the whole state is inverted: an
enemy sitting perfectly in the slot reads 180°, so `WantsTail` never fires for one that is
actually behind you, and `TickTail`'s 105° break trigger fires the moment it gets there. The
tell that this is wrong is that it contradicts `TailSlot`, which subtracts your heading and so
puts the slot **astern**: the two cannot both be right.

**Both roles fly it identically.** The scout is not excluded, and it is not a watered-down
version either: the phases and the overrides below are the same for both.

**One at a time.** Only **one** enemy runs you down: `ClaimRunDown` holds a single static claim, taken by the nearest candidate and handed over only to something at least
`TailHandover` (60 m) closer, so the claim does not flicker between two planes at similar range.
Everything else in the flight flies normal AI in its own band, which keeps the picture readable
and leaves them as a separate threat rather than a queue on your six.

**Two phases: join, then lock.** A run-down does **not** switch the safety off the moment it
starts. `AiState.Tail` runs in two phases, and `RunningDown` — the flag every override is gated
on — is only true in the second.

**Join.** Everything is normal. The enemy flies the ordinary tail approach to
`ClampToBand(TailSlot())`, inside its own band, with `KeepNoseUp`, `ChooseTurn`,
`CheckGroundAvoidance` and `Contain`'s floor all doing their jobs. It manoeuvres onto your six
the safe way, checking terrain as it goes. Nothing about this phase is new.

**Lock.** `TickTailLock` grants the lock when the enemy is genuinely *established* astern:

- `TailBehind()` — how far behind you it is, projected onto **your** direction of travel — is
  positive and no more than `TailLockReach` (260 m), and
- `NoseOnTrack()` — its heading is within `TailLockHeadingDeg` (45°) of your track, i.e. it has
  finished turning and is flying the way you are.

It holds until it drops more than `TailLockReach × TailLockGive` (416 m) behind, or ends up in
front of you. Both are measured **along your track only, never in altitude** — deliberately, or
the thing would deadlock: an enemy held two hundred metres above you by its own band would never
satisfy an altitude-aware test, so it could never earn the right to descend and fix exactly the
problem the lock exists to fix.

**Only then is the ground ignored.** While `RunningDown`:

| | Ordinarily | Locked on your six |
| --- | --- | --- |
| Tail slot | `ClampToBand(TailSlot())` | pure pursuit onto your centre |
| `Contain` | floor and ceiling push the nose back into the band | X containment only |
| `CheckGroundAvoidance` | below `minAltitudeMargin`, abort and climb | never fires |
| `KeepNoseUp` *(scout)* | below `contour + safeAltitudeMargin` any descent is **flattened to level** | off |
| `ChooseTurn` *(scout)* | picks the turn by probing arcs against the corridor floor, climbs out at 55° when both are blocked | off — turns the short way, like a fighter |
| Depth dodge *(scout)* | slides out of your plane of fire | off, and an active one is flown home (`Release`) when the lock is taken |

`KeepNoseUp` is the one that made the scout look immovable. It runs **after** `Contain`, as the
last step before `SteerToHeading`, so it overrides everything upstream: no amount of lowering the
band floor or unclamping the slot could get a scout below `contour + 260`, because the final
clamp simply deleted the downward component of its heading.

The depth dodge has to go for a different reason: `UpdateFiring` returns early while
`_dodge.Active`, and the dodge slides the plane out of the Z lane the bullets are fired in — a
dodging scout is one that has stopped shooting at you. It is **released, not cancelled**
(`ReleaseDodge`): the lock takes effect immediately, but the depth is flown back over up to 1.5 s
instead of teleporting, so the guns come back a beat later and the plane never pops. See the
depth dodge above.

What is **not** suspended is the scout's turn bias (`turnBias`, 0.65× to the right). That is the
plane's handling, like the fighter's 122 m turn radius, not a following behaviour.

**It corrects altitude, it does not dive.** `EaseDescent` caps the locked heading at
`TailDescentDeg` (35°) below horizontal, keeping the horizontal direction. So closing a two
hundred metre height difference is a steady descent onto your six over ground you have just
flown over, not a nose-down plunge from wherever the chase happened to begin — and with
`TailSpeed` running up to 1.3×, the horizontal component still exceeds your speed at full
descent (1.3 cos 35° = 1.06×), so it keeps closing while it comes down. A **scout** is the
exception: its top speed is capped below every garage plane's (see the engagement boost), so
against a player holding full throttle its 1.3× is clamped away and the range opens instead —
that is deliberate, and it is the other way out of a scout's run-down.

Once it is down there it will follow you into a hill. That is the point, and it is fair: the
slot is behind you on your own track, so an enemy that hits terrain is one you deliberately
dragged there, and by the geometry you were about to hit it first. Dropping onto the deck and
pulling up late is the intended counter, and the only one that does not require turning to
fight. **Props do not touch it** while locked — `OnTriggerEnter` ignores
`BattlefieldProps.Layer`, so trees and burned houses cannot chip away at a chase that is by
design flying through the scenery. Terrain still kills it.

**Holding it — pure pursuit onto your centre.** A run-down does not chase the slot and it does
not copy your heading. `TailHeading` points the nose **straight at `_target.position`**, the
centre of your plane, every step. Both roles, no blending, no special case for the scout.

That is what makes "same altitude" and "shoots at you" the same statement. The guns fire down
the nose (`UpdateFiring` launches along `_heading`), so a nose that is on your centre is a
firing solution by construction, and `HasFiringSolution` waves it through — at the 95 m standoff
the miss window allows 39° of error, well outside the 26° cone.

Pure pursuit at matched speed is also what closes the altitude gap on its own: an enemy pointing
at you and flying your speed converges onto your six at constant range, whatever height you pick.
`TailSpeed` supplies the throttle — it lerps from a 1.3× closing speed down to exactly your own
as `TargetDistance() − TailStandoff` shuts, so it settles at 95 m instead of overrunning, and
measuring the gap from the *range* rather than from the slot means it never accelerates when it
is already too close.

In the join phase none of this applies: the heading is still the old slot-chase, blending to
copying your heading inside `TailSlotTolerance` (55 m), which is what puts the nose on your
track and earns the lock in the first place.

**Breaking it — get off the gun line.** There is **no timer**, and **no requirement that you
shoot**: a run-down lasts as long as you keep flying where its guns already point, which is the
whole point of it. `TickTail` gives up on four things, and the first is the one you will use:

- **`OffGunLine()`** for a continuous `TailBreakSeconds` (1.1 s) — you moved far enough off the
  line of fire, measured the same way `HasFiringSolution` measures a miss: `range × sin(error)`
  between the enemy's nose and your centre, against `_targetRadius × TailGunLineFactor`
  (about 180 m). Because a locked enemy flies pure pursuit, its nose is on you by construction,
  so this can only open up when you **out-turn its rotation speed**. Out-manoeuvre it and it
  lets go — no shooting required. Checked only while locked; during the join phase the nose is
  on the slot, not on you, so the test would be meaningless.
- **`UnderThreat()`** — your velocity within `threatCone` (18°) of the enemy inside
  `threatRange` (fighter 420 m, scout 340), with its own nose swung more than `threatTailAngle`
  (fighter 95°, scout 115) away from you. It matters mostly in the join phase; a locked enemy is
  already pointed at you, so the test rarely fires — and on the scout it is narrow enough that a
  run-down survives most of what you can put on it.
- **Range** opening past `maxFireRange × 1.4` (700 m), which `TailSpeed`'s 1.3× closing speed
  is there to prevent.
- **`TailOffAngle()` past 105°** for the same 1.1 s — you turned hard enough that it is no
  longer behind you at all, so there is nothing to tail. This is what catches a full reversal.

Whichever fires, it goes straight back to `EnterAttack`, which clears `_tailLocked`. Every
override is gated on `RunningDown`, so on that single line the enemy loses the ground exemption,
the prop immunity, the pure pursuit and the 1.3× closing speed all at once, and the scout gets
`KeepNoseUp`, `ChooseTurn` and its depth dodge back. Its throttle eases to cruise through
`UpdateSpeed`'s ordinary drag and its turn rate was never touched, so it is flying stock
`flySpeed` and `rotationSpeed` under stock AI from the next tick.

Two things are deliberately suppressed while tailing: `WantsReversal` returns false, so the
enemy will not flip away from a tail it has earned, and `TickCircle` does not count (it only
accumulates in `Attack`/`Fly`), so a tailing enemy never randomly breaks into the circling
evade. Both restore the moment the tail breaks and normal combat resumes.

The heading still goes through `Contain`, but during a run-down `Contain` is doing only half its
job: the level's **X** edges still turn the enemy back, and the vertical margins are zero. It
will not follow you out through the side of the level. It will absolutely follow you into the
ground.

## When an enemy is allowed to shoot (`HasFiringSolution`)

Every shot outside the diving pass has to clear two gates, and the second one is the one that
matters: the round must actually pass close to the target.

1. **Cone.** The heading error against the aim point (`PredictIntercept`, falling back to your
   raw position) must be inside `fireAngleThreshold` — or inside the wider snap window
   `SnapFireConeDeg` (26°), which exists so a nose sweeping across the sky can still take the
   shot as it passes.
2. **Miss distance.** `range × sin(error)` — the perpendicular distance the round misses your
   centre by — must be within `_targetRadius × SnapWindowFactor` (2), about 60 m for a plane
   measuring 60 m across.

The second gate is what makes the cone range-aware, and it used to apply only to the snap
window: an error inside `fireAngleThreshold` returned true on the spot, with no reference to
how far away you were. A 14° cone is a fine gate at 200 m and a useless one at 450, where it
lets the enemy open fire aimed 109 m off — three plane-lengths clear of you. The result was a
stream of tracer flying dead level well above or below the player, which is what it looks like
on screen. Perversely it also made the *wider* snap branch the stricter of the two, since that
one did check the miss distance.

Applying it to both branches turns the fixed cone into an effective cone that closes with
range: the full `fireAngleThreshold` is usable inside about 240 m, and at 450 m the enemy has
to be within 7.7° before it will pull the trigger. Up close nothing changes — at the tail
standoff of 95 m the geometry allows 39°, so the cone is the binding limit again and the enemy
shoots as freely as it ever did.

The diving pass is the deliberate exception: it fires down the whole run without a solution at
all (see above).

## Breaking the turning circle (both roles)

Two planes that both turn toward each other at a similar rate settle into a co-rotating circle
— a Lufbery — and nothing resolves it: the enemy's `Attack` heading is a lead pursuit onto the
player, so as long as the player keeps turning, the enemy keeps turning with them at a constant
radius. It is the one shape a real pilot would never fly for long, because the answer to a
stalemated circle is always to change the *plane* of the fight rather than to keep pulling in
this one. The old state machine did not: `attackDuration` (3.5 s) broke the attack off into
`Fly`, but `Fly` perches 90 m above the player, which from inside a circle is just more circle.

`TickCircle` watches for it. Every physics step, while `Attack` or `Fly` and inside
`threatRange`, it checks the enemy's own turn rate: turning at `CircleRateFraction` (50%) or
more of its maximum, in the same direction as the step before, adds to `_circleTimer`; a
direction flip, a slack turn or the player opening the range zeroes it. `CircleSeconds` (2.5 s)
of that is a circle, and the enemy breaks out of it with a manoeuvre from the repertoire below.
The break is not free: it shares `evadeCooldown` with the ordinary evade, so a player who keeps
re-establishing the circle gets a *different* manoeuvre thrown at them roughly every 4 – 5 s
rather than a twitch every second.

The turn rate is read straight off `_angularVelocity`, not from any comparison with the player.
That is deliberate — it means the same detector catches every version of the stalemate (both
planes circling, or the enemy hauled around by a player who is simply flying a circle) without
needing to model what the player is doing.

## The evade repertoire

`EnemyEvade` owns the manoeuvres; `EnemyController` picks one and steps it while `AiState.Evade`
is up, and `ComputeHeading` just returns `_evade.Heading`. Each move is a short sequence of
phases whose target heading is recomputed every step from the *live* positions, so a move stays
aimed at where the player actually is rather than at where they were when it started. All of
them still pass through `Contain`, `ChooseTurn` and `KeepNoseUp` afterwards, so no manoeuvre can
fly a plane out of its band or into a hill.

| Move | Shape | Why it breaks the circle |
| --- | --- | --- |
| `Break` | away from the player at `evadeBreakAngle`, with the old heading jitter, for `evadeDuration` | the original reactive dodge; kept as the answer to *being shot at*, not to a stalemate |
| `Scissors` | three 0.7 s crosses, alternating ±50° either side of the line to the player | stays engaged and forces an overshoot instead of fleeing — the aggressive break, and the nose sweeps the player twice on the way through, so it shoots |
| `Chandelle` | 1.1 s climbing at 62° in the current direction, then 0.9 s pulling back down onto the player | trades speed for height and re-enters from above: the circle becomes vertical |
| `SplitDive` | 0.9 s nose-down at 48° *the other way*, then 0.8 s back toward the player | the mirror of the chandelle — reverses and trades height for speed, which the fighter's energy model then keeps |
| `Extend` | 1.5 s flat out away from the player (pitch clamped to ±12°), then a hard turn back | resets the geometry completely and ends in a head-on merge; on the fighter the turn-back is big enough to trip `WantsReversal`, so it comes back round the loop |

**Both roles fly all five.** Nothing in the trigger, the picker or the moves is role-specific —
the scout evades out of the repertoire exactly as the fighter does, and its own manoeuvre, the
depth dodge, is untouched and still runs on its own trigger and its own 14 s cooldown. The two
do not interfere: the dodge only moves the plane in **Z**, out of your plane of fire, while the
evade owns the X/Y path, so a scout can be sliding out of the firing plane and flying a scissors
at the same time. What differs between the roles is only what the airspace allows.

**The vertical pair get room to work.** A chandelle climbs ~146 m and a split-dive drops ~100 m,
and *neither band is that tall* — the fighter's is 111 m (379 – 490) and the scout's corridor
about 140 — so with plain containment both moves were quietly flattened into nothing. While one
of them is running, `Contain` gives the band `EvadeBandGive` (120 m) in the direction the move
needs:

- `Chandelle` raises the roof to `EvadeRoof` — the band ceiling + 120, hard-capped at
  `ceilingY − ManoeuvreTopMargin`, the same 40 m ceiling the fighter's dive climbs to.
- `SplitDive` lowers the floor to `EvadeFloor` — the band floor − 120, but **never below**
  `minAltitudeMargin + ManoeuvreFloorLift` above the ground, measured against `GroundRef`: the
  flat conservative `groundY` for the fighter and the *sampled terrain contour* for the scout.
  The scout's floor is the one number in this system that is never relaxed, because it is the
  one that is measured against real ground.

`PickEvade` then draws only from what fits: `Chandelle` needs `EvadeClimbRoom` (140 m) of
headroom under the raised roof, `SplitDive` needs `EvadeDiveRoom` (110 m) over the lowered
floor. That makes the pair complementary rather than always-on, and differently so per role — a
fighter near its band roof cannot chandelle, a scout low in its corridor cannot split-dive but
can climb, a scout high in it can do the reverse, and a scout squeezed over a hilltop draws
neither. `Break`, `Scissors` and `Extend` are always available, so there is never an empty pool.

From what is left the pick is random, skipping whatever it did last time. Repeating a manoeuvre
is the one thing that makes a repertoire read as a script, so it is the one outcome the picker
refuses.

A circle break never draws `Break`: flying away at an angle is a fine answer to a gun on your
tail and a poor answer to a stalemate, which is the whole distinction between the two triggers.

**Which side each move takes is picked on that move's own geometry.** `EnterEvade` measures
`roomUp` and `roomDown` inside the band and picks the roomier side, but it used to run that test
only on `Break`'s pair (`awayHeading ± evadeBreakAngle`) and then hand the winning sign to
`Scissors`, which offsets from `towardHeading` — the *opposite* heading. Adding the same signed
offset to two headings 180° apart reverses which one points up, so the room test was choosing the
scissors leg with **less** vertical room, every time: a scout low in its corridor would find that
"up has more room" and cross downward at up to 50° off a line that already pointed at a player
below it. `PickSide` is now called once per geometry — on `awayHeading ± evadeBreakAngle` for the
break heading, and again on `towardHeading ± ScissorsAngle` when the move drawn is a scissors.
`Chandelle`, `SplitDive` and `Extend` do not use the side at all.

## Engagement boost (both roles)

Without this, a player who simply flies away is uncatchable: both roles cruise at 160 and the
player cruises at 180, or 234 on a boost. So when the player is beyond `engageRange` (fighter
450 m, scout 380) **and** moving away
(`dot(playerVelocity, enemy → player) > 0`), the enemy's speed target becomes the player's own
speed × `engageFactor` (1.15), ignoring its configured `flySpeed` entirely. It eases in and out at
`engageResponse` (2 /s) and decays back to normal the moment the player stops running or the range
closes.

It is folded in with a `Max` against everything else that can raise speed — the `Return` catch-up
for an off-camera enemy, the fighter's dive, `TailSpeed`'s 1.3× — so whichever is fastest at that
moment wins.

**The scout is capped; the fighter is not.** Everything above is a *target*, and none of it
respected a ceiling: a scout chasing a fleeing Camel took 288 × 1.15 = 331 and simply outran it —
and the Albatros's 300 the same way — for as long as the player kept running — most visibly right after
shaking off a run-down, which is exactly the moment the range is open and the boost is at full.
`FlightSpeed` now clamps a scout to `flySpeed × maxSpeedMultiplier` (160 × 1.6 = **256**) after
every one of those terms, which is under the Dr.I's 264 — the slowest thing the garage sells. A
scout can still close on a player who is not running flat out, and can never overhaul one who is.
The fighter keeps the uncapped boost: it is the role that is *supposed* to catch you, and its
own `_speed` is already clamped to the same 256 by `UpdateSpeed`.

## Keeping station in the scroller

The campaign camera ratchets: `CampaignLevelController.PositionCamera` takes
`Max(camX, lerp → playerX)`, so the frame scrolls right at roughly the player's 180 m/s cruise and
never comes back. Everything in the air is therefore flying against a moving frame, and an enemy
that cruises at 160 loses ground simply by flying straight. A single loop reversal costs it
another 270 m of frame — a third of the screen — and a break-away costs more.

The old answer was a hard left wall: `CampaignEnemies` pushed
`SetLeftWall(camX − halfViewWidth)` every frame and `ApplyVelocity` pinned the enemy's x to it.
**That wall was the bug.** `IsOnCamera` accepts `vp.x > −0.05`, so a pinned enemy counted as on
camera forever, the `Return` branch that exists for exactly this case never fired, and the enemy
ground against the left edge running its attack cycle from a position it could not leave, with
`Contain`'s edge push fighting its own heading. What the player saw was a plane stuck in the
bottom-left corner turning in circles for the rest of the level.

Four things replace it. All four are keyed off `Behind()` — 0 at the camera's centre line, 1 at
the left edge of the enemy window — and `Behind()` returns 0 unless `_scroll` is positive, so the
fixed `Level` scenes, whose window never moves, are untouched by all of it. `_scroll` is the
smoothed rate the window itself is moving at, measured in `SetBounds` (`ScrollResponse` 3 /s).

**1. No wall.** Enemies are no longer walled at the left edge — only the player is
(`CubeController`, which still needs it). An enemy that loses the race slides off the frame,
which is what `Return` has always been waiting for: 1.35 × cruise, aimed back through
`ClampToBand`. The recovery was already written; it just could not be reached.

**2. Station keeping** (`StationTarget`). The engagement boost above arms on *range* — 450 m on
the fighter, 380 on the scout, which at a ~431 m half-view is a screen edge away at most, and only
while the player is actively running. Station keeping arms on *screen position* instead: the speed target ramps from cruise at
the centre line to `TopSpeed` (256) at the left edge, and is folded into `_engageSpeed` with the
same `Max` and the same `engageResponse` easing as everything else. At the centre it does nothing,
so a dogfight still ebbs and flows; at the edge it is +76 m/s against the scroll, which is a plane
visibly clawing its way back into frame. It never exceeds a ceiling the engagement boost could not
already reach, so the escape ceiling is unchanged: a player at full throttle still gets away.

**3. The break-away no longer pays for the drift.** `EnterFly` used to freeze the player's x at
the moment of the break and hold that stale point for `flyDuration` — 290 m of give-away per
cycle, while the perch *height* tracked the player live. It now tracks live in x too
(`FlyBaseX`), and when the enemy is behind it breaks away **downstream**: `_flyLeadX` puts the
perch up to half a half-view ahead of the player, and `flyDuration` shrinks to
`FlyBehindFraction` (0.35) of itself. So the further behind it is, the shorter its break-away and
the further forward it ends — the disengage doubles as the reposition.

**4. A leash** (`Reappear`). A plane that is more than `LeashScreens` (2) half-views left of the
camera has lost the fight for good — a player boosting flat out can drag one there, and no
catch-up speed under the cap will ever close it. `CampaignEnemies.SetWindow` recycles it: it is
placed off the right edge at the ordinary `SpawnAhead` distance, at a fresh height from its own
`SpawnBand`, with its speed, heading, evade, dodge and dive state reset and `_appeared` cleared,
so it re-enters exactly like a newly spawned plane and waits for the first appearance again
before it may spend a skill. It reads as another plane joining, not as a teleport: the leash is
two half-views out of frame, so the move is never visible.

Nothing here changes the reversal loop. The looping was never caused by turning too fast — it was
caused by losing station and having no way back.

### Turning at catch-up speed (`TurnBoost`)

Speed alone made the scout a liability. Turn radius is `speed / turn rate`, so station keeping —
which can add 96 m/s to a scout at the left edge — widened every arc by the same 60 % it added to
the speed, and it did so exactly where the scout already flies lowest: down on the deck, clawing
back into frame. It out-ran its own ability to pull out of a turn and made craters.

`TurnBoost` gives the rotation rate the same boost the speed gets. It is keyed off the speed
surplus rather than off `Behind()` directly — `(FlightSpeed() − flySpeed) / (TopSpeed − flySpeed)`,
clamped to 0…1, lerped from 1 to `catchUpTurnMultiplier`. Keying it off the speed means it eases in
and out on `engageResponse` for free, in lockstep with the boost it is compensating for, instead of
snapping in at the screen position while the speed is still ramping. It also covers the other
things that raise speed — the engagement boost, the `Return` catch-up, the tail chase — for the
same reason: they lengthen the radius too.

`catchUpTurnMultiplier` defaults to **1.6** in both role configs, matching `maxSpeedMultiplier`, so
at full boost the radius is exactly the cruise radius: the scout is faster, not clumsier. Set it
below `maxSpeedMultiplier` to make speed cost some agility, or to 1 to switch the whole thing off.
Per-level `enemyRotationScale` scales `rotationSpeed`, which the multiplier rides on top of, so the
ramp is preserved.

It is applied in both places the turn rate is read: `SteerToHeading`, which flies the turn, and
`TurnClear`, which decides whether the turn is survivable. Both matter — `TurnClear` already
simulated the arc at the boosted *speed*, so leaving its rate unboosted would have had the scout
refuse turns it can now make and climb away from nothing. On the fighter it is taken as a `Max`
against `DiveTurnFactor` rather than multiplied into it: the dive factor exists for the same
reason — a fast plane needs a tighter turn to pull out — and compounding the two would have made a
diving fighter turn like nothing with a propeller on it.

## Per-level difficulty

`CampaignDefinition.enemyHealthScale` and `enemyRotationScale` are **multipliers** on each role's
own base, applied to both role configs by `EnemyConfigs.Scale`. They used to be absolute values,
which would have flattened the scout and the fighter into the same plane; as multipliers the ramp
is preserved and each role keeps its identity at every level.

| Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `enemyHealthScale` | 0.50 | 0.60 | 0.65 | 0.75 | 0.70 | 0.80 | 0.90 | 1.00 |
| `enemyRotationScale` | 0.80 | 0.84 | 0.88 | 1.00 | 0.91 | 1.03 | 1.10 | 1.18 |

`0` on either keeps the asset's own figure. `EnemyConfigs.Load` clones the Resources asset before
scaling — a Resources asset mutated at runtime stays mutated for the rest of the editor session.

Level 1 flies **all scouts**: the tutorial teaches the low fight and the guns against a 63-health
Fokker that turns at 70 °/s, not against a diving fighter. Levels 3, 5, 7 and 8 already mixed
Fokkers into their waves, so those levels now mix the two fights without a line of script changing.

The scout's base 88 °/s is picked so the **top** of that ramp still clears the garage: 88 × 1.18 =
103.8, under the Albatros's 104. The player can out-turn a scout on level 9 as surely as on level
1. Speed is not scaled at all, so the 256 cap holds everywhere by construction.
