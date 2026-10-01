using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

// Save the actual rolled reward, rather than rolling a replacement on release.
public sealed class ReservedGem
{
  public float X { get; set; }
  public float Y { get; set; }
  public GemTypes Type { get; set; }
  public uint BaseValue { get; set; }
  public bool IsLucky { get; set; }
  public bool IsBloomSeed { get; set; }
  public bool IsGilded { get; set; }
  public float VelocityX { get; set; }
  public float VelocityY { get; set; }
  public int GemCount { get; set; } = 1;

  public GemSpawnData ToSpawn() => new()
  {
    Position = new Vector2(X, Y), Type = Type, BaseValue = BaseValue,
    IsLucky = IsLucky, IsBloomSeed = IsBloomSeed, IsGilded = IsGilded,
    LaunchVelocity = new Vector2(VelocityX, VelocityY)
  };

  public ReservedGem Copy() => (ReservedGem)MemberwiseClone();
}

public sealed class GemReserve
{
  public const int CondensationLimit = 4;
  private readonly Queue<ReservedGem> gems = new();
  private readonly Dictionary<(GemTypes, bool, bool), ReservedGem> condensable = new();
  public int Count => gems.Count;
  public long StoredGemCount { get; private set; }
  public ulong StoredValue { get; private set; }

  public bool TryStore(GemSpawnData spawn, int capacity, bool condensation)
  {
    if (capacity <= 0) return false;
    var key = (spawn.Type, spawn.IsLucky, spawn.IsGilded);
    if (condensation && !spawn.IsBloomSeed && condensable.TryGetValue(key, out var target)
      && target.GemCount < CondensationLimit && (ulong)target.BaseValue + spawn.BaseValue <= uint.MaxValue)
    {
      target.BaseValue += spawn.BaseValue;
      target.GemCount++;
      StoredGemCount++;
      StoredValue = PrestigeProgression.AddSaturating(StoredValue, spawn.BaseValue);
      return true;
    }
    if (Count >= capacity) return false;
    var gem = new ReservedGem
    {
      X = spawn.Position.X, Y = spawn.Position.Y, Type = spawn.Type,
      BaseValue = spawn.BaseValue, IsLucky = spawn.IsLucky,
      IsBloomSeed = spawn.IsBloomSeed, IsGilded = spawn.IsGilded,
      VelocityX = spawn.LaunchVelocity.X, VelocityY = spawn.LaunchVelocity.Y
    };
    gems.Enqueue(gem);
    StoredGemCount++;
    StoredValue = PrestigeProgression.AddSaturating(StoredValue, spawn.BaseValue);
    if (!spawn.IsBloomSeed) condensable[key] = gem;
    return true;
  }

  public bool TryRelease(out GemSpawnData spawn)
  {
    spawn = default;
    if (!gems.TryDequeue(out var gem)) return false;
    StoredGemCount -= gem.GemCount;
    StoredValue -= gem.BaseValue;
    var key = (gem.Type, gem.IsLucky, gem.IsGilded);
    if (condensable.TryGetValue(key, out var target) && ReferenceEquals(target, gem))
      condensable.Remove(key);
    spawn = gem.ToSpawn();
    return true;
  }

  public List<ReservedGem> Capture() => gems.Select(g => g.Copy()).ToList();

  public ulong BurstValue(float multiplier)
  {
    if (!float.IsFinite(multiplier) || multiplier < 1f) throw new ArgumentOutOfRangeException(nameof(multiplier));
    decimal value = StoredValue * (decimal)multiplier;
    return (ulong)Math.Min(ulong.MaxValue, decimal.Ceiling(value));
  }

  public bool TryBurst(float multiplier, out ulong payout)
  {
    payout = 0;
    if (Count == 0) return false;
    payout = BurstValue(multiplier);
    Clear();
    return true;
  }

  public void Restore(IEnumerable<ReservedGem> saved)
  {
    Clear();
    foreach (var entry in saved)
    {
      var gem = entry.Copy();
      if (!float.IsFinite(gem.X) || !float.IsFinite(gem.Y)
        || !float.IsFinite(gem.VelocityX) || !float.IsFinite(gem.VelocityY)
        || !Enum.IsDefined(gem.Type) || gem.GemCount < 1 || gem.GemCount > CondensationLimit
        || (gem.IsBloomSeed && gem.GemCount != 1))
        throw new System.IO.InvalidDataException("Invalid gem reserve entry.");
      gems.Enqueue(gem);
      StoredGemCount += gem.GemCount;
      StoredValue = PrestigeProgression.AddSaturating(StoredValue, gem.BaseValue);
      if (!gem.IsBloomSeed) condensable[(gem.Type, gem.IsLucky, gem.IsGilded)] = gem;
    }
  }

  public void Clear()
  {
    gems.Clear();
    condensable.Clear();
    StoredGemCount = 0;
    StoredValue = 0;
  }
}
