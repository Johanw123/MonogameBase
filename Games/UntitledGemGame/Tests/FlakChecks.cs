using System;
using System.IO;
using System.Linq;
using UntitledGemGame;

internal static class FlakChecks
{
  public static void Run()
  {
    static void Check(bool ok, string message)
    {
      if (!ok) throw new Exception(message);
    }

    // 1. Blueprint unlocking and WeaponryLoadout auto-fit and hardpoint assignment.
    var loadout = new WeaponryLoadout();
    Check(!loadout.IsFitted(MainShipWeapon.Flak), "Flak starts unfitted");
    Check(Weaponry.DefaultSlotForWeapon(MainShipWeapon.Flak) == -1, "Flak has no default slot reserved; fits in any free hardpoint");

    // Auto-fit into the first free hardpoint slot (slot 0).
    Check(loadout.TryAutoFit(MainShipWeapon.Flak), "Auto-fit succeeds when hardpoints are open");
    Check(loadout.FindSlot(MainShipWeapon.Flak) == 0, "Flak auto-fits into the first empty slot (slot 0)");
    Check(loadout.IsFitted(MainShipWeapon.Flak), "Flak is now fitted");
    Check(loadout.GetMount(MainShipWeapon.Flak) == Weaponry.HardpointSlots[0].HullMount,
      "Fitted Flak uses the mount coordinates of its hardpoint slot");

    // Auto-fit falls back to subsequent slots when earlier ones are occupied.
    loadout.ResetRun();
    loadout.TryEquip(MainShipWeapon.Laser, 0);
    loadout.TryEquip(MainShipWeapon.Harpoon, 1);
    Check(loadout.TryAutoFit(MainShipWeapon.Flak), "Auto-fit finds the next available slot");
    Check(loadout.FindSlot(MainShipWeapon.Flak) == 2, "Flak auto-fits into slot 2 when slots 0 and 1 are taken");

    // Manual equipping into any of the 4 hardpoints (slots 0..3).
    for (int s = 0; s < Weaponry.MaxCustomSlots; s++)
    {
      loadout.TryEquip(MainShipWeapon.Flak, s);
      Check(loadout.FindSlot(MainShipWeapon.Flak) == s, $"Flak can be equipped into hardpoint {s}");
      Check(loadout.GetMount(MainShipWeapon.Flak) == Weaponry.HardpointSlots[s].HullMount,
        $"Flak in hardpoint {s} uses HardpointSlots[{s}].HullMount");
    }

    // Unequipping Flak.
    Check(loadout.TryUnequip(MainShipWeapon.Flak), "Unequipping Flak succeeds");
    Check(!loadout.IsFitted(MainShipWeapon.Flak), "Flak is no longer fitted after unequip");
    Check(loadout.Unequipped.Contains(MainShipWeapon.Flak), "Unequipped Flak is held in inventory");

    // Full loadout rejects auto-fit and keeps weapon in Unequipped.
    loadout.TryEquip(MainShipWeapon.Laser, 0);
    loadout.TryEquip(MainShipWeapon.Harpoon, 1);
    loadout.TryEquip(MainShipWeapon.Rockets, 2);
    loadout.TryEquip(MainShipWeapon.Railgun, 3);
    Check(!loadout.TryAutoFit(MainShipWeapon.Flak), "Auto-fit fails when all 4 hardpoints are occupied");
    Check(loadout.Unequipped.Contains(MainShipWeapon.Flak), "Flak remains unequipped in inventory");

    // Save/load round-trip preserves Flak in its assigned hardpoint slot.
    loadout.TryEquip(MainShipWeapon.Flak, 3);
    var save = new GameSave { Weapons = loadout.Clone() };
    string tempPath = Path.Combine(Path.GetTempPath(), $"flak-test-{Guid.NewGuid()}.json");
    try
    {
      var store = new GameSaveStore(tempPath);
      Check(store.Save(save), "Save loadout with Flak equipped");
      var loaded = store.Load()!.Weapons;
      Check(loaded.Slots[3] == MainShipWeapon.Flak, "Flak slot assignment round-trips through save file");
      Check(loaded.IsFitted(MainShipWeapon.Flak), "Loaded loadout reports Flak as fitted");
    }
    finally { if (File.Exists(tempPath)) File.Delete(tempPath); }

    // 2. Blueprint identification & tree mapping.
    Check(Weaponry.BlueprintWeapon("FK1") == MainShipWeapon.Flak, "FK1 unlocks Flak Battery");
    string[] flakNodes = ["FK1", "FKR1", "FKP1", "FKC1", "FKW1", "FKF1", "FKS1"];
    foreach (var node in flakNodes)
    {
      Check(Weaponry.IsInTree(node), $"Flak node {node} must belong to the Weaponry tree");
      Check(Weaponry.TabOf(node) == (int)MainShipWeapon.Flak, $"Flak node {node} must be on the Flak tab");
      Check(Weaponry.IsUnreleasedNode(node) == !Weaponry.IsReleased(MainShipWeapon.Flak),
        $"Flak node {node} follows the weapon's release lock");
    }
    // Flak is held back from this version; the shipped weapons stay buyable.
    Check(!Weaponry.IsReleased(MainShipWeapon.Flak), "Flak Battery is locked in this version");
    Check(Weaponry.IsAvailable(MainShipWeapon.Flak) == Demo.IsDev, "Only dev builds can unlock the Flak Battery");
    Check(Weaponry.IsUnavailableNode("FK1") == !Demo.IsDev, "The Flak blueprint is locked outside dev builds");
    bool wasDev = Demo.IsDev;
    try
    {
      Demo.IsDev = false;
      Check(!Weaponry.IsAvailable(MainShipWeapon.Flak) && Weaponry.IsUnavailableNode("FK1"),
        "Player builds lock the Flak Battery");
      Demo.IsDev = true;
      Check(Weaponry.IsAvailable(MainShipWeapon.Flak) && !Weaponry.IsUnavailableNode("FK1"),
        "Dev builds can unlock the Flak Battery");
    }
    finally { Demo.IsDev = wasDev; }
    foreach (var weapon in new[] { MainShipWeapon.Cannon, MainShipWeapon.Laser, MainShipWeapon.Harpoon,
      MainShipWeapon.Rockets, MainShipWeapon.Railgun })
      Check(Weaponry.IsReleased(weapon) && !Weaponry.IsUnreleasedNode(Weaponry.ForWeapon(weapon).Root),
        $"{weapon} is released");

    // 3. GemsPerSecond & upgrade math.
    var ug = new UpgradesGeneratorUpgrades();
    Check(!MainShipWeapons.IsAutomatic(ug, MainShipWeapon.Flak), "Flak is not automatic before unlock");
    Check(MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 1) == 0,
      "Flak produces 0 gems/sec before unlock");

    ug.FlakBattery = true;
    Check(MainShipWeapons.IsAutomatic(ug, MainShipWeapon.Flak), "Flak is automatic once unlocked");

    // Fresh unlock base output: 8 pellets * 5 damage / 2.5s = 16 gems/sec.
    double baseRate = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 1);
    Check(Math.Abs(baseRate - 16) < 1e-6, $"Base Flak rate must be 16 gems/sec, was {baseRate}");

    // Fire rate scaling: double fire rate -> double output.
    double doubleRate = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 2f, 1);
    Check(Math.Abs(doubleRate - baseRate * 2) < 1e-6, "Doubling fire rate must double output");

    // Fire power scaling: 1 + 0.2 * (firePower - 1). At firePower 3: 1 + 0.2 * 2 = 1.4x damage.
    double powerScale = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 3);
    Check(Math.Abs(powerScale - baseRate * 1.4) < 1e-6, "Fire power 3 must scale output by 1.4x");

    // Pellet count scaling: 10 pellets vs base 8 -> 10/8 = 1.25x.
    ug.FlakPelletCount = 10;
    double countScale = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 1);
    Check(Math.Abs(countScale - baseRate * 1.25) < 1e-6, "10 pellets must yield 1.25x output");
    ug.FlakPelletCount = MainShipWeapons.FlakBasePellets;

    // Wide Choke: +50% pellets, 80% damage -> 1.5 * 0.8 = 1.2x.
    ug.FlakWideChoke = true;
    double wideRate = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 1);
    Check(Math.Abs(wideRate - baseRate * 1.2) < 1e-4, "Wide Choke must multiply output by 1.2x");
    ug.FlakWideChoke = false;

    // Flechettes: +40% ricochet damage -> 1.4x.
    ug.FlakFlechettes = true;
    double flechetteRate = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 1);
    Check(Math.Abs(flechetteRate - baseRate * 1.4) < 1e-4, "Flechettes must multiply output by 1.4x");
    ug.FlakFlechettes = false;

    // Flak Barrage (Core Shard capstone): 2 bursts per cycle -> 2x output.
    ug.FlakBarrage = true;
    double barrageRate = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 1);
    Check(Math.Abs(barrageRate - baseRate * 2) < 1e-4, "Barrage must multiply output by 2x");
    ug.FlakBarrage = false;

    // All specials combined: 1.2 * 1.4 * 2 = 3.36x.
    ug.FlakWideChoke = true;
    ug.FlakFlechettes = true;
    ug.FlakBarrage = true;
    double allSpecialsRate = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Flak, 1f, 1);
    Check(Math.Abs(allSpecialsRate - baseRate * 3.36) < 1e-4, "Combined specials must multiply output by 3.36x");

    // Flak specials must not alter other weapons' output.
    var nonFlak = MainShipWeapons.All.Where(w => w != MainShipWeapon.Flak).ToArray();
    var beforeOutputs = nonFlak.Select(w => MainShipWeapons.GemsPerSecond(ug, w, 1f, 1)).ToArray();
    ug.FlakWideChoke = false;
    ug.FlakFlechettes = false;
    ug.FlakBarrage = false;
    var afterOutputs = nonFlak.Select(w => MainShipWeapons.GemsPerSecond(ug, w, 1f, 1)).ToArray();
    Check(beforeOutputs.SequenceEqual(afterOutputs), "Flak specials must not change other weapons' output");

    Console.WriteLine("Flak checks passed: auto-fit, slot assignment, persistence, tree mapping and upgrade math.");
  }
}
