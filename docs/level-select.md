# Level select

The career campaign picked from cards. It is the second screen after the era cards to leave
the column and take the whole canvas (docs/main-menu.md), because eight levels need the
width — and it is the only screen in the menu whose content **scrolls**.

Reached from `career` → `CHAPTER 1` → `level select`. `Escape` (or gamepad east) goes back
to the era page; like the era cards page, it carries no `back` entry — a card row is content,
and hanging a text entry under it would break the single row of focus.

```
   ┌──────────────────────── level select ─────────────────────────────┐
   │  FIRST LIGHT                                                      │  ← the focused card's title
   │  ───                                                              │
   │  14 April 1916                                                    │  ← its date
   │  lorem ipsum dolor sit amet, consectetur adipiscing elit …        │  ← its brief
   │                                                                   │
   │ ◀  ╔═════════╗┌─────────┐┌─────────┐┌─────────┐                 ▶ │
   │    ║  ╱╲__╱╲ ║│ ╱╲_╱╲__ ││ ╱╲__╱╲_ ││ ~~~~~~~ │                   │
   │    ║         ║│         ││         ││         │                   │
   │    ║FIRST    ║│THE      ││FIXED    ││THE      │                   │
   │    ║LIGHT    ║│NUMBERS  ││GROUND   ││RAVEN    │                   │
   │    ╚═════════╝└─────────┘└─────────┘└─────────┘                   │
   └───────────────────────────────────────────────────────────────────┘
     ↑ 44px from the screen edge — outside the 120px content margin
        ╚═╝ a cleared level keeps a green frame with no highlight on it
```

## The header

Title, accent rule, date, brief — rewritten from whichever card holds the highlight, the
same rule that makes the era cards page titled after its focused card. All three come
straight off the level's `CampaignDefinition`: `title`, `dateline`, and the **first
paragraph** of `lore` (`CampaignLevelEntry.Brief`), so the header and the pre-level briefing
page (docs/level-briefing.md) can never drift apart. The lore is still lorem ipsum, so the
brief reads as Latin until it is written.

The middle row is the **date alone** — `14 April 1916`, not
`14 April 1916 — Verdun sector — dawn`. `CampaignLevelEntry.Date` cuts the definition's
`dateline` at its first em dash, so place and light are dropped here and nothing is stored
twice; the full dateline still runs in the briefing (docs/level-briefing.md), where the
screen is about setting the scene rather than picking from a row. This page says where you
are in the war and lets the art say where you are on the map.

The titles and datelines are the campaign's own, taken from its story source
(`Assets/Resources/docs/campaign-ww1-scenario.md`, work in progress) — so a card names a real
level rather than `level 3`.

The date is a muted 18px row between the rule and the brief — the one metric this page
adds to the era page's header, and what pushes the card row 6px lower than the era row.

## The cards

A level card carries two things and nothing else — the terrain art and the level's name:

| Slot | Content |
| --- | --- |
| upper, full-bleed | the terrain silhouette (below), from `CardPad` down to the title row |
| foot | the title, `CardPad` up from the bottom, exactly where the era card now puts its own — wrapped to at most two lines, so `NOTHING BURNS AT NIGHT` fits the face without shrinking the type |

The foot is **always two lines tall**, on every card, whether the title uses them or not.
`LevelCardTitleRowHeight` is `2 × CardTitleLineHeight`, and `CardTitleLineHeight` (38) is
Poppins-Bold's own line height at `CardTitleSize` — the font declares 1050 ascender, −350
descender and a 100 line gap over 1000 units per em, so a 25px line is 37.5px tall and two
of them need 75. The row was 66, which is where `NOTHING BURNS AT NIGHT` ran out of card:
its first line had nowhere to go but up, into the art.

`LevelCardArtBottom` then adds `CardTitleToArt` on top of that row, the same 14px of air the
era card keeps over its own title, so the terrain stops clear of the type rather than resting
on it. Both numbers are the same on all eight cards, so the art baseline does not jump
between a one-line and a two-line title — the longest title in the campaign sets the grid
and the rest sit in it.

The level number, the map name and the `COMPLETED` / `LOCKED` tags are gone. The number was
the weakest thing on the card (the row is already in order, and the header names the level),
the map name repeated what the silhouette says, and both status tags now read off the frame
instead — which frees the whole upper area for art and drops `CampaignLevelEntry.MapName`
and `TerrainNames.Full` from the code.

### The three states

State lives entirely in the frame and the title colour:

| State | Frame | Title | On highlight |
| --- | --- | --- | --- |
| locked | `Muted`, only while highlighted | `Muted` | stays grey — nothing turns accent |
| next | `Accent`, only while highlighted | `Fg` | title and frame go accent |
| completed | `CardDone` green, **always** | `Fg` | frame and title go accent, the usual hover |

`CardDone` is a fixed sage green (`#5F9159`) held next to `CardFace` rather than inside the
palettes: like the white card face it means the same thing under every theme, and none of
the five palettes carries a success colour to theme it against. A cleared level therefore
wears its green until you point at it, at which point it behaves like any other card — the
green is a state of the level, not a second kind of highlight competing with the one you are
driving.

Everything on a locked card is still legible (title, art): the campaign is a list of places
you will go, not a mystery box. Only the colours change — a locked card's title, art and
focus frame all sit at `Muted`, hovered or not.

Locked cards **do** take the highlight, for the same reason locked era cards do: the header
above the row is there to describe what you are pointing at. They just do not activate.

## The terrain silhouette (`TerrainSilhouette`)

The card art is drawn at runtime, not shipped as an image: a `MaskableGraphic` that builds a
two-layer ridge profile in `OnPopulateMesh` from **the level's own terrain kind and seed**.
Two Verdun levels therefore get visibly different skylines while still reading as the same
land.

| Kind | Profile |
| --- | --- |
| `Verdun` | rolling ridge, plus three seeded shell craters — a gaussian dip with a small rim lip either side |
| `Flanders` | dunes on one side falling through a `SmoothStep` beach to a flat sea, the shoreline seeded |
| `Dolomites` | sharp triangular peaks (three in front, four behind) over a low base |

Each layer is a triangle strip of 110 columns from the rect's baseline to the sampled height,
so the whole card costs one mesh and no texture. The back layer is the front tint mixed 62%
into the card face, which is what gives the ridge its depth without a second colour token.
`Hash(index)` is a seeded integer hash, so a card's art is stable across runs and identical in
the editor and a build.

`PlaneEmblem` is the same idea for the **era cards** — a flat planform silhouette, nose right
like the menu's flying plane, one per era: biplane, single-wing fighter, swept jet, delta
with canards and twin tails. Each is a list of convex polygons fan-triangulated in unit
space and fitted uniformly into the card's art area, so it never stretches with the card.
It is now the *fallback*: an era with a baked render of its own aircraft shows that instead
(docs/main-menu.md), which today means Chapter 1 alone.

## Scrolling (`MenuLevelRow`)

Eight cards do not fit: `LevelVisibleCards` (4) are visible on the same pitch the era row
uses — `MenuTheme.CardSize`, which is fitted to the canvas width, plus the 40px gap
(docs/main-menu.md). `MenuLevelRow` is a `RectMask2D` viewport with a track inside it, and the track slides
one card at a time.

* the viewport is padded by `CardBorder` on all four sides and the track offset back by the
  same 4px, so the focus frame of the first and last visible card is not clipped away by the
  mask;
* `Slide(±1)` moves the window one card. The triangles at the screen edges call it — they are
  `MenuTheme.GarageArrowSize`, the garage's big arrows, placed `GarageArrowInset` (44px) from
  the canvas edges and centred on the row's own vertical middle. They sit **outside** the
  120px content margin, so they never collide with a card;
* the row does **not** wrap, and a triangle greys out (`Muted`) once there is nothing that way
  — the same end-of-list rule the custom battle selectors follow;
* `←`/`→` (and the d-pad) move the *highlight*, clamped at both ends, and `Reveal` drags the
  window along only when the highlight would leave it. Sliding with a triangle does the
  reverse: if the highlight ends up off-screen it is pulled to the nearest visible card, so the
  header always describes something you can see;
* the slide itself is a `Mathf.SmoothDamp` on the track's X in `Update`, 0.18 s, on
  **unscaled** time, snapped to the target inside half a pixel so it settles instead of
  creeping.

The highlight does not start at card one: opening the page focuses `CampaignProgress.NextLevel`
— your first uncleared level — and the window follows it, so a player eight levels in lands on
the card they were about to fly.

## Progress on the page

`CampaignProgress` (docs/campaign.md) decides all three card states when the page is built:
completed, unlocked-and-next, or locked. The menu scene is rebuilt on every load, so clearing
a level and returning to the menu is enough to re-read it — nothing on this page refreshes
itself in place.

There is **no dev bypass**. Level 6 is reached by clearing levels 1–5, or by clearing the
`mr_campaign_progress` PlayerPrefs key by hand.

## Files

| File | Role |
| --- | --- |
| `MenuLevelCard.cs` | One level card: number, art, foot, status tag, focus and lock colours. |
| `MenuLevelRow.cs` | The clipped viewport, the sliding track, the focus group and the reveal rules. |
| `TerrainSilhouette.cs` | The seeded ridge art on a level card. |
| `PlaneEmblem.cs` | The era cards' plane silhouettes. |
| `CampaignRun.cs` | `CampaignLevelEntry` / `CampaignLevelList` — the card data, derived from `CampaignLevels`. |
| `MainMenuController.cs` | Builds the page, the header, the edge arrows, and the era page's `continue`. |
