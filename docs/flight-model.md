# Player flight model: cruise + dive energy

Implemented in `CubeController` (`UpdateSpeed`), tuned via `PlayerConfig`
(Assets/Resources/PlayerConfig.asset).

## Concept

The plane flies at constant throttle. The engine is strong enough that the plane
never stalls — it can fly a full loop and never drops below cruise speed. But it
is not a constant *speed*: pointing the nose at the ground trades altitude for
airspeed, so dives are faster than cruise.

Speed is a single scalar along the heading; each physics step it changes by
three terms:

1. **Gravity along the flight path**: `-sin(heading) * diveAcceleration`.
   Nose-down (sin < 0) accelerates the plane; nose-up decelerates it, which
   bleeds off dive speed on the way back up (a dive-then-zoom-climb roughly
   conserves that energy).
2. **Drag on the excess**: `(speed - flySpeed) * speedDrag` is shed per second,
   so extra speed also decays in level flight instead of persisting forever.
3. **Clamp**: speed never drops below `flySpeed` (the throttle floor — this is
   the "no stall" rule) and never exceeds `flySpeed * maxSpeedMultiplier`.

The drag term gives a natural terminal dive speed before the hard cap:
`flySpeed + diveAcceleration * |sin(heading)| / speedDrag`.

## Tunables (PlayerConfig)

| Field | Default | Meaning |
|---|---|---|
| `flySpeed` | 180 (asset) | Cruise speed and the guaranteed minimum (m/s) |
| `diveAcceleration` | 90 | Gravity pull along the path at straight-down (m/s²) |
| `speedDrag` | 0.9 | Fraction of excess speed shed per second |
| `maxSpeedMultiplier` | 1.6 | Hard cap as a multiple of `flySpeed` |

With the defaults: a straight-down dive tends toward 180 + 90/0.9 = 280 m/s
(capped at 288), a 45° dive toward ≈ 250 m/s, and after levelling out the
excess halves roughly every 0.8 s. Keep `bulletSpeed` (400) well above the
cap so rounds still pull away from the plane in a dive.

## Scope

Player only. Enemies (`EnemyController`) keep their constant `flySpeed`. The
shot-down fall is a separate mode (real rigidbody gravity, see
`CubeController.BeginFall`) and is untouched by this model.
