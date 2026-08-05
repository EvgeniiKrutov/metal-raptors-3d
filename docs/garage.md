# Garage

The plane picker. Reached from `garage` in the main list only — no other screen opens it —
and built at runtime in `GarageController`, like the rest of the game's UI. The `Garage`
scene holds a camera, a light and the controller object, nothing else.

It replaces the old mech-picker garage entirely: the three rotating preview cubes, the
uGUI buttons and the volume steppers are gone, along with `GameManager`'s
`AvailableMechs` / `CubeColors` / `SelectedMech*`. What the player picks here is a
`PlaneModelConfig`, and that pick is what the main menu and every level then fly.

## Structure

```
┌────────── 40% ──────────┬─────────── 60% ───────────┐
│                         │                           │  ← 15% of the height
│     SOPWITH CAMEL       │                           │  ← the plane's own name
│     ───                 │                           │
│                         │                           │
│     ▐FIGHTER▌           │                           │  ← type badge
│     Great Britain       │        the plane,         │
│                         │   parked, on the ground   │
│     MAX SPEED           │                           │
│     ███████████░░░░░    │                           │
│     ROTATION SPEED      │                           │
│     ███████████░░░░░    │                           │
│     MASS                │                           │
│     ██████████░░░░░░    │                           │
│     FIRE RATE           │                           │
│     ██████████░░░░░░    │                           │
│     DAMAGE              │                           │
│     ██████████░░░░░░    │                           │
│     HEALTH              │                           │
│     ██████████░░░░░░    │                           │
│                         │                           │
│     select plane        │                           │
│                         │                           │
◀                         │                           ▶  ← switch the plane
│                                                     │
│         lorem ipsum dolor sit amet, consectetur     │  ← centred on the page,
│         adipiscing elit, sed do eiusmod tempor…     │    not on the column
└─────────────────────────────────────────────────────┘
      ↑ 200px, not the 120px every other screen uses
```

The column is the same left 40% every other menu screen uses, so the title, the captions,
the bars and `select plane` all read down one left edge. The two triangles hang off the
**page** edges, not the column's, and the description is centred on the **canvas** — the
only two things in the whole menu that are not composed against a left inset, because they
belong to the screen rather than to the list.

The garage is the one screen that does **not** take the shared `PadLeft` (120px). It builds
its column through `MenuLayout.CreateRegion` with `GaragePadLeft` (200px) instead, because
it is the only screen with something outside the column on the same side: at 120px the list
would start just 46px clear of the left triangle and read as crowding it. That leaves 512px
of inner width, which the 460px stat bars still sit inside.

The description is anchored `GarageDescriptionBottom` (168px) off the bottom and drawn
`LowerCenter`, so it grows upward from that line rather than down from a top edge — it sits
just under the plane's band (which stops at 24% of the height) and about 45px under the last
entry in the column, tying the two halves together instead of floating at the page's foot.

## The plane

`GaragePlaneView` renders it, sharing `PlanePreviewRig` with the main menu's flying plane
(see `docs/main-menu.md` for how the render-texture band works). Only the framing and what
happens to the body differ:

| | main menu | garage |
| --- | --- | --- |
| band | right 60%, full height | from 32.8% across, from 24% of the height up |
| pose | cursor-driven flight, bob and sway | parked front three-quarter, drag to turn |
| framing | height-bound, room reserved for the bank | width-bound, ~86% of the band |
| propeller | always spinning | still, except during the select animation |

The band stops at `RegionBottomFraction` (24%) so the plane never sits over the
description. Both the camera and the page clear to `MenuTheme.Colors.Bg`, so the band's
edges are invisible — the plane reads as sitting on the page, not in a picture on it.

That invisibility is what lets `RegionLeftFraction` (32.8%) start the band **left of the
column boundary**, overlapping the stat bars by ~30px. The band is a `RawImage` created
before the pages, so the bars draw over it, and the overlapped strip is flat `Bg` either
way. The plane is centred in its band, so pulling that edge left is the only way to move the
plane toward the menu without shrinking the band — and a narrower band would mean a smaller
plane, which is the opposite of what is wanted.

The plane is therefore boxed in on both sides, and the two constants are set against those
edges rather than picked for looks: at 1920×1080 the model spans x 719–1775, leaving ~59px
between its left tip and the end of the stat bars (x 660) and ~71px between its right tip
and the left edge of the right triangle (x 1846). Widening the fill or moving the band
further left runs into one of those two.

### Parked pose

The body is yawed `BodyYawDeg` (+8°) and pitched by `_restPitch` about its nose axis. The
camera adds its own 13° of look-down and −32° of yaw.

**The pitch is solved from the mesh, not authored.** A hardcoded angle cannot put a plane on
the ground: too little and it sinks through, too much and it balances on its tail with the
wheels in the air, and the right value differs per model. Neither FBX has wheel nodes to
measure against either — the only named nodes are `propPivot`, `propBlades`, `tailplane` and
the wings — so there is nothing to hang a per-plane constant off.

`SolveRestingPitch` finds the angle the airframe actually rests at, the same way a real one
would settle:

1. `ContactProfile` collects every vertex of the model in **body-local** space — the frame
   the pitch rotation acts on — and flattens it to (x, y): x along the nose, y up.
2. `LowerHull` takes the lower convex hull of that silhouette (Andrew's monotone chain).
   Every edge of it is a pair of points that could touch a flat floor together.
3. The plane rests on the edge whose x-span contains the profile's centroid — the one it
   would balance on rather than tip off.
4. `EdgePitch` returns the rotation that levels that edge, `-atan2(Δy, Δx)`.

So both contact points land on the ground at once, whatever the model's proportions, and a
third plane needs no tuning. `FallbackNoseUpDeg` (12°) only applies if the model has no
measurable mesh at all.

The propeller is **excluded** from the profile (`ContactMeshes` skips anything under
`propPivotNode` / `propBladesNode`). A blade pointing down would otherwise be the lowest
geometry and the plane would be solved as resting on its propeller tip.

`ContactHeight` places the ground at the true lowest vertex of that same filtered set, not at
the renderer bounds' `min.y` — a rotated mesh's world AABB reaches below its geometry, which
would sit the ground slightly under the wheels and leave the plane floating.

Body yaw and camera yaw together decide how head-on the plane reads. At +8° against the
camera's −32° the fuselage sits ~50° off the view axis — a **front** three-quarter, nose and
propeller turned toward the viewer with the flank and wings still open. Yawing the body
further positive turns it more head-on (it would be nose-straight-at-camera around +58°);
negative swings it back to the broadside view.

After the model is built its renderer bounds are measured and the model is shifted so those
bounds are centred on the rig origin, then dropped `DropFraction` (3.5% of the plane's own
size, ~37px) below it — the camera stays aimed at the origin, so the plane sits a little
under the band's middle rather than dead centre. The ground is placed after that shift, from
the contact height, so it follows the drop and the plane stays on it.

### Drag to turn

Holding the left button anywhere over the plane's band and moving left/right turns the plane
on the spot, so it can be looked at from any side; letting go eases it back to the parked
pose. Both ends run through one `Mathf.SmoothDamp` on a single yaw offset, with the only
difference being how hard it pulls — `DragSmoothing` (0.06) while held so it tracks the hand,
`ReturnSmoothing` (0.85) once released so it drifts back rather than snaps.

* Travel is measured as a **fraction of the screen width**, not in pixels, so the same
  gesture turns the plane the same amount at any resolution: a full screen's drag is
  `DragDegreesPerScreen` (480°).
* The offset is clamped to ±`MaxDragDeg` (180°), which already reaches every side of the
  airframe, so a long drag can never wind up a multi-turn unwind on release.
* A drag only **starts** inside the band (`RectTransformUtility.RectangleContainsScreenPoint`
  against the rig's `RegionRect`), so dragging across the stat bars does not spin the plane.
  Once started it keeps tracking outside the band, which is what makes a fast flick work.
* A fresh drag anchors to wherever the plane currently sits, so grabbing it mid-return picks
  it up from there instead of jumping.

The turn is a **world-Y** rotation applied outside the parked pitch, which is what keeps the
rest of the scene valid while it spins: yaw about the vertical leaves every point's height
untouched, so the model's lowest point — and with it the ground plane placed at that
height — never moves. The body sits at the rig origin and the model's bounds are centred on
it, so the plane turns on its own axis rather than orbiting.

The framing solves the camera distance from `onScreenSize`, and `PlaneFactory.NormalizeSize`
already scales every model to that same longest-dimension size — so two planes of very
different real dimensions both arrive framed identically, and the garage needs no per-plane
scale of its own.

The garage frames **wider than the main menu**: `FillHeight` is deliberately set past 1
(1.5), which makes the height constraint slack so `FillWidth` (0.93) is always what binds.
The plane's longest dimension then runs ~86% of the band's width — a plane fraction of
`FillWidth × (1 - VerticalMarginFraction)`, which is the number to solve when retuning
either constant. That is safe only because the plane is static and lying mostly horizontal:
its projected height comes out around 83% of the band, well under its longest dimension, so
nothing clips top or bottom. The flying menu plane cannot do this — it banks and bobs, so it
needs the conservative square-ish reservation instead.

Height is the ceiling on how big it can get. The band cannot grow downward (the description
is under it) or upward (it already reaches the canvas top), so past this point extra width
only buys a taller projection against a fixed 821px of band.

### Ground and cast shadow

The plane stands on a real ground plane and casts a real shadow onto it. The ground is a
quad at the model's lowest point, scaled to `GroundSizeFactor` (8×) the plane's size, running
`Custom/GroundShadowCatcher`.

That shader is the whole trick. It is **not lit** — it samples the main light's shadow map
directly and outputs nothing but the shadow: fully transparent where the ground is lit,
`_ShadowColor` at `ShadowAlpha` (0.4, before the light's own shadow strength) where it is not. Since the preview camera clears to
`MenuTheme.Colors.Bg`, an invisible ground *is* the menu colour, so the page reads flat and
only the shadow marks it. A lit ground could not do this: its colour would ride on the
light's intensity and angle, and re-aiming the light to move the shadow would shift the page
colour underneath it.

The ground's own renderer is set `ShadowCastingMode.Off` — it receives, never casts.

### Lighting

`GarageLighting.Apply` sets the scene up the way a level's atmosphere does, because the
garage wants the same result: an airframe read in full daylight, no face of it lost to
black, and still a shadow on the ground under it. It mirrors `MiddaySky` minus the parts
that belong to a sky — no skybox, no god rays, no post FX, since the preview camera clears
to the flat menu colour.

The piece that actually fixes a dark plane is the **ambient**, not the sun. The `Garage`
scene ships on `AmbientMode.Skybox` against the default skybox, so every surface the sun
misses falls to near-black and the model reads as a silhouette. `Apply` switches to
`AmbientMode.Trilight` with `MiddaySky`'s own three colours (sky `0.61, 0.75, 1.00`, equator
`0.80, 0.84, 0.88`, ground `0.47, 0.41, 0.34`), which fills the unlit faces with sky light —
exactly what keeps panel lines and struts visible on the side facing away from the sun.

`TuneSunLight` then takes the scene's directional light the same way `MiddaySky` does —
found at runtime, so the code owns it and the scene asset is only a starting value:

| | value | why |
| --- | --- | --- |
| colour / intensity | `1.00, 0.96, 0.90` at 1.35 | `MiddaySky`'s sun, unchanged |
| rotation | `Euler(55, -92, 0)` | see below |
| shadows | soft, strength 0.75 | full strength crushes the plane's own self-shadowed panels |
| normal bias | 0.5 | `MiddaySky`'s value; keeps the wings off their own shadow |
| shadow distance | ≥150 on the URP asset | the preview camera sits ~46 units out and the asset ships at 50, so the plane fell on the edge of the range |

Shadow strength and the ground's `ShadowAlpha` **multiply**: at 0.75 and 0.4 the cast shadow
lands at 0.3 on the page. Changing one to taste means checking the other.

The light angle is set against `ViewYawDeg` (−32°), not chosen on its own:

* **Azimuth.** The light comes from ~88°, roughly 60° off to the side of where the viewer
  stands (~148°). That puts it on the same side as the plane's nose, so the nose, propeller
  and the whole camera-facing flank take direct light, while the shadow is thrown to the
  opposite side. It lands 60° off the view axis: mostly lateral, angled back — behind the
  plane rather than in front of it, but far enough round that the airframe does not sit on
  top of it and hide it.
* **Elevation.** 55° up, high enough to read as overhead. Elevation is the shadow's length
  (`length ≈ height / tan(elevation)`), so raising it tucks the shadow in tighter under the
  plane and lowering it stretches the shadow out.

The camera looks only `ViewPitchDeg` (13°) above the horizon, so whatever the ground shows is
compressed to about a fifth of its true depth. That is why azimuth matters more than
elevation here: a shadow thrown sideways keeps its length on screen, where one thrown
straight back would flatten into a band along the contact line.

### Select animation

`PlaneFactory` attaches `PropellerSpin` to every plane it builds; the garage sets its
`degreesPerSecond` to 0 on build, so the parked plane's propeller is still. Activating
`select plane` runs it through a one-shot ramp — smoothstep up to `SpinPeakDegreesPerSecond`
over `SpinUpSeconds`, held for `SpinHoldSeconds`, then smoothstepped back to a standstill
over `SpinDownSeconds`. Switching planes cancels it, and `GaragePlaneView.SetPlane` no-ops
when handed the plane it already holds, so the refresh that follows a selection does not
rebuild the body out from under the animation.

## Stats

`PlaneStats` hangs off `PlaneModelConfig`, so a plane's numbers travel with its model
definition. `PlaneStatBars.All` is the display list — one entry per bar, carrying its
label, the field to read and the **ceiling** the bar is drawn against:

| bar | ceiling | today's value |
| --- | --- | --- |
| max speed | 280 | 192 |
| rotation speed | 260 | 180 |
| mass | 4 | 2.5 |
| fire rate | 8 | 5 |
| damage | 15 | 10 |
| health | 150 | 100 |

The ceilings are display-only headroom, not caps — they exist so today's values sit around
two thirds full instead of pegged at 100%, leaving somewhere for a faster or tougher plane
to go. A bar is `fill / ceiling`, clamped.

Above the bars sit two rows that are not bars, and neither carries a caption:

* the **type badge** — a filled rectangle in the type's own colour with its name in the page
  background colour, built by `MenuBadge`. `PlaneType` pairs the label with the colour and
  `PlaneTypes` holds the table (`Fighter` `#9E4A3C`, plus `Bomber` and `Recon` ready for when
  a plane needs them); a plane points at one through `PlaneModelConfig.type`. The badge sizes
  itself to its text plus `BadgePadX` either side, so a longer type name just makes a wider
  badge. Both planes are `Fighter` today.
* the **country** — a bare `Fg` value, no caption over it. It reads fine unlabelled under the
  plane's name, and the caption would only be in the way of the flag that is going there.

`MenuStatRow` builds the bar and both value shapes; `MenuPanel.AddStatBar` / `AddStatText` /
`AddBadge` place them in the panel's cursor flow alongside the entries. None of them is
registered as focusable — they are content, so the highlight skips them.

Stat captions have their **own** size (`StatCaptionSize`, 20px on a 26px row) rather than
sharing the menu's 14px `CaptionSize`: here the caption names the thing being read, so it
carries the row, where elsewhere a caption only heads a list. `StatRowGap` (18px) is tuned
against that height to keep the badge, the country, the six bars and `select plane` inside
the column — the whole block runs ~536px from `ListTop`, ending ~288px above the bottom of a
1080 screen, with the description another ~39px below that.

**Both planes currently carry identical stats**, written out per plane rather than shared,
so giving one of them its own numbers is a one-line edit. The values mirror
`PlayerConfig.asset` (max speed being `flySpeed × maxSpeedMultiplier`, fire rate being
`1 / fireRate` in shots per second), but they are **display-only**: flight behaviour still
comes from the single `PlayerConfig` asset whichever plane is selected.

The description under each plane is placeholder lorem ipsum, one per plane so switching
visibly changes it.

## Navigation

* `←` / `→` switch the plane from anywhere on the screen — they are not routed into the
  panel, so the list never spends them. The two triangles do the same on click, and light
  `Accent` while hovered (`MenuArrowView` gained an `Exited` event for this; the selector
  rows do not subscribe, so their arrows keep the menu's no-clear-on-exit rule).
* The list **wraps**: with two planes both triangles are always live, so neither ever greys
  out the way a selector row's do at the end of its values.
* `↑` / `↓` move the focus inside the column — only `select plane` is focusable.
* **Holding the left button over the plane** and moving left/right turns it (see *Drag to
  turn* above); releasing eases it back. The band overlaps the right triangle, so a click
  there both switches the plane and opens a drag — harmless, since a click travels far too
  little to turn anything visibly.
* `Enter` / `Space` selects; `Escape` returns to the main menu. There is no `back` entry.

## Selecting

`select plane` writes the index through `GameManager.SetSelectedPlane`, which persists it to
`PlayerPrefs` (`mr_selected_plane`) immediately, and the entry becomes `selected` and
disabled — `Muted`, no hit box, `Activate` refuses. Switching to the other plane brings a
live `select plane` back. The entry stays registered as focusable while disabled: with a
one-entry list the highlight has nowhere else to go, and it reads as the row you just acted
on rather than vanishing.

`GameManager.CurrentPlane` is the read side, null-safe against `Instance`, and it is what
`MainMenuController`, `LevelController` and `CampaignLevelController` build the player's
plane from — so the pick shows up in the menu's flying plane, in the levels, and in the
HUD's `Piloting:` line.

Enemies are **not** affected: `LevelDefinition` keeps its authored `PlaneModels.Fokker`
groups, so picking the Fokker means both sides fly it. They stay distinguishable by the
mirrored, opposite-pitched build `PlaneFactory` gives an enemy.

## Files

| File | Role |
| --- | --- |
| `GarageController.cs` | Composes the screen, owns the plane index, and refreshes everything from it. |
| `GaragePlaneView.cs` | The parked plane: pose, contact shadow, propeller ramp, plane swap. |
| `PlanePreviewRig.cs` | The camera + render texture + framing shared with `MenuPlaneView`. |
| `MenuStatRow.cs` | One stat row, as a bar or as a text value (captioned or bare). |
| `MenuBadge.cs` | The type badge: a filled rectangle sized to its own label. |
| `GroundShadowCatcher.shader` | The invisible ground that shows only the plane's cast shadow. |
| `GarageLighting.cs` | Ambient, sun and shadow distance for the scene, mirroring a level's atmosphere. |
| `PlaneStats.cs` | The stat block a plane carries, and the bar list with its ceilings. |
| `PlaneModelConfig.cs` | Each plane's model, display name, country, description and stats; `PlaneModels.All` is the switch order. |
