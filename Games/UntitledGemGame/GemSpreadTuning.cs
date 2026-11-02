using System;
using ImGuiNET;

namespace UntitledGemGame;

// How far knocked-loose gems fly from the planet. Tweak the table below.
//
// Each point maps a hit's fire power to how far out its gems may land, as a fraction of the
// distance from the planet's edge (plus PlanetDebrisGap) to the farthest screen corner:
// 0 = right next to the planet, 1 = anywhere on screen. Fire powers between points are
// interpolated; beyond the last point gems keep the last point's reach. Keep both columns
// increasing.
//
// Reference fire powers (base + tree, before support modules and signals): cannon 1 -> ~18,
// laser 1 -> 6, harpoon 2 -> ~17, rockets 5 -> 15, railgun 15 -> 35. Weapons also scale the
// result with their own reach (e.g. the Core Drill's 0.8), and Deep Core Munitions widens it.
//
// The debug window ("Gem spread") edits these live for the session.
public static class GemSpreadTuning
{
  public static readonly (int FirePower, float Reach)[] ReachByFirePower =
  [
    (1, 0.04f),
    (5, 0.07f),
    (10, 0.11f),
    (15, 0.16f),
    (25, 0.28f),
    (40, 0.55f),
    (60, 1.00f),
  ];

  // Multiplies every reach above; a quick global knob (0.5 = everything lands half as far).
  public static float ReachMultiplier = 1f;

  // The fire power that reaches the last point; Weapon Overdrive throws gems this far.
  public static int FullReachFirePower => ReachByFirePower[^1].FirePower;

  public static float ReachFraction(int firePower)
  {
    var points = ReachByFirePower;
    float reach = points[^1].Reach;
    if (firePower <= points[0].FirePower)
      reach = points[0].Reach;
    else
    {
      for (int i = 1; i < points.Length; i++)
      {
        if (firePower > points[i].FirePower) continue;
        var (fromPower, fromReach) = points[i - 1];
        var (toPower, toReach) = points[i];
        float t = (firePower - fromPower) / (float)Math.Max(1, toPower - fromPower);
        reach = fromReach + (toReach - fromReach) * t;
        break;
      }
    }
    return reach * ReachMultiplier;
  }

  public static void DrawDebug()
  {
    if (!ImGui.CollapsingHeader("Gem spread")) return;
    ImGui.TextWrapped("How far gems fly per fire power (0 = at the planet, 1 = whole screen). "
      + "Session only: copy values you like into GemSpreadTuning.cs.");
    ImGui.SliderFloat("Global multiplier", ref ReachMultiplier, 0.1f, 3f);
    var points = ReachByFirePower;
    for (int i = 0; i < points.Length; i++)
    {
      ImGui.PushID(i);
      int power = points[i].FirePower;
      float reach = points[i].Reach;
      ImGui.SetNextItemWidth(90f);
      int minPower = i == 0 ? 1 : points[i - 1].FirePower + 1;
      int maxPower = i == points.Length - 1 ? 500 : points[i + 1].FirePower - 1;
      if (ImGui.InputInt("Fire power", ref power))
        points[i].FirePower = Math.Clamp(power, minPower, Math.Max(minPower, maxPower));
      ImGui.SameLine();
      ImGui.SetNextItemWidth(200f);
      if (ImGui.SliderFloat("Reach", ref reach, 0f, 1f))
        points[i].Reach = reach;
      ImGui.PopID();
    }
    ImGui.Text($"Current: FP1 {ReachFraction(1):0.00}, FP5 {ReachFraction(5):0.00}, FP15 {ReachFraction(15):0.00}, "
      + $"FP35 {ReachFraction(35):0.00}");
  }
}
