# Campaign level scripts

A career level is driven by a **script**: a JSON list of operations run one after another. The
script owns the level's pacing — dialogue, pauses, enemy waves and the win condition — so
authoring a level is editing a data file, not editing C#.

It starts on the intro fly-in rather than at `Start`: `LevelIntro` cues it as the plane enters the
frame (docs/level-intro.md), so a script that opens on a `say` block opens the level as a scene.

**A script carries no prose.** Every string the player reads is a key into one shared line table
(below), so the script file stays a description of *what happens* and the writing lives in one
place.

## Where the files live

| File | Holds |
| --- | --- |
| `Assets/CampaignScripts/Resources/CampaignScripts/<name>.json` | One level's steps, loaded with `Resources.Load<TextAsset>("CampaignScripts/" + name)`. |
| `Assets/Dialogue/Resources/Dialogue/lines.json` | Every line of text in the campaign, keyed. |

The nested `Resources` roots date from when all of `/Assets/Resources` was gitignored, so a file
dropped in it would never be committed. Unity treats *any* folder named `Resources` under
`Assets` as a resource root, the same trick `Assets/Music/Resources/Music/*.json` and
`Assets/Fonts/Resources` already use. Today only the private art and audio subfolders are
excluded (docs/conventions.md), but these files stay where they are — the paths are baked into
`CampaignScript.ResourceFolder` and every level definition.

A level names its script on its definition: `CampaignLevels.Level1.script = "level1"`. All
eight career levels now carry one (`level1` … `level8`), so all eight can be *finished* — which
is what career progression is built on (docs/campaign.md). A level with no `script` — every
custom battle — behaves as before: endless flight with no dialogue, no waves and no win
condition.

**Level 1 is written; levels 2–8 are structure.** Level 1 speaks the three cutscenes of the
campaign's story source verbatim — eleven lines climbing out, ten after the first two Fokkers, ten
flying home — and its objectives and ground scene are written with them. The other seven are still
the same scroller shape each time (opening exchange → objective → waves → closing exchange →
`finish`) with placeholder Latin in every line, wave counts and enemy mix climbing from seven
Albatros scouts in level 2 to eleven mixed machines in level 8. Level 4 is the one variation,
using `spawn` + `waitclear` for a running fight instead of blocking waves. The real levels are
designed in `Assets/Resources/docs/campaign-ww1-scenario.md` — including four modes and two boss
fights this grammar cannot express yet.

## Grammar

The file is one object with a `steps` array; every step is an object with an `op` and whatever
that op needs. Ops are case-insensitive, unknown keys are ignored, and a step that cannot be read
is reported as `CampaignScript <origin>[<index>]: …` and dropped on its own, so a typo costs one
step rather than the whole level.

| Step | Effect |
| --- | --- |
| `{ "op": "wait", "seconds": 2.5 }` | Pause the script for N seconds. |
| `{ "op": "say", "speaker": "roussel", "line": "l1_line1" }` | Speak a line; the duration is derived from its word count. |
| `{ "op": "say", "speaker": "you", "line": "l1_line6", "seconds": 3.5 }` | Same, but hold it for exactly 3.5 s. |
| `{ "op": "task", "line": "l1_task1" }` | Show the current objective under the health bar. |
| `{ "op": "taskdone" }` | Tick the objective, cross it out and fade it away; blocks for the animation. |
| `{ "op": "wave", "enemies": [ { "plane": "albatros", "count": 2 } ] }` | Spawn the wave **and block** until every plane in it is destroyed. |
| `{ "op": "wave", "enemies": [ { "plane": "albatros", "count": 2 }, { "plane": "sopwith", "count": 1 } ] }` | A wave of mixed types. |
| `{ "op": "spawn", "enemies": [ … ] }` | Same spawn, but the script continues immediately. |
| `{ "op": "waitclear" }` | Block until no scripted enemy is alive (pairs with `spawn`). |
| `{ "op": "finish" }` | End the level, and stop reading the script. It does not open LEVEL COMPLETED directly any more — it starts the outro, which flies the patrol out and shows the ground scene and the journal first (docs/level-outro.md). |

`count` defaults to 1. Plane ids are matched against `PlaneModelConfig.resourceName` either in full
(`albatros_d3`) or by its first segment (`albatros`), so `PlaneModels` stays the one place a plane is
defined.

### The incoming warning (`EnemyWarning`)

`wave` and `spawn` do not put planes in the air the moment the runner reaches them. Both go
through `CampaignScriptRunner.Warn` first, so a wave always has a lead-in and the script author
never has to write one. The plane count is the sum over the wave's groups, so a mixed wave
counts once for all of it.

**A level shows the banner exactly twice**, and the runner — not the script — decides when:

| Trigger | Fires on |
| --- | --- |
| first encounter | the first `wave`/`spawn` of the level, whatever its count |
| first pair | the first wave whose total count is ≥ 2 |

Each trigger fires at most once, and a level that opens on a 2-plane wave spends both at once and
so shows a single banner. Every other wave gets a silent 1 s beat (`SilentWaveLeadSec`) instead of
the 2.6 s banner, which is why the middle of a level tightens up as it goes: the player has been
told what a wave looks like and what two of them looks like, and after that the planes just come.
The two flags live on the runner instance, so they reset with the level on a retry.

The overlay is a centred red-bordered plate reading `ENEMY PLANE IS INCOMING` for one plane and
`TWO ENEMY PLANES INCOMING` (`THREE`, `FOUR`, … up to eight, then the digits) for more. It
rises and scales into place over 0.3 s, holds 1.8 s and fades out over 0.5 s, and its text and
border pulse at 2.2 Hz throughout — the border between 65% and full alpha, the text between 40%
and full, so the lettering does the visible breathing and the frame only glows with it. The
whole 2.6 s is what `WarnIncoming` returns and the runner waits, so the planes appear just as
the plate leaves the screen.

It is built on the HUD canvas and handed to the `HudCurtain` like the task row, so a cutscene
starting on top of it hides it with the rest of the HUD rather than leaving it burning over the
cinematic bars.

It is the only banner in the game: the supply drop (docs/supply-drops.md) deliberately has no
HUD announcement of its own — the crate coming down the screen is its own cue.

Both files are read with `Json.cs`, the same small reader the music engine uses (docs/music.md) —
`JsonUtility` cannot deserialize a bare key/text map. JSON has no comments, so group steps with
blank lines instead; whitespace between steps is free.

Timings run on **scaled** time, so opening the pause menu (`Time.timeScale = 0`) freezes the
script mid-line and resuming continues it.

## The line table (`DialogueLines`)

`Assets/Dialogue/Resources/Dialogue/lines.json` is a flat map of key → text:

```json
{
  "l1_line1": "Vasseur, my right wing. Not that close — I'm fond of my wingtips.",
  "l1_task1": "Stay on Roussel's wing",
  "l1_after1": "Six machines, two hundred and ten rounds. The armourer counted them back out of your belt and then came and found me about it."
}
```

Keys are `l<level>_line<n>` for radio calls, `l<level>_task<n>` for objectives and
`l<level>_after<n>` for the lines of the ground scene played after the level (docs/level-outro.md)
— prefixing by level keeps one file usable for the whole campaign while staying greppable. It
holds 144 keys, one block per level, each used by exactly one script step or one entry in the
level's `outro`. Level 1's block is written; levels 2–8 are lorem ipsum apart from their `_task`
entries, which are plain objective text throughout, so an objective reads correctly on the HUD
while the dialogue is still visibly placeholder. Objectives and the ground scene share the table
with radio dialogue on purpose: it is the *displayed text* file, not the *speech* file, and a
translation pass wants all of it.

The `_after` lines are the one group the script never names — they hang off `outro` on the level
definition instead, because they are spoken after `finish` has stopped the runner.

`DialogueLines.For(key)` resolves a key, caching the parsed table on first use. A missing key logs
an error and returns **the key itself**, so the miss is visible on screen while the level keeps
running. Keys are resolved once, when the script is parsed, so nothing downstream knows they exist
— `CampaignStep.text` is the finished string, and `say` durations are still derived from the real
word count.

Adding a language later means a second file next to this one and a switch on which resource
`DialogueLines` loads; nothing else in the game reaches for a string.

## Speakers

`say` names a speaker id, not a display name. `CampaignSpeakers` maps the id to what appears on
screen and to whether the line is the player's:

| Id | Shown as | Player | Used by |
| --- | --- | --- | --- |
| `you` | VASSEUR | yes | everywhere — the player is Émile Vasseur in every level |
| `roussel` | ROUSSEL | no | level 1 |
| `marchand` | MARCHAND | no | level 1's ground scene |
| `crane` | CRANE | no | — |
| `lasalle` | LASALLE | no | level 1 |
| `ravensberg` | RAVENSBERG | no | — |
| `hq` | FLIGHT CONTROL | no | the placeholder levels |
| `wing` | BLUE TWO | no | the placeholder levels |
| `ace` | RED BARON | no | the placeholder levels |

The first six are the campaign's cast, named as the story source labels them on the wireless. The
last three are the generic placeholders levels 2–8 still speak through; they go when those levels
are written. `crane` and `ravensberg` are in the table ahead of the levels that need them, so a
script can be written without touching C#.

A new character is one entry in that array. An unknown id logs an error and falls back to the
player so the level still runs.

## The dialogue bar (`DialogueBar`)

Radio lines are spoken **inside the film bars**. A block of `say` steps raises the two black
cinematic bars (docs/level-intro.md), waits for the wingman to finish flying back into formation
(`CompanionReady`, docs/companion.md — instant on a level with no companion), holds them empty for
a 0.55 s lead-in, and only then starts typing; the next op that is not a `say` lowers them again.
The bars are the bar — there is no separate stripe any more.

**The two bars are not the same height.** `CinematicBars.Height` (150) is the top one; the bottom
is `BottomHeight` (214), enough to hold the 176 px avatar with the row's own 20/18 paddings above
and below it. Sizing the portrait to fit a 150 px bar instead capped it at 112, which read as a
thumbnail next to 28 px type, and growing both bars to fit one would have cropped the flying for
nothing — the top bar carries no content. The bottom bar is the one with something in it, so it
is the one that is sized to its contents; both still slide on the same `SmoothStep`, so they
arrive together.

The line itself sits in the bottom bar, 214 px tall at the 1920×1080 reference resolution: the
speaker's avatar in a left gutter, their display name on its own 28 px row — tinted blue for the
player and amber for anyone else, which is the only visual difference between the two — and the
message wrapping under it inside a 180 px side padding. Between two lines of the same block the
text is cleared and the bars stay put, so a conversation plays as one shot instead of flickering.

### The avatar (`CampaignAvatars`)

A square portrait sits to the left of the name and the text, 176 px and `preserveAspect`, anchored
to the **bottom** of the row 18 px up from the bar's lower edge. It is loaded by **speaker id**, so
an avatar is
added by dropping a file into a `Resources` folder and naming it after the speaker
(`roussel.png`, `marchand.png`, `you.png` for the player) — no wiring, no table to keep in step
with `CampaignSpeakers`.

`CampaignAvatars.For` probes four paths in order and caches the result — hit or miss — per
speaker: `Avatars/<id>`, `Avatars/<name lowercased>`, `<id>`, `<name lowercased>`. It asks for a
`Sprite` first and falls back to loading a `Texture2D` and building a full-rect sprite from it, so
a PNG left on the default texture importer works as well as one imported as a sprite.

**The gutter only exists when the portrait does.** With an avatar the row's left padding drops
from 180 to 40 and the text starts 238 px in — so the text column keeps most of the width it had,
and the avatar is paid for out of the margin rather than out of the line. With no avatar found,
the indent is zero and the row is the old full-width layout in a taller bar. `Split` measures the
width and height it is actually given, so the line breaks follow both the gutter and the bar. The same rule runs on the after-level journal
(docs/level-outro.md).

### A line too long for the bar

The bottom bar leaves 142 px for the message under the name row — about three wrapped lines of
28 px text, and only about one in the 78 px the bar had before it was made taller. Level 1 is
written with radio calls that run well past either. They used to spill straight out of the black
and over the game.

`DialogueBar.Split` measures the message against the bar it will actually be shown in — the live
`Text`, at the live width, *after* the avatar gutter has been applied for that speaker — and
returns the segments to speak one after another. Each is its own line: shown, typed, held and
skipped exactly like any other, so a long call reads as a pilot pausing for breath rather than as
a wall of text.

Splitting prefers **sentence boundaries**: the message is cut into sentences and they are packed
greedily into as few segments as fit, so a break lands on a full stop wherever one is available.
Only a single sentence that is too long for the bar on its own falls back to packing words. The
measurement is Unity's own (`GetPreferredHeight` against the generation settings the `Text` will
be generated with), not a character count, so it stays right whatever the resolution and whether
or not the speaker has an avatar.

`CampaignScriptRunner.Say` divides the step's duration between the segments in proportion to
their length, and each segment still gets the `HoldMin` (0.8 s) floor, so a split line takes
slightly longer overall than an unsplit one of the same `seconds` — which is the right answer for
a line that is now genuinely two beats. One tap of space advances one segment.

`DialogueBar` owns the `CinematicBars` instance, which is nested in the HUD canvas, so the
pause/fail/completed overlay hides the whole thing along with the rest of the HUD. While a line is
up the HUD itself is hidden, the ground cannot kill the player, and neither the bomb nor the boost
can be used — see "Cutscene mode" in docs/level-intro.md. The dialogue does not implement any of
that itself; the controller reads `CinematicBars.AnyShowing` each frame.

### Typewriter reveal

A line is not printed whole: it streams in left to right at **55 characters per second**, the pace
of a chat that is being typed rather than a machine that is stuttering. A ~100-character radio call
lands in under two seconds.

The reveal never reflows the paragraph. The full message is in the `Text` from the first frame and
the not-yet-revealed tail is wrapped in `<color=#00000000>`, so line breaks are decided once and
characters simply become visible in place — no word jumping down a line mid-sentence. The bar
repaints only on the frames where the whole-character count actually changes.

`DialogueBar` owns the state (`Show` arms a line, `Reveal(dt)` advances it, `IsRevealing` reports)
but does not tick itself — `CampaignScriptRunner` drives it from the `say` step, so the reveal runs
on the same scaled clock as the rest of the script and freezes with the pause menu.

A line's on-screen time is unchanged by the reveal: typing is spent *out of* the step's duration,
not added to it, and whatever is left after the last character is the hold. A line whose duration is
shorter than its typing still holds for a minimum of **0.8 s** afterwards, so the last word is never
snatched away the instant it appears.

### Skipping a line (space)

**One tap of space ends the current line** — mid-typing or mid-hold, either way — and the next one
starts immediately. It is one line per tap, not "skip the conversation": holding space does
nothing, because the read is edge-triggered (`MenuInput.ReadSkip`, space, the pad's south button,
or a screen tap on a touch device — docs/touch-input.md).

Only the two loops that hold a line on screen are skippable. The bars sliding in and the 0.55 s
lead-in are not, so a tap during the opening of a block cannot skip a line that has not been shown
yet. `CampaignScriptRunner.Skipped()` also refuses while the pause menu, the briefing or a screen
fade is up: coroutines still tick at `Time.timeScale = 0`, so without that guard a space press
aimed at the pause menu's buttons would eat a line behind it.

The same guard is why the press is recorded by frame (`_skipFrame`) and why a skipped line yields
one frame before returning. The runner walks its steps inside a single coroutine, so a skip that
returned immediately would let the *next* `say` read the same still-true `wasPressedThisFrame` and
cascade through the whole block on one tap.

## The current task (`LevelTask`)

One objective at a time, under the health bar in the top-left corner of the HUD: a stylised
checkbox followed by the objective in 26 pt bold, both on the same translucent black plate the
health bar uses, sized to the text. The row anchors to the canvas's top-left corner and sits at
`LevelHud.TaskCorner`, which is the bottom of the action column — so it follows the squares
wherever the safe area and the touch metrics put them (docs/hud.md). Only the night-only light
square changes that column's height.

The checkbox is a **round** 22 px ring with a 1.5 px stroke, drawn from a procedural sprite
(`UIFactory.RingSprite`) rather than assembled from rectangles: a 128×128 antialiased annulus
whose stroke is given as a fraction of the diameter, cached per thickness alongside the menu's
triangle sprite. The same generator draws the faint disc inside the ring — a stroke of 0.5 leaves
no hole, so a filled circle is the degenerate ring. The check mark is two rotated bars, sized as
fractions of the ring so the whole mark scales with `BoxSize`.

`task` slides the row in from the left and fades it up over 0.25 s. `taskdone` plays the
completion in one pass, on scaled time:

| Phase | Time | What happens |
| --- | --- | --- |
| tick | 0.20 s | The check mark pops into the box with a slight overshoot and the box frame turns green. |
| strike | 0.30 s | A green line draws itself across the text, left to right, while the text dims to grey. |
| hold | 0.25 s | The completed objective sits there so it can be read. |
| fade | 0.45 s | The row fades out and drifts right, then destroys itself. |

`taskdone` blocks the script for those 1.20 s, so the next `task` can never overlap the one leaving
the screen; a `task` issued while a row is still on screen replaces it outright.

The animation is driven from `Update`, not a coroutine, because the pause menu deactivates the
whole HUD — a coroutine on a deactivated object is killed for good, while `Update` picks up exactly
where it left off. Running it on scaled time means a pause freezes the cross-out mid-stroke and
resuming finishes it.

The script owns objectives the same way it owns dialogue: nothing in the level watches the enemy
count and writes the text. `ShowTask`/`CompleteTask` are two more members of `ICampaignScriptHost`,
and `CompleteTask` returns the seconds the runner should wait.

## Running a script (`CampaignScriptRunner`)

A component added to the level controller that walks the steps in a coroutine. The controller
implements `ICampaignScriptHost` (`IsOver`, `EnemiesAlive`, `CompanionReady`, `SpawnWave`,
`CompleteLevel`), which is
the whole surface between the script and the level — the runner never touches the plane, the
camera or the terrain.

The runner also decides when the film bars are up, because only it knows where a block of lines
ends: the first `say` raises them, every op that is not a `say` lowers them and blocks for the
0.5 s slide, and so does running off the end of the script. That is why a `task` reads best
*after* a conversation — issued before one, it would be hidden behind the top bar for the whole
block.

The runner stops on its own when the host reports the run is over (crash, ditch, shot down), and
the controller also calls `Stop()` explicitly so a `wait` in flight can't outlive the player.

## Scripted enemies (`CampaignEnemies`)

`EnemyController`'s AI was written for the fixed levels' static `MinX`/`MaxX` world bounds. In the
campaign the world scrolls forever, so `CampaignEnemies` re-points those bounds at the camera's
view window every `LateUpdate` (`EnemyController.SetBounds`, ±70 m inside the visible edges). The
AI's `FlightSteering.Contain` boundary then keeps the dogfight inside the frame no matter how far
the player has flown, and an enemy can never be left behind. A moving window makes the containment
choice matter more than it does in the fixed levels: the bounds hug the visible edges, so a
fighter is in a boundary band often, and the old rate-forcing `EdgeSteer` could park it nose-up
against the frame indefinitely (see docs/flight-model.md).

Waves spawn off-screen ahead of the player (camera edge + 110 m, each further plane 90 m behind the
last, so a group arrives strung out rather than as one clump) at a random altitude **inside the
plane's home altitude band** — the deck for scouts, the high band for fighters (docs/enemies.md) —
so a wave arrives already in position. The AI's ground reference is the terrain's maximum height on
land and the sea level on Flanders Coast, a flat conservative floor; the scout is the one exception
and raycasts the streamed ground under itself so its corridor can follow the contour.

`AliveCount` is what makes `wave` blocking; the list also drops planes destroyed by any other
means, so nothing can wedge the script permanently.

The plane named in a wave entry decides its **role**, through `PlaneModelConfig.enemyRole`:
`albatros` flies as a fighter, `fokker` as a scout. Each role has its own asset —
`EnemyScoutConfig` and `EnemyFighterConfig` — and the whole of docs/enemies.md is about what
that changes. Per-level difficulty is a pair of multipliers on `CampaignDefinition`, applied to
both role assets; `0` on either keeps the asset's own figure:

| Field | Level 1 | Level 8 |
| --- | --- | --- |
| `enemyHealthScale` | 0.50 | 1.00 |
| `enemyRotationScale` | 0.80 | 1.18 |

`CampaignEnemies` takes the whole definition and never touches the loaded assets — `EnemyConfigs.
Load` clones each one per level and the scaling edits the clone, since a Resources asset mutated
at runtime stays mutated for the rest of the editor session.

Both level 1 multipliers pull in the same direction: it is the tutorial for the guns, not a test of
them. Half health means the opening fight is short against the 150 health the player brings into
it, and the slower turn rate stops an enemy from simply rotating onto the player's tail faster
than a new player can answer — at 70°/s it no longer out-turns the Sopwith's own 120. Level 1 also
flies **only scouts**, so the tutorial is the deck fight and never the fighter's diving pass.

## Level 1 — Warming Engines

```
say  l1_line1 … l1_line11    cutscene 1, climb-out, over the intro fly-in
task l1_task1 → wait 2.5 → taskdone
task l1_task2 → wave fokker ×1 → wait 4 → wave fokker ×1 → taskdone
wait 1.5
say  l1_line12 … l1_line21   cutscene 2, the first one
task l1_task3 → wave fokker ×1 → wait 4 → wave fokker ×1 → wait 4 → wave fokker ×2 → taskdone
wait 1.5
say  l1_line22 … l1_line31   cutscene 3, home
finish                        → the outro (docs/level-outro.md)
```

Six machines in five waves, one at a time until the last: **cutscene → 1 → breather → 2 →
cutscene → 3 → breather → 4 → 5 and 6 together → cutscene**. Every enemy is a Fokker monoplane —
one gun and slower than the Sopwith — because level 1 is the tutorial for the guns and the deck
fight, not the fighter's diving pass; the Albatros does not exist for these men until June.
`companionFoe` is a Fokker too, so the duel Roussel fights in the background layer is the same
machine the player is being handed.

Every `wave` blocks, so each `wait` between two of them is a breather that starts when the previous
machine goes down, not a timer running under the fight. The two `wait 1.5`s are different — they
are the beat between the last kill of a phase and the radio opening up, so the cinematic bars don't
slide in over a falling wreck.

The three `say` blocks are the level's spine, and they are long on purpose: eleven lines is the
flying lesson, ten more turn the level by naming the pair that is coming, and ten fly it home. The
one pair in the level is the only wave with two machines in it, which is why cutscene 2 says out
loud that it is coming. A block that long is skippable a line at a time with space.

`finish` stops the script and hands over to the outro, which flies the patrol out, plays the
armourer's count on the ground and a page of the journal, and only then opens LEVEL COMPLETED.
Because one scene serves every campaign level (docs/campaign.md), **next level** bumps the static
`CampaignRun` and reloads `CampaignLevel1` rather than loading a different scene; it is disabled on
`CampaignRun.LastLevel`. Campaign completion deliberately does not touch
`GameManager.UnlockLevel` — that counter gates the fixed `Level1`/`Level2` scenes, and the career
level list is not gated by it.
