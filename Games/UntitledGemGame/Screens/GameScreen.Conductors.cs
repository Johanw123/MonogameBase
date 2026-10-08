using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
  private static readonly Color ConductorCore = new(235, 245, 255);
  private const float ConductorSlugLength = 16f;

  // Where each lodged slug sits on the rim, oldest first.
  private readonly List<float> conductorSlugs = new();

  private void LodgeConductorSlug(float impactAngle)
  {
    if (!UpgradeManager.Instance.UG.RailgunConductor) return;
    if (conductorSlugs.Count >= MainShipWeapons.ConductorSlugs) conductorSlugs.RemoveAt(0);
    conductorSlugs.Add(impactAngle);
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
    int shed = (int)MathF.Ceiling(pulseGems * MainShipWeapons.ConductorShare);
    foreach (float angle in conductorSlugs)
    {
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

  // A lodged slug juts out of the rim, glowing as it holds charge.
  private void DrawConductorSlugs(float feather)
  {
    if (conductorSlugs.Count == 0) return;
    float flash = harpoonPulseFlash / HarpoonPulseFlashSeconds;
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    foreach (float angle in conductorSlugs)
    {
      var direction = PlanetDirection(angle);
      var root = ConductorSlugPoint(angle);
      var tip = root + direction * ConductorSlugLength;
      m_shapeBatch.FillLine(root, tip, 5f, ConductorGlow * (0.35f + 0.5f * flash), Math.Max(feather, 3f));
      m_shapeBatch.FillLine(root, tip, 1.8f, ConductorCore * (0.7f + 0.3f * flash), feather);
    }
    m_shapeBatch.End();
  }
}
