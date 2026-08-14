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
an overlay on the level behind it. Everything is centred on the 1920×1080 reference resolution:
caption at +272, title at +196 (62 pt bold), dateline at +130, a 96×4 accent rule at +86, the lore
block hanging from +34 at 1120 px wide with 1.5 line spacing, and the prompt at −336.

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

Any keyboard key, any face button or start on a pad, or a left click continues (`MenuInput.ReadAnyKey`).
Input is ignored while the screen fade is running, so the keypress that started the level from the
menu cannot fall through into it. Continuing fades to black (`ScreenFade.Swap`), restores the time
scale and the HUD at the black frame, and destroys the canvas.

`LevelBriefing.IsOpen` is checked wherever `GameMenu.IsOpen` already was — the controller (Escape
cannot open the pause menu underneath the briefing), `PlaneShooter` and `PlaneSearchlight` (the key
that dismisses the page cannot also fire a round or flick the light on, since *any* key dismisses
it). Restarting a level shows the briefing again.
