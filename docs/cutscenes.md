# Cutscenes: the paused frame

A block of `say` steps is not spoken over the flying any more. The two black bars slide in, the
game **freezes and blurs behind them**, and the conversation plays over a still, soft frame; when
the last line closes, the frame sharpens, the world starts moving again and only then do the bars
leave. The dialogue itself is unchanged (docs/campaign-scripts.md) — what changed is the state of
the game underneath it.

## The sequence

`CampaignScriptRunner` owns the order, because only it knows where a block of lines begins and
ends. Each phase is its own step and they do not overlap, except the freeze, where the pause and
the blur are deliberately the same 0.55 s ramp — the game slowing to a stop *is* the picture going
soft.

| Phase | Time | What moves |
| --- | --- | --- |
| bars in | 0.50 s (`CinematicBars.SlideSec`) | The top and bottom bars slide in. The game still runs. |
| formation | — | The wingman is given as long as it needs to fly back onto the wing (`CompanionReady`). It flies on the game clock, so this has to finish **before** the freeze or it would never finish at all. |
| freeze | 0.55 s (`CutscenePause.FreezeSec`) | `Time.timeScale` 1 → 0 and the blur 0 → 1, both on the same `SmoothStep`. |
| lead-in | 0.55 s (`DialogueBar.LeadInSec`) | The bars are held empty on a still frame before the first character. |
| the block | — | Lines are typed, held and skipped exactly as before. |
| unblur | 0.35 s (`CutsceneBlur.FadeSec`) | The picture sharpens while the game is still stopped. |
| unfreeze | 0.40 s (`CutscenePause.ThawSec`) | `Time.timeScale` 0 → 1. |
| bars out | 0.50 s | The bars slide back out over a game that is already flying. |

Coming out is three separate beats on purpose: sharpening a frozen frame reads as the camera
handing the scene back, and starting the motion before the bars have gone means the player is
already flying when the frame opens up rather than being handed a plane the instant the black
leaves.

## The clock (`CutscenePause`)

Everything that has to keep animating while the game is stopped runs on `CutscenePause.Delta`
instead of `Time.deltaTime`: the bar slide, the typewriter, every `Wait` in the runner. It is
`Time.unscaledDeltaTime`, forced to **zero** whenever the pause menu, the briefing, the outro or a
screen fade is up — so the old guarantee still holds, that opening the pause menu freezes the
script mid-line and resuming continues it. The script's non-dialogue waits (`wait`,
wave lead-ins) run on the same clock; outside a cutscene the two clocks are the same thing.

`Hold(scale)` is the only writer of `Time.timeScale` during a block, and it declines to write while
one of those overlays owns the timescale itself. That is the whole reason `GameMenu` closes with
`CutscenePause.Restore()` rather than `Time.timeScale = 1f`: pausing and resuming *inside* a
cutscene has to come back to the cutscene's own scale, not to a flying game behind a dialogue bar.
`LevelBriefing` and `LevelOutro` still restore a flat 1 — neither can overlap a block.

The freeze is released three ways, and all of them are idempotent: the runner's own `Unfreeze`,
`DialogueBar.Hide` (which is what a crash, a ditch or `CompleteLevel` reaches through), and
`CampaignScriptRunner.OnDestroy`. A scene load resets it as well, from `sceneLoaded`, so nothing
can hand the next level a stopped clock.

## The blur (`CutsceneBlur`)

A single global `Volume` at priority 500 holding one `DepthOfField` in **Gaussian** mode, built the
first time it is asked for and disabled — not destroyed — the rest of the time, so it costs nothing
outside a cutscene.

The blur is ramped by moving `gaussianEnd`, not by fading the volume in. With `gaussianStart` at 0
the circle of confusion is `depth / gaussianEnd`, so setting `gaussianEnd` to
`focus / amount` makes the play plane — which sits at a fixed `focus` of 420 units from the camera,
handed over by `CampaignLevelController.SetupCamera` — reach full blur exactly as `amount` reaches
1. Everything further away crosses that line sooner, so the ramp reads as a focus pull: the
horizon and the sky go first and the player's own machine last. Fading the volume's weight instead
would have popped, because the mode is an enum and does not interpolate.

`gaussianMaxRadius` is pinned at its maximum (1.5) and `highQualitySampling` follows
`GraphicsOptions.Mobile`. The whole effect is one half-resolution blur pass on a frame the GPU is
otherwise not doing anything with.

The UI is untouched: bars, avatar and text are on an overlay canvas, which is composited after
post-processing.

## What the freeze changes

The dogfight, the terrain streaming, the falling wreck, the supply crate and every particle stop
where they are, because they all run on the game clock. So do the bomb and boost cooldowns, which
used to tick through a radio line — a cutscene now costs the player nothing at all, rather than
almost nothing.

Audio is not stopped — Unity's mixer ignores `timeScale` and the whole sound system runs on
unscaled time. Instead the **engines duck to 30%** for as long as the bars are up, wind left alone,
so a radio call is spoken over a plane rather than through one (docs/sounds.md).

The one thing a `Time.timeScale` of 0 does *not* stop on its own is an animation that re-randomises
itself every frame rather than advancing by a delta, and the game has two: the camera shake on
`CampaignLevelController` and `ShakeEffect` on the plane model. Frozen, both would have jittered
for the whole conversation, so both now count down on `Time.unscaledDeltaTime` and are always over
within their own third of a second — a still frame is still.

Two things deliberately survive it. **Skipping** is frame-based
(`MenuInput.ReadSkip`, docs/touch-input.md), so a line is still one tap. And the **pause menu**
still opens on Escape, hides the whole HUD — bars included — over the frozen frame, and returns to
the cutscene on resume.

The intro fly-in is the one place where the freeze is visible as an interruption:
`LevelIntro` cues the script partway through the entry (docs/level-intro.md), so on a level that
opens on a `say` the plane stops mid-frame, cutscene 1 plays over it, and the fly-in finishes and
hands over control when the bars leave. That is the intended reading — the level opens on a still.

Cutscene mode itself (`CampaignLevelController.Cinematic`, the HUD curtain, the non-lethal ground,
the refused bomb and boost) is unchanged and still keyed on `CinematicBars.AnyShowing`. It covers
the bar slides at either end, where the game is still moving.
