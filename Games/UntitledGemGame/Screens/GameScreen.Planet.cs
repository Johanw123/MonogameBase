using System;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

// Gems are mined out of a planet. Main ship weapons (GameScreen.Weapons.cs) hit
// it; every hit knocks gems loose, and they fly from the rim to a debris ring
// whose reach grows with the weapon's fire power. Fire power also decides which
// colors come out (GemQualityTable). Ships treat the planet as an obstacle
// (PlanetObstacle).
public partial class UntitledGemGameGameScreen
{
  public static bool PlanetMiningEnabled = true;
  public static Vector2 PlanetPos;

  private const float BasePlanetScale = 3f;
  // Planet maths (cracks, shell, craters, radius) works in texels of a 96-texel frame.
  private const int PlanetFrameSize = 96;
  private const int PlanetFrameCount = 77;
  private const float PlanetFrameSeconds = 0.14f;
  // The drawn sprite: one revolution prerendered from the Synty crystal planet
  // (tools/blender/prerender.py, render_planet_spin), 77 frames of 288 px in a
  // 9-column grid, with the disc where the 96-texel frame expects it.
  private const int PlanetSheetFrameSize = 288;
  private const int PlanetSheetColumns = 9;
  // Expand Space enlarges the base planet; each core fracture swells it further.
  // Ships, weapons, debris and clicks share the size; the sprite balloons out to it.
  public static float PlanetSizeMultiplier { get; private set; } = 1f;
  private static float PlanetScale => BasePlanetScale * PlanetSizeMultiplier;
  // The opaque disc spans texels 17-79 of each 96 px frame.
  public static float PlanetRadius => 31f * PlanetScale;
  // Manual tuning for planet brightness and blue crystal glow into the bloom pass:
  // - PlanetBaseBrightness: overall planet brightness (0.70 = default dim so rock stays crisp; higher = more bloom).
  // - PlanetBlueGlow: subtle additive cyan-blue glow on the planet (0.0 = off, 0.06 = subtle glow).
  public static float PlanetBaseBrightness = 0.69f;
  public static float PlanetBlueGlow = 0.626f;
  private static readonly Color PlanetTint = new(250, 250, 255);
  private static readonly Color PlanetCrystalGlowTint = new(50, 190, 255);
  // Knocked-loose gems land in a ring around the planet. The ring starts close
  // and widens with fire power until, at FullReachFirePower, it covers the screen.
  public const float PlanetDebrisGap = 30f;
  private const float StartReachFraction = 0.06f;
  private const float ReachProgressExponent = 1.5f;
  private const int FullReachFirePower = 27;

  private float planetAge;
  private float planetHitPulse;
  private float planetShake;
  private bool pendingPlanetToggle;

  private void PlaceHomeBaseAndPlanet()
  {
    var center = m_camera.ScreenToWorld(BaseGame.ViewportCenter);
    var home = m_homeBaseEntity?.Get<Harvester>();
    float width = 29.2f;
    if (home != null)
      width *= m_homeBaseEntity.Get<MonoGame.Extended.Transform2>().Scale.X
        * (home.VisualCollectionRangeMultiplier ?? BaseStats.GetHarvesterCollectionRangeMultiplier(home));
    var offsets = PlanetPresentation.HorizontalOffsets(m_camera.Zoom / RenderQuality.WorldScale,
      31f * BasePlanetScale * planetVisualSize, width);
    HomeBasePos = PlanetMiningEnabled ? center + new Vector2(offsets.Ship, 0f) : center;
    PlanetPos = center + new Vector2(offsets.Planet, 0f) + IntroPlanetOffset;
  }

  private void UpdatePlanetLayout()
  {
    if (!GameStarted || loopArrival || m_prestiging || m_postPrestige) return;
    PlaceHomeBaseAndPlanet();
    var transform = m_homeBaseEntity?.Get<MonoGame.Extended.Transform2>();
    if (transform != null) transform.Position = HomeBasePos;
  }

  private static bool IsOnPlanet(Vector2 position, float margin = 0f)
    => PlanetMiningEnabled
      && Vector2.DistanceSquared(position, PlanetPos) <= (PlanetRadius + margin) * (PlanetRadius + margin);

  // Resting gems stay off the planet so it reads as their source, not a backdrop.
  private static Vector2 MoveOffPlanet(Vector2 position)
    => PlanetMiningEnabled
      ? PlanetObstacle.PushOut(position, PlanetPos, PlanetRadius + PlanetDebrisGap)
      : position;

  private static Vector2 PlanetDirection(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

  // Surface marks use the exact same stepped rotation as the prerendered sprite.
  private float PlanetRotation => FrameRotation(PlanetFrame(planetAge));

  // Map a world hit to the planet's own coordinates, undoing the displayed frame's
  // rotation once. Marks then stay attached through weapon pauses and frame wraps.
  private Vector3 WorldToPlanetNormal(Vector2 worldPos)
  {
    float radius = CraterDiscRadius * PlanetSpriteScale();
    var xy = (worldPos - PlanetPos - PlanetShakeOffset()) / Math.Max(1f, radius);
    float lenSq = xy.LengthSquared();
    if (lenSq > 0.94f * 0.94f)
      xy *= 0.94f / MathF.Sqrt(lenSq);
    return RotateWithPlanet(new Vector3(xy.X, xy.Y,
      MathF.Sqrt(Math.Max(0f, 1f - xy.LengthSquared()))), -PlanetRotation);
  }

  // Rotates a surface normal about the planet's vertical spin axis (front face moves left to right).
  private static Vector3 RotateWithPlanet(Vector3 normal, float angle)
  {
    float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
    return new Vector3(normal.X * cos + normal.Z * sin, normal.Y, -normal.X * sin + normal.Z * cos);
  }

  private Vector2 PlanetNormalToWorld(Vector3 normal)
    => PlanetNormalToDrawn(PlanetPos + PlanetShakeOffset(), PlanetSpriteScale(), normal);

  private Vector2 PlanetNormalToDrawn(Vector2 center, float texel, Vector3 normal)
  {
    var view = RotateWithPlanet(normal, PlanetRotation);
    return center + new Vector2(view.X, view.Y) * (CraterDiscRadius * texel);
  }

  // The side of the planet that faces the homebase, where weapons hit.
  private static float PlanetFacingAngle()
    => MathF.Atan2(HomeBasePos.Y - PlanetPos.Y, HomeBasePos.X - PlanetPos.X);

  private static Vector2 PlanetFacingPoint(float halfSpread)
    => PlanetPos + PlanetDirection(PlanetFacingAngle() + (Random.Shared.NextSingle() * 2f - 1f) * halfSpread)
      * PlanetRadius * 0.9f;

  public static float PlanetDebrisReach(PlayAreaBounds bounds, int firePower, float reachScale = 1f)
  {
    float far = 0f;
    foreach (var corner in new[] { bounds.Minimum, bounds.Maximum,
      new Vector2(bounds.Minimum.X, bounds.Maximum.Y), new Vector2(bounds.Maximum.X, bounds.Minimum.Y) })
      far = Math.Max(far, Vector2.Distance(corner, PlanetPos));
    float inner = PlanetRadius + PlanetDebrisGap;
    float power = Math.Clamp(MathF.Log(Math.Max(1, firePower)) / MathF.Log(FullReachFirePower), 0f, 1f);
    // Keep the starter cannon's debris close to the planet, then open the field
    // progressively as fire power grows. The old linear use of logarithmic power
    // granted most of the visible reach during the first few cheap ranks.
    float reachProgress = MathF.Pow(power, ReachProgressExponent);
    float fraction = Math.Clamp(MathHelper.Lerp(StartReachFraction, 1f, reachProgress) * reachScale, 0.05f, 1f);
    return inner + Math.Max(0f, far - inner) * fraction;
  }

  // A landing spot in the debris ring. Weapons can scale its reach and aim it
  // (facing +- spread radians) at the side they hit.
  public static Vector2 SamplePlanetDebris(PlayAreaBounds bounds, int firePower, float reachScale = 1f,
    float? facing = null, float spread = MathF.PI)
  {
    float inner = PlanetRadius + PlanetDebrisGap;
    float outer = Math.Max(inner + 1f, PlanetDebrisReach(bounds, firePower, reachScale));
    for (int attempt = 0; attempt < 8; attempt++)
    {
      float angle = facing.HasValue
        ? facing.Value + (Random.Shared.NextSingle() * 2f - 1f) * spread
        : Random.Shared.NextSingle() * MathHelper.TwoPi;
      var dir = PlanetDirection(angle);
      float maxDist = outer;
      if (dir.X > 0.0001f) maxDist = Math.Min(maxDist, (bounds.Maximum.X - PlanetPos.X) / dir.X);
      else if (dir.X < -0.0001f) maxDist = Math.Min(maxDist, (bounds.Minimum.X - PlanetPos.X) / dir.X);
      if (dir.Y > 0.0001f) maxDist = Math.Min(maxDist, (bounds.Maximum.Y - PlanetPos.Y) / dir.Y);
      else if (dir.Y < -0.0001f) maxDist = Math.Min(maxDist, (bounds.Minimum.Y - PlanetPos.Y) / dir.Y);
      if (maxDist <= inner) continue;
      // Uniform over the ring's area, so its outer part is not sparse.
      float distance = MathF.Sqrt(MathHelper.Lerp(inner * inner, maxDist * maxDist, Random.Shared.NextSingle()));
      var position = PlanetPos + dir * distance;
      if (bounds.Clamp(position) == position) return position;
    }
    // Most of a full-reach ring lies off screen; any on-screen spot will do.
    return MoveOffPlanet(RandomHelper.Vector2(bounds.Minimum, bounds.Maximum));
  }

  // Start a gem at the weapon's impact site on the planet (or on the rim facing its
  // landing spot when untargeted). Launch motion decays exponentially, so
  // velocity * (1 / damping) is exactly the distance covered.
  private static (Vector2 Origin, Vector2 Velocity) PlanetLaunch(Vector2 landing, float? launchAngle = null)
  {
    Vector2 direction;
    if (launchAngle.HasValue)
    {
      float jitter = (Random.Shared.NextSingle() * 2f - 1f) * 0.18f;
      direction = PlanetDirection(launchAngle.Value + jitter);
    }
    else
    {
      direction = landing - PlanetPos;
      direction = direction.LengthSquared() > 0.01f ? Vector2.Normalize(direction) : -Vector2.UnitX;
    }
    var origin = PlanetPos + direction * (PlanetRadius * 0.88f);
    return (origin, (landing - origin) * Gem.LaunchDamping);
  }

  // Gems the field can still take, counting gems held for shots in flight.
  private int PlanetGemRoom()
  {
    long used = (long)HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems
      + m_entityFactory.PendingGemSpawnCount + pendingPlanetGems + deferredDebris.Count;
    return (int)Math.Clamp(SignalStats.GemLimit - used, 0, int.MaxValue);
  }

  // A hit deals damage to the planet (CoreFracture), counted by source for the Damage
  // panel (PlanetDamageMeter); each point knocks one gem loose, which flies from the
  // rim to its own spot in the debris ring. A full field takes the damage without
  // spilling gems. Weapon hits feel the Core Drill's resonance and tunnels; raw damage
  // (the drill's own gems, Fault Scan's share of recent damage) does not. Returns the
  // damage dealt.
  private int KnockGemsLoose(PlanetDamageSource source, int damage, int firePower, PlayAreaBounds bounds,
    float reachScale = 1f, float? facing = null, float spread = MathF.PI, float valueMultiplier = 1f,
    bool raw = false, int bonusPercent = 0)
  {
    if (!raw)
    {
      firePower = WeaponHitPower(firePower);
      damage = WeaponHitYield(damage);
    }
    RecordPlanetDamage(source, damage);
    OnPlanetHit(source, facing, damage, firePower);
    reachScale = PrestigeTalentEffects.PlanetDebrisReachScale(reachScale);
    bool overdrive = weaponOverdriveMultiplier > 1.001f;
    int samplePower = overdrive ? Math.Max(firePower, FullReachFirePower) : firePower;
    float? sampleFacing = overdrive ? PlanetFacingAngle() : facing;
    float sampleSpread = overdrive ? 1.42f : spread;
    float? launch = overdrive ? null : facing;
    for (int spawned = 0; spawned < damage && HasGemCapacity(); spawned++)
      SpawnRolledGem(SamplePlanetDebris(bounds, samplePower, reachScale, sampleFacing, sampleSpread),
        firePower, valueMultiplier, fromPlanet: true, bonusPercent, launchAngle: launch);
    return Math.Max(0, damage);
  }

  // A chunk breaks off: its gems fly out together and land as one cluster.
  private void KnockClusterLoose(PlanetDamageSource source, int damage, int firePower, PlayAreaBounds bounds,
    float reachScale = 1f, float? facing = null, float spread = MathF.PI)
  {
    firePower = WeaponHitPower(firePower);
    int gems = WeaponHitYield(damage);
    RecordPlanetDamage(source, gems);
    OnPlanetHit(source, facing, gems, firePower);
    reachScale = PrestigeTalentEffects.PlanetDebrisReachScale(reachScale);
    if (weaponOverdriveMultiplier > 1.001f && gems > 36)
    {
      int samplePower = Math.Max(firePower, FullReachFirePower);
      float baseFacing = PlanetFacingAngle();
      int remaining = gems;
      while (remaining > 0 && HasGemCapacity())
      {
        int batch = Math.Min(remaining, 24);
        remaining -= batch;
        var batchCenter = SamplePlanetDebris(bounds, samplePower, Math.Max(reachScale, 1.25f), baseFacing, 1.42f);
        float batchRadius = Math.Min(175f, BaseStats.ClusterRadius * MathF.Sqrt(Math.Max(1, batch) / 4f));
        batchCenter = MoveOffPlanet(bounds.Inset(batchRadius * 0.5f).Clamp(batchCenter));
        for (int i = 0; i < batch && HasGemCapacity(); i++)
        {
          var offset = PlanetDirection(Random.Shared.NextSingle() * MathHelper.TwoPi)
            * MathF.Sqrt(Random.Shared.NextSingle()) * batchRadius;
          SpawnRolledGem(bounds.Clamp(batchCenter + offset), firePower, fromPlanet: true, launchAngle: null);
        }
      }
      return;
    }
    var center = SamplePlanetDebris(bounds, firePower, reachScale, facing, spread);
    float radius = Math.Min(160f, BaseStats.ClusterRadius * MathF.Sqrt(Math.Max(1, gems) / 6f));
    center = MoveOffPlanet(bounds.Inset(radius * 0.7f).Clamp(center));
    for (int i = 0; i < gems && HasGemCapacity(); i++)
    {
      var offset = PlanetDirection(Random.Shared.NextSingle() * MathHelper.TwoPi)
        * MathF.Sqrt(Random.Shared.NextSingle()) * radius;
      SpawnRolledGem(bounds.Clamp(center + offset), firePower, fromPlanet: true, launchAngle: facing);
    }
  }

  private void PulsePlanet(float flash, float shake = 0f)
  {
    planetHitPulse = Math.Min(1f, planetHitPulse + flash);
    planetShake = Math.Max(planetShake, shake);
  }

  private void UpdatePlanet(float dt, Vector2 minimumPosition, Vector2 maximumPosition)
  {
    if (!PlanetMiningEnabled) return;
    planetAge += dt;
    planetHitPulse = Math.Max(0f, planetHitPulse - dt * 5f);
    planetShake = Math.Max(0f, planetShake - dt * 1.4f);
    UpdatePlanetLayout();
    UpdateWeapons(dt, new PlayAreaBounds(minimumPosition, maximumPosition));
  }

  private void DrawPlanet()
  {
    if (!PlanetMiningEnabled) return;
    // Between time loops the planet is gone; only its black hole remains (GameScreen.TimeLoop.cs).
    if (!planetConsumed && PlanetLoopScale() > 0.01f)
    {
      // Until it bursts, the shell hides the planet (GameScreen.PlanetShell.cs).
      if (!PlanetShelled || !DrawPlanetShell())
      {
        DrawPlanetSprite();
        DrawFractureGlow();
      }
      DrawShellFragments();
    }
  }

  // World units per planet texel as drawn: swelling, hit pulses and the time loop included.
  private float PlanetSpriteScale()
    => BasePlanetScale * planetVisualSize * (1f + 0.02f * planetHitPulse * planetHitPulse) * PlanetLoopScale();

  private void DrawPlanetSprite()
  {
    if (TextureCache.Planet?.IsLoaded != true || TextureCache.Planet.IsFailed) return;
    var texture = TextureCache.Planet.Value;
    int frame = PlanetFrame(planetAge);
    var source = new Rectangle(frame % PlanetSheetColumns * PlanetSheetFrameSize,
      frame / PlanetSheetColumns * PlanetSheetFrameSize, PlanetSheetFrameSize, PlanetSheetFrameSize);
    var sheetOrigin = new Vector2(PlanetSheetFrameSize / 2f);
    float pulse = planetHitPulse * planetHitPulse;
    var shake = PlanetShakeOffset();
    // World units per planet texel; the sheet has PlanetSheetFrameSize pixels per frame.
    float scale = PlanetSpriteScale();
    float sheetScale = scale * PlanetFrameSize / PlanetSheetFrameSize;

    Color baseTint = PlanetTint * PlanetBaseBrightness;
    baseTint.A = 255;
    // A core extraction cracks, heats and crushes it in one shader (GameScreen.TimeLoop.cs).
    if (DrawCollapsingPlanet(texture, source, frame, PlanetPos + shake, sheetOrigin, sheetScale, baseTint))
      return;
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
      transformMatrix: m_camera.GetViewMatrix());
    m_spriteBatch.Draw(texture, PlanetPos + shake, source, baseTint, 0f, sheetOrigin, sheetScale, SpriteEffects.None, 0f);
    m_spriteBatch.End();
    DrawPlanetCracks(frame, PlanetPos + shake, new Vector2(PlanetFrameSize / 2f), scale);

    float crystalGlow = PlanetBlueGlow * (0.90f + 0.10f * MathF.Sin(planetAge * 2.2f));
    if (crystalGlow <= 0.001f && pulse <= 0.01f) return;
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
      transformMatrix: m_camera.GetViewMatrix());
    if (crystalGlow > 0.001f)
      m_spriteBatch.Draw(texture, PlanetPos + shake, source, PlanetCrystalGlowTint * crystalGlow, 0f, sheetOrigin, sheetScale,
        SpriteEffects.None, 0f);
    if (pulse > 0.01f)
      m_spriteBatch.Draw(texture, PlanetPos + shake, source, baseTint * (0.3f * pulse), 0f, sheetOrigin, sheetScale,
        SpriteEffects.None, 0f);
    m_spriteBatch.End();
  }
}
