using Microsoft.Xna.Framework;
using UntitledGemGame;

internal static class PlanetMiningChecks
{
  public static void Run()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    // Ships steering around the planet never enter it and still arrive.
    var center = new Vector2(400, 300);
    const float radius = 113f;
    for (int start = 0; start < 16; start++)
    {
      float angle = start * MathHelper.TwoPi / 16f;
      var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 420f;
      var target = center - (position - center) * 0.8f + new Vector2(0, start % 3 - 1) * 40f;
      int steps = 0;
      while (Vector2.Distance(position, target) > 2f && steps++ < 2000)
      {
        var waypoint = PlanetObstacle.Steer(position, target, center, radius);
        var step = waypoint - position;
        position += step.Length() > 4f ? Vector2.Normalize(step) * 4f : step;
        Check(Vector2.Distance(position, center) >= radius - 0.01f, $"A ship from {start} must not cut through the planet");
      }
      Check(steps < 2000, $"A ship from {start} must get around the planet");
    }
    Check(PlanetObstacle.PushOut(center + new Vector2(5, 0), center, radius) == center + new Vector2(radius, 0),
      "Positions inside the planet are pushed to its edge");
    Check(PlanetObstacle.Steer(center + new Vector2(-300, -200), center + new Vector2(300, -200), center, radius)
      == center + new Vector2(300, -200), "A clear line of sight goes straight to the target");

    // Each weapon adds production of its own; its fire rate and fire power scale only it.
    var ug = new UpgradesGeneratorUpgrades();
    Check(MainShipWeapons.GemsPerSecond(ug) == 0, "The starting cannon only fires when clicked");
    ug.AutoCannon = true;
    double previous = MainShipWeapons.GemsPerSecond(ug);
    Check(Math.Abs(previous - 1 / MainShipWeapons.CannonInterval) < 1e-6, "The automated cannon fires once per interval");
    foreach (var unlock in new Action[] { () => ug.MiningLaser = true, () => ug.RocketPods = true, () => ug.BigSpaceGun = true })
    {
      unlock();
      double next = MainShipWeapons.GemsPerSecond(ug);
      Check(next > previous, "Each weapon must add gems per second");
      previous = next;
    }
    double unit = MainShipWeapons.GemsPerSecond(ug, _ => 1f, _ => 1);
    foreach (var weapon in MainShipWeapons.All)
    {
      double alone = MainShipWeapons.GemsPerSecond(ug, weapon, 1f, 1);
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(ug, weapon, 2f, 3) - alone * 6) < 1e-6,
        $"{weapon} fire rate and fire power must scale its output");
      double others = MainShipWeapons.GemsPerSecond(ug, w => w == weapon ? 2f : 1f, w => w == weapon ? 3 : 1);
      Check(Math.Abs(others - unit - alone * 5) < 1e-6, $"{weapon} upgrades must not change the other weapons");
    }

    // Every special adds to its own weapon's output and leaves the others alone.
    var specials = new (MainShipWeapon Weapon, Action Enable)[]
    {
      (MainShipWeapon.Cannon, () => ug.CannonRicochet = true), (MainShipWeapon.Cannon, () => ug.CannonCritical = true),
      (MainShipWeapon.Laser, () => ug.LaserMagmaScars = true), (MainShipWeapon.Laser, () => ug.LaserOverheat = true),
      (MainShipWeapon.Rockets, () => ug.RocketClusterWarheads = true), (MainShipWeapon.Rockets, () => ug.RocketOrbitalStrike = true),
      (MainShipWeapon.BigSpaceGun, () => ug.BigSpaceGunShockwave = true),
      (MainShipWeapon.BigSpaceGun, () => ug.BigSpaceGunSingularity = true),
    };
    foreach (var (weapon, enable) in specials)
    {
      var others = MainShipWeapons.All.Where(w => w != weapon).Select(w => MainShipWeapons.GemsPerSecond(ug, w, 1f, 1)).ToArray();
      double before = MainShipWeapons.GemsPerSecond(ug, weapon, 1f, 1);
      enable();
      Check(MainShipWeapons.GemsPerSecond(ug, weapon, 1f, 1) > before, $"A {weapon} special must add gems per second");
      Check(MainShipWeapons.All.Where(w => w != weapon).Select(w => MainShipWeapons.GemsPerSecond(ug, w, 1f, 1))
        .SequenceEqual(others), $"A {weapon} special must not change the other weapons");
    }

    // The weapons spine: each weapon unlocks after the one before and has its own upgrades.
    var tree = new Upgrades();
    tree.LoadJson(File.ReadAllText("Content/Data/upgrades.json"),
      File.ReadAllText("Content/Data/upgrades_buttons.json"), tree.UpgradeButtons, tree.UpgradeDefinitions);
    var buttons = tree.UpgradeButtons;
    foreach (var (id, parent) in new[] { ("AC1", "HB"), ("CFR1", "AC1"), ("CFP1", "AC1"), ("CSS1", "CFP1"),
      ("LZ1", "CFP2"), ("LZR1", "LZ1"), ("LZP1", "LZ1"), ("LZT1", "LZP1"), ("LZD1", "LZT1"),
      ("RP1", "LZ1"), ("RPR1", "RP1"), ("RPP1", "RP1"), ("RPC1", "RPP1"),
      ("BSG1", "RP1"), ("BSGR1", "BSG1"), ("BSGP1", "BSG1"), ("BSGF1", "BSGP1"),
      ("CRB1", "CFR1"), ("CCR1", "CFR2"), ("LZM1", "LZR1"), ("LZH1", "LZM1"),
      ("RCW1", "RPC1"), ("ROS1", "RPR1"), ("BTS1", "BSGF1"), ("BSS1", "BSGR1") })
      Check(buttons.TryGetValue(id, out var node) && node.Data.BlockedBy == parent,
        $"Weapon node {id} must follow {parent}");
    foreach (var id in DebugProgressionPresets.FullWeaponNodes)
      Check(File.Exists(Path.Combine("Content", buttons[id].Data.UpgradeDefinition.Icon)), $"{id} needs its icon");
    string[] retired = ["MGC", "GSC", "GSR", "GSQ", "LG", "GSh", "GCo", "ClG", "ClZ", "CosCl", "SCl", "MV", "ML"];
    Check(!buttons.Values.Any(b => retired.Contains(b.Data.UpgradeDefinition.ShortName)),
      "Old spawning upgrades are gone from the tree");

    Console.WriteLine("Planet mining checks passed: obstacle steering, weapon output, weapon specials and the weapons branch.");
  }
}
