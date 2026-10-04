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

  // World units from the screen centre. The camera zooms around the centre, so
  // the ship-left / planet-right composition holds at every Expand Space level.
  private const float PlanetHomeBaseOffsetX = -240f;
  private const float PlanetOffsetX = 190f;
  private const float PlanetScale = 3f;
  private const int PlanetFrameSize = 96;
  private const int PlanetFrameCount = 77;
  private const float PlanetFrameSeconds = 0.14f;
  // The opaque disc spans texels 17-79 of each 96 px frame.
  public const float PlanetRadius = 31f * PlanetScale;
  // The world layer is bloomed; dim the sprite so its colours survive.
  private static readonly Color PlanetTint = new(180, 180, 190);
  // Knocked-loose gems land in a ring around the planet. The ring starts close
  // and widens with fire power until, at FullReachFirePower, it covers the screen.
  public const float PlanetDebrisGap = 30f;
  private const float StartReachFraction = 0.15f;
  private const int FullReachFirePower = 27;

  private float planetAge;
  private float planetHitPulse;
  private float planetShake;
  private bool pendingPlanetToggle;

  private void PlaceHomeBaseAndPlanet()
  {
    var center = m_camera.ScreenToWorld(BaseGame.ViewportCenter);
    HomeBasePos = PlanetMiningEnabled ? center + new Vector2(PlanetHomeBaseOffsetX, 0f) : center;
    PlanetPos = center + new Vector2(PlanetOffsetX, 0f);
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
    float fraction = Math.Clamp(MathHelper.Lerp(StartReachFraction, 1f, power) * reachScale, 0.05f, 1f);
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
      // Uniform over the ring's area, so its outer part is not sparse.
      float distance = MathF.Sqrt(MathHelper.Lerp(inner * inner, outer * outer, Random.Shared.NextSingle()));
      var position = PlanetPos + PlanetDirection(angle) * distance;
      if (bounds.Clamp(position) == position) return position;
    }
    // Most of a full-reach ring lies off screen; any on-screen spot will do.
    return MoveOffPlanet(RandomHelper.Vector2(bounds.Minimum, bounds.Maximum));
  }

  // Start a gem on the rim facing its landing spot. Launch motion decays
  // exponentially, so velocity * (1 / damping) is exactly the distance covered.
  private static (Vector2 Origin, Vector2 Velocity) PlanetLaunch(Vector2 landing)
  {
    var direction = landing - PlanetPos;
    direction = direction.LengthSquared() > 0.01f ? Vector2.Normalize(direction) : -Vector2.UnitX;
    var origin = PlanetPos + direction * (PlanetRadius + 2f);
    return (origin, (landing - origin) * Gem.LaunchDamping);
  }

  // Gems the field can still take, counting gems promised by shots in flight.
  private int PlanetGemRoom()
  {
    long used = (long)HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems
      + m_entityFactory.PendingGemSpawnCount + pendingPlanetGems;
    return (int)Math.Clamp(SignalStats.GemLimit - used, 0, int.MaxValue);
  }

  // Each gem flies from the rim to its own spot in the debris ring.
  private void KnockGemsLoose(int gems, int firePower, PlayAreaBounds bounds, float reachScale = 1f,
    float? facing = null, float spread = MathF.PI, float valueMultiplier = 1f)
  {
    for (int i = 0; i < gems && HasGemCapacity(); i++)
      SpawnRolledGem(SamplePlanetDebris(bounds, firePower, reachScale, facing, spread),
        firePower, valueMultiplier, fromPlanet: true);
  }

  // A chunk breaks off: its gems fly out together and land as one cluster.
  private void KnockClusterLoose(int gems, int firePower, PlayAreaBounds bounds, float reachScale = 1f,
    float? facing = null, float spread = MathF.PI)
  {
    var center = SamplePlanetDebris(bounds, firePower, reachScale, facing, spread);
    float radius = Math.Min(160f, BaseStats.ClusterRadius * MathF.Sqrt(Math.Max(1, gems) / 6f));
    for (int i = 0; i < gems && HasGemCapacity(); i++)
    {
      var offset = PlanetDirection(Random.Shared.NextSingle() * MathHelper.TwoPi)
        * MathF.Sqrt(Random.Shared.NextSingle()) * radius;
      SpawnRolledGem(bounds.Clamp(center + offset), firePower, fromPlanet: true);
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
    UpdateWeapons(dt, new PlayAreaBounds(minimumPosition, maximumPosition));
  }

  private void DrawPlanet()
  {
    if (!PlanetMiningEnabled) return;
    // Between time loops the planet is gone; only its black hole remains (GameScreen.TimeLoop.cs).
    if (!planetConsumed && PlanetLoopScale() > 0.01f) DrawPlanetSprite();
    DrawTimeLoopEffects();
  }

  private void DrawPlanetSprite()
  {
    if (TextureCache.Planet?.IsLoaded != true || TextureCache.Planet.IsFailed) return;
    var texture = TextureCache.Planet.Value;
    int frame = (int)(planetAge / PlanetFrameSeconds) % PlanetFrameCount;
    var source = new Rectangle(frame * PlanetFrameSize, 0, PlanetFrameSize, PlanetFrameSize);
    var origin = new Vector2(PlanetFrameSize / 2f);
    float pulse = planetHitPulse * planetHitPulse;
    var shake = new Vector2(MathF.Sin(planetAge * 91f), MathF.Sin(planetAge * 67f))
      * (2.5f * pulse + 9f * planetShake * planetShake);
    float scale = PlanetScale * (1f + 0.02f * pulse) * PlanetLoopScale();

    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
      transformMatrix: m_camera.GetViewMatrix());
    m_spriteBatch.Draw(texture, PlanetPos + shake, source, PlanetTint, 0f, origin, scale, SpriteEffects.None, 0f);
    m_spriteBatch.End();

    if (pulse <= 0.01f) return;
    // Additive second pass brightens the planet's own colours on impact.
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
      transformMatrix: m_camera.GetViewMatrix());
    m_spriteBatch.Draw(texture, PlanetPos + shake, source, PlanetTint * (0.3f * pulse), 0f, origin, scale,
      SpriteEffects.None, 0f);
    m_spriteBatch.End();
  }
}
