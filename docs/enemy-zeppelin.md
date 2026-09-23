# The enemy zeppelin (`EnemyZeppelin.cs`)

Level 3's closing threat: an airship that sails in over the road, stops above the
aerodrome's fence and holds there while its three guns shoot at the player. It is **not
meant to be destroyed**. It takes damage, shows it and burns, but its health cannot drop
below a floor, and the level ends while it is still in the air.

It is a different object from the background airships (docs/zeppelins.md). It uses the same
model, but it sits **in the play plane** at the player's own depth, so it can be shot, it can
be flown into, and it is sized against the aircraft rather than the view.

## Size

`Length` is **1.3 × `SkyZeppelin.ApparentLength`**, which is **≈ 728 units**. The background
airships are drawn at 560 ± 12 % play-plane units whatever their depth (docs/zeppelins.md), so
the largest one reads as 627. The enemy is therefore always the biggest airship on screen: at
least 16 % longer than any background ship, ~84 % of a 16:9 view's width and about 16 Camel
lengths. Tying it to the background constant keeps that true if the background is ever
re-sized.

Earlier sizes were 50 m at the plane conversion (≈ 388 units) and then ten Camel lengths
(≈ 444). Both were smaller than the background ship behind them.

## What the model measures

The FBX hull is 158 m from nose tip to tail fins. Its bounding box is **not** the envelope:
the cruciform tail fins reach ±13.5 m and the gondolas hang to −14 m, while the envelope
itself peaks at a **9.35 m** radius. Everything below comes from slicing the `zeppelin_mesh`
vertices along the length with a short binary-FBX reader, outside Unity. All values
are fractions of the hull length, measured from the bounds centre, which `Fit` puts on the root.

| | along the length (+ = tail) | height | model metres |
| --- | --- | --- | --- |
| envelope radius | — | 0.0592 | 9.35 |
| forward gondola, underside | −0.1804 | −0.0872 | y −28.5, z −14.04 |
| aft gondola, underside | +0.1329 | −0.0809 | y +21.0, z −13.05 |
| gun platform on the crown | −0.0367 | +0.0707 | y −5.8, z 10.9 |

The model carries **its own machine gun** on a railed platform just forward of amidships. That
is the top gun; nothing is built on top of the hull in code.

Building, orientation and fitting go through `SkyZeppelin.BuildModel`: nose west, bounds
centred on the root, model colliders stripped and propellers spinning. The enemy then
measures the result with `SkyZeppelin.Measure`. Unlike the background ships it **keeps its
shadows**. Every node is put on `PlaneFactory.PlaneLayer`, which is what `Bullet` sweeps.

## Entry and hover

| | value |
| --- | --- |
| altitude | centre y = 380 (`CampaignLevelController.ZeppelinY`) |
| enters at | the view's right edge + half its length + 40, so it starts just out of frame |
| speed | 30 u/s, the truck's |
| stops at | centred on `Airfield.FenceX`, half over the field and half over the road |
| easing | constant braking of 6 u/s² over the last ≈ 75 units, with a 1 u/s creep floor so it arrives |
| hanging | a ±3-unit bob with a 7 s period, starting from zero once it has stopped |

The approach time depends on where the camera is when it spawns. With the camera at the
left end of level 3 it is about 30 s; with the camera at the right end it is about 65 s.

`Hanging` turns true the frame it reaches the fence. That is what the script waits on.

## Guns

There are three guns: under the forward gondola, under the aft gondola, and the model's own
gun on the crown. Each muzzle is the point from the table above, pushed 2 units further out
from the hull (down for the gondolas, up for the top gun). All three shoot only at the
player, and none of them shells the airfield; the trucks and tanks already do that.

| | value |
| --- | --- |
| interval | 1 s per gun, phased 0.5 / 1.0 / 0.75 s (forward, aft, top) so they never fire together |
| round | its own red two-tone round (below), **200 u/s** (the tank roof gun's speed), 6 damage |
| lead | full lead, via `Gunnery.Intercept` |
| range | 500 from that muzzle, and the muzzle must be on camera |

Each gun's cooldown is reloaded **whether or not it fired**, so the rhythm is steady. A
player coming back into range therefore meets alternating shots rather than a volley.

**The round is built to be seen on every sky.** The skies run from white cloud and pale haze
(midday, morning, the coast, the Dolomites) through beige-orange evening haze to near-black
night. No single colour contrasts with all of them, and the shared tan `Bullet.RoundColor`
disappears into the evening haze in particular. `BuildRound` therefore makes a two-tone round
from `Bullet.BuildTemplate`:

- **Core.** Saturated red `(1, 0.2, 0.12)` with its emission pushed to 2.5×, so it glows
  (and blooms) against the dark night sky. Red is also the one hue none of the skies share.
- **Rim.** A near-black, non-glossy cylinder 1.7× as wide and 1.25× as long as the core, set
  ~3 units **behind** it (further from the camera). The core stays in front and the rim shows as
  a dark outline around it, which carries the round against white and beige skies.
- **Size.** The whole round is 1.6× the standard one, the same scale as the tank's shell:
  ≈ 3.8 × 11 units of core. It is a slow round you have time to dodge, so it looks like one.

The rim depth has a lower limit. It sits 0.8 of the parent's local Z behind the core, and must
stay beyond √(rim r² − core r²) (≈ 2.6 units at these widths); otherwise the rim's front
surface pokes through the core's edges.

**No gun fires through the hull.** Before a shot, `Fire` raycasts the zeppelin's own capsule
along the shot's line out to the gun's range. A hit means the line crosses the envelope, and
the gun holds fire. That one rule splits the sky between the guns: the gondolas cover the
player below the envelope and past its ends, and the top gun covers the player above it.
Nothing else decides which gun is "on".

## Health

| HP | what shows |
| --- | --- |
| 6000 | full |
| ≤ 3000 | two smoke vents open on the upper hull, on the camera side |
| ≤ 1000 | the vents burn: denser smoke plus a `PlaneFire` at each |
| 500 | the floor, which `TakeDamage` never goes below |

The only gauge is a `FloatingHealthBar`, 150 × 5 units, hung 25 units over the top of the
envelope, which clears the top gun. Because of the floor it bottoms out at
≈ 8 %. At the player's ~50 damage per second of sustained fire,
reaching the burning stage takes about 100 s on target.

The vents sit on the envelope's surface, measured off the envelope radius, at −0.2 and +0.1
of the length. Each vent is two objects. The smoke emitter is turned −90° about Z, so `SmokeTrail`'s "behind"
direction points **up** and the plume rises instead of streaming sideways. The fire host is
left unrotated, because `PlaneFire` places its flames along its parent's +X.

## Contact

- **Bullets** reach it through the root `CapsuleCollider`, which lies along X with the
  **envelope's** radius and the hull's length. The fins and gondolas are outside it. The same capsule is what the guns test their lines against.
- **Flying into it** is a scrape, as with a vehicle. `PlaneScrapes.Check` takes the zeppelin as an
  optional last argument, and `Touches` is a capsule test: the distance to the hull's axis
  segment, grown by the player's hitbox radius. Both sides take 10 damage, with a 0.5 s
  cooldown on the zeppelin's side.
- **Bombs** that strike it burst in the air: `Bomb` treats a zeppelin hit like an enemy-plane
  hit, so there is no ground blast. `ApplyBlast` still measures falloff to the root, so only a
  burst near the middle of the hull does much damage.

It is **not** counted in `EnemiesAlive`, so a `wave` or `waitclear` can never block on it.

## The background airships stand down

The first frame its bounds enter the camera frustum, it raises `Sighted`.
`CampaignLevelController` answers by calling `SkyZeppelin.Retire`, so no background airship
is sent in after that. The ones already in the sky finish their crossing and are destroyed off
the left edge as usual.

## In level 3

Level 3's script calls it after the third tank:

```json
{ "op": "zeppelin" },
{ "op": "wait", "seconds": 30 },
```

`zeppelin` spawns it and blocks until it is hanging. The 30 s wait is the fight under it, and
there is no countdown on screen. The closing lines and `finish` follow. At `finish`, and on
any failure, `StandDown` silences the guns. The airship stays where it is and keeps bobbing
through the outro.

If it cannot spawn, because the model is missing or the level has no airfield, the op
returns at once and the script carries on.

## Custom battles

The Tab panel's **SPAWN ZEPPELIN** button (docs/dev-stats.md) is offered only in a custom
battle that has an airfield, which means **Verdun in battle mode**. Only one zeppelin can be
up at a time, and the button stays disabled while one is.

## Files

| File | Role |
| --- | --- |
| `EnemyZeppelin.cs` | Spawn, approach, hover, guns, health stages, capsule, sighting. |
| `SkyZeppelin.cs` | `BuildModel` / `Measure`, shared with the background airships; `Retire`. |
| `FloatingHealthBar.cs` | The world-space bar, shared with the ground vehicles. |
| `Gunnery.cs` | `Intercept` and `OnCamera`, shared with the ground vehicles. |
| `CampaignLevelController.cs` | `SpawnZeppelin`, `ZeppelinHanging`, retirement, stand-down, dev spawn. |
| `CampaignScript.cs`, `CampaignScriptRunner.cs` | The `zeppelin` op. |
| `PlaneScrapes.cs`, `Bomb.cs` | Contact and airburst. |
