# The aerodrome (`Aerodrome.cs`, `AerodromeRoad.cs`)

Level 3, `FIXED GROUND`, opens over its own airfield: the squadron's home field fills the left
end of the map, the shelled Verdun ground starts where the field ends, and a road leaves the
field at the player's own depth and runs off the right-hand edge. Nothing else in the campaign
places a single fixed building yet — every other structure is streamed from
`BattlefieldProps` (docs/battlefield.md) — so this is a one-off placed object, not a prop grid.

## The model

`Assets/Resources/objects/aerodrome_stow_maries.fbx` (untracked, like everything under
`objects/`). One `Aerodrome` root null over eight groups: `Ground` (apron tracks), `Hangars`
(three), `Quarters` (a barrack and two huts), `Sheds` (three), `Tower` (a water tower),
`Aircraft` (three parked Sopwith Camels), `Windsock` and `Fence` (the perimeter, which is what
sets the overall footprint).

It is authored in **real metres, Z-up**, the same Blender pipeline every plane comes from
(docs/plane-scale.md) — but unlike the plane and prop models its root null already carries the
−90° X rotation, so it imports Y-up and needs no `StandUp` correction of its own. The one
rotation `Aerodrome` applies is `FieldYaw`, a flat **−90° about Y**; any pitch or roll on top of
the baked stand-up swaps the model's depth and height axes and lays the whole field up as a wall
(the 76 m depth becomes 591 units of world Y, the 14.6 m height 113 units of world Z), which is
how it was placed until this was fixed.

Imported, the Blender-authored axes land as `Blender +X → world −X`, `+Y → world −Z`,
`+Z → world +Y`, and `FieldYaw` then turns the field a quarter left, so the model's **long axis
runs into the scene** rather than across it: `model +X → world +Z`, `model +Z → world −X`.

| | model metres |
| --- | --- |
| width (X) | 141.63 |
| depth (Y → world Z) | 76.13 |
| height (Z → world Y) | 14.60 |
| parked Camel wingspan | 8.500 |

## Why the Camels set the scale

The scenery conversion (`BattlefieldProps.MetreScale`, 7.2 units/metre plus a 1.5 oversize) is
tuned for trees and houses seen at the camera's standoff and would make the airfield's own
aeroplanes the wrong size next to the player's. So the aerodrome is sized **off the machines
standing on it**: `Aerodrome.Measure` finds the `Camel_01` node, measures its wingspan in that
node's *own* local space (the parked machines are yawed, so a world-space AABB would read long),
and scales the whole model by

```
Scale = PlaneModels.Sopwith.OnScreenSize / that span
```

`OnScreenSize` is `wingspanMeters × UnitsPerMeter` = 66 units, and the model's Camels are
exactly 8.5 m across, so the scale lands on `PlaneModelConfig.UnitsPerMeter` (≈ 7.765) — the
plane conversion, reached from the other end. It is measured rather than written down so that a
re-export with differently sized aeroplanes still parks machines the size of the one the player
is flying.

| | world units |
| --- | --- |
| `Aerodrome.Width` (the yawed X footprint) | ≈ 591 |
| `Aerodrome.LandDepth` (the yawed Z footprint) | ≈ 1100 |
| `Aerodrome.Height` | ≈ 113 |

Both the measurement and the whole-model bounds come from mesh bounds transformed into the
measured space rather than from `Renderer.bounds`, which is a world-space AABB and would fold
the probe's own placement into the answer.

## Where it sits

`Aerodrome.Place(leftX, nearZ, groundY)` positions the instance so its **scaled bounds minimum**
lands on that point — the bounds being those of the *yawed* model, so the same call frames the
field whichever way `FieldYaw` turns it. The level places it at `x = 0`, `z = 0`,
`y = ProceduralTerrain.BaseLevel`: hard against the map's left wall, its front fence on the lip
of the terrain's cut wall, and its ground plane exactly on the flattened apron.

With the quarter turn the field is read end-on: the **short end carrying the water tower is the
near one** and the windsock ends up on the far edge of the strip. Where the pieces land, in
world units from the field's corner at the origin:

| | x | z |
| --- | --- | --- |
| water tower (113 tall) | 89 … 129 | 74 … 114 |
| hangar row | 27 … 223 | 267 … 1023 |
| parked Camels | 249 … 391 | 588 … 1023 |
| windsock | 380 … 415 | 1030 … 1070 |

Two things follow from it. The tower straddles `PlayPlaneZ` (100) at the very start of the run
and stands to y ≈ 143 against a `SpawnY` of 150, so the player flies through it rather than past
it — it is on `BattlefieldProps.Layer` and has no collider, so this costs nothing but the look.
And the hangars and parked machines now sit 600–1000 units deep instead of ~300, which reads
smaller and hazier; `FogEndDistance` still closes the haze 60 units short of the back edge.

It is decoration only. Every collider that comes in with the model is destroyed and the whole
tree is put on `BattlefieldProps.Layer`, so the plane flies through it the way it flies through
trees; only the terrain can be crashed into (`CubeController.GroundUnder` raycasts the ground
layer alone).

## What the land does about it

The aerodrome drives three terrain decisions, passed to `CampaignTerrain.Begin` as a
`CampaignLandOptions`:

- **`depth` = `Aerodrome.LandDepth + 50`** (≈ 1150 against the usual 800). The strip of land is
  cut to the field plus fifty units behind it, so the airfield fills the map's depth instead of
  sitting in the front third of it. `ProceduralTerrain.FogEndDistance` takes the depth as an
  argument and closes the haze before the land's back edge — `Mathf.Min` of the usual distance
  and `edge − 60`, so a full-depth map is unchanged and a shallow one is still hidden.
- **`apronUntilX` = `Aerodrome.Width`** (≈ 591 since the yaw, so the bare apron is the map's
  left quarter rather than its left half). `VerdunTerrain` flattens every height to
  `ProceduralTerrain.BaseLevel` left of that line and blends back into the rolling ridge over
  160 units (`ApronBlend`); no crater is generated whose influence would reach into it
  (`ReachesApron`), and no grass is planted on it. The airfield is bare, level dirt and the ruts,
  shell holes and grass all start on its right.
- **`roadZ` / `roadHalfWidth`**. Grass is skipped inside the road corridor for the whole length of
  the map, so tufts do not grow through the ribbon.

All three are pure functions of world position, so chunk seams still agree (docs/campaign.md,
"Why chunks are seamless without stitching").

## The road

`AerodromeRoad.Build(land, x0, x1, z)` drapes a three-column ribbon — left verge, crowned
centre, right verge — over the terrain, sampling `CampaignTerrain.SampleHeight` every 14 units
and lifting the surface 1.6 units (plus a 1.4 crown) clear of it. It is 9 m wide at the plane
conversion (≈ 70 units), runs at `PlayPlaneZ`, starts 60 units inside the airfield's boundary so
it reads as leaving the field, and overruns the map's right wall by 500 units so its end is
never in frame. Flat colour, no collider, casts no shadow.

The player's depth is inside `ProceduralTerrain.FrontStrip`, where the ground is constant in Z,
so the ribbon is level across its width except where a crater reaches forward into the strip —
and there it simply follows the hole down.

## Files

| File | Role |
| --- | --- |
| `Aerodrome.cs` | Loads and measures the model, derives the scale from the parked Camels, places it. |
| `AerodromeRoad.cs` | The draped road ribbon and its material. |
| `CampaignTerrain.cs` | `CampaignLandOptions` — depth, map width, apron line, road corridor. |
| `VerdunTerrain.cs` | The apron flattening, crater exclusion and grass exclusion. |
| `CampaignLevelController.cs` | `BuildLand` / `BuildAerodrome` — where the numbers above are chosen. |
