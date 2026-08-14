# Screen fade

Every screen change in the game is a **fade to black and back**: the screen on show ramps
to full black, the next screen is swapped in *while the black holds*, and the black ramps
back off. Nothing ever cuts. `ScreenFade` owns both halves, so a caller never sees the
midpoint — it hands over the change it wants made and gets it made under the black.

```
  screen A            black            screen B
  ──────────▶  0.22s  ─────  0.22s  ◀──────────
               out            in
                       ▲
                       └── the swap / the LoadScene happens here
```

## The two calls

| Call | What it does |
| --- | --- |
| `ScreenFade.Swap(change)` | Fades out, runs `change`, fades in. For screens inside one scene. |
| `ScreenFade.Load(scene, atBlack)` | Fades out, runs `atBlack`, loads `scene`, waits a frame, fades in. |

`Load` waits one frame after `SceneManager.LoadScene` so the incoming scene's `Start` has
already built its UI and its world — the fade-in reveals a finished screen, never a half
built one. `atBlack` is where a caller puts anything that must not be seen happening; the
in-level menu passes its `Release`, so the freeze it holds is only lifted once the level it
is leaving is already invisible.

## The rig

One `Canvas` at `sortingOrder` 1000 holding one full-screen black `Image`, created lazily on
the first transition and `DontDestroyOnLoad` from then on — that is why the black survives
`LoadScene` and the coroutine driving it survives with it. The sheet's `raycastTarget` is
true only while a fade runs, which is what stops the mouse from clicking a menu entry that
is already fading away.

Both ramps run on `Time.unscaledDeltaTime`, clamped to 50 ms a step: the in-level menu fades
out at `timeScale` 0, and a scene load stalls a frame long enough that an unclamped step
would skip the fade-in entirely.

## Who defers to it

`ScreenFade.IsBusy` is checked at the top of every menu's `Update` — `MainMenuController`,
`GarageController`, `GameMenu` — so keys pressed mid-fade are dropped rather than queued
behind the transition. The pointer is blocked by the sheet itself instead. Both level
controllers check it on their `Escape` line for the same reason: a level fades *in*, and
`Escape` mashed over that black would otherwise open the pause menu behind it.

| Screen change | Route |
| --- | --- |
| Main menu: career, custom battle, era card, level select, every `back`, `Escape` | `Swap` |
| Main menu → garage / a challenge level / a campaign level / a custom battle | `Load` |
| Garage → main menu (`Escape`) | `Load` |
| A level → `LEVEL FAILED` / `LEVEL COMPLETED` | `Swap` |
| In-level menu → restart / next level / quit to menu | `Load` |

## The one exception

The **pause menu opens instantly** — `GameMenu.Open` builds it on the spot for
`GameMenuKind.Pause`, and `resume` closes it on the spot too. `Escape` is a toggle over a
still-running screen, not a move to another one, and a fade either side of it would put a
quarter-second of black between the player and the game every time they glanced at the menu.
Fail and win *are* moves to another screen, so they take the fade.

`GameMenu.IsOpen` is true from the moment a fail/win fade starts, not from when the menu is
built (`_pending`), so the guards that read it — `PlaneShooter`, `PlaneSearchlight`,
`SoundSystem`, both level controllers — treat the fade-out as part of the menu being open.
Without it the player could still fire into the fading screen.

## Files

| File | Role |
| --- | --- |
| `ScreenFade.cs` | The black sheet, the two ramps, `Swap` / `Load` / `IsBusy`. |
