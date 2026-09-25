using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities;

public sealed class ConstellationNet
{
  public static int CaptureLimit => SignalStats.ConstellationCapacity;
  public const float Windup = 0.55f;
  public const float CollapseDuration = 0.75f;
  public const float FlashDuration = 0.3f;
  internal ChainLightningAbility Owner;
  public Vector2[] Hull { get; internal set; }
  public Vector2[] Sparks { get; internal set; }
  public Vector2 Destination { get; internal set; }
  public float Age { get; internal set; }
  public float Collapse => Math.Clamp((Age - Windup) / CollapseDuration, 0f, 1f);
  public float PullProgress => 1f - MathF.Pow(1f - Collapse, 5f);

  private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

  // A convex outline cannot cross itself, regardless of primary targeting order.
  public static Vector2[] BuildHull(List<Vector2> anchors)
  {
    var points = new List<Vector2>(anchors);
    points.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
    var hull = new List<Vector2>(points.Count * 2);
    foreach (var point in points)
    {
      while (hull.Count >= 2 && Cross(hull[^2], hull[^1], point) <= 0f)
        hull.RemoveAt(hull.Count - 1);
      hull.Add(point);
    }
    int lowerCount = hull.Count;
    for (int i = points.Count - 2; i >= 0; --i)
    {
      while (hull.Count > lowerCount && Cross(hull[^2], hull[^1], points[i]) <= 0f)
        hull.RemoveAt(hull.Count - 1);
      hull.Add(points[i]);
    }
    if (hull.Count > 0) hull.RemoveAt(hull.Count - 1);
    return hull.Count >= 3 ? hull.ToArray() : Array.Empty<Vector2>();
  }

  public static bool Contains(Vector2[] hull, Vector2 point)
  {
    if (hull.Length < 3) return false;
    for (int i = 0; i < hull.Length; ++i)
      if (Cross(hull[i], hull[(i + 1) % hull.Length], point) < -0.001f) return false;
    return true;
  }
}

public partial class ChainLightningAbility
{
  private static readonly List<ConstellationNet> activeConstellations = new();
  public static IReadOnlyList<ConstellationNet> Constellations => activeConstellations;

  private void UpdateConstellations(float dt)
  {
    for (int i = activeConstellations.Count - 1; i >= 0; --i)
    {
      var net = activeConstellations[i];
      if (net.Owner != this) continue;
      float impactTime = ConstellationNet.Windup + ConstellationNet.CollapseDuration;
      if (net.Age < impactTime && net.Age + dt >= impactTime)
        AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: -0.35f);
      net.Age += dt;
      if (net.Age >= ConstellationNet.Windup + ConstellationNet.CollapseDuration + ConstellationNet.FlashDuration)
        activeConstellations.RemoveAt(i);
    }
  }

  private void ActivateConstellation(int primaryCount)
  {
    var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
    var target = UntitledGemGameGameScreen.HomeBasePos;
    int firstChain = _activeChains.Count;
    int firstAftershock = pendingAftershocks.Count;
    var anchors = new List<Vector2>(primaryCount);
    // Reserve all primary anchors before Reaction branches can consume them.
    for (int i = 0; i < primaryCount; ++i)
    {
      int index = _gemGrabBuffer[i];
      var point = new Vector2(grid.Gems[index].X, grid.Gems[index].Y);
      if (StartChain(index, target, Color.Cyan, 0, deferReaction: true)) anchors.Add(point);
    }
    if (UpgradeManager.Instance.UGA.ChainMagnetizerChainReaction)
      foreach (var point in anchors) StartReaction(point, target, 0, 0);

    var hull = ConstellationNet.BuildHull(anchors);
    if (hull.Length < 3) return; // Sparse/collinear targets retain normal chain behavior.
    Vector2 min = hull[0], max = hull[0];
    foreach (var point in hull)
    {
      min = Vector2.Min(min, point);
      max = Vector2.Max(max, point);
    }
    // Sample evenly across the available set: bound work even for huge, sparse
    // outlines, without spending most of the budget on one dense spatial cell.
    // Snapshot before starting any chains, which mutate the available set.
    int captureLimit = ConstellationNet.CaptureLimit;
    var captures = new List<int>(captureLimit);
    var available = grid.AvailableIndices;
    int examined = Math.Min(8192, available.Length);
    for (int i = 0; i < examined; ++i)
    {
      int index = available[(int)((long)i * available.Length / examined)];
      var data = grid.Gems[index];
      if (data.X < min.X || data.X > max.X || data.Y < min.Y || data.Y > max.Y) continue;
      if (ConstellationNet.Contains(hull, new Vector2(data.X, data.Y))) captures.Add(index);
      if (captures.Count == captureLimit) break;
    }
    var sparks = new List<Vector2>(32);
    foreach (int index in captures)
    {
      var point = new Vector2(grid.Gems[index].X, grid.Gems[index].Y);
      // Captures get full Residual Charge but cannot branch or schedule waves.
      if (StartChain(index, target, Color.Cyan, 0, netCapture: true) && sparks.Count < 32)
        sparks.Add(point);
    }
    for (int i = firstChain; i < _activeChains.Count; ++i)
    {
      var chain = _activeChains[i];
      chain.Delay = ConstellationNet.Windup;
      chain.Duration = ConstellationNet.CollapseDuration;
      _activeChains[i] = chain;
    }
    // Let aftershocks follow the net's charge-up rather than steal its moment.
    for (int i = firstAftershock; i < pendingAftershocks.Count; ++i)
    {
      var pending = pendingAftershocks[i];
      pending.Remaining += ConstellationNet.Windup;
      pendingAftershocks[i] = pending;
    }
    activeConstellations.Add(new ConstellationNet
    {
      Owner = this, Hull = hull, Sparks = sparks.ToArray(), Destination = target
    });
  }
}
