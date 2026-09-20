# Ground trucks and the airfield (`EnemyTruck.cs`, `CampaignTrucks.cs`, `Airfield.cs`)

Level 3 (`FIXED GROUND`, docs/aerodrome.md) is the first level with **no flying enemies at
all**. Its three waves are one `ww1_truck` each: an armed German transport that comes down the
aerodrome road from the map's far edge, shoots at the player on the way in, parks at the wire and
shells the airfield until it is stopped. Every level from here is meant to introduce a new enemy
type; the truck is level 3's.

## The model

`Assets/Resources/objects/machines/truck_ww1.fbx` (untracked, like everything under `objects/`).
One `WW1_Truck` root null over `Chassis`, `Running_Gear`, `Fenders`, `Front_End`, `Radiator`,
`Lamps`, `Hood`, `Cab`, `Cargo_Bed`, `Canopy`, `Tarp` and `Exhaust`, with a `Wheel_XX_Spin` null
inside each of the four wheels. Ten materials, no textures — `feldgrau`, `rust`, `rubber`,
`tarp`, `brass` and the rest carry the whole look, the same way `tank_ww1` does before its camo
atlas is bound (docs/battlefield.md). They are **authored far too dark to play against**; see
*Lifting the colours* below.

It comes out of the same Blender pipeline as the planes and the aerodrome: **real metres, Z-up,
with the −90° X stand-up already baked onto the root null**, so it imports Y-up and needs no
correction. Imported, `Blender +X → model −X`, `+Y → model −Z`, `+Z → model +Y`, which leaves the
truck **facing model +Z** — its radiator and bumper are at Blender −Y.

| | metres |
| --- | --- |
| length | 6.50 |
| width | 2.20 |
| height | 2.50 |
| wheel radius | 0.50 |

## Lifting the colours

The model ships the same near-black diffuse values `tank_ww1` does, and for the same reason: it
was authored as a background object. That is fine for a wreck sitting in the middle distance and
wrong for something the player has to find on a road and shoot at, on an evening level whose key
light sits 16° above the horizon. Measured off the FBX, seven of the ten materials are below
0.12 luma and the chassis is 0.012 — black.

The tank's answer is its camo atlas, bound in code over a **white `_BaseColor`** so the authored
tints cannot crush it (docs/battlefield.md, *Skinning the tank*). The truck has no atlas, so
nothing lifts it and it reads as a silhouette. `EnemyTruck.LiftedMaterial` raises the authored
colours instead: a **gamma lift of 1/2.2 per channel**, which moves the dark materials a long way
and the already-visible ones barely at all, and keeps each one's hue.

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

The body lands a little under the road ribbon it drives on (`AerodromeRoad.RoadColor`, 0.47), so
the truck reads as a solid object against it without looking repainted.

**The lift happens in gamma space.** The project renders in Linear (`m_ActiveColorSpace: 1`), and
the FBX importer converts each authored sRGB diffuse to linear before it reaches `_BaseColor`, so
raising the stored value directly would apply the curve to a number that has already been through
one. `Lift` takes the material colour `.gamma`, raises each channel there, and converts `.linear`
again — so the numbers in the table are the authored values and their results, whatever the
importer did in between.

It is done by **swapping the renderer's shared materials for lifted copies**, cached in a static
dictionary keyed by the source material, not with a `MaterialPropertyBlock`. A block is what
`TankBlock` uses, but a block carries one colour for everything it is set on, and each of these ten
needs its own; and the truck is a hundred-odd renderers, all of which would drop out of the SRP
batcher. Ten copies, made once per session, shared by every truck, keep the batching. The
imported materials themselves are never written to.

## Scale and orientation

`EnemyTruck.Measure` probes the model once, measures every mesh into an unrotated holder's own
space, and scales by `PlaneModelConfig.UnitsPerMeter` (≈ 7.765) — the **plane** conversion, not
`BattlefieldProps.MetreScale`. It is attacking an airfield whose own scale is derived from the
Camels parked on it, so a truck sized like a tree would read wrong against the hangars it is
shooting at. At that conversion it is ≈ **50 units long, 19 tall and 17 wide** against the
Sopwith's 66-unit wingspan.

The one rotation is `RoadYaw`, a flat **−90° about Y**, the same quarter turn the aerodrome takes.
It maps `(x, y, z) → (−z, y, x)`, so the model's +Z nose swings to world **−X**: the truck faces
left, drives left, and is read side-on by the camera with its axles along Z. The measured world
box follows the same swap — `size = (box.size.z, box.size.y, box.size.x)`.

## Why the model hangs off an unscaled root

A truck is one GameObject tree in two frames:

```
Enemy Truck        EnemyTruck, kinematic Rigidbody, BoxCollider, SmokeTrail   scale 1
└── truck_ww1      RoadYaw, scale = UnitsPerMeter
    └── <fbx>
```

The root carries no scale, so its position **is** the truck's ground-contact point at the centre
of its footprint, the collider box is written straight in world units, and `PlaneFire.Ignite`
and `SmokeTrail` hang off something unscaled — a fire parented to the scaled holder would come
out ≈ 7.8× too big, the same trap `Aerodrome.LightFires` sidesteps. The holder's local position
is whatever puts the yawed, scaled model's bounds minimum at `(−length/2, 0, −width/2)`, so the
measurement and the placement cannot drift apart.

Everything is put on `PlaneFactory.PlaneLayer` and every collider that came in with the model is
destroyed in favour of one root `BoxCollider`. The layer is what makes the truck **shootable**:
`Bullet` sweeps that layer alone.

## The drive

`CampaignTrucks.Spawn(count, rightEdgeX)` puts each truck on the road at
`rightEdgeX + 140` — off the right-hand end of a bounded map, about a screen-width beyond where
the camera can ever reach — and hands it a stop line. It then drives **left at 30 units/s**, a
sixth of the player's cruise, re-sampling `CampaignTerrain.SampleHeight` every frame and riding
`AerodromeRoad.SurfaceLift` (2.999… — the ribbon's lift plus its crown) above the ground, so it
follows the draped road over ridges and down into craters rather than through them. Level 3's
run is ≈ 1568 units, about **52 seconds** from spawn to the wire.

The four `Wheel_XX_Spin` nulls turn about their own local X — which is the axle, whatever the
world orientation — at `−speed / radius`, the radius being measured at build time as the wheel
centre's height over the root. Roll without slip, derived rather than dialled in, so a re-export
with bigger wheels still turns them at the right rate.

The stop line is
`Airfield.FenceX + FenceStandoff (45) + length/2`, so the truck halts with its nose 45 units
short of the fence. With no `Airfield` in the scene — a dev-spawned truck in a custom battle —
the stop line is negative infinity and it simply drives on, which is what makes it inspectable
anywhere.

Several trucks in one wave are staggered by 1.6 lengths at both ends, so they queue up the road
instead of overlapping.

## The gun

There is no gun anywhere in the model, and none is built. Rounds and a `MuzzleFlash` leave the
**cargo bed** — `(0.16 × length, 0.86 × height)` off the root, so up and to the rear — which
reads as crew firing from under the tarp.

| | value | against |
| --- | --- | --- |
| damage a round | 6 | an enemy fighter's 6 |
| rate | 1 / s | a fighter's 4 / s |
| range | 500 | a fighter's 500 |
| bullet speed | 400 | the same rounds everything else fires |

It leads the player exactly as an aircraft does — two iterations of *fly out to where they will
be* — and fires only while it is **on camera** and the player is in range, so nothing shoots at
the player from off the edge of the frame.

**Its target is decided by whether it is still moving.** Driving, it shoots at the player. Parked
at the wire, it commits to the fence and ignores the player entirely: a stopped truck is a free
shooting gallery, and the cost of taking your time over it is the airfield.

## The airfield's health

`Airfield` is a single scene object created with the aerodrome — footprint, ground level, and a
100-point pool that does not regenerate. Each shell takes
`MaxHealth × FireInterval / ShellSeconds`, with `ShellSeconds = 45`, so **one truck left alone at
the wire empties the field in 45 seconds**. The pool is shared across all three waves; it is the
whole level's budget, not each truck's.

Every shell also throws `Sparks` at the aim point — `(fenceX, ground + 12, truck z)`, a flat shot
west into the wire — so the drain is legible on the field rather than only in the HUD.

At zero the airfield raises `OnLost`, and `CampaignLevelController.OnAirfieldLost` ends the level
as a failure: the script stops, the weapons cut out, the plane flies level, and `BurnAirfield`
walks 14 explosions with ground blasts and fresh `WreckFire`s at seeded points across the field
over 3 seconds, shaking the camera on each, before the usual `GameMenuKind.Failed` screen. The
same screen the player gets for being shot down — the field going up first is the only difference.

The pool reads as a second `HealthBar` in the HUD, directly under the player's own and captioned
`AIRFIELD  87%` (docs/hud.md). It is built only when an `Airfield` exists, so every other level's
HUD is untouched, and the action column below it shifts down by one bar on level 3.

## Taking damage

The truck is an `IDamageable` with **100 hit points**, a fighter's, and carries the same floating
world-space health bar an enemy plane does, hung a little over its roof.

| Source | Reaches it by |
| --- | --- |
| Player rounds | `Bullet`'s sphere cast over `PlaneFactory.PlaneLayer`. 10 a hit, so ten hits. |
| Bombs | `Bomb.ApplyBlast`'s `OverlapSphere`, which already damages every `IDamageable` it finds. A bomb passes through the truck, detonates on the road under it, and takes ≈ 60 off at the centre. |
| Flying into it | `PlaneScrapes.Check`, below. |

`Bullet.Hostile` used to ask *is this target an `EnemyController`?*, which quietly made anything
new the player fired at friendly. It now asks which **side** the target is on — a player round
hits anything that is not the player, an enemy round hits only the player, and the evade grace
still protects a rolling player. Behaviour for planes is unchanged; the truck simply becomes a
valid target without a type test per enemy kind.

Below 30 points it arms a `SmokeTrail`. At zero it explodes, ignites, burns for **4 seconds** and
is removed — an enemy plane's wreck, without the fall. It reports itself destroyed at the
*moment* it dies rather than when the wreck clears, so a `wave` step unblocks on the kill.

## Flying into one

`PlaneScrapes.Check` now takes trucks beside planes. A plane is a point test inside a depth band;
a truck is 50 units long and 19 tall, so it is an **AABB test** against `EnemyTruck.Touches`,
its own box grown by the player's `HitboxRadius`. The answer is the same as clipping an enemy
plane: both sides take 10, the player gets sparks and a screen shake, and it is on the 0.5 s
collision cooldown. Not a crash — the player flies on. Plane-to-plane collisions are already
ignored at the layer level, so the truck never reaches `CubeController.OnCollisionEnter` and can
never be read as terrain.

## Writing a truck into a script

Waves name ground enemies with `ground` where they would name a `plane` (docs/campaign-scripts.md):

```json
{ "op": "wave", "enemies": [ { "ground": "truck", "count": 1 } ] }
```

`EnemyGroup` carries an `EnemyKind`; `CampaignEnemies.Spawn` skips anything that is not a plane
and `CampaignLevelController.SpawnWave` totals the trucks and hands them to `CampaignTrucks`, so a
wave can mix the two. `wave` still blocks until the wave is clear, and `EnemiesAlive` counts live
trucks, so **wave 2 only rolls out once wave 1's truck is dead** — stalling costs the field
rather than the tempo.

`CampaignScriptRunner.Warn` counts planes only, so a truck wave raises **no `ENEMY PLANE IS
INCOMING` banner**. The player finds it on the road themselves.

## Dev spawning

`DevStats`' spawn panel has a third button, `SPAWN TRUCK`, beside the scout and fighter. The panel
now holds an `Action` per button rather than an `EnemyRole`, since the truck is not one.
`IDevSpawnHost.DevSpawnTruck` puts one in off the right of the view; in a custom battle there is
no fence, so it drives on past the camera, shooting at the player the whole way.

## Files

| File | Role |
| --- | --- |
| `EnemyTruck.cs` | Measures and builds the model, lifts its colours, drives it, fires it, takes damage, dies. |
| `CampaignTrucks.cs` | The live list, spawn points, stop lines and stand-down for one level. |
| `Airfield.cs` | The fence line, the field's footprint, its health and its loss. |
| `CampaignScript.cs` | `ground: "truck"` in a wave. |
| `LevelDefinition.cs` | `EnemyKind`, and `EnemyGroup`'s second constructor. |
| `PlaneScrapes.cs` | The AABB scrape against a truck. |
| `Bullet.cs` | Side-based hostility instead of the `EnemyController` type test. |
| `LevelHud.cs`, `HealthBar.cs` | The captioned airfield bar. |
| `CampaignLevelController.cs` | Where the airfield is created, waves are split and the loss is played. |
