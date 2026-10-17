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

  private void UpdateShipThrusterParticles(float dt)
  {
    if (!GameStarted || loopArrival || m_prestiging || m_postPrestige || !gameplayParticlesEnabled)
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
    // Round robin keeps a large fleet bounded to eight nozzle bursts per tick,
    // leaving the shared gameplay budget available for weapon contacts.
    int emitted = 0;
    for (int i = 0; i < Math.Min(4, fleet.Count) && emitted < 8; i++)
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
      bool boosted = ship.LaunchThrusterTimeRemaining > 0 || ship.OverdriveTimeRemaining > 0;
      emitted += EmitThrusterParticles(ship.Type, transform.Position, transform.Rotation,
        transform.Scale * size, power, boosted, 8 - emitted);
    }
  }

  private int EmitThrusterParticles(Harvester.HarvesterType type, Vector2 position,
    float rotation, Vector2 scale, float power, bool boosted, int maxNozzles = 8)
  {
    float exhaustAngle = rotation + MathHelper.PiOver2;
    int nozzle = 0;
    for (; nozzle < maxNozzles; nozzle++)
    {
      if (!PrerenderedShips.TryGetParticleNozzle(type, rotation, scale, nozzle, out var offset, out float width)) break;
      float size = Math.Clamp(width, 0.4f, 3f);
      var color = (thrusterParticleBeat + nozzle) % 3 == 0
        ? new Color(190, 245, 255) : new Color(45, 165, 255);
      EmitGameplayParticles(new GpuParticleBurst(position + offset, boosted ? 7 : 4, color * power)
      {
        Direction = exhaustAngle, Spread = boosted ? 0.35f : 0.55f,
        Speed = new Vector2(28, boosted ? 115 : 75) * MathF.Sqrt(size),
        Lifetime = new(0.12f, boosted ? 0.42f : 0.3f),
        Size = new Vector2(0.55f, 1.3f) * size, Radius = size * 0.35f, Streak = true
      });
    }
    return nozzle;
  }
}
