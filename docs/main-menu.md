# Main menu

The menu is a flat, chrome-less text column on the left of the screen — no buttons, no
panels, no gradients. It follows the HTML/CSS design template: a left 40% column holding
the title and the entries, and an intentionally empty right 60%. The column starts 15% of
the screen height down from the top.

**Everything is ragged-left**: every title, entry, caption, tag, rule and card in the menu
starts on the left edge of whatever holds it — the column, the page, or a card's own face
inset by its padding. Nothing is centred, so the title, the entries and the accent rule all
share one vertical edge to read down.

The era cards page is the same design widened: a full-canvas page hung from the same 15%,
because a row of four cards needs the width. It is the only screen that leaves the column —
every list of entries, career's included, stays in it.

Everything is built at runtime in C# (`MainMenuController`), like the rest of the game's
UI — the `MainMenu` scene only holds a camera, a light and the controller object.

## Files

| File | Role |
| --- | --- |
| `MenuTheme.cs` | The five colour palettes plus every layout metric (paddings, sizes, gaps). |
| `MenuItemView.cs` | One text entry; owns the colour/weight rules for its state. |
| `MenuPanel.cs` | A stack of entries with one shared highlight driven by mouse *and* keyboard. |
| `MenuCardView.cs` | One square era card: white face, title and years in its foot, accent frame while highlighted. |
| `MenuCardRow.cs` | A run of cards with one shared highlight; raises `FocusChanged` for the header above it. |
| `MenuSelectorRow.cs` | A value stepped in place: label column, then two triangles either side of the value. |
| `MenuArrowView.cs` | One triangle of a selector — clicks a step, greys out at the end of the list. |
| `MenuPreviewCard.cs` | A card that only shows: the picked map's square, its name in the foot, no focus and no click. |
| `MenuPlaneView.cs` | The flying plane in the main list's right band: the shared preview rig plus mouse-driven flight. |
| `PlanePreviewRig.cs` | The preview band itself — its camera, render texture and framing — shared with the garage. |
| `MenuStatRow.cs` | One garage stat row: a caption over a bar, or a caption over a text value. |
| `IMenuFocusGroup.cs` | What the navigation keys drive (`MenuPanel`, `MenuCardRow`) and, as `IMenuFocusable`, what a panel can highlight. |
| `CustomBattle.cs` | The maps a custom battle can pick, and the pick itself, read by the endless scene. |
| `CareerEras.cs` | The four eras: title, years, description, unlocked. |
| `MenuLayout.cs` | The column/page/band rects and the title + accent rule, shared with the in-level menu. |
| `MenuInput.cs` | The navigation keys (`ReadStep` / `ReadAdjust` / `ReadSubmit` / `ReadCancel`), shared with the in-level menu. |
| `ScreenFade.cs` | The fade to black between every two screens, in this scene or across a `LoadScene`. |
| `MainMenuController.cs` | Composes the column, the career pages, and reads navigation input. |

## Moving between screens

No screen here is swapped in directly. Going forward (`career`, an era card,
`level select`, `custom battle`) and going back (a `back` entry, `Escape`) both hand the
swap to `ScreenFade.Swap`, which fades the current screen to black, applies it, and fades
the next one up; leaving the scene (`garage`, a challenge, `start`, a campaign level, a
custom battle) hands the scene name to `ScreenFade.Load`. `Update` bails while
`ScreenFade.IsBusy`, so a key pressed mid-fade is dropped instead of stacking a second
transition on the first. `screen-fade.md` has the rest.
| `UIFactory.cs` | `CreateLabel` / `CreateInlineLabel` / `CreateBottomLabel` / `CreateParagraph` / `CreateRule` primitives and the project-wide font lookup. |

## Structure

```
┌────────── 40% ──────────┬─────────── 60% ───────────┐
│                         │                           │  ← 15% of the height
│  METAL RAPTORS          │                           │
│  ───                    │       (left empty)        │  ← 72×4 accent rule
│                         │                           │
│  career                 │      the flying plane     │
│  challenges             │      (muted, no click)    │
│  custom battle          │      → custom battle page │
│  garage                 │      → Garage scene       │
│  online battles         │      (muted, no click)    │
│  options                │      (muted, no click)    │
└─────────────────────────┴───────────────────────────┘
 ↑ 120px left margin — the edge every row on every screen starts on
```

The right band is no longer empty on the main list — it carries the plane preview below.
`career` still opens the era cards page, `custom battle` the custom battle page.

`garage` sits with the entries that actually do something, above the two muted ones, and
loads the `Garage` scene (`docs/garage.md`). It is a **main-menu-only** entry — the in-level
menu does not carry it, because the plane cannot be changed mid-flight.

`challenges` is muted for now — the panel below is built and reachable in one edit
(`BuildMainPanel` passes `interactable: false`), so this is what it opens when it comes
back. It replaces the list rather than expanding it: the title and its accent rule stay
put; only the column below them swaps.

```
  CHALLENGES

  level 1
  level 2   LOCKED

  back
```

A `LOCKED` tag is set after its entry on the same baseline, one `TagGap` past the entry's
measured width — both sit on the left edge's run.

`level 2` stays muted and unclickable, tagged `LOCKED`, until `GameManager.IsLevelUnlocked(2)`
returns true.

## Plane preview

The main list's right band holds the player's plane — **whichever plane is selected in the
garage** (`GameManager.CurrentPlane`) — flying nose-right and hanging in one place. It is
built by `MenuPlaneView` and shown on the **home screen only** (the main list and
challenges) — every other screen hides it, the custom battle band belonging to the map
preview card and the era cards page taking the whole canvas.

The band is the right `1 - ColumnFraction` of the canvas, full screen height (`0` to `1`) so
the plane has the whole vertical run of the window to move in. It has no left inset — the
column boundary itself is the band's left edge — and keeps `MenuTheme.PadRight` (56px) on
the right only. It is a `RawImage` created straight after the background image, so it sits
under every page in the canvas' draw order.

### How it is drawn

The overlay canvas paints over any world geometry, so the plane cannot simply be put in the
scene behind the background image. Instead `PlanePreviewRig` — shared with the garage's
parked plane, which needs the same trick with a different pose (`docs/garage.md`) — owns:

* a **camera of its own** rendering into a `RenderTexture` sized to the band's pixel rect
  (`rect × Canvas.scaleFactor`, rebuilt whenever that changes, so a window resize re-frames
  rather than stretches). It clears to `MenuTheme.Colors.Bg`, the same colour as the flat
  background — the band reads as part of the page, not as a picture pasted onto it;
* a **plane rig parked at y 5000**, far from anything else a scene may hold. The scene's own
  directional light lights it; nothing else is in the camera's view.

The camera sits ~20° off the side and 8° above (`ViewYawDeg` / `ViewPitchDeg`) — a slight 3/4
view, so the model reads as a 3D object and the bank shows wings and belly. Its distance is
solved from the band's aspect so the plane fills `FillWidth` (100%) of the visible width,
never less than `FillHeight` (95%) of the height; the framing therefore survives any window
shape. That framed height is then widened again by `VerticalMarginFraction` — headroom equal
to the rise and bob's combined travel range plus a small safety pad — so swinging the cursor
to the top or bottom of the screen never pushes the model past the top or bottom of the
frame.

The plane body is a **root** object, not a child of the rig, because `PropellerSpin` takes
its spin axis from `transform.root.right` — under a rig it would keep spinning about the
world axis while the plane yaws.

### Flight

The cursor drives it, anywhere on the screen, through `Mathf.SmoothDamp` on every channel —
that is what keeps the motion floaty instead of snapping to the pointer:

| Input | Effect | Extent |
| --- | --- | --- |
| cursor y | rise/fall + a little nose pitch | ±12% of the visible height, ±10° |
| cursor x | roll (belly / wings) + a little yaw | ±35°, ±12° |

The angles are bounded, so it never rolls past a bank into a barrel roll, and it always
eases back to level when the cursor returns to the middle. On top of that a permanent idle
layer — a two-frequency sine bob (±2% of the height) and a 3° sway — keeps it airborne when
the mouse is parked.

The plane does not fire in the menu — no muzzle is mounted and `PlaneShooter` is never
attached, unlike the in-level plane.

## Career

`career` opens the era cards across the full canvas; picking an era drops back into the
column, where the entries belong.

```
   ┌───────────────────── era cards ──────────────────────┐
   │  WORLD WAR 1                                         │  ← the highlighted card's title
   │  ───                                                 │
   │  lorem ipsum dolor sit amet, …                       │  ← its description
   │                                                      │
   │  ┌─────────┐┌─────────┐┌─────────┐┌─────────┐        │
   │  │         ││         ││         ││         │        │
   │  │         ││         ││         ││         │        │
   │  │ WORLD   ││ WORLD   ││ COLD    ││ MODERN  │        │
   │  │ WAR 1   ││ WAR 2   ││ WAR     ││ TIMES   │        │
   │  │ 1914–…  ││ 1939–…  ││ 1947–…  ││ 1991–…  │        │
   │  └─────────┘└─────────┘└─────────┘└─────────┘        │
   └──────────────────────────────────────────────────────┘
                          ↓  WORLD WAR 1
   ┌───── 40% ─────┬──────────────────────────────────────┐
   │  WORLD WAR 1  │                                      │
   │  ───          │                                      │
   │  start        │            (left empty)              │  → level 1, endless
   │  level select │                                      │  → the level list below
   │               │                                      │
   │  back         │                                      │  → main list
   └───────────────┴──────────────────────────────────────┘
                          ↓  level select
   ┌───── 40% ─────────────┬──────────────────────────────┐
   │  WORLD WAR 1          │                              │
   │  ───                  │                              │
   │  LEVEL SELECT         │                              │
   │                       │        (left empty)          │
   │  level 1   VERDUN     │                              │  → level 1
   │  level 2   FLANDERS…  │                              │  → level 2
   │                       │                              │
   │  back                 │                              │  → the era's page
   └───────────────────────┴──────────────────────────────┘
```

* The header is one title + one paragraph, rewritten from whichever card holds the
  highlight — so the page is titled after the card, exactly as `METAL RAPTORS` titles the
  column. The descriptions are placeholder lorem ipsum for now.
* Cards are white (`MenuTheme.CardFace`) whatever the palette; only their text and the
  highlight frame come from the theme. Images will go in the empty upper two thirds. A
  card's title and years read from its own left edge, inset by `CardPad` — the same rule as
  the column, applied to the card's face.
* World War 1 is the only unlocked era; the other three are muted and cannot be entered.
  Titles are set uppercase in `CareerEras`, matching `METAL RAPTORS`; a card title is the
  widest text the layout carries — `MODERN TIMES` runs 189px inside the 360px face.
* The era cards page has **no back entry** — `Escape` is the way back to the main list.
* `back` on an era's page returns to the **main list**, not to the cards: picking an era is
  a step on the way into career, not a layer worth landing on again.
* `start` becomes `continue` once campaign progress is tracked. `level select` swaps the
  column's panel in place, exactly as `challenges` swaps the main list — the era title and
  its accent rule stay put. Its rows carry the map name as a tag, the same `AddTag` the
  `LOCKED` marker uses, so a row states both the level and the land it flies.
* Nothing on this page is locked: both levels are reachable straight away, since the campaign
  has no completion condition yet.
* Both entries go through `CampaignRun.Request(n)` before loading `CampaignLevel1` — the one
  endless scene serves every level (docs/campaign.md). `start` is `level 1` by another name.
* `back` on the level list returns to the era's page, and `Escape` does the same, one layer
  at a time.

## Custom battle

One endless battle composed by hand: pick the map, pick the sky, fly it. Its screen is the
column plus one card in the right band — the first screen to use both halves.

```
   ┌────────── 40% ──────────┬───────────────────────────┐
   │  CUSTOM BATTLE          │  ┌───────────┐            │
   │  ───                    │  │           │            │
   │  map      ◀ verdun  ▶   │  │           │            │  ← the map's preview,
   │  weather  ◀ morning ▶   │  │           │            │    empty until its
   │                         │  │verdun | morning│       │    screenshot lands
   │  start                  │  └───────────┘            │  → CampaignLevel1, endless
   │                         │                           │
   │  back                   │                           │  → main list
   └─────────────────────────┴───────────────────────────┘
```

* A selector row is two columns, each reading from its own left edge: the label, then the
  control. The value column is a fixed `SelectorValueWidth`, so the right triangle holds
  still while the value changes under it.
* The labels (`map`, `weather`) are `Fg`, the same weight and colour as `start` and `back`
  — they name rows the player acts on, so they read as entries, not as captions.
* The list does **not** wrap. The triangle at either end greys out.
* `map` picks a `BattleMap` from `BattleMaps.All` — **verdun**, **flanders** and
  **dolomites**, each a name, a terrain seed and a `TerrainKind`; `weather` picks a `Daytime`,
  in the enum's own order. Neither is persisted: a custom battle's picks are not settings, so
  the screen opens on verdun/morning every time the menu is built.
* Flanders Coast is listed as `flanders` rather than its full name so it fits the 190px
  `SelectorValueWidth` that `verdun` was sized against; the full name is what career's level
  list tags its row with. `dolomites` (docs/dolomites.md) is the longest value the row
  carries and still fits at `ItemSize` 30.
* Dolomites is a custom-battle map only — no career level flies it.
* `start` fills in `CustomBattle` and loads the endless `CampaignLevel1` scene, where
  `CampaignLevelController` builds `CampaignLevels.Custom(map, daytime)` instead of the
  authored level. Career's own `start` calls `CustomBattle.Clear()` first, so an era keeps
  its authored atmosphere after a custom battle has been flown.
* The preview card is a `MenuPreviewCard` — the era card's white square and foot label
  without the frame, the hit box or the focus. Its upper two thirds are where the map's
  screenshot goes.
* The card's foot carries **both** picks, `map | weather` (`verdun | morning`), rebuilt by
  `MainMenuController.PreviewTitle` from either selector — so the card states the whole
  battle about to be flown, not just the land.

## Weather

Only the custom battle screen picks a sky. Everywhere else levels carry their own
atmosphere: career level 1 is authored at dawn (`CampaignLevels.Level1.daytime =
Daytime.Morning`) and the challenge levels keep their definitions' defaults.

`GameManager.SetCampaignDaytime` / `SetLevel1Daytime` and their PlayerPrefs keys are still
there, and nothing writes them — the custom battle deliberately goes through the
memory-only `CustomBattle` instead, so flying one changes no saved setting.

## Garage

The plane picker (`GarageController`), reached from `garage` in the main list and nowhere
else — **`docs/garage.md` is the page for it**. It is the menu's own design applied to a
whole screen: the column carries the plane's name, its stats as bars and `select plane`,
the right band carries the plane itself parked on the ground, triangles at the page edges
switch between the two planes, and a centred paragraph sits under both.

The pick writes through `GameManager.SetSelectedPlane` and is read back everywhere as
`GameManager.CurrentPlane` — the menu's flying plane and the player's plane in every level
are both built from it.

## Persistence

`GameManager` bootstraps itself with `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, so
`Instance` exists no matter which scene is pressed Play in first. It carries three kinds of
cross-scene state, each mirrored to `PlayerPrefs` on every setter and reloaded in `Load()`:
the selected plane (`mr_selected_plane`, chosen in the Garage), audio settings (master
volume), and progress (which levels are unlocked). The Level 1 / campaign daytime picks
above are persisted the same way, but nothing in the current menu writes them.

## Themes

Five palettes live in `MenuTheme.Palettes`, indexed by `MenuThemeId`:

| Id | Name | Background | Accent |
| --- | --- | --- | --- |
| `Dusk` | Dusk Squadron (**active**) | `#E7DAE0` | `#B5687E` |
| `WW1` | canvas & brass | `#E7DEC9` | `#8A6B3A` |
| `WW2` | olive drab & insignia red | `#D9D6BE` | `#9E4A3C` |
| `ColdWar` | concrete & steel teal | `#D9DEE0` | `#4E7C8A` |
| `Modern` | HUD white & jet blue | `#EDEFF2` | `#3B7BB8` |

Each carries six tokens — `Bg`, `Fg`, `Muted`, `Accent`, `Panel`, `Border`. `Border` is the
garage's stat bar track; `Panel` is unused by the current flat design and is kept so a
future boxed control
(a dropdown, a slider) inherits the right colours.

Switching is one assignment, `MenuTheme.Active = MenuThemeId.WW2`, made before the menu
scene loads. There is no UI for it yet; when `options` becomes real, that is where it
belongs (persist through `GameManager` / `PlayerPrefs` like the daytime settings).

The main menu, the garage (`docs/garage.md`) and the in-level menu (`docs/game-menu.md`) all
use these palettes — the garage takes `Border` for its stat bar tracks and `Accent` for
their fill, the first screen to use `Border` at all. Only the HUD keeps its own dark
colours.

## Entry states

`MenuItemView` has two styles, and colour + weight are the only feedback — nothing moves
or changes size.

| | normal | focused | selected | disabled |
| --- | --- | --- | --- | --- |
| **Nav** (menu entries, levels, back) | `Fg` medium | `Accent` bold | — | `Muted` medium |
| **Option** (a row of choices) | `Muted` medium | `Fg` medium | `Accent` bold | — |

Selected beats focused for options, so a chosen value stays accent-lit while the highlight
moves over its neighbours. No screen uses the Option style at the moment — the weather row
that did is now a selector instead, and `MenuPanel.AddOptionRow` waits for the next one.

A selector row follows the Nav rule on its value (`Fg`, `Accent` bold while focused) and
its label stays `Muted`. Its triangles are `Fg`, `Accent` while the row holds the highlight,
and `Muted` once there is nothing left that way.

A card is the same idea in two parts: its title is `Fg`, `Accent` when highlighted, `Muted`
when locked; its years stay `Muted`; and a 4px frame — `Accent`, or `Muted` when locked —
appears behind the face while it holds the highlight.

## Focus model

A focus group (`IMenuFocusGroup`: the vertical `MenuPanel`, the horizontal `MenuCardRow`)
owns a single focus index shared by every input device, so there is never more than one
highlighted entry:

* hovering an entry moves the focus to it (the focus does **not** clear on pointer exit —
  the template always shows one focused entry);
* `↓` and d-pad down step forward, `↑` and d-pad up step back, wrapping at both ends;
* `→`/`←` go to the focused entry first (`IMenuFocusable.Adjust`): a selector row spends
  them on its own value and keeps the highlight, everything else lets them move the
  highlight as `↓`/`↑` do. The card row reads across the screen, so they move it too;
* `Enter`, `Space` or gamepad south activates the focused entry;
* `Escape` or gamepad east goes where the screen's own `back` goes — the main list, from
  challenges, from the era cards and from an era's page alike.

A selector row's hit box is its whole row (label included), since the row *is* the control;
its triangles sit on top of that box and take their own clicks.

In a panel, disabled entries are never registered, so the highlight skips them entirely. A
card row is the exception: locked cards do take the highlight, because they are content —
the header above the row is there to describe the era you are pointing at.

Hit boxes are measured from the rendered text (in the bold weight, so the box does not
shrink when focus lands on the entry) — hovering the empty space either side of a word
does nothing. A card's hit box is its whole face.

## Metrics

The two metrics that decide where the menu sits are **fractions of the screen, not
reference pixels**, so they survive any aspect ratio — the canvas scaler matches width and
height equally, so a 1920-pixel constant would stop being 40% of the screen the moment the
viewport is not 16:9:

* `ColumnFraction = 0.4` — the column anchors from x 0 to x 0.4 of the canvas; the era
  cards page passes `1f` to the same `CreatePage` helper, and the one-era page passes
  `ColumnFraction` again;
* `PadTopFraction = 0.15` — its top edge anchors 15% of the height down.

The accent rule sits in equal air: `BarToList` is declared as `TitleToBar`, so the gap under
the rule matches the gap over it (22px), and the list starts that much below the rule on
every screen — the main menu's included.

The two side insets are reference pixels, not fractions, because what sits inside them
(cards, glyphs) is measured in reference pixels too — a fractional margin would let the
card row overflow at an extreme aspect ratio:

* `PadLeft = 120` — every screen's left inset, and so the edge the whole menu is composed
  against. One constant moves the title, the entries, the description and the card row
  together, on every screen, because they all take it from `CreatePage`;
* `PadRight = 56` — no content is right-aligned, so this only keeps stretched rows off the
  screen edge.

The custom battle screen is two bands inside one full-canvas holder, so both still measure
their fractions against the whole width: the column (`0 → ColumnFraction`, inset by
`PadLeft`) and the preview band (`ColumnFraction → 1`, no left inset — the split is already
its left edge, and the card hangs at the band's top corner).

Everything inside is a reference pixel against the canvas' 1920×1080. The proportions come
from the template's computed values at a 1920-wide viewport, but every text metric is
scaled ~1.3× from it — the template's sizes were laid out for a browser and read too small
across a room from a TV. Current values: a 44px bold title, 30px entries on a 54px pitch,
22px option rows, 14px uppercase captions, and for the era cards page a 940px-wide 20px
description over 360px cards on a 400px pitch, each card carrying a 25px title over 15px
years inset by its 28px `CardPad`. `MenuTheme` holds all of them; nothing in the layout
code hardcodes a number, so the next resize is one edit per metric there.

The card row is what the era page is sized around: four 360px faces plus three 40px gaps
run 1560px of the 1744px between the margins, from x 120 to x 1680, and the row hangs at
y 440–800 of the 1080 reference height. The column has 592px of inner width for a 358px
`METAL RAPTORS`.

Horizontal placement is by anchor, never by a stored width, and every anchor is a left
one — that is what makes the ragged-left rule hold at any width instead of needing a
measured centre:

* rows that span their parent (title, captions) stretch `0 → 1` and set their text
  `MiddleLeft`, so the glyphs start at the parent's left edge;
* entries, tags and cards anchor to their parent's top-**left** with pivot `(0, 1)`, so
  their `x` *is* their left edge and their own measured width runs rightward from it —
  hit boxes stay tight to the glyphs;
* runs (option rows, the card row) accumulate from `x = 0` rightward, so a run needs no
  total width and no second centring pass;
* a card's foot labels stretch its face and inset by `CardPad` on both sides
  (`sizeDelta.x = -2 * CardPad`), which is how they read from the face's left edge.

The description is the one place the menu wraps text instead of overflowing it
(`UIFactory.CreateParagraph`), so it needs a real width and a row height budgeted for four
lines.

Two template details do not survive the port to uGUI's legacy `Text`, which has no
tracking control: the `0.04em` title letter-spacing (~1.4px, invisible) and the `0.16em`
caption letter-spacing (noticeable on the 11px captions). Moving the menu to TextMeshPro
would restore both.

## Font

Poppins (Open Font License, `Assets/Fonts/`, licence text alongside it) is the font for
the **whole game**, not just this menu — `UIFactory` loads Regular / Medium / Bold and
every screen built through it now renders in Poppins.

The three weights sit in `Assets/Fonts/Resources/` rather than the usual
`Assets/Resources/`, because `.gitignore` excludes that folder as private content and
these files must ship with the repo. Any folder named `Resources` anywhere under `Assets`
is a resources root, so `Resources.Load<Font>("Poppins-Bold")` finds them either way.

The three weights are separate assets rather than `FontStyle.Bold` on one file: legacy
`Text` fakes bold by smearing the regular outline, so `UIFactory.CreateText` and
`CreateButton` map the requested `FontStyle` onto the real weight and pass
`FontStyle.Normal` to the renderer.

## Runtime plumbing

`UIFactory.EnsureEventSystem` creates the `EventSystem` through `InputSystemUIInputModule`,
not the legacy `StandaloneInputModule` — this project runs the Input System package, so the
legacy module never receives events. `AssignDefaultActions` wires the default UI input
actions so pointer clicks work without further setup; `CreateCanvas` calls it before
building anything else.

`CreatePrimitive3D` tears down a decorative primitive's collider with `DestroyImmediate`,
not `Destroy`, when `keepCollider` is false — `CreatePrimitive` always attaches one, and a
plain `Destroy` is deferred to end of frame, leaving the collider live for the rest of it.
Anything spawned on top of the player that frame (muzzle sparks, the Garage's preview
cubes) would otherwise register a bogus collision before the object is gone.
