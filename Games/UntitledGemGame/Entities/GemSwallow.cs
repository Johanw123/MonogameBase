using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities;

// A core fracture pulls the gem back into the planet (GameScreen.CoreFracture.cs): its
// launch played in reverse. It creeps off its spot, then rushes faster and faster to
// the rim, and shrinks away as it sinks below the surface.
public partial class Gem
{
  // How sharply the pull accelerates: low enough that the gem visibly travels before
  // it rushes in (a launch decelerates far harder).
  private const float SwallowAcceleration = 2.5f;
  private bool swallowing;
  private Vector2 swallowFrom, swallowTo, swallowCenter, swallowScale;
  private float swallowAge, swallowDelay, swallowDuration, swallowRim;

  // The planet being swallowed into, for the gem shader: centre (xy) and rim (z).
  public static Vector3 SwallowPlanet;
  // Marks a GPU swallow in the quad's Timing.y: -(SwallowTimingBase + duration).
  internal const float SwallowTimingBase = 2f;
  internal float SwallowDuration;
  internal Vector2 SwallowTo;

  internal void Swallow(Vector2 planet, float rim, float delay, float duration)
  {
    if (!IsLive || swallowing || CollectingOnGpu) return;
    TakeOverFlight();
    PickedUp = true;
    HarvesterCollectionSystem.Instance.flatSpatialHash.RemoveFromQueries(GridIndex);
    LaunchVelocity = Vector2.Zero;
    m_animating = false;
    m_targetHarvester = null;
    var direction = m_transform.Position - planet;
    direction = direction.LengthSquared() > 0.01f ? Vector2.Normalize(direction) : -Vector2.UnitX;
    if (RenderGemSystem.Instance?.DrawsGpuCollection == true)
    {
      // Drawn by the shader like a collection; retired once below the surface.
      SwallowPlanet = new Vector3(planet, rim);
      CollectStart = (float)(FlightClock + delay);
      CollectFrom = m_transform.Position;
      SwallowTo = planet + direction * rim * 0.55f;
      SwallowDuration = Math.Max(0.05f, duration);
      RenderGemSystem.Instance.UpdateGem(Id);
      UpdateSystem2.Instance.TrackGpuCollection(this, 0, FlightClock + delay + SwallowDuration, deliverHome: false);
      return;
    }
    swallowing = true;
    swallowFrom = m_transform.Position;
    swallowTo = planet + direction * rim * 0.55f;
    swallowCenter = planet;
    swallowRim = rim;
    swallowScale = m_transform.Scale;
    swallowAge = 0f;
    swallowDelay = delay;
    swallowDuration = Math.Max(0.05f, duration);
    Wake();
  }

  private bool UpdateSwallow(float dt)
  {
    if (!swallowing) return false;
    swallowAge += dt;
    float t = (swallowAge - swallowDelay) / swallowDuration;
    if (t <= 0f) return true;
    if (t >= 1f)
    {
      swallowing = false;
      ShouldDestroy = true;
      return true;
    }
    // A launch covers 1 - e^(-kt) of its path; mirrored in time it starts slow and
    // ends at full speed.
    const float k = SwallowAcceleration;
    float progress = 1f - (1f - MathF.Exp(-k * (1f - t))) / (1f - MathF.Exp(-k));
    var position = Vector2.Lerp(swallowFrom, swallowTo, progress);
    float depth = Vector2.Distance(position, swallowCenter) / swallowRim;
    m_transform.Position = position;
    m_transform.Scale = swallowScale * Math.Clamp((depth - 0.55f) / 0.45f, 0f, 1f);
    PositionMoved = true;
    return true;
  }

  private void ResetSwallow()
  {
    swallowing = false;
    swallowAge = 0f;
    SwallowDuration = 0f;
  }
}
