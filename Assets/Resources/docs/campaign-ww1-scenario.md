# Campaign scenario — World War 1

The founding era of the Metal Raptors. Eight levels, one protagonist, four pilots, 1916–1918.
This file is the **story source**: cast, plot, loading-screen text and every radio line. It is not
wired to anything yet — the levels, the loading screen UI and the script files come later
(docs/campaign.md, docs/campaign-scripts.md).

## Premise

There is no elite group yet. There is a tired French escadrille, a boy who was refused twice by
the recruiting board, and a war that lasts long enough to turn one into the other.

`Escadrille SOP.118`, aerodrome at **Vaux-le-Bois**, Verdun sector. Four pilots. Over two years
they lose one, bury the name of the unit under a nickname the enemy gave them, and end the war as
the thing every later era inherits: the Metal Raptors.

The name is not chosen by them. It is an insult — *eiserne Raubvögel*, iron birds of prey — thrown
at them by the German ace who spent two years failing to kill them. The American in the squadron
translates it into English, and the English version is what sticks. That is why a French unit's
name is English in every era that follows.

**Tone**: grounded and grim, sparse. Radio traffic is clipped and procedural. The pilots do not
make speeches. Level 6 lands hard because everything around it is restrained.

## Cast

### The squadron

| Radio label | Full name | Who they are | Fate |
| --- | --- | --- | --- |
| `VASSEUR` (player) | Émile Vasseur | 19 in 1916. Amiens. Refused twice by the board, worked eleven months as a mechanic on the field he now flies from. Quiet, obsessive, a better shot than anybody expected. | Survives. Leads the squadron from May 1918. |
| `ROUSSEL` | Capitaine Auguste Roussel | 38. Ex-cavalry, transferred in 1915. Flat, unsentimental, the only reason any of them live past the first month. He is the one who signed Vasseur's form. | Killed in level 8. |
| `MARCHAND` | Théo Marchand | 24. Lyon. The warm one — feeds the field's dogs, writes other men's letters home. Shot down and walks back in level 4. | Killed in level 6. |
| `CRANE` | Hollis Crane | 27. American volunteer, joins in level 2. Talks too much, argues with rank, does not leave a fight when told to. | Survives. Co-founder; he is the one who says "metal raptors". |

### Ground

| Radio label | Full name | Who they are |
| --- | --- | --- |
| `LASALLE` | Commandant Henri Lasalle | Operations officer, Vaux-le-Bois and afterwards wherever the squadron is sent. Signs orders he does not agree with and does not pretend otherwise. |
| `BOURDIN` | Capitaine Bourdin | Leads the Breguet flight that comes in behind them at Hohrupt in level 6. Voice only, never a gameplay object. Appears once. |

### The enemy

| Radio label | Full name | Who they are |
| --- | --- | --- |
| `RAVENSBERG` | Freiherr Otto von Ravensberg, *Jasta 41* | The Red Baron analogue. Not red — a gloss-black Fokker Dr.I with a bone-white raven on the fuselage; the French call it *le Corbeau*. Hunts alone, takes the last man in a formation, has never been seen to miss twice. Forced down alive in level 4, returns in level 8, breaks off alive at the end of it. He names them. |

Other enemies are unnamed by design: *Jasta 41*'s scouts, two-seater observers, the flak and gun
positions of levels 3 and 5, and in level 8 an unnamed Gotha G.IV of the *Englandgeschwader*.

### On radio

Single-seat scouts of this war had no radio-telephone. In-fiction, SOP.118 is a trial unit for
wireless telephony sets — that is the only liberty the story takes, and it buys the entire
dialogue system. Ravensberg cutting into their frequency in levels 4 and 8 is deliberate and is
treated in-fiction as unnerving, not routine.

## The arc

| # | Title | Date | Terrain | Time | Mode | Story beat |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | First Light | 14 Apr 1916 | Verdun | Morning | Scroller, tutorial, few enemies | Vasseur's first patrol and first kill. |
| 2 | The Numbers | 22 Jun 1916 | Verdun | Midday | Scroller, more and mixed enemies | Crane arrives; the squadron is four. |
| 3 | Fixed Ground | 12 Feb 1917 | Verdun | Evening | Fixed width, defend the aerodrome | The war reaches their own aerodrome. |
| 4 | The Raven | 6 Apr 1917 | Flanders | Morning | **Boss** — Ravensberg, 1v1 | Marchand is shot down and walks back. Vasseur forces the ace down. |
| 5 | Nothing Burns at Night | 19 Sep 1917 | Flanders coast | Night | Scroller, stealth, locate + destroy ground targets | They burn a dump and see white fire for the first time. One train escapes. |
| 6 | Hohrupt | 3 Oct 1917 | Mountain | Morning | Scroller, strike on a mountain village | The phosphorus strike on an occupied village. Marchand will not leave and is killed. |
| 7 | Two Fires | 24 Mar 1918 | Mountain | Midday | Fixed width, time attack, defend friends on the ground | Roussel and Crane are down in the valley. Vasseur holds it alone until the column comes. |
| 8 | Iron Birds of Prey | 15 May 1918 | Mountain | Evening | **Boss** — Gotha G.IV | Roussel dies. Vasseur kills the bomber. Ravensberg gives them their name. |

Levels 1–4 are the making of a pilot. Levels 5–6 are the cost. Levels 7–8 are the making of the
squadron.

### Where they fly

Three sectors, two transfers, and the transfers are the calendar of the war rather than anything the
squadron chooses.

- **Verdun (1–3)** — home is the aerodrome at Vaux-le-Bois. Two years of it, and in level 3 the war
  arrives on the field itself.
- **Flanders (4–5)** — moved north in the spring of 1917 as a fighting reserve, the way French
  escadrilles were shunted to whichever sector was loud that month. Ravensberg's *Jasta 41* is there
  ahead of them. Level 5 is over the flooded polder country behind the German-held coast.
- **The Vosges and Alsace (6–8)** — moved south in the autumn of 1917 to a mountain front everybody
  above them calls quiet. It is not quiet, it is only small, and the two are not the same. The
  villages here are Alsatian: German-held since 1871, French-speaking, and waiting to be liberated
  by exactly the people who are ordered to burn them. That is level 6, and it is why levels 7 and 8
  are fought over ground the squadron already has a grievance with.

---

## Level 1 — First Light

**14 April 1916 · Verdun sector · dawn · `Verdun`, `Morning`**
Scroller. The introduction: teaches climb, dive, turn and guns, and keeps the count deliberately
small. One enemy, then two, and nothing else in the sky.

### Loading screen

> They turned me down twice. Flat feet the first time. The second time a doctor in Amiens listened
> to my chest for a long minute and wrote something down that I was not allowed to read.
>
> So I went to the aerodrome at Vaux-le-Bois as a mechanic, because a mechanic is allowed to stand
> next to aeroplanes. I stood next to them for eleven months. In March a capitaine named Roussel
> found me sitting in a cold Sopwith at two in the morning, working the controls against nothing.
> He did not report me. He signed a form instead.
>
> This morning I am the third machine in a flight of three.
> — É. Vasseur

**Objective**: follow Roussel along the line. Bring the aeroplane back.

### Radio

**S1 — climb-out** *(opening, before any wave)*
- ROUSSEL: Vasseur. Right wing, and don't touch anything I haven't told you to touch.
- VASSEUR: Understood, Capitaine.
- ROUSSEL: Nose down buys you speed. Nose up spends it. That is the whole of flying.
- LASALLE: SOP.118, the sector is quiet. Take him up the line and bring him back.
- ROUSSEL: We'll find out what quiet means.

**S2 — first contact** *(before the first single-enemy wave)*
- ROUSSEL: Two o'clock, high. One machine. Fokker.
- VASSEUR: I see him.
- ROUSSEL: He's yours. I'll watch. Get close — closer than feels sensible.
- VASSEUR: How close?
- ROUSSEL: Until you can see the stitching on his collar.

**S3 — after the first kill** *(after wave 1 clears)*
- VASSEUR: He's going down. He's going down.
- ROUSSEL: Breathe out. Level your wings.
- VASSEUR: I didn't think he would burn.
- ROUSSEL: They mostly do. Watch your tail, not the ground.

**S4 — the pair, and home** *(before the two-enemy wave; last line after `finish`)*
- LASALLE: SOP.118, two machines crossing low, west of you.
- ROUSSEL: On me. Same as before. Nothing clever.
- VASSEUR: On you.
- ROUSSEL: That's one for the squadron and one for you. Take us home; I'll follow you in.
- VASSEUR: Yes, Capitaine.

---

## Level 2 — The Numbers

**22 June 1916 · Verdun sector · high midday · `Verdun`, `Midday`**
Scroller again, same ground under a harder light. More enemies than level 1 and a second enemy
type: two-seat observers under escort, so the player has to pick targets instead of shooting the
nearest one.

### Loading screen

> An American arrived in June with a kitbag, a cavalry pistol nobody could explain, and forty
> hours in his logbook. Crane. He argued with the Capitaine within a minute of meeting him and
> was still flying with us a year later, which tells you what Roussel thought of the argument.
>
> That made us four. Roussel, Marchand, Crane, me. Four is not a squadron — a squadron is
> fourteen machines and a hangar full of spares — but four was what was ever in the air at once,
> and four was what came back, and after a while you stop counting the rest.
> — É. Vasseur

**Objective**: sweep the sector clear so the observation flights can work.

### Radio

**S1 — the new man** *(opening)*
- CRANE: Crane. Hollis Crane. They tell me I'm on your wing.
- MARCHAND: They told us an American. They didn't say a talkative one.
- ROUSSEL: Crane, you're four. Vasseur, three. Keep your pairs and I'll keep you alive.
- CRANE: Whatever you say, Captain.
- ROUSSEL: Capitaine. Learn it, or die ignorant.

**S2 — the two-seaters** *(before the first mixed wave)*
- MARCHAND: Under the cloud. Three two-seaters, working the line.
- ROUSSEL: Those are the ones that matter. The scouts are only there to keep us off them.
- VASSEUR: The gunner's facing us the whole way in.
- ROUSSEL: Then don't come from behind. Come up underneath his tail where he can't depress.

**S3 — more than expected** *(mid-level, before the largest wave)*
- CRANE: More of them. East. I count five.
- MARCHAND: Six.
- ROUSSEL: After four it stops mattering. Pairs. Stay in pairs.
- VASSEUR: Marchand, I've lost you.
- MARCHAND: I'm under you. I've been under you for a minute. Look down occasionally.

**S4 — out** *(after the last wave, before `finish`)*
- ROUSSEL: Fuel. Break off, all of you, and go west.
- CRANE: I can get one more.
- ROUSSEL: You can get buried. West.
- MARCHAND: Vasseur — your tailplane's in ribbons.
- VASSEUR: It's still flying.

---

## Level 3 — Fixed Ground

**12 February 1917 · Vaux-le-Bois · failing light · `Verdun`, `Evening`**
Fixed-width arena over their own aerodrome. Waves of ground targets and aircraft; hold the field
while it is evacuated. The light going is the timer — they have to last until dark, and the last
wave comes in against a sky they can barely read.

### Loading screen

> In February they came for the field itself. Somebody in a staff car had decided Vaux-le-Bois
> was worth an hour of their guns, and for an hour — the last hour of light there was — it was.
>
> There is a particular feeling in defending the ground you sleep on. You know where the fuel is
> stacked. You know the sergeant standing in the open by the water tower, and his name, and that
> his wife sends him almond biscuits. You cannot chase anything, because everything you would
> chase is a way of pulling you off the thing you are standing over. So you turn in small circles
> above your own beds and you wait to be useful.
> — É. Vasseur

**Objective**: hold the field until the sheds are clear and the light is gone. Do not leave the
arena.

### Radio

**S1 — scramble** *(opening)*
- LASALLE: They're coming for the field, not the line. Everything we have goes up.
- ROUSSEL: Nobody chases. Chase, and you leave a hole, and they come through it.
- VASSEUR: How long do we hold?
- LASALLE: Until the sheds are empty, or until the light goes. An hour, perhaps.
- MARCHAND: An hour. Fine. I had nothing planned.

**S2 — the guns** *(before the first ground wave)*
- CRANE: Guns in the treeline. That's what's chewing up the field.
- ROUSSEL: Vasseur, take the guns. We'll keep the air off you.
- VASSEUR: Low pass, east to west.
- MARCHAND: Don't run the same line twice. They learn faster than you'd like.

**S3 — the balloon** *(mid-level)*
- ROUSSEL: There's an observation balloon behind the ridge. He's the one aiming them.
- CRANE: I'll take the balloon.
- ROUSSEL: You'll take everything guarding it as well.
- CRANE: I'll take that too.

**S4 — the last minutes** *(before the final wave)*
- LASALLE: Last shed is clear. Four minutes, SOP.118.
- MARCHAND: I'm out of ammunition.
- ROUSSEL: Then fly at them and make them turn. That's all we need now.
- VASSEUR: Three more, from the north.
- ROUSSEL: Let them come. There's nothing left down there worth their trouble.

---

## Level 4 — The Raven (boss)

**6 April 1917 · Flanders · hard spring light · `Flanders`, `Morning`**
Boss level, and the squadron's first morning in a new sector. Escorts first, then a one-against-one
with Ravensberg. Marchand is shot down in the opening pass and survives on the ground.

### Loading screen

> April was the worst month of the war for us, and the reason had a name.
>
> A black Fokker with a white raven on the side. He flew alone, he came out of the sun, and he
> took the last machine in a formation — always the last, always from behind, always one pass. Six
> in eight days from our sector alone. The mess called him *le Corbeau* and stopped saying it out
> loud after the fourth.
>
> Roussel's answer was arithmetic: if he only takes the last man, then today there is no last man.
> Four abreast, all morning, until he tired of waiting and came down where we could see him.
> — É. Vasseur

**Objective**: no stragglers. Find the black machine and put it on the ground.

### Radio

**S1 — briefing in the air** *(opening)*
- LASALLE: The black machine again. Six of ours in eight days, all of them from behind.
- ROUSSEL: He hunts the last man in a formation. So today there is no last man.
- CRANE: Has he got a name?
- LASALLE: Ravensberg. A Freiherr. His family has been killing people for four hundred years.
- MARCHAND: Then he's overdue a bad morning.

**S2 — he takes Marchand** *(after the escort wave, as the ace spawns)*
- MARCHAND: Where did he —
- CRANE: Behind you. Marchand, behind you —
- MARCHAND: Hit. Engine's gone. I'm putting it down in our own wire.
- ROUSSEL: Get out of the seat before it stops. Vasseur — don't go looking for him.
- VASSEUR: I have him. The black one. He's mine.

**S3 — the duel** *(during the boss fight)*
- RAVENSBERG: You turn well, little one. Better than the others did.
- VASSEUR: He's on our frequency.
- ROUSSEL: He wants you talking instead of flying. Say nothing.
- RAVENSBERG: Your capitaine is right. He usually is. It has never once saved anybody.

**S4 — forced down** *(after the boss is defeated, before `finish`)*
- VASSEUR: His engine's dead. He's going down under control.
- ROUSSEL: Then let him land. Shooting a man on the ground makes you a different animal.
- RAVENSBERG: A name, then. I would like to know what came for me.
- VASSEUR: Vasseur.
- RAVENSBERG: *Flieg heim, kleiner Raubvogel.* Fly home, little bird of prey.

---

## Level 5 — Nothing Burns at Night

**19 September 1917 · Wulpendamme, behind the Flanders coast · night · `Flanders`, `Night`,
searchlights**
Night scroller with stealth. Locate hidden ground targets by finding what is defended, destroy
them, avoid the lights. One target escapes on rails.

### Loading screen

> Night flying was new and nobody was good at it. You navigated by the shape of water against the
> sky — and in Flanders everything is water — and by the guns, which you could see long before you
> heard.
>
> They were hiding something in the plantations behind the dunes at Wulpendamme — four kilometres
> of pine and drainage ditch, no map reference worth having, and a great deal of flak for an empty
> field. Roussel's rule for finding anything at night: look for the guns first. Nobody guards
> nothing.
>
> We found it at three in the morning, and we set it alight, and then we watched it burn on the
> surface of the water in a drainage ditch, and none of us said anything on the way home.
> — É. Vasseur

**Objective**: find the dumps. Burn them. Do not fly through the smoke.

### Radio

**S1 — into the dark** *(opening)*
- LASALLE: No flares, no lights. If you can see each other you're too close.
- ROUSSEL: Four kilometres of pine and ditch, and no map worth the paper. We find it by what's
  defending it.
- CRANE: Meaning?
- ROUSSEL: Look for the guns. Nobody guards an empty field.
- VASSEUR: And the searchlights?
- ROUSSEL: If one takes you, fly straight down at it. They can't follow you all the way in.

**S2 — the siding** *(on locating the first target)*
- VASSEUR: Rail siding under the trees. Covered wagons. No markings on anything.
- MARCHAND: Unloading at three in the morning. That tells you everything.
- ROUSSEL: One pass each. Vasseur leads.
- MARCHAND: The smoke's wrong. It's white, and it isn't stopping.

**S3 — what it is** *(after the first target is destroyed)*
- CRANE: That isn't ammunition. Look at it.
- MARCHAND: It's burning in the ditch water. It's burning *on water*.
- LASALLE: SOP.118, describe the smoke.
- VASSEUR: White. Very white. Still burning.
- LASALLE: Understood. Do not fly through it. Continue to the second target.

**S4 — the train** *(final beat, before `finish`)*
- ROUSSEL: There's a train moving east off the siding, towards Bruges. Loaded.
- VASSEUR: I can reach it before the canal bridge.
- ROUSSEL: Take Crane.
- CRANE: We're not going to make the bridge.
- ROUSSEL: Then we've done half a job, and somebody else will pay for the other half.

---

## Level 6 — Hohrupt

**3 October 1917 · Hohrupt, upper Fecht valley, Alsace · low grey morning · `Mountain`, `Morning`**
Scroller, and the only one where the squadron itself is the strike. Up a closing valley with the
slopes above them held: flak and gun positions on both walls, scouts over the ridgeline, and the
village at the end of the run. The valley is the level — it is too narrow to circle in, so the
mechanic and the story say the same thing, which is that you get one pass. Drama peak. Marchand
is killed.

### Loading screen

> The order came down on the second of October and was signed by somebody who had never seen the
> valley. Two German battalions were billeted in the houses at Hohrupt and the guns that had been
> killing our infantry all summer sat on the slope above it. The houses were not empty of anyone
> else.
>
> Hohrupt was Alsatian. Its people had been French until 1871 and expected to be French again, and
> that is the part of it I have never been able to put down: they were waiting for us. They had
> been waiting forty-six years.
>
> We asked. Lasalle did not lie to us, which I have never decided whether to be grateful for. We
> carried the white fire ourselves this time — the same as we had found in the wood in September,
> only now it was ours and it was in racks under our own wings. One pass, west to east, down the
> length of the street. A valley that narrow does not let you have two.
>
> Théo went round again anyway. He was the best of us and he could not simply fly over it.
> — É. Vasseur

**Objective**: one pass down the valley. Put the fire in the street and climb out east.

### Radio

**S1 — the order** *(opening)*
- LASALLE: Hohrupt, upper valley. Two battalions in the houses, and the battery above them.
- LASALLE: You go in first and low. Bourdin's Breguets are four minutes behind you.
- VASSEUR: Commandant, we're carrying the white stuff. The same as September.
- LASALLE: You are.
- MARCHAND: The people in it. Are they out?
- LASALLE: The order is signed.
- MARCHAND: That isn't an answer.
- LASALLE: No. It isn't.

**S2 — into the valley** *(before the first flak and slope-gun wave)*
- ROUSSEL: Line astern. Follow the stream and stay under the ridge.
- CRANE: They've got guns on both walls up there. They're firing *down* at us.
- ROUSSEL: Then don't give them a second look at you. One pass. Nobody turns in here.
- MARCHAND: There's washing hung out. In October, in the rain, there's washing hung out.

**S3 — the street** *(as the strike goes in, mid-level)*
- ROUSSEL: Drop and climb. Don't look down at what you've done, look at the ridge in front of you.
- CRANE: God almighty.
- MARCHAND: There are people in the street. Roussel, there are people in the street.
- ROUSSEL: I know.
- BOURDIN: Escort — Bourdin. Two minutes out. Is the valley clear for us?
- ROUSSEL: It's clear. It's all yours.

**S4 — Marchand, and the cost** *(final beat, before `finish`)*
- MARCHAND: I'm going round again.
- ROUSSEL: There is no round again. Marchand, there's no room —
- MARCHAND: There's a woman in the square waving at me. She thinks we've come to help them.
- ROUSSEL: Marchand!
- CRANE: Flak, off the north wall. He's taken it in the left wing.
- MARCHAND: It's all right. It's all right, I've got —
- VASSEUR: Théo. Pull up. Théo, *pull up*.
- ROUSSEL: Form on me. We are going home.
- VASSEUR: Capitaine —
- ROUSSEL: Form on me.

---

## Level 7 — Two Fires

**24 March 1918 · Rimbach valley · morning into midday · `Mountain`, `Midday`**
Fixed-width arena, time attack, and the only level with friendlies to defend rather than kill for.
Roussel and Crane are on the valley floor with a German company working up it. Vasseur flies alone
against ground columns and aircraft over one stretch of mountain road until the relief column gets
up the pass at thirteen hundred. The arena walls are the valley walls — there is nowhere to go and
that is the point.

### Loading screen

> In March everything with an engine went north. The Germans came through at Saint-Quentin on the
> twenty-first, and by the twenty-third the mountains had been stripped to feed it, because the
> mountains were a quiet sector and quiet sectors pay for loud ones.
>
> They went down together on the twenty-fourth, in the Rimbach — Roussel with a leg he couldn't
> put weight on and Crane carrying most of him. A column would come up the pass road at thirteen
> hundred if the road held. Between them and the road there was a company of infantry, possibly
> two, and nobody to spare.
>
> There were three of us left in the squadron by then, and two of them were walking. So it was
> me, and it was five hours, and I have never afterwards been frightened in quite the same way.
> — É. Vasseur

**Objective**: hold the valley floor until 13:00. Do not let them reach the pass road.

### Radio

**S1 — alone** *(opening)*
- LASALLE: Roussel and Crane are down in the Rimbach. Both walking, which is more than they deserve.
- LASALLE: A column comes up the pass road for them at thirteen hundred. Until then, there is you.
- VASSEUR: What's between them and the road?
- LASALLE: A company. Perhaps two, working up the valley since first light, and we can't stop them.
- VASSEUR: Then I'll stop the ones in front.

**S2 — contact** *(after the first ground wave)*
- CRANE: Somebody up there is French. Tell me that's you, Vasseur.
- VASSEUR: It's me. Stay in the ditch.
- ROUSSEL: Machine gun on the hairpin, above the sawmill. It's the hairpin or it's nothing.
- CRANE: He's got a bad leg. I'm not carrying him three kilometres uphill.
- ROUSSEL: You will if I tell you to.

**S3 — fuel** *(mid-level, at the timer's halfway mark)*
- LASALLE: Vasseur, your fuel. Come back and we'll send another machine.
- VASSEUR: There isn't another machine.
- LASALLE: That is not your decision to take.
- VASSEUR: Say again? You're breaking up.
- LASALLE: …Thirteen hundred, Vasseur. Not one minute past.

**S4 — the column** *(final beat, before `finish`)*
- CRANE: Lorry on the pass road. There's the lorry, there's the lorry.
- ROUSSEL: Vasseur. Get out of here. Go home.
- VASSEUR: When you're on it.
- CRANE: We're on — go!
- ROUSSEL: Good hunting today. Théo would have said it better.

---

## Level 8 — Iron Birds of Prey (boss)

**15 May 1918 · over the passes, towards the Belfort gap · last light · `Mountain`, `Evening`**
Boss level. A Gotha G.IV under escort, coming over the mountains for the rail hub in the gap below
— Gothas flew at dusk and into the dark, so the light is going the whole fight and it is going
against the player. Ravensberg leads the escort. Roussel is killed. The squadron gets its name.

### Loading screen

> A Gotha is not an aeroplane so much as a decision made by a committee. Two engines, seventy-eight
> feet of wing, a gun in the nose, a gun on the back, and a tunnel cut through the floor so the
> rear gunner can shoot down and behind — the *Gotha sting*, which is where everyone who attacked
> one in the ordinary way had gone.
>
> One came over the passes in the last hour of light on the fifteenth of May, heading down for the
> rail hub in the Belfort gap, with Jasta 41 above it and the black Fokker with the white raven
> leading them.
>
> There were three of us. Two came back. The Capitaine had eleven days left of the war he'd been
> in since 1914.
> — É. Vasseur

**Objective**: bring down the Gotha before it reaches the rail hub.

### Radio

**S1 — the target** *(opening)*
- LASALLE: One Gotha with escort, over the passes, down towards Belfort. Already past the guns.
- ROUSSEL: One bomber. And nobody in this sector has brought one down yet.
- CRANE: Why not?
- ROUSSEL: Because it shoots backwards and underneath, and men keep sitting where it can see them.
- VASSEUR: Then we come from where it can't.

**S2 — the escort** *(as the escort wave and the ace arrive)*
- CRANE: Black machine, high. Same one.
- RAVENSBERG: Vasseur. Still alive. I did wonder.
- ROUSSEL: Vasseur — the bomber. That's the whole job. Leave him to me.
- VASSEUR: Capitaine —
- ROUSSEL: The bomber.

**S3 — Roussel** *(mid-fight)*
- CRANE: Roussel's hit. He's hit badly, he's —
- ROUSSEL: Keep going. Don't turn round. Keep going.
- VASSEUR: Auguste.
- ROUSSEL: It's a good enough day for it. Finish the bomber.
- CRANE: He's gone in. Vasseur — he's gone in.

**S4 — the name** *(after the Gotha is destroyed, before `finish`)*
- VASSEUR: Both engines burning. She's going down.
- RAVENSBERG: *Eiserne Raubvögel.* That is what we call you now, on our side of it.
- RAVENSBERG: Iron birds of prey. Whatever is left of you.
- CRANE: Metal raptors. That's what he said. Metal raptors.
- VASSEUR: Then that's the name. Take us home.

---

## Continuity for the later eras

- The name is an enemy's, translated by an American, adopted by a Frenchman. It travels in English
  from 1918 onward.
- **What the group inherits from this era**: Roussel's rule that nobody flies as the last man;
  Crane's refusal to leave a fight; and Vasseur's rule from level 4 — you do not shoot a man who is
  already down.
- **Vasseur** is 21 at the end of the war and runs the group after it. Any later era can reach back
  to him by name, by rule, or by a descendant.
- **Ravensberg** survives, twice spared. His line — the raven, the family, four hundred years of it
  — is the natural source of a recurring antagonist in any later era.
- **Hohrupt** is the group's private shame and the reason they later refuse certain orders. It is
  worse than an ordinary bad order because the village was Alsatian — they burned people who were
  waiting to be liberated by them — and because the squadron carried the fire itself rather than
  escorting somebody else who did. It is the strongest single hook this era hands forward.

## Production notes

Nothing here is implemented. What the story needs from the code:

- **Speakers** (`CampaignSpeakers`): the player id stays `you` but displays `VASSEUR`; add
  `roussel`, `marchand`, `crane`, `lasalle`, `bourdin`, `ravensberg`. Six new entries, no code
  change beyond the array (docs/campaign-scripts.md).
- **Loading screens** now exist as the pre-level briefing (docs/level-briefing.md): the three
  fields — `title`, `dateline`, `lore` — are on `CampaignDefinition`, and levels 1–2 carry
  placeholder lorem ipsum. Dropping each level's text in above is a data edit.
- **Terrain**: `TerrainKind` has `Verdun` and `Flanders`, which covers levels 1–5. **Levels 6–8 need
  a `Mountain` terrain that does not exist yet** — a closing valley with slopes high enough to
  shoot down from, plus the fixed-width variant for level 7. That is the single biggest asset and
  code dependency in the back half of the campaign.
- **Daytime**: `Daytime` already has `Morning`, `Midday`, `Evening`, `Night`, so every level's light
  is expressible today. Levels 3 and 8 both want the light to be visibly failing during play; if
  `Evening` is a static preset, they still work, they just lose a beat.
- **Levels 3–8 are unbuilt**, and several need mechanics the campaign scene does not have: a
  fixed-width arena (3, 7), ground targets and balloons (3, 5), night + searchlights (5,
  docs/searchlight.md), a player air-to-ground incendiary loadout against a village strip (6), a
  mission timer with defendable friendlies on the ground (7), and a Gotha G.IV — a new
  `PlaneModelConfig` entry and a multi-hit boss (8). Level 6 no longer needs escortable friendly
  bombers: Bourdin's flight is radio only.
- **`CampaignLevels` is out of date with this file**: `Level2` is currently `TerrainKind.Flanders` /
  `Daytime.Morning` and datelined "Somme sector", where the campaign now wants `Verdun` / `Midday`.
  Level 1 already matches. Fixing level 2 is a three-line data edit in `CampaignDefinition.cs`.
- **Aircraft on hand** are `fokker` and `sopwith`, which covers the squadron and *Jasta 41*.
  Ravensberg's machine is a black-and-white reskin of the Fokker Dr.I; the Gotha is a new model.
- Dialogue placement per situation is noted in italics beside each block, in the vocabulary of
  the script grammar (`wave`, `spawn`, `waitclear`, `finish`).
- **The lines below are the source text, not the shipping format.** Building a level means
  copying each line into `Assets/Dialogue/Resources/Dialogue/lines.json` under an `l<level>_line<n>`
  key and referencing that key from the level's script (docs/campaign-scripts.md).
