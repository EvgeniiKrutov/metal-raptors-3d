# Sky flak (`SkyFlak.cs`, `FlakBurst.cs`)

Anti-aircraft shells bursting in the air around the camera — the WW1 "archie" the
aircraft fly through. Built in code at runtime like every other effect: no prefabs,
no colliders, no rigidbodies. It is **purely cosmetic** — a burst can never damage,
push, block or deflect anything, and it soaks no bullets.

Deliberately *not* a flash effect. A shell goes off with a spark that is gone in a
tenth of a second, and what is left is a dirty grey-brown puff that blooms, settles
slowly downward, drifts downwind and disperses over several seconds. Several old
puffs at different depths hang in the sky at once.

## Two pieces

- `SkyFlak` — the scheduler. One per level, driven from its own `LateUpdate`. It
  decides *when* a salvo happens and *where* each burst goes.
- `FlakBurst` — one burst. Self-destructing root that owns its own timeline, the
  same shape as `Explosion` and `GroundBlast`.

## Starting it

Both level controllers start it in `Start`, straight after the battlefield:

```
SkyFlak.Begin(cam, playerTransform, halfViewWidth, halfViewHeight, PlayPlaneZ, intensity)
```

It needs no terrain, so unlike `Battlefield` it runs on every map, flat slab included.
The player transform is the aiming reference (see *Altitude* below) and is allowed to
be null — flak then simply scatters across the view.

### Per-level intensity

`LevelDefinition.flak` (arena) and `CampaignDefinition.flak` (campaign) are a plain
multiplier, **1 by default so every level is shelled**. It divides both the opening
delay and the gap between salvos, so `2` is twice as busy and `0.5` half. `0` (or
less) returns no `SkyFlak` at all — that is how a level opts out.

## Timing

| Stage | Value |
| --- | --- |
| Opening quiet | 6–16 s, randomised, before the first salvo of a level |
| Salvo | 3–5 bursts |
| Stagger inside a salvo | 0.12–0.55 s between bursts |
| Gap between salvos | 7–15 s, randomised |

The opening delay and the gap are both divided by `flak`. The stagger is not: it is
what makes a salvo read as several guns firing on the same order rather than one
simultaneous pop, and it should stay tight whatever the level's intensity is.

The whole thing is a two-state timer rather than a coroutine, so `Time.timeScale = 0`
(pause menu, fail screen) freezes it mid-salvo along with everything else.

## Placement

Bursts are **scattered**, not clustered: each one picks its own spot independently, so
a salvo reads as a whole battery firing rather than one gun ranging in.

### Depth

`z` is random in **−60 to 480**, against a play plane at `z = 100` and a camera at
`z = −320`. The near end is *in front of* the aircraft — a foreground puff drifting
across the fight is deliberate, and is most of what sells the depth of the sky.

Because a burst can be anywhere from 260 to 800 units from the camera, screen coverage
is a function of depth, and the horizontal and vertical spread are scaled by
`(z − camZ) / CameraDistance` so a burst lands inside the view at any depth. Size is
*not* corrected — perspective shrinking the far ones and swelling the near ones is the
whole point.

### Altitude

Two thirds of a burst's `y` is aimed and one third is loose:

- **Aimed** (60 %) — the player's own altitude ±40 % of the view half-height, so the
  gunners are visibly ranging at the aircraft.
- **Loose** (40 %) — anywhere across the visible height around the camera.

Whatever comes out is then lifted to at least `GroundClearance` (45 units) above the
terrain, asked through `Battlefield.Current.SampleGround`, so a burst never detonates
buried in a hill. Where there is no `Battlefield` (or no terrain streamed in yet) the
lift is simply skipped.

### The keep-out

A burst inside the play plane's own depth slab (`|z − 100| < 90`) is refused if it
lands within 110 units of the player in XY, and re-rolled up to four times. Without
it a burst occasionally goes off exactly on the aircraft, which reads as the player
being hit — and nothing here damages the player, so that reading is a lie. Depths
outside the slab are never tested: a burst far in front of or behind the plane may
overlap it on screen, which is exactly the parallax the near band exists for.

## One burst (`FlakBurst`)

`FlakBurst.Spawn(position, size, listener, sound)`. Size is 40–80 world units — roughly
a plane's length, matching the 45–90 of the ground blasts.

- **Core** — a single emissive sphere at `size × 0.16`, hot `(1, 0.85, 0.5)`, alive
  `0.1 s`, shrinking to 35 % as its emission fades. Much smaller than the puff and
  swallowed by it immediately: it says a shell went off without becoming the flash
  effect the smoke is supposed to replace.
- **Smoke** — 5–7 `BlobMesh` puffs (the same faceted shape the clouds, `Explosion` and
  the blast clods use, from a static pool of six pre-built meshes) scattered inside
  `size × 0.16` of the centre, each squashed by a random non-uniform shape factor so
  the cluster is irregular rather than a ball of spheres.

### Colour

Each burst picks one tone between sooty grey `(0.26, 0.25, 0.24)` and muddy brown
`(0.34, 0.28, 0.20)`, and every puff jitters ±0.03 around it — so bursts differ from
each other but a single burst stays one colour. Over its life a puff lightens by
`+0.16` on every channel: thinning smoke picks up light rather than staying a dark
blot to the end.

### Motion

The three motions run at once and are what separate this from a puff of steam:

- **Bloom** — each puff is launched outward from the burst centre at
  `size × 0.8 u/s` (±40 %), damped by `exp(−3.2 t)`. Fast expansion in the first half
  second, then it all but stops. This is the detonation.
- **Sink** — a shared 3.5–7 u/s downward, ±15 % per puff, ramped in over the first
  1.2 s so the burst blooms *before* it starts to settle rather than dropping out of
  the sky from frame one.
- **Drift** — a shared `+X` at 7 u/s (±3, plus a little Z), which is `SmokeColumn`'s
  own `WindX`, so the sky and the burning ground agree about which way the wind blows.

On top of that each puff grows from `size × 0.14` to `size × 0.42–0.68` on an ease-out
curve — fast while the bloom is fast — and tumbles slowly (3–14 °/s) on its own axis.

### Life and fade

6–10 s per burst, ±12 % per puff so the cluster does not vanish as one object. Alpha
is `0.62`, faded in over the first 6 % of life (so nothing pops into existence at full
strength), held to 55 %, then squared down to nothing across the tail.

Puffs are transparent URP/Lit, shadows and shadow receiving off. Each puff needs its
**own** material because each is at a different point in its own alpha ramp;
`OnDestroy` releases them all. The root removes itself once its last puff is done.

### Sound

Faint by design, and often silent. **One burst per salvo makes a sound, two 40 % of
the time** — chosen with a running `soundsLeft / burstsLeft` draw as the salvo fires,
so the pick is spread evenly across it. Five overlapping bangs was the thing to avoid;
a soft crump or double-crump is what an occasional salvo should be.

The chosen burst still has to clear the distance test: volume is
`0.16 × (1 − t)` with `t` ramping camera distance from 250 to 950 units, and anything
under 0.025 is not played at all — so deep bursts are purely visual whether or not
they were picked. Pitch is `0.7–0.95`, higher than `GroundBlast`'s 0.5–0.8 boom, so the
shared `Resources/Sounds/explosion_1..3` clips read as a sharp airburst crack rather
than a distant artillery thud. Playback is 2D from a throwaway carrier, as everywhere
else in the game: 3D rolloff would mute it at the camera's 420-unit standoff, and the
manual curve above replaces it.
