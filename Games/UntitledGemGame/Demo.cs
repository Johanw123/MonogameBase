using UntitledGemGame;

public static class Demo
{
  public const string Version = "0.1";
  public static bool IsDemo = true;
  public static bool IsDev = false;
  // Read at startup. Disable all Steam initialization and relaunch attempts.
  public static bool DisableSteam = false;
  public static string VersionLabel => IsDemo ? Loc.F("v{0} DEMO", Version) : "v" + Version;
}

// What the demo holds back. It plays the loop through the first two talent tiers, so a
// player meets fractures, Core Shards, extraction, talents and ship systems, and stops one
// step short of the Shipyard:
//  - Talents: tiers past TalentTiers can't be learned and pay no free rewards. The first of
//    them is shown in full as a preview; the rest show their names only.
//  - Prestige points: the ladder stops paying at PrestigePoints, enough to try different
//    picks in the open tiers but not to fill them.
//  - Upgrades: nodes flagged lockedindemo in upgrades_buttons.json (the late fleets and the
//    warp drive) and every node of a held-back weapon.
//  - The run played after the points for the third tier are spent closes on the
//    end-of-demo dialog (GameScreen.DemoEnd); play goes on after it.
public static class DemoLimits
{
  // Capture scenes and checks set this to play with or without the limits; unset follows the build.
  public static bool? Override;
  public static bool Active => Override ?? (Demo.IsDemo && !Demo.IsDev);

  public const int TalentTiers = 2;
  public const ulong PrestigePoints = 8;

  public static bool LocksTier(int tier) => Active && tier >= TalentTiers;
  // The locked tier shown in full: what the player would have reached next.
  public static bool PreviewsTier(int tier) => Active && tier == TalentTiers;
  public static bool LocksWeapon(MainShipWeapon weapon) => Active && weapon == MainShipWeapon.Railgun;
  public static bool PointsExhausted(ulong earned) => Active && earned >= PrestigePoints;

  public static string FullGameNote => Loc.T("Available in the full game");
}
