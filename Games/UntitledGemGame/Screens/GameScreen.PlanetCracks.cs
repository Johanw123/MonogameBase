using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Screens;

// Core fracture cracks (GameScreen.CoreFracture.cs): thin lines on a sphere that turns
// with the planet sprite, one revolution per animation loop. Positions are in texels of
// the 96-texel planet frame; the lines are rasterized at the prerendered sheet's
// resolution (CrackRasterScale texels per planet texel), anti-aliased, and drawn over
// the planet. Each fracture adds a long jagged crack with branches and hairlines; a new
// one spreads glowing like magma and cools to dark rock.
public partial class UntitledGemGameGameScreen
{
  // The planet's surface, in planet texels: a disc of radius 29.5 around the frame centre.
  private const float CrackDiscCenter = 48f;
  private const float CrackDiscRadius = 29.5f;
  private const float CrackStepDegrees = 2f;
  private const int CrackRasterScale = PlanetSheetFrameSize / PlanetFrameSize;
  private const int CrackRasterSize = PlanetFrameSize * CrackRasterScale;
  // Half the crack line's width, in raster texels.
  private const float CrackHalfWidth = 1.1f;
  private static readonly Color CrackDarkColor = new(24, 16, 20);
  private static readonly Color CrackMagmaColor = new(255, 110, 30);
  private static readonly Color CrackWhiteHotColor = new(255, 240, 200);

  private sealed class CrackPath
  {
    public Vector3[] Points;
    // The share of the crack's growth at which this path starts to spread.
    public float Appears;
    // The main crack splits from its middle outward; branches grow from their fork.
    public bool FromMiddle;
  }

  private readonly List<List<CrackPath>> crackNetworks = new();
  private Texture2D crackTexture;
  private readonly Color[] crackPixels = new Color[CrackRasterSize * CrackRasterSize];
  private long crackRasterKey = -1;

  private static float FrameRotation(int frame) => frame * MathHelper.TwoPi / PlanetFrameCount;

  private int PlanetFrame(float age) => (int)(age / PlanetFrameSeconds) % PlanetFrameCount;

  // Planet-fixed point to view space: the planet turns about its vertical axis, so its
  // surface moves left to right like the sprite's.
  private static Vector3 ToView(Vector3 point, float rotation)
  {
    float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
    return new Vector3(point.X * cos + point.Z * sin, point.Y, -point.X * sin + point.Z * cos);
  }

  private static Vector3 RotateAbout(Vector3 direction, Vector3 axis, float angle)
    => direction * MathF.Cos(angle) + Vector3.Cross(axis, direction) * MathF.Sin(angle);

  // A crack splitting across the surface: mostly straight runs with sudden kinks.
  private static List<Vector3> CrackWalk(Random random, Vector3 start, Vector3 direction, float lengthDegrees, float jag)
  {
    var points = new List<Vector3> { start };
    var point = start;
    var heading = Vector3.Normalize(direction - Vector3.Dot(direction, point) * point);
    float step = MathHelper.ToRadians(CrackStepDegrees);
    int steps = Math.Max(1, (int)(lengthDegrees / CrackStepDegrees));
    for (int i = 0; i < steps; i++)
    {
      float turn = random.NextSingle() < 0.22f
        ? (random.NextSingle() - 0.5f) * jag * 2.6f
        : (random.NextSingle() - 0.5f) * jag * 0.5f;
      heading = RotateAbout(heading, point, turn);
      var next = Vector3.Normalize(point * MathF.Cos(step) + heading * MathF.Sin(step));
      heading = heading * MathF.Cos(step) - point * MathF.Sin(step);
      heading = Vector3.Normalize(heading - Vector3.Dot(heading, next) * next);
      point = next;
      points.Add(point);
    }
    return points;
  }

  private static Vector3 Tangent(Vector3[] points, int index)
  {
    var along = points[Math.Min(points.Length - 1, index + 1)] - points[Math.Max(0, index - 1)];
    var at = points[index];
    return Vector3.Normalize(along - Vector3.Dot(along, at) * at);
  }

  // One fracture's cracks, spread away from the earlier ones (PickCrackCenter).
  private List<CrackPath> CreateCrackNetwork(int index, float? facing)
  {
    var random = new Random(1009 + index * 7919);
    var network = new List<CrackPath>();
    var center = PickCrackCenter(random, facing);
    float tilt = random.NextSingle() * MathHelper.Pi;
    var east = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, center));
    var north = Vector3.Cross(center, east);
    var direction = east * MathF.Cos(tilt) + north * MathF.Sin(tilt);

    float length = 90f + random.NextSingle() * 50f;
    var forward = CrackWalk(random, center, direction, length / 2f, 0.55f);
    var backward = CrackWalk(random, center, -direction, length / 2f, 0.55f);
    backward.Reverse();
    backward.AddRange(forward.GetRange(1, forward.Count - 1));
    var main = backward.ToArray();
    network.Add(new CrackPath { Points = main, FromMiddle = true });

    int branches = 2 + random.Next(2);
    for (int i = 0; i < branches; i++)
    {
      int fork = (int)(main.Length * (0.15f + 0.7f * random.NextSingle()));
      float side = random.Next(2) == 0 ? -1f : 1f;
      var heading = RotateAbout(Tangent(main, fork), main[fork], side * MathHelper.ToRadians(30f + 40f * random.NextSingle()));
      // Branches split off once the main crack has spread past their fork.
      float forkShare = Math.Abs(fork / (main.Length - 1f) - 0.5f) * 2f;
      network.Add(new CrackPath
      {
        Points = CrackWalk(random, main[fork], heading, 20f + random.NextSingle() * 30f, 0.6f).ToArray(),
        Appears = 0.3f + 0.6f * forkShare,
      });
    }
    int paths = network.Count;
    int hairlines = 3 + random.Next(3);
    for (int i = 0; i < hairlines; i++)
    {
      var parent = network[random.Next(paths)].Points;
      int fork = random.Next(parent.Length);
      float side = random.Next(2) == 0 ? -1f : 1f;
      var heading = RotateAbout(Tangent(parent, fork), parent[fork], side * MathHelper.ToRadians(40f + 50f * random.NextSingle()));
      network.Add(new CrackPath
      {
        Points = CrackWalk(random, parent[fork], heading, 5f + random.NextSingle() * 9f, 0.9f).ToArray(),
        Appears = 0.85f,
      });
    }
    return network;
  }

  private static Vector3 FromLatitudeLongitude(float latitude, float longitude)
    => new(MathF.Cos(latitude) * MathF.Sin(longitude), MathF.Sin(latitude), MathF.Cos(latitude) * MathF.Cos(longitude));

  // Where a new crack goes: of a handful of candidate spots, the one farthest from every
  // crack so far, so each fracture breaks open new ground and the whole globe cracks
  // up over a run. With `facing`, candidates are on the side facing the viewer then
  // (the eruption bursts out of the new crack); otherwise anywhere.
  private Vector3 PickCrackCenter(Random random, float? facing)
  {
    Vector3 Candidate()
    {
      if (facing is not float rotation)
        return FromLatitudeLongitude(MathF.Asin(random.NextSingle() * 1.7f - 0.85f), random.NextSingle() * MathHelper.TwoPi);
      var view = FromLatitudeLongitude(MathHelper.ToRadians((random.NextSingle() - 0.5f) * 110f),
        MathHelper.ToRadians((random.NextSingle() - 0.5f) * 110f));
      return ToView(view, -rotation);
    }
    if (crackNetworks.Count == 0 && facing is float front)
      return ToView(FromLatitudeLongitude(MathHelper.ToRadians((random.NextSingle() - 0.5f) * 50f),
        MathHelper.ToRadians((random.NextSingle() - 0.6f) * 40f)), -front);
    var best = Candidate();
    float bestDistance = -1f;
    for (int attempt = 0; attempt < 32; attempt++)
    {
      var candidate = attempt == 0 ? best : Candidate();
      float nearest = float.MaxValue;
      foreach (var network in crackNetworks)
        foreach (var path in network)
          for (int i = 0; i < path.Points.Length; i += 3)
            nearest = Math.Min(nearest, 1f - Vector3.Dot(candidate, path.Points[i]));
      if (nearest > bestDistance)
      {
        bestDistance = nearest;
        best = candidate;
      }
    }
    return best;
  }

  // Cracks for every fracture this run: rebuilt after loading, cleared at extraction.
  private void SyncCrackNetworks()
  {
    int fractures = m_gameState.CoreFractures;
    if (crackNetworks.Count > fractures) crackNetworks.RemoveRange(fractures, crackNetworks.Count - fractures);
    while (crackNetworks.Count < fractures)
    {
      int index = crackNetworks.Count;
      crackNetworks.Add(CreateCrackNetwork(index, null));
    }
  }

  // The newest crack is on the side facing the viewer as it erupts.
  private void AddEventCrackNetwork()
  {
    int index = m_gameState.CoreFractures - 1;
    if (index < 0) return;
    if (crackNetworks.Count > index) crackNetworks.RemoveRange(index, crackNetworks.Count - index);
    while (crackNetworks.Count < index) crackNetworks.Add(CreateCrackNetwork(crackNetworks.Count, null));
    crackNetworks.Add(CreateCrackNetwork(index, FrameRotation(PlanetFrame(planetAge + FractureEruption))));
    crackRasterKey = -1;
  }

  // Where the newest crack's centre is now, in world units (the front of the planet if
  // it has turned away).
  private Vector2 CrackFocusWorld()
  {
    if (crackNetworks.Count == 0) return PlanetPos;
    var main = crackNetworks[^1][0].Points;
    var view = ToView(main[main.Length / 2], FrameRotation(PlanetFrame(planetAge)));
    if (view.Z <= 0f) view = Vector3.UnitZ;
    return PlanetPos + new Vector2(view.X, -view.Y) * CrackDiscRadius * BasePlanetScale * planetVisualSize;
  }

  private float CrackGrowth => fractureActive && fractureTime < FractureEruption
    ? Smooth(0.1f, FractureSwallowEnd, fractureTime) : 1f;

  // Magma while it spreads, white-hot (1) as it erupts, fading to 0 as the rock cools.
  private float CrackHeat => fractureActive && fractureTime < FractureEruption
    ? 0.4f + 0.25f * CrackGrowth : freshCrack / FreshCrackSeconds;

  private static Color CrackColor(float heat)
  {
    if (heat <= 0.01f) return CrackDarkColor;
    var molten = Color.Lerp(CrackDarkColor, CrackMagmaColor, Math.Clamp(heat * 2.5f, 0f, 1f));
    return Color.Lerp(molten, CrackWhiteHotColor, Math.Clamp((heat - 0.6f) / 0.4f, 0f, 1f));
  }

  private void DrawPlanetCracks(int frame, Vector2 position, Vector2 origin, float scale)
  {
    SyncCrackNetworks();
    if (crackNetworks.Count == 0) return;
    float growth = CrackGrowth, heat = CrackHeat;
    long key = frame + PlanetFrameCount * ((long)crackNetworks.Count
      + 64L * ((int)(growth * 48f) + 64L * (int)(heat * 32f)));
    if (key != crackRasterKey || crackTexture == null)
    {
      crackRasterKey = key;
      RasterizeCracks(frame, growth, heat);
    }
    // Premultiplied texels, filtered like the planet sheet underneath.
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
      transformMatrix: m_camera.GetViewMatrix());
    m_spriteBatch.Draw(crackTexture, position, null, Color.White, 0f, origin * CrackRasterScale,
      scale / CrackRasterScale, SpriteEffects.None, 0f);
    m_spriteBatch.End();
  }

  private void RasterizeCracks(int frame, float growth, float heat)
  {
    crackTexture ??= new Texture2D(GameMain.Instance.GraphicsDevice, CrackRasterSize, CrackRasterSize);
    Array.Clear(crackPixels);
    float rotation = FrameRotation(frame);
    int count = crackNetworks.Count;
    for (int n = Math.Max(0, count - MaxDrawnCracks); n < count; n++)
    {
      bool newest = n == count - 1;
      float grow = newest ? growth : 1f;
      var color = CrackColor(newest ? heat : 0f);
      foreach (var path in crackNetworks[n])
      {
        float share = Math.Clamp((grow - path.Appears) / Math.Max(0.01f, 1f - path.Appears), 0f, 1f);
        if (share <= 0f) continue;
        int last = path.Points.Length - 1;
        int from = 0, to = (int)MathF.Round(share * last);
        if (path.FromMiddle)
        {
          int middle = last / 2;
          from = middle - (int)MathF.Round(share * middle);
          to = middle + (int)MathF.Round(share * (last - middle));
        }
        for (int i = from; i < to; i++)
          RasterizeCrackSegment(ToView(path.Points[i], rotation), ToView(path.Points[i + 1], rotation), color);
      }
    }
    crackTexture.SetData(crackPixels);
  }

  private void RasterizeCrackSegment(Vector3 a, Vector3 b, Color color)
  {
    // The far side is hidden; cracks thin out toward the limb.
    if (a.Z <= 0.02f || b.Z <= 0.02f) return;
    float alpha = Math.Clamp(Math.Min(a.Z, b.Z) / 0.3f, 0.45f, 1f);
    float center = CrackDiscCenter * CrackRasterScale, radius = CrackDiscRadius * CrackRasterScale;
    float limb = 30f * CrackRasterScale;
    var p0 = new Vector2(center + a.X * radius, center - a.Y * radius);
    var p1 = new Vector2(center + b.X * radius, center - b.Y * radius);
    var along = p1 - p0;
    float lengthSquared = Math.Max(along.LengthSquared(), 1e-6f);
    // Coverage of each texel by a line CrackHalfWidth texels either side of the segment.
    float reach = CrackHalfWidth + 1f;
    int minX = Math.Max(0, (int)MathF.Floor(Math.Min(p0.X, p1.X) - reach));
    int maxX = Math.Min(CrackRasterSize - 1, (int)MathF.Ceiling(Math.Max(p0.X, p1.X) + reach));
    int minY = Math.Max(0, (int)MathF.Floor(Math.Min(p0.Y, p1.Y) - reach));
    int maxY = Math.Min(CrackRasterSize - 1, (int)MathF.Ceiling(Math.Max(p0.Y, p1.Y) + reach));
    for (int y = minY; y <= maxY; y++)
      for (int x = minX; x <= maxX; x++)
      {
        var texel = new Vector2(x + 0.5f, y + 0.5f);
        if (Vector2.DistanceSquared(texel, new Vector2(center)) > limb * limb) continue;
        float t = Math.Clamp(Vector2.Dot(texel - p0, along) / lengthSquared, 0f, 1f);
        float distance = Vector2.Distance(texel, p0 + along * t);
        float coverage = Math.Clamp(CrackHalfWidth + 0.5f - distance, 0f, 1f) * alpha;
        ref var pixel = ref crackPixels[y * CrackRasterSize + x];
        if (coverage * 255f <= pixel.A) continue;
        pixel = new Color(color.ToVector3() * coverage) { A = (byte)MathF.Round(coverage * 255f) };
      }
  }
}
