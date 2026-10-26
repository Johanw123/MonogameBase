using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private float thrusterParticleClock;
  private int thrusterParticleShip, thrusterParticleBeat;
  private float thrusterParticleMultiplier = 1f;

  internal void SetThrusterParticleMultiplier(float multiplier)
  {
    thrusterParticleMultiplier = Math.Clamp(multiplier, 0f, 5000f);
    if (thrusterParticleMultiplier > 1f && particlePoolChoice < 3)
      SetParticlePoolChoice(3);
  }

  private void UpdateShipThrusterParticles(float dt)
  {
    if (!GameStarted || loopArrival || m_prestiging || m_postPrestige || !gameplayParticlesEnabled || thrusterParticleMultiplier <= 0f)
    {
      thrusterParticleClock = 0;
      return;
    }
    thrusterParticleClock += dt;
    if (thrusterParticleClock < 1f / 30f) return;
    thrusterParticleClock %= 1f / 30f;
    if (GetParticleSystem() == null) return;
    thrusterParticleBeat++;
    var fleet = HarvesterCollectionSystem.Instance?._harvesters;
    if (fleet == null || fleet.Count == 0) return;
    bool overdrive = thrusterParticleMultiplier > 1.001f;
    int maxShips = overdrive ? Math.Min(16, fleet.Count) : Math.Min(4, fleet.Count);
    int maxNozzlesTotal = overdrive ? 24 : 8;
    // Round robin keeps a large fleet bounded to eight nozzle bursts per tick,
    // leaving the shared gameplay budget available for weapon contacts.
    int emitted = 0;
    for (int i = 0; i < maxShips && emitted < maxNozzlesTotal; i++)
    {
      thrusterParticleShip %= fleet.Count;
      var entity = m_escWorld.GetEntity(fleet[thrusterParticleShip++]);
      var ship = entity.Get<Harvester>();
      if (ship.MarkedForDestroy || ship.Type == Harvester.HarvesterType.HomeBase
        || ship.CurrentState is not (Harvester.HarvesterState.Collecting or Harvester.HarvesterState.Refueling))
        continue;
      float power = ship.CurrentState == Harvester.HarvesterState.Refueling
        ? Math.Clamp((float)ship.refuelProgressPercent / 100f, 0, 1) : 1f;
      if (power <= 0.02f) continue;
      var transform = entity.Get<Transform2>();
      // Use the same smoothed collection scale as the hull renderer.
      float size = ship.VisualCollectionRangeMultiplier ?? BaseStats.GetHarvesterCollectionRangeMultiplier(ship);
      bool boosted = ship.LaunchThrusterTimeRemaining > 0 || ship.OverdriveTimeRemaining > 0 || overdrive;
      emitted += EmitThrusterParticles(ship.Type, transform.Position, transform.Rotation,
        transform.Scale * size, power, boosted, maxNozzlesTotal - emitted, overdrive, ship.Id);
    }
  }

  private int EmitThrusterParticles(Harvester.HarvesterType type, Vector2 position,
    float rotation, Vector2 scale, float power, bool boosted, int maxNozzles = 8, bool overdrive = false, int shipId = 0)
  {
    int nozzle = 0;
    for (; nozzle < maxNozzles; nozzle++)
    {
      if (!PrerenderedShips.TryCreateThrusterBurst(type, position, rotation, scale,
        nozzle, thrusterParticleBeat, power, boosted, out var burst))
        break;
      if (overdrive)
      {
        var particles = GetParticleSystem();
        if (particles == null) break;
        float mult = Math.Min(thrusterParticleMultiplier, 180f);
        float logMult = MathF.Log10(thrusterParticleMultiplier);
        float speedScale = Math.Clamp(1f + 0.85f * logMult, 1.4f, 3.6f);
        int count = Math.Clamp((int)MathF.Round(burst.Count * mult), 48, 950);
        var color = (Math.Abs(shipId) % 4) switch
        {
          0 => new Color(35, 215, 255),
          1 => new Color(65, 115, 255),
          2 => new Color(185, 65, 255),
          _ => new Color(255, 75, 175)
        };
        var tinted = color * power;
        tinted.A = (byte)Math.Clamp((int)MathF.Round(76f / MathF.Pow(mult / 25f, 0.16f)), 48, 76);
        float minSpeed = 240f;
        float maxSpeed = Math.Clamp(420f + 190f * logMult, 650f, 1080f);
        particles.Emit(burst with
        {
          Count = count,
          Color = tinted,
          Speed = new Vector2(minSpeed, maxSpeed),
          Lifetime = new Vector2(0.75f, 1.50f),
          Size = new Vector2(0.65f, 1.55f),
          Radius = Math.Max(burst.Radius * 1.8f, 3.5f),
          Spread = Math.Clamp(burst.Spread * 0.75f, 0.20f, 0.42f),
          Streak = true
        });
      }
      else
      {
        EmitGameplayParticles(burst);
      }
    }
    return nozzle;
  }
}
