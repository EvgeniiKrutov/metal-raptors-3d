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

The nested `Resources` roots are deliberate: `/Assets/Resources` is gitignored (private art and
sounds live there), so a file dropped in it would never be committed. Unity treats *any* folder
named `Resources` under `Assets` as a resource root, the same trick
`Assets/Music/Resources/Music/*.json` and `Assets/Fonts/Resources` already use.

A level names its script on its definition: `CampaignLevels.Level1.script = "level1"`. A level
with no `script` (level 2, and every custom battle) behaves as before — endless flight with no
dialogue, no waves and no win condition.

## Grammar

The file is one object with a `steps` array; every step is an object with an `op` and whatever
that op needs. Ops are case-insensitive, unknown keys are ignored, and a step that cannot be read
is reported as `CampaignScript <origin>[<index>]: …` and dropped on its own, so a typo costs one
step rather than the whole level.

| Step | Effect |
| --- | --- |
| `{ "op": "wait", "seconds": 2.5 }` | Pause the script for N seconds. |
| `{ "op": "say", "speaker": "hq", "line": "l1_line1" }` | Speak a line; the duration is derived from its word count. |
| `{ "op": "say", "speaker": "you", "line": "l1_line6", "seconds": 3.5 }` | Same, but hold it for exactly 3.5 s. |
| `{ "op": "task", "line": "l1_task1" }` | Show the current objective under the health bar. |
| `{ "op": "taskdone" }` | Tick the objective, cross it out and fade it away; blocks for the animation. |
| `{ "op": "wave", "enemies": [ { "plane": "fokker", "count": 2 } ] }` | Spawn the wave **and block** until every plane in it is destroyed. |
| `{ "op": "wave", "enemies": [ { "plane": "fokker", "count": 2 }, { "plane": "sopwith", "count": 1 } ] }` | A wave of mixed types. |
| `{ "op": "spawn", "enemies": [ … ] }` | Same spawn, but the script continues immediately. |
| `{ "op": "waitclear" }` | Block until no scripted enemy is alive (pairs with `spawn`). |
| `{ "op": "finish" }` | End the level: LEVEL COMPLETED overlay, and stop reading the script. |

`count` defaults to 1. Plane ids are matched against `PlaneModelConfig.resourceName` either in full
(`fokker_dr1`) or by its first segment (`fokker`), so `PlaneModels` stays the one place a plane is
defined.

Both files are read with `Json.cs`, the same small reader the music engine uses (docs/music.md) —
`JsonUtility` cannot deserialize a bare key/text map. JSON has no comments, so group steps with
blank lines instead; whitespace between steps is free.

Timings run on **scaled** time, so opening the pause menu (`Time.timeScale = 0`) freezes the
script mid-line and resuming continues it.

## The line table (`DialogueLines`)

`Assets/Dialogue/Resources/Dialogue/lines.json` is a flat map of key → text:

```json
{
  "l1_line1": "Lorem ipsum dolor sit amet…",
  "l1_task1": "Follow the flight leader"
}
```

Keys are `l<level>_line<n>` for radio calls and `l<level>_task<n>` for objectives — prefixing by
level keeps one file usable for the whole campaign while staying greppable. Objectives share the
table with dialogue on purpose: it is the *displayed text* file, not the *speech* file, and a
translation pass wants both.

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

| Id | Shown as | Player |
| --- | --- | --- |
| `you` | YOU | yes |
| `hq` | FLIGHT CONTROL | no |
| `wing` | BLUE TWO | no |
| `ace` | RED BARON | no |

A new character is one entry in that array. An unknown id logs an error and falls back to the
player so the level still runs.

## The dialogue bar (`DialogueBar`)

Radio lines are spoken **inside the film bars**. A block of `say` steps raises the two black
cinematic bars (docs/level-intro.md), holds them empty for a 0.55 s lead-in, and only then starts
typing; the next op that is not a `say` lowers them again. The bars are the bar — there is no
separate stripe any more.

The line itself sits in the bottom bar, 150 px tall at the 1920×1080 reference resolution: the
speaker's display name on its own 28 px row — tinted blue for the player and amber for anyone else,
which is the only visual difference between the two — and the message wrapping under it inside a
180 px side padding. Between two lines of the same block the text is cleared and the bars stay put,
so a conversation plays as one shot instead of flickering.

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

## The current task (`LevelTask`)

One objective at a time, under the health bar in the top-left corner of the HUD: a stylised
checkbox followed by the objective in 26 pt bold, both on the same translucent black plate the
health bar and the light indicator use, sized to the text. It sits at x −860 (the HUD's left
column) and at y 321, below the bomb and boost squares, which are always present (docs/bombs.md,
docs/boost.md) — so the column above it never changes height and the task never moves.

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
implements `ICampaignScriptHost` (`IsOver`, `EnemiesAlive`, `SpawnWave`, `CompleteLevel`), which is
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
last, so a group arrives strung out rather than as one clump) at a random altitude between the
ground's safe margin and 120 m under the ceiling. The AI's ground reference is the terrain's
maximum height on land and the sea level on Flanders Coast — a flat conservative floor, since the
streamed ground under an enemy is not sampled.

`AliveCount` is what makes `wave` blocking; the list also drops planes destroyed by any other
means, so nothing can wedge the script permanently.

## Level 1

```
say  l1_line1 … l1_line4     (hq / you / hq / you, over the intro fly-in)
task l1_task1 → wait 2.5 → taskdone
task l1_task2 → wave fokker ×1 (blocks until it is down) → taskdone
wait 2
say  l1_line5, l1_line6
wait 1
task l1_task3 → wave fokker ×2 (blocks until both are down) → taskdone
say  l1_line7, l1_line8
finish
```

`finish` stops the plane, stands the enemies down, and opens LEVEL COMPLETED. Because one scene
serves every campaign level (docs/campaign.md), **next level** bumps the static `CampaignRun` and
reloads `CampaignLevel1` rather than loading a different scene; it is disabled on
`CampaignRun.LastLevel`. Campaign completion deliberately does not touch
`GameManager.UnlockLevel` — that counter gates the fixed `Level1`/`Level2` scenes, and the career
level list is not gated by it.
