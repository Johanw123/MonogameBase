using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens;

// Prestige talents and Core Shard upgrades that make weapons and systems set each
// other off (rules and tuning in PrestigeTalentEffects):
//  - molten craters from Thermite Rounds (cannon) and Incendiary Warheads (rockets),
//    detonated by rockets and the Railgun with Magma Detonation;
//  - Lightning Rod (cannon hits charge the anchored harpoon) and Tesla Coil (harpoon
//    pulses arc to every molten spot);
//  - Beam Riders (laser beams launch rockets);
//  - volleys: Echo Protocol, Drone Gunships, Main Battery Relay, Shard Reactor and
//    Planetary Overload all fire weapons on their own;
//  - planet-wide weapon bonuses: Shard Reactor, Signal Resonance, Hollow World.
public partial class UntitledGemGameGameScreen
{
  private const int MaxCraters = 48;
  // Molten craters are drawn by MoltenCrater.fx on the planet's texel grid. A crater's
  // radius is Size * CraterRadiusPerSize world units at the base planet scale; the
  // quad's half size must match QUAD_SCALE and QUAD_PAD in the shader.
  private const float CraterRadiusPerSize = 1.5f;
  private const float CraterQuadScale = 1.6f;
  private const float CraterQuadPad = 2f;
  // Texels from the planet's centre that stay inside its outline (as for the cracks).
  private const float CraterDiscRadius = 29.5f;
  private const int MaxArcs = 48;
  private const float ArcSeconds = 0.28f;
  private const float BeamRiderSpeed = 700f;
  private static readonly Color OverloadColor = new(255, 140, 70);

  private sealed class Crater
  {
    public Vector2 Position;
    public float Angle, Age, Budget, Carry, Size;
    public int FirePower;
    public byte Seed;
  }

  private struct Arc
  {
    public Vector2 From, To;
    public float Age;
    public int Seed;
  }

  private readonly List<Crater> craters = new();
  private readonly List<Arc> arcs = new();
  private int harpoonRodPulses;
  private float beamRiderTimer;
  private float laserOvercharge;
  private int overloadPressure;
  private bool overloadPending;
  private float overloadCooldown;
  private int escortShells;
  private float escortTimer;
  private Vector2 escortFrom;
  private bool escortHigh;
  private float weaponYieldCarry;
  // Planet-wide weapon bonuses, refreshed once per weapon update.
  private float weaponYieldBonus;
  private int weaponBonusLayers;

  private static UpgradesGeneratorUpgrades_meta Talents => UpgradeManager.Instance.UGM;

  // Slag Furnace signals make craters burn (and ooze) longer.
  private static float CraterSeconds => PrestigeTalentEffects.CraterSeconds * SignalStats.MoltenDurationMultiplier;

  private bool CombatActive => PlanetMiningEnabled && GameStarted && !m_prestiging && !m_postPrestige;

  // The play area of the last weapon update, for effects triggered from outside it.
  private PlayAreaBounds weaponBounds;

  private void RefreshWeaponBonuses()
  {
    long stacks = 0;
    foreach (long count in m_gameState.Signals.Counts) stacks += count;
    weaponYieldBonus = PrestigeTalentEffects.ShardReactorBonus(m_gameState.CurrentCoreShardCount)
      + PrestigeTalentEffects.SignalResonanceBonus(stacks)
      + CoreDrill.HollowBonus(UpgradeManager.Instance.UGA, m_gameState.CoreDrillTunnels)
      + (DrillResonating ? CoreDrill.WeaponYieldBonus(UpgradeManager.Instance.UGA) : 0f);
    weaponBonusLayers = PrestigeTalentEffects.SignalResonanceLayers(stacks);
  }

  // Every planet hit by a weapon passes through here: talents add gems (fractions
  // carry over) and Planetary Overload counts the result.
  private int WeaponHitYield(int gems)
  {
    if (gems <= 0) return gems;
    if (weaponYieldBonus > 0f)
    {
      weaponYieldCarry += gems * weaponYieldBonus;
      int extra = (int)Math.Min(int.MaxValue, weaponYieldCarry);
      weaponYieldCarry -= extra;
      gems = (int)Math.Min(int.MaxValue, (long)gems + extra);
    }
    if (Talents.PlanetaryOverload)
    {
      // Pressure tops out at one eruption's worth while the planet recovers.
      int needed = SignalStats.OverloadPressure;
      overloadPressure = (int)Math.Min(needed, (long)overloadPressure + gems);
      if (overloadPressure >= needed) overloadPending = true;
    }
    return gems;
  }

  // Signal Resonance and the Core Drill's Seismic Resonance mine deeper layers.
  private int WeaponHitPower(int firePower)
  {
    int layers = weaponBonusLayers + (DrillResonating ? CoreDrill.ResonanceLayers(UpgradeManager.Instance.UGA) : 0);
    return layers > 0 ? CoreDrill.Deeper(firePower, layers) : firePower;
  }

  private void UpdateTalentCombos(float dt, PlayAreaBounds bounds)
  {
    UpdateCraters(dt, bounds);
    for (int i = arcs.Count - 1; i >= 0; i--)
    {
      var arc = arcs[i];
      arc.Age += dt;
      if (arc.Age >= ArcSeconds) arcs.RemoveAt(i);
      else arcs[i] = arc;
    }
    escortTimer = Math.Max(0f, escortTimer - dt);
    if (escortShells > 0 && escortTimer <= 0f && CombatActive) FireEscortShell();
    overloadCooldown = Math.Max(0f, overloadCooldown - dt);
    if (overloadPending && overloadCooldown <= 0f && CombatActive) EruptPlanet();
  }

  private void ClearTalentCombos()
  {
    craters.Clear();
    arcs.Clear();
    harpoonRodPulses = 0;
    beamRiderTimer = laserOvercharge = weaponYieldCarry = 0f;
    overloadPressure = 0;
    overloadPending = false;
    overloadCooldown = escortTimer = 0f;
    escortShells = 0;
  }

  // ---- Molten craters and Magma Detonation ----

  private void AddCrater(Vector2 hit, int gems, int firePower, float size)
  {
    if (gems <= 0) return;
    var fromCenter = hit - PlanetPos;
    if (craters.Count >= MaxCraters) craters.RemoveAt(0);
    craters.Add(new Crater
    {
      Position = hit,
      Angle = MathF.Atan2(fromCenter.Y, fromCenter.X),
      Budget = gems * PrestigeTalentEffects.CraterShare,
      FirePower = firePower,
      Size = size,
      Seed = (byte)Random.Shared.Next(256),
    });
  }

  private void UpdateCraters(float dt, PlayAreaBounds bounds)
  {
    for (int i = craters.Count - 1; i >= 0; i--)
    {
      var crater = craters[i];
      crater.Age += dt;
      crater.Carry += crater.Budget * dt / CraterSeconds;
      int gems = (int)Math.Min(crater.Carry, 16f);
      if (gems > 0)
      {
        crater.Carry -= gems;
        KnockGemsLoose(PlanetDamageSource.MoltenCraters, gems, crater.FirePower, bounds, 0.8f, crater.Angle, 0.3f);
      }
      if (crater.Age >= CraterSeconds) craters.RemoveAt(i);
    }
  }

  private void OnCannonHit(PlanetShot shot)
  {
    if (Talents.ThermiteRounds) AddCrater(shot.End, shot.Damage, shot.FirePower, shot.Critical ? 9f : 5f);
    if (!Talents.LightningRod || !harpoonEmbedded) return;
    int before = harpoonRodPulses;
    harpoonRodPulses = PrestigeTalentEffects.LightningRodPulses(shot.Critical, harpoonRodPulses);
    if (harpoonRodPulses > before) AddArc(shot.End, harpoonTarget);
  }

  private void OnRocketHit(PlanetShot shot, float impactAngle, PlayAreaBounds bounds)
  {
    if (UpgradeManager.Instance.UG.RocketIncendiary)
      AddCrater(shot.End, shot.Damage, shot.FirePower, shot.Mini ? 4f : 6f);
    DetonateMolten(impactAngle, PrestigeTalentEffects.RocketDetonationRadius, bounds);
  }

  private static float AngleBetween(float a, float b)
    => MathF.Abs(MathHelper.WrapAngle(a - b));

  private void DetonateMolten(float impactAngle, float radius, PlayAreaBounds bounds, bool force = false)
  {
    if (!force && !Talents.MagmaDetonation) return;
    int count = 0;
    float scarSeconds = MagmaScarSeconds;
    for (int i = magmaScars.Count - 1; i >= 0; i--)
    {
      var scar = magmaScars[i];
      if (AngleBetween(scar.Angle, impactAngle) > radius) continue;
      magmaScars.RemoveAt(i);
      int gems = PrestigeTalentEffects.DetonationGems(scar.Budget, scar.Age, scarSeconds);
      if (gems <= 0) continue;
      KnockGemsLoose(PlanetDamageSource.MagmaDetonation, gems, scar.FirePower, bounds, LaserReach, scar.Angle, 0.4f,
        scar.Value);
      DetonationFlash(PlanetPos + PlanetDirection(scar.Angle) * PlanetRadius * 0.85f, 0.55f, count++);
    }
    for (int i = craters.Count - 1; i >= 0; i--)
    {
      var crater = craters[i];
      if (AngleBetween(crater.Angle, impactAngle) > radius) continue;
      craters.RemoveAt(i);
      int gems = PrestigeTalentEffects.DetonationGems(crater.Budget, crater.Age, CraterSeconds);
      if (gems <= 0) continue;
      KnockGemsLoose(PlanetDamageSource.MagmaDetonation, gems, crater.FirePower, bounds, 1f, crater.Angle, 0.4f);
      DetonationFlash(crater.Position, 0.5f + crater.Size * 0.08f, count++);
    }
    if (count >= 3)
      ShowWorldPopup(PlanetPos + PlanetDirection(impactAngle) * (PlanetRadius + 30f), $"DETONATION x{count}", large: false);
  }

  private void DetonationFlash(Vector2 position, float scale, int index)
  {
    SpawnerEffects.Add(null, position, MagmaColor, 2f, 14f + scale * 10f, 0.3f);
    // A few explosions read as a chain; dozens would bury the planet.
    if (index < 6) planetExplosions.Add(new PlanetExplosion { Position = position, Scale = scale });
  }

  // ---- Arcs: Lightning Rod and Tesla Coil ----

  private void AddArc(Vector2 from, Vector2 to)
  {
    if (arcs.Count >= MaxArcs) arcs.RemoveAt(0);
    arcs.Add(new Arc { From = from, To = to, Seed = Random.Shared.Next(1000) });
  }

  private void TeslaArcs(int pulseGems, int firePower, PlayAreaBounds bounds)
  {
    if (!UpgradeManager.Instance.UG.HarpoonTesla) return;
    int gems = Math.Max(1, pulseGems / 4);
    int arcsLeft = PrestigeTalentEffects.TeslaArcLimit;
    for (int i = craters.Count - 1; i >= 0 && arcsLeft > 0; i--, arcsLeft--)
    {
      KnockGemsLoose(PlanetDamageSource.TeslaCoil, gems, firePower, bounds, 0.8f, craters[i].Angle, 0.3f);
      AddArc(harpoonTarget, craters[i].Position);
    }
    // Laser scars sit close together along the cut; arc to every third.
    for (int i = magmaScars.Count - 1; i >= 0 && arcsLeft > 0; i -= 3, arcsLeft--)
    {
      KnockGemsLoose(PlanetDamageSource.TeslaCoil, gems, firePower, bounds, 0.8f, magmaScars[i].Angle, 0.3f);
      AddArc(harpoonTarget, PlanetPos + PlanetDirection(magmaScars[i].Angle) * PlanetRadius * 0.85f);
    }
  }

  // ---- Beam Riders ----

  private void UpdateBeamRiders(float dt, UpgradesGeneratorUpgrades upgrades, int beams)
  {
    if (!Talents.BeamRiders || !upgrades.RocketPods || beams <= 0)
    {
      beamRiderTimer = 0f;
      return;
    }
    beamRiderTimer += dt * (laserOvercharge > 0f ? PrestigeTalentEffects.LaserOverchargeRate : 1f);
    float interval = PrestigeTalentEffects.BeamRiderInterval(SignalStats.FireRate(MainShipWeapon.Laser));
    if (beamRiderTimer < interval) return;
    beamRiderTimer = Math.Min(beamRiderTimer - interval, interval);
    int firePower = SignalStats.FirePower(MainShipWeapon.Rockets);
    int perBeam = PrestigeTalentEffects.BeamRidersPerBeam(upgrades);
    for (int beam = 0; beam < beams; beam++)
      for (int k = 0; k < perBeam; k++)
      {
        if (Talents.ProjectConstellation)
        {
          StoreConstellationRocket(firePower, upgrades);
          continue;
        }
        if (upgrades.RocketOrbitalStrike)
        {
          int orbital = AutomaticWeaponYield((int)MathF.Ceiling(firePower * MainShipWeapons.OrbitalStrikeBonus));
          LaunchOrbitalRocket(LaserMount(), orbital, firePower, beam * perBeam + k, k * 0.12f);
          continue;
        }
        int gems = AutomaticWeaponYield(firePower);
        // A rocket that rides straight down the beam to the spot it is melting.
        var start = LaserMount();
        var end = LaserContact(beam);
        var shot = new PlanetShot
        {
          Kind = PlanetShotKind.Rocket,
          Delay = k * 0.12f,
          Duration = Math.Max(0.1f, Vector2.Distance(start, end) / BeamRiderSpeed),
          Damage = gems,
          FirePower = firePower,
        };
        SetQuadraticPath(shot, start, (start + end) * 0.5f, end);
        AddPlanetShot(shot);
      }
  }

  // ---- Volleys ----

  // Main Battery Relay overcharges the laser: triple output for a moment.
  private float UpdateLaserOvercharge(float dt)
  {
    if (laserOvercharge <= 0f) return 1f;
    laserOvercharge = Math.Max(0f, laserOvercharge - dt);
    return PrestigeTalentEffects.LaserOverchargeRate;
  }

  public void FireCannonVolley(int shells)
  {
    if (!CombatActive || shells <= 0) return;
    FirePlanetCannon(shells, SignalStats.FirePower(MainShipWeapon.Cannon));
  }

  // Echo Protocol: every ship system activation fires a cannon volley.
  public void FireEchoVolley(int casts)
  {
    if (!Talents.SystemEcho) return;
    FireCannonVolley(PrestigeTalentEffects.EchoVolleyShells * Math.Max(1, casts));
  }

  // Drone Gunships: a drone fires one shell at the side of the planet facing it.
  public void FireDroneShell(Vector2 from)
  {
    if (Talents.DroneGunships) FireShipShell(from);
  }

  // Gun Pod module (and Drone Gunships): a cannon shell from a ship.
  public void FireShipShell(Vector2 from)
  {
    if (!CombatActive) return;
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    int gems = AutomaticWeaponYield(firePower);
    if (gems <= 0) return;
    var toDrone = from - PlanetPos;
    toDrone = toDrone.LengthSquared() > 0.01f ? Vector2.Normalize(toDrone) : -Vector2.UnitX;
    LaunchCannonShot(PlanetShotKind.Cannon, from, PlanetPos + toDrone * PlanetRadius * 0.9f, gems, firePower);
  }

  // Armed Escorts: a delivering ship queues one shell per module it carries.
  public void ArmEscort(Vector2 from, int modules)
  {
    if (!Talents.ArmedEscorts || !UpgradeManager.Instance.UGM.ShipyardUnlocked || modules <= 0 || !CombatActive) return;
    escortShells = Math.Min(PrestigeTalentEffects.MaxQueuedEscortShells, escortShells + modules);
    escortFrom = from;
  }

  // Queued shells merge into a steady stream that lobs over or under the weapon lane.
  private void FireEscortShell()
  {
    int shells = Math.Min(escortShells, PrestigeTalentEffects.EscortShellsPerShot);
    escortShells -= shells;
    escortTimer = PrestigeTalentEffects.EscortShellSeconds;
    int firePower = SignalStats.FirePower(MainShipWeapon.Cannon);
    int gems = AutomaticWeaponYield((int)Math.Min(int.MaxValue, (long)firePower * shells));
    if (gems <= 0) return;
    bool critical = RollCriticalShell(ref gems);
    var end = AutomaticPlanetTarget(0.55f);
    var travel = end - escortFrom;
    escortHigh = !escortHigh;
    var control = (escortFrom + end) * 0.5f + new Vector2(-travel.Y, travel.X) * (escortHigh ? 0.35f : -0.35f);
    var shot = new PlanetShot
    {
      Kind = PlanetShotKind.Cannon,
      Duration = Math.Max(0.1f, travel.Length() / CannonShotSpeed * 1.2f),
      Damage = gems,
      FirePower = firePower,
      Critical = critical,
    };
    SetQuadraticPath(shot, escortFrom, control, end);
    AddPlanetShot(shot);
  }

  // Weapon modules that fire when a fleet ship delivers its cargo; bigger loads hit harder.
  public void OnFleetDelivery(Harvester harvester, Vector2 from, uint cargo)
  {
    if (!CombatActive || !UpgradeManager.Instance.UGM.ShipyardUnlocked || cargo == 0) return;
    var toShip = from - PlanetPos;
    float facing = toShip.LengthSquared() > 0.01f ? MathF.Atan2(toShip.Y, toShip.X) : PlanetFacingAngle();
    if (harvester.HasModule(ShipModule.RocketRack))
    {
      int rockets = (int)Math.Clamp(cargo / ModuleCatalog.RocketRackGemsPerRocket, 1, ModuleCatalog.RocketRackMaxRockets);
      int firePower = SignalStats.FirePower(MainShipWeapon.Rockets);
      for (int i = 0; i < rockets; i++)
      {
        int gems = AutomaticWeaponYield(firePower);
        if (gems <= 0) break;
        // Fan the salvo across the near face of the planet.
        float spread = rockets == 1 ? 0f : (i / (rockets - 1f) - 0.5f) * 1.2f;
        LaunchPlanetShot(PlanetShotKind.Rocket, from, PlanetPos + PlanetDirection(facing + spread) * PlanetRadius * 0.9f,
          gems, firePower);
      }
    }
    if (harvester.HasModule(ShipModule.LaserUplink) && UpgradeManager.Instance.UG.MiningLaser)
      laserOvercharge = Math.Max(laserOvercharge,
        Math.Min(ModuleCatalog.LaserUplinkMaxSeconds, cargo * ModuleCatalog.LaserUplinkSecondsPerGem));
    if (harvester.HasModule(ShipModule.DetonatorCharge))
      DetonateMolten(facing, ModuleCatalog.DetonatorRadius, weaponBounds, force: true);
  }

  // Every weapon you own fires at once (Main Battery Relay, Shard Reactor, Planetary
  // Overload). The Railgun only gains charge, so it cannot set itself off.
  private void FireAllWeapons(bool chargeGun, int cannonShells)
  {
    if (!CombatActive) return;
    var upgrades = UpgradeManager.Instance.UG;
    FireCannonVolley(cannonShells);
    if (upgrades.RocketPods) FireRocketSalvo();
    if (upgrades.ArcHarpoon)
    {
      if (harpoonEmbedded) harpoonPulseTimer = Math.Max(harpoonPulseTimer, HarpoonPulseInterval());
      else if (!harpoonInFlight) harpoonReload = Math.Max(harpoonReload, HarpoonReloadTime());
    }
    if (upgrades.MiningLaser) laserOvercharge = Math.Max(laserOvercharge, PrestigeTalentEffects.RelayLaserSeconds);
    if (chargeGun && upgrades.Railgun)
      railgunCharge = Math.Min(1f, railgunCharge + PrestigeTalentEffects.AllWeaponsGunCharge);
  }

  // Shard Reactor: collecting a Core Shard makes every weapon fire.
  private void OnCoreShardCollected()
  {
    if (Talents.ShardReactor) FireAllWeapons(true, PrestigeTalentEffects.AllWeaponsVolleyShells);
  }

  // ---- Planetary Overload ----

  private int StrongestFirePower()
  {
    var upgrades = UpgradeManager.Instance.UG;
    int power = SignalStats.FirePower(MainShipWeapon.Cannon);
    if (upgrades.MiningLaser) power = Math.Max(power, SignalStats.FirePower(MainShipWeapon.Laser));
    if (upgrades.ArcHarpoon) power = Math.Max(power, SignalStats.FirePower(MainShipWeapon.Harpoon));
    if (upgrades.RocketPods) power = Math.Max(power, SignalStats.FirePower(MainShipWeapon.Rockets));
    if (upgrades.Railgun) power = Math.Max(power, SignalStats.FirePower(MainShipWeapon.Railgun));
    return power;
  }

  private void EruptPlanet()
  {
    overloadPending = false;
    overloadPressure = 0;
    overloadCooldown = PrestigeTalentEffects.OverloadCooldownSeconds;
    // The quake sheds its ring outside the weapon funnel, so it cannot feed itself.
    StartShockwave(PlanetDamageSource.PlanetaryOverload, Random.Shared.NextSingle() * MathHelper.TwoPi,
      PrestigeTalentEffects.OverloadGems, StrongestFirePower());
    PulsePlanet(1f, 1f);
    SpawnerEffects.Add(null, PlanetPos, OverloadColor, PlanetRadius, PlanetRadius * 2.6f, 0.8f);
    ShowWorldPopup(PlanetPos - Vector2.UnitY * (PlanetRadius + 60f), "PLANETARY OVERLOAD", large: true);
    FireAllWeapons(true, PrestigeTalentEffects.AllWeaponsVolleyShells);
  }

  // ---- Drawing ----

  // Molten craters, drawn solid onto the planet like the laser's scars.
  // Each crater is a quad shaded by MoltenCrater.fx: a scorched patch in the planet's
  // own pixels, with a molten core that churns and cools. Bloom gives it its glow.
  private void DrawCraters()
  {
    var effect = EffectCache.MoltenCraterFx;
    if (craters.Count == 0 || effect?.IsLoaded != true || effect.IsFailed) return;
    float texel = PlanetSpriteScale();
    if (texel <= 0.01f) return;
    // Craters ride the planet's shake with its sprite.
    var center = PlanetPos + PlanetShakeOffset();
    var parameters = effect.Value.Parameters;
    parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
    parameters["PlanetCenter"]?.SetValue(center);
    parameters["TexelSize"]?.SetValue(texel);
    parameters["DiscRadius"]?.SetValue(CraterDiscRadius);
    parameters["Time"]?.SetValue(planetAge);
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, effect: effect.Value);
    foreach (var crater in craters)
    {
      float life = Math.Clamp(1f - crater.Age / CraterSeconds, 0f, 1f);
      // The shader reads the radius back from a byte; size the quad from that same value.
      float texels = crater.Size * CraterRadiusPerSize / BasePlanetScale;
      byte radiusByte = (byte)Math.Clamp(MathF.Round(texels / 32f * 255f), 1f, 255f);
      float radius = radiusByte * 32f / 255f;
      float quad = 2f * (radius * CraterQuadScale + CraterQuadPad) * texel;
      m_spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, center + (crater.Position - PlanetPos), null,
        new Color((byte)MathF.Round(life * 255f), crater.Seed, radiusByte, (byte)255), 0f, new Vector2(0.5f), quad,
        SpriteEffects.None, 0f);
    }
    m_spriteBatch.End();
  }

  // Overload pressure: called inside the additive weapon pass. Arcs are drawn with the
  // harpoon's lightning (DrawArcHarpoon).
  private void DrawTalentGlows(float feather)
  {
    if (Talents.PlanetaryOverload && overloadPressure > 0)
    {
      float pressure = Math.Clamp(overloadPressure / (float)SignalStats.OverloadPressure, 0f, 1f);
      float throb = 0.75f + 0.25f * MathF.Sin(planetAge * (3f + 9f * pressure));
      m_shapeBatch.BorderCircle(PlanetPos, PlanetRadius + 2f, OverloadColor * (0.4f * pressure * pressure * throb),
        2f + 4f * pressure, Math.Max(feather, 8f));
    }
  }
}
