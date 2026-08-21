# Supply drops

A parachuted crate that falls past the player when the plane is badly hurt. Fly into it and it
bursts into splinters and gives health back. It is the only way to regain health in a level.

Files: `SupplyDrop.cs` (the director), `SupplyCrate.cs` (the falling crate),
`CrateBurst.cs` (the debris), `HealFlash.cs` (the green pulse on the plane).

## Per-level tuning (`CampaignDefinition`)

| Field | Default | Meaning |
| --- | --- | --- |
| `supplyDrops` | `0` | How many crates the level may drop. `0` disables the system entirely. |
| `supplyHealthFraction` | `0.3` | Fraction of `MaxHealth` at or below which a crate is sent. |
| `supplyHeal` | `50` | Health restored on catch, clamped to `MaxHealth`. |

Every career level opts in — one crate on levels 1–6, two on 7 and 8. `CampaignLevels.Custom` — the Custom Battle skirmish — leaves it at `0`, so
no crate ever falls there: the drop is a campaign beat, not a general pickup. Changing it for a
level is one line in that level's definition.

The default player has 100 health, so level 1 reads as: one crate, sent the first time the plane
is at or under 30 health, worth 50 back.

## Arming (`SupplyDrop`)

`SupplyDrop` is a component on the level controller, ticked from `CampaignLevelController`'s
`LateUpdate` with the camera's unshaken base position, so camera shake never jitters the crate.
It returns `null` from `Begin` when the level has no drops, and everything downstream is guarded
on that null — a level without supply drops pays nothing.

A crate is sent when all of these hold: drops are left, none is in the air, the level is not in a
cinematic (`IntroActive` or cinematic bars — the player is not fully flying the plane then and
would lose the crate through no fault of their own), the plane is alive, and current health is at
or below the trigger fraction. `StandDown` — called from `StopScript` and `CompleteLevel` —
zeroes the budget and removes a crate still in the air, so nothing keeps falling over the fail or
completion screen.

There is **no HUD announcement**. An earlier version put a green plate up next to the enemy
warning and it was noise: the crate enters at the top of the screen where the player is already
looking, and the banner only competed with it.

## The crate (`SupplyCrate`)

The model is `Assets/Resources/objects/supply_crate.fbx` — one FBX carrying both the `Crate` and
the `Parachute` nodes. It is normalised to 56 units on its longest axis (just under a
60-unit plane) and every collider on it is stripped: nothing about the crate is physical, it
cannot soak a bullet, brush a plane or land on terrain.

### Standing it up

**No stand-up rotation.** `supply_crate.fbx` imports already upright — dome over box — so the
only rotation applied is a 24° yaw about the vertical, so the crate does not read flat against
the side-on camera. This is the opposite of the battlefield props (docs/battlefield.md), which
need `Euler(-90, 0, 0)`; borrowing that constant here first stood the crate on its head and then,
flipped to `Euler(90, 0, 0)`, laid it over to point down the camera's own axis. Both were the
same mistake: the model needs nothing.

The catch point *is* measured: `BoxLocal` finds the `Crate` node and stores its centre in
root-local space, so the crate box — not the canopy, and not the midpoint of the two — is what
the plane has to hit. It falls back to 74% of the model height below the root if the node is not
found.

### Hanging and swinging

The root transform is the **suspension point** and sits at the top of the model, so the whole
crate hangs below it and rolling the root swings the load under the canopy the way a real chute
does. Rotating the root about its own middle would have pivoted the canopy around the box
instead, which reads as a tumbling crate rather than a parachuted one.

- Descent: a flat 80 units/s straight down, from a spawn just above the top of the view.
- Sway: ±22 units at 0.4 Hz, with the root rolled up to 12° **against** the swing so the load
  leans into the direction it is travelling. Kept gentle on purpose — at the original ±30 at
  0.5 Hz the crate slid sideways at up to 94 units/s, fast enough to duck out of the plane's way
  on the approach.
- Anchor: **a fixed world x**, `camera.x + 0.72 × halfViewWidth` captured once at spawn. The
  crate falls; it does not travel.

That anchor is the whole trick, and the obvious alternative is wrong. Holding a fixed *screen*
offset looks correct in isolation but is uncatchable: `PositionCamera` tracks the plane's x, so a
crate pinned to the camera is pinned to the plane, the horizontal gap never closes no matter how
hard the player turns, and it reads as wind blowing the crate away. Pinned to the world instead,
the plane's own ~120 units/s cruise closes the distance for free — the crate drifts left across
the screen like any scrolling pickup and the player only has to solve altitude. The 0.72 lead is
about 2.5 s of flight, so the plane arrives at the crate's x around halfway down its fall, with
the crate still high enough on screen to climb into.

### Catching and missing

The catch test is a plain distance check in XY between the plane and the crate box, inside a
42-unit radius — the plane's own scrape radius (`PlaneScrapes.HitboxRadius`) plus the box, with
some slack, because the plane sweeps past the crate's x in about half a second and that is the
whole window. It is deliberately not a physics trigger: the crate has no colliders, and matching
`PlaneScrapes`' hand-rolled proximity keeps it out of the layer matrix.

- **Caught** — `CrateBurst` at the box, the pickup chime plays, `CubeController.Heal` adds the
  health, and `HealFlash` pulses the plane green.
- **Hit the ground** — `CrateBurst` with no heal and no sound. The ground is found by a downward
  raycast against `ProceduralTerrain.GroundLayer` each frame, so the crate breaks on the actual
  terrain under it rather than at a flat height; over the Flanders sea there is no ground collider
  and it falls back to the level's `AiGroundY`, which is sea level there.
- **Left behind** — once the box is a crate-width past the left edge of the view it is destroyed
  silently. A world-anchored crate can leave the screen before it lands, and bursting where
  nobody can see it would only litter the scene with debris.

Because level 1 has a budget of one, a crate that is not caught is simply gone.

## Healing (`CubeController.Heal`)

`Heal` clamps to `MaxHealth` and, when it lifts the plane back over `SmokeHealthThreshold`,
disarms the damage smoke. `SmokeTrail` grew a `Disarm` for this: the existing `Clear` also sets
`_cleared`, which permanently stops the emitter (it is what the ditching plane wants), whereas a
healed plane has to be able to smoke again the next time it is shot up. `Clear` now delegates to
`Disarm` and keeps its own latch.

The death fire is untouched — it only ignites at zero health, by which point there is nothing
left to heal.

## Effects

**`CrateBurst`** — 12–18 wooden splinters (thin cubes tinted between `(0.28, 0.18, 0.10)` and
`(0.52, 0.36, 0.19)`) thrown out radially, tumbling, pulled down at 150 u/s² with light drag and
shrinking to a third over 0.7–1.4 s; plus 5–8 transparent brown dust cubes that drift out and up,
grow to 2.2× and fade. Shadows are off on every piece and there are no colliders. One root
object owns all of them and destroys itself when the last one expires.

**`HealFlash`** — three pulses over 0.9 s, decaying, on the plane model's mesh renderers. It
writes `_BaseColor`/`_EmissionColor` through a `MaterialPropertyBlock` fetched from the renderer
first, which is what keeps the selected skin texture (docs/plane-skins.md) intact — `PlaneSkins`
puts the texture in that same block. The original `_BaseColor` of each shared material is read
once at the start and restored at the end, so nothing is left tinted. Trail and line renderers
are skipped, so the boost trails do not turn green.

## The pickup sound

There is no wooden or chime sample in `Assets/Resources/Sounds` (docs/sounds.md), so the pickup
tone is synthesised once on first use and cached statically: two notes (D♯5 → A♯5) of a sine plus
a quarter-strength octave, each with a fast attack and an `exp(-9t)` decay, 0.42 s total, played
2D at 0.45 volume. It is built with `AudioClip.Create` in the same spirit as the music engine
(docs/music.md) rather than shipping another wav.
