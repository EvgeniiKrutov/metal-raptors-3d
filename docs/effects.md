# Effects

## Explosion (`Assets/Scripts/Explosion.cs`)

Spawned with `Explosion.Spawn(position, size)` — called from `CubeController` (player crash)
and `EnemyController` (enemy shot down). Entirely code-built at runtime: no prefabs, meshes
or materials in the project, no colliders anywhere in the effect.

### Structure

One root `Explosion` GameObject carrying 6–7 child blobs. Each blob is a procedurally built
low-poly rock-like shape: an icosahedron subdivided once (80 faces), every shared vertex
displaced radially by a random 0.72–1.3×, then vertices split per triangle so
`RecalculateNormals` gives flat, faceted shading. The mesh has a 0.5 base radius so
`localScale` reads as diameter, matching Unity's primitive-sphere convention.

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

Every player plane that reaches the ground explodes — whether it was shot down and fell
(`_falling`) or flew straight into the dirt under control. `CubeController.OnCollisionEnter`
spawns the explosion, hides the plane model, then raises `OnCrashed`.

The blast is spawned a beat before the plane is removed (`Explosion.RemovalDelay`, ~0.15 s),
so the plane is briefly visible inside the growing fireball and then vanishes into it rather
than blinking out the instant the effect appears. This applies to both sides:

- **Player** (`CubeController`) delays `HideModel` by `RemovalDelay` via a coroutine; the body
  object survives regardless (it stays the camera's follow target).
- **Enemy** (`EnemyController.Explode`) freezes the wreck's velocity and drops its health bar
  and collider immediately — so it can't drift, be hit again, or leave a floating bar — then
  removes the whole object with `Destroy(gameObject, RemovalDelay)`.

For all fail cases the fail screen ("MISSION FAILED") is delayed until the blast finishes:
`LevelController` / `CampaignLevelController` freeze the plane and stand down the enemies
immediately, then wait `Explosion.Duration` (final blob delay + lifetime ≈ 2.3 s) via a
coroutine before drawing the overlay, so the player watches the explosion play out first.
`Explosion.Duration` is the single source of truth for that wait. Winning a level is not a
crash and its overlay is still immediate.

### History

Replaced the original effect (single emissive orange sphere flash + 8 ballistic dark debris
cubes) — the grey end-stage of the blobs now serves as the debris/smoke reading. Originally
only shot-down planes exploded and the fail screen appeared instantly; now every ground
crash explodes and the fail screen waits for it.
