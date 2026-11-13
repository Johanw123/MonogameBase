using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Screens;

// Lightning upgrades: other weapons give the Arc Harpoon's pulses more to arc to.
//  - Arc Lance: every pulse also runs down each laser beam and knocks gems loose where
//    the beam touches (not while Overheat Surge vents);
//  - Conductor Round: Railgun slugs stay lodged in the planet, and every pulse arcs to
//    each one, shedding gems along the way.
// Arc Lance is a core shard, Conductor Round a regular railgun upgrade. Both work once per
// pulse, however many harpoons are anchored (tuning in MainShipWeapons).
public partial class UntitledGemGameGameScreen
{
  private static readonly Color ConductorGlow = new(150, 205, 255);
  private const float ConductorSlugLength = 19f;
  private const float ConductorSlugHalfWidth = 2.3f;
  // A slug lands white hot and cools to bare steel.
  private const float ConductorCoolSeconds = 2.5f;

  // A lodged slug: where on the rim it sits, and when it landed (in planetAge).
  private readonly record struct ConductorSlug(float Angle, float Landed);

  // Oldest first.
  private readonly List<ConductorSlug> conductorSlugs = new();

  private void LodgeConductorSlug(float impactAngle)
  {
    if (!UpgradeManager.Instance.UG.RailgunConductor) return;
    if (conductorSlugs.Count >= MainShipWeapons.ConductorSlugs) conductorSlugs.RemoveAt(0);
    conductorSlugs.Add(new ConductorSlug(impactAngle, planetAge));
  }

  private Vector2 ConductorSlugPoint(float angle) => PlanetPos + PlanetDirection(angle) * PlanetRadius * 0.9f;

  private void ConductPulse(int pulseGems, int firePower, PlayAreaBounds bounds, UpgradesGeneratorUpgrades upgrades)
  {
    if (upgrades.LaserArcLance && upgrades.MiningLaser && !LaserVenting)
    {
      int gems = (int)MathF.Ceiling(pulseGems * MainShipWeapons.ArcLanceShare);
      var mount = LaserMount();
      for (int beam = 0; beam < LaserBeamCount; beam++)
      {
        KnockGemsLoose(PlanetDamageSource.ArcLance, LightningHit(gems, LaserContact(beam)), firePower, bounds, LaserReach,
          LaserContactAngle(beam), 0.35f);
        AddArc(mount, LaserContact(beam));
      }
    }

    if (!upgrades.RailgunConductor) return;
    int shed = (int)MathF.Ceiling(pulseGems * MainShipWeapons.RailgunConductorDamage(UpgradeManager.Instance.UG));
    foreach (var lodged in conductorSlugs)
    {
      float angle = lodged.Angle;
      var slug = ConductorSlugPoint(angle);
      var anchor = NearestHarpoonAnchor(slug);
      var fromCenter = anchor - PlanetPos;
      float anchorAngle = MathF.Atan2(fromCenter.Y, fromCenter.X);
      float turn = MathHelper.WrapAngle(angle - anchorAngle);
      // The arc crosses from the anchor to the slug: its gems come off all along the way.
      KnockGemsLoose(PlanetDamageSource.ConductorRound, LightningHit(shed, slug), firePower, bounds, 0.8f,
        anchorAngle + turn / 2f, Math.Max(0.2f, MathF.Abs(turn) / 2f));
      AddArc(anchor, slug);
    }
  }

  private void ClearConductors() => conductorSlugs.Clear();

  // A lodged slug juts out of the rim: a steel rod that lands white hot, quivers and cools,
  // and lights up from its root to its tail as each pulse runs through it (ModuleEffects.fx).
  private void DrawConductorSlugs()
  {
    if (conductorSlugs.Count == 0) return;
    float charge = harpoonPulseFlash / HarpoonPulseFlashSeconds;
    var effects = WeaponEffects;
    for (int i = 0; i < conductorSlugs.Count; i++)
    {
      var slug = conductorSlugs[i];
      float age = planetAge - slug.Landed;
      if (age < 0f) age = ConductorCoolSeconds;
      // It quivers about where it went in, like an arrow.
      float quiver = MathF.Sin(age * 46f) * MathF.Exp(-age * 7f) * 0.2f;
      var root = ConductorSlugPoint(slug.Angle);
      var tail = root + PlanetDirection(slug.Angle + quiver) * ConductorSlugLength;
      effects.Slug(root, tail, age / ConductorCoolSeconds, charge, ConductorGlow, ConductorSlugHalfWidth,
        slug.Angle * 0.37f + i * 0.21f);
    }
    effects.Draw(m_camera.ViewProjection(), planetAge);
  }
}
