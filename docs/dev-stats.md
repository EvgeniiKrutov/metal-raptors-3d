# Dev stats overlay

`DevStats` is a runtime performance overlay showing the game's own CPU, GPU and
RAM consumption. **Tab** toggles it open and closed.

## Lifetime

It bootstraps itself with `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` — the
same pattern `GameManager` uses — so no scene needs to reference it. The object is
`DontDestroyOnLoad`, and its canvas is parented under it, so the overlay survives
scene loads and keeps sampling in the main menu, the garage and every level.

The canvas uses `sortingOrder = 500`, above `GameMenu` (200), so the panel stays
readable while paused. Sampling and refresh run on `Time.unscaledDeltaTime`, so the
numbers keep moving when `Time.timeScale` is 0.

Nothing is sampled while the panel is hidden — `Update` returns right after the Tab
check, so the overlay costs nothing when closed.

## Metrics

Values are averaged over a 0.25 s window and redrawn at 4 Hz. Averaging avoids the
unreadable per-frame jitter you get from raw values.

| Row | Shown | Source |
| --- | --- | --- |
| CPU | main thread ms + % of frame budget | `ProfilerRecorder(Internal, "Main Thread")` |
| GPU | gpu frame ms + % of frame budget | `ProfilerRecorder(Render, "GPU Frame Time")` |
| TRIS | triangles submitted per frame | `ProfilerRecorder(Render, "Triangles Count")` |
| RAM | total used MB + managed heap MB | `ProfilerRecorder(Memory, "System Used Memory")` / `"GC Used Memory"` |
| FPS | frames per second + frame ms | `Time.unscaledDeltaTime` |

### Fallback chains

Built-in profiler counters are only guaranteed in the editor and in development
builds; in a release player `ProfilerRecorder.Valid` can be false. Each metric
therefore degrades instead of showing nothing:

- **CPU**: profiler counter → `FrameTiming.cpuMainThreadFrameTime` → whole frame time.
- **GPU**: profiler counter → `FrameTiming.gpuFrameTime` → `n/a` (the row shows
  `n/a` and an empty meter rather than a fabricated number).
- **RAM**: profiler counters → `Profiler.GetTotalAllocatedMemoryLong()` /
  `GetMonoUsedSizeLong()` → `GC.GetTotalMemory(false)`.
- **TRIS**: profiler counter → a scene face count (see below).

`FrameTimingManager` needs **Frame Timing Stats** enabled in Player Settings
(`enableFrameTimingStats: 1` in `ProjectSettings.asset`), which is why that flag was
turned on. Without it the GPU fallback returns nothing in release builds.

Note that the GPU driver reports its timings a few frames late, so `GetLatestTimings`
returns slightly stale data — fine for a load readout, not for frame-exact profiling.

## Frame budget and meters

The percentage is time spent against the frame budget, not against total machine
capacity: `Application.targetFrameRate` when set, otherwise the display refresh rate,
otherwise 60 Hz. So 100% means "this stage alone fills the frame", which is the
number that matters when hunting for the bottleneck.

Meter colours follow that fraction: green under 70%, amber under 100%, red at or
above 100%.

RAM and TRIS have no meter — there is no meaningful ceiling to draw the bar against.

## Triangle count

The counter is what the renderer actually submitted last frame, so it already accounts
for culling, shadow passes and every extra camera; it is averaged over the same 0.25 s
window as the rest and printed as `184.2k` / `1.24M`.

When the counter is unavailable — a release player, where `ProfilerRecorder.Valid` is
false — the row falls back to a face count of what is on screen, prefixed with `~` and
suffixed `faces` so the two are never confused. That pass walks every active `Renderer`
that `isVisible`, takes the `MeshFilter` (or `SkinnedMeshRenderer`) shared mesh and sums
`GetIndexCount / indices-per-face` over its submeshes: 3 for triangles, 4 for quads, 2
for lines, 1 for points. Meshes that are drawn several times are counted once per
renderer, as the GPU sees them.

The fallback differs from the counter in three ways worth knowing: it counts source
mesh faces rather than post-tessellation triangles, it ignores geometry with no mesh
asset behind it (particle systems, line and trail renderers, the UI), and it counts a
mesh once even when shadow passes redraw it. It is a scale readout, not a profiler
number — hence the `~`.

Cost is kept off the hot path: it only runs on the 4 Hz refresh, only while the panel is
open, and only when the counter is missing. Per-mesh face counts are cached by mesh, and
the cache is cleared on `sceneLoaded` so it neither grows across levels nor keeps
wrappers for destroyed meshes alive. The
`FindObjectsByType` sweep it needs still allocates an array each refresh.

## Spawn buttons (custom battle)

Below the metrics the panel grows a **SPAWN** section with two buttons, *SPAWN SCOUT*
and *SPAWN FIGHTER*. It exists so a custom battle — which flies no script and therefore
never spawns anything on its own (docs/campaign-scripts.md) — can be given traffic on
demand while testing.

`DevSpawn` is the seam. `CampaignLevelController` registers itself as the
`IDevSpawnHost` from `Start`, **only when `CustomBattle.Requested`**, and unregisters in
`OnDestroy`; `DevStats` never references the level. The section is shown only while
`DevSpawn.Available` is true, which the host answers as "actually flying": not over, not
falling after being shot down, and not in a cutscene or the fly-in intro
(docs/level-intro.md). `DevStats` polls it every frame and resizes the panel between its
stats-only and full heights, so the section appears and disappears mid-flight.

A press does not spawn immediately. The button goes non-interactable and its label counts
`DevSpawn.Delay` (1.5 s) down in tenths — amber while pending — and the plane is launched
when it reaches zero. The two buttons count independently. Timing runs on
`Time.unscaledDeltaTime` in `Update`, *before* the visibility check, so a pending spawn
still fires if Tab closes the console mid-countdown; a pending spawn is cancelled if the
level stops being spawnable first.

The plane launched is `PlaneModels.EnemyFor(role)` — the Dr.I for scout, the D.III for
fighter, the same mapping campaign scripts get through `PlaneModelConfig.enemyRole`
(docs/enemies.md). It goes through `CampaignEnemies.Spawn` as a one-plane wave, so it
enters off the right edge of the view and behaves exactly like a scripted wave's plane.
The custom battle's `CampaignEnemies` is created lazily on the first spawn.
