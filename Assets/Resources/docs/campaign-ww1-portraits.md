# WW1 character portraits — image generation prompts

Avatar prompts for every named speaker in the WW1 campaign (docs/campaign-ww1-scenario.md).
Written for natural-language image generators — Nano Banana / Gemini, ChatGPT image, or any model
that takes instructions rather than tag soup. Each prompt below is self-contained: copy one block,
paste it, done.

## What these are for

A square bust portrait shown beside the speaker's name in the dialogue bar (docs/campaign-scripts.md
— the bottom film bar is 150 px tall at the 1920×1080 reference, so the portrait sits at roughly
96–114 px on screen). Everything in the shared style block exists to keep the face readable at that
size.

## Fixed rules across the whole set

| Rule | Value |
| --- | --- |
| Style | Detailed pixel art, **128×128 logical pixel grid**, nearest-neighbour upscale, no anti-aliasing |
| Crop | Square 1:1 bust — head and upper chest, cut just below the shoulders |
| Pose | Three-quarter view, head turned ~30° to the **viewer's left**, both eyes visible, eyes on the viewer |
| Lighting | Flat and even. **No cast shadow, no drop shadow, no chin shadow, no rim light, no glow, no vignette** |
| Background | One flat solid colour `#1B1F24`, completely empty |
| Outline | 1 px dark outline `#14151A` around the whole silhouette (not pure black) |
| Palette | ~28 muted period colours (below) |
| Headgear | Pilots: brown leather flight helmet, goggles **pushed up on the forehead**. Ground officers: kepi |

Same angle for everyone, including the German — the set has to look like one set when the portraits
appear one after another in the same bar.

## Palette

| Group | Colours |
| --- | --- |
| Skin | `#F2D0B4` `#D9A783` `#B87F5C` `#8A5740` `#5E3A2C` |
| Hair | `#241C18` `#4A3626` `#B8975E` `#9A968E` |
| Leather | `#6B4A31` `#4E3524` `#35241A` `#2A1B13` |
| Sheepskin / fur | `#D8C4A0` `#B39F7C` `#8A7757` |
| Horizon blue (French uniform) | `#8FA3B8` `#6C819A` `#4C6076` |
| Khaki | `#7C7A55` `#5A5940` |
| Black leather (German) | `#2B2B2E` `#1A1A1D` `#0F0F11` |
| Accent red (Marchand's scarf) | `#A33B32` `#7A2A24` |
| Enamel + metal | `#3E6FA8` `#26497A` `#C9CDD2` `#8E959C` |
| Neutrals | `#E6E4DC` `#B9B6AC` `#7E7B73` |
| Outline / background | `#14151A` `#1B1F24` |

---

## Émile Vasseur — speaker `you`, label `VASSEUR`

**Reads at 96 px as**: the youngest face, a too-big helmet, a black grease smear on the cheek.

- 19 years old, 1916. Thin, unfinished face — sharp chin, hollow cheeks, no facial hair at all.
- The grease mark is his mechanic's past, and it is his identifying feature. Keep it on every version.
- His helmet is a size too large and the chin strap hangs unbuckled — he is wearing someone else's war.

```
A detailed pixel-art character portrait of a 19-year-old French fighter pilot of 1916.

Style: hand-made pixel art drawn on a 128x128 logical pixel grid, then scaled up with
nearest-neighbour so every pixel is a crisp hard-edged square. No anti-aliasing, no blur, no soft
gradients — shade with flat colour bands and sparse ordered dithering only. Limited muted palette of
about 28 colours: leather browns, sheepskin cream, horizon blue, khaki, and pale weathered skin
tones. A 1-pixel dark outline in #14151A, not pure black, around the whole silhouette.

Framing: square 1:1 video-game avatar bust. Head and upper chest only, cropped just below the
shoulders. The head is turned about 30 degrees to the viewer's left in three-quarter view, both eyes
visible, eyes looking straight at the viewer. The head fills the upper two thirds of the frame,
centred, with a little headroom.

Lighting: completely flat and even, front-on. No cast shadow, no drop shadow, no shadow under the
chin, no shadow behind the figure, no rim light, no glow, no bloom, no vignette, no depth of field.

Background: one flat solid colour #1B1F24, completely empty. No scenery, no sky, no clouds, no
aircraft, no texture, no gradient, no border, no frame, no text, no signature, no watermark.

Subject: a very young man, nineteen, thin and unfinished — sharp chin, hollow cheeks, no facial hair
at all. Dark brown hair, damp and flattened, a few strands escaping at the temple. Large pale
grey-blue eyes with dark lashes and faint sleepless shadows beneath. A smear of black engine grease
along his left cheekbone. A brown leather flight helmet one size too large, its chin strap hanging
unbuckled, with round brass-rimmed goggles pushed up onto his forehead. A cream sheepskin collar
swallowing his neck, over the collar of a horizon-blue tunic.

Expression: quiet and alert, faintly startled to be here. Mouth closed, jaw tight.

Do not render this as a 3D render, oil painting, anime cel or smooth digital illustration. It must
read as deliberately authored pixel art.
```

---

## Auguste Roussel — speaker `roussel`, label `ROUSSEL`

**Reads at 96 px as**: the grey cavalry moustache and the only tightly buckled helmet in the set.

- 38, ex-cavalry, capitaine. The oldest flying face and the most kept-together.
- Everything about his kit is correct and maintained — the visual opposite of Crane.
- Use him as the **style anchor**: generate this one first and feed it back as a reference image for the rest.

```
A detailed pixel-art character portrait of a 38-year-old French capitaine and fighter pilot of 1917.

Style: hand-made pixel art drawn on a 128x128 logical pixel grid, then scaled up with
nearest-neighbour so every pixel is a crisp hard-edged square. No anti-aliasing, no blur, no soft
gradients — shade with flat colour bands and sparse ordered dithering only. Limited muted palette of
about 28 colours: leather browns, sheepskin cream, horizon blue, khaki, and pale weathered skin
tones. A 1-pixel dark outline in #14151A, not pure black, around the whole silhouette.

Framing: square 1:1 video-game avatar bust. Head and upper chest only, cropped just below the
shoulders. The head is turned about 30 degrees to the viewer's left in three-quarter view, both eyes
visible, eyes looking straight at the viewer. The head fills the upper two thirds of the frame,
centred, with a little headroom.

Lighting: completely flat and even, front-on. No cast shadow, no drop shadow, no shadow under the
chin, no shadow behind the figure, no rim light, no glow, no bloom, no vignette, no depth of field.

Background: one flat solid colour #1B1F24, completely empty. No scenery, no sky, no clouds, no
aircraft, no texture, no gradient, no border, no frame, no text, no signature, no watermark.

Subject: a weathered man of thirty-eight with a square, hard-used face — deep-set dark eyes under
heavy brows, high cheekbones, deep lines running from nose to mouth. A full dark moustache going
grey at the edges, trimmed in the old cavalry manner. Black hair cropped close and greying at the
temples. A thin white scar through the outer end of his right eyebrow. A well-kept dark brown
leather flight helmet with the chin strap buckled tight, goggles pushed up onto the forehead. A worn
sheepskin collar over a horizon-blue tunic with faded gold rank braid at the collar.

Expression: flat and unimpressed, a level stare. Mouth a hard straight line.

Do not render this as a 3D render, oil painting, anime cel or smooth digital illustration. It must
read as deliberately authored pixel art.
```

---

## Théo Marchand — speaker `marchand`, label `MARCHAND`

**Reads at 96 px as**: the dull red scarf — the only saturated warm colour on any French pilot.

- 24, from Lyon, the warm one. The only portrait in the set that is smiling.
- The scarf is hand-knitted and slightly wrong, and it is the thing the player will remember after level 6.
- Give him the softest silhouette: curls out from under the helmet on both sides.

```
A detailed pixel-art character portrait of a 24-year-old French fighter pilot of 1917.

Style: hand-made pixel art drawn on a 128x128 logical pixel grid, then scaled up with
nearest-neighbour so every pixel is a crisp hard-edged square. No anti-aliasing, no blur, no soft
gradients — shade with flat colour bands and sparse ordered dithering only. Limited muted palette of
about 28 colours: leather browns, sheepskin cream, horizon blue, khaki, a dull brick red, and pale
weathered skin tones. A 1-pixel dark outline in #14151A, not pure black, around the whole silhouette.

Framing: square 1:1 video-game avatar bust. Head and upper chest only, cropped just below the
shoulders. The head is turned about 30 degrees to the viewer's left in three-quarter view, both eyes
visible, eyes looking straight at the viewer. The head fills the upper two thirds of the frame,
centred, with a little headroom.

Lighting: completely flat and even, front-on. No cast shadow, no drop shadow, no shadow under the
chin, no shadow behind the figure, no rim light, no glow, no bloom, no vignette, no depth of field.

Background: one flat solid colour #1B1F24, completely empty. No scenery, no sky, no clouds, no
aircraft, no texture, no gradient, no border, no frame, no text, no signature, no watermark.

Subject: a round-faced open-looking young man of twenty-four, full cheeks, warm brown eyes with
laugh lines at the corners. Thick dark brown curls escaping from under his helmet at the forehead
and over both ears. Freckles scattered across the nose and cheekbones, and short dark stubble. A
brown leather flight helmet pushed back on his head with the goggles up on the forehead. A
hand-knitted dull brick-red woollen scarf, uneven and obviously homemade, wound twice around his
neck over a sheepskin collar.

Expression: the beginning of a smile, easy and unforced — the only warm face in the squadron.

Do not render this as a 3D render, oil painting, anime cel or smooth digital illustration. It must
read as deliberately authored pixel art.
```

---

## Hollis Crane — speaker `crane`, label `CRANE`

**Reads at 96 px as**: the loose dangling chin strap and the matchstick in the corner of the mouth.

- 27, American volunteer. Tanned where the French pilots are pale, and out of uniform where he can get away with it.
- Nothing he wears is fastened correctly. That is the whole character in one silhouette.

```
A detailed pixel-art character portrait of a 27-year-old American volunteer pilot flying for France
in 1917.

Style: hand-made pixel art drawn on a 128x128 logical pixel grid, then scaled up with
nearest-neighbour so every pixel is a crisp hard-edged square. No anti-aliasing, no blur, no soft
gradients — shade with flat colour bands and sparse ordered dithering only. Limited muted palette of
about 28 colours: leather browns, sheepskin cream, horizon blue, khaki, and pale weathered skin
tones. A 1-pixel dark outline in #14151A, not pure black, around the whole silhouette.

Framing: square 1:1 video-game avatar bust. Head and upper chest only, cropped just below the
shoulders. The head is turned about 30 degrees to the viewer's left in three-quarter view, both eyes
visible, eyes looking straight at the viewer. The head fills the upper two thirds of the frame,
centred, with a little headroom.

Lighting: completely flat and even, front-on. No cast shadow, no drop shadow, no shadow under the
chin, no shadow behind the figure, no rim light, no glow, no bloom, no vignette, no depth of field.

Background: one flat solid colour #1B1F24, completely empty. No scenery, no sky, no clouds, no
aircraft, no texture, no gradient, no border, no frame, no text, no signature, no watermark.

Subject: a lean, angular man of twenty-seven with a heavy jaw and a nose that was broken once and
set badly. Three days of sandy stubble. Sun-bleached dirty-blond hair cropped short. Narrow
grey-green eyes with pale squint lines fanning from the corners across sunburnt skin. A brown
leather flight helmet shoved back off his forehead with the strap hanging loose and swinging,
goggles up. The collar of a dirty knitted civilian sweater showing under his tunic instead of
regulation dress. A wooden matchstick held in the corner of his mouth.

Expression: amused and faintly insubordinate, one eyebrow slightly raised.

Do not render this as a 3D render, oil painting, anime cel or smooth digital illustration. It must
read as deliberately authored pixel art.
```

---

## Henri Lasalle — speaker `lasalle`, label `LASALLE`

**Reads at 96 px as**: the kepi and the round spectacles — the only portrait without flight gear.

- 50, Commandant, operations officer. Ground staff, so he breaks the helmet rule deliberately: when
  his portrait appears the player should know instantly that this voice is not in the air.

```
A detailed pixel-art character portrait of a 50-year-old French army Commandant, an operations
officer of 1917.

Style: hand-made pixel art drawn on a 128x128 logical pixel grid, then scaled up with
nearest-neighbour so every pixel is a crisp hard-edged square. No anti-aliasing, no blur, no soft
gradients — shade with flat colour bands and sparse ordered dithering only. Limited muted palette of
about 28 colours: horizon blue, dark navy, gold braid, khaki, and pale tired skin tones. A 1-pixel
dark outline in #14151A, not pure black, around the whole silhouette.

Framing: square 1:1 video-game avatar bust. Head and upper chest only, cropped just below the
shoulders. The head is turned about 30 degrees to the viewer's left in three-quarter view, both eyes
visible, eyes looking straight at the viewer. The head fills the upper two thirds of the frame,
centred, with a little headroom.

Lighting: completely flat and even, front-on. No cast shadow, no drop shadow, no shadow under the
chin, no shadow behind the figure, no rim light, no glow, no bloom, no vignette, no depth of field.

Background: one flat solid colour #1B1F24, completely empty. No scenery, no sky, no clouds, no
aircraft, no texture, no gradient, no border, no frame, no text, no signature, no watermark.

Subject: a tired man of fifty with a long narrow face, thinning grey hair and a close-trimmed grey
moustache. Round wire-rimmed spectacles. Heavy pouches under grey eyes. He wears no flight gear at
all: a dark blue French officer's kepi with gold rank braid, worn straight, and a buttoned
horizon-blue tunic with a high collar, gold collar tabs and a small Croix de Guerre ribbon on the
chest.

Expression: exhausted and patient, faintly ashamed of something he has already signed.

Do not render this as a 3D render, oil painting, anime cel or smooth digital illustration. It must
read as deliberately authored pixel art.
```

---

## Capitaine Bourdin — speaker `bourdin`, label `BOURDIN`

**Reads at 96 px as**: a much wider, heavier head than any scout pilot, in a black coat.

- 41, bomber flight leader, one appearance (level 6). Built like a different job to the others:
  broader, bulkier kit, nothing expressive.

```
A detailed pixel-art character portrait of a 41-year-old French bomber pilot, a capitaine, in 1917.

Style: hand-made pixel art drawn on a 128x128 logical pixel grid, then scaled up with
nearest-neighbour so every pixel is a crisp hard-edged square. No anti-aliasing, no blur, no soft
gradients — shade with flat colour bands and sparse ordered dithering only. Limited muted palette of
about 28 colours: leather browns, black leather, sheepskin cream, horizon blue, khaki, and pale
weathered skin tones. A 1-pixel dark outline in #14151A, not pure black, around the whole silhouette.

Framing: square 1:1 video-game avatar bust. Head and upper chest only, cropped just below the
shoulders. The head is turned about 30 degrees to the viewer's left in three-quarter view, both eyes
visible, eyes looking straight at the viewer. The head fills the upper two thirds of the frame,
centred, with a little headroom.

Lighting: completely flat and even, front-on. No cast shadow, no drop shadow, no shadow under the
chin, no shadow behind the figure, no rim light, no glow, no bloom, no vignette, no depth of field.

Background: one flat solid colour #1B1F24, completely empty. No scenery, no sky, no clouds, no
aircraft, no texture, no gradient, no border, no frame, no text, no signature, no watermark.

Subject: a broad, heavy man of forty-one with a wide jaw, a thick neck and a boxer's flattened
features. Short black hair and a clipped black moustache. Sweat on the forehead. Small dark eyes set
deep, giving nothing away. A heavy black leather flying coat with a wide collar turned up, visibly
bulkier kit than a scout pilot's, and a brown leather helmet with the strap buckled and the goggles
pushed up.

Expression: closed and professional, deliberately blank.

Do not render this as a 3D render, oil painting, anime cel or smooth digital illustration. It must
read as deliberately authored pixel art.
```

---

## Otto von Ravensberg — speaker `ravensberg`, label `RAVENSBERG`

**Reads at 96 px as**: an almost black bust with one bright blue enamel cross at the throat.

- 33, German ace, *Jasta 41*. He is the darkest and palest portrait in the set — near-black kit,
  bloodless face. The Pour le Mérite is the only saturated colour anywhere on him.
- Keep him polite, not monstrous. He spares Vasseur twice and gives the squadron its name.

```
A detailed pixel-art character portrait of a 33-year-old German fighter ace, an aristocrat, in 1917.

Style: hand-made pixel art drawn on a 128x128 logical pixel grid, then scaled up with
nearest-neighbour so every pixel is a crisp hard-edged square. No anti-aliasing, no blur, no soft
gradients — shade with flat colour bands and sparse ordered dithering only. Limited muted palette of
about 28 colours: gloss black leather, cold greys, bone white, one blue-and-gold enamel accent, and
very pale skin tones. A 1-pixel dark outline in #14151A, not pure black, around the whole silhouette.

Framing: square 1:1 video-game avatar bust. Head and upper chest only, cropped just below the
shoulders. The head is turned about 30 degrees to the viewer's left in three-quarter view, both eyes
visible, eyes looking straight at the viewer. The head fills the upper two thirds of the frame,
centred, with a little headroom.

Lighting: completely flat and even, front-on. No cast shadow, no drop shadow, no shadow under the
chin, no shadow behind the figure, no rim light, no glow, no bloom, no vignette, no depth of field.

Background: one flat solid colour #1B1F24, completely empty. No scenery, no sky, no clouds, no
aircraft, no texture, no gradient, no border, no frame, no text, no signature, no watermark.

Subject: a narrow aristocratic face, high forehead, long straight nose, thin colourless lips,
clean-shaven. Very pale ice-blue eyes. White-blond hair combed flat, visible only at the temple.
Pale, almost bloodless skin. Everything he wears is black: a gloss black leather flight helmet with
goggles pushed up on the forehead, and a black leather coat with a high stiff collar. At his throat,
a blue-and-gold enamel Pour le Merite cross on a black ribbon — the only saturated colour in the
picture. A small bone-white enamel raven pin on his collar.

Expression: composed, incurious, faintly amused. Polite rather than cruel.

Do not render this as a 3D render, oil painting, anime cel or smooth digital illustration. It must
read as deliberately authored pixel art.
```

---

## Keeping the set consistent

Generators drift between runs, and seven portraits that drifted apart look worse than seven that are
all slightly wrong in the same way.

1. Generate **Roussel** first and keep re-rolling until the style is right. He is the anchor.
2. For everyone else, attach the Roussel image as a reference and prepend: *"Match the style,
   palette, pixel size, crop, head angle and flat lighting of the attached portrait exactly. Only the
   person changes."*
3. Check the whole set side by side at 96 px before accepting any of them. If one face is unreadable
   at that size, the problem is almost always too many palette steps on the skin — ask for fewer.

## After generation

1. Downscale to **128×128 with nearest-neighbour** (Aseprite, or GIMP with interpolation set to
   None). Generators output soft 1024 px images that only *look* like pixel art; this step makes the
   grid real.
2. Quantize to the palette above, then hand-fix the outline where quantization broke it.
3. The bar behind the portrait is solid black, so key `#1B1F24` to alpha (or repaint it black) to
   stop the background reading as a lighter square.
4. Unity import settings: Texture Type Sprite, **Filter Mode Point (no filter)**, Compression None,
   Generate Mip Maps off. Anything else will blur the pixels back out.
