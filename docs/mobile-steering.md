# Mobile steering: stick, arrow, heading follow

Touch builds do not steer the plane directly. They aim an **arrow** that orbits the plane,
and the plane flies to that arrow. Three pieces: `TouchStick` (the on-screen control),
`HeadingArrow` (the marker), and a heading-follow branch inside `CubeController`. `LevelHud`
owns all three, so both `LevelController` and `CampaignLevelController` get them for free.

Everything here is gated on `HudTheme.IsTouch`. A desktop build never constructs the stick,
never enables the heading branch, and keeps the `A` / `D` model described in
docs/flight-model.md unchanged.

## Why a heading, not a turn rate

`A` / `D` set a *turn rate* and the heading integrates out of it — you hold a key until the
nose looks right. A thumb on glass has no equivalent of "hold": the finger is already pointing
somewhere, and the natural reading of that is a *heading*. So the stick's angle becomes a
target heading and the plane closes on it.

The target is **latched**. Lifting the finger does not clear it — the arrow stays where it was
put and the plane keeps flying that way. This is the one place the design departs from the 2D
sibling, where releasing the stick dropped `targetHeading` and the heading coasted to a stop.
Latching is what makes a phone playable one-handed: you set a course, let go, and shoot.

## `TouchStick`

A uGUI widget: an invisible full-size `Image` (the grab area) with a visible ring as its child
(the thumb). The base circle the thumb is clamped to is **not drawn** — only the thumb is, and
at rest it sits at the base's centre.

| Metric (`HudTheme`) | Value | Meaning |
| --- | --- | --- |
| `StickGrabSize` | 720 × 620 | The grab rect, anchored to the bottom-right corner inside the safe area. |
| `StickInsetRight` / `StickInsetBottom` | 260 / 250 | Base centre, measured from that corner. |
| `StickClampRadius` | 210 | How far the thumb travels before it stops. |
| `StickThumbSize` / `StickThumbStroke` | 150 / 4 | The visible ring, drawn with `UIFactory.RingSprite`. |
| `StickDeadzone` | 0.20 | Fraction of the clamp radius below which the angle is not read. |
| `StickReturn` | 0.12 s | Time constant of the thumb easing back to centre on release. |
| `ArrowOrbit` | 110 | Radius the arrow orbits the plane at, in canvas units. |
| `ArrowArm` / `ArrowStroke` | 16 / 1 | The chevron's arm length and line width. |

**Grab area is wider than the clamp.** A finger landing anywhere in the 720 × 620 rect grabs the
stick and the thumb jumps under it; only the *travel* is limited to 210. Aiming at a 150px
circle in the corner of a phone held in two hands is a miss waiting to happen, and the base is
invisible anyway, so there is no visual edge for the player to expect.

**Dead zone gates the read, not the render.** Inside 20% the thumb still follows the finger —
it has to, or the control feels stuck — but `Steering` stays false and the latched `Angle` is
left alone. Magnitude does nothing beyond that gate: past it only the angle matters, so a half
push and a full push fly the same course.

**Multi-touch comes from uGUI.** The stick is a normal `IPointerDownHandler` / `IDragHandler`
and tracks the `pointerId` it captured, ignoring every other one. That is what lets the stick
and the `FIRE` square be held at the same time — `InputSystemUIInputModule`'s default action map
binds `<Touchscreen>/touch*/press`, so each finger is its own pointer with its own id. Reading
`Touchscreen.current.primaryTouch` instead (the way `MenuInput` does, see docs/touch-input.md)
would have made steering and firing mutually exclusive.

`OnInitializePotentialDrag` clears `useDragThreshold`, so the thumb tracks from the first pixel
rather than after uGUI's 10px drag threshold.

## `HeadingArrow`

A `>` drawn as two rectangles rather than a text glyph: arms of `ArrowArm` (16) by `ArrowStroke`
(1), pivoted at their right end, rotated ±45° so they meet at a tip, on a parent that carries the
heading rotation. A `Text` `>` was the first cut and it read far too heavy — a font has no stroke
width to set, and the bold face put a wedge next to the plane instead of a mark. Two rects give
the thickness as a number, which is what lets the mark be a hairline: a single unit of stroke on a
16 unit arm, well under the stick ring's 4, so it reads as a pointer and never as a piece of UI
competing with the plane.

It lives on the same screen-space overlay canvas as everything else. Each frame it takes the
plane's world position through `Camera.WorldToScreenPoint`, converts that to canvas space, and
offsets by `ArrowOrbit` (110) along the **target** heading. The orbit circle itself is never
drawn. `LevelController.LateUpdate` moves the camera *before* it ticks the HUD, so the arrow
never lags a frame behind the plane, camera shake included.

It shows the target, not the plane's nose, which is the whole point: the gap between the glyph
and the plane's actual heading *is* the turn you have asked for and not yet got. When the two
agree the arrow sits dead ahead, as it does at level start (heading 0, both level controllers
call `Initialize(..., startHeadingRad: 0f, ...)`).

The glyph is disabled — not moved off-screen — when it should not be seen, including the
`screen.z <= 0` case behind the camera.

## The heading branch in `CubeController`

`EnableHeadingSteering()` flips the plane into the branch and seeds the target with the current
heading. From then on `FixedUpdate` computes

```
desiredRate = FlightSteering.SteerToHeading(heading, target, maxRate, dt, angularVelocity)
```

instead of reading `Keyboard.current`, and everything downstream is untouched: `EdgeSteer` still
gets the last word near the world edges, the `turnResponsiveness / mass` smoothing still eases
`angularVelocity` toward `desiredRate`, and speed is still `CruiseSpeed`. Turn radius therefore
stays exactly what docs/flight-model.md says it is.

**There is no explicit delay.** "The plane follows the arrow after a moment" is entirely the
existing inertia — `approach = 1 - exp(-(turnResponsiveness / mass) * dt)` — so no new tunable
was added and the touch build turns on the same arc the desktop one does. The same inertia is
what the rate command has to compensate for, below.

`error` comes from `Mathf.DeltaAngle`, which is what makes the plane pick the **shorter** way
round. What is done with it is the whole of why the plane flies steady:

```
residual    = error - angularVelocity * lag        lag = mass / turnResponsiveness
desiredRate = clamp(residual / lag, ±maxRate)
```

**The braking term is the point.** The first cut was the 2D sibling's `clamp(error / dt, ±maxRate)`,
which saturates for any error over `maxRate * dt` — about 3.6° — so it is bang-bang: full rate
right up to the target. But `angularVelocity` is not the commanded rate, it *chases* it with a
time constant of `lag` (0.5 s at the stock mass 2.5 and `turnResponsiveness` 5). Arriving on the
target with the rate still near maximum, the plane sails straight past, the command flips to full
opposite rate, and the same thing happens on the way back — a limit cycle. On screen that is a
plane wobbling around the arrow instead of settling on it, whether or not a finger is on the
stick.

Subtracting `angularVelocity * lag` is the angle the plane will *still* cover while its rate
decays to zero — its stopping distance. Commanding `residual / lag` therefore asks for exactly
the rate whose own stopping distance is the angle left to travel. Solving the loop
(`ë + 2k·ė + k²e = 0` with `k = 1 / lag`) gives damping ratio **1**: critically damped, the
fastest approach that cannot overshoot, and no tuning constant to pick — `lag` is read from the
plane's own `mass` and `turnResponsiveness`, so a heavy plane brakes earlier on its own.

The cost is honest: at full rate the plane needs `maxRate * lag` — 90° at the stock numbers — to
stop, so a 180° reversal turns hard for the first half and eases through the second. That is the
inertia the model already had; the keyboard just left the player to unwind it by hand.

**The 180° guard.** `DeltaAngle` returns `+180` for an exact reversal, so a plane already
rolling left would be told to reverse and turn right — the same angle, but the long way in
*time*, because the existing angular velocity has to be unwound first. Within `ReverseGuard`
(0.15 rad) of a reversal the error takes the sign of the current `angularVelocity` instead, so a
turn in progress is never asked to flip. Away from that band the sign of the error decides, as
it should.

**Roll.** The keyboard branch calls a turn "steady" when neither key is down. The heading branch
has no key to look at, so it asks `PlaneRoll.Steady(desiredRate, maxRate)` — the helper that
already existed for the AI — which reads as steady once the plane is aligned to within about a
degree. That is what keeps the barrel roll from firing mid-turn.

## Visibility and hand-off

`LevelHud.TickSteering` runs every frame and gates both the stick and the arrow on
`plane.Steerable && !GameMenu.IsOpen` — that is, active, controlled, and not falling. So the
`LevelIntro` fly-in (which calls `SetControlled(false)`), the pause menu and the death spiral all
hide them.

While hidden, the target heading is pinned to the plane's live heading. Without that, an intro
that turned the plane would end with a stale target from before the cut and the plane would
snap onto it the moment control returned. Pinning means the arrow always comes back pointing
where the nose already is, and the player starts from rest.

## Not on desktop

`A` / `D` are ignored on a touch build even with a Bluetooth keyboard attached: the two models
cannot both own `desiredRate` without one stuttering the other, and the stick is the one the
build is laid out for. The desktop build never enters the branch at all.
