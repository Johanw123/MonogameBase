#if !KNI_WEB
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Capture;

// A stand-in player for pacing playthroughs (scene "autoplay"). It clicks at a set rate: on
// Core Shards as they appear, on the planet while the cannon is still manual or the field is
// thin, and on gem clusters otherwise. It fires fleet commands as they come ready and goes
// shopping every few seconds: Core Shard upgrades first, then the cheapest affordable regular
// upgrade (or a power cell when that is cheaper), then system talents. It extracts by the
// scene's rule, learns talents (highest open tier first, skipping ones a pick rules out) and
// starts the next run. It works in game seconds, whatever the scene's time scale. A shopping
// round that buys nothing logs "idle" with the cheapest open upgrade, its price and the gems
// held ("none" when nothing is left to buy), to find dead time.
public sealed class SceneAutoplay
{
  public double ClickRate { get; set; } = 3;
  public double ShopEvery { get; set; } = 10;
  // Share of clicks aimed at the planet once the cannon fires on its own and the field has gems.
  public double PlanetShare { get; set; } = 0.15;
  // When to extract: "never", "points" (this run holds Points) or "stall" (no new point for
  // StallMinutes, after at least MinRunMinutes). Loops stops after that many extractions.
  public string Extract { get; set; } = "never";
  public int Points { get; set; } = 3;
  public double StallMinutes { get; set; } = 5;
  public double MinRunMinutes { get; set; } = 10;
  public int Loops { get; set; } = 100;
  // Core Shard upgrades to buy, in order of preference, and only these ([] buys none). Unset:
  // ShardPriority, then any other.
  public string[] Shards { get; set; }
}

internal static class Autoplay
{
  // Core Shard upgrades a player would grab first: the economy ones, then weapons.
  private static readonly string[] ShardPriority = ["MDT1", "GLH1", "GTC1", "LZQ1", "RSW1", "THP1", "TC1", "IW1", "BDS1"];
  private const int SparseField = 150;

  private static double nextClick, nextShop, nextCommand, runStart, lastProgress;
  private static Vector2 from, to;
  private static double moveStart, moveSeconds;
  private static bool moving, pressed;
  private static ulong pending, fractures;
  private static int extractions;

  public static void Update(SceneAutoplay plan, double t)
  {
    var screen = UntitledGemGameGameScreen.Instance;
    var pointer = CaptureSession.Pointer;
    var state = screen.State;
    pointer.Visible = true;
    if (pressed) { pointer.Left = false; pressed = false; }
    if (screen.m_prestiging) return;
    if (screen.m_postPrestige)
    {
      LearnTalents(state);
      screen.CaptureStartNewRun();
      CaptureSession.Log("run", (extractions + 1).ToString());
      runStart = lastProgress = t;
      return;
    }

    if (state.PendingPrestigePoints > pending)
    {
      CaptureSession.Log("point", state.PrestigePointsEarned.ToString());
      lastProgress = t;
    }
    pending = state.PendingPrestigePoints;
    if ((ulong)state.CoreFractures > fractures) CaptureSession.Log("shard", state.CoreFractures.ToString());
    fractures = (ulong)state.CoreFractures;

    Click(plan, screen, t);
    if (t >= nextCommand)
    {
      nextCommand = t + 1;
      for (int slot = 0; slot < ManualFleetAbilities.Definitions.Length; slot++)
        if (screen.CaptureActivateManual(slot)) CaptureSession.Log("command", ManualFleetAbilities.Definitions[slot].Name);
    }
    if (t >= nextShop)
    {
      nextShop = t + plan.ShopEvery;
      Shop(plan, state);
    }
    if (extractions < plan.Loops && ShouldExtract(plan, state, t))
    {
      CaptureSession.Log("extract", state.PendingPrestigePoints.ToString());
      extractions++;
      pending = 0;
      fractures = 0;
      moving = false;
      screen.ExtractCore();
    }
  }

  private static bool ShouldExtract(SceneAutoplay plan, GameState state, double t)
  {
    if (!CoreExtraction.CanExtract(state.PendingPrestigePoints, state.CoreExtractions)) return false;
    return plan.Extract switch
    {
      "points" => state.PendingPrestigePoints >= (ulong)plan.Points,
      "stall" => t - runStart >= plan.MinRunMinutes * 60 && t - lastProgress >= plan.StallMinutes * 60,
      _ => false,
    };
  }

  // Glide to a target at a hand-like speed, click on arrival, then wait out the click interval.
  private static void Click(SceneAutoplay plan, UntitledGemGameGameScreen screen, double t)
  {
    var pointer = CaptureSession.Pointer;
    if (!moving)
    {
      if (t < nextClick) return;
      from = pointer.Position;
      to = PickTarget(plan, screen);
      float distance = Vector2.Distance(from, to);
      moveSeconds = Math.Clamp(0.08 + 0.35 * distance, 0.1, 0.4);
      moveStart = t;
      moving = true;
    }
    float progress = (float)Math.Clamp((t - moveStart) / moveSeconds, 0, 1);
    pointer.Position = Vector2.Lerp(from, to, progress * progress * (3 - 2 * progress));
    if (progress < 1) return;
    pointer.Left = pressed = true;
    moving = false;
    nextClick = moveStart + Math.Max(moveSeconds, 1 / Math.Max(0.1, plan.ClickRate));
  }

  private static Vector2 PickTarget(SceneAutoplay plan, UntitledGemGameGameScreen screen)
  {
    var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
    Vector2 world;
    if (screen.CaptureShardPosition is Vector2 shard) world = shard;
    else
    {
      bool planet = !UpgradeManager.Instance.UG.AutoCannon || grid.NumActiveGems < SparseField
        ? Random.Shared.NextDouble() < 0.6 : Random.Shared.NextDouble() < plan.PlanetShare;
      if (planet || !grid.TryGetWeightedClusterPosition(Random.Shared, out world))
      {
        float angle = Random.Shared.NextSingle() * MathHelper.TwoPi;
        world = UntitledGemGameGameScreen.PlanetPos
          + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * UntitledGemGameGameScreen.PlanetRadius * 0.5f * Random.Shared.NextSingle();
      }
    }
    var window = screen.m_camera.WorldToScreen(world);
    // Keep clear of the HUD strip, as a player would.
    return new Vector2(Math.Clamp(window.X / CaptureSession.WindowWidth, 0.03f, 0.97f),
      Math.Clamp(window.Y / CaptureSession.WindowHeight, 0.05f, 0.82f));
  }

  private static bool Purchasable(UpgradeButton button)
    => button.State is UpgradeButton.UnlockState.Unlocked or UpgradeButton.UnlockState.Purchased
      && button.CurrentLevel < button.Data.LevelInfo.Count;

  private static ulong Price(UpgradeButton button) => button.Data.LevelInfo[button.CurrentLevel].Cost;

  private static int bought;

  // The real purchase path; false when the game refused it.
  private static bool Buy(GameState state, UpgradeButton button)
  {
    var before = (state.CurrentRedGemCount, state.CurrentBlueGemCount, state.CurrentPurpleGemCount, state.CurrentCoreShardCount);
    UpgradeManager.Instance.Upgrade(button);
    if ((state.CurrentRedGemCount, state.CurrentBlueGemCount, state.CurrentPurpleGemCount, state.CurrentCoreShardCount) == before)
      return false;
    CaptureSession.Log("buy", button.Data.ShortName);
    bought++;
    return true;
  }

  private static void Shop(SceneAutoplay plan, GameState state)
  {
    var trees = UpgradeManager.CurrentUpgrades;
    var refused = new HashSet<UpgradeButton>();
    bought = 0;
    var shards = plan.Shards != null
      ? plan.Shards.Select(id => trees.UpgradeButtons.GetValueOrDefault(id))
      : ShardPriority.Select(id => trees.UpgradeButtons.GetValueOrDefault(id))
        .Concat(trees.UpgradeButtons.Values.OrderBy(b => b.Data.ShortName, StringComparer.Ordinal));
    foreach (var button in shards)
      if (button != null && button.Data.UpgradeDefinition.Currency == CoreShards.Currency && Purchasable(button)
        && state.CurrentCoreShardCount >= Price(button))
        Buy(state, button);

    for (int purchases = 0; purchases < 500; purchases++)
    {
      var cheapest = trees.UpgradeButtons.Values
        .Where(b => b.Data.UpgradeDefinition.Currency == "red" && Purchasable(b) && !refused.Contains(b))
        .OrderBy(Price).ThenBy(b => b.Data.ShortName, StringComparer.Ordinal).FirstOrDefault();
      bool cellsUseful = UpgradeManager.Instance.UGM.ShipSystemsUnlocked
        && trees.UpgradeButtonsAbilities.Values.Any(b => Purchasable(b) && b.State != UpgradeButton.UnlockState.Revealed);
      if (cellsUseful && state.NextAbilityPointPrice is ulong cell && cell <= state.CurrentRedGemCount
        && (cheapest == null || cell <= Price(cheapest)))
      {
        if (!state.TryBuyAbilityPoint()) break;
        CaptureSession.Log("buy", "power_cell");
        bought++;
        continue;
      }
      if (cheapest == null || Price(cheapest) > state.CurrentRedGemCount) break;
      if (!Buy(state, cheapest)) refused.Add(cheapest);
    }

    refused.Clear();
    while (state.CurrentBlueGemCount > 0)
    {
      var talent = trees.UpgradeButtonsAbilities.Values
        .Where(b => Purchasable(b) && !refused.Contains(b) && Price(b) <= state.CurrentBlueGemCount)
        .OrderBy(Price).ThenBy(b => b.Data.ShortName, StringComparer.Ordinal).FirstOrDefault();
      if (talent == null) break;
      if (!Buy(state, talent)) refused.Add(talent);
    }
    if (bought > 0) return;
    var next = trees.UpgradeButtons.Values
      .Where(b => b.Data.UpgradeDefinition.Currency == "red" && Purchasable(b))
      .OrderBy(Price).FirstOrDefault();
    CaptureSession.Log("idle", next == null ? "none"
      : $"{next.Data.ShortName} {Price(next)} {state.CurrentRedGemCount}");
  }

  // The highest open tier first (its Gem Lore is worth the most), left to right, leaving out
  // the playstyle talents presets also skip.
  private static void LearnTalents(GameState state)
  {
    var tree = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    while (state.CurrentPurpleGemCount > 0)
    {
      string next = PrestigeTalentLayout.Tiers.Reverse().SelectMany(tier => tier.Talents)
        .FirstOrDefault(id => !PrestigeTalentLayout.PlaystyleTalents.Contains(id) && tree.TryGetValue(id, out var talent)
          && talent.CurrentLevel == 0 && PrestigeTalentLayout.CanLearn(tree, id));
      if (next == null || !Buy(state, tree[next])) break;
      CaptureSession.Log("talent", next);
    }
  }
}
#endif
