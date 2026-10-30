using UntitledGemGame;

internal static class WeaponSupportChecks
{
  public static void Run()
  {
    static void Check(bool ok, string message)
    {
      if (!ok) throw new Exception(message);
    }
    var loadout = new WeaponryLoadout();
    Check(loadout.UnlockedHardpoints == 2, "Start with two open secondary hardpoints");
    Check(!loadout.TryEquip(MainShipWeapon.Laser, 2), "Locked weapon mount rejects a drop");
    loadout.SyncWithUnlockedBlueprints(_ => true);
    Check(!loadout.IsFitted(MainShipWeapon.Laser), "Buying a blueprint leaves it in inventory");
    Check(loadout.TryEquip(MainShipWeapon.Laser, 0), "Equip unlocked mount");
    loadout.AwardSupport(WeaponSupportModule.Accelerator);
    loadout.AwardSupport(WeaponSupportModule.Payload);
    Check(!loadout.EquipSupport(WeaponSupportModule.Payload, 9), "Locked support rejects a drop");
    Check(loadout.SupportInventory[2] == 1, "Invalid drops preserve inventory");
    Check(loadout.EquipSupport(WeaponSupportModule.Accelerator, 3), "Fit accelerator");
    Check(loadout.EquipSupport(WeaponSupportModule.Payload, 4), "Fit payload");
    Check(loadout.SupportBonus(MainShipWeapon.Laser) == (1.25f, 2), "Mount supports modify its weapon");
    Check(loadout.SupportBonus(MainShipWeapon.Cannon) == (1f, 0), "Other weapons remain unchanged");
    Check(!loadout.EquipSupport(WeaponSupportModule.Payload, 5), "Cannot duplicate an owned module");
    Check(loadout.EquipSupport(WeaponSupportModule.Payload, 3, 4), "Swap modules between sockets");
    Check(loadout.Supports[3] == WeaponSupportModule.Payload && loadout.Supports[4] == WeaponSupportModule.Accelerator,
      "Swapping conserves both modules");
    loadout.TryEquip(MainShipWeapon.Rockets, 0);
    Check(loadout.SupportBonus(MainShipWeapon.Rockets) == (1.25f, 2), "Supports stay with hardpoint on replacement");
    Check(loadout.SupportBonus(MainShipWeapon.Laser) == (1f, 0), "Unequipped weapon has no support bonus");
    loadout.SetUnlockedHardpoints(4);
    loadout.TryEquip(MainShipWeapon.Railgun, 3);
    loadout.AwardSupport(WeaponSupportModule.Overcharger);
    loadout.EquipSupport(WeaponSupportModule.Overcharger, 12);
    var save = new GameSave { Weapons = loadout.Clone() };
    string path = Path.Combine(Path.GetTempPath(), $"weapon-support-{Guid.NewGuid()}.json");
    WeaponryLoadout restored;
    try
    {
      var store = new GameSaveStore(path);
      Check(store.Save(save), "Save equipment");
      restored = store.Load()!.Weapons;
    }
    finally { File.Delete(path); }
    restored.SetUnlockedHardpoints(4);
    Check(restored.Slots[3] == MainShipWeapon.Railgun && restored.Supports[12] == WeaponSupportModule.Overcharger,
      "Current save format round-trips weapons and supports");
    loadout.ReturnSupport(3);
    Check(restored.Supports[3] == WeaponSupportModule.Payload, "Save clone owns its arrays");
    restored.SetUnlockedHardpoints(2);
    Check(restored.Slots[3] == null && restored.SupportInventory[3] == 1 && restored.Supports[12] == WeaponSupportModule.None,
      "Removing talent returns modules and weapons from locked mounts");
    restored.ResetRun();
    Check(restored.Supports.All(m => m == WeaponSupportModule.None) && restored.SupportInventory.All(n => n == 0),
      "Run reset clears run equipment");
    Console.WriteLine("Weapon support checks passed: locks, conservation, swapping, bonuses, save round-trip and reset.");
  }
}
