using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Graphics;

namespace UntitledGemGame.Entities;

public partial class Harvester
{
  public float SlipstreamTimeRemaining;
  // Keep the visual on the real engine nozzles and ease back to normal thrust as
  // the boost expires. Stopped and refueling ships keep their usual engine state.
  public float SlipstreamExhaustBoost => PositionMoved && CurrentState == HarvesterState.Collecting
    ? 1f + 0.45f * MathHelper.SmoothStep(0f, 1f, Math.Clamp(SlipstreamTimeRemaining / 0.6f, 0f, 1f))
    : 1f;
  public uint TowedGemCount { get; private set; }
  internal readonly List<int> ClaimedTailGems = new();

  // Retire the loose gem on pickup, keeping only a bounded set of visual copies.
  // Its value joins the delivery total immediately; its count stays outside the hold.
  // No references to pooled gem entities survive the pickup.
  public struct TailGem
  {
    public Vector2 Position;
    public Texture2DRegion Region;
    public Color Color;
    public float Rotation;
  }
  private List<TailGem> tailGems;
  public IReadOnlyList<TailGem> TailGems => tailGems;

  public sealed class DeliveredTail
  {
    public Vector2 Anchor;
    public List<TailGem> Gems;
  }
  public List<DeliveredTail> DeliveredTails { get; } = new();

  internal void AddTowedGem(Gem gem, Sprite sprite, Vector2 position)
  {
    PickedUpGem(gem);
    --CarryingGemCount;
    ++TowedGemCount;
    tailGems ??= new(PrestigeTalentEffects.TractorTailVisualLimit);
    if (tailGems.Count < PrestigeTalentEffects.TractorTailVisualLimit)
      tailGems.Add(new TailGem
      {
        Position = position, Region = sprite?.TextureRegion,
        Color = new Color(GemQualityTable.GetColor(gem.GemType), 1f),
        Rotation = TowedGemCount * 2.3999632f,
      });
  }

  internal void UpdateTractorTail(Vector2 position, float rotation, float dt)
  {
    // Delivered visuals continue independently while the ship starts its next trip.
    float travel = 320f * Math.Max(0f, dt);
    for (int t = DeliveredTails.Count - 1; t >= 0; t--)
    {
      var delivery = DeliveredTails[t];
      for (int i = delivery.Gems.Count - 1; i >= 0; i--)
      {
        var gem = delivery.Gems[i];
        var target = i == 0 ? delivery.Anchor : delivery.Gems[i - 1].Position;
        var offset = target - gem.Position;
        float distance = offset.Length();
        gem.Position += distance > 0f ? offset * (Math.Min(travel, distance) / distance) : Vector2.Zero;
        delivery.Gems[i] = gem;
      }
      while (delivery.Gems.Count > 0 && Vector2.DistanceSquared(delivery.Gems[0].Position, delivery.Anchor) < 4f)
        delivery.Gems.RemoveAt(0);
      if (delivery.Gems.Count == 0) DeliveredTails.RemoveAt(t);
    }
    if (tailGems == null || tailGems.Count == 0) return;
    var behind = new Vector2(-MathF.Sin(rotation), MathF.Cos(rotation));
    float blend = 1f - MathF.Exp(-14f * Math.Max(0f, dt));
    var anchor = position + behind * (BoundingCircle.Radius + 12f);
    for (int i = 0; i < tailGems.Count; i++)
    {
      var gem = tailGems[i];
      var offset = gem.Position - anchor;
      var direction = offset.LengthSquared() > 0.01f ? Vector2.Normalize(offset) : behind;
      gem.Position = Vector2.Lerp(gem.Position, anchor + direction * 20f, blend);
      tailGems[i] = gem;
      anchor = gem.Position;
    }
  }

  // Both ordinary deliveries and shuttle transfers use this before counting cargo.
  internal void StowTractorTail()
  {
    CarryingGemCount = (uint)Math.Min(uint.MaxValue, (ulong)CarryingGemCount + TowedGemCount);
    TowedGemCount = 0;
    if (tailGems is { Count: > 0 })
    {
      DeliveredTails.Add(new DeliveredTail { Anchor = BoundingCircle.Center, Gems = tailGems });
      tailGems = new(PrestigeTalentEffects.TractorTailVisualLimit);
    }
  }

  private void ClearFleetTalents()
  {
    TowedGemCount = 0;
    tailGems?.Clear();
    DeliveredTails.Clear();
    ClaimedTailGems.Clear();
    SlipstreamTimeRemaining = 0f;
  }
}
