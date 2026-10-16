using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Screens;

internal static class WorldPresentationChecks
{
  public static void Run()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    var start = PlanetPresentation.HorizontalOffsets(3.5f, 93f, 29.2f);
    Check(start == (-240f, 190f), "The first run must retain its original composition");
    float previous = 0;
    foreach (float zoom in new[] { 3.5f, 3f, 2.5f, 2f, 1.5f, 1f })
    {
      var offsets = PlanetPresentation.HorizontalOffsets(zoom, 93f, 29.2f);
      float gap = (offsets.Planet - offsets.Ship - 93f - 29.2f) * zoom;
      Check(gap >= previous, "Zooming out must preserve visible space between the bodies");
      previous = gap;
      var grown = PlanetPresentation.HorizontalOffsets(zoom, 145f, 180f);
      Check(Math.Abs(grown.Planet - grown.Ship - 145f - 180f
        - (offsets.Planet - offsets.Ship - 93f - 29.2f)) < 0.001f,
        "Planet and ship growth must preserve the gap between their edges");
      Check(PlanetPresentation.ShardSize(zoom) * zoom >= 128f,
        "Core Shards must stay at least 128 logical pixels tall when zoomed out");
    }

    var grid = new GemSpatialIndex(4, 10);
    int touching = grid.AddGem(1, 15, 0, 1, collectionRadius: 3);
    int outside = grid.AddGem(2, 15.01f, 0, 1, collectionRadius: 3);
    int diagonal = grid.AddGem(3, 10, 10, 1, collectionRadius: 3);
    grid.PrepareQueries();
    var collected = new List<int>();
    foreach (int index in grid.QueryCollection(0, 0, 12)) collected.Add(index);
    Check(collected.Contains(touching) && collected.Contains(diagonal) && !collected.Contains(outside),
      "Circular collection must include touching gem radii and exclude gems beyond that reach");

    for (int level = 0; level <= 5; level++)
    {
      float size = PlanetPresentation.PlanetSize(0, level);
      Check(Math.Abs(size - (1f + 0.2f * level)) < 0.0001f,
        "Each Expand Space level must increase the planet's base size by 20%");
      Check(Math.Abs(PlanetPresentation.PlanetSize(1, level) / size - 1.07f) < 0.0001f,
        "Core-crack growth must stack on top of every Expand Space size");
      Check(Math.Abs(PlanetPresentation.PlanetSize(100, level)
        - size * CoreFracture.PlanetSize(CoreFracture.MaxGrowthFractures)) < 0.0001f,
        "Core-crack growth must retain its cap at every Expand Space level");
    }
    Check(PlanetPresentation.PlanetSize(0, -1) == 1f,
      "Negative Expand Space levels must not shrink the starting planet");

    // Run the actual planet projection without creating a graphics window. No
    // weapon update is involved: marks must keep turning even when weapons pause.
    var screen = (UntitledGemGameGameScreen)RuntimeHelpers.GetUninitializedObject(typeof(UntitledGemGameGameScreen));
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var age = typeof(UntitledGemGameGameScreen).GetField("planetAge", flags)!;
    var toNormal = typeof(UntitledGemGameGameScreen).GetMethod("WorldToPlanetNormal", flags)!;
    var toDrawn = typeof(UntitledGemGameGameScreen).GetMethod("PlanetNormalToDrawn", flags)!;
    var visualSize = typeof(UntitledGemGameGameScreen).GetField("planetVisualSize", flags)!;
    var previousPlanet = UntitledGemGameGameScreen.PlanetPos;
    try
    {
      UntitledGemGameGameScreen.PlanetPos = Vector2.Zero;
      visualSize.SetValue(screen, 1f);
      age.SetValue(screen, 0f);
      var normal = (Vector3)toNormal.Invoke(screen, new object[] { Vector2.Zero })!;
      Vector2 Project(float time, Vector2 center)
      {
        age.SetValue(screen, time);
        return (Vector2)toDrawn.Invoke(screen, new object[] { center, 3f, normal })!;
      }
      Check(Project(0, Vector2.Zero) == Project(0.139f, Vector2.Zero),
        "Marks must stay still between planet animation frames");
      float angle = 19f * MathHelper.TwoPi / 77f;
      Check(Vector2.Distance(Project(19f * 0.14f + 0.001f, Vector2.Zero),
        new Vector2(MathF.Sin(angle) * 93f, 0)) < 0.001f,
        "Marks must follow the prerendered rotation and radius even without a weapon update");
      var shake = new Vector2(21, -13);
      Check(Vector2.Distance(Project(3f, shake) - Project(3f, Vector2.Zero), shake) < 0.001f,
        "Marks must inherit the planet's full shake offset");
      Check(Vector2.Distance(Project(77f * 0.14f, Vector2.Zero), Vector2.Zero) < 0.001f,
        "Marks must wrap exactly with the planet animation");
      age.SetValue(screen, 1.23f);
      visualSize.SetValue(screen, 1.3f);
      typeof(UntitledGemGameGameScreen).GetField("planetHitPulse", flags)!.SetValue(screen, 0.7f);
      typeof(UntitledGemGameGameScreen).GetField("planetShake", flags)!.SetValue(screen, 0.8f);
      var toWorld = typeof(UntitledGemGameGameScreen).GetMethod("PlanetNormalToWorld", flags)!;
      var hit = new Vector2(-30, 20);
      var hitNormal = (Vector3)toNormal.Invoke(screen, new object[] { hit })!;
      Check(Vector2.Distance(hit, (Vector2)toWorld.Invoke(screen, new object[] { hitNormal })!) < 0.001f,
        "New scars must land at the visible impact even during shake, swelling and hit pulses");
    }
    finally { UntitledGemGameGameScreen.PlanetPos = previousPlanet; }
    Console.WriteLine("World presentation checks passed: surface rotation, shake, spacing, shard visibility, planet growth and circular collection.");
  }
}
