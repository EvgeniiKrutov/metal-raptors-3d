# Music system

## Overview

Soundtracks are procedurally synthesized at runtime from note data in JSON files — no audio
assets are shipped. A track is **baked offline into `AudioClip`s** the first time it is
requested, then played through ordinary `AudioSource`s.

There are **two renderers**, chosen per track file:

| Engine | Selected by | Output | Character |
| --- | --- | --- | --- |
| chiptune (default) | no `engine` key | stereo (amplitude-panned per track) | naive oscillators, AD envelope, one noise channel |
| retrowave | `"engine": "retrowave"` | stereo (pan + width per track, stereo delay/reverb) | supersaw, resonant filter + filter envelope, ADSR, drum kit, ping-pong delay, plate reverb, sidechain |

Both renderers bake **two-channel** clips — see [Stereo output](#stereo-output). Apart from
that placement, the chiptune renderer is untouched legacy code: the voices, envelopes and
noise are the same sample math they always were, and the retrowave features stay strictly
opt-in.

A track bakes into two clips:

* **intro** — the sequence entries before `loopStart`, played once;
* **loop** — the rest, on a looping `AudioSource`.

The loop source is queued with `AudioSource.PlayScheduled(introStart + introLength)`, so the
intro→loop handoff is sample-accurate on the DSP clock.

## Files

| File | Role |
| --- | --- |
| `Assets/Music/Resources/Music/*.json` | The soundtracks (note data). |
| `Json.cs` | Minimal JSON reader (objects, arrays, strings, numbers) — the note tuples mix strings and numbers, which `JsonUtility` cannot read. Shared with the campaign scripts (docs/campaign-scripts.md). |
| `MusicConfig.cs` | Data model and `MusicLibrary`, which loads + parses + caches configs from `Resources/Music/<id>`. Pitch names resolve to frequencies here (A4 = 440 Hz, `#`/`b` supported). |
| `MusicSynth.cs` | Bake entry point + the chiptune renderer, including its per-track pan table. `Bake(config, rate)` is pure sample math; `ToClips` wraps the result in `AudioClip`s. |
| `MusicSynthRetro.cs` | The retrowave renderer. |
| `MusicPlayer.cs` | Persistent player (bootstraps itself `BeforeSceneLoad`, `DontDestroyOnLoad`). Owns the two `AudioSource`s and the fades, and reacts to scene loads. |
| `AudioOutput.cs` | Keeps the audio session and the mixer in stereo — see [Output configuration](#output-configuration-audiooutput). |
| `Assets/Plugins/iOS/MetalRaptorsAudio.mm` | Sets the iOS audio session category to `Playback`, which is what gives an iPhone its second speaker. |

The JSON lives in `Assets/Music/Resources/Music/` rather than `Assets/Resources/` because
`.gitignore` used to exclude all of `/Assets/Resources` as private content — any folder named
`Resources` is a resources root, so `Resources.Load<TextAsset>("Music/raptor-march-neon")`
finds them (docs/conventions.md).

`.gitignore` no longer excludes `/Assets/Music`, so the soundtracks are tracked and ship with
the repo.

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

### Start-latency scheduling

`MusicPlayer` starts the intro as soon as it is baked, held back only long enough that the
still-rendering loop is predicted to land before the intro's musical end — the loop enters
on the intro's last beat, so holding the intro back is what keeps that handoff clean. The
prediction comes from the intro's own measured bake-cost-per-second of audio: both halves
render the same way, so the intro's cost predicts the loop's, on this machine, at this load.
`LoopBakeSafety` (1.3) adds margin to that prediction, `HandoffMarginSec` (0.35 s) is how
long before it is due the loop clip must already exist, `MaxStartWaitSec` (2.5 s) caps how
long the intro is ever held back, and if the loop still misses its slot it is scheduled
`LateLoopDelaySec` (0.05 s) later — as soon as possible, under the intro's tail.

`Prewarm(id)` bakes a track into the cache without playing it, so a later `Play` is instant.
`MusicPlayer` calls `Prewarm(MenuThemeId)` from `Awake` (itself invoked from the
`BeforeSceneLoad` bootstrap), so that bake overlaps the first scene's load and UI
construction instead of starting only once `Start` requests playback.

The volume ramp does not start ticking until the scheduled playback actually begins, so a
fade is never spent counting down through silence before the DSP-scheduled start.

## Menu music

`MusicPlayer` is scene-driven; nothing in any scene references it:

* **MainMenu loads** → plays `MusicPlayer.MenuThemeId` (currently `black-tide`) with a
  **1.5 s fade-in**. Because the bake is asynchronous, the music starts a beat after the
  menu appears on first load and instantly on every later return.
* **Any other scene loads** → **0.8 s fade-out**, then the sources stop.

Fades scale both sources' `volume` and run on unscaled time. The options page's `music` row
scales the 0.45 bed those fades aim at, and re-aims `_volumeTarget` live from
`AudioOptions.Changed` — so a 5% step is a fast ramp on the same `MoveTowards`, not a jump.
The `general` row still applies on top, via `AudioListener.volume` (docs/options.md).

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
seconds (defaults 0.01 / 0.05; noise attack 0.005), optional `pan` (−1 left … +1 right).
`noise` ignores pitch and plays filtered white noise (highpass at 3.5 kHz).

`pan` is read whenever the key is present — including `"pan": 0`, which is how a track opts
out of the default placement below (`MusicTrack.HasPan` records that the key was written, so
an authored centre is not confused with an unauthored one).

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

## Stereo output

Both engines bake `Channels = 2`, interleaved L/R, and `MusicSynth.ToClip` divides the sample
count by that to get the frame count — so an `AudioClip` is always created two-channel.

**Why it matters beyond the mix:** an iPhone drives its second speaker (the earpiece) only
for stereo playback; anything less comes out of the bottom speaker alone. A one-channel clip
is one way to land there — what the twelve chiptune tracks used to bake, and what would have
hit every one of them the moment it played. It was not, however, why the *menu* theme played
from one speaker: that track is retrowave and has always been two-channel. The rest of that
story is the audio session, in [Output configuration](#output-configuration-audiooutput).

### Chiptune placement

The chiptune engine has no pan controls in its authored data, so a track's placement comes
from its **name**, through `MusicSynth.TrackPans`:

| Left | Centre | Right |
| --- | --- | --- |
| `lead` −0.20, `hats` −0.30, `organLow` −0.22, `lowbrass` −0.28 | anything not listed — `bass`, `kick`, `snare`, `drums` | `harmony` +0.28, `arp` +0.42, `organMid` +0.26, `organHigh` +0.40, `horns` +0.30, `toms` +0.24, `pulse` +0.30, `grit` +0.30, `drone` +0.22 |

The names are the ones the twelve chiptune files already use, and an unknown name lands in
the centre, so a new track is centred until it is either named like an existing role or given
an explicit `pan`. The values are set against each other rather than picked in isolation: no
file leans to one side once its own tracks are placed (`grit` and `toms` sit right precisely
because the `lead` in those files is left).

Low end and backbeat stay centred, which is ordinary mixing practice and also what keeps the
loudest material identical to the old mono bake.

### The panning law

`StereoGain` is a plain balance law: a track panned right keeps `R = 1` and loses the pan
from the left (`L = 1 − pan`), and the mirror for one panned left — not the equal-power law
`MusicSynthRetro.PanGains` uses. It is chosen for what it
does **not** do: neither channel is ever boosted above the mono amplitude, so the final
`Mathf.Clamp(±1)` over the buffer clips exactly where it used to. A centred track writes the
full signal to both channels, unchanged from the mono bake.

## Output configuration (`AudioOutput`)

Two things have to be stereo before an iPhone will use its second speaker, and only one of
them is Unity's:

1. **The iOS audio session category.** This is the one that was actually keeping the earpiece
   silent. Unity does not set it — `UnityAppController.mm` only touches the session when the
   audio manager is disabled ("FMOD should have already handled all of this AVAudioSession
   init"), and FMOD picks the category from Player Settings: `AVAudioSessionCategoryAmbient`
   with *Mute Other Audio Sources* off, `SoloAmbient` with it on. Ambient audio is *secondary,
   mixable* audio as far as iOS is concerned, and it is routed to the primary bottom speaker.
   The stereo pair belongs to `AVAudioSessionCategoryPlayback` — the media-playback category.
2. **Unity's mixer speaker mode.** `AudioConfiguration.speakerMode`, which is what
   `AudioSettings.Reset` re-opens the output with.

`AudioOutput.EnsureStereo` does both, in that order — the session first, because the mixer has
to re-open *against* the corrected route. It runs at `BeforeSplashScreen` (after FMOD has
initialised, so the category is ours and not overwritten), from `MusicPlayer.Bootstrap`, at
the top of `MusicPlayer.Play`, on resume (`MusicPlayer.OnApplicationPause`), and on a device
change. Every one of those is the same idempotent call.

### The plugin

`Assets/Plugins/iOS/MetalRaptorsAudio.mm` sets `Playback`, activates the session, asks for
`setPreferredOutputNumberOfChannels: 2` when the hardware has them, and returns
`outputNumberOfChannels` so the managed side can see what the route actually gave. It also
`NSLog`s the category, options and channel count on the way in and out — the in-line is what
names the category the platform had chosen, so an Xcode console says in two lines whether the
route was the problem.

`AllowMixing` (true) keeps `AVAudioSessionCategoryOptionMixWithOthers`, so a podcast or
Spotify playing behind the game is not stopped — the same courtesy `Mute Other Audio Sources:
0` was buying under Ambient. If the route comes back with fewer than two channels,
`ConfigureRoute` retries **without** the option, since a stereo game that interrupts the music
app is better than a mono one that does not. Set the constant to `false` to skip straight to
the exclusive route.

### What `Playback` costs

**The ring/silent switch no longer mutes the game.** That behaviour is a property of the
category, not of the mixing option: `Ambient` is silenced by the switch, `Playback` is not,
and only `Playback` reaches the second speaker. There is no combination that keeps both, so
this is the trade the fix makes — the same one every media app makes. Reverting means going
back to one speaker: delete the plugin call and the game is `Ambient` again.

### Not resetting the mixer twice

`AudioSettings.Reset` stops every source, so it must not fire on an ordinary `Play`.
`ConfigureRoute` returns `true` only when the channel count **changed** since the last call, so
the reset happens once at launch and afterwards only on a real route change. The startup
sequence settles at the first call: the `BeforeSplashScreen` pass configures the session, sees
the count go from unknown to 2, and resets the mixer while nothing is playing; every later call
is a no-op.

The path is self-stabilising rather than looping, because the callbacks disagree about what
they answer: `Apply` ignores the `OnAudioConfigurationChanged` its own reset raises
(`deviceChanged` is false there), and the one a genuine route change raises finds the channel
count unchanged and the mode already stereo. `MusicPlayer` listens to the same event and
restarts the current track, so a reset — ours or the platform's — does not leave the menu
silent.

`ProjectSettings/AudioManager.asset` already asks for stereo (`Default Speaker Mode: 2`); the
managed half of this is the backstop for platforms that do not honour it at startup, and the
plugin is the half that iOS actually needed.

## Differences from the web engine

* Oscillators are naive shapes, not band-limited like Web Audio's — slight aliasing shimmer
  on high square/saw harmonics.
* The noise source is seeded, so a bake is deterministic.
* The web `MusicSystem`'s pause/game-over/toggle plumbing is not ported. The music on/off
  preference (`mr_music_enabled`) has no equivalent here — the options page's `music` row is
  the control, and 0% is the off switch (docs/options.md).

## The tracks

`raptor-march-neon` (in `raptor-march.json`) is the reference template for the format: the
Raptor March harmony at 108 BPM in C major (≈ 4.4 s intro + ≈ 71.1 s loop). The menu theme
(`MusicPlayer.MenuThemeId`) is currently `flak-parade`. See
[The Raptor March arrangement](#the-raptor-march-arrangement) below.

`air-assault` is the aerial-combat strain at 152 BPM in A minor, scored as retrowave off the
same template (≈ 3.2 s intro + ≈ 50.5 s loop, stereo). See
[The Air Assault arrangement](#the-air-assault-arrangement) below. Not assigned to a scene
yet.

`apex-colossus` is the industrial strain at 180 BPM in D minor, also re-scored as retrowave
(≈ 5.3 s intro + ≈ 138.7 s loop, stereo). See
[The Apex Colossus arrangement](#the-apex-colossus-arrangement) below. Not assigned to a
scene yet.

`black-tide` is the dark surf strain at 138 BPM in E minor, re-scored as retrowave off the
same template (≈ 7.0 s intro + ≈ 125.2 s loop, stereo). See
[The Black Tide arrangement](#the-black-tide-arrangement) below. Not assigned to a scene
yet.

`brass-battalion` is the military march at 132 BPM in F major, re-scored as retrowave off the
same template (≈ 7.3 s intro + ≈ 145.5 s loop, stereo — the longest loop in the library). See
[The Brass Battalion arrangement](#the-brass-battalion-arrangement) below. Not assigned to a
scene yet.

`dread-legion` is the dark march at 112 BPM in C minor, re-scored as retrowave off the same
template (≈ 8.6 s intro + ≈ 137.1 s loop, stereo). See
[The Dread Legion arrangement](#the-dread-legion-arrangement) below. Not assigned to a scene
yet.

`flak-parade` is the fast military parade at 164 BPM in G minor, re-scored as retrowave off
the same template (≈ 2.9 s intro + ≈ 105.4 s loop, stereo). See
[The Flak Parade arrangement](#the-flak-parade-arrangement) below. Not assigned to a scene
yet.

The other 13 range from marches (`obsidian-march`, `iron-requiem`) through
swing, surf and western (`squadron-swing`, `gallows-gallop`, `slipstream`, `velvet-dossier`)
into synthwave, drum-and-bass, techno and industrial (`neon-strafe`, `mach-break`,
`klaxon-circuit`, `thunder-run`, `steel-talons`, `iron-skies`), all with
50–150 s loops — all still on the chiptune engine, none assigned yet.

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

## The Air Assault arrangement

`air-assault.json` is the same twenty-voice scoring applied to the aerial-combat theme. The
`lead`, `arp` and `bass` note data is **unchanged** from the chiptune original — tempo (152),
key (A minor with the raised G# over E), chord progression and melody are all untouched. What
changed is the instrumentation: the four chiptune tracks were replaced with the retrowave
definitions, the single noise `drums` line was split into a nine-piece kit, and seven new
synth voices were written around the existing material.

**Register allocation** — the same band-per-voice discipline as Raptor March, transposed to
A minor:

| Voice | Band | Role |
| --- | --- | --- |
| `sub` | 55–98 Hz | sine sub-bass on the chord root, one note per bar, `glide` between them. Roots fold into a single octave (A → A1) so the floor never leaps. |
| `bass` | 82–165 Hz | unchanged root/fifth eighth-note pump |
| `arp` | 147–330 Hz | unchanged eighth-note arpeggio |
| `horns` | 165–349 Hz | two sustained brass notes per bar, rising through the chord |
| `pad` / `padHi` | 208–494 Hz | slow-attack saw + triangle pair, voice-led so the two lines spell a real chord: `pad` takes the root or third, `padHi` the fifth or seventh |
| `counter` | 175–831 Hz | pulse-wave counter-riff on the off-beat sixteenths, panned right |
| `harm` | 247–659 Hz | the lead's harmony voice — same rhythm, a third or sixth below, resolved to chord tones, panned left |
| `lead` | 220–880 Hz | unchanged melody |
| `bell` | 698–1319 Hz | plucked triangle accents (`sustain: 0`) with a heavy dotted-eighth delay send |

**Groove** — the chiptune `drums` line was one noise channel whose velocity accents implied a
backbeat (loud hits on beats 2 and 4, quieter ones on 1 and 3). The kit makes that explicit
rather than replacing it: `snare` + `clap` on 2 and 4, `kick` on 1, the "and" of 2, beat 3 and
the "and" of 4 — four hits a bar at uneven spacing, which drives the sidechain without
flattening into four-on-the-floor. Bridges drop to a half-time kick (beat 1 and the "and" of
3) with `ride` on quarters. `shaker` runs sixteenths panned left, `openhat` marks the "and" of
4, `crash` opens each section, and `tomHi` / `tomLo` pan hard opposite each other for the
end-of-section fills.

**Arrangement arc** — the loop is `intro · mainA · mainA2 · mainB · bridge · mainC · mainC2 ·
mainD · bridge2`, eight 16-beat sections after a 8-beat intro. As in Raptor March, the `…2`
patterns carry the same melody and drum groove as their originals but add the counter-riff,
the full bell line, the shaker and a tom fill, so each strain's repeat answers its first
statement. Total loop length is unchanged from the chiptune version.

| Section | harm | counter | bell | horns | pad | sub | clap | shaker | ride | toms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `intro` | pickup | — | pickup | ✓ | ✓ | bar 2 | — | — | — | — |
| `mainA` | ✓ | — | sparse | ✓ | ✓ | ✓ | — | — | — | — |
| `mainA2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill |
| `mainB` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill |
| `bridge` | ✓ | melodic | sparse | ✓ | ✓ | ✓ | — | — | ✓ | — |
| `mainC` | ✓ | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| `mainC2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill |
| `mainD` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill |
| `bridge2` | ✓ | melodic | sparse | ✓ | ✓ | ✓ | — | — | ✓ | — |

**Headroom** — every track volume and the master `volume` (0.42) are copied from Raptor March,
so the two themes sit at the same level and the same summed gain (≈ 2.06 across the synth
voices). Only the time-based settings were retuned for the faster tempo: sidechain `release`
0.22 → 0.14 s (a 0.5-beat gap is 0.197 s at 152 BPM, so a slower release would never recover),
delay `feedback` 0.32 → 0.28 and `mix` 0.45 → 0.40, reverb `mix` 0.28 → 0.24, and the synth
envelopes are shortened throughout — with 40% more note onsets per second, the Raptor March
decay times would smear.

## The Apex Colossus arrangement

`apex-colossus.json` is the same twenty-one-voice scoring applied to the industrial theme.
The `lead`, `arp`, `bass` and `alarm` note data is **unchanged** from the chiptune original —
tempo (180), key (D minor with the Neapolitan Eb, the Ab flat-five and the C# of the harmonic
minor V), chord progression, melody and the nine-entry `sequence` are all untouched. What
changed is the instrumentation, plus seven new synth voices and a ten-piece kit written
around the existing material.

Two structural moves, both of which preserve the original harmony exactly:

* The old **`pad` line became `horns`**. It was already a two-notes-per-bar root→fifth brass
  figure, which is precisely the `horns` role in the other two arrangements — at 180 BPM a
  slow-attack pad could never speak over its 0.67 s notes. The new `pad` / `padHi` pair holds
  whole-bar chords underneath it instead.
* The old **`alarm` siren was kept**, re-voiced from a bare detuned square to a three-voice
  resonant pulse with a heavy delay send. Its raw-Hz note tuples (`622`, `466`, …) survive
  verbatim; the engine reads numeric pitches as frequencies.

**Register allocation** — the same band-per-voice discipline, transposed to D minor:

| Voice | Band | Role |
| --- | --- | --- |
| `sub` | 44–78 Hz | sine sub-bass on the chord root, folded into the F1–E2 octave (the original line dropped to D1 ≈ 37 Hz, below most speakers) so the floor never leaps |
| `bass` | 49–196 Hz | unchanged sixteenth-note saw ostinato |
| `pad` | 98–208 Hz | whole-bar chord root, slow-attack saw |
| `horns` | 110–330 Hz | the original pad line: sustained root→fifth brass, two notes a bar |
| `padHi` | 220–440 Hz | whole-bar chord third, slow-attack triangle, voice-led so consecutive bars move by a step |
| `counter` | 175–622 Hz | pulse-wave counter-riff, six syncopated stabs a bar (0.5 · 0.75 · 1.75 · 2.5 · 2.75 · 3.75), panned right |
| `arp` | 131–1175 Hz | unchanged sixteenth-note octave-jump ostinato |
| `harm` | 349–1397 Hz | the lead's harmony voice — a diatonic third below in D natural minor, chromatics snapped to the nearest scale degree, panned left |
| `lead` | 415–1760 Hz | unchanged melody, re-voiced from square to a seven-voice supersaw |
| `bell` | 880–1397 Hz | plucked triangle accents on the last half-beat of the bar |
| `alarm` | 415–880 Hz | the siren, on its original intro / breakdown / climax entries |

**Groove** — the chiptune kick landed on beats 1, 3 and the "and" of 4, which is what drives
the sidechain; the kit keeps that spacing rather than flattening it to four-on-the-floor, and
the climaxes add a hit on beat 2. `snare` keeps the original backbeat-plus-ghost figure and
`hat` the original sixteenths. New: `clap` doubles the backbeat from `mainB` on, `openhat`
marks the "and" of 4, `ride` runs eighths through the peaks, `crash` opens each section,
`tomHi` / `tomLo` pan hard opposite for the end-of-section fills, and `shaker` is a rising
sixteenth build reserved for the two breakdowns (and the final `climax2`), where the original
already ramps the kick and rolls the snare.

| Section | harm | counter | bell | alarm | pad | clap | openhat | shaker | ride | toms | crash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `intro` | pickup | — | pickup | ✓ | ✓ | — | — | — | — | — | ✓ |
| `mainA` | ✓ | — | sparse | — | ✓ | — | ✓ | — | — | — | ✓ |
| `mainB` | ✓ | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | — | fill | ✓ |
| `breakdown` | ✓ | — | — | ✓ | pedal | — | — | ✓ | — | — | — |
| `climax` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ | fill | ✓ |
| `mainC` | ✓ | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | ✓ | — | ✓ |
| `mainD` | ✓ | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | ✓ | fill | ✓ |
| `breakdown2` | ✓ | — | — | ✓ | pedal | — | — | ✓ | — | — | — |
| `climax2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |

Both breakdowns are chromatic: the pad walks the descent (D→G) or the rise (G→D) in
half-notes under a held `padHi` pedal, which is what the original's chromatic lead and
arpeggio rise were already implying.

**Headroom** — every **per-track** volume is copied verbatim from Raptor March, so the three
retrowave themes share one palette. The master `volume` is **0.35, not 0.42**: this track
sounds up to 21 lines at once (Raptor March peaks at 14) over a sixteenth-note grid at 180
BPM, so the identical per-voice gains sum to a hotter bus. Measured over the baked loop, 0.42
gave −13.8 dBFS RMS with peaks at 0.96 — 1.6 dB above Raptor March and riding `SoftClip` hard
through the climaxes; 0.35 lands at −15.4 dBFS RMS / 0.88 peak, which is Raptor March's
−15.4 / 0.78. Trimming the master rather than the voices is the same move Raptor March itself
made when it went 0.46 → 0.42 after gaining voices. If you re-balance, re-measure: matching
the master number across files would *not* match the loudness.

Time-based settings scale with the tempo, at 108⁄180 = 0.6 of the Raptor March values:
sidechain `release` 0.22 → 0.12 s, delay `feedback` 0.32 → 0.26 and `mix` 0.45 → 0.36,
reverb `size` 0.84 → 0.72 and `mix` 0.28 → 0.20, and every synth envelope shortened to
match — at 180 BPM a sixteenth is 83 ms, so the Raptor March decay times would smear into
mush.

**Bake cost** — the loop is 416 beats ≈ 138.7 s, roughly twice Raptor March's, so expect
roughly twice its bake (order of 4–5 s on a worker thread). That is invisible behind the
asynchronous start and it is cached for the lifetime of the app, but it is the longest bake
in the library — worth knowing before assigning this track to a scene that loads quickly.

## The Black Tide arrangement

`black-tide.json` is the same twenty-voice scoring applied to the dark surf theme. The
`lead`, `arp` and `bass` note data is **unchanged** from the chiptune original — tempo (138),
key (E minor with the Neapolitan-ish F, the harmonic-minor B major and the A#dim tritone
that gives the track its bite), chord progression and melody are all untouched. What changed
is the instrumentation, plus six new synth voices and a ten-piece kit written around the
existing material.

One structural move, which preserves the original harmony exactly: the old **`harmony` line
became `horns`**. It was already a two-notes-per-bar sustained figure — precisely the `horns`
role in the other three arrangements — so it transfers verbatim, and the new `pad` / `padHi`
pair holds whole-bar chords underneath it instead.

**Register allocation** — the same band-per-voice discipline, transposed to E minor:

| Voice | Band | Role |
| --- | --- | --- |
| `sub` | 62–117 Hz | sine sub-bass on the chord root, one note per chord, `glide` between them. Folded into the B1–A#2 octave, which turns the Andalusian descent of `mainB` (E→D→C→B) into a stepwise slide rather than an octave leap |
| `bass` | 65–262 Hz | unchanged octave-jumping saw |
| `arp` | 87–392 Hz | unchanged eighth-note arpeggio |
| `pad` | 165–294 Hz | whole-bar chord root, slow-attack saw |
| `horns` | 196–523 Hz | the original harmony line: sustained brass, two notes a bar |
| `counter` | 196–784 Hz | pulse-wave counter-riff on the off-beat sixteenths, panned right |
| `padHi` | 247–440 Hz | whole-bar third or fifth, slow-attack triangle, whichever is nearer the previous bar's note, so the pair spells a real chord and moves by a step |
| `harm` | 247–784 Hz | the lead's harmony voice, panned left |
| `lead` | 330–988 Hz | unchanged melody, re-voiced from square to a seven-voice supersaw |
| `bell` | 587–1397 Hz | plucked triangle accents on the last beat of the bar |

`harm` is picked per note as the **highest seventh-chord tone at least three semitones below
the lead** — the sevenths matter, because this melody is full of passing chromatics that a
bare triad cannot shadow at a third (F#5 over the C chord in `mainB` would leap to a fifth
below without the B of Cmaj7). Every interval in the finished line is a third, fourth or
fifth below; a tritone falls back to the nearest scale degree, where the scale is E natural
minor with the chord's own accidentals replacing the degree they raise (D# under B, A#/C#
under A#dim).

**Groove** — the chiptune original had no kick: one loud noise line carried the accents (beats
1 and 3) and a quieter one ran eighths. The kit makes that explicit — four-on-the-floor with
beats 1 and 3 the loudest hits, so the original accent hierarchy survives as the sidechain
pump — plus `snare` on 2 and 4 with a sixteenth ghost after the first backbeat, `clap`
doubling beat 4, `hat` on eighths, `openhat` on the "and" of 4, `shaker` sixteenths panned
left, `ride` eighths through the peaks and `crash` on each section downbeat. Bridges drop to a
half-time kick (beat 1 and the "and" of 3) with the snare on beat 3 and `ride` on quarters.

The original's signature is kept intact: **every eight-bar section ends with a rising
sixteenth-note snare roll** across its last bar. Those bars now also carry a `tomHi` / `tomLo`
fill panned hard opposite each other and an extra kick on the final "and".

**Arrangement arc** — the original repeated `mainA` and `mainC` back to back. As in Raptor
March those repeats became `mainA2` / `mainC2`: identical melody, harmony and drum groove,
plus the counter-riff, the full bell line, the shaker and a tom fill, so each strain's repeat
answers its first statement. Total loop length is unchanged at 288 beats.

| Section | harm | counter | bell | horns | pad | sub | clap | shaker | ride | toms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `intro` | pickup | — | pickup | pickup | ✓ | bar 2 | — | — | — | — |
| `mainA` | ✓ | — | sparse | ✓ | ✓ | ✓ | — | — | — | — |
| `mainA2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill |
| `mainB` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill |
| `bridge` | ✓ | melodic | sparse | ✓ | ✓ | ✓ | — | — | ✓ | — |
| `climax` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill |
| `mainC` | ✓ | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| `mainC2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill |
| `bridge2` | ✓ | melodic | sparse | ✓ | ✓ | ✓ | — | — | ✓ | — |
| `climax2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill |

**Headroom** — every per-track volume and the master `volume` (0.42) are copied from Raptor
March. Measured over the baked loop that gives −15.9 dBFS RMS with a 0.878 peak, against
Raptor March's −15.4 / 0.784 and Apex Colossus's −15.4 / 0.876 — the same family, no
sustained clipping. The master was checked rather than assumed: 0.44 would match Raptor
March's RMS exactly but push the peak to 0.904.

Time-based settings scale with the tempo, at 108⁄138 ≈ 0.78 of the Raptor March values:
sidechain `release` 0.22 → 0.17 s, delay `feedback` 0.32 → 0.30 and `mix` 0.45 → 0.42,
reverb `size` 0.84 → 0.78 and `mix` 0.28 → 0.25, and the synth envelopes shortened to match.

**Bake cost** — the loop is 288 beats ≈ 125.2 s, so the bake is roughly twice Raptor March's
and just under Apex Colossus's. Asynchronous and cached, but the same caveat applies before
assigning it to a fast-loading scene.

## The Brass Battalion arrangement

`brass-battalion.json` is the same twenty-voice scoring applied to the military march. The
`lead`, `horns`, `counter`, `bass` **and `snare`** note data is **unchanged** from the
chiptune original — tempo (132), key (F major, with the Eb of the F7 mixture, the Bb trio and
the secondary A7/D7 of `mainB`), chord progression, melody and total length (336 beats) are
all untouched. What changed is the instrumentation, plus six new synth voices and a ten-piece
kit written around the existing material.

Two structural moves, both of which preserve the original harmony exactly:

* The old **`horns` line stayed `horns`** but changed patch. It is not the sustained brass of
  the other four arrangements — it is the march *afterbeat*, the off-beat eighth-note oom-pah
  that gives the track its stride (and, in `intro` / `climax` / `finale`, a high descant over
  the melody). So `horns` is voiced as a stab: `gate: 0.72` with a 12 ms attack rather than
  the 0.35–0.45 s swell used elsewhere. The new `pad` / `padHi` pair holds the whole-bar
  chords underneath it instead.
* The old **`cymbal` line became `openhat`**. It was already a plain hit on beats 2 and 4 of
  every bar, which is exactly the openhat role; in `climax` and `finale`, where the original
  doubled it to off-beat eighths, that figure moved to `ride`.

**Register allocation** — the same band-per-voice discipline, transposed to F major. The
melody sits a fourth higher than Raptor March's, which is what frees the 250–500 Hz band for
the afterbeats and the arpeggio:

| Voice | Band | Role |
| --- | --- | --- |
| `sub` | 44–82 Hz | sine sub-bass on the chord root, one note per chord, `glide` between them. Folded into the F1–E2 octave so the floor never leaps |
| `bass` | 87–175 Hz | unchanged staccato tuba line, its `[null, 0.5]` rests intact under `gate: 0.55` |
| `counter` | 131–349 Hz | the original trombone countermelody, re-voiced as a pulse wave panned right. `gate: 0.86`, not the clipped 0.68 of the other tracks, because this line is legato |
| `arp` | 131–466 Hz | new eighth-note arpeggio, root folded into C3–B3 so it interlocks with the afterbeats instead of masking them |
| `horns` | 220–330 Hz (880 Hz in the descant sections) | the original afterbeat, re-voiced as brass stabs |
| `pad` | 262–494 Hz | whole-bar chord root, slow-attack saw |
| `padHi` | 311–698 Hz | whole-bar third, fifth or seventh, slow-attack triangle, picked as the chord tone nearest the previous bar's note so the pair spells a real chord and moves by a step |
| `harm` | 349–988 Hz | the lead's harmony voice — same rhythm, the highest chord tone at least three semitones below, panned left |
| `lead` | 523–1175 Hz | unchanged melody, re-voiced from square to a seven-voice supersaw |
| `bell` | 587–1397 Hz | plucked triangle accents on the last beat of every other bar |

`harm` is **omitted from `intro`, `climax` and `finale`**. In those three sections the
original `horns` line is already a high descant harmonising the lead a third or sixth below —
a derived `harm` would land in unison with it and merely double the level.

**Groove** — the original had no kick: the march snare carried everything, accenting beats 1
and 3 of every bar across a ten-hit rudimental figure, with a rising sixteenth/thirty-second
roll closing most sections. That line is kept **verbatim**, and the kit is built to sit with
it rather than over it: four-on-the-floor `kick` with beats 1 and 3 loudest, so the original
accent hierarchy becomes the sidechain pump; `clap` on 2 and 4 from `mainA2` on, which is the
one genuinely new idea — a retrowave backbeat crossing the march's strong-beat accents;
`hat` on eighths, `openhat` on 2 and 4 (the original cymbal), `shaker` sixteenths panned left,
`ride` through the peaks, `crash` on each section downbeat, and `tomHi` / `tomLo` panned hard
opposite for a fill across the last two beats of bar 6 — clear of the bar-8 snare rolls.

Where `ride` runs eighths, `hat` drops to a quieter accent pattern; two eighth-note cymbal
lines at full level just double each other.

**Arrangement arc** — the original sequence repeated `mainA`, `mainC`, `mainB` and `trio`
verbatim. As in Raptor March those repeats became `mainA2` / `mainC2` / `mainB2` / `trio2`:
identical melody, harmony and drum groove, plus the full bell line, the shaker, the clap and
a tom fill, so each strain's repeat answers its first statement. The sequence is
`intro · mainA · mainA2 · mainB · trio · climax · mainC · mainC2 · mainB2 · trio2 · finale`
and the total length is unchanged at 336 beats.

| Section | harm | arp | bell | pad | sub | clap | openhat | shaker | ride | toms | crash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `intro` | horns | ✓ | sparse | ✓ | bar 2 | — | ✓ | — | — | — | ✓ |
| `mainA` | ✓ | ✓ | sparse | ✓ | ✓ | — | ✓ | — | — | — | ✓ |
| `mainA2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `mainB` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `trio` | ✓ | ✓ | sparse | ✓ | ✓ | — | ✓ | — | quarters | — | ✓ |
| `climax` | horns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | offbeat | fill | ✓ |
| `mainC` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ |
| `mainC2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `mainB2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `trio2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | quarters | fill | ✓ |
| `finale` | horns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | offbeat | fill | ✓ |

**Headroom** — every per-track volume is copied from Raptor March **except the snare**, and
the master is **0.44**. Both numbers were measured over the baked loop rather than assumed:

| Track | master | RMS | peak |
| --- | --- | --- | --- |
| Raptor March | 0.42 | −15.4 dBFS | 0.831 |
| Apex Colossus | 0.35 | −15.4 dBFS | 0.826 |
| Black Tide | 0.42 | −15.9 dBFS | 0.835 |
| Brass Battalion | 0.44 | −15.9 dBFS | 0.859 |

The snare is the whole story here. This line plays **ten hits a bar** where Raptor March's
plays two, so at the shared palette settings it dominated the mix: muting it dropped the
4 kHz-and-up energy by 13 dB, while muting `hat`, `openhat`, `shaker`, `ride` and `crash`
one at a time each changed it by less than 0.05 dB. At `volume: 0.66` / `noiseMix: 0.7` /
`gatedReverb: 0.7` the track carried 3.9 dB more high-band energy than Raptor March and
peaked at 0.907.

The fix was `gatedReverb` **0.7 → 0.4**, not a level cut: the 80s gated wash is designed for a
sparse backbeat, and across continuous rudiments its tails overlap into a permanent hiss.
That one change bought 2.5 dB of high band and took the peak from 0.907 to 0.843 at unchanged
loudness. With `volume` 0.66 → 0.52 and `noiseMix` 0.7 → 0.45 on top, the peak fell far enough
to raise the master from 0.42 to 0.44 and land on Black Tide's loudness. The track is still
brighter than the rest of the library, which is correct — it is a snare march.

Time-based settings scale with the tempo, at 108⁄132 ≈ 0.82 of the Raptor March values:
sidechain `release` 0.22 → 0.18 s, delay `feedback` 0.32 → 0.30 and `mix` 0.45 → 0.42, reverb
`size` 0.84 → 0.79 and `mix` 0.28 → 0.25, and the synth envelopes shortened to match.

**Bake cost** — the loop is 320 beats ≈ 145.5 s, the longest in the library, overtaking Apex
Colossus. Measured at ≈ 8.5 s on one worker thread. Asynchronous and cached for the lifetime
of the app, but do not assign this track to a scene that loads quickly and expects music
immediately.

## The Dread Legion arrangement

`dread-legion.json` is the same scoring applied to the dark march, at twenty-one voices. The
`lead`, `lowbrass`, `bass`, `snare` **and `hats`** note data is **unchanged** from the
chiptune original — tempo (112), key (C minor with the Ab of the flat submediant, the
Neapolitan Db, and the B natural of the harmonic-minor V), chord progression, melody and
total length (272 beats) are all untouched. What changed is the instrumentation, plus seven
new synth voices and a ten-piece kit written around the existing material.

Two structural moves, both of which preserve the original harmony exactly:

* The old **`harmony` line became `horns`**. It was already a two-notes-per-bar sustained
  figure — precisely the `horns` role in the other arrangements — so it transfers verbatim,
  and the new `pad` / `padHi` pair holds whole-bar chords underneath it instead.
* The old **`lowbrass` line stayed `lowbrass`**, which is the one voice this track has and
  the others do not. It is the march ostinato — a dotted `0.75 · 0.25` downbeat then straight
  eighths, mostly on a repeated pitch — so it is voiced as a four-voice saw *stab*
  (`gate: 0.62`, 12 ms attack, cutoff 780 Hz) rather than the slow swell used for `horns`. It
  owns 98–247 Hz outright, which is what leaves the 260–620 Hz band free for the new `arp`.

**Register allocation** — the same band-per-voice discipline, transposed to C minor:

| Voice | Band | Role |
| --- | --- | --- |
| `sub` | 49–87 Hz | sine sub-bass on the chord root, one note per chord, `glide` between them. Folded into the G1–F#2 octave, which is what turns the track's constant Ab→G cadence into a semitone step and G→C into a rising fourth |
| `bass` | 65–208 Hz | unchanged octave-jumping line, re-voiced from triangle to saw |
| `lowbrass` | 98–247 Hz | the original march ostinato, re-voiced as brass stabs |
| `pad` | 196–349 Hz | whole-bar chord root, slow-attack saw |
| `arp` | 262–622 Hz | new eighth-note arpeggio. Written in **inversions** rather than root position — Ab is voiced C–Eb–Ab–C, Fm as C–F–Ab–C — so every chord's figure stays inside one window and shares tones with its neighbours instead of leaping with the root |
| `horns` | 262–622 Hz | the original harmony line: sustained brass, two notes a bar |
| `padHi` | 311–523 Hz | whole-bar third or fifth, slow-attack triangle, whichever is nearer the previous bar's note |
| `counter` | 156–587 Hz | pulse-wave counter-riff on the off-beat sixteenths, panned right |
| `harm` | 294–784 Hz | the lead's harmony voice, panned left |
| `lead` | 392–988 Hz | unchanged melody, re-voiced from saw to a seven-voice supersaw |
| `bell` | 622–1175 Hz | plucked triangle accents on beat 4 of the bar |

`harm` is picked per note as the **highest chord tone at least three semitones below the
lead**, with sevenths admitted where a bare triad cannot shadow the line — D5 over Cm takes
Bb4 (Cm7), G5 over Fm takes Eb5 (Fm7), F5 over G takes D5 (G7). Every interval in the
finished line is a third, fourth, fifth or sixth below.

**Groove** — the original had no kick: the rudimental march snare carried everything, eleven
hits a bar accenting beats 1 and 3, with a rising sixteenth roll closing every section. That
line and the eighth-note hats are kept **verbatim**, and the kit is built to sit with them:
four-on-the-floor `kick` with beats 1 and 3 loudest, so the original accent hierarchy becomes
the sidechain pump; `clap` on 2 and 4 from `marchA2` on — a retrowave backbeat crossing the
march's strong-beat accents; `openhat` on the "and" of 4, `shaker` sixteenths panned left,
`crash` on each section downbeat, and `tomHi` / `tomLo` panned hard opposite for a fill across
the **last two beats of bar 7**, clear of the bar-8 snare rolls.

`ride` runs **quarters**, not the eighths used elsewhere in the library. The original hat line
is already eighths and is kept verbatim, so an eighth-note ride would merely double it; on
quarters it reinforces the march pulse instead.

**Arrangement arc** — the original sequence repeated `marchA`, `marchC` and `marchB`
verbatim. As in Raptor March those repeats became `marchA2` / `marchC2` / `marchB2`: identical
melody, harmony and drum groove, plus the counter-riff, the full bell line, the shaker, the
clap and a tom fill, so each strain's repeat answers its first statement. The sequence is
`intro · marchA · marchA2 · marchB · climax · marchC · marchC2 · marchB2 · finale` and the
total length is unchanged at 272 beats.

| Section | harm | counter | arp | bell | horns | pad | sub | clap | shaker | ride | toms | crash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `intro` | pickup | — | ✓ | pickup | pickup | ✓ | bar 3 | — | — | — | — | ✓ |
| `marchA` | ✓ | — | ✓ | sparse | ✓ | ✓ | ✓ | — | — | — | — | ✓ |
| `marchA2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `marchB` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `climax` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `marchC` | ✓ | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ |
| `marchC2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `marchB2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `finale` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |

**Headroom** — every per-track volume is copied from Raptor March **except the snare**, and
the master is **0.40**. Both numbers were measured over the baked loop rather than assumed:

| Track | master | RMS | peak | > 4 kHz |
| --- | --- | --- | --- | --- |
| Raptor March | 0.42 | −15.4 dBFS | 0.784 | −31.9 dB |
| Apex Colossus | 0.35 | −15.4 dBFS | 0.876 | −31.2 dB |
| Black Tide | 0.42 | −15.9 dBFS | 0.878 | −29.4 dB |
| Brass Battalion | 0.44 | −15.9 dBFS | 0.861 | −32.4 dB |
| Dread Legion | 0.40 | −15.4 dBFS | 0.811 | −31.7 dB |

At Raptor March's master of 0.42 this track landed at −15.0 dBFS, hotter than anything else in
the library — twenty-one lines against Raptor March's fourteen. Trimming the master to 0.40
rather than the voices is the move Raptor March itself made when it gained voices, and it puts
the loudness exactly on Raptor March's.

The snare is the other half of the story, and it is the Brass Battalion problem again, worse:
this line plays **eleven hits a bar** where Raptor March's plays two, so the shared palette's
`gatedReverb: 0.7` smears into continuous hiss. Applying the Brass Battalion fix verbatim
(`volume` 0.52, `noiseMix` 0.45, `gatedReverb` 0.4) over-corrected — the track came out at
−32.9 dB above 4 kHz, *darker* than Brass Battalion despite having more snare hits. Settling
at `volume: 0.58` / `noiseMix: 0.52` / `gatedReverb: 0.4` lands on −31.7 dB, next to Raptor
March, which is right: it should read as a snare march without the wash.

Time-based settings barely move, because 112 BPM is within 4% of Raptor March's 108: sidechain
`release` 0.22 → 0.21 s, delay `feedback` 0.32 → 0.31 and `mix` 0.45 → 0.44, reverb `size`
0.84 → 0.83 and `mix` 0.28 → 0.27. The synth envelopes are copied unchanged — at this tempo
difference retuning them would be noise.

**Bake cost** — the loop is 256 beats ≈ 137.1 s, comparable to Apex Colossus and just under
Brass Battalion. Measured at ≈ 4.5 s on one worker thread. Asynchronous and cached for the
lifetime of the app, but do not assign this track to a scene that loads quickly and expects
music immediately.

## The Flak Parade arrangement

`flak-parade.json` is the same twenty-voice scoring applied to the fast military parade. The
`lead`, `harmony`, `sax`, `bass`, `snare` **and `hats`** note data is **unchanged** from the
chiptune original — tempo (164), key (G minor with the F# of the harmonic-minor V, the Eb of
the flat submediant and the Bb/F major mixture of `mainC`), chord progression, melody and
total length (8-beat intro + 288-beat loop) are all untouched. Every one of the original's six
lines survives verbatim; what changed is the instrumentation, plus six new synth voices and a
ten-piece kit written around the existing material.

Two structural moves, both of which preserve the original harmony exactly:

* The old **`harmony` line became `horns`**, but voiced as a *stab* rather than the slow swell
  used elsewhere (`gate: 0.9`, 20 ms attack). It has to be: in `mainA` / `mainC` the line is
  quarter notes, and at 164 BPM a quarter note is 366 ms, so the 0.45 s attack that works for
  Raptor March's brass would never speak. The new `pad` / `padHi` pair holds the whole-bar
  chords underneath it instead.
* The old **`sax` line became `arp`**. It was already an eighth-note broken-chord ostinato —
  exactly the `arp` role in the other arrangements — so it transfers verbatim and no new
  arpeggio had to be written.

**Register allocation** — the same band-per-voice discipline, transposed to G minor:

| Voice | Band | Role |
| --- | --- | --- |
| `sub` | 49–87 Hz | sine sub-bass on the chord root, one note per chord, `glide` between them. Folded into the G1–F#2 octave so the tonic anchors the floor and it never collides with the bass |
| `bass` | 73–262 Hz | unchanged root/fifth eighth-note pump, re-voiced from triangle to saw |
| `counter` | 147–587 Hz | pulse-wave counter-riff on the off-beat sixteenths, panned right |
| `pad` | 196–349 Hz | whole-bar chord root, slow-attack saw |
| `padHi` | 349–466 Hz | whole-bar third or fifth, slow-attack triangle, whichever is nearer the previous bar's note |
| `horns` | 196–784 Hz | the original harmony line, re-voiced as brass stabs |
| `arp` | 131–880 Hz | the original sax ostinato |
| `harm` | 294–784 Hz | the lead's harmony voice, panned left |
| `lead` | 392–932 Hz | unchanged melody, re-voiced from square to a seven-voice supersaw |
| `bell` | 698–1175 Hz | plucked triangle accents on beat 4 of the bar |

`harm` is picked per note as the **highest seventh-chord tone at least three semitones below
the lead** — every interval in the finished line is a third, fourth, fifth or sixth below. It
is **omitted from `intro`, `bridge`, `climax`, `bridge2` and `climax2`**: in those five
sections the original `harmony` line already shadows the lead note-for-note, so a derived
`harm` would land on top of it and merely double the level. It appears only where `harmony`
is a chordal quarter- or half-note line (`mainA`, `mainB`, `mainC` and their repeats).

**Groove** — the original had no kick: the rudimental parade snare carried everything, ten
hits a bar accenting beats 1 and 3, with a rising sixteenth roll closing every section, over
an eighth-note hat line. Both are kept **verbatim**, and the kit is built to sit with them:
four-on-the-floor `kick` with beats 1 and 3 loudest, so the original accent hierarchy becomes
the sidechain pump; `clap` on 2 and 4 from `mainA2` on — a retrowave backbeat crossing the
parade's strong-beat accents; `openhat` on the "and" of 4, `shaker` sixteenths panned left,
`crash` on each section downbeat, and `tomHi` / `tomLo` panned hard opposite for a fill across
the **last two beats of bar 7**, clear of the bar-8 snare rolls. Both bridges drop to a
half-time kick (beat 1 and the "and" of 3).

`ride` runs **quarters**, not eighths — the same call as Dread Legion. The original hat line
is already eighths and is kept verbatim, so an eighth-note ride would merely double it; on
quarters it reinforces the parade pulse instead.

**Arrangement arc** — the original sequence repeated `mainA`, `mainC` and `mainB` verbatim. As
in Raptor March those repeats became `mainA2` / `mainC2` / `mainB2`: identical melody, harmony
and drum groove, plus the counter-riff, the full bell line, the shaker, the clap and a tom
fill, so each strain's repeat answers its first statement. The sequence is
`intro · mainA · mainA2 · mainB · bridge · climax · mainC · mainC2 · bridge2 · mainB2 ·
climax2` and the total length is unchanged at 288 beats.

| Section | harm | counter | bell | pad | sub | clap | openhat | shaker | ride | toms | crash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `intro` | horns | — | pickup | ✓ | bar 2 | — | — | — | — | — | ✓ |
| `mainA` | ✓ | — | sparse | ✓ | ✓ | — | ✓ | — | — | — | ✓ |
| `mainA2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `mainB` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | fill | ✓ |
| `bridge` | horns | melodic | sparse | ✓ | ✓ | — | — | — | ✓ | — | ✓ |
| `climax` | horns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `mainC` | ✓ | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ |
| `mainC2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `bridge2` | horns | melodic | sparse | ✓ | ✓ | — | — | — | ✓ | — | ✓ |
| `mainB2` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |
| `climax2` | horns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | fill | ✓ |

Both bridges take the melodic counter-riff rather than the sixteenth figure, and its root is
**voice-led** (nearest octave to the previous bar) rather than folded into a fixed window —
folding sent Gm down an octave against its Eb/F neighbours and broke the line.

**Headroom** — every per-track volume is copied from Raptor March **except the snare**, and
the master is Raptor March's **0.42**. Both numbers were measured over the baked loop rather
than assumed:

| Track | master | RMS | peak | > 4 kHz |
| --- | --- | --- | --- | --- |
| Raptor March | 0.42 | −15.4 dBFS | 0.79 | −31.9 dB |
| Apex Colossus | 0.35 | −15.4 dBFS | 0.87 | −31.3 dB |
| Black Tide | 0.42 | −15.9 dBFS | 0.82 | −29.5 dB |
| Brass Battalion | 0.44 | −15.9 dBFS | 0.85 | −32.4 dB |
| Dread Legion | 0.40 | −15.4 dBFS | 0.80 | −31.7 dB |
| Flak Parade | 0.42 | −15.3 dBFS | 0.76 | −31.6 dB |

Unlike Apex Colossus and Dread Legion, this track did **not** need the master trimmed. It
carries twenty lines rather than twenty-one, and — this is the part that is easy to get
backwards — the fast tempo does not by itself make the mix hotter: four-on-the-floor at
164 BPM ducks the whole bus 2.7 times a second, and the sidechain
pump is what buys back the headroom the extra onsets spend. Measured at 0.42 the peak is
**0.76, the lowest of the six**, so trimming the master would only have pushed the track
quieter than the rest of the library. 0.38 was tried first and landed at −16.2 dBFS, audibly
below the family.

The snare is the whole story instead, and it is the Brass Battalion / Dread Legion problem in
its sharpest form: **ten hits a bar at 164 BPM** is the densest snare in the library by hits
per second, so the shared palette's `gatedReverb: 0.7` smears into continuous hiss. At the
unmodified Raptor March settings the track carried −27.6 dB above 4 kHz — 4.3 dB brighter than
Raptor March and 1.9 dB brighter than Black Tide, which is the brightest track in the library.
Applying the Brass Battalion fix verbatim (`volume` 0.52, `noiseMix` 0.45) over-corrected to
−32.6 dB. Settling at `volume: 0.55` / `noiseMix: 0.5` / `gatedReverb: 0.4` lands on −31.6 dB,
between Raptor March and Dread Legion, which is right: it should read as a parade snare
without the wash.

Time-based settings scale with the tempo, at 108⁄164 ≈ 0.66 of the Raptor March values:
sidechain `release` 0.22 → 0.13 s, delay `feedback` 0.32 → 0.26 and `mix` 0.45 → 0.36, reverb
`size` 0.84 → 0.70 and `mix` 0.28 → 0.21, and every synth envelope shortened to match — at
164 BPM a sixteenth is 91 ms, so the Raptor March decay times would smear. The `pad` attack
in particular had to come down 0.85 → 0.5 s, because the chord changes on the half-bar in
`mainA` / `mainC` are only 0.73 s long.

**Bake cost** — the loop is 288 beats ≈ 105.4 s, second-shortest of the six retrowave tracks
behind Raptor March's 71.1 s. Rendered offline it costs 1.7× Raptor March and 0.72× Dread
Legion, which puts the Unity bake at roughly 3.5 s on one worker thread — mid-pack, and well
inside the asynchronous window. Cached for the lifetime of the app.
