# In-game HUD

Both level types build the same HUD out of `LevelHud` (`Assets/Scripts/LevelHud.cs`), which owns
the health bar, the action column and the bottom hint line. `LevelController` and
`CampaignLevelController` only create it, hand it the four player components, and call `Tick()`
from their own `LateUpdate`. Everything it makes is a direct child of the HUD canvas, so
`HudCurtain` still hides the lot during a cutscene and `GameMenu` still hides the lot when paused.

## What is on screen

| Element | Where |
| --- | --- |
| Health bar | Top-left, hung from the top-left corner of the canvas. |
| Action column | Directly under the bar: **bomb**, **boost**, **fire** (touch only), and **light** on night levels — one square per row. |
| Pause | Top-**right** corner, touch only — a `P` square wired to the controller's `TryPause`. |
| Steering stick | Bottom-right corner, touch only — a ring on an invisible base (docs/mobile-steering.md). |
| Heading arrow | Orbiting the plane, touch only — a `>` at the heading the stick last set. |
| Objective hint | Bottom edge, centred, stretched to the canvas width less the side inset. |
| `Piloting: …` | Top-centre, on the authored levels only. |

The level title and the campaign's distance-in-metres readout were removed. The title said
nothing the pre-level briefing had not (docs/level-briefing.md), and the metre count was the only
reader of `_furthestX`, so both the field and the `Distance` property went with it.

## Colour scheme

`HudTheme` (`Assets/Scripts/HudTheme.cs`) holds the whole palette and every metric, so the health
bar and the squares cannot drift apart:

One light grey at two alphas, plus white, is the whole palette:

| Token | Value | Means |
| --- | --- | --- |
| `Fill` | white, opaque | ready — a live button's outline and caption, and the health left |
| `Idle` | grey `(0.88, 0.89, 0.91)` at **0.85** | reloading — the progress arc and the caption |
| `Charge` | the same grey at **0.42** | the clock wedge inside a reloading button |
| `Track` | the same grey at **0.28** | drained health, the button's body, and the arc's unlit track |
| `Ink` | near-black | the health number, which sits over `Fill` |

**Nothing has a plate or an inset border.** The health bar is two stacked rounded rectangles: the
root image is the translucent grey track, and a left-pinned white child, full height, scaled on X
by the health fraction. Damage uncovers translucent grey rather than turning the bar red — the old
green→red lerp is gone. The number stays `Ink`, since it sits over the white fill for the top half
of the range.

A `CooldownSquare` is **outlined, not filled**, and the cooldown reads on two layers at once —
a hand sweeping the interior and an arc walking the border, both `Radial360` from the top,
clockwise, off the same `fillAmount`. Four rounded layers in all:

| Layer | Shape | Rect |
| --- | --- | --- |
| body | filled, sliced | the whole square, `Track` |
| **wedge** | filled, radial | inset by `WedgeInset` (twice the stroke), `Charge` |
| outline | stroke, sliced | the whole square |
| **arc** | stroke, radial | the whole square, `Idle` |

`Set(charge, ready)` drives both fills together and recolours the other two:

| | Base outline | Wedge + arc | Caption |
| --- | --- | --- | --- |
| ready | `Fill`, opaque white | hidden — `fillAmount` 0 | `Fill` |
| reloading | `Track`, barely there | sweeping 0 → 1 | `Idle` |

So a live button is a crisp white rounded outline with a white letter in it; a reloading one fades
its outline to a ghost, and a faint clock hand turns inside while a brighter grey arc closes round
the perimeter — the two landing on full together and snapping back to white. **No stroke is ever
thicker than the border**: the arc is the border, and the wedge is held a full stroke-width clear
of it so the ring of body colour between them stays readable. The per-weapon amber and pale-blue
tints are gone.

## Rounded corners

`UIFactory.RoundedSprite(radius, stroke)` generates them, in the same cache-once way as the menu's
triangle and ring sprites. It rasterises a rounded-box **SDF** — `length(max(|p| − (half − r), 0)) − r`
— so one loop draws both variants: a `stroke` of 0 gives the filled body, and any other value
multiplies in a second edge at `d + stroke` to leave a hollow ring. The shape's outer boundary is
the texture boundary, so the sprite has no dead margin.

The texture is only `2·(⌈r⌉+1) + 4` px square, because it is **9-sliced**: the sprite carries a
border of `⌈r⌉+1` on all four sides, so the corners are drawn at their authored size and only the
4 px middle stretches. That is what lets one sprite serve a 400 × 38 health bar and a 108 × 108
button with the same corner radius, and it also keeps the radius in canvas units rather than a
fraction of the rect — sprite and canvas both sit at 100 pixels-per-unit, so a border pixel is a
canvas unit. `SpriteMeshType.FullRect` is required; the default `Tight` mesh would drop the
transparent corners the slicing needs.

| Metric | Desktop | Touch |
| --- | --- | --- |
| `BarRadius` | 6 | 9 |
| `SquareRadius` | 8 | 14 |
| `SquareOutline` | 2 | 4 |

Two things fall out of the slicing:

* **The health fill is sliced too**, so it is a rounded pill at both ends rather than a rounded
  left and a square cut. Below about 3 % health it is narrower than its own two borders and they
  overlap into a small nub, which is the whole extent of the artefact.
* **Neither radial layer can be sliced**, because `Image` allows one of `Sliced` and `Filled`, not
  both. Both take the second `RoundedSprite` overload, which rasterises at an **exact pixel size**
  and leaves the border at zero, so each is drawn `Simple` at 1:1 — the arc at `SquareSize` with
  the outline's stroke, the wedge at `SquareSize − 2·WedgeInset` with a correspondingly smaller
  radius and no stroke. That is only possible because a button is square and therefore scales
  uniformly, and it is what makes the arc land on exactly the same 2 px as the sliced outline
  underneath it. Hierarchy order is body, wedge, outline, arc, so the arc draws over the ghost
  outline rather than being washed out by it.

Fire and the searchlight have no cooldown, so they pass `charge: 0` and only ever swap the outline
between white and grey — hollow either way. The searchlight lost its own widget entirely; it is a
`CooldownSquare` switched by `PlaneSearchlight.IsOn`, and `SearchlightIndicator.cs` was deleted.

## Fitting a phone screen

Nothing is placed at a hand-tuned centre-relative coordinate any more. Every widget anchors to the
canvas corner it belongs to — the bar and the squares to `(0, 1)` with a `(0, 1)` pivot, the hint
to the bottom edge — and offsets from there.

That alone fixes the overflow: an iPhone canvas is about 2118 × 978 reference units, so the old
title at y 480 and the hint at y −500 were both outside the 978-unit height, and the bar's top
edge was clipped.

The offsets themselves come from `MenuTheme`'s safe area, which now carries all four sides.
`UIFactory.ScreenSafeInsets` divides `Screen.safeArea` by the canvas scale factor and hands
`MenuTheme.Fit` a `SafeInsets(left, right, top, bottom)`; the HUD reads `SafeLeft` for the column
and `SafeBottom` for the hint. In landscape the Dynamic Island eats ~147 units of one edge, which
is the side the health bar is on half the time, so the column starts at `SafeLeft + MarginSide`
rather than at a constant. The hint uses `max(SafeLeft, SafeRight) + MarginSide` on both sides so
it stays centred whichever way the phone is held.

## Touch metrics

`HudTheme` swaps a set of numbers on `MenuInput.IsTouchPlatform`, read once into `static readonly`
fields, the same shape `MenuTheme` already used for the menus. A desktop build computes exactly
what it did before.

| Field | Desktop | Touch |
| --- | --- | --- |
| `SquareSize` | 56 | **132** |
| `SquareGap` | 8 | **20** |
| `SquareRadius` | 8 | **16** |
| `SquareHitPad` | 0 | **10** |
| `SquareLabelSize` | 26 | **22** |
| `BarWidth` / `BarHeight` | 400 / 38 | **460 / 54** |
| `HintSize` | 28 | **34** |
| `MarginSide` | 100 | **44** |

132 units is not arbitrary. A canvas unit is about 0.4 pt on a landscape iPhone, so the desktop
56-unit square would be a 22 pt target — half of Apple's 44 pt minimum. 132 units lands at ~53 pt,
and the 10-unit hit pad takes the tappable area to ~60 pt. The pad is the same trick
`MenuArrowView.AddTouchPad` uses: a transparent `raycastTarget` image stretched past the square,
whose pointer events bubble up to the handler on the parent.

Even at four squares — the night case, plus the pause button off in the other corner — the column
runs to roughly 710 of the canvas's 978 units, which still leaves the column and the bottom hint
clear of each other.

`HudTheme.Label` also swaps the caption. A phone has no `H` key, so the squares read `BOMB`,
`BOOST`, `LIGHT` on touch and keep the single key letter on desktop. The bottom hint drops its
`A / D to steer • F to fire • …` prefix on touch for the same reason and shows only the objective;
the controller passes just that half and `LevelHud` prepends the key legend on desktop.

## The two touch-only squares

`HudTheme.IsTouch` gates both, so a desktop HUD is unchanged by either.

* **Fire** is hidden on desktop. It has no cooldown, so on a keyboard it would be a permanently
  white square carrying no information the `F` in the hint line does not already give. On touch it
  is the only way to shoot, and it is `holdable` rather than click-once — `HudPressRelay.Held` feeds
  `PlaneShooter.SetHeld`, ORed with the `F` key, so the existing `fireRate` still paces the shots.
* **Pause** hangs from the **top-right** corner rather than the column, since it is not a weapon and
  the left edge is where the Dynamic Island lands in one of the two landscape rotations. It is a
  plain outlined `P` — always `ready`, so always white — and `CooldownSquare` grew a `fromRight`
  flag that flips its anchor and pivot to `(1, 1)` for it. It calls the controller's `TryPause`,
  which is the same method `Escape` now routes through, so the guards (`_gameOver`, `GameMenu`,
  the briefing, `ScreenFade`) are shared rather than duplicated.

## Making the squares work

The squares are pressable on **every** platform — a mouse click does what a tap does — because the
plumbing is the same either way and branching it would only add a second path to get wrong.

`HudPressRelay` (`Assets/Scripts/HudPressRelay.cs`) is an `IPointerDownHandler` /
`IPointerUpHandler` on the square's root that raises `OnPressed` on the way down and tracks `Held`.
Down-edge is what bomb, boost and the light want; `Held` is what fire wants, since it repeats.
Unity delivers the pointer-up to whatever received the pointer-down, so dragging off the square
still releases it, and `OnDisable` clears `Held` for the case where the curtain or the pause menu
deactivates the HUD mid-press.

The actions themselves are not duplicated in the HUD. Each component grew one public entry point
that its own `Update` now calls too, so key and tap run identical code:

| Component | Entry point | Notes |
| --- | --- | --- |
| `PlaneBomber` | `Request()` | Checks `IsReady`, starts the cooldown, releases. `Update` calls it on `H`. |
| `PlaneBoost` | `Request()` | Checks `IsReady` and that no boost is running. `Update` calls it on `R`. |
| `PlaneShooter` | `SetHeld(bool)` | ORed with `fKey.isPressed`; the existing `fireRate` cooldown still paces the shots. |
| `PlaneSearchlight` | `Toggle()` | Same guard as the `T` path. |

`PlaneBomber` and `PlaneBoost` lost their separate `CinematicBars.AnyShowing` early-out in
`Update`, because `IsReady` — which `Request` consults — already carries it.

The **fire** square has no cooldown, so its sweep is always full; it only greys out when the
shooter is stopped (shot down, or the campaign fly-in, docs/level-intro.md). It is drawn on desktop
too, where it doubles as the legend for the `F` key next to `H`, `R` and `T`.

## Steering

`LevelHud` also builds the touch-only steering pair — a virtual stick in the bottom-right corner
and a `>` arrow orbiting the plane — and drives both from `Tick`, which is why the objective hint
still says nothing about steering: there are no keys to name. The stick, the arrow and the
heading-follow branch they feed in `CubeController` are all in docs/mobile-steering.md.
