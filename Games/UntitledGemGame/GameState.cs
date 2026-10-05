using System;
using System.Collections.Generic;
using UntitledGemGame;

public class GameState
{
  public UntitledGemGame.ShipyardModules Modules { get; set; } = new();
  public UntitledGemGame.SignalProgression Signals { get; set; } = new();
  public ulong CurrentRedGemCount = 0;
  public ulong CurrentBlueGemCount = 0;
  public ulong CurrentPurpleGemCount = 0;
  public ulong CurrentCoreShardCount = 0;
  // Completed core extractions; the first one opens the talent tree and its tiers.
  public ulong CoreExtractions = 0;
  // Core Drill Hollow World tunnels dug this run.
  public int CoreDrillTunnels;
  // Core fractures this run (CoreFracture); each raises the damage the next one needs.
  public int CoreFractures;
  public ulong RedGemsEarnedThisRun { get; private set; }
  public double PeakGemsPerMinute { get; private set; }
  public ulong AbilityPointsPurchased { get; private set; }
  public ulong? NextAbilityPointPrice => AbilityPointProgression.GetPrice(AbilityPointsPurchased);

  public void Restore(ulong red, ulong blue, ulong purple, ulong earnedThisRun, ulong abilityPointsPurchased = 0, double peakGemsPerMinute = 0)
  {
    CurrentRedGemCount = red;
    CurrentBlueGemCount = blue;
    CurrentPurpleGemCount = purple;
    RedGemsEarnedThisRun = earnedThisRun;
    AbilityPointsPurchased = abilityPointsPurchased;
    PeakGemsPerMinute = peakGemsPerMinute;
  }

  public void RestoreCoreShards(ulong coreShards, int coreFractures)
  {
    CurrentCoreShardCount = coreShards;
    CoreFractures = Math.Max(0, coreFractures);
  }

  public ulong GetBalance(string currency) => currency switch
  {
    "red" => CurrentRedGemCount,
    "blue" => CurrentBlueGemCount,
    "purple" => CurrentPurpleGemCount,
    CoreShards.Currency => CurrentCoreShardCount,
    _ => 0
  };

  // Callers check the balance first; an unknown currency spends nothing.
  public void Spend(string currency, ulong amount)
  {
    switch (currency)
    {
      case "red": CurrentRedGemCount -= amount; break;
      case "blue": CurrentBlueGemCount -= amount; break;
      case "purple": CurrentPurpleGemCount -= amount; break;
      case CoreShards.Currency: CurrentCoreShardCount -= amount; break;
    }
  }

  public void RecordIncome(double gemsPerMinute)
  {
    if (double.IsFinite(gemsPerMinute) && gemsPerMinute > PeakGemsPerMinute)
      PeakGemsPerMinute = gemsPerMinute;
  }

  public bool TryRefundAbilityPoints(ulong points)
  {
    if (points == 0 || points > ulong.MaxValue - CurrentBlueGemCount)
      return false;
    CurrentBlueGemCount += points;
    return true;
  }

  public bool TryBuyAbilityPoint()
  {
    if (NextAbilityPointPrice is not ulong price || CurrentRedGemCount < price
      || CurrentBlueGemCount == ulong.MaxValue)
      return false;

    CurrentRedGemCount -= price;
    CurrentBlueGemCount++;
    AbilityPointsPurchased++;
    return true;
  }

  public void EarnRedGems(ulong amount)
  {
    CurrentRedGemCount = PrestigeProgression.AddSaturating(CurrentRedGemCount, amount);
    RedGemsEarnedThisRun = PrestigeProgression.AddSaturating(RedGemsEarnedThisRun, amount);
  }

  // Pending deliveries have already been earned; only their HUD counting is delayed.
  public ulong GetPrestigeReward(ulong pendingDeliveries)
    => PrestigeProgression.GetReward(
      PrestigeProgression.AddSaturating(RedGemsEarnedThisRun, pendingDeliveries));

  public void CompletePrestige(ulong purpleReward)
  {
    Modules.EndHarvesting();
    Modules.ResetRun(Random.Shared);
    CurrentPurpleGemCount = PrestigeProgression.AddSaturating(
      CurrentPurpleGemCount, purpleReward);
    CurrentRedGemCount = 0;
    RedGemsEarnedThisRun = 0;
    PeakGemsPerMinute = 0;
    // Power cells and the ship system talents they bought last one run.
    CurrentBlueGemCount = 0;
    AbilityPointsPurchased = 0;
    CoreDrillTunnels = 0;
    CurrentCoreShardCount = 0;
    CoreFractures = 0;
    CoreExtractions = PrestigeProgression.AddSaturating(CoreExtractions, 1);
  }
}
