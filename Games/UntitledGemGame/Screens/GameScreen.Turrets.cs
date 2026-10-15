using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;

namespace UntitledGemGame.Screens;

// Weapon turrets on the home base, prerendered from the Synty model pack
// (tools/blender/prerender.py `render_turret`, catalogued in FleetAtlas). A turret
// appears when its weapon is owned: the cannon's twin turrets from the start, the
// mining laser, arc harpoon, rocket pod and railgun once bought. Each has a static
// base and a head that turns toward what it shoots; shots leave from the barrel tip.
// The railgun keeps its own aim and recoil (GameScreen.Railgun.cs) and only borrows
// the sprite and barrel length from here.
public partial class UntitledGemGameGameScreen
{
  private sealed class HullTurret
  {
    public readonly string Head, Base;
    // Hull units from the hull's centre (see HullMount); the hull faces up with the
    // planet to its right.
    public readonly Vector2 Mount;
    // Hull units from the pivot to the barrel tip (muzzle_units in the render's meta.json).
    public readonly float Muzzle;
    // Screen angle the barrel points along; 0 is right, like Atan2.
    public float Aim;
    // A target held for a moment after firing, then the turret drifts back to idle.
    public Vector2 Target;
    public float Hold;
    public float SinceFired = float.MaxValue;

    public HullTurret(string head, string @base, Vector2 mount, float muzzle)
    {
      Head = head;
      Base = @base;
      Mount = mount;
      Muzzle = muzzle;
    }
  }

  // Game units per texel of the turret renders: the home base's density.
  private const float TurretTexel = 0.19f;
  private const float TurretHoldSeconds = 0.8f;
  private const float TurretTurnRate = 8f;
  private const float TurretRecoilSeconds = 0.18f;
  private const float TurretRecoilUnits = 1.6f;
  // Rockets leave the pod's launch tubes side by side.
  private const float RocketTubeSpacing = 1.4f;

  private readonly HullTurret cannonTurretLeft =
    new(FleetAtlas.TurretCannon, FleetAtlas.TurretCannonBase, new(-17f, -30f), 9f);
  private readonly HullTurret cannonTurretRight =
    new(FleetAtlas.TurretCannon, FleetAtlas.TurretCannonBase, new(17f, -30f), 9f);
  private readonly HullTurret laserTurret =
    new(FleetAtlas.TurretLaser, FleetAtlas.TurretLaserBase, new(0f, -17f), 6.8f);
  private readonly HullTurret harpoonTurret =
    new(FleetAtlas.TurretHarpoon, FleetAtlas.TurretHarpoonBase, new(15f, -6f), 8f);
  private readonly HullTurret rocketTurret =
    new(FleetAtlas.TurretRockets, FleetAtlas.TurretRocketsBase, new(13f, 30f), 2.2f);
  private readonly HullTurret railgunTurret =
    new(FleetAtlas.TurretRailgun, FleetAtlas.TurretRailgunBase, new(15f, 9f), 20.8f);

  private HullTurret[] aimedTurrets;
  private HullTurret[] AimedTurrets
    => aimedTurrets ??= [cannonTurretLeft, cannonTurretRight, laserTurret, harpoonTurret, rocketTurret];

  private static bool TurretsVisible => PrerenderedShips.Enabled && TextureCache.Fleet != null;

  private Vector2 TurretPivot(HullTurret turret) => HullMount(turret.Mount.X, turret.Mount.Y);

  private Vector2 TurretMuzzle(HullTurret turret)
    => TurretPivot(turret) + PlanetDirection(turret.Aim) * turret.Muzzle * HullScale();

  // Snaps the turret onto its target so the shot leaves straight down the barrel.
  private Vector2 FireTurret(HullTurret turret, Vector2? target = null)
  {
    if (target is Vector2 point)
    {
      var toTarget = point - TurretPivot(turret);
      turret.Aim = MathF.Atan2(toTarget.Y, toTarget.X);
      turret.Target = point;
      turret.Hold = TurretHoldSeconds;
    }
    turret.SinceFired = 0f;
    return TurretMuzzle(turret);
  }

  // The cannon alternates between its two turrets.
  private Vector2 CannonMount(Vector2? target = null)
    => FireTurret((cannonLowerTurret = !cannonLowerTurret) ? cannonTurretRight : cannonTurretLeft, target);

  private Vector2 LaserMount() => TurretMuzzle(laserTurret);

  private Vector2 HarpoonMount() => FireTurret(harpoonTurret);

  private Vector2 RocketMount(int index)
  {
    var muzzle = FireTurret(rocketTurret);
    var direction = PlanetDirection(rocketTurret.Aim);
    float tube = (index % 3 - 1) * RocketTubeSpacing * HullScale();
    return muzzle + new Vector2(-direction.Y, direction.X) * tube;
  }

  // Runs with the weapon effects, so turrets settle even while weapons are held.
  private void UpdateTurrets(float dt)
  {
    if (m_homeBaseEntity == null) return;
    var upgrades = UpgradeManager.Instance.UG;
    // The laser follows its first beam while it fires; the harpoon watches its anchor.
    if (upgrades.MiningLaser && !LaserVenting)
    {
      laserTurret.Target = LaserContact(0);
      laserTurret.Hold = TurretHoldSeconds;
    }
    var anchor = harpoonAnchors[0];
    if ((harpoonInFlight || harpoonEmbedded) && anchor.Path != null)
    {
      harpoonTurret.Target = anchor.Landed ? anchor.Target : anchor.Path.End;
      harpoonTurret.Hold = TurretHoldSeconds;
    }

    var idle = RailgunIdleTarget();
    float blend = 1f - MathF.Exp(-dt * TurretTurnRate);
    foreach (var turret in AimedTurrets)
    {
      turret.Hold -= dt;
      if (turret.SinceFired < float.MaxValue) turret.SinceFired += dt;
      var toTarget = (turret.Hold > 0f ? turret.Target : idle) - TurretPivot(turret);
      turret.Aim += MathHelper.WrapAngle(MathF.Atan2(toTarget.Y, toTarget.X) - turret.Aim) * blend;
    }
  }

  // ---- Drawing ----

  private void DrawTurrets()
  {
    if (!TurretsVisible || m_homeBaseEntity == null || !GameStarted) return;
    var upgrades = UpgradeManager.Instance.UG;
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
      transformMatrix: m_camera.GetViewMatrix());
    DrawTurret(cannonTurretLeft);
    DrawTurret(cannonTurretRight);
    if (upgrades.MiningLaser) DrawTurret(laserTurret);
    if (upgrades.ArcHarpoon) DrawTurret(harpoonTurret);
    if (upgrades.RocketPods) DrawTurret(rocketTurret);
    m_spriteBatch.End();
  }

  private void DrawTurret(HullTurret turret)
  {
    float kick = turret.SinceFired < TurretRecoilSeconds
      ? (1f - turret.SinceFired / TurretRecoilSeconds) * TurretRecoilUnits : 0f;
    var pivot = TurretPivot(turret);
    DrawTurretSprite(turret, pivot, pivot - PlanetDirection(turret.Aim) * kick * HullScale(), turret.Aim);
  }

  // The base stays put at the mount; the head (and its recoil) turns with the aim.
  // Turntable frames point up at 0 and turn clockwise, hence the quarter turn.
  private void DrawTurretSprite(HullTurret turret, Vector2 basePivot, Vector2 headPivot, float aim)
  {
    var fleet = TextureCache.Fleet;
    float scale = TurretTexel * HullScale();
    var baseRegion = fleet.Region(turret.Base);
    m_spriteBatch.Draw(fleet.Texture, basePivot, baseRegion.Bounds, Color.White, 0f, fleet.Pivot(turret.Base),
      scale, SpriteEffects.None, 0f);
    var head = PrerenderedShips.Frame(fleet, turret.Head, aim + MathHelper.PiOver2, out var origin, out float rotation);
    m_spriteBatch.Draw(fleet.Texture, headPivot, head.Bounds, Color.White, rotation, origin, scale,
      SpriteEffects.None, 0f);
  }
}
