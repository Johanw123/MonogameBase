using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using System.Reflection;
using UntitledGemGame;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

internal static class ChainLifetimeChecks
{
  private sealed class FleetProbe : HarvesterCollectionSystem
  {
    public FleetProbe() : base(null, null) { }
    public override void Update(GameTime gameTime) { }
  }

  public static void Run()
  {
    var fleet = new FleetProbe();
    using var world = new WorldBuilder().AddSystem(fleet).Build();
    var entity = world.CreateEntity();
    var transform = new Transform2(new Vector2(500, 200));
    entity.Attach(transform);
    var gem = new Gem();
    gem.Initialize(entity, 18, 1);
    gem.GridIndex = fleet.flatSpatialHash.AddGem(entity.Id, 500, 200, 1);
    entity.Attach(gem);
    var frame = new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1));
    world.Update(frame);
    var ability = new ChainLightningAbility();
    var addChain = typeof(ChainLightningAbility).GetMethod("AddChain", BindingFlags.Instance | BindingFlags.NonPublic)!;
    addChain.Invoke(ability, new object[] { gem.GridIndex, Vector2.Zero, false, Color.Yellow });
    ability.Update(frame);
    if (transform.Position == new Vector2(500, 200)) throw new Exception("A live chain must still move its gem");

    // Match collection cleanup: the component is reset before ECS processes Destroy.
    int retiredIndex = gem.GridIndex;
    fleet.flatSpatialHash.RecycleIndex(retiredIndex);
    entity.Destroy();
    gem.Reset();
    ability.Update(frame);
    if (ChainLightningAbility.TargetLines.ContainsKey(entity.Id))
      throw new Exception("A chain must retire its line when its gem is returned to the pool");
    ability.Deactivate();
    world.Update(frame);

    Entity Respawn(Vector2 position)
    {
      var spawned = world.CreateEntity();
      spawned.Attach(new Transform2(position));
      gem.Initialize(spawned, 18, 1);
      gem.GridIndex = fleet.flatSpatialHash.AddGem(spawned.Id, position.X, position.Y, 1);
      spawned.Attach(gem);
      world.Update(frame);
      return spawned;
    }
    void Retire(Entity retired)
    {
      fleet.flatSpatialHash.RecycleIndex(gem.GridIndex);
      retired.Destroy();
      gem.Reset();
      world.Update(frame);
    }

    var second = Respawn(new Vector2(600, 200));
    addChain.Invoke(ability, new object[] { gem.GridIndex, Vector2.Zero, false, Color.Yellow });
    Retire(second); // Leave the old chain pending while its component and IDs are reused.
    var newStart = new Vector2(800, 300);
    var replacement = Respawn(newStart);
    if (replacement.Id != second.Id || gem.GridIndex != retiredIndex)
      throw new Exception("The regression fixture must exercise entity and spatial slot reuse");
    var newTarget = new Vector2(1000, 500);
    addChain.Invoke(ability, new object[] { gem.GridIndex, newTarget, false, Color.Blue });
    var replacementLine = ChainLightningAbility.TargetLines[replacement.Id];
    ability.Update(frame);
    float remaining = 1f - 0.1f;
    float easedProgress = 1f - remaining * remaining * remaining * remaining * remaining;
    if (replacement.Get<Transform2>().Position != Vector2.Lerp(newStart, newTarget, easedProgress)
      || fleet.flatSpatialHash.Gems[gem.GridIndex].ClaimState != 1
      || fleet.flatSpatialHash.AvailableCount != 0
      || !ChainLightningAbility.TargetLines.TryGetValue(replacement.Id, out var activeLine)
      || !ReferenceEquals(activeLine, replacementLine))
      throw new Exception("An expired chain must not move, release or remove the line of a replacement gem");
    ability.Deactivate();
    if (fleet.flatSpatialHash.Gems[gem.GridIndex].ClaimState != 0 || fleet.flatSpatialHash.AvailableCount != 1)
      throw new Exception("Cancelling a live chain must restore its gem to queries");

    var manager = new UpgradeManager();
    if (ability.DurationTimeMax != 0)
      throw new Exception("Chain recharge must not wait for an active duration");

    // Cast again before the previous pull finishes: existing claims and lines survive.
    manager.UGA.ChainMagnetizerCount = 1;
    ability.Activate();
    var firstLine = ChainLightningAbility.TargetLines[replacement.Id];
    ability.Update(frame);
    var overlapping = world.CreateEntity();
    overlapping.Attach(new Transform2(new Vector2(700, 400)));
    var overlappingGem = new Gem();
    overlappingGem.Initialize(overlapping, 18, 1);
    overlappingGem.GridIndex = fleet.flatSpatialHash.AddGem(overlapping.Id, 700, 400, 1);
    overlapping.Attach(overlappingGem);
    world.Update(frame);
    ability.Activate();
    if (ChainLightningAbility.TargetLines.Count != 2
      || !ReferenceEquals(firstLine, ChainLightningAbility.TargetLines[replacement.Id]))
      throw new Exception("Overlapping casts must preserve earlier pulls");
    ability.Update(new GameTime(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.91)));
    if (ChainLightningAbility.TargetLines.ContainsKey(replacement.Id)
      || !ChainLightningAbility.TargetLines.ContainsKey(overlapping.Id))
      throw new Exception("Overlapping pulls must finish independently");
    ability.Update(frame);
    if (ChainLightningAbility.TargetLines.Count != 0 || fleet.flatSpatialHash.AvailableCount != 2)
      throw new Exception("All overlapping pulls must release their claims and lines");
    manager.UGA.ChainMagnetizerAftershock = true;
    manager.UGA.ChainMagnetizerAftershockChance = 100;
    ability.Activate();
    ability.Update(new GameTime(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(0.3)));
    if (ability.IsActive || ChainLightningAbility.TargetLines.Count != 2)
      throw new Exception("Aftershocks must fire while the durationless ability recharges");
    ability.Update(new GameTime(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1)));
    if (ChainLightningAbility.TargetLines.Count != 0 || fleet.flatSpatialHash.AvailableCount != 2)
      throw new Exception("Aftershock completion must leave no hanging lines or claims");
    fleet.flatSpatialHash.RecycleIndex(overlappingGem.GridIndex);
    overlapping.Destroy();
    overlappingGem.Reset();
    world.Update(frame);

    manager.UGA.ChainMagnetizerAftershock = true;
    manager.UGA.ChainMagnetizerAftershockChance = 100;
    addChain.Invoke(ability, new object[] { gem.GridIndex, newTarget, true, Color.Blue });
    ability.DurationTime = 1000;
    ability.Cancel();
    ability.Update(new GameTime(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.3)));
    if (ability.IsActive || ability.CooldownTime != ability.MaxCooldownTime
      || fleet.flatSpatialHash.AvailableCount != 1 || ChainLightningAbility.TargetLines.ContainsKey(replacement.Id))
      throw new Exception("Unequipping must reset timers, release the pull and cancel delayed aftershocks");

    // A leftover pull must also be released when the active timer has already expired.
    addChain.Invoke(ability, new object[] { gem.GridIndex, newTarget, true, Color.Blue });
    ability.DurationTime = 0;
    ability.Cancel();
    ability.Cancel();
    ability.Update(new GameTime(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.3)));
    if (fleet.flatSpatialHash.AvailableCount != 1 || fleet.flatSpatialHash.Gems[gem.GridIndex].ClaimState != 0)
      throw new Exception("Reset cancellation must release expired abilities and be safe to repeat");

    addChain.Invoke(ability, new object[] { gem.GridIndex, newTarget, false, Color.Blue });
    ability.Update(new GameTime(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
    if (replacement.Get<Transform2>().Position != newTarget || fleet.flatSpatialHash.AvailableCount != 1
      || ChainLightningAbility.TargetLines.ContainsKey(replacement.Id))
      throw new Exception("A completed chain must release its gem and remove its line");

    addChain.Invoke(ability, new object[] { gem.GridIndex, Vector2.Zero, false, Color.Yellow });
    Retire(replacement);
    var last = Respawn(new Vector2(900, 300));
    fleet.flatSpatialHash.TryClaim(gem.GridIndex);
    fleet.flatSpatialHash.RemoveFromQueries(gem.GridIndex);
    ability.Deactivate();
    if (fleet.flatSpatialHash.Gems[gem.GridIndex].ClaimState != 1 || fleet.flatSpatialHash.AvailableCount != 0)
      throw new Exception("Cancelling an expired chain must not release another collector's replacement gem");
    var droneAbility = new DroneAbility();
    droneAbility.Activate();
    droneAbility.Cancel();
    // No EntityFactory exists here: a stale queued spawn would fail if cancellation did not suppress it.
    GUI.Shared.Helpers.TimerHelper.PumpEndOfFrameObjects();
    if (droneAbility.IsActive || droneAbility.CooldownTime != droneAbility.MaxCooldownTime)
      throw new Exception("Cancelled drone activation must clear timers and suppress its queued spawn");
    Retire(last);
    Console.WriteLine("Chain lifetime checks passed: pool reset, reused objects/IDs/slots, movement, completion and cancellation.");
  }
}
