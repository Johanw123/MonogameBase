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
    string[] firstTier = ["CC1", "OH1", "FLR1", "DCM1", "TPM1"];
    Check(firstTier.All(id => tree[id].State == UpgradeButton.UnlockState.Unlocked)
      && tree["RH1"].State == UpgradeButton.UnlockState.Invisible
      && tree["AR1"].State == UpgradeButton.UnlockState.Revealed,
      "The five run-changing talents are active in the first prestige tier");

    manager = new UpgradeManager();
    manager.RestoreProgress(new GameSave { Meta = new() { ["CC1"] = 1, ["OH1"] = 1, ["FLR1"] = 1 } });
    Check(manager.UGM.CommandCenterUnlocked && manager.UGM.OverloadedHolds && manager.UGM.FleetRequisition
      && tree["AR1"].State == UpgradeButton.UnlockState.Unlocked,
      "Any three first-tier points activate their effects and unlock the second tier");

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

    Check(manager.RespecPrestigeTalents() == 3 && manager.CurrentPrestigePoints == 3,
      "Refund all returns the exact allocated prestige cost");
    Check(tree.Values.All(button => button.CurrentLevel == 0)
      && !manager.UGM.CommandCenterUnlocked && !manager.UGM.OverloadedHolds && !manager.UGM.FleetRequisition,
      "Refund all clears talent levels and their effects");

    Console.WriteLine("Prestige talent checks passed: first-tier mechanics, tier gate, refund and effect reset.");
  }
}
