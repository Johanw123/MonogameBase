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
    int nozzle = 0;
    for (; nozzle < maxNozzles; nozzle++)
    {
      if (!PrerenderedShips.TryCreateThrusterBurst(type, position, rotation, scale,
        nozzle, thrusterParticleBeat, power, boosted, out var burst))
        break;
      EmitGameplayParticles(burst);
    }
    return nozzle;
  }
}
