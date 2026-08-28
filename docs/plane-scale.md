# Plane scale

## One scale for every airframe (2026-08-26)

Every plane is sized from the real aircraft's dimensions through a single
metres-to-world-units constant, so two planes parked side by side are as big relative to
each other as the machines were.

`PlaneModelConfig` carries the blueprint figures:

| | length | wingspan | height | length in units | scale vs Camel | `OnScreenSize` |
| --- | --- | --- | --- | --- | --- | --- |
| Sopwith Camel | 5.72 m | 8.5 m | 2.59 m | 44.4 | 1.000 | 66.0 |
| Fokker Dr.I | 5.77 m | 7.19 m | 2.95 m | 44.8 | 1.009 | 55.8 |
| Albatros D.III | 7.35 m | 9.0 m | 2.8 m | 57.1 | 1.285 | 69.9 |

`PlaneModelConfig.UnitsPerMeter` is `66 / 8.5` ≈ 7.76. The **Camel is the reference at
1.0**: the 8.5 in the denominator is its wingspan, which used to be fitted to a flat 60
units, and the 66 raises that by 10% — the planes read too small at 60 once they were sized
off length instead of span. Every plane rides that one number, so the 10% is uniform and the
ratios above are untouched.

### Why length, and not the bounding box

`PlaneFactory.NormalizeLength` measures the model along its own nose-to-tail axis and scales
it so that extent is `lengthMeters × UnitsPerMeter`. The previous `NormalizeSize` fitted the
**longest** bounding-box dimension — the wingspan for all three — to 60 units, which made
the Dr.I (7.19 m span) exactly as wide as the D.III (9 m).

Length is the one axis worth pinning: the models are built from blueprints, so scaling by it
carries span and height along, and a model whose span drifts a few percent from the real
machine still lands at the right length rather than dragging its length off with it.

### Finding the nose axis

`BuildPlaneModel` already rotates the raw FBX with `standUpEuler` (plus the wheels-down roll)
so the nose points down the body's +X. `NormalizeLength` is handed
`Inverse(standUp) * Vector3.right` — that same nose direction expressed back in model space —
and `ExtentAlong` takes the mesh box's extent along it.

The nose **pitch** (`ModelPitchDeg + pitchTrimDeg`) is deliberately left out of that
rotation. It differs per plane — −10° on the Camel, −0.6° on the Albatros, whose
`pitchTrimDeg` cancels most of the model's built-in nose-down — so measuring in the body's
frame after pitching would fold a different slice of each airframe's height into its
"length".

Bounds are gathered from the mesh assets rather than `Renderer.bounds`, which is a
world-space AABB and would pick up the body's own rotation.

### What the size feeds

`OnScreenSize` is derived, not hand-set: `wingspanMeters × UnitsPerMeter`, the plane's
widest on-screen extent. It drives the garage and main-menu camera framing, the enemy
ceiling in `LevelController` / `CampaignEnemies`, and `DuelPlane`'s explosion, fire, smoke
and muzzle clearance.

Everything else follows the model at runtime and needed no change: colliders are built from
the scaled mesh, and `PlaneShooter` / `EnemyController` measure the body radius off the
renderers. `CubeController.ExplosionSize` (60) is the one size still written by hand — it
scales the *player's* smoke, fire and scrape sparks and knows nothing about which plane is
being flown. The practical effect in a level is that the Dr.I is now a genuinely smaller
target (span 55.8 against the Camel's 66) and the D.III a slightly larger one (69.9).

Scenery keeps its own conversion: `BattlefieldProps.MetreScale` is 7.2 units per metre and
then multiplies by a deliberate 1.5 oversize, so trees and houses read at the camera's
standoff (`docs/battlefield.md`). It is not tied to `UnitsPerMeter` and was not raised with
it — raising both would have left the planes looking exactly as small as before.

How much of the screen a plane fills is a camera question, not a model one — see
`docs/level-camera.md` for the framing, which is where the mobile size problem was.

`garageZoom` is untouched and still garage-only — it corrects for how much of the band's
*height* a flat airframe fills, which real span cannot answer (`docs/garage.md`).

### Adding a plane

Fill in `lengthMeters`, `wingspanMeters` and `heightMeters` from the real aircraft. There is
no size to tune by eye.

## Files

| File | Role |
| --- | --- |
| `PlaneModelConfig.cs` | `UnitsPerMeter`, each plane's real dimensions, and the derived `LengthUnits` / `OnScreenSize`. |
| `PlaneFactory.cs` | `NormalizeLength`, `ModelBounds` and `ExtentAlong` — the scaling applied when a model is built. |
