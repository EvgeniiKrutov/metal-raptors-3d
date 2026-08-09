# Campaign level scripts

A career level is driven by a **script**: a plain-text list of operations run one after another
from the moment the scene starts. The script owns the level's pacing — dialogue, pauses, enemy
waves and the win condition — so authoring a level is editing a text file, not editing C#.

## Where scripts live

`Assets/CampaignScripts/Resources/CampaignScripts/<name>.txt`, loaded at runtime with
`Resources.Load<TextAsset>("CampaignScripts/" + name)`.

The nested `Resources` root is deliberate: `/Assets/Resources` is gitignored (private art and
sounds live there), so a script dropped in it would never be committed. Unity treats *any*
folder named `Resources` under `Assets` as a resource root, the same trick
`Assets/Music/Resources/Music/*.json` and `Assets/Fonts/Resources` already use.

A level names its script on its definition: `CampaignLevels.Level1.script = "level1"`. A level
with no `script` (level 2, and every custom battle) behaves as before — endless flight with no
dialogue, no waves and no win condition.

## Grammar

One operation per line. Blank lines are skipped, and `#` starts a comment line. The keyword is
case-insensitive; parse errors are reported per line as `CampaignScript <origin>:<line>: …` and
that line alone is dropped, so a typo costs one step rather than the whole level.

| Line | Effect |
| --- | --- |
| `wait 2.5` | Pause the script for N seconds. |
| `say hq: Lorem ipsum…` | Show a dialogue line; the duration is derived from the word count. |
| `say you 4: Lorem ipsum…` | Same, but hold it for exactly 4 seconds. |
| `wave fokker x2` | Spawn the wave **and block** until every plane in it is destroyed. |
| `wave fokker x2, sopwith x1` | A wave of mixed types; comma-separated groups. |
| `spawn fokker x1` | Same spawn, but the script continues immediately. |
| `waitclear` | Block until no scripted enemy is alive (pairs with `spawn`). |
| `finish` | End the level: LEVEL COMPLETED overlay, and stop reading the script. |

Counts may be written `x2` or `2`. Plane ids are matched against `PlaneModelConfig.resourceName`
either in full (`fokker_dr1`) or by its first segment (`fokker`), so `PlaneModels` stays the one
place a plane is defined.

Timings run on **scaled** time, so opening the pause menu (`Time.timeScale = 0`) freezes the
script mid-line and resuming continues it.

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

A full-width solid stripe pinned to the bottom of the HUD canvas, 176 px tall at the 1920×1080
reference resolution, with a hairline top edge. The speaker's display name sits on its own small
row above the message — tinted blue for the player and amber for anyone else, which is the only
visual difference between the two. The message wraps inside a 180 px side padding.

It shares the HUD canvas, so the pause/fail/completed overlay hides it along with the rest of the
HUD. While a line is up, the HUD's control hint (which sits at the same screen edge) is hidden and
restored afterwards. There is no fade and no typewriter reveal: lines appear and disappear whole.

## Running a script (`CampaignScriptRunner`)

A component added to the level controller that walks the steps in a coroutine. The controller
implements `ICampaignScriptHost` (`IsOver`, `EnemiesAlive`, `SpawnWave`, `CompleteLevel`), which is
the whole surface between the script and the level — the runner never touches the plane, the
camera or the terrain.

The runner stops on its own when the host reports the run is over (crash, ditch, shot down), and
the controller also calls `Stop()` explicitly so a `wait` in flight can't outlive the player.

## Scripted enemies (`CampaignEnemies`)

`EnemyController`'s AI was written for the fixed levels' static `MinX`/`MaxX` world bounds. In the
campaign the world scrolls forever, so `CampaignEnemies` re-points those bounds at the camera's
view window every `LateUpdate` (`EnemyController.SetBounds`, ±70 m inside the visible edges). The
existing `FlightSteering.EdgeSteer` then keeps the dogfight inside the frame no matter how far the
player has flown, and an enemy can never be left behind.

Waves spawn off-screen ahead of the player (camera edge + 110 m, each further plane 90 m behind the
last, so a group arrives strung out rather than as one clump) at a random altitude between the
ground's safe margin and 120 m under the ceiling. The AI's ground reference is the terrain's
maximum height on land and the sea level on Flanders Coast — a flat conservative floor, since the
streamed ground under an enemy is not sampled.

`AliveCount` is what makes `wave` blocking; the list also drops planes destroyed by any other
means, so nothing can wedge the script permanently.

## Level 1

```
wait 2
say hq / you / hq / you      (four lorem-ipsum lines)
wait 1.5
wave fokker x1               (blocks until it is down)
wait 2
say hq / you
wait 2
wave fokker x2               (blocks until both are down)
say hq / you
finish
```

`finish` stops the plane, stands the enemies down, and opens LEVEL COMPLETED. Because one scene
serves every campaign level (docs/campaign.md), **next level** bumps the static `CampaignRun` and
reloads `CampaignLevel1` rather than loading a different scene; it is disabled on
`CampaignRun.LastLevel`. Campaign completion deliberately does not touch
`GameManager.UnlockLevel` — that counter gates the fixed `Level1`/`Level2` scenes, and the career
level list is not gated by it.
