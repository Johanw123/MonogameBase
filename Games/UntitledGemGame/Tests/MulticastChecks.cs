using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Entities;
using GUI.Shared.Helpers;

internal static class MulticastChecks
{
  // Exercise the real end-of-frame deployment queue without loading ship textures.
  private sealed class DroneProbe : DroneAbility
  {
    public readonly List<Harvester> Spawned = new();
    protected override void SpawnDrone(Vector2 position)
      => Spawned.Add(new Harvester { Type = Harvester.HarvesterType.Drone });
  }

  private sealed class TimingProbe : IHomeBaseAbility
  {
    public readonly List<double> CastTimes = new();
    private double elapsed;
    public override int DurationTimeMax => 0;
    public override void Activate() => CastTimes.Add(elapsed);
    public override void Deactivate() { }
    protected override void UpdateEffects(GameTime gameTime) => elapsed += gameTime.ElapsedGameTime.TotalSeconds;
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new Exception(message);
  }

  private static void CheckSchedulerTiming()
  {
    var largeFrame = new TimingProbe();
    largeFrame.ActivateWithMulticast(true, MulticastTable.MaxLevel, 0);
    largeFrame.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)));
    largeFrame.ActivateWithMulticast(true, MulticastTable.MaxLevel, 0);
    largeFrame.CooldownTime = 123;
    largeFrame.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.1)));
    double[] expected = { 0, 0.1, 0.25, 0.35, 0.5, 0.6, 0.75, 0.85, 1, 1.1 };
    Check(largeFrame.CastTimes.Count == expected.Length
      && largeFrame.CastTimes.Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 0.000001),
      "Long frames must dispatch interleaved multicast batches at their actual scheduled times");
    Check(largeFrame.CooldownTime == 123, "Delayed echoes must not restart cooldown");
    var smallFrames = new TimingProbe();
    smallFrames.ActivateWithMulticast(true, MulticastTable.MaxLevel, 0);
    for (int i = 0; i < 10; ++i) smallFrames.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.01)));
    smallFrames.ActivateWithMulticast(true, MulticastTable.MaxLevel, 0);
    for (int i = 0; i < 110; ++i) smallFrames.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.01)));
    Check(smallFrames.CastTimes.Count == expected.Length
      && smallFrames.CastTimes.Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 0.000001),
      "Multicast cadence must be independent of frame rate");
  }

  public static void Run()
  {
    var previousManager = UpgradeManager.Instance;
    var manager = new UpgradeManager();
    manager.UGA.IncreaseDroneCount = 4;
    manager.UGA.DroneFinalSweep = true;
    manager.UGA.DroneSweepEfficiency = 25;
    manager.UGA.DroneFission = true;
    manager.UGA.DroneRecharge = true;
    try
    {
      CheckSchedulerTiming();
      foreach (var (roll, casts) in new[] { (99d, 1), (70d, 2), (45d, 3), (25d, 4), (0d, 5) })
      {
        var ability = new DroneProbe();
        Check(ability.ActivateWithMulticast(true, MulticastTable.MaxLevel, roll) == casts
          && ability.Spawned.Count == 0, "Multicast must enqueue each drone cast once, without recursive rolls");
        // Normal expiry of the activation pulse must not cancel deferred deployment.
        ability.Deactivate();
        TimerHelper.PumpEndOfFrameObjects();
        Check(ability.Spawned.Count == 4, "Only the first drone batch may deploy immediately");
        for (int cast = 1; cast < casts; ++cast)
        {
          ability.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.24)));
          TimerHelper.PumpEndOfFrameObjects();
          Check(ability.Spawned.Count == cast * 4, "Drone echoes must not deploy before their interval");
          ability.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.01)));
          TimerHelper.PumpEndOfFrameObjects();
          Check(ability.Spawned.Count == (cast + 1) * 4, "Each quarter second must deploy one more drone batch");
        }
        Check(ability.Spawned.Count == casts * 4 && ability.Spawned.All(d => !d.IsDroneOffspring),
          "Every multicast drone batch must contain the full upgraded count of original drones");
        int fissions = 0;
        foreach (var drone in ability.Spawned)
        {
          drone.AdvanceDroneTimers(100f);
          Check(drone.TryBeginFinalSweep(Vector2.Zero) && !drone.TryBeginFinalSweep(Vector2.Zero),
            "Every multicast drone must own exactly one Final Sweep");
          drone.PickedUpGem(new Gem { BaseValue = 100 });
          Check(drone.CarryingGemBaseValue == 125 && drone.ReturningToHomebase,
            "Sweep Efficiency must apply to each drone without recharge reviving expired drones");
          drone.FinishFinalSweep();
          drone.MarkedForDestroy = true; // The delivery path sets this before fission.
          if (drone.TryConsumeDroneFission()) ++fissions;
          Check(!drone.TryConsumeDroneFission(), "Each original drone may fission only once");
        }
        Check(fissions == casts * 4, "Every original multicast drone must remain eligible for Fission");
        ability.ActivateWithMulticast(true, MulticastTable.MaxLevel, roll);
        ability.Cancel();
        ability.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(2)));
        TimerHelper.PumpEndOfFrameObjects();
        Check(ability.Spawned.Count == casts * 4 && ability.CooldownTime == ability.MaxCooldownTime,
          "Cancellation must suppress every pending multicast deployment and reset cooldown");
        ability.ActivateWithMulticast(false, MulticastTable.MaxLevel, 0);
        TimerHelper.PumpEndOfFrameObjects();
        Check(ability.Spawned.Count == casts * 4 + 4, "Disabled multicast must deploy exactly one batch");
      }
    }
    finally { UpgradeManager.Instance = previousManager; }
    Console.WriteLine("Multicast checks passed: one-roll dispatch, 1x–5x drone deployment, sweep, fission and cancellation.");
  }
}
