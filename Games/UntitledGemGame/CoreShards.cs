using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Core Shards are a rare per-run currency released by core fractures (CoreFracture)
// and spent on the powerful upgrades in the regular tree. A run cannot fund every
// powerful upgrade, so each run commits to a build. Shards, fractures and the
// upgrades bought with them all reset at prestige.
public static class CoreShards
{
  public const string Currency = "gold";
  public const string Name = "Core Shards";
  public static readonly Color Color = new(255, 200, 90);
  public const string IconPath = "Textures/Gems/Gem4/GEM 4 - GOLD - Spritesheet.png";
  public const int IconFrames = 11;

  // Powerful upgrades outside the weapons; weapon ones are tuned in MainShipWeapons.
  public const float MidasTouchValueMultiplier = 3f;
  public const float GoldenHoldsValueMultiplier = 2f;

  public static float ClickValueMultiplier(UpgradesGeneratorUpgrades ug)
    => ug.MidasTouch ? MidasTouchValueMultiplier : 1f;

  public static float FleetValueMultiplier(UpgradesGeneratorUpgrades ug)
    => ug.GoldenHolds ? GoldenHoldsValueMultiplier : 1f;
}
