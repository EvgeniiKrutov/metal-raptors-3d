# Game menu (pause / fail / win)

One overlay, three titles. `GameMenu` is the only in-level menu: it is what `Escape` opens
mid-flight, what a crash shows once the explosion has played out, and what clearing a level
shows. It is built from the same pieces as the main menu (`MenuTheme`, `MenuLayout`,
`MenuPanel`, `MenuItemView`, `MenuInput`), so a palette change moves both.

```
┌────────── 40% ──────────┬─────────── 60% ───────────┐
│                         │                           │  ← 15% of the height
│  LEVEL FAILED           │   the frozen game,        │
│  ───                    │   darkened (black 60%)    │  ← 72×4 accent rule
│  level 1 | verdun       │                           │  (muted, no click)
│  restart                │                           │
│  options                │                           │  (muted, no click)
│                         │                           │
│  quit to menu           │                           │
└─────────────────────────┴───────────────────────────┘
   opaque theme Bg
```

The left band is the theme's `Bg` at full alpha — the main menu's flat column, dropped over
the level. The right band is black at 60%, so the frozen scene reads through it.

## The three kinds

| `GameMenuKind` | Title | Entries | Opened by |
| --- | --- | --- | --- |
| `Pause` | `PAUSE` | resume · restart · options · quit to menu | `Escape` / gamepad east, any time in a level |
| `Failed` | `LEVEL FAILED` | restart · options · quit to menu | the crash coroutine, after `Explosion.Duration` |
| `Completed` | `LEVEL COMPLETED` | restart · next level · options · quit to menu | the last enemy dying |

* **resume** (pause only) closes the menu and unfreezes, the same thing `Escape` does.
* **restart** reloads the active scene. A custom battle restarts as the same battle —
  `CustomBattle` is static and survives the reload.
* **next level** loads the next scene when there is one (Level 1 → Level 2) and is drawn
  muted otherwise. `GameMenu.Open` takes that scene name; passing null is what mutes it.
* **options** is muted everywhere for now, like `challenges` in the main menu.
* **quit to menu** loads `MainMenu`.

## Subtitle

The line under the accent rule names the flight, lowercase. It is not a caption — it is the
panel's **first entry, added disabled** (`AddNav(subtitle, null, interactable: false)`), so it
carries the entry size (`ItemSize`) and the disabled entry's `Muted` weight and colour, and
the highlight skips it exactly as it skips `options`. It sits on the list's own pitch — no
extra gap under it, so it reads as the first row of the list rather than a header over it:

* an authored level — `level 1 | verdun`, the level number and its terrain generator
  (`TerrainNames.For(TerrainPart.kind)`);
* a career campaign level — `level 1 | verdun`, the endless scene's own generator;
* a **custom battle** — `verdun | morning`, map then sky (`DaytimeNames`), the same pair the
  menu's preview card carries. `CustomBattle.Requested` is what picks this form.

## Freezing

Opening any of the three sets `Time.timeScale = 0` and hides the level's HUD canvas; closing
restores both. So all three screens are a still frame, not a running level behind glass, and
the health bar / distance / control hints never sit under the overlay.

* Music keeps playing — `MusicPlayer` runs on `Time.unscaledDeltaTime` and
  `realtimeSinceStartup`, so it is untouched by the freeze.
* `FixedUpdate` stops, which is what actually stills the planes (`CubeController`) and the
  bullets. Two `Update` loops read keys directly and would otherwise still fire while paused,
  so both bail out on `GameMenu.IsOpen`: `PlaneShooter` (F) and `PlaneSearchlight` (T).
* Anything that leaves the scene resets `timeScale` to 1 **before** `LoadScene`, and
  `OnDestroy` resets it too — a frozen menu can never leak its freeze into the next scene.

`Escape` closes only the pause menu; on fail and win it does nothing, so the screen has to be
answered. The frame guard in `GameMenu.Update` is why the keypress that opens the pause menu
does not immediately close it again.

## Files

| File | Role |
| --- | --- |
| `GameMenu.cs` | The overlay: bands, title, subtitle, entries, freeze, and its own input loop. |
| `MenuLayout.cs` | `CreatePage` / `CreateRegion` / `CreateScreen` / `CreateBand` / `BuildTitle`, shared with the main menu. |
| `MenuInput.cs` | `ReadStep` / `ReadAdjust` / `ReadSubmit` / `ReadCancel`, shared with the main menu. |

`MenuLayout` and `MenuInput` were lifted out of `MainMenuController` when this screen landed
— it needs the same 15%-down, 120px-inset column and the same navigation keys, and neither
should be described twice.
