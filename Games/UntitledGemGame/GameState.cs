public class GameState
{
  public UntitledGemGame.ShipyardModules Modules { get; set; } = new();
  public UntitledGemGame.SignalProgression Signals { get; set; } = new();
  public ulong CurrentRedGemCount = 0;
  public ulong CurrentBlueGemCount = 0;
  public ulong CurrentPurpleGemCount = 0;
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

  public void RecordIncome(double gemsPerMinute)
  {
    if (double.IsFinite(gemsPerMinute) && gemsPerMinute > PeakGemsPerMinute)
      PeakGemsPerMinute = gemsPerMinute;
  }

  public const ulong MinimumRespecCostPerPoint = 10;
  public const double RespecPeakIncomeFraction = 0.1;

  // Six seconds of peak income per point, with an early-game floor.
  public ulong? GetRespecCost(ulong points)
  {
    double cost = System.Math.Max(MinimumRespecCostPerPoint, System.Math.Ceiling(PeakGemsPerMinute * RespecPeakIncomeFraction)) * points;
    return double.IsFinite(cost) && cost < (double)ulong.MaxValue ? (ulong)cost : null;
  }

  public bool TryRefundAbilityPoints(ulong points)
  {
    if (points == 0 || GetRespecCost(points) is not ulong cost
      || CurrentRedGemCount < cost || points > ulong.MaxValue - CurrentBlueGemCount)
      return false;
    CurrentRedGemCount -= cost;
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
    CurrentPurpleGemCount = PrestigeProgression.AddSaturating(
      CurrentPurpleGemCount, purpleReward);
    CurrentRedGemCount = 0;
    RedGemsEarnedThisRun = 0;
    PeakGemsPerMinute = 0;
  }
}
