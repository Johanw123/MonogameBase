using System;
using System.Collections.Generic;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

// Core Fracture (tuning in CoreFracture.cs): when the damage dealt over the last
// minute reaches the next threshold, the planet's core gives way. There is no
// warning and no caption: the player sees it happen. Weapons, ships and ship systems
// hold still from the first tremor until the shard is out.
//  1. Tremor (0-1.8 s): quickening heartbeats shake the planet as a glowing fissure
//     spreads across its face.
//  2. Swallow (1.8-3.4 s): every loose gem is pulled back into the planet, each a
//     launch in reverse (Gem.Swallow), starting one after another.
//  3. Eruption (3.4 s): the fissure bursts open into a crack that stays for the rest
//     of the run. A shockwave blows the fleet out toward the edges of the screen, and
//     gems pour out over the whole field: twice what it swallowed, from layers below
//     the strongest weapon's reach.
//  4. Shard (4 s): a Core Shard flies out of the crack and hovers, and everything
//     moves again. Clicking the shard collects it; otherwise it flies home on its own.
public partial class UntitledGemGameGameScreen
{
  private const float FractureSwallowStart = 1.8f;
  private const float FractureSwallowEnd = 3f;
  private const float FractureEruption = 3.4f;
  private const float FractureEruptionSeconds = 1.2f;
  private const float FractureShardRelease = 4f;
  private const float FractureResume = 4.4f;
  private const float SwallowStaggerSeconds = 0.8f;
  private const float SwallowFlightSeconds = 0.8f;
  private const float FractureShockwaveSeconds = 0.85f;
  private const float FractureEnd = 5.2f;
  // Swallowed gems hold their field slots until they vanish; the eruption waits that long.
  private const float EruptionDeadline = 7.5f;
  private const float FractureCooldownSeconds = 4f;
  private const float FreshCrackSeconds = 6f;
  private const int MaxEruptionGemsPerFrame = 400;
  private const float ShardFlightSeconds = 0.9f;
  private const float ShardCollectSeconds = 0.7f;
  private const float ShardPopupSeconds = 3.5f;
  private const int MaxDrawnCracks = 12;
  private static readonly float[] FractureHeartbeats = [0.15f, 0.6f, 0.95f, 1.22f, 1.42f, 1.58f, 1.7f];
  private static readonly Color FractureColor = new(255, 110, 35);
  private static readonly Color FractureHotColor = new(255, 236, 200);

  private readonly PlanetDamageTracker planetDamage = new();
  public double DamagePerMinute => planetDamage.PerMinute;

  private bool fractureActive;
  private float fractureTime;
  private float fractureCooldown;
  private float freshCrack;
  private long fractureSwallowed;
  private long eruptionTotal;
  private long eruptionRemaining;
  private float eruptionCarry;
  private int eruptionPower;
  // Shards released (or about to be) but not collected yet; saves count them as owned.
  private int shardsOwed;
  private float shardPopupTime = -1f;
  private Texture2D shardTexture;

  private sealed class ShardPickup
  {
    public Vector2 From, Control, To, Position, CollectFrom;
    public float Age, CollectAge;
    public bool Collecting;
  }

  private readonly List<ShardPickup> shardPickups = new();

  private struct ShipPush
  {
    public Transform2 Transform;
    public Vector2 From, To;
    public float StartRotation, Spin;
  }

  private readonly List<ShipPush> shipPushes = new();
  private readonly List<(Harvester Ship, Transform2 Transform)> pushedShips = new();
  private float shockwaveAge;

  public int CoreFractures => m_gameState.CoreFractures;
  // Everything but the planet and its gems holds still until the shard is out.
  public bool FracturePaused => fractureActive && fractureTime < FractureResume;
  public double NextFractureDamage => CoreFracture.Threshold(m_gameState.CoreFractures);
  private ulong OwedCoreShards => (ulong)Math.Max(0, shardsOwed);

  private void RecordPlanetDamage(double damage)
  {
    if (!m_prestiging && !m_postPrestige) planetDamage.Record(damage);
  }

  private void UpdateCoreFracture(float dt)
  {
    planetDamage.Update(dt);
    freshCrack = Math.Max(0f, freshCrack - dt);
    // The shard popup waits until the next fracture, if one has started, is over.
    if (shardPopupTime >= 0f && !fractureActive && (shardPopupTime += dt) >= ShardPopupSeconds)
      shardPopupTime = -1f;
    UpdateShardPickups(dt);
    if (fractureActive)
    {
      UpdateFractureEvent(dt);
      return;
    }
    fractureCooldown = Math.Max(0f, fractureCooldown - dt);
    if (!progressReady || !GameStarted || !PlanetMiningEnabled || m_prestiging || m_postPrestige
      || fractureCooldown > 0f || m_upgradeManager.UpdatingButtons || m_upgradeManager.UpgradeGuiEditMode)
      return;
    if (planetDamage.PerMinute >= NextFractureDamage) StartCoreFracture();
  }

  public void StartCoreFracture()
  {
    if (fractureActive || !PlanetMiningEnabled) return;
    fractureActive = true;
    fractureTime = 0f;
    fractureSwallowed = eruptionTotal = eruptionRemaining = 0;
    eruptionCarry = 0f;
    // The fracture and its shard count at once, so a save mid-event loses neither.
    m_gameState.CoreFractures++;
    ++shardsOwed;
    AddEventCrackNetwork();
    PulsePlanet(0.5f, 0.3f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: -0.6f);
    SaveProgress();
  }

  private void UpdateFractureEvent(float dt)
  {
    float previous = fractureTime;
    fractureTime += dt;
    bool Crossed(float moment) => previous < moment && fractureTime >= moment;

    for (int i = 0; i < FractureHeartbeats.Length; i++)
      if (Crossed(FractureHeartbeats[i]))
      {
        PulsePlanet(0.4f + 0.5f * i / (FractureHeartbeats.Length - 1f));
        if (i % 2 == 0)
          AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: -0.8f + 0.1f * i);
      }
    if (fractureTime < FractureSwallowStart)
      planetShake = Math.Max(planetShake, 0.15f + 0.75f * Smooth(0f, FractureSwallowStart, fractureTime));

    if (Crossed(FractureSwallowStart))
    {
      // Gravity turns inward: a ring of light collapses onto the planet.
      SpawnerEffects.Add(null, PlanetPos, FractureColor, PlanetRadius * 7f, PlanetRadius * 0.8f, 1.1f);
      SpawnerEffects.Add(null, PlanetPos, FractureHotColor, PlanetRadius * 4f, PlanetRadius * 0.6f, 0.8f);
      AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: -0.4f);
    }
    if (fractureTime >= FractureSwallowStart && fractureTime < FractureSwallowEnd)
    {
      // Keep swallowing, so gems the weapons knock loose meanwhile go back in too.
      fractureSwallowed += SwallowLooseGems();
      planetShake = Math.Max(planetShake, 0.7f);
      planetHitPulse = Math.Max(planetHitPulse, 0.35f + 0.4f * Smooth(FractureSwallowStart, FractureSwallowEnd, fractureTime));
    }
    if (fractureTime >= FractureSwallowEnd && fractureTime < FractureEruption)
    {
      planetShake = Math.Max(planetShake, 1f);
      planetHitPulse = Math.Max(planetHitPulse, Smooth(FractureSwallowEnd, FractureEruption, fractureTime));
    }

    if (Crossed(FractureEruption)) EruptCore();
    if (eruptionRemaining > 0) EmitEruption(dt);
    UpdateShockwave(dt);
    if (Crossed(FractureShardRelease)) ReleaseCoreShard();

    if (fractureTime >= FractureEnd && (eruptionRemaining <= 0 || fractureTime >= EruptionDeadline))
    {
      fractureActive = false;
      fractureCooldown = FractureCooldownSeconds;
    }
  }

  // Every unclaimed gem in the field flies into the planet and is gone. Start times
  // are staggered, so the field streams in rather than shrinking as one block.
  private long SwallowLooseGems()
  {
    var grid = HarvesterCollectionSystem.Instance?.flatSpatialHash;
    if (grid == null || UpdateSystem2.Instance == null) return 0;
    long swallowed = 0;
    for (int i = 0; i < grid.AllocatedSlotCount; i++)
    {
      ref var data = ref grid.Gems[i];
      if (!data.IsActive || data.ClaimState != 0) continue;
      var gem = UpdateSystem2.Instance.GetEntityP(data.EntityId)?.Get<Gem>();
      if (gem == null || !gem.IsLive || gem.PickedUp || gem.WasClicked || !grid.TryClaim(i)) continue;
      float stagger = Math.Max(0f, SwallowStaggerSeconds - (fractureTime - FractureSwallowStart));
      gem.Swallow(PlanetPos, PlanetRadius, Random.Shared.NextSingle() * stagger, SwallowFlightSeconds);
      ++swallowed;
    }
    return swallowed;
  }

  private void EruptCore()
  {
    eruptionPower = CoreDrill.Deeper(StrongestFirePower(), CoreFracture.EruptionLayers);
    eruptionTotal = eruptionRemaining = CoreFracture.EruptionGems(fractureSwallowed, planetDamage.PerMinute);
    eruptionCarry = 0f;
    freshCrack = FreshCrackSeconds;
    PulsePlanet(1f, 1f);
    planetExplosions.Add(new PlanetExplosion { Position = CrackFocusWorld(), Scale = 3f });
    planetExplosions.Add(new PlanetExplosion { Position = PlanetPos, Scale = 4f });
    SpawnerEffects.Add(null, PlanetPos, Color.White, PlanetRadius, PlanetRadius * 4f, 0.7f);
    SpawnerEffects.Add(null, PlanetPos, FractureColor, PlanetRadius, PlanetRadius * 7f, 1.2f);
    SpawnerEffects.Add(null, PlanetPos, CoreShards.Color, PlanetRadius * 1.2f, PlanetRadius * 10f, 1.6f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
    AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect, pitch: -0.5f);
    StartShockwave();
  }

  // The blast blows every ship in flight out toward the edge of the screen, tumbling.
  private void StartShockwave()
  {
    shipPushes.Clear();
    shockwaveAge = 0f;
    if (weaponBounds.Maximum == weaponBounds.Minimum) return;
    var margin = new Vector2(50f);
    var minimum = weaponBounds.Minimum + margin;
    var maximum = weaponBounds.Maximum - margin;
    float far = 0f;
    foreach (var corner in new[] { weaponBounds.Minimum, weaponBounds.Maximum,
      new Vector2(weaponBounds.Minimum.X, weaponBounds.Maximum.Y), new Vector2(weaponBounds.Maximum.X, weaponBounds.Minimum.Y) })
      far = Math.Max(far, Vector2.Distance(corner, PlanetPos));
    // The visible front that does the pushing.
    SpawnerEffects.Add(null, PlanetPos, Color.White, PlanetRadius, far, FractureShockwaveSeconds * 1.4f);

    HarvesterCollectionSystem.Instance?.CollectFlyingShips(pushedShips);
    foreach (var (_, transform) in pushedShips)
    {
      var from = transform.Position;
      var away = from - PlanetPos;
      away = away.LengthSquared() > 1f ? Vector2.Normalize(away) : PlanetDirection(Random.Shared.NextSingle() * MathHelper.TwoPi);
      // Out along the blast until the ship meets the edge of the play area.
      float reach = float.MaxValue;
      if (away.X > 0.001f) reach = Math.Min(reach, (maximum.X - from.X) / away.X);
      if (away.X < -0.001f) reach = Math.Min(reach, (minimum.X - from.X) / away.X);
      if (away.Y > 0.001f) reach = Math.Min(reach, (maximum.Y - from.Y) / away.Y);
      if (away.Y < -0.001f) reach = Math.Min(reach, (minimum.Y - from.Y) / away.Y);
      reach = Math.Max(0f, reach) * (0.8f + 0.2f * Random.Shared.NextSingle());
      shipPushes.Add(new ShipPush
      {
        Transform = transform,
        From = from,
        To = Vector2.Clamp(from + away * reach, minimum, maximum),
        StartRotation = transform.Rotation,
        Spin = (Random.Shared.Next(2) == 0 ? -1f : 1f) * MathHelper.Pi * (0.8f + 0.8f * Random.Shared.NextSingle()),
      });
    }
    pushedShips.Clear();
  }

  private void UpdateShockwave(float dt)
  {
    if (shipPushes.Count == 0) return;
    shockwaveAge += dt;
    // The blast front reaches nearer ships first, then each decelerates to a stop.
    float t = Math.Clamp(shockwaveAge / FractureShockwaveSeconds, 0f, 1f);
    float eased = 1f - (1f - t) * (1f - t) * (1f - t);
    foreach (var push in shipPushes)
    {
      push.Transform.Position = Vector2.Lerp(push.From, push.To, eased);
      push.Transform.Rotation = push.StartRotation + push.Spin * eased;
    }
    if (t >= 1f) shipPushes.Clear();
  }

  // The eruption streams out over about a second instead of in one frame.
  private void EmitEruption(float dt)
  {
    eruptionCarry += eruptionTotal / FractureEruptionSeconds * dt;
    int count = (int)Math.Min(Math.Min(eruptionCarry, eruptionRemaining), MaxEruptionGemsPerFrame);
    eruptionCarry -= count;
    for (int i = 0; i < count; i++)
    {
      if (!HasGemCapacity())
      {
        eruptionCarry = 0f;
        break;
      }
      SpawnRolledGem(SamplePlanetDebris(weaponBounds, eruptionPower, 10f), eruptionPower, fromPlanet: true);
      --eruptionRemaining;
    }
    planetShake = Math.Max(planetShake, 0.5f);
  }

  private void ReleaseCoreShard()
  {
    var start = CrackFocusWorld();
    var outward = start - PlanetPos;
    var direction = outward.LengthSquared() > 1f ? Vector2.Normalize(outward) : -Vector2.UnitY;
    float angle = MathF.Atan2(direction.Y, direction.X);
    // Out of the crack, then curving up to hover in view above the planet, beside
    // any shard still waiting there.
    var landing = PlanetPos + PlanetDirection(-MathHelper.PiOver2 + MathHelper.WrapAngle(angle + MathHelper.PiOver2) * 0.35f)
      * (PlanetRadius + 130f) + new Vector2((shardPickups.Count % 2 == 0 ? 1f : -1f) * 80f * ((shardPickups.Count + 1) / 2), 0f);
    var margin = new Vector2(70f);
    if (weaponBounds.Maximum.X - weaponBounds.Minimum.X > 2f * margin.X
      && weaponBounds.Maximum.Y - weaponBounds.Minimum.Y > 2f * margin.Y)
      landing = Vector2.Clamp(landing, weaponBounds.Minimum + margin, weaponBounds.Maximum - margin);
    shardPickups.Add(new ShardPickup
    {
      From = start,
      Control = start + direction * PlanetRadius * 2.4f,
      To = landing,
      Position = start,
    });
    SpawnerEffects.Add(null, start, CoreShards.Color, 4f, 70f, 0.5f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.UpgradeDoneEffect, pitch: 0.3f);
  }

  private float ShardClickRadius => 30f + 18f / Math.Max(0.3f, m_camera.Zoom);

  // The oldest Core Shard still waiting to be clicked, for capture pointers.
  internal Vector2? CaptureShardPosition
  {
    get
    {
      foreach (var shard in shardPickups)
        if (!shard.Collecting) return shard.Position;
      return null;
    }
  }

  private void UpdateShardPickups(float dt)
  {
    for (int i = shardPickups.Count - 1; i >= 0; i--)
    {
      var shard = shardPickups[i];
      shard.Age += dt;
      if (!shard.Collecting)
      {
        float flight = Math.Clamp(shard.Age / ShardFlightSeconds, 0f, 1f);
        float eased = 1f - (1f - flight) * (1f - flight);
        float u = 1f - eased;
        shard.Position = u * u * shard.From + 2f * u * eased * shard.Control + eased * eased * shard.To
          + (flight >= 1f ? new Vector2(0f, MathF.Sin(shard.Age * 2.6f) * 5f) : Vector2.Zero);
        bool clicked = WorldClickTriggered && flight > 0.4f
          && Vector2.Distance(gemPointerWorld, shard.Position) <= ShardClickRadius;
        if (clicked || shard.Age >= ShardFlightSeconds + CoreFracture.ShardAutoCollectSeconds)
        {
          shard.Collecting = true;
          shard.CollectFrom = shard.Position;
          SpawnerEffects.Add(null, shard.Position, CoreShards.Color, 6f, 50f, 0.35f);
          AudioManager.Instance.PlaySound(AudioManager.Instance.GemClickSoundEffect, pitch: 0.4f);
        }
        continue;
      }
      shard.CollectAge += dt;
      float t = Math.Clamp(shard.CollectAge / ShardCollectSeconds, 0f, 1f);
      shard.Position = Vector2.Lerp(shard.CollectFrom, HomeBasePos, t * t);
      if (t < 1f) continue;
      shardPickups.RemoveAt(i);
      CollectCoreShard();
    }
  }

  private void CollectCoreShard()
  {
    shardsOwed = Math.Max(0, shardsOwed - 1);
    m_gameState.CurrentCoreShardCount = PrestigeProgression.AddSaturating(m_gameState.CurrentCoreShardCount, 1);
    shardPopupTime = 0f;
    SpawnerEffects.Add(null, HomeBasePos, CoreShards.Color, 10f, 90f, 0.5f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.UpgradeDoneEffect);
    OnCoreShardCollected();
    m_upgradeManager.UpdateTooltipContent();
    SaveProgress();
  }

  private void ClearCoreFracture()
  {
    planetDamage.Reset();
    fractureActive = false;
    fractureTime = fractureCooldown = freshCrack = eruptionCarry = 0f;
    fractureSwallowed = eruptionTotal = eruptionRemaining = 0;
    shardsOwed = 0;
    shardPickups.Clear();
    shipPushes.Clear();
    shardPopupTime = -1f;
  }

  // ---- Drawing ----

  // Cracks spread around the planet by the golden angle, so they rarely overlap.
  private Vector2 PlanetShakeOffset()
  {
    float pulse = planetHitPulse * planetHitPulse;
    return new Vector2(MathF.Sin(planetAge * 91f), MathF.Sin(planetAge * 67f))
      * (2.5f * pulse + 9f * planetShake * planetShake);
  }

  // The whole planet glows hotter while it swallows the field.
  private void DrawFractureGlow()
  {
    if (!fractureActive || fractureTime >= FractureEruption || !PlanetMiningEnabled || planetConsumed) return;
    float feather = 1.25f / Math.Max(0.1f, m_camera.Zoom);
    float swell = Smooth(FractureSwallowStart, FractureEruption, fractureTime);
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    m_shapeBatch.FillCircle(PlanetPos + PlanetShakeOffset(), PlanetRadius * (1.05f + 0.25f * swell),
      FractureColor * (0.08f + 0.22f * swell), Math.Max(feather, PlanetRadius * 0.4f));
    m_shapeBatch.End();
  }

  private void DrawShardPickups()
  {
    if (shardPickups.Count == 0) return;
    shardTexture ??= AsyncContent.AssetManager.Load<Texture2D>(CoreShards.IconPath);
    float feather = 1.25f / Math.Max(0.1f, m_camera.Zoom);
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    foreach (var shard in shardPickups)
    {
      bool hovered = !shard.Collecting && Vector2.Distance(gemPointerWorld, shard.Position) <= ShardClickRadius;
      float glow = 0.8f + 0.2f * MathF.Sin(shard.Age * 5f) + (hovered ? 0.4f : 0f);
      m_shapeBatch.FillCircle(shard.Position, 34f, CoreShards.Color * (0.25f * glow), 28f);
      // Slowly turning rays make it read as a prize, not a gem.
      for (int ray = 0; ray < 6; ray++)
      {
        float angle = shard.Age * 0.8f + ray * MathHelper.TwoPi / 6f;
        m_shapeBatch.FillLine(shard.Position, shard.Position + PlanetDirection(angle) * (42f + 8f * glow),
          3f, CoreShards.Color * (0.18f * glow), Math.Max(feather, 6f));
      }
      if (!shard.Collecting && shard.Age >= ShardFlightSeconds)
      {
        // A ring closes in as the shard's time to be clicked runs out.
        float left = 1f - Math.Clamp((shard.Age - ShardFlightSeconds) / CoreFracture.ShardAutoCollectSeconds, 0f, 1f);
        m_shapeBatch.BorderCircle(shard.Position, 22f + 22f * left, CoreShards.Color * 0.5f, 1.5f, feather);
      }
    }
    m_shapeBatch.End();

    int frameWidth = shardTexture.Width / CoreShards.IconFrames;
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
      transformMatrix: m_camera.GetViewMatrix());
    foreach (var shard in shardPickups)
    {
      int frame = (int)(shard.Age / 0.09f) % CoreShards.IconFrames;
      float scale = shard.Collecting ? 2.6f * (1f - 0.6f * Math.Clamp(shard.CollectAge / ShardCollectSeconds, 0f, 1f)) : 2.6f;
      m_spriteBatch.Draw(shardTexture, shard.Position, new Rectangle(frame * frameWidth, 0, frameWidth, shardTexture.Height),
        Color.White, 0f, new Vector2(frameWidth / 2f, shardTexture.Height / 2f), scale, SpriteEffects.None, 0f);
    }
    m_spriteBatch.End();
  }

  // HUD: the popup after collecting a shard. The fracture itself needs no words.
  private void DrawCoreFractureHud()
  {
    if (GameMain.IsPaused || RenderGuiSystem.Instance.DrawingPopout || !PlanetMiningEnabled) return;
    bool overlay = RenderGuiSystem.Instance.IsOverlayVisible;
    float centerX = HudLayout.Width / 2f;
    if (shardPopupTime >= 0f && !overlay && !fractureActive)
    {
      float fadeIn = Math.Clamp(shardPopupTime / 0.2f, 0f, 1f);
      float alpha = Math.Min(fadeIn, Math.Clamp((ShardPopupSeconds - shardPopupTime) / 0.25f, 0f, 1f));
      float y = 230f + (1f - fadeIn) * -24f;
      var panel = new Rectangle((int)(centerX - 460), (int)y - 52, 920, 196);
      m_spriteBatch.Begin();
      m_spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, panel, OrbitSkin.PanelBackgroundTint * (0.92f * alpha));
      OrbitSkin.NineSlice(m_spriteBatch, "modal_info_complete", panel, 8, alpha);
      m_spriteBatch.End();
      ulong shards = m_gameState.CurrentCoreShardCount;
      DrawCenteredNotification("+1 CORE SHARD", centerX, y, 40f, CoreShards.Color * alpha, Color.Black * alpha);
      DrawCenteredNotification($"{shards} {(shards == 1 ? "Core Shard" : CoreShards.Name)} to spend", centerX, y + 52f, 32f,
        OrbitSkin.StatHeadingColor * alpha, Color.Black * alpha);
      DrawCenteredNotification("Buy gold-ringed upgrades in Upgrades", centerX, y + 98f, 24f,
        OrbitSkin.MutedTextColor * alpha, Color.Black * alpha);
    }
  }
}
