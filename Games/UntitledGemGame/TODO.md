

In demo and play tests, on main menu when clicking quit, add a "are you sure to quit?" but ask users to wishlist game, add personal touch with image or something
open in steam overlay for wishlist

Pack texture into atlas

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
Some modules references "full cargo" deliveries. we always deliver full cargos so they need to change
Add stats and info about ships in the shipyard view

playtest:
    Make the fuel economy upgrades better faster or even base fuel higher

    Reset the current selection of meta upgrades when prestigeing

    gem color upgrade should be way more expensive the later, like exponentially curve for the colors
    Upgrade for drones to spawn wider out from home base

    first prestige should be at about 5 points
    prestige loop spam is a bit odd

<!-- passive income tooltips -->
<!-- buy ability point "red gems" -> all refering to "red gems" to user should be removed -->
<!-- Drones should return? weird they disapear? -->
    hard to see what can be upgraded and whats max, visually

    Second systems?
     - Infinite upgrades
     - Rogue-like 3 option selection for permanent buff
     - Positive and negative effects slots you can select powers in
       - Like hollow knight charms
       - Like more gem value but less spawns
       - Like make harvesters worse but abilities better
       - Etc





       › ok any good ideas for how we should give these modules to the player? one option is like signlas and ability etc with just
  gem cost. Is there any other good options thats common in incremental games?


• I’ll look at a few incremental-game examples and suggest acquisition systems that fit your modules and prestige loop.

• Searching the web

• Searched the web for site.store.steampowered.com incremental game loot equipment crafting prestige lootun ...

─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

• I’d lean toward salvage discoveries, with some control over what you find. Finding a new module gives players a reason to
  rethink their loadout, which fits your experimentation goal well.

  Several established incremental-game approaches could work:

   Approach                  How it could work here                               Main benefit
  ━━━━━━━━━━━━━━━━━━━━━━━━  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   Random loot               Harvesters occasionally recover wreckage             Exciting discoveries during normal play.
                             containing a module.
  ────────────────────────  ───────────────────────────────────────────────────  ────────────────────────────────────────────
   Expeditions               Send ships to investigate derelicts; different       Players can pursue particular rewards.
                             locations offer different module pools.
  ────────────────────────  ───────────────────────────────────────────────────  ────────────────────────────────────────────
   Milestones / contracts    Complete objectives such as collecting a certain     Predictable progress and clear goals.
                             number of gems or reaching a new sector.
  ────────────────────────  ───────────────────────────────────────────────────  ────────────────────────────────────────────
   Crafting / blueprints     Find blueprints, then spend recovered materials      A long-term route toward a desired module.
                             to construct modules.
  ────────────────────────  ───────────────────────────────────────────────────  ────────────────────────────────────────────
   Challenge rewards         Complete special runs with restrictions to earn      Makes rare modules feel earned.
                             particular powerful modules.
  ────────────────────────  ───────────────────────────────────────────────────  ────────────────────────────────────────────
   Choose-one rewards        Opening a cache presents three modules; keep one.    Combines surprise with meaningful choice.

  Loot, crafting, and challenge rewards are established in idle RPGs—for example, Lootun combines equipment drops, crafting,
  and endgame challenges.

  For your game, my preferred starting version would be:

  - Shipyard grants three starter modules, giving players an immediate choice for their two slots.
  - Harvesting fills a salvage meter. When full, recover a cache and choose one of three unowned modules.
  - Progression unlocks better cache tiers, gradually introducing higher rarities.
  - Owned modules stay through prestige.
  - Later, add expeditions with different reward pools so players can hunt missing pieces.

  I’d make salvage progress depend on meaningful harvesting activity, with a controlled reward rate. A flat chance per gem
  could let fast builds unlock the entire collection too quickly.

  Avoid duplicate modules initially. You currently have unique equipment with distinct effects; finding something new
  supports experimentation better than collecting copies. And I’d keep acquisition objectives neutral—don’t require a
  particular module pairing, since that would reveal the combinations you want players to discover.

─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

─ Conversation recap ────────────────────────────────────────────────────────────────────────────────────────────────────────

  We’re exploring how players should acquire modules beyond gem purchases like signals and abilities. The suggestion so far
  is salvage discoveries with some control over rewards. Next is to compare common alternatives and choose what fits the
  game.
