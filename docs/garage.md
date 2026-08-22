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
│  ▐FIGHTER▌ Great Britain│        the plane,         │  ← badge + country, one row
│     colour   ◀ green ▶  │   parked, on the ground   │  ← Sopwith only; the value
│                         │                           │    is centred between them
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
│     back                │                           │  ← a SectionGap above it
│                         │                           │
◀                         │                           ▶  ← switch the plane
│                                                     │
│      Britain's most successful scout of the war,    │  ← centred on the page,
│      credited with more enemy aircraft downed…      │    not on the column
└─────────────────────────────────────────────────────┘
      ↑ 200px, not the 120px every other screen uses
        the badge row is drawn to the column edge here for want of characters;
        it is really ~256px of the column's 592px
```

The column is the same left 40% every other menu screen uses, so the title, the captions,
the bars and `select plane` all read down one left edge. The two triangles hang off the
**page** edges, not the column's, and the description is centred on the **canvas** — the
only two things in the whole menu that are not composed against a left inset, because they
belong to the screen rather than to the list.

### The triangles and the notch

Each triangle is inset from its own side by `GarageArrowInset` (44px) **plus that side's
safe-area inset** — `MenuTheme.SafeLeft` / `SafeRight`, read from `Screen.safeArea` when the
canvas is built (docs/touch-input.md). On a desktop both are 0 and nothing moves. On an
iPhone in landscape the Dynamic Island covers ~147 canvas units of one edge, which is where
the left triangle was sitting: invisible, and unreachable by a finger. Because the value comes
from `safeArea` rather than a constant, the inset follows the phone through a rotation and
lands on whichever side the island is on.

On touch the triangles are also `ArrowScale`× bigger and carry an invisible `ArrowPad` hit
area, since a 30×38 glyph is about 12pt across on a phone (docs/touch-input.md).

The garage is the one screen that does **not** take the shared `PadLeft` (120px). It builds
its column through `MenuLayout.CreateRegion` with `GaragePadLeft` (200px) instead, because
it is the only screen with something outside the column on the same side: at 120px the list
would start just 46px clear of the left triangle and read as crowding it. That leaves 512px
of inner width, which the 460px stat bars still sit inside.

`GaragePadLeft` is a property rather than that flat 200, because the triangle it is clearing
does not always start at the same place: it is
`max(200, SafeLeft + GarageArrowInset + GarageArrowSize.x + GarageArrowToColumn)`, so a
notched phone in landscape pushes the column to 313 and keeps the 80px of air over the
triangle (docs/touch-input.md). The `max` is what keeps the bars fitting — 479px of column
against a 460px bar — where adding the inset to the 200 outright would have left 445.

The description is anchored `GarageDescriptionBottom` (124px) off the bottom and drawn
`LowerCenter`, so it grows upward from that line rather than down from a top edge — it sits
under the plane's band (which stops at 24% of the height) and about 40px under the last
**wide** entry in the column, tying the two halves together instead of floating at the page's
foot.

That constant is set against the column, not chosen for looks. It moved from 168px when the
460px-wide `colour` row was added low in the column — it reached into the description's
x-range where `select plane` (~150px of text) never did, so the description dropped by the
row's own 54px to keep the clearance it had. The `colour` row has since moved up under the
badge, which leaves `select plane` and `back` as the lowest rows and both are narrow again;
124 stays anyway, because what it now clears is the **plane's band**, which stops at 24% of
the height. At 168 the description's box would reach past that line and print over the band.

## The plane

`GaragePlaneView` renders it, sharing `PlanePreviewRig` with the main menu's flying plane
(see `docs/main-menu.md` for how the render-texture band works). Only the framing and what
happens to the body differ:

| | main menu | garage |
| --- | --- | --- |
| band | right 60%, full height | from 32.8% across, from 24% of the height up |
| pose | cursor-driven flight, bob and sway | parked front three-quarter, drag or swipe to turn |
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
wheels in the air, and the right value differs per model. No FBX gives anything to hang a
per-plane constant off either: the Camel and the Dr.I carry only `propPivot`, `propBlades`,
`tailplane` and the wings, and the Albatros only its propeller nodes and a row of empty
grouping nodes left over from its export. None of them is a wheel.

`SolveRestingPitch` finds the angle the airframe actually rests at, the same way a real one
would settle:

1. `ContactProfile` collects every vertex of the model in **body-local** space — the frame
   the pitch rotation acts on — and flattens it to (x, y): x along the nose, y up.
2. `LowerHull` takes the lower convex hull of that silhouette (Andrew's monotone chain).
   Every edge of it is a pair of points that could touch a flat floor together.
3. The plane rests on the edge whose x-span contains the profile's centroid — the one it
   would balance on rather than tip off.
4. `EdgePitch` returns the rotation that levels that edge, `-atan2(Δy, Δx)`.

So both contact points land on the ground at once, whatever the model's proportions. The
Albatros was added without a single pose constant, which is what this was built for.
`FallbackNoseUpDeg` (12°) only applies if the model has no measurable mesh at all.

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

### Where it stands

The model is shifted **horizontally** so its renderer bounds are centred on the rig origin —
the camera is aimed there, so that is what puts the plane in the middle of its band.

**Vertically it is anchored on the wheels, not on the airframe.** The ground line is placed
`GroundLineFraction` (0.213) of the plane's framed size below the origin, and the model is
dropped so `ContactHeight` — the lowest vertex of everything but the propeller — lands
exactly on it. The ground quad then goes to the same `groundY`, so the plane is standing on
it by construction rather than by a second measurement.

It used to centre the **bounds** vertically too and let the wheels fall wherever the airframe's
lower extent happened to be, which meant the ground line moved every time the plane changed:

| plane | ground line below the origin, as a fraction of size |
| --- | --- |
| Sopwith Camel | −0.422 |
| Fokker Dr.I | −0.213 |
| Albatros D.III | −0.444 |

Over a fifth of a plane's length of travel between two entries in the same list — the ground
visibly jumped, and only the Dr.I sat at a natural height. 0.213 is the Dr.I's number, so it
is the one plane the change leaves where it was.

The fraction is of `GarageSize` — `onScreenSize / garageZoom`, the same figure the camera
distance is solved from — not of `onScreenSize`. Scaling it by the raw size would put the
ground at a fixed *world* offset, which a zoomed-in plane would then render further down the
screen; against the framed size it lands on the same **screen** row for every plane.

The trade is that the airframe is no longer vertically centred: a taller plane now reaches
higher in the band instead of hanging lower. You can align the wheels or centre the airframe,
not both, and the wheels are the edge the eye tracks when the plane changes — a plane's own
height is read against the ground it stands on, not against the middle of an invisible band.

### Drag to turn

Holding the left button — or a finger, `MenuInput.ReadPointer` reads whichever is down
(docs/touch-input.md) — anywhere over the plane's band and moving left/right turns the plane
on the spot, so it can be looked at from any side; letting go eases it back to the parked
pose. Both ends run through one `Mathf.SmoothDamp` on a single yaw offset, with the only
difference being how hard it pulls — `DragSmoothing` (0.06) while held so it tracks the hand,
`ReturnSmoothing` (0.85) once released so it drifts back rather than snaps.

* Travel is measured as a **fraction of the screen width**, not in pixels, so the same
  gesture turns the plane the same amount at any resolution: a full screen's drag is
  `DragDegreesPerScreen` (480°).
* The offset is clamped to ±`MaxDragDeg` (180°), which already reaches every side of the
  airframe, so a long drag can never wind up a multi-turn unwind on release.
* A lifted finger reports no pointer at all, which reads the same as a released button: the
  drag ends and the position is never used, since it is only read while `_dragging`.
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
already scales every model to that same longest-dimension size — so planes of very
different real dimensions all arrive framed identically. All three share `onScreenSize` 60.

That normalisation is on the **longest** dimension, which for every plane here is the
wingspan, and it is the only thing the framing knows about. A flatter airframe therefore
arrives at the same width but fills less of the band's height and reads as further away:
the Camel stands 0.365 of its span tall, the Albatros only 0.287. `PlaneModelConfig.garageZoom`
is the correction — the garage frames the camera from `onScreenSize / garageZoom`, so 1.1
puts the camera 10% closer and the plane 10% larger. It is **garage-only**: `onScreenSize`
itself is the plane's real size in a level and its hitbox, so it must not be touched for
framing. The Albatros is 1.1; the other two are 1.

There is not much room above that. At 1920×1080 the Camel spans x 719–1775 with ~59px clear
to the stat bars and ~71px to the right triangle — about 12% of headroom in total, shared by
every plane, since they all normalise to the same width.

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

`PlaneFactory` attaches `PropellerSpin` to every plane it builds and hands it the body to
read its spin axis from (`docs/effects.md`); the garage sets its `degreesPerSecond` to 0 on
build, so the parked plane's propeller is still. Activating
`select plane` runs it through a one-shot ramp — smoothstep up to `SpinPeakDegreesPerSecond`
over `SpinUpSeconds`, held for `SpinHoldSeconds`, then smoothstepped back to a standstill
over `SpinDownSeconds`. Switching planes cancels it, and `GaragePlaneView.SetPlane` only
repaints when handed the plane it already holds, so the refresh that follows a selection does
not rebuild the body out from under the animation.

## Stats

`PlaneStats` hangs off `PlaneModelConfig`, so a plane's numbers travel with its model
definition. `PlaneStatBars.All` is the display list — one entry per bar, carrying its
label, the field to read and the **ceiling** the bar is drawn against:

| bar | ceiling | Sopwith Camel | Fokker Dr.I | Albatros D.III |
| --- | --- | --- | --- | --- |
| max speed | 360 | 288 | 264 | 300 |
| rotation speed | 200 | 120 | 140 | 104 |
| mass | 4 | 2.5 | 2.1 | 3 |
| fire rate | 8 | 5 | 5.5 | 5.5 |
| damage | 15 | 10 | 10 | 10 |
| health | 200 | 150 | 128 | 165 |

The ceilings are display headroom, not caps — they exist so today's values sit around two
thirds full instead of pegged at 100%, leaving somewhere for a faster or tougher plane to go.
A bar is `fill / ceiling`, clamped. Three of them moved when the planes were given real
numbers: **max speed** from 280 (the Camel's 288 would have pegged the bar), **rotation
speed** from 260 (at 120 the bar read under half full and looked broken rather than modest)
and **health** from 150, when the Camel was raised to 150 flat and would have pegged it.

The health numbers were 100 / 85 before that raise; the Dr.I keeps the same 85% of the
Camel it always had, so the two planes still read the same way against each other — the
whole scale just moved up. The Albatros arrived after that raise and was written against the
new scale directly.

Above the bars sits **one** row that is not a bar, carrying two things and no caption:

* the **type badge** — a filled rectangle in the type's own colour with its name in the page
  background colour, built by `MenuBadge`. `PlaneType` pairs the label with the colour and
  `PlaneTypes` holds the table (`Fighter` `#9E4A3C`, plus `Bomber` and `Recon` ready for when
  a plane needs them); a plane points at one through `PlaneModelConfig.type`. The badge sizes
  itself to its text plus `BadgePadX` either side, so a longer type name just makes a wider
  badge. All three planes are `Fighter` today.
* the **country**, `BadgeValueGap` (16px) to the right of it — a bare `Fg` value, no caption.
  It reads fine unlabelled beside the type, and a caption would only be in the way of the flag
  that is going there.

The country used to be its own row under the badge. It is a short value against a short
badge — `FIGHTER` is ~90px of a 592px column — so a whole 46px row for it was two thirds air,
and pairing them reads as one line about *what this aeroplane is* before the numbers start.
It also buys back 46px of column, which is what the taller touch rows spend
(docs/touch-input.md).

`MenuBadge` owns the value rather than the panel: the badge's width changes with the type
name, so the label is anchored to the badge's **right edge** with a pivot on its own left
(`anchorMin = anchorMax = (1, 0.5)`) and follows any resize for free — no reflow pass, and
`Set` stays the only thing that measures text.

`MenuStatRow` now builds bars only, and `MenuPanel.AddStatBar` / `AddBadge` place them in the
panel's cursor flow alongside the entries. Neither is registered as focusable — they are
content, so the highlight skips them. (`AddStatText`, `MenuStatRow.CreateBareValue` and
`StatValueRowHeight` went with the old row; the badge's value is the only bare value left and
it keeps `StatValueSize`.)

Stat captions have their **own** size (`StatCaptionSize`, 20px on a 26px row) rather than
sharing the menu's 14px `CaptionSize`: here the caption names the thing being read, so it
carries the row, where elsewhere a caption only heads a list. `StatRowGap` (18px) is tuned
against that height to keep the badge row, the six bars and `select plane` inside the column —
the whole block runs ~490px from `ListTop`, ending ~334px above the bottom of a 1080 screen,
with the description another ~39px below that.

### The bars are real

The stats are **not** a spec sheet — they are what the player flies. `PlaneLoadout.Build`
turns the selected plane's `PlaneStats` into the `PlayerConfig` the player's plane is
initialised with, so picking the Dr.I really does buy a harder turn for a lower top speed.
See `docs/flight-model.md` for the conversion and what it leaves alone.

The Camel's block is set to exactly what `PlayerConfig.asset` already held (`flySpeed` 180 ×
`maxSpeedMultiplier` 1.6 = 288, `fireRate` 0.2 s = 5 shots/s), so the plane the game shipped
with flies unchanged and the asset stays the baseline everything else is measured from. The
old numbers here — 192 and 180 — were stale copies that had drifted from it, which is the
failure mode a display-only table invites.

The Dr.I is drawn from the aircraft: lighter and quicker on the controls, slower in level
flight, more fragile, and a shade faster on the guns (its twin Spandaus outpaced the Camel's
Vickers). Damage is the one bar all three share — every one of them carried a pair of
synchronised rifle-calibre machine guns.

The Albatros D.III is the other extreme: a 160 hp inline Mercedes instead of a rotary, so it
is the **fastest** and the **toughest** of the three and by far the **worst turner** (104°/s,
against the Camel's 120 and the Dr.I's 140). It is the heaviest as well, at 3 of a 4 ceiling.
It shares the Dr.I's 5.5 fire rate — the same twin Spandaus — which leaves turn rate as the
price the player pays for its speed and its health.

**Enemies are unaffected.** An enemy flying an Albatros is initialised from
`EnemyConfig.asset`, which has its own numbers and its own AI fields; the garage block only
ever reaches a plane the player selected. Same for the companion wingman, which keeps the
shared `PlayerConfig` (`docs/companion.md`) — the stats follow the player, not the model.

The description under each plane is a short history of the real aircraft, held in
`PlaneModels` next to the rest of its definition.

## Colour

Directly under the badge row, above the first stat caption, sits the `colour` selector — the
plane's skin, described in full in `docs/plane-skins.md`. It is an ordinary
`MenuPanel.AddSelector`, the same widget custom battle uses for `map` and `weather`, so it
comes with its own pair of triangles that grey out at the ends of the list.

It used to sit under the bars, a `SectionGap` above `select plane`. It reads better against
the badge: what the plane *is* and what it *wears* are both identity, and the six bars below
are one uninterrupted block of numbers rather than a block with a widget hanging off it. The
column is no taller for the move — the `SectionGap` that used to be above the row is now above
`select plane`, so every row below sits exactly where it did.

Three things are particular to it:

* **It is per plane, and only the Sopwith has more than one skin.** `PlaneSkins.Selectable`
  is false for the Fokker (no skins) and for the Albatros (one, `plywood`, which it always
  wears), so the row is switched off and **everything below it** — the six bars, `select
  plane` and `back` — moves up by `ColourRowHeight` (54px) to close the gap it left. That is
  why `MenuStatRow` builds its caption and bar under a root of its own now: a bar is one
  transform with a `SetY`, like a `MenuItemView`, so the garage can shift the block by
  re-anchoring seven things it recorded the tops of at build time. `MenuPanel` skips
  focusables whose GameObject is inactive — that rule lives in `MoveFocus` / `FocusFirst`
  rather than in the garage, so any panel can hide a row now — and if the hidden row happened
  to hold the focus, `RefreshColour` hands it to `select plane`.
* **Changing it is not a selection.** There is no confirm step: `PickColour` writes straight
  through `GameManager.SetSkin` (persisted to `PlayerPrefs` immediately, like the plane) and
  repaints the preview. `select plane` is unaffected — the colour of a plane you are only
  browsing is still saved, since it is stored under that plane's own key.
* **The repaint does not rebuild the plane.** `GaragePlaneView.SetSkin` pushes the texture
  onto the model standing there, so the resting pitch stays solved, the ground stays put and
  a drag in progress is not interrupted.

## Navigation

* `←` / `→` switch the plane from anywhere on the screen — with one exception: while the
  `colour` row holds the focus they go to the row instead, because a selector that ignored
  the arrow keys would be the only one in the game that did. Everywhere else in the column
  they still step planes, so the list never spends them. The two page triangles do the same
  on click, and light `Accent` while hovered (`MenuArrowView` gained an `Exited` event for
  this; the selector rows do not subscribe, so their arrows keep the menu's no-clear-on-exit
  rule).
* The list **wraps**: with three planes both triangles are always live, so neither ever greys
  out the way a selector row's do at the end of its values.
* `↑` / `↓` move the focus inside the column — `colour` (when shown) and `select plane`.
* **Holding the left button (or a finger) over the plane** and moving left/right turns it (see
  *Drag to turn* above); releasing eases it back. The band overlaps the right triangle, so a click
  there both switches the plane and opens a drag — harmless, since a click travels far too
  little to turn anything visibly.
* `Enter` / `Space` selects; `Escape` and the `back` row both return to the main menu through
  the same `GoBack` → `ScreenFade.Load`, so the garage fades to black and the menu fades up out
  of it (`screen-fade.md`). Stepping planes is not a screen change and never fades — `Update`
  only bails while `ScreenFade.IsBusy`, which is the departing transition itself.

### The `back` row

The garage used to have no `back` entry, because `Escape` was always at hand. On a phone it is
not, and nothing else on the screen leaves it — so the row exists for the same reason the era
and custom-battle pages have one, and is written the same way: a `SectionGap` and a
`MenuPanel.AddNav`, tappable through `MenuItemView`'s pointer click (docs/touch-input.md).

It sits under `select plane`, which means `RefreshColour` now moves **two** rows by
`ColourRowHeight` when the `colour` selector is hidden, not one. It does *not* move the
description: `GarageDescriptionBottom` is solved against the widest low row in the column
(see *Structure*), and `back` is ~90px of text against the 460px `colour` row — it never
reaches into the description's centred x-range, so the constant stands.

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
HUD's `Piloting:` line. `GameManager.CurrentSkin` rides along at the same three sites, so
the colour travels with the plane.

Enemies are **not** affected: `LevelDefinition` keeps its authored `PlaneModels.Albatros`
groups, so picking the Albatros means both sides fly it. They stay distinguishable by the
mirrored, opposite-pitched build `PlaneFactory` gives an enemy — not by paint, since enemies
now wear the plane's *default* skin (`docs/plane-skins.md`) and the Albatros has only one.
They do not borrow the plane's stats either, since an enemy is built from
`EnemyConfig.asset`.

## Propeller nodes are per model

The names are not a convention — each FBX brings its own, and the config carries them so a
differently-exported model is a registry entry rather than a code change:

| plane | `propPivotNode` | `propBladesNode` | under them |
| --- | --- | --- | --- |
| Sopwith Camel | `propPivot` | `propBlades` | the blade mesh |
| Fokker Dr.I | `propPivot` | `propBlades` | the blade mesh |
| Albatros D.III | `propAssembly` | `prop` | `cyl.013` spinner, and `blade` + `cyl.014` under `prop` |

The Albatros carries a spinner as well as blades, which is why its pivot is a level above its
blade node: `PropellerSpin` goes on `propPivotNode`, so the spinner turns with the blades
instead of being left behind at the nose.

**A node that exists is not enough — it has to have geometry under it.** The Albatros first
arrived with its meshes joined into one object and `propAssembly` / `prop` left behind empty.
Nothing about the config looked wrong, but three things broke quietly:

* `StartPropeller` attached `PropellerSpin` to an empty transform, so nothing turned.
* `NoseLocal` found no renderer under the node and fell back to
  `Bounds(body.transform.position, one)` — the muzzle landed in the middle of the fuselage and
  the plane fired from its own cockpit.
* `ContactMeshes` had nothing to exclude, so the propeller — welded into the airframe mesh —
  joined the resting-pitch solve.

Pointing both fields at **null** is strictly better than pointing them at an empty node:
`NoseLocal` then falls back to the whole model and still puts the muzzle at the nose.
`PlaneFactory` logs the plane by name when it finds no propeller node at all.

## Adding a plane

The Albatros D.III went in without touching a line of `GarageController` or `GaragePlaneView`,
which is the bar a fourth plane should also clear:

1. Drop the FBX in `Assets/Resources/objects/planes/world_war_1/`. Its `.meta` must carry
   `isReadable: 1` or the plane gets a box hitbox instead of a mesh collider
   (`docs/standalone-builds.md`) — copy an existing plane's `.meta` and give it a fresh guid.
2. Add a `PlaneModelConfig` to `PlaneModels` and append it to `PlaneModels.All`, which is the
   `←` / `→` order. `resourceName` is the file's bare name and doubles as the id campaign
   scripts spawn it by, matched in full or on its first segment.
3. Name its propeller nodes in `propPivotNode` / `propBladesNode`, and **check they hold
   geometry** — see *Propeller nodes are per model* above for what an empty one costs.
4. Give it a `PlaneStats` block. It is what the player flies, not a spec sheet — see
   *The bars are real* above.
5. Skins, if any: `docs/plane-skins.md`. One skin means no `colour` row and the plane simply
   always wears it.

Everything else is derived: the pose is solved from the mesh, the framing from `onScreenSize`,
the badge from `type`, and the list length from `PlaneModels.All`.

## Files

| File | Role |
| --- | --- |
| `GarageController.cs` | Composes the screen, owns the plane index, refreshes everything from it, and holds `GoBack`. |
| `MenuInput.cs` | The shared reads, including `ReadPointer` for the drag (docs/touch-input.md). |
| `GaragePlaneView.cs` | The parked plane: pose, contact shadow, propeller ramp, plane swap. |
| `PlanePreviewRig.cs` | The camera + render texture + framing shared with `MenuPlaneView`. |
| `MenuStatRow.cs` | One stat row, as a bar or as a text value (captioned or bare). |
| `MenuBadge.cs` | The type badge: a filled rectangle sized to its own label, plus the country value anchored off its right edge. |
| `GroundShadowCatcher.shader` | The invisible ground that shows only the plane's cast shadow. |
| `GarageLighting.cs` | Ambient, sun and shadow distance for the scene, mirroring a level's atmosphere. |
| `PlaneSkin.cs` | The skins a plane can wear and how one is painted onto a model (`docs/plane-skins.md`). |
| `PlaneStats.cs` | The stat block a plane carries, and the bar list with its ceilings. |
| `PlaneLoadout.cs` | Turns the selected plane's stat block into the `PlayerConfig` it is flown with. |
| `PlaneModelConfig.cs` | Each plane's model, display name, country, description, stats and `garageZoom`; `PlaneModels.All` is the switch order. |
