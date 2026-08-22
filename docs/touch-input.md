# Touch input (iOS)

Everything the menus read goes through `MenuInput`, so touch was added there rather than at
each call site: the briefing, the campaign dialogue and the garage's plane drag all got it
without knowing a screen was tapped. Nothing is `#if UNITY_IOS`-gated — the reads ask the
Input System for a `Touchscreen` and get `null` on a desktop, which is the same shape as the
existing `Keyboard.current` / `Gamepad.current` guards.

The project runs on the Input System package alone (`activeInputHandler: 1`), and there is no
project-level input settings asset, so the default device list applies and `Touchscreen` is
present on iOS with no further setup.

## What was added

| Member | Reads |
| --- | --- |
| `MenuInput.ReadTap()` | `Touchscreen.current.primaryTouch.press.wasPressedThisFrame` — a finger going down, edge-triggered like every other read here. |
| `MenuInput.ReadPointer()` | The `MenuPointer` struct: position, held, pressed-this-frame. Returns the touch while a finger is down, otherwise the mouse, otherwise `default` (nothing held, position zero). |
| `MenuInput.IsTouchPlatform` | `Application.isMobilePlatform`, for **wording only** — never for gating a read. Unity's Device Simulator overrides it, so a simulated iPhone in the editor shows the touch strings too. |

## Where a tap lands

* **`ReadAnyKey`** — so `Press any key to continue...` on the pre-level briefing is dismissed
  by tapping anywhere (docs/level-briefing.md). The prompt itself is reworded to
  `Tap anywhere to continue...` on a mobile platform, which is the one place `IsTouchPlatform`
  is used.
* **`ReadSkip`** — so a tap completes the briefing's print, and skips a line of campaign
  dialogue (docs/campaign-scripts.md). A single tap cannot do two things at once in the
  briefing: while it is still printing, `Update` returns after the skip, and the press is gone
  by the next frame.
* **`ReadPointer`** — the garage's drag-to-turn, so the plane can be swiped around
  (docs/garage.md). A lifted finger reports no pointer, which the drag reads the same as a
  released mouse button.

`ReadSubmit` and `ReadCancel` deliberately take **no** tap:

* Submit would double up. uGUI already delivers taps as pointer clicks — the default UI action
  map `InputSystemUIInputModule.AssignDefaultActions()` installs binds `<Touchscreen>/touch*/press`
  and `/position` — so every `MenuItemView`, `MenuSelectorRow` arrow and `MenuArrowView` is
  tappable through `IPointerClickHandler` already. A tap-to-submit read on top of that would
  fire the focused row as well as the row actually touched.
* Cancel has no gesture to bind. `Escape` is what leaves a screen on desktop, and a phone has
  nothing equivalent, so screens that can be left need a visible way out instead — which is why
  the garage gained a `back` row (docs/garage.md).

## Mobile layout

`MenuInput.IsTouchPlatform` also drives a set of metric swaps in `MenuTheme`, read once into
`static readonly` fields when the type initialises — so a desktop build computes the same
numbers it always did, and every screen that composes itself out of `MenuTheme` picks the
touch ones up without knowing why.

| Field | Desktop | Touch | Applies to |
| --- | --- | --- | --- |
| `TextScale` | 1.0 | **1.4** | `ItemSize` / `ItemRowHeight` / `ItemGap`, `OptionSize` / `OptionRowHeight` / `OptionGap` — the interactive rows, which are also their own hit boxes |
| `WidthScale` | 1.0 | **1.2** | the selector row's label, value and arrow-gap widths |
| `ArrowScale` | 1.0 | **1.4** | `SelectorArrowSize`, `GarageArrowSize` |
| `ArrowPad` | 0 | **20** | an invisible hit area around every triangle |
| `PadTopFraction` | 0.15 | **0.10** | the top band every page hangs from |

The metrics that used to be `const` are now properties over a `…Base` constant, which is why
`GarageController.ColourRowHeight` had to become a property too — a `const` cannot be built
out of one.

Three of those five want explaining:

* **Widths scale by half.** A 1.4× `colour` row would be 644px wide against the garage
  column's 592px of inner width, so the arrows would end up over the plane. Text grows by
  1.4 and the columns it sits in by 1.2, which lands the row at 559px and still fits the
  longest value (`Dolomites`, ~212px at the scaled size).
* **The arrows get an invisible pad, not just a bigger glyph.** A 1.4× page triangle is still
  only ~17pt across on a phone. `MenuArrowView.AddTouchPad` parents a transparent
  `raycastTarget` image to the triangle, stretched `ArrowPad` past it on all four sides;
  pointer events on it bubble to the `MenuArrowView` on the parent, so nothing about
  positioning or state changes. It is the same trick `MenuSelectorRow`'s own `Hit Box`
  already used. At 20px the selector arrows grow to 68px tall inside a 70px row pitch, so
  neighbouring rows still do not overlap.
* **The top band shrinks.** A phone canvas is ~2120×978 reference units — wider and *shorter*
  than the 1920×1080 desktop one — while the rows on it are 40% taller. The garage column is
  the tightest screen in the game (badge, six stat bars, `colour`, `select plane`, `back`),
  and at 0.15 its last row would sit on the home indicator. 0.10 buys back 49px and leaves
  `back` ~37pt clear of the bottom edge.

## The notch and the Dynamic Island

`Screen.safeArea` is read in `UIFactory.CreateCanvas`, divided by the same scale factor the
canvas size is computed with, and handed to `MenuTheme.Fit` as a `SafeInsets` — all four sides,
since the in-game HUD needs the top and bottom edges too (docs/hud.md).

In landscape the island eats about 147 canvas units of one edge — and the page triangles sit
at `GarageArrowInset` (44), so one of them was underneath it. Three things take the inset now,
each by its **own** side's value, so a rotation moves whichever side the island is actually
on:

* **the triangles** — `GarageController` and `MainMenuController` add `SafeLeft` / `SafeRight`
  to the 44px inset;
* **every page** — `MenuLayout.CreatePage` insets by `MenuTheme.PageInsetLeft` /
  `PageInsetRight`, which is the page margin widened to the arrow lane, plus the safe inset.
  That is what keeps the title, the entries and the card rows out from under the island, and
  it is also what keeps a full-width card row from sliding under a triangle
  (docs/main-menu.md);
* **the garage column**, which does not use `CreatePage`: `GaragePadLeft` is
  `max(200, SafeLeft + GarageArrowInset + GarageArrowSize.x + GarageArrowToColumn)`. On a
  desktop the 200 wins and nothing moves; on an iPhone it resolves to 313, which still leaves
  479px of column for the 460px stat bars. That headroom is the reason the column takes a
  `max` against the arrow rather than the plain sum a page takes — the sum (347) would put the
  bars over the plane.

## Still keyboard-only

* **Leaving the two card screens.** The career (`Eras`) and level-select pages are
  `MenuCardRow` / `MenuLevelRow` rows with no `back` entry — `MainMenuController.Cancel` is
  the only way out of either (docs/main-menu.md, docs/level-select.md). The panel-based pages
  (`era`, `custom battle`, `challenges`) all have one, and the garage now does too.
* **Steering.** `CubeController` reads `A` / `D` off `Keyboard.current`; there is no on-screen
  stick yet, so the touch build's bottom hint says nothing about steering rather than naming keys
  a phone does not have.

The pause menu used to be on this list — `ReadCancel` is `Escape`-only and a phone has no key for
it. A touch build now puts a `P` button in the HUD's top-right corner instead, calling the same
`TryPause` the key does (docs/hud.md). Its rows were always tappable once open.

## In-game controls

The weapon squares in the HUD's top-left column are tappable — bomb, boost, fire and, on night
levels, the searchlight — plus a pause button in the top-right corner. `PlaneShooter`,
`PlaneBomber`, `PlaneBoost` and `PlaneSearchlight` each gained one public entry point that both the
key and the tap go through, and `HudTheme` grows the squares to a ~60 pt hit target on touch. Fire
and pause exist **only** on touch. The full account, including why the column had to move off the
Dynamic Island's edge, is in docs/hud.md.
