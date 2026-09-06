public class GameState
{
  public ulong CurrentRedGemCount = 0;
  public ulong CurrentBlueGemCount = 0;
  public ulong CurrentPurpleGemCount = 0;
  public ulong RedGemsEarnedThisRun { get; private set; }

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
    CurrentPurpleGemCount = PrestigeProgression.AddSaturating(
      CurrentPurpleGemCount, purpleReward);
    CurrentRedGemCount = 0;
    RedGemsEarnedThisRun = 0;
  }
}
