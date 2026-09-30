using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

internal static class ManualAbilityChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var abilities = new ManualFleetAbilities();
    var effects = new List<int>();
    Check(Enumerable.Range(0, 5).All(abilities.IsReady), "All five commands should start ready.");
    Check(!abilities.TryActivate(-1, effects.Add) && !abilities.TryActivate(5, effects.Add), "Invalid slots must be ignored.");
    Check(abilities.TryActivate(0, effects.Add), "Overdrive should activate manually.");
    Check(abilities.SpeedMultiplier == 2f && abilities.FreeFuel, "Overdrive must boost speed and suspend fuel use.");
    Check(!abilities.TryActivate(0, effects.Add), "An active command cannot be retriggered.");
    abilities.Update(10f);
    Check(abilities.SpeedMultiplier == 1f && !abilities.FreeFuel, "Overdrive must expire exactly at its duration.");
    Check(abilities.RemainingCooldown(0) == 35f, "Cooldown should start when the command is cast.");
    Check(abilities.TryActivate(1, effects.Add) && abilities.TryActivate(2, effects.Add), "Different buffs must overlap.");
    Check(abilities.RangeMultiplier == 2f && abilities.DeliveryMultiplier == 2f, "Overlapping buffs must be independent.");
    Check(abilities.TryActivate(3, effects.Add) && abilities.TryActivate(4, effects.Add), "Instant commands should activate.");
    Check(effects.SequenceEqual(new[] { 3, 4 }), "Instant effects must run exactly once per activation.");
    Check(!abilities.TryActivate(3, effects.Add), "An instant command cannot bypass its cooldown.");
    abilities.Update(1000f);
    Check(Enumerable.Range(0, 5).All(abilities.IsReady), "Long frames must finish every timer without automatic casts.");
    Check(effects.Count == 2, "Ready commands must never cast automatically.");

    _ = new UpgradeManager();
    var game = (Microsoft.Xna.Framework.Game)System.Runtime.CompilerServices.RuntimeHelpers
      .GetUninitializedObject(typeof(Microsoft.Xna.Framework.Game));
    var screen = new UntitledGemGameGameScreen(game);
    var previousScreen = UntitledGemGameGameScreen.Instance;
    UntitledGemGameGameScreen.Instance = screen;
    try
    {
      var ship = new Harvester { Type = Harvester.HarvesterType.Harvester };
      var home = new Harvester { Type = Harvester.HarvesterType.HomeBase };
      float speed = BaseStats.GetHarvesterSpeed(ship);
      float range = BaseStats.GetHarvesterCollectionRange(ship);
      float homeRange = BaseStats.GetHarvesterCollectionRange(home);
      ulong value = BaseStats.GetHarvesterDeliveryValue(ship, 100);
      screen.ManualAbilities.TryActivate(0, effects.Add);
      screen.ManualAbilities.TryActivate(1, effects.Add);
      screen.ManualAbilities.TryActivate(2, effects.Add);
      Check(BaseStats.GetHarvesterSpeed(ship) == speed * 2f, "Fleet movement must use Overdrive.");
      Check(BaseStats.GetHarvesterCollectionRange(ship) == range * 2f, "Fleet collection must use Wide Sweep.");
      Check(BaseStats.GetHarvesterCollectionRange(home) == homeRange, "Wide Sweep should affect the fleet, not home.");
      Check(BaseStats.GetHarvesterDeliveryValue(ship, 100) == value * 2, "Fleet deliveries must use Double Yield.");
      screen.ManualAbilities.Reset();
      Check(BaseStats.GetHarvesterSpeed(ship) == speed && BaseStats.GetHarvesterCollectionRange(ship) == range
        && BaseStats.GetHarvesterDeliveryValue(ship, 100) == value, "Clearing transient effects must restore fleet stats.");
    }
    finally { UntitledGemGameGameScreen.Instance = previousScreen; }
    Console.WriteLine("Manual ability checks passed: activation, overlapping buffs, cooldowns, expiry, instant effects and fleet stats.");
  }
}
