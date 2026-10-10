

In demo and play tests, on main menu when clicking quit, add a "are you sure to quit?" but ask users to wishlist game, add personal touch with image or something
open in steam overlay for wishlist

Pack textures into atlas

Crash when sfx volume is 100% and hover in upgrade menu.
Cluster core there should be visible center gem with an effect or something
Lucky gem needs more of a visual thing also

Upgrade to double gem value but spawn half as many or something.

Gotta ramp up upgrades more probably. Right now theres no clear point when to prestige and theres too many buttons to click
probably shouldnt be possible to do full upgrade tree without prestige
require zoom level 1 for regular prestige?

Should we re-add merge gem and fix it?
 - Should be after auto refuel upgrade in that case so you cant stack up

More upgrades in the regular tree that boosts gem value and speed up money, that you cant realisticly reach without prestige
Is collection range too large with all meta and normal upgrades?


Back to main menu sometimes the buttons doesnt appear.
<!-- Some modules references "full cargo" deliveries. we always deliver full cargos so they need to change -->
<!-- Add stats and info about ships in the shipyard view -->


System for exchanging which harvester types you want? like swap a seeker for a prospector???? maybe...
Or decomission a harvester type for a bonus, click stats bonus? ability bonus? or something


<!-- Sounds on abilities is too much -->
<!-- Add signals for the clicking powers -->
Drones adds the chain lightning/link from click ability
<!-- Right click to make a little black hole/magnetizer to suck gems in. -->
replace one of the commands with one that reduses cooldown on abilities for a short perioid or something.


<!-- More granular points to jump between when testing, like all abilities but no ships etc. -->
<!-- Or like all clicker upgrades but no meta upgrades, etc. so we can test different parts of the game more easily -->


<!-- Spread out the upgrade tree a bit, its very clumped up -->

left click animation can be skipped if its a simple click, make it wait tiny but so its only used when holding down.
replace reserve burst command with an upgrade that automatically bursts it when its becomes full.


Meta upgrade like a super ability slot where you can pick one of the abilities to have, so you can have 2 of the same ability for one of them.








MOdule ideas:
    a powerful module that combines all harvesters of a type into one, but it costs a lot of fuel to run and is slow, but can be upgraded to be faster and more efficient. (like a mega harvester)

Talent ideas:
  - a talent where each point of damage releases twice as many gems. empoweres a gem collecting build -> damage from unspent gems
  - A talent where unspent gems give damage increase, small percentage but it adds up over time.
  - Recoil harvest doesnt make sense in tier 1, you probably wont have the weapon this early



Trailer:

Ok so what i have here in this project is a bunch of recorded clips. I enumerated them in a suggested order to use them in the trailer, not set in stone but an example.

They might need to be cut a but in the start and end of clip since i had to move mouse to the window when recording.

So basically what i need is a video game trailer, around the 1:00 to 1:30 minutes timeframe. Gotta make it juicy and make people want to play the game, also need to show the features of the game in a good way. We could also overlay text to explain whats happening, i named the clips with a hint of what they want to show. Add some music and make an engaging trailer that makes people want to play the game Beyond the Belt!


No need to export and publish a clip at the end, i can do that after i inspected the timeline.










Both pieces are installed and tested against your Resolve Studio 21.1. Restart Claude Code before using them: this session
  can't load the new MCP tools.

  What's installed:
  - Blackmagic's MCP server, registered in Claude Code as davinci-resolve for all projects. It lives in its own folder,
    ~/.local/share/blackmagic-resolve-mcp.
  - The resolve-game-video skill in ~/.claude/skills/. It isn't tied to one project: each trailer or Short gets its own
    working folder and a new versioned timeline, and your existing timelines are never modified.
  - Nothing else needed installing; it runs on ffmpeg, numpy and ImageMagick, which you already had.

  What the skill does:
  - Checks footage and makes fixed copies Resolve can read. Your originals are never touched.
  - Scans long recordings for action moments and makes timestamped contact sheets.
  - Finds tempo, beats, bar starts and drops in your music. On synthetic test tracks it was within 10 ms; I haven't tried it
    on real music yet.
  - Makes title and call-to-action cards at exact lengths.
  - Builds the timeline in Resolve from a plan you approve first, places beat markers, renders, then makes the upload file at
    standard loudness with platform checks.

  I made a mistake earlier. The unzip path from my first answer was the folder of a samuelgursky Resolve MCP you use from
  Codex, and you ran that command. It overwrote that install's package.json and LICENSE and added three files. I restored the
  exact originals from its npm package and confirmed the folder matches. Your Codex config never read those files, so it
  should have kept working.

  Things I found by testing, now built into the skill:
  - Audio: AAC audio (the usual OBS default) imports but comes out completely silent, MKV audio isn't read at all, and Opus
    files are refused. The skill converts these first.
  - Frame rate: variable-frame-rate recordings are read at the wrong speed (a 60 fps clip showed as 48 fps), so they get
    converted to a constant rate.
  - Rendering: Resolve on Linux can't actually encode AAC, even with its YouTube preset, and puts FLAC in the MP4. The last
    delivery step converts it to AAC.
  - Render folder: Resolve only renders into your Media Storage folder (~/Videos). Anywhere else, it opens a dialog that
    freezes all scripting.
  - API traps: Text+ titles inserted by script cut into V1 and shift everything after, so title cards go on V2 as clips
    instead. Saving an untitled project opens a dialog that freezes scripting too.

  For testing I started Resolve with your ~/start-davinci.sh and used a throwaway project, which I deleted afterwards, along
  with its test renders. I closed the dialogs my tests caused. Your three projects were never opened. Resolve is still
  running.

  The skill only ever uses music you supply; the generated tracks were just test fixtures with known beat positions. If you
  send me the track you plan to use, I'll check the beat detection on it. After restarting, you can just ask, for example:
  "make a 60-second Steam trailer from my footage in ~/Videos/capture with this track".
