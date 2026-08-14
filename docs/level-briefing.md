# Pre-level briefing (`LevelBriefing`)

A full-screen page shown before a campaign level begins: the level's name, its dateline, a block of
lore, and `Press any key to continue...` with a blinking cursor. It is the screen the WW1 scenario
calls the loading screen (docs/campaign-ww1-scenario.md).

## Where the text comes from

Three fields on `CampaignDefinition`, authored next to the seed and the terrain:

| Field | Level 1 | Shown as |
| --- | --- | --- |
| — | — | `LEVEL 1`, the caption above the title (the run's level number, not a field) |
| `title` | `FIRST LIGHT` | the big centred title |
| `dateline` | `14 April 1916 — Verdun sector — dawn` | a muted line under the title |
| `lore` | lorem ipsum, two paragraphs | the body, split on a blank line |

The bodies are placeholder lorem ipsum for now; the real briefings are written in
docs/campaign-ww1-scenario.md, one per level.

A definition with an empty `title` shows no briefing, and **custom battles never show one** — they
drop straight into the flight as before.

## Layout

Its own canvas at sorting order 300 — above the HUD, below the screen fade (1000) — filled opaquely
with the active menu palette (docs/main-menu.md), so it reads as one of the game's menus rather than
an overlay on the level behind it. Everything is centred on the 1920×1080 reference resolution.

The block is **top-weighted**: caption at +452, title at +376 (62 pt bold), dateline at +310, a
96×4 accent rule at +266, and the lore hanging from +214 at 1120 px wide with 1.5 line spacing.
That puts the caption's top edge 70 px below the screen's, which is the padding — the page is not
flush to the edge, it just no longer floats in the middle with the lore running down into the
prompt.

The prompt does not sit at a fixed height. Lore text has `verticalOverflow = Overflow`, so it
renders past its rect and a constant would eventually be overrun by a long briefing — which is
exactly what used to happen. `BuildLore` returns its measured bottom (`LoreTop − preferredHeight`)
and the prompt is placed at `min(−400, bottom − 56)`, floored at −470 so it can never leave the
screen. With today's two-paragraph bodies the −400 default wins; a longer one pushes the prompt
down instead of colliding with the text. The blinking cursor follows the prompt's row.

The lore appears whole. Only the in-flight radio lines type themselves out
(docs/campaign-scripts.md).

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

Any keyboard key, any face button or start on a pad, or a left click continues (`MenuInput.ReadAnyKey`).
Input is ignored while the screen fade is running, so the keypress that started the level from the
menu cannot fall through into it. Continuing fades to black (`ScreenFade.Swap`), restores the time
scale and the HUD object at the black frame, fires `onDismissed`, and destroys the canvas. Note
that restoring the HUD *object* is not the same as showing the HUD: the campaign controller's
`HudCurtain` has its contents hidden for the fly-in and the opening conversation
(docs/level-intro.md), so the page hands over to an empty frame.

`LevelBriefing.IsOpen` is checked wherever `GameMenu.IsOpen` already was — the controller (Escape
cannot open the pause menu underneath the briefing), `PlaneShooter` and `PlaneSearchlight` (the key
that dismisses the page cannot also fire a round or flick the light on, since *any* key dismisses
it). Restarting a level shows the briefing again.
