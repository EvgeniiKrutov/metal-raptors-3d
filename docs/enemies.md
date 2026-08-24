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
(150 m/s ÷ 135 °/s ≈ 64 m), so a scout held inside it could not complete a turn without flying
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

150 m/s, 135 °/s to the left and 88 °/s to the right, 125 health, 6 damage a round. It is the pressure role, so its gun discipline is
loose and its runs are long — where the fighter arrives, hurts and leaves, the scout is simply
always shooting at you:

| | Scout | Fighter |
| --- | --- | --- |
| `fireRate` | 0.18 s | 0.20 s |
| `fireAngleThreshold` | 18° | 14° |
| `maxFireRange` | 560 | 500 |
| `attackDuration` / `flyDuration` | 4.5 s / 1.0 s | 3.5 s / 1.6 s |
| `evadeDuration` / `evadeCooldown` | 1.0 s / 4.5 s | 1.3 s / 3.0 s |

Longer runs, shorter break-aways, shorter and rarer evades: the scout spends most of an engagement
pointed at you, and its 6 damage a round is what keeps that survivable.

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

A turn in a side-scroller costs altitude: half of it is spent pointing downward, and at 150 m/s the
scout's turn radius is 64 m one way and 98 m the other, so a reversal taken the wrong way round
drops it 127 – 196 m — straight through the corridor floor and into the ground.

So before committing to a turn of more than 30°, the scout works out whether it can survive it.
`TurnClear` runs a short forward simulation of the arc — 2 s at 0.15 s steps, using the *actual*
asymmetric turn rate at each simulated heading, so the wide slow right turn is modelled as wide and
slow — and probes the terrain under every sample. The arc is flown at the speed the plane is
*actually* making (`FlightSpeed` — cruise, the engagement boost, or the `Return` catch-up,
whichever is highest), not at the configured `flySpeed`: a scout chasing a boosting Camel is doing
270 m/s, not 150, and at that speed the real arc is nearly twice as wide and sinks nearly twice as
deep as the cruise figure. The simulated turn rate also ramps in on the same
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
full 135 °/s. Both sides are quicker than the player's 120 °/s on the strong side and slower on
the weak one, so which way you break decides who wins the turn. `TurnLimitAt` decides which by asking whether the turn is increasing the nose's
x-component (`-sin(heading) × sign(turnRate)`), so it is the *direction the nose is heading
toward* that is penalised, not a fixed rotational sign — and the penalty scales with that same
term, biting hardest through the vertical and vanishing in level flight, where no turn is either
left or right yet.

Since a wave attacks from the right flying left, the turn a scout most needs is the one back to
the right to chase a player who has run past it. That is the slow one. It is the scout's
exploitable weak spot, and the engagement boost below is what stops it from being a free escape.

This asymmetry *is* the scout's reversal cost — there is no separate manoeuvre for it. A scout
turning around simply takes longer one way than the other, through ordinary steering. It also
feeds the terrain check below: the slow side's turn radius is half again as wide (98 m against
64 m), so it is the side more likely to be refused when the ground is close.

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
| 4 | fly straight | +120 | 0 | `dodgeHold` 2.5 |
| 5 | roll onto the other wing | held at +120 | 0 → −75 | `dodgeRoll` 0.35 |
| 6 | slide back | +120 → lane | −75 | `dodgeBack` 0.8 |
| 7 | roll level | held in lane | −75 → 0 | `dodgeRoll` 0.35 |

5.5 s in all, then `dodgeCooldown` (14 s) counted from the moment it returns. Every phase is eased
with the same `SmoothStep`, so each movement starts and ends at rest and the joins between them are
seamless — the plane never snaps from one to the next. `ApplyRotation` adds `Bank` to
`PlaneRoll.Angle`, the same local-X roll the companion banks on; negate `dodgeBank` in the asset to
roll it the other way.

Mechanically the rigidbody's `FreezePositionZ` is cleared for the duration and the Z is driven to
the dodge's curve; it is restored and the plane snapped back to its lane when the dodge ends.
Once displaced more than `EnemyDepthDodge.ClearDepth` (35 m, half a plane) it is `OffPlane`:
`TakeDamage` and `Scrape` both refuse, so bombs and mid-air collisions miss it as cleanly as
bullets do. It does not fire while dodging — its own rounds are Z-frozen at its own depth and
could not reach you anyway.

**The dodge and the ordinary evade do not stack.** `TakeDamage` skips `EnterEvade` while a dodge is
running, so a round that lands in the first phases — before `ClearDepth` (35 m) makes it
untouchable, about 0.65 s in — does not also kick off a break turn. The ordinary evade is otherwise unchanged: same
`threatRange` trigger, same `evadeDuration` and `evadeCooldown` — what it *flies* now comes from
the repertoire below.

The cost is symmetrical, which is what keeps it fair: the 5.5 s manoeuvre is 5.5 s in which the
scout cannot be hit *and* cannot shoot, on a ~20 s cycle. You lose the kill you had lined up; it
loses a quarter of its gun time.

### Climbing when ignored

A scout that cannot reach the player — out of `maxFireRange`, or the player more than 80 m above
its corridor roof — counts the seconds. After `pressDelay` (5 s) its roof is raised to the top of
the **mid** band (379 m) for `pressDuration` (8 s); its floor stays on the contour, so it climbs
to meet the player rather than teleporting up a band. It drops back early once it closes to 60% of
its firing range. So parking high buys you a lull, not immunity.

## Fighter

150 m/s, 75 °/s, 130 health, a shot every 0.20 s, 6 damage. It used to cruise at the player's
own 180 and turn at 105 °/s; it is now the widest-turning thing in the air, with a **115 m turn
radius** (`v / ω` — 150 ÷ 1.31 rad/s) against the scout's 64 m one way and 98 m the other, and
the player's 86 m at cruise. It cannot follow you round a corner and it is not supposed to try:
a level runaway is answered by the engagement boost below, and a turning fight by the loop
reversal or a dive, never by matching your arc. Dropping the cruise under the player's 180 also
means a Camel flying level away from it is genuinely leaving, which is what pushes the fighter
onto the vertical instead of onto your tail.

### Dive energy

The fighter runs the dive-energy model — gravity along the flight path (`diveAcceleration` 90),
drag on the excess (`speedDrag` 0.9), a floor at cruise and a cap at `maxSpeedMultiplier` (1.6),
so 150 to 240 m/s — from its own `EnemyFighterConfig.asset`. The cap still clears the player's
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
`flySpeed` = 225 m/s, capped by `maxSpeedMultiplier` at 240) for the whole manoeuvre — climb
included, so even the set-up is quick. The 108 m turn radius that comes out of those two is
*tighter* than its 115 m cruise radius despite the extra speed. When the pass ends the floor
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
turning around — 120°/s through the loop against 75 °/s of ordinary steering, a 72 m radius
against 115 — so the manoeuvre is no longer an alternative to a hard turn, it is the hard turn.
That is also why the diving pass borrows it for the wingover at the top. It
is hard to punish — it keeps all its energy — but it takes a while and it is legible, so it is a
beat you can reposition into rather than a corner you get turned in.

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

## Engagement boost (both roles)

Without this, a player who simply flies away is uncatchable: both roles cruise at 150 and the
player cruises at 180, or 234 on a boost. So when the player is beyond `engageRange` (450 m) **and** moving away
(`dot(playerVelocity, enemy → player) > 0`), the enemy's speed target becomes the player's own
speed × `engageFactor` (1.15), ignoring its configured `flySpeed` entirely. It eases in and out at
`engageResponse` (2 /s) and decays back to normal the moment the player stops running or the range
closes.

It is folded in with a `Max` against everything else that can raise speed — the `Return` catch-up
for an off-camera enemy, the fighter's dive — so whichever is fastest at that moment wins.

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
Fokker that turns at 84 °/s, not against a diving fighter. Levels 3, 5, 7 and 8 already mixed
Fokkers into their waves, so those levels now mix the two fights without a line of script changing.
