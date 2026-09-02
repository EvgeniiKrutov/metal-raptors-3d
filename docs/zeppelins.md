# Background zeppelins (`Assets/Scripts/SkyZeppelin.cs`)

An airship drifting far behind the fight, purely to make the sky look inhabited. It has
no collider, casts no shadow, never damages anything and can never be hit — it is
scenery that happens to move.

## Where it runs

`CampaignLevelController.Start` calls
`SkyZeppelin.Begin(cam, halfViewWidth, halfViewHeight, playPlaneZ, cameraDistance, wanted)`
right after `SkyFlak.Begin`. `wanted` is `CampaignDefinition.zeppelins`, true on campaign
**levels 1, 2 and 3** and on a **custom battle flown over Verdun** (`CampaignLevels.Custom`
reads it off `map.Terrain`) — the airship is a Verdun-sector fixture, so Flanders and the
Dolomites get none. `Begin` returns null when the flag is off, so a level with no airships
builds no GameObject at all.

The arena's `LevelController` does not use it.

## The model

`Resources/objects/machines/zeppelin` — one hull mesh plus four separate propeller meshes
named `front_prop_1`, `front_prop_2`, `back_prop_1`, `back_prop_2`, each hanging off an
`outrigger_*_prop` node under its gondola. It ships with its own material and the
`zeppelin_texture.png` in `Assets/Textures`, so nothing is recoloured or re-textured in code.

Like every model in this project it is authored **Z-up with the nose along −Y**, so the
instantiated prefab root is turned by `NoseWest` — `Euler(-90, -90, 0)` — which puts its nose
on **−X** and its top on **+Y**. That is the same rotation the planes get from `standUpEuler`
+ `rollWheelsDown` mirrored end for end, written directly rather than as a two-step
composition because the airship has no pitch trim.

`Fit` then measures the model's world AABB, scales it so its X extent is the wanted length,
and shifts it so that box centres on the root's origin — the root is what the drift moves and
what the off-screen tests measure, so an off-centre pivot would make the margins lie.

## Depth and size

It sits **behind the companion duel**: `playPlaneZ + CompanionFlight.Depth` (100 + 250 = 350)
plus a random 50–120, so Z lands in 400–470. The atmosphere caps how much deeper it can go:
`ProceduralTerrain.FogEndDistance` puts full haze at 870 from the eye, the camera sits at
z −320, and the airship is 720–790 out — 59–78 % of the way into the haze at morning light.
Past about z 520 the aerial haze erases it completely, so depth cannot be bought beyond that
without reworking the level's fog.

Size is authored **as it appears**, not as a world length: `ApparentLength` (560) is in
play-plane units, multiplied by the depth grade `(z − eyeZ) / cameraDistance` (≈1.7–1.9) and
jittered ±12 %. So however deep the random draw puts it, it always covers about two thirds of
the view's width — roughly twelve player-plane lengths. Depth therefore buys haze and parallax,
never a smaller silhouette. The spawn altitude uses the same grade: 35–70 % of the half view
height above the camera's centre, which keeps it in the top of the frame, well clear of the
play space.

## One at a time, one per chunk

`Begin` spawns the first one immediately, so a level opens with an airship already in the
sky rather than waiting for one to sail in. That first spawn is the only one placed **inside**
the window — `OnScreenMin`/`OnScreenMax`, −30 % to +60 % of the half window off the camera's
centre — so it reads as having been there all along.

After that `Consider` replaces one the moment it can, gated on only two things:

- none is alive — there is never more than one airship in the scene, and
- the camera is in a different terrain chunk (`CampaignTerrain.ChunkLength`, 512) than the one
  the last spawn happened in, which is the original "no more than one per open chunk" rule.

There is no waiting period and no dice roll. Neither is needed: an airship lives long enough to
cross several chunks, so by the time one dies the chunk gate is always already open, and the
only gap left is the couple of seconds the replacement spends closing on the right edge. The
variety comes from the per-airship draws — depth, size, altitude, speed — not from spawn
timing.

## Drift and death

It moves along X only, always **westbound**, at 10–20 units/s — an idle drift next to the
player's ~200, so nearly all of the crossing is parallax rather than the airship's own motion.
Every spawn after the first one is placed just past the **right** edge of its own window
(`_halfWindow`, the half view width at its depth), heading into the oncoming camera.

Spawn and death both use the same `HideMargin` (0.6 lengths past the window edge) — barely
more than the half length it takes to be out of sight. Anything larger is time the airship
spends alive but invisible, which at this size is several seconds of empty sky at each end.
The left edge is the only exit test needed: the campaign camera's X never decreases and the
airship's never increases, so the gap between them only ever closes.

## Propellers

`StartPropellers` puts a `PropellerSpin` on each of the four `outrigger_*_prop` pivot nodes —
the empty parents the artist hung the `front_prop_1/2` and `back_prop_1/2` blade meshes off —
falling back to the blade mesh itself if a pivot is missing. That is the same
pivot-before-blades order `PlaneFactory.StartPropeller` uses on the aircraft. One shared speed,
380–520 °/s, is drawn per airship; `PropellerSpin` finds each disc's hub from the blade mesh
bounds, so it turns in place however far the node's own origin sits from it.

The axis is handed in as `axisSpace = root`, `axisInSpace = right` — **world X**. The airship
only ever flies along X and is never pitched or rolled, so its hull axis *is* the world X axis;
expressing the spin that way keeps the propellers correct without depending on how the FBX's
own axes survived import.
