# Beyond the Belt — Trailer Structure & Reference Guide

This document captures the trailer structure blueprint for **Beyond the Belt**, synthesized from two reference videos on game trailer editing (**Derek Lieu's "5 Simple Game Trailer Templates"** and **Artindi's "11 Tips To Make Better Game Trailers"**), along with the full transcripts of both videos for future reference.

---

## Part 1: Core Principles for *Beyond the Belt*

1. **The Stand-Up Comedy / 5-Second Hook Rule (Derek Lieu + Artindi Tip #1)**
   - Open immediately (0:00–0:05) with the most visually striking late-game spectacle — no slow fade-in, no origin story in the first 5 seconds.
   - Once you've earned the viewer's goodwill and set the promise of where the game goes, **reset** to the humble beginning (a single ship, manual cannon shots knocking the first red gems loose) and build back up to an even bigger finish.
2. **Hybrid Format: *Chronological Escalation* + *Tell, Show, Repeat* Overlays (Derek Lieu #1, #2, #3 + Artindi Tip #2)**
   - Incremental games have a built-in *Chronological Order* arc: 1 ship → first upgrades → multi-weapon bombardment → specialized fleet → active systems & modules → cracking & extracting the core → massive late-game scale.
   - Use short, verb-driven text **overlaid directly on gameplay** (never cut away to black title cards) and cut strictly on the musical grid.
3. **Skip Genre Clichés — Highlight Unique Mechanics & Cause → Effect (Artindi Tip #3, #7, #8)**
   - Viewers already expect an incremental game to have an upgrade tree. Instead of lingering on generic "Upgrade!" labels or transition filler, highlight what makes *Beyond the Belt* tactile and surprising:
     - **Physical planetary mining:** Cracking the planet's outer shell, harpoons, railguns, and cluster rockets physically knocking faceted gems into orbit.
     - **Visible ship behaviors & modules:** Fitting *World Eater* to turn a ship into a roaming black hole, *Trove Hunters* warping back to base, or *Genesis Pulse* blooming golden rings of gems.
     - **The Core Fracture & Extraction loop:** Loose gems collapsing back into the planet before erupting a Core Shard, and collapsing the entire planet into a singularity to start the next time loop.
4. **Visual Variety & Clarity (Derek Lieu Clarity Tip + Artindi Tip #10 & #11)**
   - Prevent "every shot looks like dots around a circle" by alternating **camera scale** (tight close-ups on weapon impacts/single ship vs. wide fleet views), **color shifts** (early red gems → golden rings → purple/blue singularity), and **clean cause-and-effect pairs** (1.5s UI action → immediate 2s battlefield payoff).
   - Keep total runtime tight (**50–60 seconds**) and end with a clear **Call to Action** (*Wishlist Beyond the Belt on Steam*).

---

## Part 2: Audit of `03-gameplay-trailer/edit.json` vs. Trailer Principles

The 60-second cut in [`03-gameplay-trailer/edit.json`](03-gameplay-trailer/edit.json) already uses a cold open before the break in *Pleiadeans*, resets to the crash-landing shell break, and builds chronologically to the drop and endgame. Key areas to tighten in future cuts:

| Area | Current `03-gameplay-trailer` | Improvement from Reference Videos |
| :--- | :--- | :--- |
| **0:06–0:15 Reset / Lore** | Spends **8.3 seconds** (`intro_shell_v2` + `shell_break_v2`) on the crash-landing and shell breaking with `"ENGINE FAILURE... NEXT TO A MYSTERIOUS PLANET"`. | **Artindi Tip #1, #2, #3 & #8:** Don't slow the momentum right after the cold open with a dip-to-black and 8 seconds of slow intro lore. Trim the crash/shell-shatter reset to **~3.3 seconds** (impact → shatter → first manual cannon shots) so the core gameplay loop starts immediately. |
| **Caption Copy** | Uses some generic noun/genre-expected labels: `"*UPGRADE*"`, `"*SHIP SYSTEMS*"`, `"PERMANENT *TALENTS*"`. | **Derek Lieu #1 + Artindi Tip #7:** Replace feature nouns with concrete verbs/goals or unexpected twists (e.g., *"CRACK THE PLANET'S SHELL"*, *"TURN SHIPS INTO BLACK HOLES"*, *"COLLAPSE THE CORE. KEEP YOUR POWER."*). |
| **UI Shots vs. Payoff** | Several UI shots (`upgrade_ui_v2`, `shipyard_ui_v3`, `signals_ui_v3`, `talents_v3`) flash by at 1.7x–2.4x speed, or show a module (`module_worldeater_v3`) for only `0.83s`. | **Derek Lieu Clarity Tip + Artindi Tip #3:** Fast-forwarded UI can read as visual spreadsheet noise, while a `0.83s` payoff is too short to register what *World Eater* actually does. Pair a crisp, readable 1.5s UI action (fitting *World Eater* or picking a Signal) with a clear 1.6–2.5s close-up of the resulting effect on the field. |
| **Game Audio** | `"game_audio": null` (music only). | **Artindi Tip #6 + Showcase Ground Rules:** Layer in key game sound effects (shell shatter, railgun/cannon impacts, legendary reveal hit, core fracture implosion) synced with the music to make the mechanics feel tactile. |

---

## Part 3: Proposed 60-Second Trailer Blueprint ("The Comedy-Club Escalation")

### Structural Flow

1. **0:00–0:05 — Cold Open Hook:** 50k-Gem Spectacle, Chain Lightning, & Core Fracture
2. **0:05–0:13 — The Reset:** Shell Shatter, 1 Ship, & First Upgrade
3. **0:13–0:25 — Act I (Escalate Firepower):** Orbital Weapons Barrage & Automated Fleet Classes
4. **0:25–0:40 — Act II (Break the Rules):** Active Ship Systems, *World Eater* Module, & Core Shard
5. **0:40–0:47 — The Twist / Loop:** Extract Core Singularity → Permanent Talents → Empowered Restart
6. **0:47–0:55 — Big Finish Climax:** Full Fleet + Golden Spirals + Graviton Cascade Pull-Out
7. **0:55–1:00 — Call to Action:** Wishlist *Beyond the Belt* on Steam

### Beat-by-Beat Shot Plan

| Time | Section | Visuals & Camera Framing (Visual Variety) | Overlay Text (Action / Verb Focus) | Audio & Pacing Notes |
| :--- | :--- | :--- | :--- | :--- |
| **0:00 – 0:05** | **1. The 5-Second Hook** *(Stand-up opener)* | **3 rapid high-contrast shots (1.67s / 0.83s / 2.5s):**<br>1. Wide late-game swarm firing railguns & rockets into a packed gem field.<br>2. Close-up of *Graviton Cascade* chaining across hundreds of gems.<br>3. *Core Fracture* pulling the entire field inward and erupting. | *(No text for first 3s — let visual hook speak)* | Open at high energy + punchy game SFX (chain lightning + fracture boom). Quick cut (no long fade) into the reset. |
| **0:05 – 0:13** | **2. The Reset: Humble Start** *(Contrast)* | **Tight close-up (`zoom: 1.8–2.2`):**<br>1. The solitary ship cracks the planet's rocky outer shell (`shell_break`, ~2.5s).<br>2. Manual cannon clicks knock a handful of red gems loose; the lone Drifter slowly scoops them up (~3.5s).<br>3. Quick, clean punch-in on the Upgrade Tree lighting up weapon & fleet nodes (~2.0s). | **START WITH ONE SHIP**<br><br>**MINE EVERY GEM** | Music drops to the quiet break. Sparse, distinct cannon *thuds* and single gem pickup *chimes* emphasize how small you start. |
| **0:13 – 0:25** | **3. Act I: Arm & Automate** *(Cause → Effect)* | **Medium framing (`zoom: 1.1–1.4`), cut on bars:**<br>1. **Drop hits:** Cannon, Twin Mining Lasers, Harpoons, Rocket Clusters, and Railgun fire in rapid succession — each shot visibly knocking higher-tier gems loose.<br>2. Specialized ship classes join in: *Prospectors* chain-scooping rocket gem clusters; *Trove Hunters* warping back to homebase in a flash. | **ARM YOUR ORBITAL ARSENAL**<br><br>**BUILD AN AUTOMATED FLEET** | Music drop lands at the start of the weapon montage. Cut each weapon on the half-bar/bar so impacts match the beat. |
| **0:25 – 0:40** | **4. Act II: Unique Mechanics** *(Defy Genre Expectations)* | **Alternate crisp UI → close-up gameplay payoff:**<br>1. **Ship Systems:** Trigger *Genesis Pulse* — golden rings of gems bloom outward, followed by *Kamikaze Wing* / *Graviton Cascade* sweeping the ring.<br>2. **Shipyard Modules:** Punch-in (`zoom: 1.8`) dragging **World Eater** into a ship slot (1.5s) → immediate cut to that ship on the field acting as a roaming black hole devouring gem clusters (2.0s).<br>3. **Signals & Core Fracture:** Pick a deep-space Signal card (1.5s) → trigger a *Core Fracture* to shatter out a glowing Core Shard (2.5s). | **TRIGGER SHIP SYSTEMS**<br><br>**FIT GAME-CHANGING MODULES**<br><br>**SHATTER THE CORE** | Each UI interaction has a single clear focal point (cursor click → immediate on-field transformation). |
| **0:40 – 0:47** | **5. The Prestige Twist** *(The Macro Loop)* | 1. Hold *Extract Core* — the planet collapses into a dramatic singularity (`prestige`, ~3.3s).<br>2. Spend prestige points unlocking a Keystone in the Permanent Talent tree (~1.7s).<br>3. New run begins — immediately firing a massive golden ring on second 1 (~2.0s). | **COLLAPSE THE PLANET**<br>*Loop back stronger every run* | Musical riser into the singularity collapse; satisfying bass hit when the new run explodes with immediate power. |
| **0:47 – 0:55** | **6. The Climax** *(Stand-up closer)* | **Pull-out to full scale (`zoom: 1.6 → 1.0`):**<br>1. Multi-weapon barrage + *Fault Scan* sweeping weak points across the planet.<br>2. Golden *Genesis Spirals* + full fleet + *Graviton Cascade* chaining across thousands of gems at once. | *(No text — pure visual payoff)* | Peak of the musical track; full layered audio spectacle. |
| **0:55 – 1:00** | **7. Call to Action** *(Artindi Tip #9)* | Endgame gem collection continues underneath the slam end card / logo. | **WISHLIST BEYOND THE BELT ON STEAM** | Clean musical ending hit + tail fade. |

---

## Part 4: Reference Video Transcripts

### Video 1: *5 Simple Game Trailer Templates* — Derek Lieu (`KRvEFwHXm8s`)

- **URL:** https://www.youtube.com/watch?v=KRvEFwHXm8s
- **Creator:** Derek Lieu (Video Game Trailer Academy)

#### Summary of Templates Covered
1. **Core Rule — The Stand-Up Comedy Act (`00:28`):** Open with one of your best "jokes" (most exciting shots) to earn goodwill, reset, and build up to your best material at the end.
2. **Template 1 — Tell, Show, Repeat / Bullet Point Trailer (`00:52`):** Use title cards or narration to call out what you *do* or the *goals* of the game (verbs, not raw feature counts like "10 levels"), then immediately show it. Sandwich between a strong opener and closer.
3. **Template 2 — Music Video Montage (`01:56`):** Cut fun, easy-to-read gameplay to the beat of the music. Sequence shots from simplest to understand at the beginning to most complex at the end.
4. **Template 3 — Chronological Order (`02:44`):** Present gameplay in the exact progression order the player experiences in-game (like Super Mario Bros. World 1-1: introduce mechanic, twist it, escalate), cutting out the boring parts.
5. **Template 4 — Just Explain the Game (`03:32`):** Best for complex/abstract games (e.g., *SpaceChem*, *Heat Signature*) where fast cuts aren't readable at a glance. Walk the viewer linearly through how a problem is solved.
6. **Template 5 — "In a World..." Story Structure (`05:06`):** (1) The world/premise → (2) The character → (3) The problem they face → (4) How they confront it → (5) The obstacles in their way.
7. **Closing Rule — Keep Gameplay Capture Clear (`06:28`):** Hide clutter or compose shots to limit how many unfamiliar elements are on screen at once. Retention of knowledge beats quantity of content.

#### Full Timestamped Transcript (`KRvEFwHXm8s`)

```text
[00:00] Here are five of the most simple and effective ways to structure a game trailer,
[00:04] especially if you're a first time editor.
[00:06] But to be honest, I've seen trailers for big budget games...
[00:09] which don't even have this much structure.
[00:12] Hi, my name is Derek Lieu and this is Video Game Trailer Academy,
[00:15] a series of videos to help you make better game trailers.
[00:18] Creating the basic structure of a game trailer can be daunting...
[00:21] especially if you've never done any video editing,
[00:24] so I'm going to share five basic outlines to help you get started.
[00:28] But before that, my first tip is to think of your trailer like a stand up comedy act.
[00:33] A comic needs to open with one of their best jokes to earn good will with the audience,
[00:38] and they need to finish with their best material to leave them with a good feeling at the end.
[00:42] So don't start with your most vanilla footage,
[00:46] start with one or two really exciting ones, then reset and build back up to a big finish.
[00:52] First trailer structure you might try is what I'm going to call
[00:56] Tell, show, repeat.
[00:58] For this format you use title cards and/or narration...
[01:02] which describe a thing, and then show the thing.
[01:04] For example:
[01:19] I sometimes call these bullet point trailers because they start from a list of bullet points,
[01:24] call them out and then show them.
[01:26] If you choose this format I recommend calling out the things...
[01:29] you DO in the game, or the GOALS of the game like:
[01:41] Don't call out raw features like how many levels or boss battles there are,
[01:46] because those are pretty boring selling features and won't differentiate your game.
[01:51] Don't forget to sandwich this bullet point section between an exciting opening and closing.
[01:56] Next up is to make a Music Video Montage
[01:59] This is simply taking fun gameplay, and cutting it to the beat of the music.
[02:19] This format is best used for games which are very simple to understand from watching,
[02:24] like games with running and jumping, shooting, or physical violence.
[02:29] If you can also sequence the gameplay so the simplest to understand shots are at the beginning,
[02:34] and the most complex are at the end,
[02:36] this will make it much easier for the audience to watch and understand.
[02:40] Again, sandwich this between an exciting opening and ending.
[02:44] Next is to simply make your trailer in Chronological Order
[02:48] Simply capture footage from your game, keep everything in the order they appear in the game,
[02:53] and cut them in an exciting way while maintaining that order,
[02:56] just without the boring bits.
[03:08] A lot of games are structured somewhat like level 1-1 in Super Mario Bros.
[03:03] in that they introduce ideas, make some twists on that idea,
[03:07] and then test the player's ability to understand them.
[03:10] This format for games is essentially what a lot of game trailers do,
[03:14] so it stands to reason keeping this sequence of events...
[03:17] in that same order might work for a trailer!
[03:19] I'm often surprised to see game trailers which don't do this considering
[03:23] how hard game designers work to create this sort of arc in their games.
[03:27] The excellent trailers for Papers, Please and Astroneer's Release Trailer
[03:30] follow this chronological format.
[03:32] Number four is Just Explain the Game
[03:36] Some games are really difficult to understand at a glance.
[03:39] Maybe the game uses a lot of abstract symbols which have no analog in real life,
[03:44] or maybe there are multiple steps which the audience will just not understand
[03:48] until they've played the game.
[03:50] This spec trailer by Ashley Ruhl for the Zachtronics game Spacechem is a good example.
[03:55] It uses title cards to explain the game in a linear fashion,
[03:58] and it uses good editing and music to make it entertaining.
[04:01] Some other examples are this amazing trailer for Heat Signature narrated by Alex Ashby.
[04:06] NARRATOR: You play a contractor living on a remote nebula...
[04:09] NARRATOR: Taking on missions to break into space ships.
[04:12] NARRATOR: Breaking into space ships is a ridiculous job...
[04:14] NARRATOR: that will certainly get you killed.
[04:16] NARRATOR: But luckily there are EASY missions you can choose...
[04:18] NARRATOR: Well, that one is labeled MISTAKE, so you might not want to...
[04:21] NARRATOR: Okay fine, we're doing the one literally labeled MISTAKE.
[04:23] And also this trailer made by Kert Gartner for the VR version of Fantastic Contraption.
[04:28] NARRATOR: Fantastic Contraption is about building and using your creativity.
[04:33] NARRATOR: The challenge of the game is simple.
[04:35] NARRATOR: You have to get the pink ball to the pink goal.
[04:41] NARRATOR: And to do that, you have to build a Fantastic Contraption!
[04:48] Again, this approach mostly works for games where the visuals...
[04:51] are difficult to read in a fast cut trailer.
[04:54] If you try to explain something which is easily understood,
[04:57] you're probably going to bore the audience or insult their intelligence...
[05:01] explaining how a double jump or dialogue selection UI works.
[05:06] Last but not least is the infamous In a world...
[05:10] No seriously.
[05:11] For a story trailer this is about as dead simple as you can get.
[05:15] Story trailers are probably the most difficult to execute of the formats in this list.
[05:19] but an "in a world..." style voiceover can be a crutch to lean on.
[05:24] The voiceover simply needs to say in this order:
[05:27] 1. This is the world or the premise
[05:29] NARRATOR: In the year 2004 time travel is a reality.
[05:34] 2. This is the person in that world
[05:40] How are you doing, partner?
[05:41] EX-partner.
[05:42] Let me go, Max. I'm not hurting anybody.
[05:44] I've got to take you back.
[05:45] 3. This is the problem that person faces
[05:48] NARRATOR: One man...
[05:48] You ever hear the name Aaron McComb?
[05:50] NARRATOR: Is about to take the ultimate power trip.
[05:53] He's gonna be president!
[05:54] 4. This is what they're going to do to confront it
[05:57] NARRATOR: One man is determined to stop him.
[06:00] If I cannot go back to save her...
[06:01] this scumbag is not going back to steal money.
[06:04] 5. These are the obstacles they're going to face
[06:07] Stay here Walker. In my future you're dead.
[06:14] I think you planned too far ahead
[06:15] This is what nearly all story trailers do!
[06:18] Some of them skip over the world and start with characters.
[06:21] And a lot of teasers stop at what problem the character is faced with,
[06:24] because the premise is often interesting enough to tease the audience.
[06:28] My last tip is to make sure the gameplay capture is CLEAR.
[06:32] That can mean having no HUD or UI elements or just composing shots to limit the number
[06:37] of things on screen at any given time.
[06:39] Retention of knowledge is far more important than quantity of content.
[06:43] And remember, start with an exciting and hooky intro...
[06:46] whether it's via the art, animation, music, voiceover, game mechanics or something else.
[06:52] Given how hard it is to gain and hold someone's attention,
[06:56] the first few seconds can't waste any time hooking the audience.
[06:59] These trailer structures are all very simple, but that doesn't make them bad.
[07:03] I still cut trailers using a mix of these formats!
[07:05] The only "advanced" thing I think I do now is play with the pacing and mix the formats.
[07:11] A logical order is often best because it makes the information easy to consume.
[07:15] If you've ever watched a trailer with little to no rhyme or reason to the order of the clips...
[07:20] especially with dialogue...
[07:21] it can become an incoherent mess.
[07:23] So good luck and have fun!
[07:25] By the way, I send out game trailer making tips like this every Sunday via my mailing list,
[07:29] which you can subscribe to at GameTrailerTips.com
[07:32] As always, thank you to my top Patreon supporters who help me make more videos like this!
[07:36] Links to all the trailers in this video are down below.
```

---

### Video 2: *11 Tips To Make Better Game Trailers* — Artindi (`2e6DUA7U1OE`)

- **URL:** https://www.youtube.com/watch?v=2e6DUA7U1OE
- **Creator:** Artindi

#### Summary of the 11 Tips
1. **The First 5 Seconds Are the Most Important (`00:22`):** Viewers click away around 5 seconds. Don't fade in, slowly build tension, or explain world lore. Show your most interesting core loop/mechanic or tease the climax immediately.
2. **Don't Waste Any Time (`01:02`):** Never cut away from gameplay to black title cards. Overlay text directly on gameplay (or use voiceover), and avoid slow fades to black between cuts.
3. **Show Relevant Gameplay (`01:23`):** Cut out approach/transition filler (e.g., walking up to ore before mining, or walking through a tavern door). Cut straight to the action.
4. **Get the Shot You Need (`01:58`):** Re-record takes until the framing and timing are clean rather than settling for the first recording.
5. **Don't Try to Be Fancy with Transitions (`02:31`):** Amateur wipe/spin transitions make a game look unprofessional. Stick to clean cuts.
6. **Don't Skimp on the Music (`02:59`):** Use the game's actual soundtrack or music from the same source that fits its exact vibe.
7. **Avoid Genre Expectations (`03:31`):** Don't call out things players already assume exist in your genre (like "command lots of units" in an RTS). Call out what breaks or expands genre expectations.
8. **Focus on Unique Mechanics, Not Story (`04:36`):** Players don't trust an unproven studio's story promise (like *Portal 1* focusing almost entirely on the portal mechanic before *Portal 2* earned the right to lead with story). Show mechanics first.
9. **Have a Call to Action (`05:55`):** Explicitly tell the viewer what to do at the end ("Wishlist on Steam", "Play it now").
10. **Include Visual Variety (`06:36`):** If every clip looks visually identical (e.g., staring at the same UI or background), viewers tune out even if different mechanics are happening underneath. Vary environments, colors, shapes, and interactions.
11. **Don't Drag It On (`07:20`):** Get to the point and leave the viewer wanting more. A tight, punchy trailer beats padding to 2–3 minutes.

#### Full Timestamped Transcript (`2e6DUA7U1OE`)

```text
[00:00] An important part of any commercial indie game is a trailer.
[00:03] Often times it's the first thing people see about your game and first impressions really matter.
[00:07] As part of my channel, I watch a lot of game trailers and as part of my real life job in a marketing team,
[00:10] I have a few tips that could help you out next time you need to make a trailer.
[00:13] Tip number seven I think is especially helpful, so stay tuned for that one.
[00:17] By the way, most of my videos are very sarcastic, but this one is not. So enjoy some actually straightforward advice.
[00:22] Number one, the first five seconds are the most important.
[00:24] The attention span of potential customers is very short and most people will click away from an ad around the five second mark.
[00:30] So you have five seconds to pique their interest.
[00:33] Don't waste the most important five seconds of your trailer with fading in, building up tension,
[00:37] or telling the origin story of the world your game takes place in.
[00:40] Instead, show the most interesting part of your main gameplay loop or main mechanic first,
[00:43] focusing on the particularly unique aspects of your game.
[00:47] If you really need to have some sort of theatrical build up to your trailer, perhaps due to the nature of your core gameplay loop,
[00:52] I would still hold off for five seconds and instead tease what is to come later in the trailer.
[00:56] It might seem like a spoiler, but people are much more willing to sit through the whole thing if their expectations are properly set.
[01:02] Number two, don't waste any time.
[01:04] I know it seems cool and cinematic, but don't cut away from the gameplay to put text on the screen.
[01:09] You only have about a minute to show what your game is about and showing gameplay is a much better use of time.
[01:14] If you must include text, overlay it on top of the gameplay or better yet, use a voice over if possible.
[01:19] You also don't want to waste time with long fade ins or fade outs to black between cuts.
[01:23] Number three, show relevant gameplay.
[01:25] This kind of goes along with the last point of not wasting time,
[01:29] but for example, if you have a mining game and you want to show the player mining something,
[01:32] you don't need to include a three second shot of the player approaching the ore that needs mined.
[01:37] Or let's say you have an RPG where the player can enter different buildings.
[01:40] It might only take a second or two, but showing the transition of a player entering a tavern is wasted time in a trailer.
[01:45] Instead, just show a shot of the player fighting something outside, then a shot of the player talking with a tavern keep.
[01:50] The viewer will understand that these are different places and that you can travel between them.
[01:54] Cut out anything that isn't going to help sell your game. Every fraction of a second counts.
[01:58] Number four, get the shot you need.
[02:00] Take the time to record any part of gameplay you need,
[02:02] but be willing to do it over and over again until you get it as perfect as possible.
[02:07] When making ads for products, I've gone hours shooting the same shot until I get the take that works,
[02:12] especially when there are uncontrollable variables in the shot.
[02:15] And even then, I've reset up a whole scene and done it over again just because something was off.
[02:20] Of course, you don't impede yourself with the perfectionism,
[02:22] but don't assume your first recording is the one you need. It often isn't.
[02:25] Just be willing to try a few takes to shine the best light you can on your game.
[02:28] It'll take some patience, but could be well worth it.
[02:31] Number five, don't try to be fancy unless you know what you're doing.
[02:34] Often when I see trailers made by amateur video editors, they will include creative transition between cuts.
[02:40] Obviously doing this makes your trailer feel unprofessional and gives the impression that your game may be just as unprofessional.
[02:45] Most of the time you don't need any fancy transitions at all.
[02:48] If possible, try to watch some tutorials for video editing, specifically for marketing videos or trailers.
[02:52] Taking a few minutes to research what you're doing can really make a big difference.
[02:55] Don't be afraid of doing it yourself though. Just remember, sometimes less is more.
[02:59] Number six, don't skimp on the music.
[03:01] If you plan to make a trailer for a game, then you have made or are making a game.
[03:05] This game has music somehow, whether you outsourced it or made it yourself, you hopefully have unique music.
[03:10] So, your trailer should have the same. And you should either use music from your game or something from the same source.
[03:15] Using stock music is fine, but maybe try to find something unique that fits the vibe of your game.
[03:20] Keep in mind, whatever shortcuts you take in making your trailer will be noticed
[03:23] and will suggest that similar shortcuts may have been taken while making your game.
[03:27] It's worth the effort to come up with fitting music to have your trailer give a good first impression.
[03:31] Number seven, avoid genre expectations.
[03:34] Whatever genre your game is, you don't want to point out things that are assumed to be a core aspect of that genre.
[03:39] For example, if you are showing gameplay of an RTS game, you don't want to say something like "command lots of units."
[03:44] This is assumed to be a core aspect of RTS games.
[03:47] Instead, whatever you do expressly point out, have it focus on things that players might not expect to be in your genre
[03:52] or might not expect to be missing from your genre.
[03:54] Plus, this will hopefully set expectations for when players actually play your game.
[03:57] For example, most RTS games take place on a single 2D battlefield.
[04:01] If you were to make a multi-level RTS, saying something like "command on every level" while showing some multi-surface terrain battle on screen,
[04:07] it would definitely draw interest. In fact, I kind of wish someone would make an RTS like that. I would for sure give it a try.
[04:12] As with every rule, there are exceptions.
[04:14] For example, the survival game Vintage Story, which is the gameplay you see on screen right now,
[04:17] has a trailer that basically only points out common things found in many survival games.
[04:21] But because Vintage Story has so many of these things, it turns out to be a good strategy for selling a sandbox survival game,
[04:27] where the more things you can do, the better.
[04:30] They're basically saying, "Our sandbox survival game is the best because you have so many options."
[04:34] Go check out their full trailer for yourself. It's really well done.
[04:36] Number eight, focus on unique mechanics, not story.
[04:40] The reason why I recommend focusing on mechanics instead of story is it's easy to make a story sound like it could be good,
[04:45] but people know that doesn't mean it will be.
[04:47] I'm sure you've experienced this yourself by watching a movie because the trailer was really awesome, only to find out that the movie sucks.
[04:53] And many people will watch a movie without ever seeing a trailer just because it was made by a particular director who has proven themselves.
[04:59] You have to earn a reputation as a good storyteller before people really believe it.
[05:04] The example I often give is the Portal trailers.
[05:06] The trailer for the first Portal game focuses almost exclusively on the portal mechanic with only hints of the story in the trailer,
[05:12] even though the story is amazing.
[05:14] But Valve understood that most players are interested in gameplay, and their trust in storytelling is hard to earn.
[05:19] Only after the game was a hit and the story was enjoyed by millions of people do they lead with the story in the marketing for the sequel.
[05:25] The trailer for the second game flips the paradigm and focuses on the story with hints of more mechanics.
[05:31] By this time, the trust of the player base was already earned. People knew the story was awesome and were confident the sequel would deliver.
[05:37] And boy, did it. Even so, subsequent trailers still focused on new mechanics.
[05:41] So, in general, it's better to focus your trailers on your unique mechanics and hint at a good story.
[05:46] The only exception to this in my opinion are games that are extraordinarily story driven,
[05:50] but even so, you should demonstrate how the story drives the game, which is a focus on gameplay.
[05:55] Number nine, have a call to action.
[05:57] A call to action in marketing is basically telling the viewer of your ad or trailer to do something.
[06:02] For some reason, humans respond to such commands statistically much better than if no command is given.
[06:07] This is why YouTubers so often say, like, share, and subscribe, even though it's obvious that that's an option.
[06:12] It's been proven to increase subscriptions.
[06:15] Which I'll be honest, I've never done that on my channel, even though I probably should. It just feels too awkward and cliché.
[06:20] But have that be a lesson for what you shouldn't do. Don't let feeling awkward stop you from making a good trailer for your game.
[06:26] End it with something like, "Play it now," or "Find it on Steam," or even be brand specific: "Slay dragons this July."
[06:36] Number 10, include visual variety.
[06:38] If you watch a trailer and every single clip of the game seems to be pretty much the same thing, you're going to lose interest really fast.
[06:44] I've seen trailers for games that are based on spreadsheets and found that each clip told me more or less the same thing as the one before it:
[06:50] that the game uses spreadsheets.
[06:52] Even if deep down what's happening on screen is actually distinct parts of gameplay,
[06:56] if it looks too visually similar, the viewer isn't going to know that.
[06:59] Remember, they might not understand exactly how your game works.
[07:02] Try to pull clips from different levels or in-game interactions to make each unique part of your game look distinct from the others.
[07:08] If you find this is hard to do, it might be a sign that your game needs a bit more variety in color or shape to improve marketability,
[07:14] or that your game requires a unique approach to marketing that standard trailers are unable to convey.
[07:20] Number 11, don't drag it on.
[07:21] While you want to use every bit of real estate you have in a trailer, you don't want to overdo it.
[07:26] Get to the point, express the point, and leave your viewer wanting more.
[07:29] Some games are simple and don't require a lot, and that's all right. In fact, that can be a good thing.
[07:34] Remember, the goal of a trailer is to get potential players enticed.
[07:37] If you can do that in 15 seconds, there's no need to force fill in 3 minutes.
[07:41] And often times, shorter is sweeter because you can get to that call to action sooner.
[07:44] Plus, using an appropriate amount of time to showcase your game will likely make your trailer feel more professional.
[07:49] And that's 11 tips that will improve your game trailers.
[07:52] Of course, your game trailer can really only be as good as your game.
[07:55] And honestly, the better your game is, the more it will sell itself.
[07:58] But don't let some simple mistake in trailer making keep people from realizing your game's full potential.
[08:03] And if you made it this far, thanks for watching and have a great day.
```

---

### Video 3: *How to Use Sound Effects to Make a Great Game Trailer* — Derek Lieu (`brNYbUlPrBk`)

- **URL:** https://www.youtube.com/watch?v=brNYbUlPrBk
- **Creator:** Derek Lieu (Video Game Trailer Academy)

#### Summary of Sound Effects Tips
1. **Convey Tactile Game Feel (`00:18`):** Good sound effects make player actions feel tactile and physical (weapons firing, coins/gems collected, abilities triggering) so viewers understand what it *feels* like to play.
2. **Use Sound to Guide the Viewer's Eye (`01:17`):** In a busy frame with lots of objects on screen, a clear, prominent sound effect makes the viewer naturally look for its source and understand what is happening.
3. **Treat SFX as Part of the Musical Score (`02:03`, `03:29`, `04:08`):** Syncing sound effects to the beat acts like an extra percussion track that makes hits land harder and punctuates transitions.
4. **Tip #1 — Strip Out Clutter & Highlight the Shot's Focus (`04:54`):** Raw game audio often has too many background sounds firing at once (or auto-ducked background mixes). Pick the primary focus of each shot and make sure its sound effect reads clearly without distracting noise. Always capture with in-game music turned off.
5. **Tip #2 — Give SFX Time to Shine in the Mix (`06:15`):** Don't let key sound effects drown out under the trailer music, and don't worry about 100% raw in-game volume/sync authenticity if boosting or nudging a sound makes the trailer hit harder and read more clearly.

#### Full Timestamped Transcript (`brNYbUlPrBk`)

```text
[00:00] If you want to show what a game feels like to play,
[00:03] make sure the trailer includes the game’s sound effects!
[00:10] Hi, I'm Derek Lieu and this is Video Game Trailer Academy.
[00:13] A series of videos to help you make better game trailers.
[00:18] If a trailer's intention is to convey the look, idea, and feel of a game,
[00:23] trailers with no sound effects simply cannot provide the full experience.
[00:27] Of course this is assuming the game HAS sound effects.
[00:35] Good sound effects make the actions of the player tactile,
[00:38] and they help the person watching the trailer understand what it FEELS like to play the game.
[00:46] Think of all the sound effects designed to help the player understand
[00:50] what is happening in a game like:
[00:52] an enemy about to attack,
[00:55] coins being collected,
[01:00] a gun reloading,
[01:03] or your health getting recharged.
[01:08] If those sounds weren’t present, you’d have a far more limited sense of the feel of the game.
[01:17] Sound can even help people better SEE what’s happening on screen,
[01:20] because when we hear a sound effect we’ll naturally look for its source.
[01:24] For example look at this clip with sound effects turned off
[01:32] And here it is with them turned on
[01:39] Even though the frame is pretty busy,
[01:41] the sound guides your eye and helps you see the source of the sound.
[01:45] The first time I played Katana ZERO I didn't care about
[01:48] how many mistakes lead to my character's death,
[01:50] because every sword swipe, and bullet deflection felt so good to listen to.
[01:59] When I had the opportunity to work on a trailer for it, I was SO excited...
[02:03] to use those sound effects to make each moment very punchy and exciting,
[02:07] especially when synced to the beat of the music.
[02:18] That said, I understand why someone might make a game trailer with no sound effects.
[02:22] It makes it much easier to capture footage,
[02:24] especially if you forgot to turn music off while capturing,
[02:27] and sometimes the soundscape of a game can be very chaotic and distracting when in a trailer.
[02:34] Editing without sound is also much easier because sound effects can compete with music and dialogue.
[02:39] Mixing sound is that much more complicated with sound effects layered on top,
[02:43] and it can be unpleasant if they don't work well with the music.
[02:46] Take a look at this trailer for La Mulana 1 & 2 Hidden Treasures Edition.
[02:51] This trailer doesn’t integrate the sound effects of the game well into the trailer at all.
[02:55] There’s a loud noise when a puzzle is solved,
[03:00] and when text is writing onto the screen there’s an invasive buzzing sound.
[03:06] It’s as if someone brought an air horn into a classical music concert.
[03:12] This is an example of when clips and their sounds are NOT well integrated
[03:15] into the soundtrack of the trailer.
[03:17] But you shouldn’t think of sound as a hindrance to the trailer;
[03:20] it's another opportunity to flesh out the game,
[03:22] and stimulate the audience's senses in a way music, dialogue, and visuals cannot.
[03:29] Syncing sound effects to the music is one of my favorite things to do;
[03:32] I like to make my trailers at least as fun to listen to as they are to watch if not more so.
[03:46] And I’m certainly not the only one doing it, just watch the trailer for any modern FPS
[04:00] It’s silly as heck, but it WORKS.
[04:02] It makes trailers a lot of fun to watch.
[04:08] I think of sound effects as part of the trailer's musical score;
[04:11] it's like an unusual percussion track which makes some beats hit extra hard,
[04:22] and punctuates lines of dialogue.
[04:27] Sound is a physical force; it affects us by literal vibrations in the air...
[04:32] which are received not just through our ears, but our bodies too.
[04:35] It doesn't even have to be a deep subwoofer bass note to register;
[04:38] a sound effect will provide a more complete PHYSICAL experience...
[04:41] of the game for those able to hear it.
[04:50] Here are some tips for integrating sound effects into a game trailer.
[04:54] TIP #1: If the raw game audio sounds too chaotic for the trailer
[04:57] pick and choose what you want the focus of the shot to be,
[05:00] and only include those sound effects.
[05:03] For example, Dead Cells has a LOT of sounds firing at all times whether it's the sound of:
[05:08] gems being collected, bloody explosions, swords swiping, magic fire, or enemy birds squawking.
[05:22] It's essential to strip out anything distracting so the focus of the shot can be understood.
[05:34] I always capture with music turned off so I can start with the raw game audio.
[05:37] But If I need to separate out sound effects I’ll either request files from the developers,
[05:41] or record them individually in-game and sync them manually.
[05:45] Something to keep an ear out for when working with raw audio files:
[05:48] Some games dynamically adds reverb or other post processes based on where they occur in the game.
[05:54] For example, a sound might be affected if it‘s heard...
[05:56] in a cavernous location versus a wide open space.
[06:05] If this is the case, the sound might need to be processed to match what it's like in-game.
[06:10] Or you might want to just capture it from in-game as a quicker way to make it sound authentic.
[06:15] TIP #2: When editing, give the sound effects their time to shine,
[06:19] and make sure they don’t step on the other elements in the soundtrack.
[06:22] Remember, raw game audio can make the trailer’s soundtrack random and chaotic.
[06:30] Don’t worry about being 100% authentic.
[06:33] Sometimes I even nudge the sound effects a little off sync from what is correct...
[06:37] just so it'll fit into the trailer better.
[06:41] The game and sound designers will likely notice anything being off sync,
[06:45] but the trailer audience is unlikely to since they're not as familiar with the game.
[06:49] Sorry, sound designers!
[06:50] As always this advice will not fit every single game.
[06:53] Trailers I made for Heaven’s Vault, Neo Cab and Ooblets...
[06:56] have either no sound effects or virtually none,
[06:59] either because there were very few in game, they were early in development, or some other reason.
[07:04] You need to pick and choose depending on the game,
[07:07] but I still recommend trying really hard to include the game’s audio...
[07:10] before you decide it’s not working for the trailer.
[07:13] Sound is an OPPORTUNITY to give a better feel for what it's like to be immersed in the game.
[07:18] Yes, this does mean more work and potential problems,
[07:21] but it will make the trailer that much more representative of the game,
[07:24] and help the audience better understand whether or not they want to play it.
[07:35] If you want more game trailer making tips like this,
[07:37] I send them out every Sunday via my mailing list, which you can subscribe to at GameTrailerTips.com
[07:42] Thanks as always to my top Patreon supporters who help me make more videos like this!
[07:46] Links to all the trailers in this video are located down below.
```

