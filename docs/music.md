# Music system

## Overview

Soundtracks are procedurally synthesized at runtime from note data in JSON files — no audio
assets are shipped. A track is **baked offline into `AudioClip`s** the first time it is
requested, then played through ordinary `AudioSource`s.

There are **two renderers**, chosen per track file:

| Engine | Selected by | Output | Character |
| --- | --- | --- | --- |
| chiptune (default) | no `engine` key | mono | naive oscillators, AD envelope, one noise channel |
| retrowave | `"engine": "retrowave"` | stereo | supersaw, resonant filter + filter envelope, ADSR, drum kit, ping-pong delay, plate reverb, sidechain |

The chiptune renderer is untouched legacy code. Tracks that do not declare `engine` render
**byte-for-byte identically** to how they always have — the retrowave features are strictly
opt-in, so adding one track's worth of new sound cannot disturb the other nineteen.

A track bakes into two clips:

* **intro** — the sequence entries before `loopStart`, played once;
* **loop** — the rest, on a looping `AudioSource`.

The loop source is queued with `AudioSource.PlayScheduled(introStart + introLength)`, so the
intro→loop handoff is sample-accurate on the DSP clock.

## Files

| File | Role |
| --- | --- |
| `Assets/Music/Resources/Music/*.json` | The soundtracks (note data). |
| `MusicJson.cs` | Minimal JSON reader (objects, arrays, strings, numbers) — the note tuples mix strings and numbers, which `JsonUtility` cannot read. |
| `MusicConfig.cs` | Data model and `MusicLibrary`, which loads + parses + caches configs from `Resources/Music/<id>`. Pitch names resolve to frequencies here (A4 = 440 Hz, `#`/`b` supported). |
| `MusicSynth.cs` | Bake entry point + the legacy chiptune renderer. `Bake(config, rate)` is pure sample math; `ToClips` wraps the result in `AudioClip`s. |
| `MusicSynthRetro.cs` | The stereo retrowave renderer. |
| `MusicPlayer.cs` | Persistent player (bootstraps itself `BeforeSceneLoad`, `DontDestroyOnLoad`). Owns the two `AudioSource`s and the fades, and reacts to scene loads. |

The JSON lives in `Assets/Music/Resources/Music/` rather than `Assets/Resources/` because
`.gitignore` excludes `/Assets/Resources` as private content — any folder named `Resources`
is a resources root, so `Resources.Load<TextAsset>("Music/raptor-march-neon")` finds them.

> Note: `.gitignore` currently also excludes `/Assets/Music`, so the soundtracks are **not
> tracked by git** despite living outside `Assets/Resources`. Remove that line if they are
> meant to ship with the repo.

## Baking off the main thread

The chiptune bake is ~30–200 ms, but a retrowave bake is ~1.1 s (stereo, seven-voice
supersaws, per-note filters, reverb). That is far too long to block a scene load, so
`MusicPlayer.Play` does the work on a worker thread:

1. `MusicLibrary.Load(id)` runs on the main thread (`Resources.Load` requires it).
2. `MusicSynth.Bake(config, rate)` runs via `Task.Run` — it touches no Unity API, only
   `float[]` math, so it is thread-safe.
3. `Update` polls the task; on completion `MusicSynth.ToClips` creates the `AudioClip`s on
   the main thread and playback begins with the requested fade.

A `Play` call for a track still baking is coalesced; `FadeOutAndStop` cancels a pending
start. Rendered clips are cached per track id for the lifetime of the app, so re-entering
the menu replays the cached bake instantly.

## Menu music

`MusicPlayer` is scene-driven; nothing in any scene references it:

* **MainMenu loads** → plays `MusicPlayer.MenuThemeId` (currently `raptor-march`) with a
  **1.5 s fade-in**. Because the bake is asynchronous, the music starts a beat after the
  menu appears on first load and instantly on every later return.
* **Any other scene loads** → **0.8 s fade-out**, then the sources stop.

Fades scale both sources' `volume` and run on unscaled time. `GameManager`'s master volume
still applies on top, via `AudioListener.volume`.

To give a level its own soundtrack, call `MusicPlayer.Instance.Play("<id>", fadeSec)`.

## Music JSON format

A track file is a tracker-style document: named synth **tracks** (instruments), named
**patterns** (sections) holding one note line per track, and a **sequence** ordering the
patterns.

Top level: `tempo` (BPM, one beat = one quarter note), `volume` (master gain 0–1),
`loopStart` (index into `sequence` where playback resumes; `1` means entry 0 is a play-once
intro), `tracks`, `patterns`, `sequence`. Optional: `engine`, `name`, `fx`, `sidechain`.

Note tuple: `[pitch, durationBeats, velocity?]` — pitch is a name (`"A4"`, `"C#5"`,
`"Bb3"`), a raw Hz number, or `null`; velocity is a 0–1 loudness multiplier. A pattern's
length is its longest line. **Velocity `0` is a rest** — the note advances time and makes no
sound, which is how drum lines encode gaps.

### Chiptune track fields

`wave` (`sine` | `square` | `triangle` | `sawtooth` | `noise`), `volume`, optional `detune`
(cents — spawns two voices at ±half the value), optional `attack` / `release` envelope
seconds (defaults 0.01 / 0.05; noise attack 0.005). `noise` ignores pitch and plays filtered
white noise (highpass at 3.5 kHz).

## Retrowave engine

Set `"engine": "retrowave"` at the top level. `raptor-march-neon.json` is the reference
template — copy its shape for new tracks.

### Top-level extras

```json
"fx": {
  "delay":  { "beats": 0.75, "feedback": 0.32, "mix": 0.5, "damp": 3800, "pingpong": true },
  "reverb": { "size": 0.84, "damp": 0.38, "mix": 0.45, "width": 1 }
},
"sidechain": { "source": "kick", "amount": 0.75, "attack": 0.004, "release": 0.26 }
```

* **delay** — `beats` is the echo time in beats, so it stays tempo-synced (`0.75` = dotted
  eighth, the retrowave default). `pingpong` alternates echoes across the stereo field.
* **reverb** — Freeverb-style plate: 8 combs + 4 allpasses per channel. `size` 0–1,
  `damp` 0–1 (high-frequency absorption), `width` 0–1.
* **sidechain** — builds a duck envelope from the `source` track's onsets and applies it to
  every track in proportion to that track's `duck` value. This is the pump.

### Synth track fields

| Field | Meaning |
| --- | --- |
| `wave` | `sine` \| `square` \| `pulse` \| `triangle` \| `sawtooth` |
| `pulseWidth` | duty cycle for `pulse` (default 0.5) |
| `voices` | supersaw stack size, 1–7+ (default 1) |
| `spread` | total detune across the stack, cents |
| `width` | how far the stack fans across stereo, 0–1 |
| `pan` | −1 left … +1 right |
| `gate` | note-on length as a fraction of its slot (default 0.9); the release rings on **past** the slot, so pads with `gate: 1.0` overlap legato |
| `glide` | portamento seconds from the previous pitch on the same line |
| `duck` | how strongly the sidechain applies, 0–1 |
| `adsr` | `{ attack, decay, sustain, release }` in seconds / 0–1 |
| `filter` | `{ cutoff, resonance, env, decay, keytrack }` |
| `send` | `{ delay, reverb }` bus sends, 0–1 |

`filter` is a TPT state-variable lowpass. `cutoff` is the base in Hz, `resonance` 0–1,
`env` is how many Hz the filter envelope adds at the note's start, `decay` how fast that
envelope falls, and `keytrack` 0–1 scales the cutoff with pitch so high notes stay open.
The signature synthwave sweep is a high `env` with a short `decay`.

Signal path per note: oscillator stack → filter → amplitude ADSR → pan → dry bus + sends.

### Drum tracks

A track with a `drum` field is percussion; its note pitches are ignored.

| `drum` | Voice | Tunable fields |
| --- | --- | --- |
| `kick` | pitch-swept sine + click transient, saturated | `tune`, `drop`, `decay`, `click` |
| `snare` | two-tone body + highpassed noise, optional gated reverb | `tune`, `decay`, `noiseMix`, `gatedReverb`, `gateHold` |
| `clap` | three noise bursts then a tail | `decay` |
| `hat` | highpassed noise (use a short `decay` for closed, long for open) | `decay`, `pan` |

All four voices honour `pan`. Two equal-power laws are in play: `hat` keeps the original
0.707-at-centre law it has always used, while `kick` / `snare` / `clap` use a law normalised
to **unity at centre**, so adding `pan` to the kit leaves every existing track's mix
bit-identical. Panning is applied to the dry bus only — the `send` buses stay pre-pan.

`gatedReverb` (0–1) adds the abrupt 80s snare wash, held for `gateHold` seconds then cut in
12 ms. A drum's length comes from its own `decay`, **not** from its slot, so the slot
duration only positions the next hit.

Drum lines are therefore written as one tuple per hit, where the duration is the distance to
the next hit:

```json
"kick": [
  [null, 1, 1], [null, 1, 0.86], [null, 1, 0.86], [null, 1, 0.86]
],
"snare": [
  [null, 1, 0], [null, 2, 0.88], [null, 1, 0.92]
]
```

### Seamless looping

The loop clip is rendered with wrap-around: a note (or its release tail) that runs past the
end of the loop is folded back into the start, and the delay and reverb are run over the
buffer **twice**, keeping only the second pass, so their tails arrive at the loop point
already saturated. The result is a loop with no gap and no swelling reverb on repeat.

The intro clip is rendered 2.5 s **longer** than its musical length so its own reverb and
delay tails survive the handoff. `RenderedMusic.IntroDuration` still reports the musical
length, so the loop is scheduled on the beat while the non-looping intro source plays its
tail out underneath.

## Differences from the web engine

* Oscillators are naive shapes, not band-limited like Web Audio's — slight aliasing shimmer
  on high square/saw harmonics.
* The noise source is seeded, so a bake is deterministic.
* The web `MusicSystem`'s pause/game-over/toggle plumbing is not ported. The music on/off
  preference (`mr_music_enabled`) has no equivalent here — volume control is `GameManager`'s
  master volume.

## The tracks

`air-assault` is the menu theme: the aerial-combat strain at 152 BPM in A minor, re-voiced
as retrowave (≈ 3.2 s intro + ≈ 50.5 s loop, stereo). Its lead, arp and bass note data is
unchanged from the chiptune original — the rework replaced the instrument definitions, split
the single noise `drums` line into a `kick` / `snare` / `clap` / `hat` / `openhat` kit and
added a sustained `pad` line tracing the chord tones.

`raptor-march-neon` (in `raptor-march.json`) is the menu theme and the reference template for
the format: the Raptor March harmony at 108 BPM in C major (≈ 4.4 s intro + ≈ 71.1 s loop).
See [The Raptor March arrangement](#the-raptor-march-arrangement) below.

The other 18 range from marches (`obsidian-march`, `brass-battalion`, `iron-requiem`,
`dread-legion`) through swing, surf and western (`squadron-swing`, `gallows-gallop`,
`slipstream`, `velvet-dossier`, `black-tide`) into synthwave, drum-and-bass, techno and
industrial (`neon-strafe`, `mach-break`, `klaxon-circuit`, `apex-colossus`, `flak-parade`,
`thunder-run`, `steel-talons`, `iron-skies`), all with 50–150 s loops — all still on the
chiptune engine, none assigned yet.

## Two-half track structure

Every loop is built from two halves of equal length. The first half is the original
material as migrated from the web repo; the second is a later composition pass that adds
new patterns and appends them to `sequence`, so a loop plays fresh music for its whole
second half before repeating.

The added patterns follow the naming already used by each file — a new main strain is
`mainC` (and `mainD` where a track already had two), a second quiet section is `bridge2`
/ `breakdown2`, and a second peak is `climax2` (`finale` where the track's peak was
already called `climax`). They keep the track's tempo, key, instrument definitions and
per-section drum grooves; only the harmony and melody are new.

## The Raptor March arrangement

`raptor-march.json` layers ten synth and drum voices on top of the original four-part
(lead / arp / horns / bass) skeleton, so the same melody is re-scored rather than rewritten.
Nothing about the tempo, key, chord progression or lead line changed; the extra voices fill
the register above and below it and give each section a different amount of company.

**Register allocation** — every voice owns a band, which is what keeps twenty lines from
turning into mud:

| Voice | Band | Role |
| --- | --- | --- |
| `sub` | 55–98 Hz | sine sub-bass on the chord root, one note per chord, `glide` between them. Held to a single octave (roots below C fold up: A → A1, Bb → Bb1) so the floor never leaps around. `duck: 0.9` keeps it out of the kick's way. |
| `bass` | 87–294 Hz | unchanged octave-jumping saw |
| `arp` | 131–466 Hz | unchanged eighth-note arpeggio |
| `horns` | 165–349 Hz | unchanged sustained brass |
| `pad` / `padHi` | 247–494 Hz | two slow-attack chord voices (saw + triangle), voice-led so the pair is a real two-note chord rather than a doubled line. `gate: 1.0` plus a long release makes consecutive chords cross-fade. |
| `counter` | 175–784 Hz | pulse-wave counter-riff: an off-beat sixteenth figure that interlocks with the lead instead of doubling it, panned right against `harm` |
| `harm` | 294–698 Hz | the lead's harmony voice — same rhythm, a third or sixth below, resolved to chord tones, panned left |
| `lead` | 392–880 Hz | unchanged melody |
| `bell` | 659–1397 Hz | plucked triangle accents (`sustain: 0`) with a heavy dotted-eighth delay send — the sparkle that sprays across the bar line |

**Percussion additions** — `shaker` (sixteenths, panned left), `ride` (eighths, panned
right), `crash` (a long-decay hat on each section downbeat) and `tomHi` / `tomLo`, a pair of
short pitched kicks panned hard opposite each other so end-of-section fills sweep the stereo
field. These are what the new drum `pan` support is for.

**Arrangement arc** — the loop no longer repeats `mainA` and `mainC` verbatim. The sequence
is `intro · mainA · mainA2 · mainB · bridge · mainC · mainC2 · mainD · bridge2`, where the
`…2` patterns carry identical melody and drum grooves to their originals but add the
counter-riff, the full bell line, the shaker and a tom fill. So the first statement of a
strain is plain and its repeat answers it, and the loop's total length is unchanged.

| Section | harm | pad | sub | counter | bell | shaker | ride | toms | crash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `intro` | pickup | ✓ | bar 2 | — | pickup | — | — | — | ✓ |
| `mainA` | ✓ | ✓ | ✓ | — | sparse | — | — | — | ✓ |
| `mainA2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `mainB` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `bridge` | ✓ | ✓ | ✓ | melodic | sparse | — | ✓ | — | ✓ |
| `mainC` | ✓ | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | ✓ |
| `mainC2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `mainD` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `bridge2` | ✓ | ✓ | ✓ | melodic | sparse | — | ✓ | — | ✓ |

**Headroom** — the extra voices roughly double the summed signal, so master `volume` dropped
0.46 → 0.42, `lead` 0.34 → 0.33, `horns` 0.20 → 0.17 (the pads now cover that register) and
`bass` 0.45 → 0.42; the delay and reverb mixes came down a little too, since ten more voices
feed those buses. The new voices sit at 0.075–0.26. Occasional peaks still reach `SoftClip`,
which is the intended glue.

Bake cost scales with total voice-seconds, and the pads are the expensive part (five voices
holding whole bars with a long release). Expect roughly double the previous ≈ 1.1 s bake —
still well inside the asynchronous window, and still cached for the lifetime of the app.
