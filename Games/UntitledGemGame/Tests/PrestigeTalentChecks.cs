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
      "CC1", "SSU1", "SYU1", "DCM1", "OH1", "TR1",
      "SGU1", "CAT1", "GM1", "MHF1", "LR1",
      "BR1", "MD1", "EP1", "AE1", "MGD1", "MCSN1",
      "PCO1", "MBR1", "DG1", "CN1", "HICM1",
      "SR1", "SGR1", "PO1", "MGF1", "WCM1",
    ];
    Check(activeTalents.SequenceEqual(PrestigeTalentLayout.Tiers.SelectMany(tier => tier.Talents)),
      "The tiers hold exactly the designed talents, in order");
    Check(activeTalents.Distinct().Count() == 27
      && activeTalents.All(id => tree[id].Data.NumLevels == 1
        && tree[id].Data.LevelInfo.Count == 1 && tree[id].Data.LevelInfo[0].Cost == 1),
      "The complete tree contains twenty-seven unique one-point talents");
    Check(activeTalents.All(id => File.Exists(Path.Combine("Content", tree[id].Data.UpgradeDefinition.Icon))),
      "Every talent has an icon");
    foreach (string retired in new[] { "TPM1", "JHM1", "FLR1", "CA1", "RCM1", "QEM1", "MA1", "MM1", "CCN1", "MGS1" })
      Check(!PrestigeTalentLayout.IsInTree(retired) && tree[retired].State == UpgradeButton.UnlockState.Invisible,
        $"{retired} is retired from the tree");
    Check(PrestigeTalentLayout.Tiers[0].Talents.All(id => tree[id].State == UpgradeButton.UnlockState.Unlocked)
      && tree["GM1"].State == UpgradeButton.UnlockState.Revealed,
      "The first tier is open and the second is locked");

    GameSave Points(int fullTiers, int extra = 0)
    {
      var meta = new Dictionary<string, int>();
      foreach (var id in PrestigeTalentLayout.Tiers.Take(fullTiers).SelectMany(t => t.Talents)) meta[id] = 1;
      foreach (var id in PrestigeTalentLayout.Tiers[fullTiers].Talents.Take(extra)) meta[id] = 1;
      return new GameSave { Meta = meta };
    }
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["SYU1"] = 1, ["OH1"] = 1 } });
    Check(manager.UGM.CommandCenterUnlocked && manager.UGM.ShipyardUnlocked && manager.UGM.OverloadedHolds
      && tree["GM1"].State == UpgradeButton.UnlockState.Unlocked,
      "Any three first-tier points activate their effects and unlock the second tier");
    manager.RestoreProgress(Points(0, 4));
    Check(tree["BR1"].State == UpgradeButton.UnlockState.Revealed, "Four points do not open Tier 3");
    manager.RestoreProgress(Points(0, 5));
    Check(tree["BR1"].State == UpgradeButton.UnlockState.Unlocked, "Five earlier points unlock Tier 3");
    manager.RestoreProgress(Points(1, 3));
    Check(tree["PCO1"].State == UpgradeButton.UnlockState.Revealed, "Nine points do not open Tier 4");
    manager.RestoreProgress(Points(1, 4));
    Check(tree["PCO1"].State == UpgradeButton.UnlockState.Unlocked && manager.UGM.SignalsUnlocked,
      "Ten earlier points unlock Tier 4");
    manager.RestoreProgress(Points(2, 4));
    Check(tree["SR1"].State == UpgradeButton.UnlockState.Revealed, "Fifteen points do not open Tier 5");
    manager.RestoreProgress(Points(3));
    Check(tree["SR1"].State == UpgradeButton.UnlockState.Unlocked, "Sixteen earlier points unlock Tier 5");

    // Return to three actually allocated points for the refund check below.
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["OH1"] = 1, ["TR1"] = 1 } });

    var ship = new UntitledGemGame.Entities.Harvester
      { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Harvester };
    int overloadedCapacity = BaseStats.GetHarvesterCapacity(ship);
    manager.UGM.OverloadedHolds = false;
    int normalCapacity = BaseStats.GetHarvesterCapacity(ship);
    manager.UGM.OverloadedHolds = true;
    Check(overloadedCapacity == normalCapacity * 2, "Overloaded Holds doubles fleet cargo capacity");
    Check(PrestigeTalentEffects.FuelCostMultiplier(ship) == 1f, "Overloaded Holds does not penalize an empty ship");
    ship.CarryingGemCount = 1;
    Check(PrestigeTalentEffects.FuelCostMultiplier(ship) == 2f,
      "Overloaded Holds doubles fuel consumption once cargo is aboard");

    manager.UGM.DeepCoreMunitions = true;
    Check(PrestigeTalentEffects.DeepCoreQualityPower(1) == 3
      && PrestigeTalentEffects.DeepCoreQualityPower(3) == 6
      && PrestigeTalentEffects.DeepCoreQualityPower(35) == 43
      && Math.Abs(PrestigeTalentEffects.PlanetDebrisReachScale(1f) - 1.4f) < 0.001f,
      "Deep-Core Munitions advances one quality layer and expands debris reach");
    manager.UGM.DeepCoreMunitions = false;

    manager.UGM.CargoCatapult = true;
    manager.UGM.HarvestersInstantCollection = true;
    manager.UGM.WeaponizedCompression = true;
    Check(Math.Abs(PrestigeTalentEffects.CargoCatapultCharge(10) - 0.8f) < 0.001f
      && PrestigeTalentEffects.CargoCatapultCharge(1000) == 4f,
      "Cargo Catapult scales with cargo and caps its timer advance");
    Check(PrestigeTalentEffects.PhaseLogisticsValue(100) == 75
      && PrestigeTalentEffects.CompressedGemValue(100) == 200
      && PrestigeTalentEffects.ConstellationPayload(100) == 150,
      "Endgame conversions apply their advertised value and payload tradeoffs");
    manager.UGM.CargoCatapult = manager.UGM.HarvestersInstantCollection = manager.UGM.WeaponizedCompression = false;

    CheckWeaponTalents(manager);

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
    Check(tree.Values.All(button => button.CurrentLevel == 0)
      && !manager.UGM.CommandCenterUnlocked && !manager.UGM.OverloadedHolds && !manager.UGM.ThermiteRounds,
      "Refund all clears talent levels and their effects");

    Console.WriteLine("Prestige talent checks passed: 27 talents, all tier gates, weapon interactions, refund and reset.");
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
      "Magma Detonation releases twice what a scar has left to ooze");

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

    meta.LightningRod = meta.SystemEcho = meta.ShardReactor = meta.SignalResonance = false;
    meta.ThermiteRounds = thermite;
  }
}
