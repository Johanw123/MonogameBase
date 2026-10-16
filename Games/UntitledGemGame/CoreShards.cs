using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Core Shards are a rare per-run currency released by core fractures (CoreFracture)
// and spent on the powerful upgrades in the regular tree. A run cannot fund every
// powerful upgrade, so each run commits to a build. Shards, fractures and the
// upgrades bought with them all reset at prestige.
public static class CoreShards
{
  public const string Currency = "gold";
  public static string Name => Loc.T("Core Shards");
  public static readonly Color Color = new(255, 200, 90);
  public const string IconPath = "Textures/Gems/core_shard.png";
  public const int IconFrames = 1;
  public const string PrestigeIconPath = "Textures/Gems/prestige_orb.png";

  // Powerful upgrades outside the weapons; weapon ones are tuned in MainShipWeapons.
  public const float MidasTouchValueMultiplier = 2f;
  public const float GoldenHoldsValueMultiplier = 2f;

  public static float ClickValueMultiplier(UpgradesGeneratorUpgrades ug)
    => ug.MidasTouch ? MidasTouchValueMultiplier : 1f;

  public static float FleetValueMultiplier(UpgradesGeneratorUpgrades ug)
    => ug.GoldenHolds ? GoldenHoldsValueMultiplier : 1f;
}
