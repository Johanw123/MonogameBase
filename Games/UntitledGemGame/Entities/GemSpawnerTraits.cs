using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities;

public partial class Gem
{
  public bool IsBloomSeed { get; private set; }
  public bool IsGilded { get; private set; }
  public Vector2 LaunchVelocity { get; private set; }
  private bool bloomOpened;
  public const int BloomGemCount = 4;

  public void ConfigureSpawnerTraits(bool seed, bool gilded, Vector2 launchVelocity = default)
  {
    if (IsBloomSeed) SpawnerEffects.Seeds.Remove(this);
    IsBloomSeed = seed;
    IsGilded = gilded;
    bloomOpened = false;
    LaunchVelocity = launchVelocity;
    if (seed) SpawnerEffects.Seeds.Add(this);
    if (seed || gilded) RefreshSpawnerColor();
    if (launchVelocity != Vector2.Zero) Wake();
  }

  private void RefreshSpawnerColor()
  {
    if (m_sprite == null) return;
    var color = IsGilded ? Color.Gold : IsBloomSeed ? new Color(140, 210, 185) : GemQualityTable.GetColor(GemType);
    m_sprite.Color = new Color(color.R, color.G, color.B,
      IsLucky || IsGilded ? byte.MaxValue : (byte)0);
    RenderGemSystem.Instance?.UpdateGem(Id);
  }

  public bool TryGild()
  {
    if (!IsLive || PickedUp || WasClicked || IsGilded) return false;
    BaseValue = AbilityGemValue.AddBonus(BaseValue, 100);
    HarvesterCollectionSystem.Instance.flatSpatialHash.SetGemValue(GridIndex, BaseValue);
    IsGilded = true;
    RefreshSpawnerColor();
    return true;
  }

  public bool TryBloom()
  {
    if (!IsBloomSeed || bloomOpened || !PickedUp) return false;
    bloomOpened = true;
    SpawnerEffects.Seeds.Remove(this);
    var position = BoundingCircle.Center;
    float angleOffset = Random.Shared.NextSingle() * MathHelper.TwoPi;
    for (int i = 0; i < BloomGemCount; ++i)
    {
      float angle = angleOffset + i * MathHelper.TwoPi / BloomGemCount;
      var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
      // Split the actual charged/gilded value exactly; no second quality/luck roll.
      uint value = BaseValue / BloomGemCount + (i < BaseValue % BloomGemCount ? 1u : 0u);
      EntityFactory.Instance.QueueGemSpawn(position + direction * 24f, GemType, value, IsLucky,
        isGilded: IsGilded, launchVelocity: direction * 160f);
    }
    SpawnerEffects.Add(null, position, IsGilded ? Color.Gold : Color.Aquamarine, 8f, 75f, 0.5f);
    return true;
  }

  private void ResetSpawnerTraits()
  {
    if (IsBloomSeed) SpawnerEffects.Seeds.Remove(this);
    IsBloomSeed = IsGilded = bloomOpened = false;
    LaunchVelocity = Vector2.Zero;
  }

  private void UpdateSpawnMotion(float dt)
  {
    if (LaunchVelocity == Vector2.Zero) return;
    if (!IsLive || PickedUp || WasClicked
      || HarvesterCollectionSystem.Instance.flatSpatialHash.Gems[GridIndex].ClaimState != 0)
    {
      LaunchVelocity = Vector2.Zero;
      return;
    }
    float decay = MathF.Exp(-8f * dt);
    var velocity = LaunchVelocity;
    MoveByChain(BoundingCircle.Center + velocity * ((1f - decay) / 8f));
    LaunchVelocity = velocity.LengthSquared() * decay * decay < 1f ? Vector2.Zero : velocity * decay;
  }
}
