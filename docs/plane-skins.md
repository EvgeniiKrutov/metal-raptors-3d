# Plane skins

A **skin** is an alternate base texture for a plane model. The Sopwith Camel has two —
`green` and `blue` — and the player picks one in the garage (`docs/garage.md`); the Albatros
D.III has one, `plywood`, which it simply always wears. The pick is per plane, persists
across launches, and is worn by the **player's** plane everywhere: the garage preview, the
main menu's flying plane, challenge levels and campaign levels.

**Enemies wear the plane's default skin** — `PlaneSkins.Default(group.plane)`, i.e. `skins[0]`,
passed at the three mirrored build sites (`LevelController`, `CampaignEnemies`, and
`DuelPlane` when it is spawning the background foe rather than the wingman). A plane with no
skins resolves to `null`, so the Fokker is untouched and keeps the texture inside its FBX;
the Albatros gets `plywood`, which it otherwise flew unpainted.

It is the **default**, never the player's pick: enemies read `PlaneSkins`, not `GameManager`,
so repainting your own plane cannot repaint the other side.

The **companion wingman is still left out** and keeps the texture that ships inside its FBX
(`DuelPlane` passes a skin only when `mirrored`).

The cost is that the player's Albatros and an enemy Albatros now wear the same paint. They
stay apart by the mirrored, opposite-pitched build `PlaneFactory` gives an enemy, and by
which way each is flying — the skin is no longer doing that work.

## The registry

`PlaneSkin.cs` holds both halves:

* `PlaneSkin` — one skin: an `id` (what is written to disk), a `label` (what the selector
  shows), and `texture` (the `Resources` path).
* `PlaneSkins` — the table plus every query the UI needs (`Of`, `Selectable`, `Default`,
  `ById`, `IndexOf`, `Labels`) and the one that does the work (`Apply`).

A plane points at its skins through `PlaneModelConfig.skins`, the same way it points at its
stats and its model:

| plane | skins |
| --- | --- |
| Sopwith Camel | `green`, `blue` |
| Fokker Dr.I | none (`skins` left null) |
| Albatros D.III | `plywood` |

**The first entry is the default.** `PlaneSkins.Default` returns `skins[0]`, so the Sopwith
is green until the player says otherwise, and a plane with no skins resolves to `null` —
which `Apply` treats as "leave the model alone".

`Selectable` is `skins.Length > 1`, not `> 0`: one skin is not a choice, so the garage hides
the row rather than showing a selector that cannot move. The Albatros is exactly that case —
it is painted `plywood` because that is its only entry, and no `colour` row appears.

## Where the textures live

`Assets/Textures/Resources/skins/<plane>/<skin>.png`, loaded as
`Resources.Load<Texture2D>("skins/sopwith_camel/green")` or
`Resources.Load<Texture2D>("skins/albatros_d3/plywood")`.

The `Resources` folder in the middle is the whole point — a texture cannot be loaded by name
at runtime from anywhere else, and `Assets/Textures` on its own is not a resources root.
Unity treats **any** folder named `Resources` under `Assets` as one, which is the same trick
`Assets/Fonts/Resources` uses (see `docs/standalone-builds.md`). The `skins/` level keeps the
path from colliding with the plane models, which own the bare name `sopwith_camel` under
`Assets/Resources/objects/planes/world_war_1`.

Both `Assets/Textures` and `Assets/Resources/objects` are gitignored, so the PNGs are not in
the repo — same as the FBX models they paint (docs/conventions.md).

## How a skin is applied

`PlaneSkins.Apply` walks the model's renderers and pushes the texture through a
`MaterialPropertyBlock`, setting **both** `_BaseMap` (URP Lit) and `_MainTex` (anything
built-in). A property the shader does not declare is ignored, so setting both costs nothing
and means the swap survives a change of shader on the model.

A property block rather than a material swap, because the skin is changed **live** in the
garage, once per press of the arrow:

* `renderer.material` would instantiate a fresh `Material` per renderer per press and leak
  every previous one — the garage would accumulate them for as long as the player fiddles.
* Editing `renderer.sharedMaterial` would write into the FBX's imported material asset and
  repaint every plane in the scene, enemies included.
* A property block is per-renderer override state. Setting it again just overwrites, so
  switching colours is free and repeatable, and destroying the model takes the override with
  it.

The cost is that a skinned renderer drops out of the SRP Batcher. That is one plane's worth
of renderers, which is why it does not matter here.

`Apply` is called from two places:

* `PlaneFactory.BuildPlaneModel`, through its optional `skin` argument — this is how a plane
  is born already painted, so no frame ever shows the default texture first.
* `GaragePlaneView.SetSkin`, on the model that is already standing there — repainting does
  **not** rebuild the body, so the parked pose, the solved resting pitch, the ground plane
  and any drag in progress all survive a colour change.

## Storage

`GameManager` writes one `PlayerPrefs` key **per plane**, `mr_plane_skin_<resourceName>` —
so `mr_plane_skin_sopwith_camel` holds `green` or `blue`. A second plane with skins gets its
own key for free and the two never overwrite each other.

The key stores the skin's `id`, not its index, so reordering `PlaneSkins.SopwithCamel` or
inserting a skin in the middle cannot silently repaint a player's plane. `SkinFor` reads it
back through `ById`, which returns `null` for an id that no longer exists, and falls through
to `Default` — an unknown or missing value lands on green rather than throwing.

`GameManager.SkinFor(plane)` / `SetSkin(plane, skin)` are the read and write sides;
`GameManager.CurrentSkin` is the shorthand for "the selected plane's skin", and it is what
the three player build sites pass.

## Adding a skin

1. Drop the PNG in `Assets/Textures/Resources/skins/<plane>/`.
2. Add a `PlaneSkin` to that plane's array in `PlaneSkins`.
3. Nothing else. The garage's selector reads its values from `PlaneSkins.Labels`, so the new
   entry appears on its own, and a plane that had none becomes `Selectable` as soon as it has
   two.

## Files

| File | Role |
| --- | --- |
| `PlaneSkin.cs` | The skin record, the per-plane table, and `Apply`. |
| `PlaneModelConfig.cs` | `skins` hangs off the plane, next to its model and stats. |
| `GameManager.cs` | Per-plane persistence and `CurrentSkin`. |
| `PlaneFactory.cs` | Paints a model at build time through the optional `skin` argument. |
| `GaragePlaneView.cs` | `SetSkin` repaints the parked plane in place. |
| `GarageController.cs` | The `colour` selector row. |
