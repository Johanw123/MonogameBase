using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using UntitledGemGame.Entities;

internal static class CollectorScaleChecks
{
  public static void Run()
  {
    using var world = new WorldBuilder().Build();
    var entity = world.CreateEntity();
    var transform = new Transform2(Vector2.Zero);
    entity.Attach(transform);
    var collector = new Harvester { Entity = entity };

    void Check(Harvester.HarvesterType type, Vector2 scale, float expected)
    {
      collector.Type = type;
      transform.Scale = scale;
      float actual = BaseStats.GetHarvesterBaseCollectionRange(collector);
      if (MathF.Abs(actual - expected) > 0.0001f)
        throw new Exception($"{type} at scale {scale}: expected radius {expected}, got {actual}");
    }

    Check(Harvester.HarvesterType.Harvester, Vector2.One, 12f);
    Check(Harvester.HarvesterType.AdvancedHarvester, new Vector2(0.8f), 12f);
    Check(Harvester.HarvesterType.ExpertHarvester, new Vector2(0.8f), 12.8f);
    Check(Harvester.HarvesterType.UltimateHarvester, new Vector2(0.8f), 16.8f);
    Check(Harvester.HarvesterType.Drone, new Vector2(0.4f), 6.4f);
    Check(Harvester.HarvesterType.HomeBase, Vector2.One, 42.5f);
    Check(Harvester.HarvesterType.HomeBase, new Vector2(2f), 85f);
    Check(Harvester.HarvesterType.Harvester, new Vector2(0.5f, 2f), 22f);
    Check(Harvester.HarvesterType.Drone, new Vector2(-0.4f), 6.4f);
    Check(Harvester.HarvesterType.Harvester, Vector2.Zero, 0f);
    Console.WriteLine("Collector scale checks passed: all hulls, live resizing, per-axis scaling and flips.");
  }
}
