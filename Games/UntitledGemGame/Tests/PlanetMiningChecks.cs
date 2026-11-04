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

    // Gems stay near the planet for most of a run; reach opens late and spans the
    // complete play area at the tuning table's last fire power (GemSpreadTuning).
    UntitledGemGame.Screens.UntitledGemGameGameScreen.PlanetPos = Vector2.Zero;
    var miningBounds = new PlayAreaBounds(new Vector2(-1000, -600), new Vector2(1000, 600));
    float innerReach = UntitledGemGame.Screens.UntitledGemGameGameScreen.PlanetRadius
      + UntitledGemGame.Screens.UntitledGemGameGameScreen.PlanetDebrisGap;
    float fullReach = new Vector2(1000, 600).Length();
    float ReachFraction(int firePower) =>
      (UntitledGemGame.Screens.UntitledGemGameGameScreen.PlanetDebrisReach(miningBounds, firePower) - innerReach)
      / (fullReach - innerReach);
    int fullReachPower = GemSpreadTuning.FullReachFirePower;
    Check(ReachFraction(1) <= 0.07f, "The starter cannon must keep debris close to the planet");
    Check(ReachFraction(15) < 0.25f, "Mid-game fire power must keep debris fairly close to the planet");
    Check(Math.Abs(ReachFraction(fullReachPower) - 1f) < 0.001f, "Late-game fire power must retain full-field reach");
    float previousReach = ReachFraction(1);
    for (int firePower = 2; firePower <= fullReachPower; firePower++)
    {
      float reach = ReachFraction(firePower);
      Check(reach > previousReach, "Every fire-power rank through full reach must spread debris farther");
      previousReach = reach;
    }

    // Each weapon adds production of its own; its fire rate and fire power scale only it.
    var ug = new UpgradesGeneratorUpgrades();
    Check(MainShipWeapons.GemsPerSecond(ug) == 0, "The starting cannon only fires when clicked");
    ug.AutoCannon = true;
    double previous = MainShipWeapons.GemsPerSecond(ug);
    Check(Math.Abs(previous - 1 / MainShipWeapons.CannonInterval) < 1e-6, "The automated cannon fires once per interval");
    foreach (var unlock in new Action[] { () => ug.MiningLaser = true, () => ug.ArcHarpoon = true,
      () => ug.RocketPods = true, () => ug.Railgun = true, () => ug.FlakBattery = true })
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
      // Double fire rate and triple fire power; the laser's and flak's fire power adds half its base stream per point.
      double scale = 2 * (weapon == MainShipWeapon.Laser ? 1 + 2 * MainShipWeapons.LaserPowerDamage
        : weapon == MainShipWeapon.Flak ? 1 + 2 * MainShipWeapons.FlakPowerDamage : 3);
      Check(Math.Abs(MainShipWeapons.GemsPerSecond(ug, weapon, 2f, 3) - alone * scale) < 1e-6,
        $"{weapon} fire rate and fire power must scale its output");
      double others = MainShipWeapons.GemsPerSecond(ug, w => w == weapon ? 2f : 1f, w => w == weapon ? 3 : 1);
      Check(Math.Abs(others - unit - alone * (scale - 1)) < 1e-6, $"{weapon} upgrades must not change the other weapons");
    }

    // Every special adds to its own weapon's output and leaves the others alone.
    var specials = new (MainShipWeapon Weapon, Action Enable)[]
    {
      (MainShipWeapon.Cannon, () => ug.CannonRicochet = true), (MainShipWeapon.Cannon, () => ug.CannonCritical = true),
      (MainShipWeapon.Laser, () => ug.LaserMagmaScars = true), (MainShipWeapon.Laser, () => ug.LaserOverheat = true),
      (MainShipWeapon.Harpoon, () => ug.HarpoonConductiveBarbs = true),
      (MainShipWeapon.Harpoon, () => ug.HarpoonForkedCurrent = true),
      (MainShipWeapon.Harpoon, () => ug.HarpoonCapacitorDischarge = true),
      (MainShipWeapon.Harpoon, () => ug.HarpoonTectonicWinch = true),
      (MainShipWeapon.Rockets, () => ug.RocketClusterWarheads = true), (MainShipWeapon.Rockets, () => ug.RocketOrbitalStrike = true),
      (MainShipWeapon.Railgun, () => ug.RailgunShockwave = true),
      (MainShipWeapon.Flak, () => ug.FlakWideChoke = true),
      (MainShipWeapon.Flak, () => ug.FlakFlechettes = true),
      (MainShipWeapon.Flak, () => ug.FlakBarrage = true),
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

    // The Weaponry blueprints: each weapon blueprint unlocks from HB and has its own upgrade tree.
    var tree = new Upgrades();
    tree.LoadJson(File.ReadAllText("Content/Data/upgrades.json"),
      File.ReadAllText("Content/Data/upgrades_buttons.json"), tree.UpgradeButtons, tree.UpgradeDefinitions);
    var buttons = tree.UpgradeButtons;
    foreach (var (id, parent) in new[] { ("AC1", "HB"), ("CFR1", "AC1"), ("CFP1", "AC1"), ("CSS1", "CFP1"),
      ("LZ1", "HB"), ("LZR1", "LZ1"), ("LZP1", "LZ1"), ("LZT1", "LZP1"), ("LZD1", "LZV1"),
      ("AH1", "HB"), ("AHR1", "AH1"), ("AHP1", "AH1"), ("AHF1", "AHR1"),
      ("AHDX1", "AH1"), ("AHB1", "AHP1"), ("AHD1", "AHB1"), ("AHW1", "AHD1"),
      ("RP1", "HB"), ("RPR1", "RP1"), ("RPP1", "RP1"), ("RPC1", "RP1"),
      ("RG1", "HB"), ("RGR1", "RG1"), ("RGP1", "RG1"), ("RGF1", "RG1"),
      ("FK1", "HB"), ("FKR1", "FK1"), ("FKP1", "FK1"), ("FKW1", "FKR1"), ("FKC1", "FK1"), ("FKF1", "FKP1"), ("FKS1", "FKC1"),
      ("CRB1", "AC1"), ("CCR1", "CFR1"), ("LZM1", "LZ1"), ("LZH1", "LZR1"),
      ("RCW1", "RPP1"), ("ROS1", "RPR1"), ("BTS1", "RGP1"), ("RCN1", "RGR1") })
      Check(buttons.TryGetValue(id, out var node) && node.Data.BlockedBy == parent,
        $"Weapon node {id} must follow {parent}");
    foreach (var id in DebugProgressionPresets.FullWeaponNodes)
    {
      Check(Weaponry.IsInTree(id), $"Weapon node {id} must belong to the Weaponry tree");
      Check(File.Exists(Path.Combine("Content", buttons[id].Data.UpgradeDefinition.Icon)), $"{id} needs its icon");
    }

    var expandedWeapons = new UpgradesGeneratorUpgrades
    {
      HarpoonForkBonus = 0.75f, HarpoonWinchBonus = 2, HarpoonExtraction = true,
      RocketOrbitBonus = 0.45f, RocketOrbitalOverload = true, RocketClusterBonus = 0.3f,
      RailgunWaveBonus = 0.5f, RailgunSuperconductor = true,
    };
    Check(MainShipWeapons.HarpoonForkDamage(expandedWeapons) == 1.25f
      && MainShipWeapons.HarpoonExtractionPulses(expandedWeapons) == 8,
      "Harpoon upgrades must strengthen forks and double upgraded extraction");
    Check(Math.Abs(MainShipWeapons.RocketOrbitalDamage(expandedWeapons) - 3.4f) < 0.0001f
      && Math.Abs(MainShipWeapons.RocketClusterDamage(expandedWeapons) - 0.9f) < 0.0001f,
      "Rocket upgrades must amplify orbital and cluster payloads");
    Check(MainShipWeapons.RailgunWaveDamage(expandedWeapons) == 1f
      && MainShipWeapons.RailgunConductorDamage(expandedWeapons) == 1f,
      "Railgun upgrades must amplify shockwaves and conductor arcs");
    int basePayload = MainShipWeapons.RailgunGems(expandedWeapons, 10);
    expandedWeapons.RailgunPayloadBonus = 0.6f;
    Check(MainShipWeapons.RailgunGems(expandedWeapons, 10) >= basePayload * 1.59,
      "Dense Slugs must increase the actual railgun payload");
    foreach (var weapon in new[] { MainShipWeapon.Harpoon, MainShipWeapon.Rockets, MainShipWeapon.Railgun })
      foreach (int column in Enumerable.Range(0, 3))
      {
        string capstone = Weaponry.ForWeapon(weapon).Rows.Select(row => row[column]).Last(id => id != "");
        Check(buttons[capstone].Data.UpgradeDefinition.Currency == CoreShards.Currency,
          $"Every {weapon} column must end in a core shard upgrade");
      }

    var laserUpgrades = new UpgradesGeneratorUpgrades { LaserMagmaScars = true, LaserScarDamage = 0.75f };
    Check(Math.Abs(MainShipWeapons.LaserScarShare(laserUpgrades) - 1.75f) < 0.0001,
      "Deep Melting must add to scar damage");
    laserUpgrades.LaserMagmaReactor = true;
    Check(Math.Abs(MainShipWeapons.LaserScarShare(laserUpgrades) - 3.5f) < 0.0001,
      "Magma Reactor must double upgraded scars");
    laserUpgrades.LaserSurgeDuration = 1.5f;
    Check(MainShipWeapons.LaserSurgeSeconds(laserUpgrades) == 3f,
      "Heat Capacitors must extend surges");
    laserUpgrades.MiningLaserThermalLance = true;
    laserUpgrades.LaserThermalValue = 0.75f;
    Check(MainShipWeapons.LaserGemValue(laserUpgrades) == 2.25f,
      "Crystal Refinement must increase thermal gem value");
    foreach (int column in Enumerable.Range(0, 3))
    {
      string capstone = Weaponry.Tabs[1].Rows.Select(row => row[column]).Last(id => id != "");
      Check(buttons[capstone].Data.UpgradeDefinition.Currency == CoreShards.Currency,
        "Every laser column must end in a core shard upgrade");
    }

    var cannonUpgrades = new UpgradesGeneratorUpgrades { CannonRicochet = true };
    double cannonBefore = MainShipWeapons.SpecialMultiplier(cannonUpgrades, MainShipWeapon.Cannon);
    cannonUpgrades.CannonExtraBounces = 2;
    cannonUpgrades.CannonBounceRetention = 0.3f;
    Check(MainShipWeapons.SpecialMultiplier(cannonUpgrades, MainShipWeapon.Cannon) > cannonBefore,
      "Ricochet upgrades must increase cannon output");
    cannonBefore = MainShipWeapons.SpecialMultiplier(cannonUpgrades, MainShipWeapon.Cannon);
    cannonUpgrades.CannonBlastRadius = 0.4f;
    cannonUpgrades.CannonAftershock = cannonUpgrades.CannonSeismic = true;
    Check(Math.Abs(MainShipWeapons.SpecialMultiplier(cannonUpgrades, MainShipWeapon.Cannon) / cannonBefore - 2.4) < 0.0001,
      "Blast, aftershock and seismic damage must contribute to cannon output");
    foreach (int column in Enumerable.Range(0, 3))
    {
      string capstone = Weaponry.Tabs[0].Rows.Select(row => row[column]).Last(id => id != "");
      Check(buttons[capstone].Data.UpgradeDefinition.Currency == CoreShards.Currency,
        "Every cannon column must end in a core shard upgrade");
    }

    // Every visible edge must descend from its actual prerequisite and clear all
    // other nodes in the three straight weapon columns.
    Weaponry.ApplyLayout(buttons);
    var weaponJoints = new Dictionary<string, UpgradeJoint>();
    foreach (var tab in Weaponry.Tabs)
    {
      Check(tab.Rows.All(row => row.Length == 3), $"{tab.Name} must fit three columns");
      foreach (var id in tab.Rows.SelectMany(row => row).Where(id => id != "" && id != tab.Root))
      {
        var node = buttons[id];
        var parent = buttons[node.Data.BlockedBy];
        var position = Weaponry.Locate(id)!.Value;
        var parentPosition = Weaponry.Locate(parent.Data.ShortName)!.Value;
        Check(parentPosition.Tab == position.Tab && parentPosition.Row < position.Row,
          $"{id} must sit below its prerequisite {parent.Data.ShortName}");
        Check(position.Row == parentPosition.Row + 1 && (parentPosition.Row == 0 || position.Column == parentPosition.Column),
          $"{id} must connect directly down its column");
        weaponJoints.Add(id, new UpgradeJoint { StartButton = parent, EndButton = node });
      }
    }
    Weaponry.RouteLinks(weaponJoints);
    foreach (var (id, joint) in weaponJoints)
    {
      Vector2 Center(UpgradeButton node)
        => new(node.Data.PosX + 25f * node.Data.ButtonSizeScale,
          node.Data.PosY + 25f * node.Data.ButtonSizeScale);
      var offset = new Vector2(25f * joint.StartButton.Data.ButtonSizeScale);
      var path = new[] { Center(joint.StartButton) }
        .Concat(joint.MidwayPoints.Select(point => point + offset))
        .Append(Center(joint.EndButton)).ToArray();
      foreach (var other in Weaponry.Tabs[Weaponry.TabOf(id)].Rows.SelectMany(row => row)
        .Where(other => other != "" && other != id && other != joint.StartButton.Data.ShortName))
      {
        var node = buttons[other];
        var c = Center(node);
        float half = 25f * node.Data.ButtonSizeScale + 8f;
        for (int i = 1; i < path.Length; i++)
        {
          var a = path[i - 1];
          var b = path[i];
          bool crosses = Math.Min(a.X, b.X) < c.X + half && Math.Max(a.X, b.X) > c.X - half
            && Math.Min(a.Y, b.Y) < c.Y + half && Math.Max(a.Y, b.Y) > c.Y - half;
          Check(!crosses, $"Link to {id} must clear {other}");
        }
      }
    }

    // WeaponryLoadout slot equipping, swapping, unequipping, and hull mount coordinates.
    var loadout = new WeaponryLoadout();
    Check(loadout.IsFitted(MainShipWeapon.Cannon), "Primary cannon is always fitted in the default bow mount");
    Check(!loadout.IsFitted(MainShipWeapon.Laser), "Secondary weapons start unfitted until unlocked/equipped");
    Check(loadout.TryEquip(MainShipWeapon.Rockets, 0) && loadout.FindSlot(MainShipWeapon.Rockets) == 0,
      "A weapon can be fitted into any of the 4 customizable hardpoint slots");
    Check(loadout.GetMount(MainShipWeapon.Rockets) == Weaponry.HardpointSlots[0].HullMount,
      "Fitted weapon uses the hull coordinates of its chosen hardpoint slot");
    Check(!loadout.TryEquip(MainShipWeapon.Laser, 4), "Out-of-range weapon mount rejects a drop");
    Check(loadout.TryEquip(MainShipWeapon.Laser, 2) && loadout.FindSlot(MainShipWeapon.Laser) == 2,
      "Second weapon equips into slot 2");
    Check(loadout.TryEquip(MainShipWeapon.Rockets, 2)
      && loadout.FindSlot(MainShipWeapon.Rockets) == 2
      && loadout.FindSlot(MainShipWeapon.Laser) == 0,
      "Equipping an already-fitted weapon onto an occupied slot swaps the two weapons");
    Check(loadout.TryUnequip(MainShipWeapon.Rockets) && !loadout.IsFitted(MainShipWeapon.Rockets),
      "Unequipping a weapon removes it from its hardpoint slot");
    loadout.SyncWithUnlockedBlueprints(w => w is MainShipWeapon.Laser or MainShipWeapon.Rockets);
    Check(!loadout.IsFitted(MainShipWeapon.Rockets),
      "Explicitly unequipped weapon stays unequipped during blueprint sync");

    string[] retired = ["MGC", "GSC", "GSR", "GSQ", "LG", "GSh", "GCo", "ClG", "ClZ", "CosCl", "SCl", "MV", "ML"];
    Check(!buttons.Values.Any(b => retired.Contains(b.Data.UpgradeDefinition.ShortName)),
      "Old spawning upgrades are gone from the tree");

    Console.WriteLine("Planet mining checks passed: obstacle steering, weapon output, weapon specials and the weapons branch.");
  }
}
