# Flanders Coast — populating the map (plan)

Flanders currently carries **no placed objects at all**: `Battlefield.BeginCoast` sets
`_placeProps = _placePeople = false`, so the map is heightmap, sea, blasts and smoke columns
and nothing else (docs/flanders-coast.md, docs/battlefield.md). This is the plan for filling
it to roughly Verdun's density.

Nothing here is implemented yet.

## The budget to match

Verdun's visible X span is about **700 units** (trees at a 58-unit cell give ~12 on screen).
Against that span it carries:

| | Cell | On screen |
| --- | --- | --- |
| Trees | 58 | ~12 |
| Houses | 620 | ~1 |
| Tank wrecks | 400 | 1–2 |
| Smoke columns | 600 (75 %) | 2–3 |
| People | — | several squads |

So the target for Flanders is **~15–18 solid props on screen**, plus the smoke and blasts it
already has, plus ships.

## The three bands

The map's Z structure (docs/flanders-coast.md) splits the population into three problems that
do not share code well, because one is on ground, one is shore-relative and one is on water.

```
z 0–170     dry Yser plain          ground props, people          ← most of the count
z ≈ 210–345 the sand                shore-relative, everything low
z 380–1180  the North Sea           moving ships, camera-streamed
```

### 1. The plain (z 20–170)

The band the plane actually flies over, and the only one where Verdun's existing machinery
transfers almost unchanged — `BattlefieldProps` already streams deterministic per-cell grids
here.

| Kind | Cell | On screen | Source |
| --- | --- | --- | --- |
| Wind-bent coastal trees | 110 | ~6 | reuse `bent_tree_01..04` only |
| Burned cottages | 620 | ~1 | reuse `burned_houses/*` |
| Sandbag breastworks | 220 | ~3 | **new**, chained so they read as a line |
| Wrecked barge / bogged tank | 700 | ~1 | **new** barge + reuse `tank_ww1` |
| Broken windmill | 900 | ~0.8 | **new** |

Two notes on reuse. Only the `bent_tree_*` set belongs here — a coastal treeline is wind-shaped
and leans inland, and the straight `dead_tree_*` silhouettes read as inland forest. And the
tank is worth keeping: a Mark IV bogged to the sponsons in wet sand at the dune line is a better
image here than it is on Verdun, and it costs nothing.

The **sandbag breastwork** is the one that gives this band its identity. You cannot dig a trench
in Flanders — the water table is a foot down — so the line was built *upward*, out of sandbags,
above ground. That is the opposite of Verdun's dug-in look and it is the single most
map-specific thing on the plain. Author it as 2–3 short segments that chain end to end plus a
dugout mouth, so a cell can lay a run of them along X rather than dropping one lonely block.

People should come back on (`_placePeople = true`) but capped to this band — `z` 40–170, so
squads stay on the plain and never wander onto the sand or into the sea. `BattlefieldPeople`
already takes a Z ceiling via `Battlefield.PeopleZMax`; it needs a floor and a ceiling here.

### 2. The sand

This is the open question, and it needs three answers, only one of which is props.

The sand band is **250 units deep** (`SandInland`) and deliberately so — the doc's argument is
that beach area is bought with the alphamap rather than by flattening the profile. But it is
currently the worst surface on the map: flat colour with **no grain at all** (`FlatLayer`), and
**micro relief faded to zero** across it (`MicroOnBeach = 0`). It is the brightest thing in
frame and it has nothing on it. Props alone will not fix a 250-unit sheet of blank white.

**a. Groynes — the structure.** Rows of timber piles running **along Z**, from up the beach out
past the waterline into the water, at a ~200-unit cell (~3–4 on screen). They are the right
answer three times over:

- they run into the screen, so their perspective convergence sells the depth of the beach the
  same way the embankments sell the depth of the plain;
- at the low sun angles three of the four daytimes use, a row of posts throws **long dark
  stripes across the sand**, which is what actually breaks up a flat bright sheet;
- they **cross the waterline**, so they interrupt the sand/sea junction the map works hard to
  keep crisp, and give the eye a vertical reference where that junction is.

They are also genuinely the Belgian coast's signature — the whole shore from Nieuwpoort north is
groynes every few hundred metres.

**b. A tide line — no model needed.** A third terrain layer, wet sand, darker and more saturated
than dry, keyed to distance from the local shore centre: full wet from the waterline to about
30 units up the beach, fading out by 60. Then a scatter of small debris along the high-water
mark — driftwood, a broken spar, washed-up crates (`supply_crate` is already in the project).
A beach with a wrack line reads as a beach; a beach without one reads as a desert.

**c. Broken dunes — heightmap, not props.** Low hummocks along X at the **back** of the sand,
between `centre − 140` and `centre − 70`, discrete with wide gaps rather than a continuous
crest. The "no dune crest" rule in docs/flanders-coast.md exists because a crest would
obstruct the sea; broken hummocks with gaps do not. They must stay **inland of the slope** —
`MicroOnBeach = 0` protects the waterline crossing, and that protection should not be undone.

**d. What else stands on the sand,** all of it low:

| Kind | Cell | On screen |
| --- | --- | --- |
| Beach stakes / wire belt | 350 | ~2 |
| Beached hulk, half-buried | 900 | ~0.8 |
| Concrete pillbox, settled and tilted | 800 | ~0.9 |
| Coastal gun in its emplacement | 1400 | ~0.5 |

The hard rule for this band: **nothing tall on the sand**. Everything on the beach sits between
the camera and the sea, and the sea is the reason this map exists. The beached hulk is the one
allowed exception, and it earns it by straddling the waterline.

### 3. The sea (z 380–1180)

Two layers, and neither uses `BattlefieldProps` — these move, so they stream like `SkyZeppelin`
does rather than sitting on a deterministic ground grid.

**Near ships, z 420–780.** Two or three live at a time, crossing the frame along X. The right
ship is a **monitor** — a shallow-draft shore-bombardment ship, all turret and no freeboard,
which is exactly what the Royal Navy had off this coast — plus the occasional narrow
multi-funnel destroyer for contrast and speed.

Make them **fire inland**, not only at each other. A monitor's muzzle flash followed a second or
two later by a `GroundBlast` on the plain gives the map's existing random shelling an on-screen
cause, which is a much better scene than two systems running independently. Ship-to-ship
exchanges (flash, then a `WaterSplash` short of the target) can run alongside it.

They need a funnel plume — `SmokeColumn.Begin` already takes a `scale`, and the puffs drift
downwind on +X anyway, so a plume trailing a ship is close to free.

**Far ships, z 900–1150.** One or two capital-ship silhouettes drifting slowly in the haze,
just inside the 1180 fog close-out. These can be low detail by construction — at that depth
they are 80–95 % fogged and what reads is the hull line, the funnels and the smoke. Night pulls
the fog in to `z ≈ 930` (the mist bank), so on the night palette they want to sit nearer, around
850–950, or they vanish entirely.

**Small floating detail, z 380–520.** Buoys, a horned mine, a half-sunk wreck with the sea
cutting through it. The half-sunk wreck is the valuable one: it sits on the water plane and
gives the near sea a fixed object to move past, which is the only thing that makes the swell
legible as motion.

## New models to author

Authored in metres, **Z-up** with the −90° X root rotation, like every other model here
(docs/battlefield.md, *Standing them up*). Priority order:

| | Model | Notes |
| --- | --- | --- |
| 1 | `groyne_01..03` | pile rows ~8 m long; intact, rotted, and one with a capping beam |
| 2 | `monitor` | squat, one big forward turret, minimal freeboard |
| 3 | `beached_hulk_01, _02` | small coastal steamer / barge, listing, plating gone |
| 4 | `sandbag_work_01..03` | above-ground breastwork segments that chain + a dugout mouth |
| 5 | `pillbox_01, _02` | concrete blockhouse, settled and tilted in sand |
| 6 | `beach_stakes_01, _02` | angled anti-landing timber with wire |
| 7 | `destroyer` | narrow hull, 3–4 funnels |
| 8 | `dreadnought` | far-haze silhouette, low detail on purpose |
| 9 | `coastal_gun` | naval gun on a concrete emplacement, laid seaward |
| 10 | `barge` | canal lighter, wrecked on the plain |
| 11 | `windmill_burned` | Flanders post mill, broken sails |
| 12 | `buoy`, `naval_mine` | tiny, near-water |

Free reuse: `bent_tree_01..04`, `burned_houses/*`, `machines/tank_ww1`, `supply_crate`.

That is 12 new models against Verdun's 19, and roughly the same authoring weight.

## What the code needs

- **`FlandersTerrain.ShoreCentre` must go public and static.** The shore meanders (`300 ± 45`,
  driven by long-wavelength X noise), so groynes, the wire belt, the tide line and the pillboxes
  all have to be placed relative to the local shore centre sampled at their own X. Placing them
  at a fixed `z = 300` would leave half of them in the water and half stranded inland.
- **`SeaSurface.WaveAt` must go public**, along with the swell ramp, so ships ride the surface
  instead of floating on the flat `Level`.
- **`BattlefieldProps` needs per-kind Z bands.** It currently hardcodes one band plus a tank
  special case; the coast wants a small table, and a shore-relative mode for the beach kinds.
- **Ships get no collider and no damage**, exactly like `SkyZeppelin` — scenery that moves.
  They must also be floored at `z ≥ 380` so one can never drift onto the shelf and beach itself.
- **`BeginCoast` stops forcing `_placeProps = _placePeople = false`** and instead passes the
  coast's bands.
