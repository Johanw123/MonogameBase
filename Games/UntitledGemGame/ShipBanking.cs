using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// A visual roll amount (-1..1, rendered as -30..30 degrees). Simulation
// positions, headings and collection bounds remain unaffected.
public static class ShipBanking
{
  public static void Update(ref float bank, ref float? previousRotation, float rotation, float dt)
  {
    if (dt <= 0f) return;
    if (previousRotation is not float previous)
    {
      previousRotation = rotation;
      bank = 0f;
      return;
    }
    float rate = MathHelper.WrapAngle(rotation - previous) / dt;
    previousRotation = rotation;
    float target = MathF.Abs(rate) < 0.1f ? 0f : Math.Clamp(rate / 2.4f, -1f, 1f);
    // Quick turn-in, gentler recovery. Exponential smoothing follows elapsed
    // simulation time, including captures with multiple updates per draw.
    float response = target == 0f ? 6f : 10f;
    bank += (target - bank) * (1f - MathF.Exp(-response * dt));
    if (target == 0f && MathF.Abs(bank) < 0.001f) bank = 0f;
  }
}
