# Night searchlight (`Assets/Scripts/PlaneSearchlight.cs`)

The player plane's nose light for night levels: a warm cone from the cowl, toggled with **T**,
off when the level starts. Purely cosmetic — it changes nothing about enemies, targeting or
difficulty.

## When it exists

`PlaneSearchlight.Mount(body, noseLocal, daytime)` returns `null` for every daytime but
`Daytime.Night`, so on a day level no light object is created and T does nothing at all. Both
level controllers mount it right after the guns, on the same cowl point
(`PlaneFactory.NoseLocal`, the propeller-hub centre line just ahead of the prop disc):

- `LevelController.SpawnPlayer` — the Air Fight levels.
- `CampaignLevelController.SpawnPlayer` — campaign levels and custom battles (whose daytime
  comes from the custom-battle menu, see docs/main-menu.md).

The returned component is also what gates the HUD readout — the controllers build the
indicator only when it is non-null.

## The two halves

**1. The spot light** — a real URP additional light, so it lights and shadows whatever the cone
lands on: ground, trees, enemy planes. Mounted on a child rotated `Euler(0, 90, 0)`, because a
spot shines down its own +Z while the plane's nose is the body's +X (the body is yawed about Z
to the flight heading, so +X is always the direction of flight — the beam points exactly where
the nose does, with no downward tilt).

| Setting | Value | Note |
|---|---|---|
| Range | 250 m | ~4 plane lengths, well under half the screen width |
| Cone angle | 25° | inner cone at half that, so the rim feathers |
| Colour | `(1, 0.88, 0.62)` | warm yellow, clearly not the moon's silver-blue |
| Shadows | Soft, strength 0.85 | hills block the beam; planes throw shadows onto the land |

**Intensity is derived, not authored.** URP attenuates an additional light by `1/d²`, so a raw
intensity number is meaningless without a distance: the constant `BrightnessAtRange` (1.6) is
the lighting wanted *head-on* at the end of the beam, and the total intensity is
`BrightnessAtRange × Range²` (100 000). Retune `BrightnessAtRange`, never the product.

That number looks absurd for something nominally "white at 1.0", and it has to be. The beam
flies level, so it rakes flat ground at a grazing angle and Lambert's `N·L` throws away most of
what arrives — only slopes facing the plane get the head-on figure. On top of that the night
grade lifts the whole frame by 2 EV, so a pool that merely matches the ambient reads as no pool
at all. Earlier values (0.30, then 0.45) lit surfaces on paper and were invisible in play.

**Why several lights.** Some platforms (Metal among them) carry a light's colour as a 16-bit
half, which overflows to infinity past 65504 — so the total is split across
`ceil(total / MaxLightIntensity)` coincident spots (2 at the current numbers, 50 000 each).
URP adds their contributions, and because they share a position, cone and range, their shadow
maps agree exactly. Raising `BrightnessAtRange` past another multiple of `MaxLightIntensity`
simply adds another light and another shadow map.

Both controllers already push `urp.shadowDistance` out to `CameraDistance + 200` (620 m) for the
plane's own shadow; that also covers the searchlight's shadows, which live at most ~490 m from
the camera.

**2. The visible shaft** — the beam in the air. The camera never rotates and the plane's Z is
frozen, so a *flat wedge* lying in the play plane reads exactly like a cone from this view and
costs nothing: a triangle fan (16 segments) out to a flat tip, built once at unit length and
scaled. `Custom/SearchlightBeam` (`Assets/Shaders/Resources/SearchlightBeam.shader`, in
Resources so `Shader.Find` sees it in builds) draws it additively — `Blend SrcAlpha One`, ZWrite
off, no shadows, no collider — with three fades: lengthwise (`_FarFade`), softening toward the
cone's rim (`_EdgeSoftness`), and a short ramp-in off the nose (`_NoseRamp`, ~8 m). Base alpha
is 0.35: the air holds far less light than the surface the beam lands on.

### Where the cone starts

The shaft's geometric apex is **buried inside the fuselage**, `ApexInsideFraction` (0.75) of the
way back from the nose to the body's centre, so the cone is already open where it leaves the
nose instead of pinching to a point there. Only the mesh moves back: the light itself stays on
the nose, so the airframe never sits inside its cone (and never shadows it).

The shader is told about the offset through `_ApexOffset` (the pullback as a fraction of range),
and measures everything — the lengthwise fade and the nose ramp — from the nose rather than from
the buried apex. That keeps the shaft invisible where it passes through the plane and puts the
end of the fade exactly `Range` metres ahead of the nose.

### Truncation

Every frame the light is on, `MeasureReach` raycasts from the nose along the heading over the
full range and the shaft is scaled to `apex pullback + hit distance`, so it visibly stops on the
hillside or the enemy it is lighting instead of running through it. Aimed at open air it runs
the full 250 m and fades to nothing — nothing to reflect off.

Two things are skipped when picking the hit: the plane's own airframe (the ray starts inside its
bounds) and `Bullet`s (the player's own rounds fly straight down the beam and would make it
stutter).

The shader's `_Reach` is the fraction of the *full* range the shaft's tip stands for, written
alongside the scale. Without it a truncated shaft would restart its lengthwise fade and dim out
just where it lands; with it, a beam cut at 40 m is still at full brightness where it hits the
ground.

## HUD

`SearchlightIndicator` (`Assets/Scripts/SearchlightIndicator.cs`) — a 150×30 plate reading
`LIGHT  T` under the health bar at `(-719, 425)`, to the right of the bomb square that now holds
the left end of that row (docs/bombs.md), dim grey `(0.55, 0.55, 0.62)` when off and warm
`(1, 0.85, 0.45)` when on. Refreshed from `PlaneSearchlight.IsOn` in each controller's HUD
update. The bottom control-hint line is deliberately left alone.

## Death

The searchlight object is a child of the physics body, so `CubeController.HideModel` — which
deactivates every child after the crash explosion — takes the light and its shaft with it, and
`IsOn` (which ANDs in `isActiveAndEnabled`) drops the HUD readout back to dim at the same
moment. No extra teardown path.
