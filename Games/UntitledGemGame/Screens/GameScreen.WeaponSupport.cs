using System;
using JapeFramework;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private double nextWeaponSupportRoll;

  private void TryDropWeaponSupport(PlanetDamageSource source, int damage)
  {
    if (damage <= 0 || !UpgradeManager.Instance.UG.HomeBase
      || source is not (PlanetDamageSource.ManualShots or PlanetDamageSource.AutoCannon
        or PlanetDamageSource.MiningLaser or PlanetDamageSource.ArcHarpoon
        or PlanetDamageSource.RocketPods or PlanetDamageSource.Railgun)) return;
    double now = BaseGame.Time?.TotalGameTime.TotalSeconds ?? 0;
    if (now < nextWeaponSupportRoll) return;
    // At most one roll per five seconds of combat, regardless of beam tick count or damage.
    nextWeaponSupportRoll = now + 5;
    if (Random.Shared.NextDouble() >= 0.15) return;
    var module = WeaponSupportCatalog.All[Random.Shared.Next(WeaponSupportCatalog.All.Length)];
    m_gameState.Weapons.AwardSupport(module);
    ShowWorldPopup(PlanetPos, Loc.F("MODULE FOUND: {0}", WeaponSupportCatalog.Name(module)), true);
    AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
    SaveProgress();
  }
}
