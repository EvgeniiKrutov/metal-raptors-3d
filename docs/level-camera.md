# Level camera framing

## Everything read small on iOS (2026-08-26)

The level camera sits a fixed 420 units back with the scene's 60° field of view, and Unity's
`Camera.fieldOfView` is **vertical**. The visible world height was therefore identical on
every device and the width simply grew with the aspect ratio — a 19.5:9 iPhone in landscape
showed ~1050 units across where a 16:9 monitor showed ~860. Same plane, same world, 22% less
of the screen: the plane and the enemies read as small.

`LevelCamera.Frame` is what both `LevelController.SetupCamera` and
`CampaignLevelController.SetupCamera` now call. It drives the camera's vertical FOV rather
than reading it:

* **Cap the width, not the height.** The 16:9 view is the reference. Anything wider than
  that narrows the FOV so the visible *width* stays at the reference instead of growing.
  Screens at 16:9 or narrower — an iPad's 4:3 included — are left alone.
* **`MobileZoom`** (1.15) then narrows it a further 15% on `Application.isMobilePlatform`,
  because a phone screen is physically small even once the framing is honest. It is the one
  knob to turn if the planes still want to be bigger.

| device | aspect | FOV | visible world | Camel span (66u) across the screen |
| --- | --- | --- | --- | --- |
| desktop 16:9 | 1.778 | 60.0° | 862 × 485 | 7.7% |
| iPhone 19.5:9, before | 2.167 | 60.0° | 1051 × 485 | 6.3% |
| iPhone 19.5:9, now | 2.167 | 44.8° | 750 × 346 | 8.8% |
| iPad 4:3 | 1.333 | 53.3° | 562 × 422 | 11.7% |

So the iPhone gains **1.4×** on what it had, and the trade is the vertical band: 346 units of
sky are in frame instead of 485, which is less warning of an enemy coming from above.

### Why the camera and not the models

Planes, trees, houses and soldiers all live at one world scale
(`docs/plane-scale.md`, `docs/battlefield.md`). Scaling the planes up for iOS would have made
them wrong against the scenery on the same screen. Moving the camera scales all of it
together and leaves desktop untouched.

Nothing else needed a mobile size of its own — **no** code path changes a world scale on
mobile. What `GraphicsOptions` does change on mobile is **density**, not size:
`PeopleScale` (0.35) scales the *number* of people groups, `PeopleGroupCap` (4) their
headcount, and `TreeCellScale` (2.2) widens the tree cell spacing. `BattlefieldProps` keeps
its `MetreScale` 7.2 and 1.5 oversize on every platform.

### Ordering

`Frame` writes `cam.fieldOfView` before `Battlefield`, `SkyFlak`, `CloudSystem` and
`MountainRange` are begun, and those read the camera (or the `_halfViewWidth` /
`_halfViewHeight` it returns) rather than assuming 60° — so the whole scene follows from the
one call. It runs once, in `SetupCamera`: a mid-game resolution or orientation change is not
re-fitted.

`BaseFieldOfView` (60) is a constant rather than the value read off the scene camera, so the
result does not drift if a scene is re-authored, and calling `Frame` twice is harmless.

## Files

| File | Role |
| --- | --- |
| `LevelCamera.cs` | `Frame` — the FOV solve, the 16:9 width cap and `MobileZoom`. |
| `LevelController.cs` / `CampaignLevelController.cs` | Call it from `SetupCamera`; hold `_halfViewWidth` / `_halfViewHeight` for spawning, walls and the supply drop. |
