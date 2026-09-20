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

## The relay

`Begin` spawns the first one immediately, so a level opens with an airship already in the
sky rather than waiting for one to sail in. That first spawn is the only one placed **inside**
the window — `OnScreenMin`/`OnScreenMax`, −30 % to +60 % of the half window off the camera's
centre — so it reads as having been there all along, and it is the only one the left edge can
push right, since an airship drawn past that edge would be handed over the frame it appeared.

After that, `Consider` sends the **next** one in off the right as soon as the newest airship
alive is `Handover` (0.7) of its own length past the map's left edge. An airship spans its
`length` about its own X, so that is its centre reaching `edge − 0.2 × length`. The map's left
edge is set by `SetLeftEdge`, which `CampaignLevelController` calls with `WorldLeft` on a
bounded level; unset, each airship measures against the left edge of its own window instead, so
a scroller behaves the same way without knowing where its map ends.

This replaced a rule that allowed **one airship at a time**, gated on the camera entering a new
512-unit terrain chunk (2026-09-20). On the endless levels that produced a steady relay because
the camera never stopped moving, but level 3's camera is clamped to a 1138-unit span — less than
three chunks, and never in a new one for long — so the sky kept one airship and then, once it
died, went empty.

`SkyZeppelin` therefore holds a **list** of airships rather than one, each with its own speed,
length and window; the handover deliberately overlaps, so the arriving one is already on the
right while the departing one finishes leaving on the left. Two is the practical maximum: a
third would need the first to still be alive after the second had crossed the whole map.

If the list ever empties — the camera sitting at the right of a bounded map kills an airship
off-screen before it reaches the handover line — the next one goes in at once, so the sky is
never empty for a frame.

## Drift and death

It moves along X only, always **westbound**, at 10–20 units/s — an idle drift next to the
player's ~200, so nearly all of the crossing is parallax rather than the airship's own motion.
Every spawn after the first one is placed just past the **right** edge of its own window
(`halfWindow`, the half view width at its depth), heading into the oncoming camera.

Spawn and death both use the same `HideMargin` (0.6 lengths past the window edge) — barely
more than the half length it takes to be out of sight. Anything larger is time the airship
spends alive but invisible, which at this size is several seconds of empty sky at each end.
The left edge is the only exit test needed: the campaign camera's X never decreases and the
airship's never increases, so the gap between them only ever closes.

**What the drift speed costs.** On level 3 an airship is spawned about 1410 units right of the
camera and has to reach x ≈ −200 to hand over, ~2600 units at 10–20 units/s: **two and a half to
four minutes** between arrivals. That is the drift's price, not the handover rule's — the rule
could fire at the window's edge instead of the map's and only save a few hundred units.
`SpeedMin`/`SpeedMax` are the knob if the relay should read as a procession rather than as one
airship per sortie.

## What replaced the left limit

`SetLeftLimit(x)` used to stop the airship dead at a world X and hold it there — the airfield's
right edge on level 3, so that nothing hostile drifted over the squadron's own aerodrome. With
the camera clamped inside the arena, an airship parked there was never far enough behind the
camera to die, so it hung over the field for the rest of the sortie and nothing else ever flew.

`SetLeftEdge(x)` takes its place with the opposite meaning: the same line is now something an
airship crosses rather than stops at, and crossing it is what calls the next one in. Airships do
pass over the aerodrome now — which is what a raid looks like, and the flak battery's own
`SetLeftLimit(ApronEndX)` is untouched, so the guns still do not fire over the field.

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
