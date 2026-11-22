#if !KNI_WEB
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Capture;

// A stand-in player for pacing playthroughs (scene "autoplay"). It clicks at a set rate: on
// Core Shards as they appear, on the planet while the cannon is still manual or the field is
// thin, and on gem clusters otherwise. It fires fleet commands as they come ready and goes
// shopping every few seconds: Core Shard upgrades first, then the cheapest affordable regular
// upgrade (or a power cell when that is cheaper), then system talents, and fits each weapon
// blueprint it buys to a free hardpoint. It extracts by the
// scene's rule, learns talents (highest open tier first, skipping ones a pick rules out) and
// starts the next run. It works in game seconds, whatever the scene's time scale. A shopping
// round that buys nothing logs "idle" with the cheapest open upgrade, its price and the gems
// held ("none" when nothing is left to buy), to find dead time.
//
// With a profile (written by tools/record_run.py from a recorded human run) it makes that
// player's choices instead. Each run buys what the player bought in that run, in the same
// order: it saves up for the next red purchase (Patience seconds at most) and passes over
// shard, system talent and still-locked entries until they can be bought. Within a talent
// tier it learns in the player's order. Once a run's order is used up it shops by the rules above;
// runs past the recording reuse its last run's order. With the warp drive repaired it
// escapes at once, logs "escape" and ends the take a few seconds later.
public sealed class SceneAutoplay
{
  public double ClickRate { get; set; } = 3;
  public double ShopEvery { get; set; } = 10;
  // Share of clicks aimed at the planet once the cannon fires on its own and the field has gems.
  public double PlanetShare { get; set; } = 0.15;
  // The planet share once Trigger Finger is bought (every click then fires a shell anyway);
  // unset keeps PlanetShare.
  public double? TriggerFingerShare { get; set; }
  // When to extract: "never", "points" (this run holds Points), "stall" (no new point for
  // StallMinutes, after at least MinRunMinutes) or "bar" (as stall, but the extraction bar
  // must also have stopped climbing: no 5% gain for StallMinutes, the ExtractionGuide's test).
  // Loops stops after that many extractions.
  public string Extract { get; set; } = "never";
  public int Points { get; set; } = 3;
  public double StallMinutes { get; set; } = 5;
  public double MinRunMinutes { get; set; } = 10;
  // "stall" extracts only a run holding at least this many points. Above 0, a run that stops
  // earning points is played on instead, so a late game saves up for the warp drive.
  public int MinPoints { get; set; }
  public int Loops { get; set; } = 100;
  // Core Shard upgrades to buy, in order of preference, and only these ([] buys none). Unset:
  // ShardPriority, then any other.
  public string[] Shards { get; set; }
  // A human run's purchase and talent order (autoplay-profile.json), relative to the scene file.
  public string Profile { get; set; }
  // Seconds spent saving up for the profile's next red purchase before buying past it.
  public double Patience { get; set; } = 60;
  // Fire fleet commands as they come ready.
  public bool Commands { get; set; } = true;
  // Place a Tractor Belt intake on a gem cluster this many seconds after one is free
  // (negative: never use the belt).
  public double BeltDelay { get; set; } = -1;
  // Seconds on the talent screen after an extraction, before the next run starts.
  public double TalentSeconds { get; set; }
  // Save up for Repair Warp Drive (the full game's way out) once this run's income would pay
  // for it within this many minutes, as a player heading for the ending would (0: shop for it
  // like any other upgrade).
  public double WarpMinutes { get; set; }
}

// What a recorded player bought, per run, as "ID:level" ("power_cell:N" is the run's Nth
// power cell), and the talents in the order they were first learned.
internal sealed class AutoplayProfile
{
  public readonly List<(string Id, int Level)[]> Runs = new();
  public string[] Talents = [];

  public static AutoplayProfile Load(string path)
  {
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var root = document.RootElement;
    var profile = new AutoplayProfile();
    foreach (var run in root.GetProperty("runs").EnumerateArray())
      profile.Runs.Add(run.EnumerateArray().Select(entry =>
      {
        string text = entry.GetString() ?? "";
        int colon = text.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(text[(colon + 1)..], out int level) || level < 1)
          throw new ArgumentException($"{path}: \"{text}\" is not ID:level");
        return (text[..colon], level);
      }).ToArray());
    if (root.TryGetProperty("talents", out var talents))
      profile.Talents = talents.EnumerateArray().Select(t => t.GetString()).Where(t => t != null).ToArray();
    if (profile.Runs.Count == 0) throw new ArgumentException($"{path} has no runs");
    return profile;
  }
}

internal static class Autoplay
{
  // Core Shard upgrades a player would grab first: the economy ones, then weapons.
  private static readonly string[] ShardPriority = ["MDT1", "GLH1", "GTC1", "MGN1", "TDP1", "LZQ1", "RSW1", "THP1", "TC1", "IW1", "BDS1", "XPL1"];
  private const int SparseField = 150;
  private const string PowerCell = "power_cell";
  private const double EscapeTailSeconds = 10;
  private const int BeltCandidates = 8;
  private const string WarpRepair = "WDR1";

  private static double nextClick, nextShop, nextCommand, runStart, lastProgress;
  private static Vector2 from, to;
  private static double moveStart, moveSeconds;
  private static bool moving, pressed, belt, escaped;
  private static ulong pending, fractures;
  private static int extractions;
  private static double postPrestigeSince = double.NaN, beltFreeSince = double.NaN;
  private static double barMark, barSince;
  private static ulong barPending;
  private static AutoplayProfile profile;
  private static List<(string Id, int Level)> order;
  private static string savingFor;
  private static double savingSince;

  public static void LoadProfile(SceneAutoplay plan)
    => profile = string.IsNullOrEmpty(plan.Profile) ? null : AutoplayProfile.Load(plan.Profile);

  public static void Update(SceneAutoplay plan, double t)
  {
    var screen = UntitledGemGameGameScreen.Instance;
    var pointer = CaptureSession.Pointer;
    var state = screen.State;
    pointer.Visible = true;
    if (pressed) { pointer.Left = pointer.Right = false; pressed = false; }
    if (escaped) return;
    if (order == null) StartOrder();
    if (screen.m_prestiging) return;
    if (screen.m_postPrestige)
    {
      if (double.IsNaN(postPrestigeSince)) postPrestigeSince = t;
      if (t - postPrestigeSince < plan.TalentSeconds) return;
      postPrestigeSince = double.NaN;
      if (screen.DemoEndVisible)
      {
        CaptureSession.Log("demo_end", state.PrestigePointsEarned.ToString());
        screen.ContinueAfterDemoEnd();
      }
      LearnTalents(state);
      screen.CaptureStartNewRun();
      CaptureSession.Log("run", (extractions + 1).ToString());
      runStart = lastProgress = barSince = t;
      barMark = 0;
      barPending = 0;
      StartOrder();
      return;
    }

    if (state.PendingPrestigePoints > pending)
    {
      CaptureSession.Log("point", state.PrestigePointsEarned.ToString());
      lastProgress = t;
    }
    pending = state.PendingPrestigePoints;
    if (state.PendingPrestigePoints != barPending || state.BestPrestigeProgress >= barMark + ExtractionGuide.WallClimb)
    {
      barPending = state.PendingPrestigePoints;
      barMark = state.BestPrestigeProgress;
      barSince = t;
    }
    if ((ulong)state.CoreFractures > fractures) CaptureSession.Log("shard", state.CoreFractures.ToString());
    fractures = (ulong)state.CoreFractures;

    Click(plan, screen, t);
    if (plan.Commands && t >= nextCommand)
    {
      nextCommand = t + 1;
      for (int slot = 0; slot < ManualFleetAbilities.Definitions.Length; slot++)
        if (screen.CaptureActivateManual(slot)) CaptureSession.Log("command", ManualFleetAbilities.Definitions[slot].Name);
    }
    if (t >= nextShop)
    {
      nextShop = t + plan.ShopEvery;
      if (!SaveForWarp(plan, state) && !ShopProfile(plan, state, t)) Shop(plan, state);
    }
    if (screen.WarpEscapeReady)
    {
      CaptureSession.Log("escape", state.CoreExtractions.ToString());
      escaped = true;
      screen.ExtractCore();
      CaptureSession.EndAfter(EscapeTailSeconds / CaptureSession.Scene.TimeScale);
      return;
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
      "stall" => t - runStart >= plan.MinRunMinutes * 60 && t - lastProgress >= plan.StallMinutes * 60
        && state.PendingPrestigePoints >= (ulong)plan.MinPoints,
      "bar" => t - runStart >= plan.MinRunMinutes * 60 && t - lastProgress >= plan.StallMinutes * 60
        && t - barSince >= plan.StallMinutes * 60 && state.PendingPrestigePoints >= (ulong)plan.MinPoints,
      _ => false,
    };
  }

  // Glide to a target at a hand-like speed, click on arrival, then wait out the click interval.
  // A free Tractor Belt intake takes the next move: a right press on a gem cluster.
  private static void Click(SceneAutoplay plan, UntitledGemGameGameScreen screen, double t)
  {
    var pointer = CaptureSession.Pointer;
    if (!moving)
    {
      if (t < nextClick) return;
      from = pointer.Position;
      belt = BeltDue(plan, screen, t) && TryBeltTarget(screen, out to);
      if (!belt) to = PickTarget(plan, screen);
      float distance = Vector2.Distance(from, to);
      moveSeconds = Math.Clamp(0.08 + 0.35 * distance, 0.1, 0.4);
      moveStart = t;
      moving = true;
    }
    float progress = (float)Math.Clamp((t - moveStart) / moveSeconds, 0, 1);
    pointer.Position = Vector2.Lerp(from, to, progress * progress * (3 - 2 * progress));
    if (progress < 1) return;
    if (belt)
    {
      pointer.Right = true;
      beltFreeSince = double.NaN;
      CaptureSession.Log("belt", screen.TractorBelt.ActiveCount.ToString());
    }
    else pointer.Left = true;
    pressed = true;
    moving = false;
    nextClick = moveStart + Math.Max(moveSeconds, 1 / Math.Max(0.1, plan.ClickRate));
  }

  private static bool BeltDue(SceneAutoplay plan, UntitledGemGameGameScreen screen, double t)
  {
    var upgrades = UpgradeManager.Instance.UG;
    if (plan.BeltDelay < 0 || !upgrades.TractorBeltEnabled) return false;
    var stats = TractorBeltStats.From(upgrades, screen.GemClickRadius, UpgradeManager.Instance.Signals,
      UpgradeManager.Instance.UGM);
    if (screen.TractorBelt.ActiveCount >= Math.Max(1, stats.MaxIntakes))
    {
      beltFreeSince = double.NaN;
      return false;
    }
    if (double.IsNaN(beltFreeSince)) beltFreeSince = t;
    return t - beltFreeSince >= plan.BeltDelay;
  }

  // A player drags the intake deep into the field, so the belt runs through the gems: the
  // farthest of a few clusters (weighted by their gems) from the home base, kept on screen.
  private static bool TryBeltTarget(UntitledGemGameGameScreen screen, out Vector2 target)
  {
    target = default;
    var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
    var home = HomeBase.Instance?.Entity?.Get<Transform2>()?.Position ?? UntitledGemGameGameScreen.HomeBasePos;
    float farthest = -1;
    for (int i = 0; i < BeltCandidates; i++)
    {
      if (!grid.TryGetWeightedClusterPosition(Random.Shared, out var world)) continue;
      var pointer = ToPointer(screen, world);
      float distance = Vector2.DistanceSquared(screen.m_camera.ScreenToWorld(
        pointer * new Vector2(CaptureSession.WindowWidth, CaptureSession.WindowHeight)), home);
      if (distance > farthest) { farthest = distance; target = pointer; }
    }
    return farthest >= 0;
  }

  private static Vector2 PickTarget(SceneAutoplay plan, UntitledGemGameGameScreen screen)
  {
    var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
    Vector2 world;
    if (screen.CaptureShardPosition is Vector2 shard) world = shard;
    else
    {
      var upgrades = UpgradeManager.Instance.UG;
      double share = upgrades.ClickTriggerFinger && plan.TriggerFingerShare is double fingers ? fingers : plan.PlanetShare;
      bool planet = !upgrades.AutoCannon || grid.NumActiveGems < SparseField
        ? Random.Shared.NextDouble() < 0.6 : Random.Shared.NextDouble() < share;
      if (planet || !grid.TryGetWeightedClusterPosition(Random.Shared, out world))
      {
        float angle = Random.Shared.NextSingle() * MathHelper.TwoPi;
        world = UntitledGemGameGameScreen.PlanetPos
          + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * UntitledGemGameGameScreen.PlanetRadius * 0.5f * Random.Shared.NextSingle();
      }
    }
    return ToPointer(screen, world);
  }

  // Keep clear of the HUD strip, as a player would.
  private static Vector2 ToPointer(UntitledGemGameGameScreen screen, Vector2 world)
  {
    var window = screen.m_camera.WorldToScreen(world);
    return new Vector2(Math.Clamp(window.X / CaptureSession.WindowWidth, 0.03f, 0.97f),
      Math.Clamp(window.Y / CaptureSession.WindowHeight, 0.05f, 0.82f));
  }

  // Capture runs as a dev build, which can unlock unreleased weapons; players can't.
  private static bool Purchasable(UpgradeButton button)
    => button.State is UpgradeButton.UnlockState.Unlocked or UpgradeButton.UnlockState.Purchased
      && button.CurrentLevel < button.Data.LevelInfo.Count
      && (Weaponry.BlueprintWeapon(button.Data.ShortName) is not { } weapon || Weaponry.IsReleased(weapon));

  private static ulong Price(UpgradeButton button) => button.Data.LevelInfo[button.CurrentLevel].Cost;

  private static ulong Wallet(GameState state, string currency) => currency switch
  {
    "red" => state.CurrentRedGemCount,
    "blue" => state.CurrentBlueGemCount,
    "purple" => state.CurrentPurpleGemCount,
    CoreShards.Currency => state.CurrentCoreShardCount,
    _ => 0,
  };

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
    // A bought weapon blueprint waits in inventory; fit it as a player drags it to a hardpoint.
    if (Weaponry.BlueprintWeapon(button.Data.ShortName) is { } weapon && UpgradeManager.Instance.Weapons.TryAutoFit(weapon))
      CaptureSession.Log("fit", weapon.ToString());
    return true;
  }

  // True while the run saves up for the warp drive repair (or has just bought it).
  private static bool SaveForWarp(SceneAutoplay plan, GameState state)
  {
    if (plan.WarpMinutes <= 0 || UpgradeManager.CurrentUpgrades.UpgradeButtons.GetValueOrDefault(WarpRepair) is not { } repair
      || !Purchasable(repair))
      return false;
    ulong price = Price(repair);
    if (state.CurrentRedGemCount >= price) return Buy(state, repair);
    double perMinute = state.Income.PerMinute;
    return perMinute > 0 && (price - state.CurrentRedGemCount) / perMinute <= plan.WarpMinutes;
  }

  private static void StartOrder()
  {
    order = profile == null ? new() : profile.Runs[Math.Min(extractions, profile.Runs.Count - 1)].ToList();
    savingFor = null;
  }

  private static UpgradeButton Find(string id)
  {
    var trees = UpgradeManager.CurrentUpgrades;
    return trees.UpgradeButtons.GetValueOrDefault(id) ?? trees.UpgradeButtonsAbilities.GetValueOrDefault(id);
  }

  // Buys down the profile's order for this run. False once the order no longer steers the
  // run (used up, only locked entries left, or patience ran out), so Shop runs as well.
  private static bool ShopProfile(SceneAutoplay plan, GameState state, double t)
  {
    if (order.Count == 0) return false;
    bool waiting = false;
    var refused = new HashSet<string>();
    for (int i = 0, guard = 0; i < order.Count && guard < 2000; guard++)
    {
      var (id, level) = order[i];
      ulong price, held;
      bool red;
      if (id == PowerCell)
      {
        if (state.AbilityPointsPurchased >= (ulong)level || state.NextAbilityPointPrice is not ulong cell)
        {
          order.RemoveAt(i);
          continue;
        }
        if (!UpgradeManager.Instance.UGM.ShipSystemsUnlocked) { i++; continue; }
        price = cell;
        held = state.CurrentRedGemCount;
        red = true;
        if (held >= price)
        {
          if (!state.TryBuyAbilityPoint()) { i++; continue; }
          CaptureSession.Log("buy", PowerCell);
          continue;
        }
      }
      else
      {
        var button = Find(id);
        if (button == null || button.CurrentLevel >= level || button.CurrentLevel >= button.Data.LevelInfo.Count)
        {
          order.RemoveAt(i);
          continue;
        }
        if (!Purchasable(button) || refused.Contains(id)) { i++; continue; }
        string currency = button.Data.UpgradeDefinition.Currency;
        price = Price(button);
        held = Wallet(state, currency);
        red = currency == "red";
        if (held >= price)
        {
          if (!Buy(state, button)) refused.Add(id);
          continue;
        }
      }
      // Only gems are saved up for; shards and system points come on their own schedule.
      if (red)
      {
        if (savingFor != id) { savingFor = id; savingSince = t; }
        if (t - savingSince < plan.Patience) return true;
        waiting = true;
      }
      i++;
    }
    // Still steering while a gem purchase it can make is left; shard and system entries alone
    // must not stop the rest of the shopping.
    return !waiting && order.Any(e => e.Id == PowerCell ? UpgradeManager.Instance.UGM.ShipSystemsUnlocked
      : Find(e.Id) is { } b && b.Data.UpgradeDefinition.Currency == "red" && Purchasable(b));
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
        CaptureSession.Log("buy", PowerCell);
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

  // The highest open tier first (its Gem Lore is worth the most), leaving out the playstyle
  // talents presets also skip. Within a tier the profile's talents come first, in the order
  // the player learned them, then left to right. (A demo recording only reaches two tiers, so
  // its order can't decide between tiers.)
  private static void LearnTalents(GameState state)
  {
    var tree = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    while (state.CurrentPurpleGemCount > 0)
    {
      string next = PrestigeTalentLayout.Tiers.Reverse()
        .SelectMany(tier => (profile?.Talents.Where(tier.Talents.Contains) ?? []).Concat(tier.Talents))
        .FirstOrDefault(id => !PrestigeTalentLayout.PlaystyleTalents.Contains(id) && tree.TryGetValue(id, out var talent)
          && talent.CurrentLevel == 0 && PrestigeTalentLayout.CanLearn(tree, id));
      if (next == null || !Buy(state, tree[next])) break;
      CaptureSession.Log("talent", next);
    }
  }
}
#endif
