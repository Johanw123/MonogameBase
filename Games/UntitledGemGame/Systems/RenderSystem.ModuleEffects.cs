using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems;

// What each ship's modules look like this frame, queued for ModuleEffects.fx.
public partial class RenderSystem
{
  private static readonly Color StormTint = new(90, 165, 255);
  private static readonly Color ThunderTint = new(190, 150, 255);
  private static readonly Color TractorTint = new(60, 230, 255);
  private static readonly Color WakeTint = new(120, 220, 255);
  private static readonly Color OverdriveTint = new(255, 140, 40);
  private static readonly Color RelayTint = new(255, 200, 70);
  private static readonly Color GravityTint = new(160, 110, 255);
  private static readonly Color GhostTint = new(70, 230, 255);

  private static float ShipSeed(Harvester ship) => ship.Id * 0.137f;

  private void QueueModuleEffects(Harvester ship, Transform2 transform)
  {
    float seed = ShipSeed(ship);
    var center = ship.BoundingCircle.Center;

    // Tractor Link and Recall Tether: a beam from the gem to where it is going.
    if (ship.TractorFlashRemaining > 0f)
      _moduleEffects.Beam(ship.TractorOrigin, ship.TractorTarget, 1f - ship.TractorFlashRemaining / 0.25f,
        TractorTint, 1.2f, seed);

    // Wake Collector: the trail the ship sweeps gems out of.
    if (ship.WakeFlashRemaining > 0f)
    {
      var travel = ship.WakeEnd - ship.WakeStart;
      if (travel.LengthSquared() > 0.0001f)
        _moduleEffects.Plume(ship.WakeEnd, ship.WakeEnd - Vector2.Normalize(travel) * 70f, WakeTint,
          ModuleCatalog.WakeRadius, seed, 0.5f * ship.WakeFlashRemaining / 0.15f);
    }

    // Overdrive Coil: an afterburner while the boost lasts.
    if (ship.OverdriveTimeRemaining > 0f)
    {
      var forward = new Vector2(MathF.Cos(transform.Rotation - MathHelper.PiOver2),
        MathF.Sin(transform.Rotation - MathHelper.PiOver2));
      float boost = ship.OverdriveTimeRemaining / ModuleCatalog.OverdriveDuration;
      _moduleEffects.Plume(transform.Position - forward * 6f, transform.Position - forward * (34f + 22f * boost),
        OverdriveTint, 5.5f, seed, 0.45f + 0.55f * boost);
    }

    // World Eater: the ship is a black hole that grows as it feeds.
    if (ship.HasModule(ShipModule.WorldEater))
      _moduleEffects.Vortex(center, ship.WorldEaterRadius, 22f + ship.WorldEaterPickups * 0.25f, GravityTint,
        seed, spin: 0.6f, brightness: 0.6f);

    // Thunder God: every branch of the storm.
    if (ship.ThunderFlashRemaining > 0f)
    {
      float progress = 1f - ship.ThunderFlashRemaining / ModuleCatalog.PulseDuration;
      int index = 0;
      foreach (var arc in ship.ThunderArcs)
        _moduleEffects.Strike(arc.Start, arc.End, progress, seed + ++index * 0.193f, ThunderTint, 0.5f, 0.85f, forks: 1);
    }

    // Time Heist: a hologram of the ship flying its last route again.
    if (ship.GhostRoute != null)
    {
      var heading = ship.GhostPosition - ship.GhostPreviousPosition;
      if (heading.LengthSquared() < 0.0001f && ship.GhostSegment < ship.GhostRoute.Length)
        heading = ship.GhostRoute[ship.GhostSegment].End - ship.GhostRoute[ship.GhostSegment].Start;
      if (heading.LengthSquared() < 0.0001f) heading = Vector2.UnitX;
      heading.Normalize();
      _moduleEffects.Plume(ship.GhostPosition - heading * 8f, ship.GhostPosition - heading * 95f, GhostTint, 9f,
        seed, 0.55f);
      _moduleEffects.Ghost(ship.GhostPosition, heading, 30f, GhostTint, seed);
    }

    // Gravity Buoy: a small well that winds up before it pulls.
    if (ship.BuoyRemaining > 0f)
    {
      float charge = 1f - ship.BuoyRemaining;
      _moduleEffects.Vortex(ship.BuoyPosition, 30f + 24f * charge, 0f, GravityTint, seed + 0.4f,
        spin: 1f, brightness: 0.4f + 0.6f * charge, intensity: 0.5f + 0.5f * charge);
    }

    // Storm Coil, Arc Emitter and Storm Drones: a chain from gem to gem.
    if (ship.StormArcRemaining > 0f)
    {
      float progress = 1f - ship.StormArcRemaining / ModuleCatalog.PulseDuration;
      for (int arc = 1; arc < ship.StormArcCount; arc++)
        _moduleEffects.Strike(ship.StormArcPoints[arc - 1], ship.StormArcPoints[arc], progress,
          ship.StormArcSeed + seed + arc * 0.193f, StormTint);
    }

    // Astral Relay: the cargo's value beamed home. It fires every few pickups across the
    // whole field, so the beam is kept thin and faint.
    if (ship.RelayFlashRemaining > 0f)
      _moduleEffects.Beam(ship.RelayOrigin, UntitledGemGameGameScreen.HomeBasePos,
        1f - ship.RelayFlashRemaining / ModuleCatalog.PulseDuration, RelayTint, 1f, seed, 0.4f);

    if (ship.ModulePulseRemaining > 0f)
    {
      // Gravity effects pull inward, value and fuel bursts flare, the rest are shock fronts.
      var color = ship.ModulePulseColor;
      var style = color == Color.MediumPurple ? ModuleEffectBatch.RingStyle.Collapse
        : color == Color.Gold || color == Color.OrangeRed ? ModuleEffectBatch.RingStyle.Burst
        : ModuleEffectBatch.RingStyle.Shock;
      // The widest pulses cover a good part of the field, so they are drawn fainter.
      float intensity = Math.Clamp(130f / ship.ModulePulseRadius, 0.5f, 1f);
      _moduleEffects.Ring(ship.ModulePulsePosition, ship.ModulePulseRadius,
        1f - ship.ModulePulseRemaining / ModuleCatalog.PulseDuration, color, style, seed, intensity);
    }

    if (ship.FinalSweepTimeRemaining > 0f)
      _moduleEffects.Ring(ship.FinalSweepPosition, ship.FinalSweepRadius,
        1f - ship.FinalSweepTimeRemaining / BaseStats.DroneFinalSweepDurationSeconds, Color.LightCyan,
        ModuleEffectBatch.RingStyle.Shock, seed, 0.6f);
  }

  // A portal closing where a ship left and a burst where it arrives, each at its own site.
  private void QueueWarpFlash(Vector2 position, float size, float progress, bool arriving)
  {
    if (arriving)
      _moduleEffects.Ring(position, size * 1.4f, progress, Color.Cyan, ModuleEffectBatch.RingStyle.Burst,
        position.X * 0.013f);
    else
      _moduleEffects.Ring(position, size, progress, new Color(155, 100, 255), ModuleEffectBatch.RingStyle.Collapse,
        position.Y * 0.013f);
  }
}
