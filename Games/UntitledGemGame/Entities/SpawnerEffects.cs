using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Entities;

public sealed class SpawnerPulse
{
  public GemSpawnerAbility Owner;
  public Vector2 Position;
  public Color Color;
  public float StartRadius, EndRadius, Lifetime, Age;
  public float Progress => MathHelper.Clamp(Age / Lifetime, 0f, 1f);
}

public static class SpawnerEffects
{
  public static readonly List<Gem> Seeds = new();
  private static readonly List<SpawnerPulse> pulses = new();
  public static IReadOnlyList<SpawnerPulse> Pulses => pulses;

  public static void Add(GemSpawnerAbility owner, Vector2 position, Color color,
    float startRadius, float endRadius, float lifetime)
  {
    if (pulses.Count >= 64) pulses.RemoveAt(0);
    pulses.Add(new SpawnerPulse { Owner = owner, Position = position, Color = color,
      StartRadius = startRadius, EndRadius = endRadius, Lifetime = lifetime });
  }

  public static void Update(float dt)
  {
    for (int i = pulses.Count - 1; i >= 0; --i)
      if ((pulses[i].Age += dt) >= pulses[i].Lifetime) pulses.RemoveAt(i);
  }
  public static void Cancel(GemSpawnerAbility owner) => pulses.RemoveAll(p => p.Owner == owner);
  public static void Clear() => pulses.Clear();
}
