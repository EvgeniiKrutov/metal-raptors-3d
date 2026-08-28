# Pre-level briefing (`LevelBriefing`)

A full-screen page shown before a campaign level begins: the level's name, its dateline and a block
of lore, all typed out like a radio transmission coming in, then `Press any key to continue...`
with a blinking cursor. It is the level's loading screen.

The prompt reads `Tap anywhere to continue...` on a mobile platform — `LevelBriefing.Prompt` is a
property, not a constant, picking between `KeyPrompt` and `TouchPrompt` on
`MenuInput.IsTouchPlatform` (docs/touch-input.md). The cursor is placed off the string's measured
width, so the longer sentence needs no other constant.

## Where the text comes from

Three fields on `CampaignDefinition`, authored next to the seed and the terrain:

| Field | Level 1 | Shown as |
| --- | --- | --- |
| — | — | `LEVEL 1`, the caption above the title (the run's level number, not a field) |
| `title` | `WARMING ENGINES` | the big centred title |
| `dateline` | `14 April 1916` | a muted line under the title |
| `lore` | the journal's *Before* page, three paragraphs and a signature | the body, split on a blank line |

All eight career levels carry a written `title` and `dateline` — the same two fields the level
select cards and their header read (docs/level-select.md). Level 1's `lore` is the written page;
the other seven are still placeholder lorem ipsum, two paragraphs each, waiting for the same
treatment. The card on the level-select page shows the body's first paragraph
(`CampaignLevelEntry.FirstParagraph`), so writing a level's `lore` writes its card blurb too.

Level 1's dateline is the **bare date**. The other seven still read as
`22 June 1916 — Verdun sector — high midday`, and the level-select card has always shown only the
part before the first em dash (`CampaignLevelEntry.DatePart`), so shortening the field changes the
briefing and nothing else.

A definition with an empty `title` shows no briefing, and **custom battles never show one** — they
drop straight into the flight as before.

## The same page, twice

`LevelBriefing` also draws the **journal page** at the end of a level (docs/level-outro.md).
`OpenJournal(title, dateline, text, onDismissed)` is the same build with a second layout preset:
no caption row, a 42 pt title instead of 62, and the dateline, rule and body shifted up into the
space the caption left. Everything else — the parchment palette, the typewriter reveal, the space
skip, the two-second wait, the blinking caret, the closing fade — is shared, which is the whole
reason the outro does not have a page class of its own.

`_hud` is null on that call: there is no HUD left to hide or restore by then.

## Layout

Its own canvas at sorting order 300 — above the HUD, below the screen fade (1000) — filled opaquely
with the active menu palette (docs/main-menu.md), so it reads as one of the game's menus rather than
an overlay on the level behind it. Everything is centred on the 1920×1080 reference resolution.

The block is **top-weighted**: caption at +452, title at +376 (62 pt bold), dateline at +310, a
96×4 accent rule at +266, and the lore hanging from +214 at 1180 px wide, 24 pt, with 1.15 line
spacing. That puts the caption's top edge 70 px below the screen's, which is the padding — the page
is not flush to the edge, it just no longer floats in the middle with the lore running down into
the prompt.

The body is **set tight**: it was 26 pt at 1120 px with 1.5 line spacing, which is comfortable for
two paragraphs of lorem and impossible for the real thing. A written page runs three or four
paragraphs, and at the old metrics level 1's ran about 875 px from +214 — past the prompt and off
the bottom of the screen. Narrower leading and a slightly wider, slightly smaller measure bring the
same text to roughly 550 px, which clears the prompt at −400 with room to spare and still leaves
the body a step larger than the dateline. The same three constants set the journal page, so both
written pages tighten together.

The prompt does not sit at a fixed height. Lore text has `verticalOverflow = Overflow`, so it
renders past its rect and a constant would eventually be overrun by a long briefing — which is
exactly what used to happen. `BuildLore` returns its measured bottom (`LoreTop − preferredHeight`)
and the prompt is placed at `min(−400, bottom − 56)`, floored at −470 so it can never leave the
screen. With today's two-paragraph bodies the −400 default wins; a longer one pushes the prompt
down instead of colliding with the text. The blinking cursor follows the prompt's row.

## The print

Nothing on the page is there when it opens. The four text blocks type themselves out in order —
caption, title, dateline, lore — the way the in-flight radio lines do (docs/campaign-scripts.md),
at the same 55 characters a second, except the lore which runs at 2.2× that: at one rate a
two-paragraph briefing takes nine seconds to land on its own, and the body is the part the player
is reading rather than watching. A 0.35 s beat separates the blocks, 0.5 s after the dateline,
where the accent rule flicks on — it is the one element that appears whole, being four pixels tall.

The reveal is the `<color=#00000000>` trick: the full string is in the `Text` from the start and
the untyped tail is painted transparent, so the layout is final on the first frame. Typing by
appending would re-centre every line on every character, and would have invalidated the lore
measurement the prompt row is placed from.

Printing runs on `Time.unscaledDeltaTime` — the page has already frozen the game — and does not
start until `ScreenFade.IsBusy` clears, so the first characters are not typed behind the black
frame of the fade that brought the level in.

Once the last character lands the page waits **two seconds** before the prompt and its cursor fade
in over 0.35 s. Until they do, the page cannot be dismissed at all: `Update` returns before it
ever reads a key. The one exception is **space** (or a pad's south button, or a tap, `MenuInput.ReadSkip`),
which completes the print and skips the two-second wait, putting the prompt up at once — a testing
shortcut, not a designed affordance, and the reason the prompt still has to be dismissed with a
second press.

## The cursor

A filled 12×26 accent-coloured block sitting after the prompt's last character, blinking hard on and
off every **0.5 s** on *unscaled* time — the briefing freezes the game with `Time.timeScale = 0`, so
a scaled clock would leave the cursor dead on the screen.

The prompt is centred, so the block cannot be part of the string without shifting the sentence every
time it toggles. It is a separate `Image` placed at `preferredWidth / 2 + gap` from the centre, which
is measured once when the page is built.

## Lifetime

`LevelBriefing.Open(...)` is the last thing `CampaignLevelController.Start` does — the world, the
HUD and the script runner are all built behind it. Opening the page hides the HUD and sets
`Time.timeScale = 0`, which is what actually holds the level: the script runner's waits and the
dialogue reveal both run on scaled time, and physics does not step, so nothing has happened when the
player finally looks up.

The level is also **silent** behind the page: `SoundSystem` is begun with `silent: true` and holds
every source until `Open`'s `onDismissed` callback arms it at the black frame of the closing fade
(docs/sounds.md). Freezing time would not have done it — the sound system runs on unscaled time —
and an engine droning under a static briefing page reads as a bug.

Once the prompt is up, any keyboard key, any face button or start on a pad, a left click or a
screen tap continues (`MenuInput.ReadAnyKey`); before that only space or a tap skips the print, as
above. A single tap never does both: the skip branch returns for that frame, and the press is
edge-triggered, so it is gone by the next one. Input is
ignored while the screen fade is running, so the keypress that started the level from the
menu cannot fall through into it. Continuing fades to black (`ScreenFade.Swap`), restores the time
scale and the HUD object at the black frame, fires `onDismissed`, and destroys the canvas. Note
that restoring the HUD *object* is not the same as showing the HUD: the campaign controller's
`HudCurtain` has its contents hidden for the fly-in and the opening conversation
(docs/level-intro.md), so the page hands over to an empty frame.

`LevelBriefing.IsOpen` is checked wherever `GameMenu.IsOpen` already was — the controller (Escape
cannot open the pause menu underneath the briefing), `PlaneShooter` and `PlaneSearchlight` (the key
that dismisses the page cannot also fire a round or flick the light on, since *any* key dismisses
it). Restarting a level shows the briefing again.
