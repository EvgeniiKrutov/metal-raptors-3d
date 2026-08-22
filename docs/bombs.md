# Bombs

The player's second weapon: **H** releases a free-falling bomb from under the aircraft. It is
the only weapon in the game with an area effect, and the only one that can hurt the plane that
fired it.

Three scripts, all built in code at runtime like everything else in the level scenes:

| Script | Role |
| --- | --- |
| `PlaneBomber.cs` | The H key, the cooldown, and the release. Lives on the player's physics body next to `PlaneShooter`. |
| `Bomb.cs` | The falling ordnance: ballistic flight, contact detection, detonation and the blast. |
| `CooldownSquare.cs` | The HUD square, shared with the R boost (docs/boost.md). |

## Configuration

All three tunables live in `PlayerConfig` (`Assets/Resources/PlayerConfig.asset`), next to the
gun's `damage` / `fireRate` / `bulletSpeed`:

| Field | Default | Meaning |
| --- | --- | --- |
| `bombDamage` | 60 | Damage at the dead centre of the blast. |
| `bombBlastRadius` | 90 | Radius of the lethal circle, in world units. |
| `bombCooldown` | 5 | Seconds before H can be pressed again. |

For scale: a plane is 60 units long with 100 hit points, one bullet does 10, and the camera
sees roughly 860 units across. So a centred hit costs six bullets' worth of health, the blast
spans about a fifth of the screen, and the plane crosses the whole view between two bombs.

Everything else — the bomb's size, its gravity, how fast it swings nose-down — is a constant in
`Bomb.cs`, since none of it is a balance dial.

## Release (`PlaneBomber`)

H is read with `wasPressedThisFrame`, not `isPressed`: unlike the gun there is no held-down
auto-fire, one press is one bomb. The key is gated exactly like `PlaneShooter`'s F — nothing
drops while the pause menu or the level briefing is open — plus one gate the gun does not have:
nothing drops while the cinematic bars are showing either (docs/level-intro.md). The component is
`Stop()`ped and `Resume()`d by the same callers that stand the gun down: `LevelIntro` during the
fly-in, and both level controllers' `StopWeapons()` on a crash, a ditching, being shot down, or
the level being completed.

There is no ammunition count. The cooldown is the only limit, it starts the instant the bomb
leaves the plane, and it does not tick while the bomber is stopped (a disabled `MonoBehaviour`
runs no `Update`), so an intro or a pause never eats into it. A radio line is the exception: the
cooldown keeps ticking through one, since the game itself is still running — only the release is
refused.

The bomb is spawned `BellyClearance` (8 units) below the plane along the plane's **own** down
axis, not world down — it separates from the belly, so releasing in a bank throws it out
sideways and releasing inverted throws it upward, which is what a real rack does. The clearance
is only slightly more than the bomb's own half-height (3), so it reads as leaving the fuselage
rather than appearing under it; nothing needs more room, because the bomb ignores its own plane's
collider from the moment it is launched.

It leaves with the plane's **full current velocity**, so the drop is a genuine ballistic
problem: at cruise from a few hundred units up it lands two to three hundred units ahead of the
release point, and bombing in a dive throws it much further and much faster.

Bombs are instantiated from a single inactive template (`Bomb.BuildTemplate`) held by the
bomber, the same pattern `PlaneShooter` uses for rounds, so all bombs in a level share one mesh
and one material.

## Flight (`Bomb`)

A plain grey box, 16 × 6 × 6 — about a quarter of the plane's length — dressed metallic and dull
so it reads as iron rather than as another glowing round.

It is spawned **level**, whatever the plane was doing, and then weathervanes: every physics step
its Z angle eases toward `atan2(vy, vx)` with an exponential approach (`AlignResponse`, 4/s). In
level flight the velocity starts horizontal and tips further down every step as gravity builds,
so the bomb visibly hangs flat for a moment and then swings nose-down into its dive — the
alignment is a consequence of the trajectory, not an animation played over it.

Gravity is applied by hand at 200 u/s² (`Bomb.Gravity`) rather than through `Rigidbody.useGravity`,
for the same reason `PlaneFall` does it: real gravity is far too slow at this world scale, where
a plane is 60 units long and flies at 180 u/s. There is no drag, so horizontal speed is kept for
the whole fall. Collision detection is continuous — a bomb at terminal speed covers several
units per step and would otherwise tunnel through thin terrain.

A bomb that never hits anything is removed silently after `MaxLife` (12 s) or once it falls past
`FloorY` (−300), the same idea as `PlaneFall.Timeout` for wrecks: on the campaign scroller a bomb
can be dropped over ground that has not streamed in, and it must not hang in the scene forever.

## What a bomb hits

Bombs sit on **layer 10**, their own layer, and the choice matters:

- Layer 0 (where bullets live) would not work: `BattlefieldProps` calls
  `Physics.IgnoreLayerCollision(9, 0, true)` so shots pass through trees and houses, and the
  layer matrix gates trigger events too — a bomb there would fall straight through a house.
- Layer 8 (`PlaneFactory.PlaneLayer`) would not work either: `PlaneScrapes.DisablePlanePlaneCollisions`
  disables 8-vs-8, so a bomb would pass through the enemy it was aimed at, and 8-vs-11 is toggled
  off for the length of a cutscene (docs/level-intro.md).

The fourth layer in play is **11**, `ProceduralTerrain.GroundLayer`, which carries the terrain
itself. Layer 10 meets terrain, planes and props alike. `PlaneBomber.Initialize` disables 10-vs-10 so two
bombs in the air cannot deflect each other, and each bomb ignores **its own plane's collider**
(`Physics.IgnoreCollision`, exactly as `Bullet.Launch` does), so it can never be batted away by
the aircraft that just dropped it.

Two things a contact must not be mistaken for:

- A **bullet** striking a bomb is ignored by the bomb, mirroring how `CubeController` and
  `EnemyController` already ignore bullets in their crash handlers. Rounds are near-massless and
  cannot move it.
- A **plane** touching a bomb must not read as a crash. Both `CubeController.OnCollisionEnter`
  and `EnemyController.OnCollisionEnter` treat any solid contact as death, so both now skip a
  `Bomb` the same way they already skip a `Bullet`. Without that guard an enemy would be
  vaporised by the bomb *body* before the blast was ever computed, and the falloff would never
  matter.

Props (trees, burned houses) are trigger colliders, so they arrive through `OnTriggerEnter`
instead, gated on `BattlefieldProps.Layer` like the plane scrape handlers. The bomb detonates
against the prop; the prop itself is unharmed, since nothing on the battlefield has a damage
model.

## Detonation

Three outcomes, chosen at the contact point:

- **Ground, terrain or prop** — `Explosion.Spawn` (the fireball blob cluster, see
  `docs/effects.md`) *plus* `GroundBlast.Spawn` (dirt clods and dust, see `docs/battlefield.md`),
  both sized to the blast radius. Blast damage is applied, and any infantry inside the radius is
  wiped out through `Battlefield.KillPeopleWithin`.
- **Enemy plane (airburst)** — the fireball and the blast damage only. No dirt, and **no
  infantry casualties**: `BattlefieldPeople.KillWithin` measures distance in XZ alone, so an
  airburst at altitude would otherwise mow down the squad directly below it.
- **Water** — a `WaterSplash` at sea level and nothing else: no damage, no dirt, matching how
  the ambient shelling behaves over the sea. This needs a live `SeaSurface`, a contact at or
  behind `SeaSurface.NearEdge`, and a point at or below `SeaSurface.Level` — the same
  region-not-height rule `Battlefield.TickBlasts` uses, because the coast's foreground is dry
  land whose crater floors sit below the waterline. With the play plane at `z = 100`, in front
  of the sea's near edge at 170, no bomb can currently reach the water; the branch exists so the
  behaviour is right if the flight lane is ever moved back.

Both spawned effects play their own clip from `Resources/Sounds/explosion_1..3`: the `Explosion`
one at 0.55 flat, the `GroundBlast` one quieter and pitched down to 0.5–0.8. Layering them is
deliberate — the ground detonation gets a low thump under the crack, which is what makes it read
as heavier than a plane blowing up.

### Blast damage

`Physics.OverlapSphere` at the contact point, triggers excluded, and every collider whose parent
carries an `IDamageable` is hit once (the list is de-duplicated, since a plane can answer through
more than one collider). Damage falls off **linearly** from the centre:

```
damage = bombDamage × (1 − distance / bombBlastRadius)
```

measured from the blast to the target's body origin, so a dead-centre hit takes the full value
and anything at the rim takes nothing.

The player's own plane is found by that sweep like anything else — bombing from too low, or
turning back into your own blast, costs real health. Wrecks are not: `TakeDamage` on both
controllers already refuses damage once a plane is falling.

## Feedback

**Camera shake.** Each detonation reports back to its level controller, which raises the existing
`_camShake` (7-unit jitter decaying over 0.3 s, shared with tree and enemy scrapes) by
`1 − distance / (radius × Bomb.ShakeRadii)` — full strength on top of the camera, nothing at three
blast radii out. Distance is measured in XY from `_camBasePos`, i.e. as seen on screen, not in 3D
from the camera itself, which sits 420 units back and would flatten the whole curve. It is raised
with `Mathf.Max`, so a bomb never damps a shake already running.

**HUD.** The bomb owns the first `CooldownSquare` in the HUD's left-hand action column, labelled
`H` on desktop and `BOMB` on touch. Inside it a **radial sweep** fills the cooldown like a clock:
an `Image` in `Filled` / `Radial360` mode, origin **top**, running **clockwise**, its `fillAmount`
driven by `PlaneBomber.Charge`. While the bomb is ready the square is hollow — a white rounded
outline and a white letter, nothing inside. On release the outline fades to a ghost, a faint clock
hand turns inside it, and a grey arc of the **same stroke width as the border** closes round the
perimeter over the 5-second cooldown; both land on full together and snap back to white the moment
it is ready (docs/hud.md). Radial fill needs a sprite (a
null-sprite `Image` draws a plain quad and ignores `fillAmount`), so it uses
`UIFactory.SolidSprite` — a cached 4 × 4 white texture, the same generate-once pattern as the
triangle and ring sprites.

It reads not-ready whenever the bomber is stopped, and whenever the cinematic bars are showing,
so it stays honest during the campaign fly-in and radio lines — though in a cutscene the HUD is
not on screen at all (docs/level-intro.md).

The square is also **pressable**, through `HudPressRelay` on to `PlaneBomber.Request()` — the same
method the `H` key now calls. See docs/hud.md for the column, the colour scheme and the touch
metrics.
