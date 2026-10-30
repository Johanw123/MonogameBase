using System;
using System.Text.Json.Serialization;

namespace UntitledGemGame;

public enum WeaponSupportModule { None, Accelerator, Payload, Overcharger }

public static class WeaponSupportCatalog
{
  public const int SlotsPerMount = 3;
  public static readonly WeaponSupportModule[] All =
    [WeaponSupportModule.Accelerator, WeaponSupportModule.Payload, WeaponSupportModule.Overcharger];
  public static string Name(WeaponSupportModule module) => module switch
  {
    WeaponSupportModule.Accelerator => Loc.T("Accelerator"),
    WeaponSupportModule.Payload => Loc.T("Payload Core"),
    WeaponSupportModule.Overcharger => Loc.T("Overcharger"),
    _ => Loc.T("Support socket")
  };
  public static string Description(WeaponSupportModule module) => module switch
  {
    WeaponSupportModule.Accelerator => Loc.T("+25% fire rate for this mount's weapon. Bonuses add together."),
    WeaponSupportModule.Payload => Loc.T("+2 fire power for this mount's weapon. Bonuses add together."),
    WeaponSupportModule.Overcharger => Loc.T("+10% fire rate and +1 fire power for this mount's weapon."),
    _ => Loc.T("Drag a weapon module here. Modules stay with their hardpoint when you swap weapons.")
  };
  // Reuse the existing licensed module icon atlas.
  public static ShipModule Icon(WeaponSupportModule module) => module switch
  {
    WeaponSupportModule.Accelerator => ShipModule.IonBooster,
    WeaponSupportModule.Payload => ShipModule.GunPod,
    _ => ShipModule.OverdriveCoil
  };
}

public sealed partial class WeaponryLoadout
{
  // Mount 0 is the fixed bow; mounts 1–4 are the customizable hardpoints.
  public WeaponSupportModule[] Supports { get; set; } = new WeaponSupportModule[15];
  public int[] SupportInventory { get; set; } = new int[4];
  [JsonIgnore] public int UnlockedHardpoints { get; private set; } = 2;

  public void SetUnlockedHardpoints(int count)
  {
    UnlockedHardpoints = Math.Clamp(count, 2, Weaponry.MaxCustomSlots);
    for (int slot = UnlockedHardpoints; slot < Weaponry.MaxCustomSlots; slot++)
    {
      TryUnequipSlot(slot);
      for (int support = 0; support < WeaponSupportCatalog.SlotsPerMount; support++)
        ReturnSupport((slot + 1) * WeaponSupportCatalog.SlotsPerMount + support);
    }
  }

  public bool IsSupportUnlocked(int index)
    => index >= 0 && index < (UnlockedHardpoints + 1) * WeaponSupportCatalog.SlotsPerMount;

  public bool EquipSupport(WeaponSupportModule module, int target, int source = -1)
  {
    if (module <= WeaponSupportModule.None || module > WeaponSupportModule.Overcharger
      || !IsSupportUnlocked(target)) return false;
    if (source >= 0)
    {
      if (!IsSupportUnlocked(source) || Supports[source] != module) return false;
      (Supports[source], Supports[target]) = (Supports[target], Supports[source]);
      return true;
    }
    if (SupportInventory[(int)module] <= 0) return false;
    SupportInventory[(int)module]--;
    ReturnSupport(target);
    Supports[target] = module;
    return true;
  }

  public bool ReturnSupport(int index)
  {
    if (index < 0 || index >= Supports.Length || Supports[index] == WeaponSupportModule.None) return false;
    SupportInventory[(int)Supports[index]]++;
    Supports[index] = WeaponSupportModule.None;
    return true;
  }

  public void AwardSupport(WeaponSupportModule module)
  {
    if (module > WeaponSupportModule.None && module <= WeaponSupportModule.Overcharger
      && SupportInventory[(int)module] < int.MaxValue)
      SupportInventory[(int)module]++;
  }

  public (float Rate, int Power) SupportBonus(MainShipWeapon weapon)
  {
    int mount = weapon == MainShipWeapon.Cannon ? 0 : FindSlot(weapon) + 1;
    if (weapon != MainShipWeapon.Cannon && mount == 0) return (1f, 0);
    float rate = 1;
    int power = 0;
    for (int i = mount * 3; i < mount * 3 + 3; i++)
    {
      switch (Supports[i])
      {
        case WeaponSupportModule.Accelerator: rate += 0.25f; break;
        case WeaponSupportModule.Payload: power += 2; break;
        case WeaponSupportModule.Overcharger: rate += 0.1f; power++; break;
      }
    }
    return (rate, power);
  }
}
