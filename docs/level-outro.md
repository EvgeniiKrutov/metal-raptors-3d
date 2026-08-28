# Post-level outro (`LevelOutro`)

A campaign level no longer cuts from the last radio line to LEVEL COMPLETED. It lands: the patrol
flies on, the camera stops and lets the aeroplanes go, the screen fades, and the player reads the
ground scene and a page of Vasseur's journal before being told they won.

This is the tail the story source designs — *cutscene 3 → `finish` → the ground scene → LEVEL
COMPLETED* — so the player reads what it cost before the menu appears.

## The order of things

| Beat | What the player sees | Owned by |
| --- | --- | --- |
| `finish` | The runner lowers the film bars and calls `CompleteLevel`. | `CampaignScriptRunner` |
| fly-on | 4 s. The stick is gone — the plane levels itself and flies straight. No HUD, enemies and the supply drop stand down, the ground is not lethal. | `CampaignLevelController.FlyOut` |
| fly-out | The camera stops dead. The player and the wingman carry on out past the right edge (capped at 4 s in case a slow machine never gets there). | same |
| fade | 1.2 s to black, with the engine and the wind fading down with the picture. | `ScreenFade`, `SoundSystem.FadeOut` |
| ground scene | The conversation on the field, on black, stacking up one line under the other. | `LevelOutro` |
| journal | A page of the journal on the menu's parchment, typed like the pre-level briefing. | `LevelBriefing.OpenJournal` |
| success | LEVEL COMPLETED. | `GameMenu` |

Each hand-over is the house 0.22 s fade; only the one that leaves the flight is slow.

## Where the text comes from

Two fields on `CampaignDefinition`, next to `lore`:

| Field | Holds |
| --- | --- |
| `outro` | `CampaignOutroLine[]` — a speaker id and a line key per line, resolved through `CampaignSpeakers` and `DialogueLines` when the page is built. |
| `journal` | The journal passage itself, as prose, the way `lore` is. |

The prose split follows what is already there: spoken lines are keyed into
`Assets/Dialogue/Resources/Dialogue/lines.json` (`l<level>_after<n>`, docs/campaign-scripts.md)
because they are dialogue with a speaker; the journal is a page of body copy and lives on the
definition beside the briefing's `lore`.

**The outro is not in the script.** The script grammar describes pacing the player can influence —
waves, waits, objectives — and the outro has none: it is a fixed sequence that begins the moment
`finish` runs. Putting it on the definition also means `finish` needs no payload and the runner
never learns the level is over twice.

A level with no `outro` goes straight to the journal; one with no `journal` goes straight to LEVEL
COMPLETED. Custom battles never reach any of it, having no script and so no `finish`.

Level 1 carries the written scene and journal from the campaign's story source. Levels 2–8 carry
five lorem lines and a lorem journal each, so the whole sequence is exercised from any level while
the writing is still to come.

## The fly-out

`CompleteLevel` no longer stops the plane. It stands the enemies and the supply drop down, hides
the dialogue bar, and calls `CubeController.FlyLevel` — heading steering on with a target of level
flight, which makes the plane roll out and hold it while ignoring the keyboard, the pad and the
touch stick. `_outro` folds into `Cinematic` for the rest of the level, so the HUD curtain stays
shut, the ground stays survivable and the wingman keeps formation.

The wingman is stood down at the *end* of the 4 s, not the beginning: standing down puts a
`DuelPlane` in `Idle` with no bounds, which is exactly the behaviour wanted for leaving the frame,
and doing it early would have dropped it out of formation while the player is still watching.

The camera halt is one flag on `LateUpdate`'s call to `PositionCamera` — the camera keeps its last
position and the plane simply outruns it. `PlaneGone` watches for the plane crossing the view's
right edge plus 120 m; `OutroExitMaxSec` is the backstop.

`SoundSystem.FadeOut(seconds)` is a master gain ramp multiplied into the pause gain, so the engine,
the enemy voices and the wind all come down together over the same 1.2 s as the picture. It sets
`_gameOver`, so nothing re-arms behind it.

## The ground scene

Its own canvas at sorting order 300 — the briefing's order, above the HUD and below the screen
fade — filled with opaque black. `Time.timeScale` is 0 for the whole page.

**Layout.** One column, inset from all four edges by the safe area plus 96 px at the sides, 84 px
at the top and 72 px at the bottom, with room reserved under it for the prompt. Everything is
left-aligned inside it and the column takes the full width it is given, so the page reflows with
the screen and keeps clear of an iOS notch or home indicator without a second set of constants.

**A line** is the speaker's square avatar in a left gutter, their name on its own 30 px row —
amber for anyone else, blue for Vasseur, the same two accents the in-flight dialogue bar uses —
and the line wrapping underneath the name. The portrait is 148 px, `preserveAspect`, and comes
from `CampaignAvatars` (docs/campaign-scripts.md), which loads it by speaker id out of a
`Resources` folder. Name and body are indented past it by the avatar plus 26 px; a speaker with
no avatar file gets no gutter and the old full-width layout. A row is never shorter than its
portrait, so a one-line remark keeps the same 148 px of air as a long one.

**The arrival** is a fade from zero with a 26 px rise over 0.4 s on a `SmoothStep`. Lines are held
for `CampaignScript.ReadingTime`, the same word-count formula that times a radio call, and the next
one arrives when that runs out. Nothing is typed here: the ground scene is read as a page, not
heard as a transmission, which is the difference between it and everything spoken in the air.

**The stack scrolls.** Lines are laid out downward from the top of the column and the column
carries a `RectMask2D`. Once the block is taller than the column, the whole stack slides up on an
exponential ease so the newest line is always in view and the oldest ones clip out of the top. The
scroll and the rise run at once, so a line that arrives into a full page fades in while the page is
still moving under it.

**Space skips one line**, mid-hold or mid-animation, the way it does in the film bars — it also
snaps every line already on screen to its finished position, so a fast reader is never left
watching an animation catch up. It is one line per press; the read is edge-triggered.

Two seconds after the last line, `Press any key to continue...` fades in over 0.35 s with the
briefing's blinking accent caret. Until it does the page cannot be dismissed.

## The journal page

The journal reuses `LevelBriefing` rather than repeating it: same parchment palette, same
typewriter reveal at 55 characters a second (2.2× for the body), same accent rule, same two-second
wait and blinking prompt, same `ScreenFade` close. `OpenJournal` picks a second layout preset — no
caption row, a 42 pt title instead of 62, and everything shifted up to fill the space the caption
left:

| | Level briefing | Journal page |
| --- | --- | --- |
| caption | `LEVEL 1` at +452 | — |
| title | the level's title, 62 pt, +376 | `JOURNAL OF É. VASSEUR`, 42 pt, +424 |
| dateline | the level's full dateline, +310 | the level's date alone, +366 |
| rule | +266 | +322 |
| body | the level's `lore`, from +214 | the level's `journal`, from +272 |

The date is `CampaignLevelEntry.DatePart` — everything before the first em dash — so a level whose
dateline still names its sector and its light shows only `22 June 1916` on this page.

Closing it fades and opens LEVEL COMPLETED. `GameMenu.Open` starts its own fade, but a fade is
already running at that point and `ScreenFade` runs a nested request inline, so the menu is built
on the black frame of the one fade rather than behind a second one.
