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
      "CC1", "TR1", "MHF1", "PRL1", "SHS1", "CAT1",
      "SSU1", "LR1", "ODP1", "SYF1", "KNH1", "MCSN1",
      "SYU1", "BR1", "MD1", "EP1", "AE1", "VPL1",
      "SGU1", "MBR1", "DG1", "KD1", "FSD1", "DSP1",
      "CN1", "HICM1", "CHR1", "MLC1", "SR1", "PO1",
      "SGR1", "OVC1", "LOP1", "RCO1", "CBK1",
    ];
    Check(activeTalents.SequenceEqual(PrestigeTalentLayout.Tiers.SelectMany(tier => tier.Talents)),
      "The tiers hold exactly the designed talents, in order");
    Check(PrestigeTalentLayout.Tiers.Length == 6
      && PrestigeTalentLayout.Tiers.Select(t => t.RequiredEarlierPoints).SequenceEqual([0, 3, 5, 10, 16, 24]),
      "Six tiers open at 0, 3, 5, 10, 16 and 24 earlier points");
    Check(activeTalents.Distinct().Count() == 35
      && activeTalents.All(id => tree[id].Data.NumLevels == 1
        && tree[id].Data.LevelInfo.Count == 1 && tree[id].Data.LevelInfo[0].Cost == 1),
      "The complete tree contains thirty-five unique one-point talents");
    // The system unlocks are spread over the first four tiers.
    Check(PrestigeTalentLayout.TierIndex("CC1") == 0 && PrestigeTalentLayout.TierIndex(ShipSystems.UnlockTalent) == 1
      && PrestigeTalentLayout.TierIndex("SYU1") == 2 && PrestigeTalentLayout.TierIndex("SGU1") == 3,
      "Command Center, Auxiliary Power, Shipyard and Deep Space Signals open one per tier");

    string[] freeRewards = PrestigeTalentLayout.Tiers.SelectMany(tier => tier.FreeRewards).ToArray();
    Check(freeRewards.SequenceEqual(["XSP1", "XSP2", "SPC1", "XSP3", "HST1", "XSP4", "XSP5", "SCH1", "CMY1"])
      && PrestigeTalentLayout.Tiers.All(tier => tier.FreeRewards.Length > 0)
      && freeRewards.All(id => PrestigeTalentLayout.IsFreeReward(id) && !PrestigeTalentLayout.IsInTree(id)
        && PrestigeTalentLayout.IsShown(id) && tree[id].Data.LevelInfo[0].Cost == 0),
      "Every tier shows free rewards that cost nothing and cannot be bought");
    Check(activeTalents.Concat(freeRewards).All(id => File.Exists(Path.Combine("Content", tree[id].Data.UpgradeDefinition.Icon))),
      "Every talent and free reward has an icon");
    foreach (string retired in new[] { "TPM1", "JHM1", "FLR1", "CA1", "RCM1", "QEM1", "MA1", "MM1", "CCN1", "MGS1",
      "DCM1", "OH1", "GM1", "MGD1", "PCO1", "MGF1", "WCM1" })
      Check(!PrestigeTalentLayout.IsShown(retired) && tree[retired].State == UpgradeButton.UnlockState.Invisible,
        $"{retired} is retired from the tree");
    Check(PrestigeTalentLayout.Tiers[0].Talents.All(id => tree[id].State == UpgradeButton.UnlockState.Unlocked)
      && tree["LR1"].State == UpgradeButton.UnlockState.Revealed,
      "The first tier is open and the second is locked");
    Check(PrestigeTalentLayout.PlaystyleTalents.SequenceEqual(["LOP1"])
      && DebugProgressionPresets.Names.Select((_, stage) => DebugProgressionPresets.Create(stage, UpgradeManager.CurrentUpgrades))
        .All(save => !save.Meta.ContainsKey("LOP1") && !freeRewards.Any(save.Meta.ContainsKey)),
      "Presets never dock the fleet with Lone Operator or buy free rewards");

    GameSave Points(int fullTiers, int extra = 0)
    {
      var meta = new Dictionary<string, int>();
      foreach (var id in PrestigeTalentLayout.Tiers.Take(fullTiers).SelectMany(t => t.Talents)) meta[id] = 1;
      foreach (var id in PrestigeTalentLayout.Tiers[fullTiers].Talents.Take(extra)) meta[id] = 1;
      return new GameSave { Meta = meta };
    }
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["TR1"] = 1, ["MHF1"] = 1 } });
    Check(manager.UGM.CommandCenterUnlocked && manager.UGM.ThermiteRounds && manager.UGM.QuantumTouch
      && tree["LR1"].State == UpgradeButton.UnlockState.Unlocked,
      "Any three first-tier points activate their effects and unlock the second tier");
    manager.RestoreProgress(Points(0, 4));
    Check(tree["BR1"].State == UpgradeButton.UnlockState.Revealed, "Four points do not open Tier 3");
    manager.RestoreProgress(Points(0, 5));
    Check(tree["BR1"].State == UpgradeButton.UnlockState.Unlocked, "Five earlier points unlock Tier 3");
    manager.RestoreProgress(Points(1, 3));
    Check(tree["MBR1"].State == UpgradeButton.UnlockState.Revealed, "Nine points do not open Tier 4");
    manager.RestoreProgress(Points(1, 4));
    Check(tree["MBR1"].State == UpgradeButton.UnlockState.Unlocked && manager.UGM.ShipSystemsUnlocked,
      "Ten earlier points unlock Tier 4");
    manager.RestoreProgress(Points(2, 3));
    Check(tree["SR1"].State == UpgradeButton.UnlockState.Revealed, "Fifteen points do not open Tier 5");
    manager.RestoreProgress(Points(2, 4));
    Check(tree["SR1"].State == UpgradeButton.UnlockState.Unlocked, "Sixteen earlier points unlock Tier 5");
    manager.RestoreProgress(Points(3, 5));
    Check(tree["OVC1"].State == UpgradeButton.UnlockState.Revealed, "Twenty-three points do not open Tier 6");
    manager.RestoreProgress(Points(4));
    Check(tree["OVC1"].State == UpgradeButton.UnlockState.Unlocked, "Twenty-four earlier points unlock Tier 6");

    CheckFreeRewards(Points);

    // Return to three actually allocated points for the refund check below.
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["TR1"] = 1, ["MHF1"] = 1 } });

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

    Console.WriteLine("Prestige talent checks passed: 35 talents in 6 tiers, free tier rewards, all tier gates, combo rules, refund and reset.");
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
    Check(!manager.UGM.FreeExpandSpace && !manager.UGM.FreeSpareCells && tree["XSP1"].CurrentLevel == 0,
      "Free rewards wait for the first core extraction");
    manager = Extracted(1, points(0, 3));
    Check(manager.UGM.FreeExpandSpace && manager.UGM.FreeSpareCells && !manager.UGM.FreeHeadStart
      && tree["SPC1"].State == UpgradeButton.UnlockState.MaxedOut && tree["HST1"].State == UpgradeButton.UnlockState.Revealed,
      "Reaching a tier claims its free rewards, and only those of the tiers reached");
    Check(PrestigeTalentLayout.SpentPoints(tree) == 3, "Free rewards never count as spent points");
    manager = Extracted(1, points(3, 4));
    Check(manager.UGM.FreeHeadStart && manager.UGM.FreeShardCache && !manager.UGM.FreeCoreMemory
      && manager.ExpandSpaceLevel == 5, "Five tiers reach Expand Space V, Head Start and Shard Cache");
    manager = Extracted(1, points(5, 0));
    Check(manager.UGM.FreeCoreMemory && manager.ExpandSpaceLevel == 5,
      "The sixth tier grants Core Memory instead of a sixth Expand Space");
    Check(manager.RespecPrestigeTalents() > 0 && !manager.UGM.FreeShardCache && manager.UGM.FreeExpandSpace,
      "Refunding talents gives back only the rewards of the first tier");
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
    Check(PrestigeTalentEffects.AutomaticWeaponFireRate(1.5f) == 1.5f && PrestigeTalentEffects.OverclockYield(10) == 10
      && PrestigeTalentEffects.FleetCount(7) == 7 && PrestigeTalentEffects.HandValueMultiplier == 1f
      && PrestigeTalentEffects.LaserBeamSplit == 1 && PrestigeTalentEffects.ShellDamageMultiplier == 1f
      && PrestigeTalentEffects.FractureThresholdMultiplier == 1 && PrestigeTalentEffects.ResonanceGems(100_000) == 0
      && PrestigeTalentEffects.CargoCatapultSlugs(ref load, 1000) == 0 && load == 0,
      "Unbought combo talents do nothing");

    meta.Overclock = true;
    Check(PrestigeTalentEffects.AutomaticWeaponFireRate(1.5f) == 3f && PrestigeTalentEffects.OverclockYield(10) == 5
      && PrestigeTalentEffects.OverclockYield(3) == 2 && PrestigeTalentEffects.OverclockYield(1) == 1
      && PrestigeTalentEffects.OverclockLaserShare == 0.5f,
      "Overclock doubles every weapon's fire rate and halves each hit, never below one");
    PrestigeTalentEffects.ArsenalSurge = PrestigeTalentEffects.SurgeFireRate;
    Check(PrestigeTalentEffects.AutomaticWeaponFireRate(1f) == 4f,
      "Overdrive Protocol and Shard Reactor's surge stack with Overclock");
    PrestigeTalentEffects.ArsenalSurge = 1f;
    meta.Overclock = false;

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
  }
}
