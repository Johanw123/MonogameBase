using UntitledGemGame;

internal static class PrestigeTalentChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    var tree = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    var manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave());
    string[] activeTalents =
    [
      "TR1", "MHF1", "PRL1", "CAT1", "RCH1",
      "LR1", "ODP1", "SYF1", "KNH1", "MCSN1",
      "BR1", "MD1", "IOM1", "EP1", "AE1", "GVS1",
      "MBR1", "DG1", "KD1", "FSD1", "KNB1",
      "CN1", "HICM1", "CHR1", "SR1", "MRS1",
      "SGR1", "OVC1", "HVO1", "LOP1", "RCO1",
    ];
    Check(activeTalents.SequenceEqual(PrestigeTalentLayout.Tiers.SelectMany(tier => tier.Talents)),
      "The tiers hold exactly the designed talents, in order");
    Check(PrestigeTalentLayout.Tiers.Length == 6
      && PrestigeTalentLayout.Tiers.Select(t => t.RequiredEarlierPoints).SequenceEqual([0, 3, 5, 10, 16, 20]),
      "Six tiers open at 0, 3, 5, 10, 16 and 20 earlier points");
    Check(activeTalents.Distinct().Count() == 31
      && activeTalents.All(id => tree[id].Data.NumLevels == 1
        && tree[id].Data.LevelInfo.Count == 1 && tree[id].Data.LevelInfo[0].Cost == 1),
      "The complete tree contains thirty-one unique one-point talents");
    // The system unlocks are free rewards of the first four tiers.
    string[] systemUnlocks = ["CC1", ShipSystems.UnlockTalent, "SYU1", "SGU1"];
    Check(PrestigeTalentLayout.Tiers.Take(4).Select(tier => tier.FreeRewards[0]).SequenceEqual(systemUnlocks),
      "Command Center, Auxiliary Power, Shipyard and Deep Space Signals come free, one per tier");

    string[] freeRewards = PrestigeTalentLayout.Tiers.SelectMany(tier => tier.FreeRewards).ToArray();
    Check(freeRewards.SequenceEqual(["CC1", "XSP1", "SSU1", "XSP2", "SYU1", "XSP3", "SGU1", "XSP4", "XSP5", "CMY1"])
      && PrestigeTalentLayout.Tiers.All(tier => tier.FreeRewards.Length > 0)
      && freeRewards.All(id => PrestigeTalentLayout.IsFreeReward(id) && !PrestigeTalentLayout.IsInTree(id)
        && PrestigeTalentLayout.IsShown(id) && tree[id].Data.LevelInfo[0].Cost == 0),
      "Every tier shows free rewards that cost nothing and cannot be bought");
    Check(activeTalents.Concat(freeRewards).All(id => File.Exists(Path.Combine("Content", tree[id].Data.UpgradeDefinition.Icon))),
      "Every talent and free reward has an icon");
    foreach (string retired in new[] { "TPM1", "JHM1", "FLR1", "CA1", "RCM1", "QEM1", "MA1", "MM1", "CCN1", "MGS1",
      "DCM1", "OH1", "GM1", "MGD1", "PCO1", "MGF1", "WCM1", "SPC1", "HST1", "SCH1",
      "CBK1", "PO1", "MLC1", "SHS1", "DSP1" })
      Check(!PrestigeTalentLayout.IsShown(retired) && tree[retired].State == UpgradeButton.UnlockState.Invisible,
        $"{retired} is retired from the tree");
    Check(PrestigeTalentLayout.Tiers[0].Talents.All(id => tree[id].State == UpgradeButton.UnlockState.Unlocked)
      && tree["LR1"].State == UpgradeButton.UnlockState.Revealed,
      "The first tier is open and the second is locked");
    Check(PrestigeTalentLayout.PlaystyleTalents.SequenceEqual(["LOP1"])
      && DebugProgressionPresets.Names.Select((_, stage) => DebugProgressionPresets.Create(stage, UpgradeManager.CurrentUpgrades))
        .All(save => !save.Meta.ContainsKey("LOP1") && !freeRewards.Except(systemUnlocks).Any(save.Meta.ContainsKey)),
      "Presets never dock the fleet with Lone Operator, and grant no free rewards but the system unlocks");
    PrestigeTalentLayout.ApplyPrototypeLayout(tree);
    Check(Enumerable.Range(0, PrestigeTalentLayout.TalentColumns).All(column => PrestigeTalentLayout.Tiers
        .Where(tier => tier.Talents.Length > column).Select(tier => tree[tier.Talents[column]].Data.PosX).Distinct().Count() == 1),
      "Talents line up in columns across the tiers");

    GameSave Points(int fullTiers, int extra = 0)
    {
      var meta = new Dictionary<string, int>();
      foreach (var id in PrestigeTalentLayout.Tiers.Take(fullTiers).SelectMany(t => t.Talents)) meta[id] = 1;
      foreach (var id in PrestigeTalentLayout.Tiers[fullTiers].Talents.Take(extra)) meta[id] = 1;
      return new GameSave { Meta = meta };
    }
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["TR1"] = 1, ["MHF1"] = 1, ["PRL1"] = 1 } });
    Check(manager.UGM.PrismaticLens && manager.UGM.ThermiteRounds && manager.UGM.QuantumTouch
      && tree["LR1"].State == UpgradeButton.UnlockState.Unlocked,
      "Any three first-tier points activate their effects and unlock the second tier");
    manager.RestoreProgress(Points(0, 4));
    Check(tree["BR1"].State == UpgradeButton.UnlockState.Revealed, "Four points do not open Tier 3");
    manager.RestoreProgress(Points(1));
    Check(tree["BR1"].State == UpgradeButton.UnlockState.Unlocked, "Five earlier points unlock Tier 3");
    manager.RestoreProgress(Points(1, 4));
    Check(tree["MBR1"].State == UpgradeButton.UnlockState.Revealed, "Nine points do not open Tier 4");
    manager.RestoreProgress(Points(1, 5));
    Check(tree["MBR1"].State == UpgradeButton.UnlockState.Unlocked, "Ten earlier points unlock Tier 4");
    manager.RestoreProgress(Points(2, 5));
    Check(tree["SR1"].State == UpgradeButton.UnlockState.Revealed, "Fifteen points do not open Tier 5");
    manager.RestoreProgress(Points(2, 6));
    Check(tree["SR1"].State == UpgradeButton.UnlockState.Unlocked, "Sixteen earlier points unlock Tier 5");
    manager.RestoreProgress(Points(3, 3));
    Check(tree["OVC1"].State == UpgradeButton.UnlockState.Revealed, "Nineteen points do not open Tier 6");
    manager.RestoreProgress(Points(3, 4));
    Check(tree["OVC1"].State == UpgradeButton.UnlockState.Unlocked, "Twenty earlier points unlock Tier 6");

    CheckFreeRewards(Points);
    CheckTalentChoices();

    // Return to three actually allocated points for the refund check below (none of them a
    // combo talent the next checks expect unbought).
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["TR1"] = 1, ["MHF1"] = 1, ["AE1"] = 1 } });

    CheckWeaponTalents(manager);
    CheckComboTalents(manager);

    manager.UGM.CommandCenterUnlocked = true;
    manager.UGM.CommandNexus = true;
    var commands = new ManualFleetAbilities();
    commands.UpdateUnlocks(0);
    Check(commands.UnlockedCount == ManualFleetAbilities.Definitions.Length
      && commands.TryActivate(0, _ => { })
      && Math.Abs(commands.RemainingCooldown(0) - 31.5f) < 0.001f
      && commands.TryActivate(1, _ => { })
      && Math.Abs(commands.RemainingCooldown(0) - 31.5f * 0.75f) < 0.001f,
      "Command Nexus unlocks all commands, shortens recharge and chains cooldowns, without a global cooldown");
    manager.UGM.CommandNexus = false;
    manager.UGM.CommandCenterUnlocked = true;

    Check(manager.RespecPrestigeTalents() == 3 && manager.CurrentPrestigePoints == 3,
      "Refund all returns the exact allocated prestige cost");
    Check(tree.Values.All(button => button.CurrentLevel == 0 || PrestigeTalentLayout.IsFreeReward(button.Data.ShortName))
      && !manager.UGM.CommandCenterUnlocked && !manager.UGM.QuantumTouch && !manager.UGM.ThermiteRounds,
      "Refund all clears talent levels and their effects");

    Console.WriteLine("Prestige talent checks passed: 31 talents in 6 tiers, free tier rewards, all tier gates, either/or choices, combo rules, unlearning, refund and reset.");
  }

  // Either/or talents and unlearning one talent at a time.
  private static void CheckTalentChoices()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var tree = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    Check(PrestigeTalentLayout.ExclusiveWith("IOM1").SequenceEqual(["MD1", "CHR1"])
      && PrestigeTalentLayout.ExclusiveWith("MD1").SequenceEqual(["IOM1"])
      && PrestigeTalentLayout.ExclusiveWith("CHR1").SequenceEqual(["IOM1"])
      && !PrestigeTalentLayout.ExclusiveWith("TR1").Any(),
      "Ionized Magma rules out Magma Detonation and Chain Reaction, which still go together");
    Check(PrestigeTalentLayout.ExclusiveWith("OVC1").SequenceEqual(["HVO1"])
      && PrestigeTalentLayout.ExclusiveWith("HVO1").SequenceEqual(["OVC1"]),
      "Overclock and Heavy Ordnance rule each other out");
    // The test harness loads the trees without the game's layout pass.
    PrestigeTalentLayout.ApplyPrototypeLayout(tree);
    Check(PrestigeTalentLayout.EitherOrPairs.SequenceEqual([("MD1", "IOM1"), ("OVC1", "HVO1")])
      && PrestigeTalentLayout.EitherOrPairs.All(pair =>
        tree[pair.Right].Data.PosX - tree[pair.Left].Data.PosX == PrestigeTalentLayout.TalentSpacing
        && tree[pair.Left].Data.PosY == tree[pair.Right].Data.PosY),
      "Magma Detonation and Ionized Magma, and Overclock and Heavy Ordnance, sit side by side as either/or pairs");
    Check(PrestigeTalentLayout.CompatibleTalents.Contains("MD1") && PrestigeTalentLayout.CompatibleTalents.Contains("CHR1")
      && !PrestigeTalentLayout.CompatibleTalents.Contains("IOM1")
      && PrestigeTalentLayout.CompatibleTalents.Contains("OVC1") && !PrestigeTalentLayout.CompatibleTalents.Contains("HVO1")
      && DebugProgressionPresets.Names.Select((_, stage) => DebugProgressionPresets.Create(stage, UpgradeManager.CurrentUpgrades))
        .All(save => PrestigeTalentLayout.ExclusiveGroups.All(group => group.Count(save.Meta.ContainsKey) <= 1)),
      "Presets take one side of every either/or choice");

    // Three first-tier and two second-tier points open the third tier.
    var opened = new Dictionary<string, int> { ["TR1"] = 1, ["MHF1"] = 1, ["PRL1"] = 1, ["LR1"] = 1, ["ODP1"] = 1 };
    var manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new(opened) { ["MD1"] = 1 } });
    Check(!PrestigeTalentLayout.CanLearn(tree, "IOM1") && tree["IOM1"].State == UpgradeButton.UnlockState.Revealed
      && PrestigeTalentLayout.ExcludedBy(tree, "IOM1") == "MD1" && PrestigeTalentLayout.CanLearn(tree, "EP1"),
      "Owning Magma Detonation locks Ionized Magma, and nothing else in its tier");
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new(opened) { ["IOM1"] = 1 } });
    Check(!PrestigeTalentLayout.CanLearn(tree, "MD1") && tree["MD1"].State == UpgradeButton.UnlockState.Revealed
      && PrestigeTalentLayout.ExcludedBy(tree, "CHR1") == "IOM1" && manager.UGM.IonizedMagma,
      "Owning Ionized Magma locks Magma Detonation and Chain Reaction");

    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new(opened) { ["MD1"] = 1 } });
    ulong points = manager.CurrentPrestigePoints;
    Check(!PrestigeTalentLayout.CanUnlearn(tree, "TR1") && !manager.UnlearnPrestigeTalent(tree["TR1"])
      && tree["TR1"].CurrentLevel == 1 && manager.CurrentPrestigePoints == points,
      "A talent whose point a later tier needs cannot be unlearned");
    Check(manager.UnlearnPrestigeTalent(tree["MD1"]) && tree["MD1"].CurrentLevel == 0 && !manager.UGM.MagmaDetonation
      && manager.CurrentPrestigePoints == points + 1 && PrestigeTalentLayout.CanLearn(tree, "IOM1")
      && tree["IOM1"].State == UpgradeButton.UnlockState.Unlocked && tree["MHF1"].CurrentLevel == 1 && manager.UGM.QuantumTouch,
      "Unlearning one talent refunds its point, ends its effect, frees its rivals and keeps the rest");
    Check(manager.UnlearnPrestigeTalent(tree["ODP1"]) && !manager.UGM.OverdriveProtocol
      && tree["IOM1"].State == UpgradeButton.UnlockState.Revealed
      && !manager.UnlearnPrestigeTalent(tree["TR1"]) && manager.UGM.ThermiteRounds,
      "With the third tier empty a second-tier point can go, closing that tier; the second tier still needs the first");

    var meta = manager.UGM;
    Check(PrestigeTalentEffects.MoltenArcReach(false) < 0f && PrestigeTalentEffects.MoltenArcReach(true) == MathF.PI
      && PrestigeTalentEffects.MoltenArcSustain == 0f,
      "Without Ionized Magma only Tesla Coil arcs to molten spots, and arcs do not sustain them");
    meta.IonizedMagma = true;
    Check(PrestigeTalentEffects.MoltenArcReach(false) == PrestigeTalentEffects.IonizedReach
      && PrestigeTalentEffects.MoltenArcReach(true) == MathF.PI
      && PrestigeTalentEffects.MoltenArcSustain == PrestigeTalentEffects.IonizedSustainSeconds,
      "Ionized Magma arcs to nearby molten spots on its own, and every arc keeps a spot burning");
    meta.IonizedMagma = false;
  }

  private static UpgradeManager Extracted(ulong extractions, GameSave save)
  {
    var manager = new UpgradeManager();
    typeof(UpgradeManager).GetField("m_gameState", System.Reflection.BindingFlags.Instance
      | System.Reflection.BindingFlags.NonPublic)!.SetValue(manager, new GameState { CoreExtractions = extractions });
    manager.RestoreProgress(save);
    return manager;
  }

  private static void CheckFreeRewards(Func<int, int, GameSave> points)
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var tree = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    var manager = Extracted(0, points(4, 0));
    Check(!manager.UGM.FreeExpandSpace && !manager.UGM.CommandCenterUnlocked && tree["XSP1"].CurrentLevel == 0,
      "Free rewards wait for the first core extraction");
    manager = Extracted(1, points(0, 3));
    Check(manager.UGM.FreeExpandSpace && manager.UGM.CommandCenterUnlocked && manager.UGM.ShipSystemsUnlocked
      && !manager.UGM.ShipyardUnlocked && tree[ShipSystems.UnlockTalent].State == UpgradeButton.UnlockState.MaxedOut
      && tree["SYU1"].State == UpgradeButton.UnlockState.Revealed,
      "Reaching a tier claims its free rewards, and only those of the tiers reached");
    Check(PrestigeTalentLayout.SpentPoints(tree) == 3, "Free rewards never count as spent points");
    manager = Extracted(1, points(3, 1));
    Check(manager.UGM.ShipyardUnlocked && manager.UGM.SignalsUnlocked && !manager.UGM.FreeCoreMemory
      && manager.ExpandSpaceLevel == 5, "Five tiers reach Expand Space V and every system unlock");
    manager = Extracted(1, points(5, 0));
    Check(manager.UGM.FreeCoreMemory && manager.ExpandSpaceLevel == 5,
      "The sixth tier grants Core Memory instead of a sixth Expand Space");
    Check(manager.RespecPrestigeTalents() > 0 && !manager.UGM.ShipyardUnlocked && manager.UGM.CommandCenterUnlocked
      && manager.UGM.FreeExpandSpace, "Refunding talents gives back only the rewards of the first tier");
    manager = Extracted(0, new GameSave { Meta = new() { ["SYU1"] = 1 } });
    Check(manager.UGM.ShipyardUnlocked && !manager.UGM.SignalsUnlocked,
      "A system unlock granted directly (debug tools, presets) stays claimed");
  }

  private static void CheckWeaponTalents(UpgradeManager manager)
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var meta = manager.UGM;
    bool thermite = meta.ThermiteRounds;
    meta.ThermiteRounds = false;
    Check(PrestigeTalentEffects.LightningRodPulses(true, 0) == 0 && PrestigeTalentEffects.HarpoonReloadMultiplier == 1f
      && PrestigeTalentEffects.EchoCastCount(0) == 1 && PrestigeTalentEffects.ShardReactorBonus(5) == 0f
      && PrestigeTalentEffects.SignalResonanceBonus(500) == 0f && PrestigeTalentEffects.MagmaScarSeconds(4f) == 4f,
      "Unbought weapon talents do nothing");

    meta.LightningRod = true;
    Check(PrestigeTalentEffects.LightningRodPulses(false, 0) == 1 && PrestigeTalentEffects.LightningRodPulses(true, 1) == 4
      && PrestigeTalentEffects.LightningRodPulses(true, 9) == PrestigeTalentEffects.LightningRodMaxExtraPulses
      && PrestigeTalentEffects.HarpoonReloadMultiplier == 2f,
      "Lightning Rod adds a pulse per hit, three per critical, capped, and halves the reload");

    meta.ThermiteRounds = true;
    Check(PrestigeTalentEffects.MagmaScarSeconds(4f) == 8f, "Thermite Rounds doubles how long Magma Scars burn");
    Check(PrestigeTalentEffects.DetonationGems(10f, 0f, 4f) == 20 && PrestigeTalentEffects.DetonationGems(10f, 3f, 4f) == 5
      && PrestigeTalentEffects.DetonationGems(10f, 9f, 4f) == 0,
      "Magma Detonation deals twice what a scar has left to ooze");

    var ug = new UpgradesGeneratorUpgrades();
    Check(PrestigeTalentEffects.BeamRidersPerBeam(ug) == 1 && PrestigeTalentEffects.BeamRiderInterval(1f) == 3f
      && PrestigeTalentEffects.BeamRiderInterval(3f) == 1f, "Beam Riders fire every three seconds, faster with laser fire rate");
    ug.RocketSwarm = true;
    Check(PrestigeTalentEffects.BeamRidersPerBeam(ug) == MainShipWeapons.RocketSwarmMultiplier,
      "Rocket Swarm doubles the rockets every beam fires");

    meta.SystemEcho = true;
    Check(PrestigeTalentEffects.EchoCastCount(0) == 2 && PrestigeTalentEffects.EchoCastCount(24.9) == 2
      && PrestigeTalentEffects.EchoCastCount(25) == 1, "Echo Protocol echoes a quarter of system activations");

    meta.ShardReactor = true;
    meta.SignalResonance = true;
    Check(Math.Abs(PrestigeTalentEffects.ShardReactorBonus(3) - 0.6f) < 0.001f,
      "Shard Reactor adds a fifth per held Core Shard");
    Check(Math.Abs(PrestigeTalentEffects.SignalResonanceBonus(40) - 0.4f) < 0.001f
      && PrestigeTalentEffects.SignalResonanceBonus(5000) == PrestigeTalentEffects.SignalResonanceCap
      && PrestigeTalentEffects.SignalResonanceLayers(49) == 1
      && PrestigeTalentEffects.SignalResonanceLayers(5000) == PrestigeTalentEffects.SignalResonanceMaxLayers,
      "Signal Resonance scales with signals, capped, and mines deeper every 25");

    Check(!PrestigeTalentEffects.KamikazeDrones && ShipSystems.VisibleTabs[0] == ShipSystems.DroneSwarmTab
      && !ShipSystems.VisibleTabs.Contains(ShipSystems.KamikazeWingTab),
      "Drone Swarm is a ship system until Kamikaze Drones is bought");
    meta.KamikazeDrones = true;
    Check(ShipSystems.VisibleTabs[0] == ShipSystems.KamikazeWingTab && ShipSystems.VisibleTabs.Length == 4
      && !ShipSystems.IsTabAvailable(ShipSystems.DroneSwarmTab) && ShipSystems.Resolve(ShipSystems.DroneSwarmTab) == ShipSystems.KamikazeWingTab,
      "Kamikaze Drones swaps Drone Swarm for the Kamikaze Wing in the same slot");
    meta.KamikazeDrones = false;

    meta.LightningRod = meta.SystemEcho = meta.ShardReactor = meta.SignalResonance = false;
    meta.ThermiteRounds = thermite;
  }

  // The rules behind the combo talents (their effects play out in the game screen).
  private static void CheckComboTalents(UpgradeManager manager)
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    var meta = manager.UGM;
    long load = 0;
    Check(PrestigeTalentEffects.AutomaticWeaponFireRate(1.5f) == 1.5f && PrestigeTalentEffects.ArsenalHitYield(10) == 10
      && PrestigeTalentEffects.ArsenalLaserShare == 1f && PrestigeTalentEffects.HeavyOrdnanceBonusLayers == 0
      && PrestigeTalentEffects.FleetCount(7) == 7 && PrestigeTalentEffects.HandValueMultiplier == 1f
      && PrestigeTalentEffects.LaserBeamSplit == 1 && PrestigeTalentEffects.ShellDamageMultiplier == 1f
      && PrestigeTalentEffects.FractureThresholdMultiplier == 1 && PrestigeTalentEffects.ResonanceGems(100_000) == 0
      && PrestigeTalentEffects.CargoCatapultSlugs(ref load, 1000) == 0 && load == 0
      && !PrestigeTalentEffects.LightningCrits && PrestigeTalentEffects.KineticBatteryCharge(0.5f) == 0.5f,
      "Unbought combo talents do nothing");
    PrestigeTalentEffects.RecoilHarvestActive = true;
    Check(PrestigeTalentEffects.HandValueMultiplier == 1f, "Recoil Harvest needs its talent");
    meta.RecoilHarvest = true;
    Check(PrestigeTalentEffects.HandValueMultiplier == PrestigeTalentEffects.RecoilHarvestValue,
      "Recoil Harvest makes hand collection worth triple while its window is open");
    PrestigeTalentEffects.RecoilHarvestActive = false;
    Check(PrestigeTalentEffects.HandValueMultiplier == 1f, "Recoil Harvest ends with its window");
    meta.RecoilHarvest = false;
    meta.KineticBattery = true;
    Check(Math.Abs(PrestigeTalentEffects.KineticBatteryCharge(0f) - 0.01f) < 1e-6f
      && PrestigeTalentEffects.KineticBatteryCharge(1.995f) == PrestigeTalentEffects.KineticBatteryMax,
      "Kinetic Battery stores 1% per gem collected by hand, up to 200%");
    meta.KineticBattery = false;
    Check(PrestigeTalentEffects.MoltenSpotCap(48) == 48 && PrestigeTalentEffects.MoltenBurn == 1f,
      "Molten spots keep their cap and burn time without Magma Reservoir");
    meta.MagmaReservoir = true;
    Check(PrestigeTalentEffects.MoltenSpotCap(48) == 96 && PrestigeTalentEffects.MoltenSpotCap(32) == 64
      && PrestigeTalentEffects.MoltenBurn == 1.5f,
      "Magma Reservoir makes molten spots burn 50% longer, and doubles how many can burn at once");
    meta.MagmaReservoir = false;

    meta.Overclock = true;
    Check(PrestigeTalentEffects.AutomaticWeaponFireRate(1.5f) == 3f && PrestigeTalentEffects.ArsenalHitYield(10) == 5
      && PrestigeTalentEffects.ArsenalHitYield(3) == 2 && PrestigeTalentEffects.ArsenalHitYield(1) == 1
      && PrestigeTalentEffects.ArsenalLaserShare == 0.5f,
      "Overclock doubles every weapon's fire rate and halves each hit, never below one");
    PrestigeTalentEffects.ArsenalSurge = PrestigeTalentEffects.SurgeFireRate;
    Check(PrestigeTalentEffects.AutomaticWeaponFireRate(1f) == 4f,
      "Overdrive Protocol and Shard Reactor's surge stack with Overclock");
    PrestigeTalentEffects.ArsenalSurge = 1f;
    meta.Overclock = false;

    meta.HeavyOrdnance = true;
    Check(PrestigeTalentEffects.AutomaticWeaponFireRate(1.5f) == 0.75f && PrestigeTalentEffects.ArsenalHitYield(10) == 20
      && PrestigeTalentEffects.ArsenalHitYield(1) == 2 && PrestigeTalentEffects.ArsenalHitYield(int.MaxValue) == int.MaxValue
      && PrestigeTalentEffects.ArsenalLaserShare == 2f
      && PrestigeTalentEffects.HeavyOrdnanceBonusLayers == PrestigeTalentEffects.HeavyOrdnanceLayers,
      "Heavy Ordnance halves every weapon's fire rate, doubles each hit and reaches a layer deeper");
    meta.HeavyOrdnance = false;

    meta.LoneOperator = true;
    Check(PrestigeTalentEffects.FleetCount(7) == 0 && PrestigeTalentEffects.HandValueMultiplier == 5f
      && PrestigeTalentEffects.ManualShotMultiplier == 5,
      "Lone Operator docks the fleet and makes hand collection and clicked shots five times stronger");
    meta.LoneOperator = false;

    meta.PrismaticLens = meta.ShrapnelShell = meta.CoreBreaker = meta.ResonantCore = meta.CargoCatapult = true;
    Check(PrestigeTalentEffects.LaserBeamSplit == 2, "Prismatic Lens splits every beam in two");
    Check(PrestigeTalentEffects.ShellDamageMultiplier == 2f, "Shrapnel Shell doubles damage to the shell");
    Check(PrestigeTalentEffects.FractureThresholdMultiplier == 0.5, "Core Breaker halves fracture thresholds");
    Check(PrestigeTalentEffects.ResonanceGems(100_000) == 5_000
      && PrestigeTalentEffects.ResonanceGems(1e12) == PrestigeTalentEffects.ResonanceMaxGems
      && PrestigeTalentEffects.ResonanceGems(0) == 0,
      "Resonant Core echoes 5% of the last minute's damage, capped");
    Check(PrestigeTalentEffects.CargoCatapultSlugs(ref load, 600) == 2 && load == 100
      && PrestigeTalentEffects.CargoCatapultSlugs(ref load, 150) == 1 && load == 0,
      "Cargo Catapult loads a slug for every 250 gems delivered, carrying the rest over");
    meta.PrismaticLens = meta.ShrapnelShell = meta.CoreBreaker = meta.ResonantCore = meta.CargoCatapult = false;

    meta.GalvanicShock = true;
    Check(PrestigeTalentEffects.LightningCrits && PrestigeTalentEffects.LightningCritChance == 0.05f
      && PrestigeTalentEffects.ShockMultiplier(0) == 1f
      && Math.Abs(PrestigeTalentEffects.ShockMultiplier(1) - 1.01f) < 1e-5f
      && Math.Abs(PrestigeTalentEffects.ShockMultiplier(PrestigeTalentEffects.MaxShockStacks) - 3f) < 1e-5f
      && Math.Abs(PrestigeTalentEffects.ShockMultiplier(5000) - 3f) < 1e-5f,
      "Galvanic Shock gives lightning a 5% crit, and each stack of shock adds 1% damage taken, up to 200%");
    meta.GalvanicShock = false;
  }
}
