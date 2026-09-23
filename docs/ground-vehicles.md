# Ground vehicles (`GroundVehicle.cs`, `EnemyTruck.cs`, `EnemyTank.cs`)

Level 3 (`FIXED GROUND`, docs/aerodrome.md) is the one level with **no enemy aircraft at all**;
its only airborne enemy is the zeppelin that closes it (docs/enemy-zeppelin.md).
Its enemy is a column of vehicles that come down the aerodrome road from the map's far edge,
shoot at the player on the way in, park at the wire and shell the airfield until they are
stopped. Two types today, and every level from here is meant to introduce one more.

| | `EnemyTruck` | `EnemyTank` |
| --- | --- | --- |
| model | `machines/truck_ww1` | `machines/tank_ww1` (the wreck's mesh) |
| health | 100 | 300 |
| speed | 30 u/s (~52 s in) | 18 u/s (~87 s in) |
| at the player | cargo-bed gun, only while driving | roof gun, **always** |
| at the airfield | the same gun, once stopped | two sponson guns, once stopped |
| drains the field in | 45 s | 22 s |

`GroundVehicle` is the shared half: the drive down the road, the queue, the collider, health,
the floating bar, damage, death and the shot plumbing. Each subclass adds only its model and its
guns, through one `Aim(dt)` call per frame.

## The order they arrive in

Level 3's script is three truck waves each followed by a tank wave. `wave` blocks until the wave
is clear, so **the tank only rolls out once the truck is dead** and there is one vehicle on the
road at a time:

```
say ×3           (the opening cutscene)
wave truck  →  killed
wave tank   →  killed
wait / say ×2
wave truck  →  wave tank
wait 3
wave truck  →  wave tank
zeppelin    →  hanging over the fence
wait 30
say ×2
finish
```

Writing it as two consecutive `wave` steps rather than one wave holding both is what makes the
ordering fall out of the existing op — no new mechanism, and the pair still reads as one beat.

## Queueing at the wire

A vehicle's stop line is the **later of the fence and the vehicle in front**:

```
line = fenceX + FenceStandoff (45) + ownLength/2
if something is ahead and alive:
    line = max(line, itsRearX + QueueGap (30) + ownLength/2)
```

recomputed every frame rather than latched, so the column behaves correctly as it changes:

- A vehicle that catches the one in front **stops behind it** and, being stopped, starts shelling
  the airfield over its roof — stopped is stopped, whatever stopped it.
- When the one in front dies, the line drops back to the fence and the follower **rolls on again**
  by itself, and goes back to shooting at the player.
- `CampaignConvoy` keeps its live list in road order and re-links on a death, so B dying makes C
  follow A rather than jumping to the fence.

None of this fires on level 3 as scripted — one vehicle at a time means nothing ever queues. It
is there for dev spawns, for `spawn`/`waitclear` scripts, and for whenever a wave is allowed to
put two on the road at once.

## The models

Both are untracked, like everything under `objects/`, and both come out of the same Blender
pipeline as the planes and the aerodrome: **real metres, Z-up, with the −90° X stand-up already
baked onto the root null**, so they import Y-up and need no correction of their own.

`truck_ww1.fbx` — one `WW1_Truck` root over `Chassis`, `Running_Gear`, `Fenders`, `Front_End`,
`Radiator`, `Lamps`, `Hood`, `Cab`, `Cargo_Bed`, `Canopy`, `Tarp`, `Exhaust`, with a
`Wheel_XX_Spin` null inside each wheel. Ten materials, **no textures** — see *Lifting the
colours*.

`tank_ww1.fbx` — a Mark IV, one `markIV` root over `cab`, `hull`, `rearDeck`, `sponsons`,
`trackFrames`, `tracks`, `unditching`. It is the same mesh the battlefield streams as a burning
wreck (docs/battlefield.md). Five materials, and the 2048² camo atlas covers **one of them** —
the body mesh's (docs/battlefield.md, *Skinning the tank*). The other four are authored dark and
are left that way; unlike the truck, the tank takes no colour lift. Three of its nodes are
load-bearing here:

| Node | Is |
| --- | --- |
| `cannon` | the roof gun's barrel, resting forward and 32° up |
| `cannonAxle` | its pivot, at `(0, 1.95, 2.09)` in model metres |
| `gunMuzzle`, `gunMuzzle.001` | the two sponson muzzles, at `x = ∓1.74`, pointing **forward** |

| | truck (m) | tank (m) |
| --- | --- | --- |
| length | 6.50 | 7.97 |
| width | 2.20 | 4.12 |
| height | 2.50 | 3.28 |

`VehicleModel` owns the measuring and the building for both: it probes the model once into an
unrotated holder, measures every mesh into that holder's own space, and scales by
`PlaneModelConfig.UnitsPerMeter` (≈ 7.765) — the **plane** conversion, not
`BattlefieldProps.MetreScale`. These are attacking an airfield whose own scale is derived from
the Camels parked on it, so a vehicle sized like a tree would read wrong against the hangars it
is shooting at. The results are **50 × 19 × 17** for the truck and **62 × 25 × 32** for the tank,
against the Sopwith's 66-unit wingspan.

Note that the enemy tank is therefore ~8 % smaller than the battlefield's wrecks of the same
mesh, which use the prop conversion with a 1.15 oversize.

## Orientation

The one rotation is `RoadYaw`, a flat **−90° about Y**, the same quarter turn the aerodrome
takes. It maps `(x, y, z) → (−z, y, x)`, so the models' +Z noses swing to world **−X**: they face
left, drive left, and are read side-on with their axles along Z. The measured world box follows
the same swap — `size = (box.size.z, box.size.y, box.size.x)`.

That also puts the tank's sponson guns, which point straight forward in the model, **straight at
the aerodrome** without any aiming at all.

## Why the model hangs off an unscaled root

A vehicle is one tree in two frames:

```
Enemy Truck / Enemy Tank    the component, kinematic Rigidbody, BoxCollider, SmokeTrail   scale 1
└── model                   RoadYaw, scale = UnitsPerMeter
    └── <fbx>
```

The root carries no scale, so its position **is** the ground-contact point at the centre of the
footprint, the collider box is written straight in world units, and `PlaneFire.Ignite` and
`SmokeTrail` hang off something unscaled — a fire parented to the scaled holder would come out
≈ 7.8× too big, the same trap `Aerodrome.LightFires` sidesteps. The holder's local position is
whatever puts the yawed, scaled model's bounds minimum at `(−length/2, 0, −width/2)`.

Everything is put on `PlaneFactory.PlaneLayer` and every collider that came in with the model is
destroyed in favour of one root `BoxCollider`. The layer is what makes a vehicle **shootable**:
`Bullet` sweeps that layer alone.

## The drive

`CampaignConvoy.Spawn(kind, count, rightEdgeX)` puts each vehicle on the road at
`rightEdgeX + 140` — off the right-hand end of a bounded map, about a screen-width beyond where
the camera can ever reach — never closer than a queue length behind whatever is already out
there. It then drives **left**, re-sampling `CampaignTerrain.SampleHeight` every frame and riding
`AerodromeRoad.SurfaceLift` (the ribbon's lift plus its crown) above the ground, so it follows the
draped road over ridges and down into craters rather than through them.

The truck's four `Wheel_XX_Spin` nulls turn about their own local X — which is the axle, whatever
the world orientation — at `−speed / radius`, the radius measured at build time as the wheel
centre's height over the root. Roll without slip, derived rather than dialled in. **The tank's
tracks do not move**: they wear the flat `tracks` material, not the camo atlas, so scrolling their
UVs would move nothing on screen. Animating them needs a track texture first.

With no `Airfield` in the scene — a dev-spawned vehicle in a custom battle — the stop line is
negative infinity and it simply drives on, which is what makes it inspectable anywhere.

## The truck's gun

There is no gun anywhere in the truck model, and none is built. Rounds and a `MuzzleFlash` leave
the **cargo bed** — `(0.16 × length, 0.86 × height)` off the root, so up and to the rear — which
reads as crew firing from under the tarp.

**Its target is decided by whether it is moving.** Driving, it shoots at the player, leading them
exactly as an aircraft does and only while it is on camera and they are in range. Stopped, it
commits to the fence and ignores the player entirely.

## The tank's guns

The tank fights on two axes at once, which is the point of it.

**The roof gun tracks the player at all times** — driving, queued or parked. `MountRoofGun` slips
a pivot in at `cannonAxle`'s position and reparents the `cannon` mesh under it, then measures the
barrel: its rest direction is the cannon's farthest mesh corner from the axle, which lands ≈ 148°
(up and toward the aerodrome), and its length is that same span. The gun is then laid by rotating
the pivot about **world Z** — the axis perpendicular to the play plane, so one angle covers the
whole 2.5D sky.

Aiming clamps before it slews. The wanted bearing is taken as
`atan2(max(Δy, ε), Δx)`, which is always 0…180°, then clamped to `[8°, 172°]` — so a player below
the gun projects to just above the horizon on the correct side and **the barrel can never point
into its own hull or the ground**. The lay then moves toward it at `TraverseDegPerSec` (60°/s),
which is slow enough to watch and slow enough to out-turn.

It fires only when the barrel is actually within `FireConeDeg` (12°) of the wanted bearing, so
out-turning the traverse really does make it hold fire, and the shell leaves **along the barrel**
rather than at the target — the lay is the shot.

**The two sponson guns attack the airfield**, and only once the vehicle is stopped. They point
forward in the model, which the road yaw turns straight at the fence, and they alternate every
0.5 s from `gunMuzzle` and `gunMuzzle.001` so the pair reads as a ripple rather than a salvo.

| | speed | damage | interval |
| --- | --- | --- | --- |
| enemy plane, for reference | 400 | 6 | 0.25 s |
| truck | 280 | 6 | 1 s |
| tank roof gun | 200 | 12 | 2.5 s |
| tank sponsons | 280 | 6 | 0.5 s, alternating |

The roof gun's shell is built from its own `Bullet` template — 1.6× the size and a hot orange
instead of `Bullet.RoundColor` — so a shot you have time to dodge looks like one.

## The airfield's health

`Airfield` is a single scene object created with the aerodrome — footprint, ground level, and a
100-point pool that **does not regenerate and is shared by the whole level**. Each shell takes
`MaxHealth × interval / ShellSeconds`:

| | `ShellSeconds` | drain |
| --- | --- | --- |
| truck | 45 | 2.22 / s |
| tank | 22 | 4.55 / s |

So the level's entire tolerance for something sitting at the wire is 22–45 seconds in aggregate,
across all six vehicles — not per vehicle. That is the difficulty knob; raise `Airfield.MaxHealth`
or either `ShellSeconds` to loosen it.

Every shell also throws `Sparks` at the aim point — `(fenceX, ground + 12, vehicle z)`, a flat
shot west into the wire — so the drain is legible on the field rather than only in the HUD.

At zero the airfield raises `OnLost`, and `CampaignLevelController.OnAirfieldLost` ends the level
as a failure: the script stops, the weapons cut out, the plane flies level, and `BurnAirfield`
walks 14 explosions with ground blasts and fresh `WreckFire`s at seeded points across the field
over 3 seconds, shaking the camera on each, before the usual `GameMenuKind.Failed` screen.

The pool reads as a second `HealthBar` in the HUD, directly under the player's own and captioned
`AIRFIELD  87%` (docs/hud.md). It is built only when an `Airfield` exists, so every other level's
HUD is untouched.

## Taking damage

Both are `IDamageable` and both carry the same floating world-space health bar an enemy plane
does, hung a little over the roof.

| Source | Reaches them by |
| --- | --- |
| Player rounds | `Bullet`'s sphere cast over `PlaneFactory.PlaneLayer`. 10 a hit — ten hits for a truck, thirty for a tank. |
| Bombs | `Bomb.ApplyBlast`'s `OverlapSphere`, which already damages every `IDamageable` it finds. A bomb passes through, detonates on the road underneath, and takes ≈ 60 off at the centre. |
| Flying into one | `PlaneScrapes.Check`, below. |

`Bullet.Hostile` used to ask *is this target an `EnemyController`?*, which quietly made anything
new the player fired at friendly. It now asks which **side** the target is on — a player round
hits anything that is not the player, an enemy round hits only the player, and the evade grace
still protects a rolling player. Behaviour for planes is unchanged; ground vehicles simply become
valid targets without a type test per enemy kind.

Below 30 points a `SmokeTrail` arms. At zero it explodes, ignites, burns for **4 seconds** and is
removed — an enemy plane's wreck, without the fall. It reports itself destroyed at the *moment* it
dies rather than when the wreck clears, so a `wave` step unblocks on the kill and the vehicle
behind it starts rolling again.

## Flying into one

`PlaneScrapes.Check` takes vehicles beside planes. A plane is a point test inside a depth band;
a vehicle is 50–62 units long and 19–25 tall, so it is an **AABB test** against
`GroundVehicle.Touches`, its own box grown by the player's `HitboxRadius`. The answer is the same
as clipping an enemy plane: both sides take 10, the player gets sparks and a screen shake, and it
is on the 0.5 s collision cooldown. Not a crash — the player flies on. Plane-to-plane collisions
are already ignored at the layer level, so a vehicle never reaches
`CubeController.OnCollisionEnter` and can never be read as terrain.

## Lifting the truck's colours

The truck model ships near-black authored diffuse, the same way `tank_ww1` does, and for the same
reason: it was authored as a background object. That is fine for a wreck in the middle distance
and wrong for something the player has to find on a road and shoot at, on an evening level whose
key light sits 16° above the horizon. Seven of its ten materials are below 0.12 luma and the
chassis is 0.012 — black.

`EnemyTruck.LiftedMaterial` raises its authored colours: a **gamma lift of 1/2.2 per channel**,
which moves the dark materials a long way and the already-visible ones barely at all, and keeps
each one's hue. The tank is not put through it — its four flat materials stand as authored.

| material | luma authored | lifted |
| --- | --- | --- |
| `chassis` | 0.012 | 0.134 |
| `glass` | 0.020 | 0.170 |
| `rubber` | 0.032 | 0.208 |
| `cargo_bed` | 0.070 | 0.298 |
| `feldgrau_tint` | 0.076 | 0.310 |
| `rust` | 0.081 | 0.317 |
| `feldgrau` (the body) | 0.112 | 0.369 |
| `tarp` | 0.140 | 0.406 |
| `brass` | 0.162 | 0.435 |
| `lamp` | 0.305 | 0.581 |

**The lift happens in gamma space.** The project renders in Linear (`m_ActiveColorSpace: 1`), and
the importer converts each authored sRGB diffuse to linear before it reaches `_BaseColor`, so
raising the stored value directly would apply the curve to a number that has already been through
one. `Lift` takes the material colour `.gamma`, raises each channel there, and converts `.linear`
again — so the numbers above are the authored values and their results.

It is done by **swapping the renderer's shared materials for lifted copies**, cached in a static
dictionary keyed by the source material, not with a `MaterialPropertyBlock`. A block carries one
colour for everything it is set on, and each of these ten needs its own; and the truck is a
hundred-odd renderers, all of which would drop out of the SRP batcher. Ten copies, made once per
session, shared by every truck. The imported materials themselves are never written to.

## Writing vehicles into a script

Waves name ground enemies with `ground` where they would name a `plane`
(docs/campaign-scripts.md):

```json
{ "op": "wave", "enemies": [ { "ground": "truck", "count": 1 } ] },
{ "op": "wave", "enemies": [ { "ground": "tank",  "count": 1 } ] }
```

`EnemyGroup` carries an `EnemyKind` (`Plane`, `Truck`, `Tank`); `CampaignEnemies.Spawn` skips
anything that is not a plane and `CampaignLevelController.SpawnWave` hands every other group to
`CampaignConvoy`, so a wave can mix aircraft and ground. `wave` still blocks until the wave is
clear, and `EnemiesAlive` counts live vehicles.

`CampaignScriptRunner.Warn` counts planes only, so a ground wave raises **no `ENEMY PLANE IS
INCOMING` banner**. The player finds them on the road.

## Dev spawning

`DevStats`' spawn panel has `SPAWN TRUCK` and `SPAWN TANK` beside the scout and fighter. The panel
holds an `Action` per button rather than an `EnemyRole`, since neither vehicle is one.
In a custom battle there is no fence, so they drive on past the camera — the truck shooting at the
player the whole way, the tank tracking them with its roof gun.

## Files

| File | Role |
| --- | --- |
| `GroundVehicle.cs` | The shared half: drive, queue, collider, health, bar, damage, death, shots. |
| `FloatingHealthBar.cs` | The world-space bar over the roof, shared with the enemy zeppelin. |
| `Gunnery.cs` | Lead aiming and the on-camera test, shared with the enemy zeppelin. |
| `EnemyTruck.cs` | The truck's model, colour lift, spinning wheels and one gun. |
| `EnemyTank.cs` | The tank's model, the traversing roof gun and the two sponsons. |
| `VehicleModel.cs` | Measuring and building either model at the plane conversion. |
| `CampaignConvoy.cs` | The live column in road order, spawn points, stop lines, re-linking, stand-down. |
| `TankSkin.cs` | The camo atlas, on a skinned copy of the body slot's material alone. Shared with the battlefield's wrecks. |
| `Airfield.cs` | The fence line, the field's footprint, its health and its loss. |
| `CampaignScript.cs` | `ground: "truck"` in a wave. |
| `LevelDefinition.cs` | `EnemyKind`, and `EnemyGroup`'s second constructor. |
| `PlaneScrapes.cs` | The AABB scrape against a truck. |
| `Bullet.cs` | Side-based hostility instead of the `EnemyController` type test. |
| `LevelHud.cs`, `HealthBar.cs` | The captioned airfield bar. |
| `CampaignLevelController.cs` | Where the airfield is created, waves are split and the loss is played. |
