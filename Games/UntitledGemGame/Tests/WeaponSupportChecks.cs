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
    Check(loadout.UnlockedHardpoints == 4, "Start with all four secondary hardpoints");
    Check(!loadout.TryEquip(MainShipWeapon.Laser, 4), "Out-of-range weapon mount rejects a drop");
    loadout.SyncWithUnlockedBlueprints(_ => true);
    Check(!loadout.IsFitted(MainShipWeapon.Laser), "Buying a blueprint leaves it in inventory");
    Check(loadout.TryEquip(MainShipWeapon.Laser, 0), "Equip unlocked mount");
    Check(!WeaponSupportCatalog.Enabled, "Weapon supports are disabled for this version");
    foreach (var module in WeaponSupportCatalog.All) loadout.AwardSupport(module);
    Check(loadout.SupportInventory.All(n => n == 0), "Disabled supports cannot be awarded");
    Check(Enumerable.Range(0, 15).All(i => !loadout.IsSupportUnlocked(i)), "All support sockets are disabled");
    // Seed equipped and inventory modules to check loaded/debug state cannot bypass the gate.
    loadout.SupportInventory[(int)WeaponSupportModule.Payload] = 2;
    loadout.Supports[3] = WeaponSupportModule.Payload;
    loadout.Supports[4] = WeaponSupportModule.Accelerator;
    loadout.Supports[0] = WeaponSupportModule.Overcharger;
    Check(!loadout.EquipSupport(WeaponSupportModule.Payload, 5), "Cannot equip disabled inventory modules");
    Check(!loadout.EquipSupport(WeaponSupportModule.Payload, 4, 3), "Cannot move disabled modules");
    Check(!loadout.ReturnSupport(3), "Disabled sockets cannot be edited");
    Check(loadout.SupportBonus(MainShipWeapon.Laser) == (1f, 0)
      && loadout.SupportBonus(MainShipWeapon.Cannon) == (1f, 0), "Equipped supports grant no bonuses");
    Check(loadout.SupportInventory[2] == 2 && loadout.Supports[3] == WeaponSupportModule.Payload,
      "Disabled interactions preserve equipment");
    loadout.TryEquip(MainShipWeapon.Railgun, 3);
    loadout.Supports[12] = WeaponSupportModule.Overcharger;
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
    Check(restored.Slots[3] == MainShipWeapon.Railgun && restored.Supports[12] == WeaponSupportModule.Overcharger,
      "Current save format round-trips weapons and supports");
    loadout.Supports[3] = WeaponSupportModule.None;
    Check(restored.Supports[3] == WeaponSupportModule.Payload, "Save clone owns its arrays");
    Check(restored.Slots[3] == MainShipWeapon.Railgun && restored.Supports[12] == WeaponSupportModule.Overcharger,
      "All four hardpoints retain equipment without a talent");
    restored.ResetRun();
    Check(restored.Supports.All(m => m == WeaponSupportModule.None) && restored.SupportInventory.All(n => n == 0),
      "Run reset clears run equipment");
    Console.WriteLine("Weapon support checks passed: disabled sockets, awards, interactions, bonuses, save round-trip and reset.");
  }
}
