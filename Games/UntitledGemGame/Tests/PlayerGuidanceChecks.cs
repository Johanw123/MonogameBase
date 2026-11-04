using UntitledGemGame;

// What the terminal marks for a stuck player: a bought weapon still off the ship, and a run past
// the point where extracting pays. Drawing lives in the GUI; these are the rules behind it.
internal static class PlayerGuidanceChecks
{
  private static void Check(bool ok, string message)
  {
    if (!ok) throw new Exception(message);
  }

  public static void Run()
  {
    CheckWeaponNudge();
    CheckFirstRunPoints();
    CheckWall();
    Console.WriteLine("Player guidance checks passed: unattached weapons, first run points and income walls.");
  }

  private static void CheckWeaponNudge()
  {
    var loadout = new WeaponryLoadout();
    Func<MainShipWeapon, bool> none = _ => false;
    Func<MainShipWeapon, bool> laser = weapon => weapon == MainShipWeapon.Laser;
    Func<MainShipWeapon, bool> all = _ => true;

    Check(loadout.AwaitingHardpoint(none) == null, "Nothing bought, nothing to attach");
    Check(loadout.AwaitingHardpoint(laser) == MainShipWeapon.Laser, "A bought weapon off the ship needs attaching");
    Check(loadout.OpenSlotFor(MainShipWeapon.Laser) == Weaponry.DefaultSlotForWeapon(MainShipWeapon.Laser),
      "A weapon is pointed at its natural hardpoint");

    Check(loadout.TryEquip(MainShipWeapon.Laser, 0), "The laser attaches");
    Check(loadout.AwaitingHardpoint(laser) == null, "An attached weapon needs nothing");

    Check(loadout.TryUnequipSlot(0), "The laser comes off");
    Check(loadout.AwaitingHardpoint(laser) == null, "A weapon the player took off is their choice");

    var next = new WeaponryLoadout();
    Check(next.TryEquip(MainShipWeapon.Rockets, 0), "Rockets take the laser's natural hardpoint");
    Check(next.OpenSlotFor(MainShipWeapon.Laser) == 1, "A taken natural hardpoint falls back to the first empty one");

    var displaced = new WeaponryLoadout();
    Check(displaced.TryEquip(MainShipWeapon.Laser, 0) && displaced.TryEquip(MainShipWeapon.Rockets, 0),
      "Rockets replace the laser");
    Check(displaced.AwaitingHardpoint(all) == MainShipWeapon.Laser, "A replaced weapon was not taken off on purpose");

    var full = new WeaponryLoadout();
    Check(full.TryEquip(MainShipWeapon.Laser, 0) && full.TryEquip(MainShipWeapon.Harpoon, 1)
      && full.TryEquip(MainShipWeapon.Rockets, 2) && full.TryEquip(MainShipWeapon.Railgun, 3), "Four weapons fill the ship");
    Check(full.AwaitingHardpoint(all) == null, "No hardpoint free, nothing to ask for");
  }

  private static void CheckFirstRunPoints()
  {
    var guide = new ExtractionGuide();
    Check(!guide.Recommended(0, 0), "A first run with no point can't extract");
    guide.Update(1, 1, 0.1);
    Check(!guide.Recommended(1, 0), "One point is too early while the bar climbs");
    guide.Update(1, 2, 0.1);
    Check(!guide.Recommended(2, 0), "Two points is too early while the bar climbs");
    guide.Update(1, 3, 0.1);
    Check(guide.Recommended(3, 0), "Three points end a first run");
    Check(!guide.AtWall, "Reaching three points is not a wall");
    guide.Update(1, 4, 0.1);
    Check(guide.Recommended(4, 0), "A first run past three points stays worth ending");
    Check(!new ExtractionGuide().Recommended(3, 1), "Later runs have no point target");
  }

  private static void CheckWall()
  {
    var guide = new ExtractionGuide();
    guide.Update(1, 1, 0.4);
    guide.Update(ExtractionGuide.WallSeconds - 1, 1, 0.4);
    Check(!guide.AtWall, "Short of the wall time");
    guide.Update(1, 1, 0.4);
    Check(guide.AtWall && guide.Recommended(1, 0), "A flat bar with a point to collect is a wall, even below three points");
    Check(guide.Recommended(1, 4), "Walls count on later runs too");
    Check(!guide.Recommended(0, 4), "A wall with nothing to collect suggests nothing");

    guide.Update(1, 1, 0.45);
    Check(!guide.AtWall, "A climbing bar leaves the wall behind");

    // A slow player: one small step of the bar every four minutes, for an hour.
    var slow = new ExtractionGuide();
    double bar = 0.1;
    slow.Update(1, 1, bar);
    for (int minutes = 0; minutes < 60; minutes += 4)
    {
      slow.Update(4 * 60, 1, bar += ExtractionGuide.WallClimb);
      Check(!slow.AtWall, "Slow progress is still progress");
    }

    var wobble = new ExtractionGuide();
    wobble.Update(1, 1, 0.5);
    wobble.Update(ExtractionGuide.WallSeconds, 1, 0.5 + ExtractionGuide.WallClimb / 2);
    Check(wobble.AtWall, "Income wobbling around one level is not climbing");

    var earned = new ExtractionGuide();
    earned.Update(1, 1, 0.9);
    earned.Update(ExtractionGuide.WallSeconds, 1, 0.9);
    earned.Update(1, 2, 0);
    Check(!earned.AtWall, "A new point restarts the clock, though the next bar starts lower");
  }
}
