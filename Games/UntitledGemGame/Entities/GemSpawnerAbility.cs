using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities;

public class GemSpawnerAbility : IHomeBaseAbility
{
  public const int MidasLimit = 128;
  public const float MidasRadius = 600f;
  private const float MidasDuration = 0.8f;
  private sealed class Ring
  {
    public Vector2 Center;
    public int Count, Seeds;
    public float Radius, Angle, Remaining;
    public bool Finale, Spiral;
  }
  private sealed class GoldenWave
  {
    public Vector2 Center;
    public float Age;
    public readonly List<(Gem Gem, int Id, ulong Lifetime)> Targets = new(MidasLimit);
  }
  private readonly List<Ring> pendingRings = new();
  private readonly List<GoldenWave> goldenWaves = new();
  private readonly HashSet<(Gem Gem, int Id, ulong Lifetime)> reservedMidasTargets = new();
  public override string IconPath => "Textures/scifi_icons/icon_accuracy/14_accuracy.png";
  public override int Level => UpgradeManager.Instance.UGA.GemSpawner;
  public override int DurationTimeMax => 0;
  protected override int BaseCooldownMilliseconds => BaseStats.GemSpawnerCooldownMilliseconds;
  protected override float CooldownMultiplier => UpgradeManager.Instance.UGA.GemSpawnerCooldown;

  public static int GetNextRingGemCount(int count, int reductionPercent)
    => (int)(count * (1.0 - Math.Clamp(reductionPercent, 0, 100) / 100.0));

  public static GemSpawnData ApplyRichVeins(GemSpawnData spawn)
  {
    if (Random.Shared.NextDouble() * 100 < UpgradeManager.Instance.UGA.GemSpawnerRichVeins)
    {
      spawn.BaseValue = AbilityGemValue.AddBonus(spawn.BaseValue, 100);
      spawn.IsLucky = true;
    }
    return spawn;
  }

  public override void Activate() => ActivateAt(UntitledGemGameGameScreen.HomeBasePos,
    BaseStats.GetHarvesterCollectionRange(HomeBase.Instance.Entity.Get<Harvester>()));

  public void ActivateAt(Vector2 center, float collectionRadius)
  {
    var upgrades = UpgradeManager.Instance.UGA;
    // Snapshot pre-existing gems before any rings enter the spawn queue.
    if (upgrades.GemSpawnerMidasPulse) BeginGoldenWave(center);
    bool spiral = upgrades.GemSpawnerGenesisSpiral;
    int count = upgrades.GemSpawnerNrGems;
    int seeds = upgrades.GemSpawnerCrystalBloom ? 3 : 0;
    float angle = Random.Shared.NextSingle() * MathHelper.TwoPi;
    float delay = spiral ? 0.3f : 0f;
    int rings = Math.Clamp(upgrades.GemSpawnerNumberOfRings, 0, 32);
    if (spiral) SpawnerEffects.Add(this, center, Color.Violet, collectionRadius + 45f, 8f, delay);
    for (int i = 0; i < rings; ++i)
    {
      int ringSeeds = Math.Min(seeds, count);
      Schedule(new Ring { Center = center, Count = count, Seeds = ringSeeds,
        Radius = collectionRadius + 25f + (spiral ? i * 24f : Random.Shared.NextSingle() * 125f),
        Angle = angle + i * 0.45f, Remaining = delay, Spiral = spiral });
      seeds -= ringSeeds;
      count = GetNextRingGemCount(count, upgrades.GemSpawnerRingReduction);
      if (spiral) delay += MathF.Max(0.06f, 0.18f * MathF.Pow(0.8f, i));
    }
    if (spiral)
      Schedule(new Ring { Center = center, Count = upgrades.GemSpawnerNrGems,
        Seeds = Math.Min(seeds, upgrades.GemSpawnerNrGems), Radius = collectionRadius + 25f + rings * 24f,
        Angle = angle + rings * 0.45f, Remaining = delay, Finale = true, Spiral = true });
  }

  private void Schedule(Ring ring)
  {
    if (ring.Remaining <= 0f) SpawnRing(ring);
    else pendingRings.Add(ring);
  }

  private void SpawnRing(Ring ring)
  {
    for (int i = 0; i < ring.Count; ++i)
    {
      float angle = ring.Angle + i * MathHelper.TwoPi / ring.Count;
      var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
      var spawn = ApplyRichVeins(GemQualityTable.RollCurrent());
      if (ring.Finale) spawn.BaseValue = AbilityGemValue.AddBonus(spawn.BaseValue, 100);
      // Spread seeds evenly around the ring instead of clumping at its first arc.
      bool seed = ring.Seeds > 0 && (i == 0
        || (long)i * ring.Seeds / ring.Count != (long)(i - 1) * ring.Seeds / ring.Count);
      if (seed) spawn.BaseValue = (uint)Math.Min(uint.MaxValue, (ulong)spawn.BaseValue * Gem.BloomGemCount);
      EntityFactory.Instance.QueueGemSpawn(ring.Center + direction * ring.Radius, spawn.Type,
        spawn.BaseValue, spawn.IsLucky, isBloomSeed: seed,
        launchVelocity: ring.Spiral ? direction * (ring.Finale ? 240f : 160f) : Vector2.Zero);
    }
    if (ring.Spiral)
      SpawnerEffects.Add(this, ring.Center, ring.Finale ? Color.Gold : Color.Orchid,
        ring.Radius, ring.Radius + 40f, ring.Finale ? 0.55f : 0.3f);
    if (ring.Finale) AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: 0.25f);
  }

  private void BeginGoldenWave(Vector2 center)
  {
    var fleet = HarvesterCollectionSystem.Instance;
    var available = fleet.flatSpatialHash.AvailableIndices;
    int examined = Math.Min(8192, available.Length);
    var wave = new GoldenWave { Center = center };
    for (int i = 0; i < examined && wave.Targets.Count < MidasLimit; ++i)
    {
      var data = fleet.flatSpatialHash.Gems[available[(int)((long)i * available.Length / examined)]];
      if (Vector2.DistanceSquared(center, new Vector2(data.X, data.Y)) > MidasRadius * MidasRadius) continue;
      var gem = fleet.GetEntityP(data.EntityId)?.Get<Gem>();
      if (gem != null && gem.IsLive && !gem.IsGilded && !gem.PickedUp && !gem.WasClicked
        && reservedMidasTargets.Add((gem, gem.Id, gem.LifetimeVersion)))
        wave.Targets.Add((gem, gem.Id, gem.LifetimeVersion));
    }
    goldenWaves.Add(wave);
    SpawnerEffects.Add(this, center, Color.Gold, 0f, MidasRadius, MidasDuration);
  }

  protected override void UpdateEffects(GameTime gameTime)
  {
    float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
    // Preserve chronological ring order even when one frame spans several pulses.
    for (int i = 0; i < pendingRings.Count;)
    {
      var ring = pendingRings[i];
      ring.Remaining -= dt;
      if (ring.Remaining > 0f) { ++i; continue; }
      SpawnRing(ring);
      pendingRings.RemoveAt(i);
    }
    for (int i = goldenWaves.Count - 1; i >= 0; --i)
    {
      var wave = goldenWaves[i];
      wave.Age += dt;
      float radius = MathF.Min(1f, wave.Age / MidasDuration) * MidasRadius;
      foreach (var target in wave.Targets)
        if (target.Gem.MatchesLifetime(target.Id, target.Lifetime)
          && Vector2.DistanceSquared(target.Gem.BoundingCircle.Center, wave.Center) <= radius * radius)
          target.Gem.TryGild();
      if (wave.Age >= MidasDuration)
      {
        foreach (var target in wave.Targets) reservedMidasTargets.Remove(target);
        goldenWaves.RemoveAt(i);
      }
    }
  }

  public override void Deactivate()
  {
    pendingRings.Clear();
    goldenWaves.Clear();
    reservedMidasTargets.Clear();
    SpawnerEffects.Cancel(this);
  }
}
