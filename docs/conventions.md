# Conventions

## No comments in code (2026-07-29)

Reinforcing CLAUDE.md rule 2: `.cs` files carry no comments of any kind —
no `//`, `/* */`, or `///` XML doc comments. Anything worth explaining
(design rationale, non-obvious behaviour, tuning notes) goes in a
`/docs/*.md` file instead, keyed to the class/system it describes.

## What lives in `Assets/Resources` (2026-08-16)

Anything loaded by name at runtime. The root is laid out by *what a thing is*, not by
who loads it:

| Path | Holds | Tracked? |
| --- | --- | --- |
| `Assets/Resources/*.asset` | The `ScriptableObject` tunables — `PlayerConfig`, `EnemyConfig`. | yes |
| `Assets/Resources/objects/planes/<era>/` | Aircraft FBX, one folder per career era (`world_war_1` today, matching `CareerEras`). | no |
| `Assets/Resources/objects/trees`, `objects/burned_houses` | Scenery prop FBX (docs/battlefield.md). | no |
| `Assets/Resources/Sounds/` | Every sound effect (docs/sounds.md). | no |

`.gitignore` excludes `/Assets/Resources/objects` and `/Assets/Resources/Sounds` — the
private art and audio — and **nothing else** under the root, so the config assets are
ordinary tracked files. It used to exclude the whole root, which is why fonts, music,
campaign scripts and dialogue sit in their own nested `Resources` roots
(`Assets/Fonts/Resources`, `Assets/Music/Resources`, …): Unity treats any folder named
`Resources` under `Assets` as a root, and that was the only way to ship a
runtime-loaded file. Those roots still work and stay where they are; new *trackable*
runtime assets can simply go in the main root.

Two paths are built from this layout in code rather than written out per file:
`PlaneModelConfig.folder` (defaulted to `PlaneModelConfig.WorldWar1`) joined with
`resourceName` gives `ResourcePath`, which is what `PlaneFactory` loads — `resourceName`
stays the plane's bare **id**, since campaign scripts (`"albatros"`), the skin PlayerPrefs
keys and the model's GameObject name all key off it. `BattlefieldProps` prefixes its
model table with `objects/` at load time for the same reason: the table entries double as
dictionary keys.
