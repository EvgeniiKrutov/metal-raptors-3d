# Options

One page, reached from two places: `options` in the main menu column and `options` in the
in-level menu (docs/game-menu.md). Both build the same `OptionsPage`; the difference is only
how it arrives on screen.

```
┌────────── 40% ──────────┬─────────── 60% ───────────┐
│                         │                           │  ← 15% of the height
│  OPTIONS                │   GENERAL                 │
│  ───                    │   ◀ ██████████░░░░ ▶  70% │  ← 72×4 accent rule
│  sound                  │                           │
│  graphics               │   MUSIC                   │
│                         │   ◀ ████████████░░ ▶  85% │
│  back                   │                           │
│                         │   SFX                     │
│                         │   ◀ ███████████░░░ ▶  80% │
└─────────────────────────┴───────────────────────────┘

┌────────── 40% ──────────┬─────────── 60% ───────────┐
│                         │   GOD RAYS                │
│  OPTIONS                │   ◀        on         ▶   │
│  ───                    │   SHADOWS                 │
│  sound                  │   ◀       high        ▶   │
│  graphics               │   BLOOM                   │
│                         │   ◀       full        ▶   │
│  back                   │   GROUND DETAIL           │
│                         │   ◀       high        ▶   │
│                         │   FRAME CAP               │
│                         │   ◀        off        ▶   │
└─────────────────────────┴───────────────────────────┘
```

The left column is a category list in the ordinary menu column — `sound`, `graphics`, then
`back`. The right 60% holds one region per category, and only the highlighted category's
region is active, so hovering `graphics` swaps the rows under the cursor. Both columns start
at `MenuTheme.ListTop`, so the first row lines up with the first category.

**On mobile the `graphics` category is not built at all** — `OptionsPage` puts only the sound
category in `_cats`, so the column is `sound` and `back` and the page behaves exactly as it
did before graphics existed. See *A fixed profile on mobile* below for why.

Categories are built by `BuildSound` / `BuildGraphics`, each returning a `Category` — its nav
entry, its region `GameObject`, its rows as `IMenuOptionRow[]`, and a `Refresh` delegate that
re-reads the store. `Adopt` wires the nav entry and the row events; adding a third category is
one more `Build…` method in the `_cats` array.

## Two focus states

The page is its own `IMenuFocusGroup`, and it has one bit of state: `_live`, whether the
selectors on the right are being operated or only previewed.

| | left column | right rows |
| --- | --- | --- |
| **preview** (`_live == false`) | up/down move the highlight; the highlighted category activates | drawn `Muted` — captions, bars, values, triangles and percentages all |
| **live** (`_live == true`) | the owning category stays accent-lit, nothing is driven by keys | up/down move between that category's rows, left/right step the focused one |

Entering: `enter`/click/tap on a category, or a click on any row or triangle. Leaving:
`Escape` (gamepad east), or hovering any left-column entry — the hover that highlights a
category also puts the page back in preview, which is what makes the left column feel like
the thing in charge. Hovering a *different* category also swaps the visible region and resets
`_row` to 0.

`Escape` in preview leaves the page entirely; `Cancel()` returns `false` there and the host
(`MainMenuController` / `GameMenu`) does the leaving. In live it returns `true` and the page
handles it itself.

**A triangle stays clickable while the page is in preview** — dimmed, not disabled. That is
what `MenuArrowView.SetState(interactable, focused, dimmed)` is for: the two-argument
overload still means "muted because there is nothing left this way", the third argument
means "muted because the row is not live yet". A click on a dimmed triangle engages the
category *and* applies its step, so there is no dead first click.

## The rows

`MenuVolumeRow` is the garage's stat bar (docs/garage.md) turned into a control: the same
uppercase caption over the same `Border` track and `Accent` fill, with a `MenuArrowView`
either side and the percentage to the right of the second one. The bar alone cannot show a
5% step, which is why the number is there.

`AudioOptions.Steps` is 20, so every step is 5% and the value is carried as that integer —
`ToStep` / `FromStep` are the only conversion, and `Snap` runs a loaded preference through
both so an old or hand-edited value lands on the grid.

`MenuChoiceRow` is the same row with a word where the bar goes: the caption, the two
triangles at exactly the same x as the volume rows' triangles, and the current value centred
in the bar's slot. It carries an index into a `string[]` instead of a step, so a two-state
switch and a three-state tier are the same widget — the array length is the only difference.
It shares `IMenuOptionRow` with `MenuVolumeRow` (`SetLive`, `SetFocused`, `Adjust`,
`Hovered`, `Engaged`), which is what lets `OptionsPage` hold mixed rows in one array.

## What the sound rows do

| Row | Multiplies | Where |
| --- | --- | --- |
| `general` | everything, as `AudioListener.volume` | `AudioOptions.SetMaster`, applied by `GameManager` at boot |
| `music` | `MusicPlayer`'s `MusicVolume` bed | `MusicTarget`, re-aimed live from `AudioOptions.Changed` |
| `sfx` | every engine voice, the wind, and every one-shot | `SoundSystem` and each `PlayOneShot` site |

`general` is a master over the other two: it sits on the listener, so a `music` of 100% under
a `general` of 50% is half volume. All three default to 100%.

Changes apply as they are made. `AudioOptions` writes each change to `PlayerPrefs` and raises
`Changed`; `MusicPlayer` re-aims `_volumeTarget` at the new bed and ramps to it through the
same `MoveTowards` the fades use, so a step is a fast slide rather than a click. `SoundSystem`
recomputes its voice volumes every `Update`, so it needs no subscription — but note the
engine bed is already faded out while the pause menu is open, so the `sfx` row is only
audible on the main menu's copy of the page or after resuming.

`mr_master_volume` is the key `GameManager` already used, so an existing save keeps its
volume; `mr_music_volume` and `mr_sfx_volume` are new and default to 1.

## What the graphics rows do

| Row | Values | Drives | Live? |
| --- | --- | --- | --- |
| `god rays` | off / on | `GodRays.OnBeginCamera` skips enqueuing `RayPass` when off | yes |
| `shadows` | off / low / high | every tracked `Light` plus the URP asset's cascades and shadowmap size | yes |
| `bloom` | off / low / full | each sky's `Bloom` override — `active`, `downscale`, `maxIterations` | yes |
| `ground detail` | low / medium / high | `BattlefieldPeople` group count and size, `BattlefieldProps` tree grid | next level |
| `frame cap` | 30 / 60 / 120 / off | `Application.targetFrameRate` (with `vSyncCount` forced to 0) | yes |

Every row is written to `PlayerPrefs` on change and raises `GraphicsOptions.Changed`. All but
`ground detail` apply live, which matters because the in-level menu is the place you actually
notice the difference.

### A fixed profile on mobile

Mobile does not get *defaults*, it gets a **fixed profile**. `Load()` branches on
`GraphicsOptions.Mobile` (`Application.isMobilePlatform`) and returns before touching
`PlayerPrefs` at all:

| | desktop default | mobile, fixed |
| --- | --- | --- |
| `god rays` | on | **off** |
| `shadows` | high | **low** |
| `bloom` | full | **off** |
| `ground detail` | high | **low** |
| `frame cap` | off | **60** |

Skipping `PlayerPrefs` on mobile is the point, not an optimisation. The category is hidden
there, so nothing can ever write these keys — and if a device had already saved a value from
an earlier build, reading it back would pin that phone to a stale setting with no UI left to
change it. Loading the profile fresh every launch makes the mobile look a property of the
build.

Two consequences worth knowing:

- `GodRays.Attach` returns `null` outright when `Mobile && !GodRays`, so the component, its
  material and its per-camera delegate are never created — on desktop it must still be built
  because the switch is live. This is the pass worth killing first on iOS:
  `requiresIntermediateTexture = true` forces an off-tile copy, the most expensive shape of
  render pass on a tile GPU.
- `shadows` is `low` rather than `off` because the mobile pipeline asset already authors
  `m_SoftShadowsSupported: 0`, so `Hard` filtering costs nothing extra there and the tier's
  real saving is the halved shadowmap (1024 → 512) over the same distance.

**God rays** is the cheapest to gate and the most expensive to run — a full-screen radial
blur with 48 samples that every sky attaches to the camera. The component stays alive when
the switch is off; `OnBeginCamera` just returns before `EnqueuePass`, so nothing is recorded
into the render graph and flipping it back on costs nothing. (`ScriptableRenderer` clears its
pass queue every frame, so not enqueuing is the whole gate — there is nothing to unregister.)

The switch controls the **shafts only**. The bright sun itself — disc, halo and the glow
around it — is drawn by `AerialHaze` into the sky at `BeforeRenderingTransparents`, and the
`Bloom` in each sky's `BuildPostFx` blooms it afterwards; neither is affected. The shafts are
additive on top at `BeforeRenderingPostProcessing` and are scaled by `RayIntensity`, which
`MiddaySky` sets to 0.45 — roughly half of `MorningSky`'s 0.85 and `EveningSky`'s 0.8. Midday
is therefore the daytime where toggling it looks like nothing happened; morning and evening
are where the difference reads.

**Shadows** cannot simply walk every light, because a light with no shadows is usually a
deliberate fill light rather than one waiting to be switched on. Instead `GraphicsOptions`
keeps a register of *casters*: `Rescan` (run from `GameManager` on `sceneLoaded`, before any
`Start`) records every scene light that already casts along with the mode it was authored
with, and runtime-spawned lights call `Track` themselves — `GarageLighting` and
`PlaneSearchlight` do this in place of setting `light.shadows` directly. `Downgrade` then
never *raises* a light above its authored mode: `high` restores it, `low` forces `Hard`, `off`
forces `None`.

`low` deliberately **does not touch `shadowDistance`**. The camera sits `CameraDistance` =
420 units in front of the play plane (`LevelController.CamZ`), and the terrain is further back
again — the authored 620 m is sized to reach past that gap, so trimming it even moderately
puts every shadow caster beyond the far edge and `low` renders as `off`. What `low` cuts
instead is filtering and resolution: `LightShadows.Hard` in place of `Soft`, the cascade count
clamped to 2, and `mainLightShadowmapResolution` halved (2048 → 1024 on PC; the mobile asset
authors 1 cascade at 1024 and lands at 512). Shadows stay where they are and get cheaper and
coarser, which is what the tier is for.

The URP asset is a shared `ScriptableObject`, so changes made in play mode survive into the
Editor — which would let each session's `low` compound on the last. The authored cascade count
and shadowmap resolution are therefore captured once, before any scene script runs
(`GameManager.Awake` → `GraphicsOptions.Apply`), and restored on `Application.quitting`.

**Bloom** is the bandwidth hog: left at its defaults it runs a `Half`-resolution pyramid with
6 iterations, so up to twelve full-screen passes on a tile GPU. Each sky's `BuildPostFx`
registers its `Bloom` through `TrackBloom`, and the tier overrides `downscale` and
`maxIterations` on all of them at once — `low` is `Quarter` at 3 iterations, `full` restores
`Half` at 6, and `off` clears `bloom.active`. `Rescan` empties the register on each scene
load, before the skies re-register in `Start`. Note the thresholds sit at 0.85–1.10 and
emission is deliberately pushed past 1 (`EnemyController` uses `color * 2f`), so bloom is
doing real work here — `off` is a visible change, not a free one.

**Ground detail** is the one row that does not apply live, and deliberately. `BattlefieldProps`
places trees in a spatial hash keyed by cell size; changing that size while the grid holds
entries would break every `Nearest` lookup. So `_treeCell` is captured once in `Begin`, and
`BattlefieldPeople` likewise fixes `_targetGroups` there. The tier takes effect on the next
level, which is also why `Tick` never has to reconcile a shrinking target — groups drain
through the normal `Expired` cull instead.

## Arriving

**Main menu** — an ordinary `ScreenFade.Swap`, like every other page there
(docs/screen-fade.md). `MenuScreen.Options` joins the enum and `SetScreen` shows the page.

**In-level menu** — the pause overlay's opaque band is 40% wide, so the page cannot simply
appear over it. `GameMenu.Slide` widens the band to the full screen (and walks the scrim's
left edge along with it, so the two never overlap) over `ExpandSec` = 0.28s of *unscaled*
time — the game is frozen at `timeScale` 0 — on a smoothstep. The pause entries fade out over
the first half and the options page fades in over the second, both through a `CanvasGroup`.
Closing runs the same coroutine backwards and returns the highlight to the `options` entry.
`Update` is dead while `_sliding`, so a key pressed mid-slide cannot stack a second one.

## Files

| File | Role |
| --- | --- |
| `AudioOptions.cs` | The three volumes: the 5% grid, the `PlayerPrefs` keys, and the `Changed` event. |
| `GraphicsOptions.cs` | The six graphics settings, the caster and bloom registers, and the URP shadow tiers. |
| `MenuVolumeRow.cs` | One volume row — caption, triangles, bar, percentage — and its live/preview colours. |
| `MenuChoiceRow.cs` | One choice row — caption, triangles, centred value — for switches and tiers alike. |
| `IMenuFocusGroup.cs` | `IMenuOptionRow`, the `SetLive` + `Engaged` contract both row types share. |
| `OptionsPage.cs` | The categories, the two columns, and the preview/live focus rules. |
