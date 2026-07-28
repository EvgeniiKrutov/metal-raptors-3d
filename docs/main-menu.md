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
| `IMenuFocusGroup.cs` | What the navigation keys drive — implemented by both `MenuPanel` and `MenuCardRow`. |
| `CareerEras.cs` | The four eras: title, years, description, unlocked. |
| `MainMenuController.cs` | Composes the column, the career pages, and reads navigation input. |
| `UIFactory.cs` | `CreateLabel` / `CreateInlineLabel` / `CreateBottomLabel` / `CreateParagraph` / `CreateRule` primitives and the project-wide font lookup. |

## Structure

```
┌────────── 40% ──────────┬─────────── 60% ───────────┐
│                         │                           │  ← 15% of the height
│  METAL RAPTORS          │                           │
│  ───                    │       (left empty)        │  ← 72×4 accent rule
│                         │                           │
│  career                 │      → era cards page     │
│  challenges             │      → challenges panel   │
│  custom battle          │      → Battlefield scene  │
│  online battles         │      (muted, no click)    │
│  options                │      (muted, no click)    │
└─────────────────────────┴───────────────────────────┘
 ↑ 120px left margin — the edge every row on every screen starts on
```

`challenges` replaces the list rather than expanding it. The title and its accent rule stay
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
   │  start        │            (left empty)              │  → CampaignLevel1 scene
   │  level select │                                      │    (muted, no click)
   │               │                                      │
   │  back         │                                      │  → main list
   └───────────────┴──────────────────────────────────────┘
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
* `start` becomes `continue` once campaign progress is tracked; `level select` is drawn
  muted until there is more than one level to pick.

## Weather

The menu no longer offers a weather (daytime) row. Levels carry their own atmosphere
instead: career level 1 is authored at dawn (`CampaignLevels.Level1.daytime =
Daytime.Morning`) and the challenge levels keep their definitions' defaults.

`GameManager.SetCampaignDaytime` / `SetLevel1Daytime` and their PlayerPrefs keys are still
there, and nothing writes them now — the plumbing waits for the selector to come back
(likely under `options`, or per-era once eras have their own weather).

## Themes

Five palettes live in `MenuTheme.Palettes`, indexed by `MenuThemeId`:

| Id | Name | Background | Accent |
| --- | --- | --- | --- |
| `Dusk` | Dusk Squadron (**active**) | `#E7DAE0` | `#B5687E` |
| `WW1` | canvas & brass | `#E7DEC9` | `#8A6B3A` |
| `WW2` | olive drab & insignia red | `#D9D6BE` | `#9E4A3C` |
| `ColdWar` | concrete & steel teal | `#D9DEE0` | `#4E7C8A` |
| `Modern` | HUD white & jet blue | `#EDEFF2` | `#3B7BB8` |

Each carries six tokens — `Bg`, `Fg`, `Muted`, `Accent`, `Panel`, `Border`. `Panel` and
`Border` are unused by the current flat design and are kept so a future boxed control
(a dropdown, a slider) inherits the right colours.

Switching is one assignment, `MenuTheme.Active = MenuThemeId.WW2`, made before the menu
scene loads. There is no UI for it yet; when `options` becomes real, that is where it
belongs (persist through `GameManager` / `PlayerPrefs` like the daytime settings).

Only the main menu uses these palettes. The Garage, the HUD and the level screens keep
their existing dark colours.

## Entry states

`MenuItemView` has two styles, and colour + weight are the only feedback — nothing moves
or changes size.

| | normal | focused | selected | disabled |
| --- | --- | --- | --- | --- |
| **Nav** (menu entries, levels, back) | `Fg` medium | `Accent` bold | — | `Muted` medium |
| **Option** (a row of choices) | `Muted` medium | `Fg` medium | `Accent` bold | — |

Selected beats focused for options, so a chosen value stays accent-lit while the highlight
moves over its neighbours. No screen uses the Option style at the moment — the weather
row that did is gone (see *Weather*) and `MenuPanel.AddOptionRow` waits for the next one.

A card is the same idea in two parts: its title is `Fg`, `Accent` when highlighted, `Muted`
when locked; its years stay `Muted`; and a 4px frame — `Accent`, or `Muted` when locked —
appears behind the face while it holds the highlight.

## Focus model

A focus group (`IMenuFocusGroup`: the vertical `MenuPanel`, the horizontal `MenuCardRow`)
owns a single focus index shared by every input device, so there is never more than one
highlighted entry:

* hovering an entry moves the focus to it (the focus does **not** clear on pointer exit —
  the template always shows one focused entry);
* `↓`/`→` and d-pad down/right step forward, `↑`/`←` and d-pad up/left step back, wrapping
  at both ends;
* `Enter`, `Space` or gamepad south activates the focused entry;
* `Escape` or gamepad east goes where the screen's own `back` goes — the main list, from
  challenges, from the era cards and from an era's page alike.

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

The two side insets are reference pixels, not fractions, because what sits inside them
(cards, glyphs) is measured in reference pixels too — a fractional margin would let the
card row overflow at an extreme aspect ratio:

* `PadLeft = 120` — every screen's left inset, and so the edge the whole menu is composed
  against. One constant moves the title, the entries, the description and the card row
  together, on every screen, because they all take it from `CreatePage`;
* `PadRight = 56` — no content is right-aligned, so this only keeps stretched rows off the
  screen edge.

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
