# Effects

The player plane's night searchlight has its own page: docs/searchlight.md.
The ambient Verdun ground life — random shell blasts, burning smoke columns and
infantry squads — has its own page: docs/battlefield.md.
The player's bombs — release, ballistic fall and area blast — have their own page:
docs/bombs.md.
The parachuted health crate, its splinter burst and the green heal pulse on the plane have
their own page: docs/supply-drops.md.
The anti-aircraft shells bursting in the sky around the camera have their own page:
docs/sky-flak.md.

## Muzzle flash (`Assets/Scripts/MuzzleFlash.cs`)

Spawned with `MuzzleFlash.Spawn(position, direction, size)` the instant a round is created —
called from both sides' guns: the player's `PlaneShooter.Fire` (at the cowl flash point, along
`transform.right`) and the enemy's `EnemyController.UpdateFiring` (at the nose muzzle, along the
heading). Entirely code-built at runtime, no prefabs; purely cosmetic — no colliders or
rigidbodies, so it can never brush a plane, soak a bullet, or deal damage.

### Placement

The flash sits at the engine **cowl**, not up at the raised gun. `PlaneFactory.MountMuzzle`
emits a separate `MuzzleFlashPoint` transform at the same nose X as the gun muzzle but dropped
to the propeller-hub centre line (it skips the muzzle's `GunHeightAboveHub` lift), so the flash
reads as bursting from the nose cowling while the bullet still leaves the raised gun. The enemy
fires from a single nose muzzle on its centreline and flashes there.

### Structure

One root `MuzzleFlash` GameObject, rotated so its local +X points along the firing direction,
carrying:

- **Core** — one emissive sphere at the cowl, hot near-white `(1, 0.96, 0.75)`, diameter
  `size × 0.18`.
- **Spikes** — four thin emissive cubes fanning forward across ±28° around the barrel, flame
  orange `(1, 0.7, 0.25)`, up to `size × 0.32` long and `size × 0.05` thick. Each spike's
  length, width and angle are randomised per shot so no two flashes look identical.

`size` is the firing plane's body radius (half its longest renderer extent, ~30 for the
`onScreenSize = 60` models), so the flash scales to the plane. `PlaneShooter` measures it once
in `Initialize` the same way `EnemyController` does.

### Animation

The whole effect lives `0.07 s` — a few frames. It pops to full size instantly, then the root's
scale collapses (`1 − t²`, keeping core and spikes shrinking together) while emission dims
linearly to zero, so it reads as a quick bright pop rather than a lingering glow. The root
self-destructs at the end of its life; `OnDestroy` releases the per-piece material instances
(each piece needs its own material to animate its emission).

## Explosion (`Assets/Scripts/Explosion.cs`)

Spawned with `Explosion.Spawn(position, size)` — called from `CubeController` (player crash)
and `EnemyController` (enemy shot down). Entirely code-built at runtime: no prefabs, meshes
or materials in the project, no colliders anywhere in the effect.

### Structure

One root `Explosion` GameObject carrying 6–7 child blobs. Each blob is a procedurally built
low-poly rock-like shape from the shared `BlobMesh.Build()` builder
(`Assets/Scripts/BlobMesh.cs`, also used by the cloud layer — see `docs/clouds.md`): an
icosahedron subdivided once (80 faces), every shared vertex displaced radially by a random
0.72–1.3×, then vertices split per triangle so `RecalculateNormals` gives flat, faceted
shading. The mesh has a 0.5 base radius so `localScale` reads as diameter, matching Unity's
primitive-sphere convention.

Blobs spawn at random offsets inside `0.5 × size` around the impact point with random
rotations, so the cluster overlaps into one big irregular fireball.

### Animation

Each blob runs its own timeline: a random start delay (0–0.3 s) and lifetime (1.5–2 s),
so the cluster pulses organically; the whole effect runs ~1.5–2 s. Over a blob's
normalized life `t`:

- **Scale** — ease-out growth from 15 % to its peak (`size × 0.9–1.5`) during the first
  35 % of life, then ease-in shrink down to 7 % (the "small particle") until it vanishes.
- **Colour** — orange `(1, 0.45, 0.08)` → bright warm yellow `(1, 0.93, 0.45)` by `t = 0.3`,
  then smoothstep to dark grey `(0.17, 0.16, 0.15)` by `t = 0.85`.
- **Emission** — URP/Lit emissive at 2× colour while hot, fading to zero by `t = 0.75` so
  the grey end-stage particles do not glow.

The root destroys itself when the last blob finishes; `OnDestroy` releases the per-blob
meshes and material instances (each blob needs its own material because the delays put
blobs at different colour stages at any instant).

### Sound

One of `Resources/Sounds/explosion_1..3` played as 2D audio at 0.55 volume from a separate
carrier GameObject so it outlives the visual (3D rolloff would mute it at the camera's
~420 m distance).

### Crash flow

**Nothing explodes in the air.** Zero health on either side starts a fall (see "Death fall"
below); the explosion only ever happens where the plane meets the ground. Every plane that
reaches the ground explodes — whether it was shot down and fell (`_falling`) or flew straight
into the dirt under control. `CubeController.OnCollisionEnter` spawns the explosion, hides the
plane model, then raises `OnCrashed`; `EnemyController.OnCollisionEnter` calls `Explode`.

The blast is spawned a beat before the plane is removed (`Explosion.RemovalDelay`, ~0.15 s),
so the plane is briefly visible inside the growing fireball and then vanishes into it rather
than blinking out the instant the effect appears. This applies to both sides:

- **Player** (`CubeController`) delays `HideModel` by `RemovalDelay` via a coroutine; the body
  object survives regardless (it stays the camera's follow target).
- **Enemy** (`EnemyController.Explode`) freezes the wreck's velocity and drops its health bar
  and collider immediately — so it can't drift, be hit again, or leave a floating bar — then
  removes the whole object with `Destroy(gameObject, RemovalDelay)`.

Because the last enemy now dies on the ground rather than in the air, `LevelController` holds
the win screen back until the wreck is gone (`WinAfterWreck` waits on the destroyed
`EnemyController` reference), so the completed menu's `Time.timeScale = 0` can't freeze a plane
mid-fall. If the player was shot down in the meantime the win is dropped and the fail screen
takes over as usual.

## Death fall and burning wreck (`PlaneFall.cs`, `PlaneFire.cs`)

At zero health a plane is *not* destroyed — it becomes a burning wreck that falls. Both sides
run the same sequence (`CubeController.BeginFall`, `EnemyController.BeginFall`):

1. `SmokeTrail.Ignite` — the damage trail switches to its heavy burning mode (below).
2. `PlaneFire.Ignite` — flames on the model.
3. `PlaneFall.Begin` — the nose eases into a −38° dive while the plane rolls about its own
   nose axis at 230°/s and picks up speed: a diving barrel roll, the same fall the background
   duel's losing plane has always had (`DuelPlane`, docs/companion.md).

`PlaneFall` (see docs/flight-model.md) holds the shared fall constants and the per-step
integration both controllers call from their `FixedUpdate`; each falling plane owns one
instance of it. The wreck keeps its collider so the
ground contact still registers; it is out of the fight the moment it starts falling — it can't
be damaged again, can't scrape, can't fire, and `EnemyController.IsAlive` goes false so the
level's kill count, the campaign wave logic and its engine voice all drop it immediately rather
than waiting for the impact.

An enemy wreck that never reaches ground (its terrain chunk streamed away behind the camera, a
fall out over the sea) is removed silently after `PlaneFall.Timeout` instead of hanging in the
scene forever — no explosion, since it is well off camera by then.

### Flames (`PlaneFire.cs`)

`PlaneFire.Ignite(plane, size)` parents a `Fire` root to the plane's **physics body**, so the
flames ride the wreck through its tumble, and hangs five `BlobMesh` blobs off it (the same
faceted shape the explosion and clouds use) clustered at the **nose**, over the engine, each
jittered a little sideways and back along the fuselage so the fire isn't a single ball.

The nose anchor is measured at ignition, not assumed: models are not centred on the body pivot,
so `NoseLocal` walks the eight corners of the **hitbox mesh's** local bounds into body space and
takes the front-most point along the body's local `+X` (the nose axis every plane is built
along), at that mesh's lateral centre line. It is measured in body space rather than from world
`Renderer.bounds` because the plane is already banking — and often tumbling — when it catches
fire, and a world AABB inflates and shifts under rotation. The plane's hitbox is the fuselage
(the biggest mesh, see `PlaneFactory.AddPlaneCollider`), which is also why the search starts
there rather than at every child renderer: the searchlight's beam shaft is a child renderer too,
and it reaches hundreds of units ahead of the nose. A plane with no readable hitbox renderer
falls back to a fixed forward offset.

Each flame flickers on its own clock: a randomised rate (5–10 Hz) and phase drive a two-sine
scale pulse — so no two flames beat together and the fire never looks like a pulsing sphere —
while the colour slides between deep orange `(1, 0.32, 0.05)` and hot yellow `(1, 0.86, 0.38)`
with emission at 2.6× colour. Flames cast no shadows and carry no collider or rigidbody.

`Extinguish()` destroys the fire root and, through `OnDestroy`, its per-flame meshes and
materials (each flame needs its own material to flicker independently). It is called when the
wreck explodes on impact, when the player's model is hidden, and when a ditching player sinks;
a wreck removed outright takes its fire down with the `GameObject`.

For all fail cases the fail screen (`GameMenuKind.Failed`, see `docs/game-menu.md`) is delayed
until the blast finishes: `LevelController` / `CampaignLevelController` freeze the plane and
stand down the enemies immediately, then wait `Explosion.Duration` (final blob delay +
lifetime ≈ 2.3 s) via a coroutine before opening the menu, so the player watches the explosion
play out first. `Explosion.Duration` is the single source of truth for that wait — the menu's
own `Time.timeScale = 0` only lands after it, so it never freezes the explosion mid-blast.
Winning a level is not a crash and its screen is still immediate.

### History

Replaced the original effect (single emissive orange sphere flash + 8 ballistic dark debris
cubes) — the grey end-stage of the blobs now serves as the debris/smoke reading. Originally
only shot-down planes exploded and the fail screen appeared instantly; now every ground
crash explodes and the fail screen waits for it.

## Guns and rounds (`Bullet.cs`, `PlaneShooter.cs`)

`PlaneShooter` (player) and `EnemyController`'s own firing (enemy) both fire the same brass
`Bullet` from `Bullet.Build`: a stubby glowing slug, thick enough to read at the camera's
~420 m distance, dressed as metallic brass with only a faint emission so it looks like a lit
round rather than a laser bolt. Rounds fly straight at constant speed in the XY play-plane
and self-destruct on the first real collision (triggers, like the goal sphere, are ignored),
dealing damage through `IDamageable` if the target implements it — the ground just stops the
round.

Rounds carry almost no mass (`Bullet.Mass`, near zero) on purpose: a round only needs to
register the hit, never to physically push anything, so a near-massless round can't transfer
enough impulse to visibly shake or shove the plane it strikes — a full-mass round at
`bulletSpeed` would. `Rigidbody.collisionDetectionMode` is set to continuous, since at
`bulletSpeed` a round can cover several metres per physics step and would otherwise tunnel
through the ground slab or thin terrain. A round is also ignored against its own shooter's
collider, so a fresh round can never clip the plane that fired it while turning hard into the
muzzle's path.

Missed rounds are removed once they leave the camera view — but only *after* they have first
been seen on camera. Enemy rounds are frequently fired from just off-camera and fly into view
on their way to the player; culling on "off camera" without that seen-first gate would destroy
an incoming round before it ever rendered, which is exactly why enemy fire used to appear to
hit from nowhere. A hard lifetime cap exists underneath as a safety net for a round that never
re-enters the view at all.

`PlaneShooter` lives on the player's physics body next to `CubeController`, whose yaw makes
the shooter's own `+X` the flight heading, so rounds always leave along the nose; it is wired
up (muzzle placement, size) by `LevelController`. Guns on both sides fall silent once the
level ends (crash or win).

## Propeller (`PropellerSpin.cs`)

Spins the propeller pivot about the plane **body's** nose axis (`+X`, the same axis
`CubeController` yaws to the flight heading) at a constant ~2 rev/s — fast enough to look
alive, slow enough that individual blades still read. `PlaneFactory` wires the body in as
`axisSpace` at build time and the axis is recomputed from `axisSpace.rotation` every frame,
so it follows the plane through every bank, the garage's rest pitch and a drag-to-turn.

The rotation is centred on the hub — the combined mesh bounds of everything under the pivot,
not the pivot's origin — so the prop spins in place instead of orbiting an off-centre point.
Combining all the meshes matters for a multi-part assembly like the Albatros' (spinner, blade,
hub pin): taking the first `MeshFilter` found instead made the hub depend on Unity's
name-sort order.

### Why the body's axis and not the model's

The model is not exactly aligned with the body. `BuildPlaneModel` pitches it `ModelPitchDeg`
(−10°) nose-down inside the body, and the FBX's own built-in attitude adds to that — see
*The nose trim* in `docs/campaign.md`. What is left over is the angle between the body's `+X`
and where the model's nose really points:

| plane | built in at | + `ModelPitchDeg` | + `pitchTrimDeg` | off the body's `+X` |
| --- | --- | --- | --- | --- |
| Sopwith Camel | +7.3° | −10° | — | −2.7° |
| Fokker Dr.I | +5.9° | −10° | — | −4.1° |
| Albatros D.III | −2.1° | −10° | +9.4° | −2.7° |

Three or four degrees of cone is not visible on a spinning blade, so the body's axis is the
right one to use: it needs no per-plane data, it cannot drift out of step with a re-export,
and it is already the axis everything else about the plane is measured against.

**Chasing the axis is the wrong fix when a propeller looks wrong.** It has been blamed twice:

* Once correctly — an early version derived the axis from the blade mesh's shortest bounds
  extent, which assumed a symmetric disc and picked the wrong axis outright for a flat
  two-blade prop.
* Once wrongly. The Albatros' prop swept a badly slanted cone, and the axis was moved to the
  model's `Rz(ModelPitchDeg) * right` to chase it. That was **−10° off instead of −2.7°**, so
  it made the Camel and the Dr.I worse while barely helping. The real fault was the last
  column above: the Albatros was exported in a level attitude rather than a parked one, so it
  sat at −12.1° and the propeller honestly reported it. Fixing the attitude with
  `pitchTrimDeg` fixed the propeller, and the axis went back to the body's `+X`.

If a prop cones, check the plane's attitude and its propeller nodes before the axis.

## Scrape shake (`ShakeEffect.cs`)

A short, decaying position-and-roll wobble on the plane *model* only — the physics body flies
straight on, so the shake sells a scrape without ever moving the collider or the flight path.
`Play` restarts the shake at full strength and it eases back to a rest pose over a fixed decay
time. That rest pose is captured lazily on the first `Play`, from whatever orientation/offset
`LevelController` already built the model at, so the wobble always layers on top of the
model's real pose instead of overwriting it. The jitter amplitude is kept deliberately small
(a couple of metres, a few degrees of roll) — the model also carries the plane's collider, so
a bigger translational jolt could dip it into the ground during a low scrape.

The player's scrape also shakes the **camera**, which is a separate mechanism living in the
level controllers: `CubeController.Scrape` raises `OnScraped` whenever it actually applies (so
the 0.5 s cooldown gates both shakes together), and each controller answers by setting
`_camShake = 1f`, which decays over 0.3 s and offsets the camera by up to 7 units in XY. The
offset is applied to `_cam.transform.position` only, never to `_camBasePos` — that base
position is what terrain streaming and the campaign's left wall are measured from, and a
jittering base would drag the world around with the shake. Every scrape source reaches it:
enemies, trees and burned houses alike.

## Damage smoke (`SmokeTrail.cs`)

Armed once a plane's health drops below the shared danger threshold (`CubeController` /
`EnemyController`, see docs/flight-model.md) and never disarmed — once a plane is hurt enough
to smoke, it smokes until it's gone. `Arm` is idempotent, so it's safe to call again on every
subsequent hit. The emitter lives on the plane's physics body (so it always knows the current
nose axis and emits from the tail, the opposite end) and steadily spawns dark, half-transparent
cube puffs that tumble, drift backward and slightly upward, shrink, and fade out.

`Ignite` is the burning-wreck escalation of the same emitter, called when a plane's health
reaches zero: it arms the trail (if damage hadn't already) and switches it to a shorter emit
interval and a much larger puff size, so the falling wreck lays down a thick column of smoke
instead of the thin damage wisp. Everything else — world-space puffs, the live list, `Clear` —
is unchanged, so a burning trail dies exactly like a damage trail.

The puffs themselves are spawned in **world space, not parented to the plane** — once born
they hang in the air and fall behind as the plane flies on, rather than riding along with it.
Because of that they don't automatically die when the plane does: the emitter keeps a
live-puff list, and `Clear()` stops emission and destroys every outstanding puff at once. Both
level controllers call `Clear()` when a plane is destroyed or its model is hidden (so a killed
plane leaves no smoke hanging in the air), and the emitter also clears itself in `OnDestroy`
as a backstop, so tearing down the plane `GameObject` takes its trailing smoke with it even
without an explicit call. Each puff removes itself from the emitter's list as it
self-destructs, so the list never accumulates destroyed puffs. Like the other cosmetic
effects, puffs carry no collider and no rigidbody.

## Scrape sparks (`Sparks.cs`)

A small shower of emissive-yellow motes sprayed from the plane's position on a scrape (see
`CubeController.Scrape`), to sell the metal-on-metal contact — purely cosmetic, no collider or
rigidbody, so sparks can never brush a plane, soak a bullet, or deal damage themselves. Each
mote sprays outward in the play plane (Z stays flat, matching everything else in the level),
decelerates, shrinks to nothing, and cools from bright yellow-orange to a dim ember over its
short randomised life before self-destructing.

### Stripping the primitive collider

`UIFactory.CreatePrimitive3D(..., keepCollider: false)` is what makes all of this
collider-free, and *how* it strips matters. `GameObject.CreatePrimitive` always attaches one,
and the strip used to be a `DestroyImmediate` — which Unity **refuses to run inside a physics
trigger or contact callback**, throwing instead. Anything spawned from one of those callbacks
therefore kept its collider.

That is not hypothetical: with `CubeController.Scrape` reachable from `OnTriggerEnter` (a plane
clipping a tree — see `docs/battlefield.md`), all 14 spark cubes kept their `BoxCollider` and
appeared as static colliders at the plane's own position. The plane rammed its own sparks, was
held in mid-air by them, and `OnCollisionEnter` read that as a crash: explosion, level over.
`Explosion.Spawn` had the same exposure, since it is called from `OnCollisionEnter`.

The strip now **disables the collider first, then `Destroy`s it**. Disabling takes it out of the
physics scene immediately and is legal in any context; the deferred `Destroy` only tidies up the
component. `Destroy` alone would not do — it leaves the collider live for the rest of the frame,
which is exactly the window that crashed the plane. `BattlefieldPeople.AddSegment` builds its
figures from primitives too and now disables the same way.

## HUD health bar (`HealthBar.cs`)

Shared by both level controllers: a dark plate, a fill anchored to the left edge that drains
right-to-left as damage comes in (scaled on X by the health fraction) and shades from green to
red, plus the number on top.

## `IDamageable`

The contract anything a bullet can hurt implements: `Bullet` looks for it on whatever it hits
and applies damage through it before self-destructing. Both `CubeController` (player) and
`EnemyController` (enemy fighters) implement it so gunfire can wear either side's health down.
