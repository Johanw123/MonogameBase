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
  // Damage the planet's shell has taken this run (PlanetShell); at its health it is gone.
  public double ShellDamage;
  public bool ShellBroken => PlanetShell.Broken(ShellDamage);
  // The planet's damage by source this run, for the HUD's Damage panel.
  public PlanetDamageMeter Damage { get; } = new();
  public ulong RedGemsEarnedThisRun { get; private set; }
  // Gem income over the last minute: sustained, it earns prestige points (PrestigeProgression).
  public RollingMinute Income { get; } = new();
  // Every prestige point ever earned, this run's included; each one raises the next one's bar.
  public ulong PrestigePointsEarned { get; private set; }
  // Earned this run and paid at extraction.
  public ulong PendingPrestigePoints { get; private set; }
  // The share of the next point's bar remembered from earlier loops, 0 to 1.
  public double PrestigeEcho { get; private set; }
  // This run's best share of the next point's bar from income alone, 0 to 1.
  public double BestPrestigeProgress { get; private set; }
  public double PrestigeProgress => PrestigeProgression.Progress(Income.PerMinute, PrestigePointsEarned);
  public double EchoAfterExtraction => PrestigeProgression.BankEcho(PrestigeEcho, BestPrestigeProgress);
  public ulong AbilityPointsPurchased { get; private set; }
  public ulong? NextAbilityPointPrice => AbilityPointProgression.GetPrice(AbilityPointsPurchased);

  public void Restore(ulong red, ulong blue, ulong purple, ulong earnedThisRun, ulong abilityPointsPurchased = 0)
  {
    CurrentRedGemCount = red;
    CurrentBlueGemCount = blue;
    CurrentPurpleGemCount = purple;
    RedGemsEarnedThisRun = earnedThisRun;
    AbilityPointsPurchased = abilityPointsPurchased;
  }

  public void RestorePrestige(ulong earned, ulong pending, double echo, double bestProgress)
  {
    PrestigePointsEarned = Math.Max(earned, pending);
    PendingPrestigePoints = pending;
    PrestigeEcho = PrestigeProgression.SanitizeEcho(echo);
    BestPrestigeProgress = PrestigeProgression.SanitizeEcho(bestProgress);
  }

  public void RestoreCoreShards(ulong coreShards, int coreFractures, double shellDamage)
  {
    CurrentCoreShardCount = coreShards;
    CoreFractures = Math.Max(0, coreFractures);
    // A fractured core has lost its shell already.
    ShellDamage = CoreFractures > 0 ? PlanetShell.Health : PlanetShell.Sanitize(shellDamage);
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

  // Earns a point each time the echo and the last minute's income fill the bar, and
  // returns how many. The echo goes into the first one; each after needs more income.
  public int UpdatePrestigeProgress()
  {
    int earned = 0;
    double progress = PrestigeProgress;
    while (PrestigeEcho + progress >= 1 && PrestigePointsEarned < ulong.MaxValue)
    {
      PrestigePointsEarned++;
      PendingPrestigePoints = PrestigeProgression.AddSaturating(PendingPrestigePoints, 1);
      PrestigeEcho = 0;
      BestPrestigeProgress = 0;
      earned++;
      progress = PrestigeProgress;
    }
    BestPrestigeProgress = Math.Max(BestPrestigeProgress, Math.Min(progress, 1));
    return earned;
  }

  public bool TryRefundAbilityPoints(ulong points)
  {
    if (points == 0 || points > ulong.MaxValue - CurrentBlueGemCount)
      return false;
    CurrentBlueGemCount += points;
    return true;
  }

  public bool CanBuyAbilityPoint => NextAbilityPointPrice is ulong price && CurrentRedGemCount >= price
    && CurrentBlueGemCount < ulong.MaxValue;

  public bool TryBuyAbilityPoint()
  {
    if (!CanBuyAbilityPoint) return false;
    ulong price = NextAbilityPointPrice.Value;
    CurrentRedGemCount -= price;
    CurrentBlueGemCount++;
    AbilityPointsPurchased++;
    return true;
  }

  public void EarnRedGems(ulong amount)
  {
    CurrentRedGemCount = PrestigeProgression.AddSaturating(CurrentRedGemCount, amount);
    RedGemsEarnedThisRun = PrestigeProgression.AddSaturating(RedGemsEarnedThisRun, amount);
    Income.Record(amount);
  }

  // Pays this run's points and banks its echo; the screen stops earning points once
  // the extraction starts, so both are what the player saw when they extracted.
  public void CompletePrestige()
  {
    Modules.EndHarvesting();
    Modules.ResetRun(Random.Shared);
    CurrentPurpleGemCount = PrestigeProgression.AddSaturating(
      CurrentPurpleGemCount, PendingPrestigePoints);
    PrestigeEcho = EchoAfterExtraction;
    PendingPrestigePoints = 0;
    BestPrestigeProgress = 0;
    Income.Reset();
    CurrentRedGemCount = 0;
    RedGemsEarnedThisRun = 0;
    // Power cells and the ship system talents they bought last one run.
    CurrentBlueGemCount = 0;
    AbilityPointsPurchased = 0;
    CoreDrillTunnels = 0;
    CurrentCoreShardCount = 0;
    CoreFractures = 0;
    ShellDamage = 0;
    Damage.Reset();
    CoreExtractions = PrestigeProgression.AddSaturating(CoreExtractions, 1);
  }
}
