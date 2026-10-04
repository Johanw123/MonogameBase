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
      "CC1", "OH1", "FLR1", "DCM1", "TPM1",
      "RH1", "AR1", "GM1", "MHF1", "CAT1",
      "MA1", "RCM1", "JHM1", "MGD1", "CA1",
      "QEM1", "MGS1", "MCSN1", "PCO1", "CCN1",
      "HICM1", "MGF1", "MM1", "CN1", "WCM1",
    ];
    Check(activeTalents.Length == 25 && activeTalents.Distinct().Count() == 25
      && activeTalents.All(id => tree[id].Data.NumLevels == 1
        && tree[id].Data.LevelInfo.Count == 1 && tree[id].Data.LevelInfo[0].Cost == 1),
      "The complete tree contains twenty-five unique one-point talents");
    string[] firstTier = ["CC1", "OH1", "FLR1", "DCM1", "TPM1"];
    Check(firstTier.All(id => tree[id].State == UpgradeButton.UnlockState.Unlocked)
      && tree["RH1"].State == UpgradeButton.UnlockState.Revealed
      && tree["AR1"].State == UpgradeButton.UnlockState.Revealed,
      "The five run-changing talents are active in the first prestige tier");

    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["OH1"] = 1, ["FLR1"] = 1 } });
    Check(manager.UGM.CommandCenterUnlocked && manager.UGM.OverloadedHolds && manager.UGM.FleetRequisition
      && tree["AR1"].State == UpgradeButton.UnlockState.Unlocked,
      "Any three first-tier points activate their effects and unlock the second tier");

    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new()
    {
      ["CC1"] = 1, ["OH1"] = 1, ["FLR1"] = 1, ["DCM1"] = 1, ["TPM1"] = 1,
      ["RH1"] = 1, ["AR1"] = 1,
    }});
    Check(tree["MA1"].State == UpgradeButton.UnlockState.Unlocked,
      "Seven earlier points unlock Tier 3");
    manager.RestoreProgress(new GameSave { Meta = new()
    {
      ["CC1"] = 1, ["OH1"] = 1, ["FLR1"] = 1, ["DCM1"] = 1, ["TPM1"] = 1,
      ["RH1"] = 1, ["AR1"] = 1, ["GM1"] = 1, ["MHF1"] = 1, ["CAT1"] = 1,
      ["MA1"] = 1, ["RCM1"] = 1,
    }});
    Check(tree["QEM1"].State == UpgradeButton.UnlockState.Unlocked,
      "Twelve earlier points unlock Tier 4");
    manager.RestoreProgress(new GameSave { Meta = new()
    {
      ["CC1"] = 1, ["OH1"] = 1, ["FLR1"] = 1, ["DCM1"] = 1, ["TPM1"] = 1,
      ["RH1"] = 1, ["AR1"] = 1, ["GM1"] = 1, ["MHF1"] = 1, ["CAT1"] = 1,
      ["MA1"] = 1, ["RCM1"] = 1, ["JHM1"] = 1, ["MGD1"] = 1, ["CA1"] = 1,
      ["QEM1"] = 1, ["MGS1"] = 1, ["MCSN1"] = 1,
    }});
    Check(tree["HICM1"].State == UpgradeButton.UnlockState.Unlocked,
      "Eighteen earlier points unlock Tier 5");

    // Return to the three actually allocated points used by the refund check below.
    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["OH1"] = 1, ["FLR1"] = 1 } });

    var ship = new UntitledGemGame.Entities.Harvester
      { Type = UntitledGemGame.Entities.Harvester.HarvesterType.Harvester };
    int overloadedCapacity = BaseStats.GetHarvesterCapacity(ship);
    manager.UGM.OverloadedHolds = false;
    int normalCapacity = BaseStats.GetHarvesterCapacity(ship);
    manager.UGM.OverloadedHolds = true;
    Check(overloadedCapacity == normalCapacity * 2,
      "Overloaded Holds doubles fleet cargo capacity");
    Check(PrestigeTalentEffects.FuelCostMultiplier(ship) == 1f,
      "Overloaded Holds does not penalize an empty ship");
    ship.CarryingGemCount = 1;
    Check(PrestigeTalentEffects.FuelCostMultiplier(ship) == 2f,
      "Overloaded Holds doubles fuel consumption once cargo is aboard");
    Check(PrestigeTalentEffects.FleetCount(4) == 4 && PrestigeTalentEffects.FleetCount(5) == 6
      && PrestigeTalentEffects.FleetCount(10) == 12,
      "Fleet Requisition adds one ship for every five purchased of a type");

    manager.UGM.DeepCoreMunitions = true;
    Check(PrestigeTalentEffects.DeepCoreQualityPower(1) == 3
      && PrestigeTalentEffects.DeepCoreQualityPower(3) == 6
      && PrestigeTalentEffects.DeepCoreQualityPower(35) == 43
      && Math.Abs(PrestigeTalentEffects.PlanetDebrisReachScale(1f) - 1.4f) < 0.001f,
      "Deep-Core Munitions advances one quality layer and expands debris reach");

    manager.UGM.TargetPainter = true;
    Check(Math.Abs(PrestigeTalentEffects.AutomaticWeaponFireRate(10f) - 6.5f) < 0.001f
      && PrestigeTalentEffects.PaintedYield(7, true) == 14
      && PrestigeTalentEffects.PaintedYield(7, false) == 7,
      "Target Painter trades permanent fire rate for doubled painted output");

    manager.UGM.CargoCatapult = true;
    manager.UGM.CombinedArms = true;
    manager.UGM.HarvestersInstantCollection = true;
    manager.UGM.WeaponizedCompression = true;
    Check(Math.Abs(PrestigeTalentEffects.CargoCatapultCharge(10) - 0.8f) < 0.001f
      && PrestigeTalentEffects.CargoCatapultCharge(1000) == 4f,
      "Cargo Catapult scales with cargo and caps its timer advance");
    Check(PrestigeTalentEffects.CombinedArmsYield(10, 1) == 10
      && PrestigeTalentEffects.CombinedArmsYield(10, 4) == 16,
      "Combined Arms gains twenty percent output per additional weapon");
    Check(PrestigeTalentEffects.PhaseLogisticsValue(100) == 75
      && PrestigeTalentEffects.CompressedGemValue(100) == 200
      && PrestigeTalentEffects.ConstellationPayload(100) == 150,
      "Endgame conversions apply their advertised value and payload tradeoffs");

    manager.UGM.CommandCenterUnlocked = true;
    manager.UGM.CommandChain = true;
    var commands = new ManualFleetAbilities();
    commands.UpdateUnlocks(ulong.MaxValue);
    Check(commands.TryActivate(0, _ => { }) && commands.TryActivate(1, _ => { })
      && Math.Abs(commands.RemainingCooldown(0) - 33.75f) < 0.001f,
      "Command Chain removes one quarter of the previous command cooldown");
    commands.Reset();
    manager.UGM.CommandChain = false;
    manager.UGM.CommandNexus = true;
    commands.UpdateUnlocks(0);
    Check(commands.UnlockedCount == ManualFleetAbilities.Definitions.Length
      && commands.TryActivate(0, _ => { })
      && Math.Abs(commands.RemainingCooldown(0) - 31.5f) < 0.001f
      && Math.Abs(commands.RemainingCooldown(1) - 10f) < 0.001f,
      "Command Nexus unlocks all commands, shortens recharge and applies its global cooldown");
    Check(MulticastTable.GetCastCount(true, PrestigeTalentEffects.MulticastMasteryLevels, 0) == 5,
      "Multicast Mastery reaches quintuple automatic ability casts");

    Check(manager.RespecPrestigeTalents() == 3 && manager.CurrentPrestigePoints == 3,
      "Refund all returns the exact allocated prestige cost");
    Check(tree.Values.All(button => button.CurrentLevel == 0)
      && !manager.UGM.CommandCenterUnlocked && !manager.UGM.OverloadedHolds && !manager.UGM.FleetRequisition,
      "Refund all clears talent levels and their effects");

    Console.WriteLine("Prestige talent checks passed: 25 talents, all tier gates, combinations, refund and reset.");
  }
}
