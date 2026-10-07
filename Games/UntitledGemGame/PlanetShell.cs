using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Each run's planet starts sealed in a dark shell (GameScreen.PlanetShell.cs), so at
// first the player cannot tell what they are mining. The shell has fixed health: all
// weapon damage goes into it, and glowing cracks spread across it as it wears down.
// Once it has taken Health damage it shatters and the planet beneath shows. It
// releases no Core Shard; damage only starts counting toward core fractures
// (CoreFracture) once it is gone. It comes back at extraction.
public static class PlanetShell
{
  // One step below the first core fracture, but as total damage rather than per minute.
  public const double Health = CoreFracture.FirstThreshold / CoreFracture.ThresholdGrowth;

  public static bool Broken(double damage) => damage >= Health;

  public static double Sanitize(double damage) => double.IsFinite(damage) ? Math.Clamp(damage, 0, Health) : 0;

  // How far the cracks have spread: 0 untouched, 1 about to shatter.
  public static float Wear(double damage) => (float)(Sanitize(damage) / Health);

  // ---- Layout, shared with PlanetShell.fx ----

  // The shell breaks into plates: the nearest plate centre owns each point of the
  // sphere, with a little warp so the seams run jagged. Cracks open along the seams,
  // spreading from origins spread over the shell that start at increasing wear
  // (CrackWear). Each spreads fast at first and slows, so the share of seams open
  // keeps pace with the wear: about half at half health, nearly all just before it bursts.
  public const int PlateCount = 14;
  public const int CrackOriginCount = 12;
  public const float SeamWarp = 0.045f;
  public const float SeamWarpFrequency = 11f;
  // A crack spreads CrackReach * sqrt(wear since it started) radians along the seams.
  public const float CrackReach = 0.9f;
  // Past every seam's crack wear: the whole network shows as the shell strains to burst.
  public const float FullWear = 2.2f;
  // The first crack opens with the first hit, the last one at LastCrackStart.
  private const float FirstCrackStart = -0.03f;
  private const float LastCrackStart = 0.65f;

  public sealed class Layout
  {
    public Vector3[] Plates;
    // xyz: where a crack starts, on a seam; w: the wear at which it starts.
    public Vector4[] CrackOrigins;
  }

  // Plates spread evenly over the sphere (a jittered Fibonacci spiral). Each run's
  // shell gets its own seed, so it breaks differently.
  public static Layout CreateLayout(int seed)
  {
    var random = new Random(seed);
    var plates = new Vector3[PlateCount];
    float golden = MathF.PI * (3f - MathF.Sqrt(5f));
    for (int i = 0; i < PlateCount; i++)
    {
      float y = 1f - 2f * (i + 0.5f) / PlateCount;
      float ring = MathF.Sqrt(1f - y * y);
      var jitter = new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle()) - new Vector3(0.5f);
      plates[i] = Vector3.Normalize(new Vector3(MathF.Cos(i * golden) * ring, y, MathF.Sin(i * golden) * ring)
        + jitter * 0.44f);
    }
    // Cracks start on seams: halfway between two plates that border each other. Each
    // next one starts as far as possible from those before, so they cover the shell evenly.
    var seams = new List<Vector3>();
    for (int a = 0; a < PlateCount; a++)
      for (int b = a + 1; b < PlateCount; b++)
      {
        var middle = Vector3.Normalize(plates[a] + plates[b]);
        float shared = Vector3.Dot(middle, plates[a]);
        bool bordering = true;
        for (int c = 0; c < PlateCount && bordering; c++)
          bordering = c == a || c == b || Vector3.Dot(middle, plates[c]) < shared;
        if (bordering) seams.Add(middle);
      }
    var origins = new Vector4[CrackOriginCount];
    var taken = new List<Vector3> { seams[random.Next(seams.Count)] };
    while (taken.Count < CrackOriginCount)
    {
      var farthest = seams[0];
      float farthestDistance = float.MinValue;
      foreach (var seam in seams)
      {
        float nearest = float.MaxValue;
        foreach (var origin in taken) nearest = Math.Min(nearest, 1f - Vector3.Dot(seam, origin));
        if (nearest > farthestDistance)
        {
          farthestDistance = nearest;
          farthest = seam;
        }
      }
      taken.Add(farthest);
    }
    for (int k = 0; k < CrackOriginCount; k++)
      origins[k] = new Vector4(taken[k], MathHelper.Lerp(FirstCrackStart, LastCrackStart, k / (CrackOriginCount - 1f)));
    return new Layout { Plates = plates, CrackOrigins = origins };
  }

  // The plate that owns a point of the sphere, in the planet's own frame. Matches PlateAt in PlanetShell.fx.
  public static int PlateAt(Layout layout, Vector3 point)
  {
    var warped = point + SeamWarp * new Vector3(MathF.Sin(SeamWarpFrequency * point.Y + 1.3f),
      MathF.Sin(SeamWarpFrequency * point.Z + 0.4f), MathF.Sin(SeamWarpFrequency * point.X + 2.1f));
    int plate = 0;
    float best = float.MinValue;
    for (int i = 0; i < layout.Plates.Length; i++)
    {
      float dot = Vector3.Dot(warped, layout.Plates[i]);
      if (dot > best)
      {
        best = dot;
        plate = i;
      }
    }
    return plate;
  }

  // The wear at which a seam at this point has cracked open. Matches CrackWear in PlanetShell.fx.
  public static float CrackWear(Layout layout, Vector3 point)
  {
    float wear = float.MaxValue;
    foreach (var origin in layout.CrackOrigins)
    {
      float angle = MathF.Acos(Math.Clamp(Vector3.Dot(point, new Vector3(origin.X, origin.Y, origin.Z)), -1f, 1f));
      wear = Math.Min(wear, origin.W + angle * angle / (CrackReach * CrackReach));
    }
    return wear;
  }
}
