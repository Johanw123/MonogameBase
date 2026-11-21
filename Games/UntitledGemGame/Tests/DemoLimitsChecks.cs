using UntitledGemGame;

// The demo's limits (DemoLimits): two talent tiers with the third shown as a preview, a
// prestige ladder that stops paying, and the nodes and weapon it holds back.
internal static class DemoLimitsChecks
{
  public static void Run()
  {
    static void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }

    var tree = UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta;
    var regular = UpgradeManager.CurrentUpgrades.UpgradeButtons;
    static GameSave Talents(params string[] ids) => new() { Meta = ids.ToDictionary(id => id, _ => 1) };
    static UpgradeManager Extracted(GameSave save)
    {
      var manager = new UpgradeManager();
      typeof(UpgradeManager).GetField("m_gameState", System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.NonPublic)!.SetValue(manager, new GameState { CoreExtractions = 1 });
      manager.RestoreProgress(save);
      return manager;
    }

    bool? before = DemoLimits.Override;
    bool wasDemo = Demo.IsDemo, wasDev = Demo.IsDev;
    try
    {
      DemoLimits.Override = null;
      Demo.IsDemo = true; Demo.IsDev = false;
      Check(DemoLimits.Active, "A demo build plays with the limits");
      Demo.IsDev = true;
      Check(!DemoLimits.Active, "A dev build plays the full game");
      Demo.IsDemo = false; Demo.IsDev = false;
      Check(!DemoLimits.Active, "The full game has no limits");
      Demo.IsDemo = wasDemo; Demo.IsDev = wasDev;

      DemoLimits.Override = true;
      Check(DemoLimits.TalentTiers == 2 && PrestigeTalentLayout.Tiers[DemoLimits.TalentTiers].FreeRewards.Contains("SYU1"),
        "The demo stops one tier short of the Shipyard");

      // Four points: both open tiers play as in the full game, the wall is not reached.
      var manager = Extracted(Talents("TR1", "PRL1", "DEY1", "LR1"));
      Check(tree["TT1"].State == UpgradeButton.UnlockState.Unlocked && tree["BR1"].State == UpgradeButton.UnlockState.Unlocked
        && manager.UGM.ShipSystemsUnlocked && manager.ExpandSpaceLevel == 2,
        "The demo's two tiers open, pay their free rewards and can be learned");
      // Four spent: the third tier is not within reach, so the run it plays is not the last,
      // unless the ladder has already paid its last point.
      Check(!PrestigeTalentLayout.DemoComplete(tree, 4) && !PrestigeTalentLayout.DemoComplete(tree, DemoLimits.PrestigePoints - 1)
        && PrestigeTalentLayout.DemoComplete(tree, DemoLimits.PrestigePoints),
        "A run short of the wall ends the demo only when no points are left to earn");
      Check(tree["EP1"].State == UpgradeButton.UnlockState.Revealed && tree["SYU1"].State == UpgradeButton.UnlockState.Revealed
        && tree["MBR1"].State == UpgradeButton.UnlockState.DemoLocked && tree["SGU1"].State == UpgradeButton.UnlockState.DemoLocked
        && tree["OUR1"].State == UpgradeButton.UnlockState.DemoLocked,
        "The next tier is shown as a preview and the ones below it are locked");

      // Five points would open tier three: that is the wall.
      manager = Extracted(Talents("TR1", "PRL1", "DEY1", "LR1", "BR1"));
      Check(PrestigeTalentLayout.IsTierReached(tree, 2) && PrestigeTalentLayout.DemoComplete(tree, 5)
        && !PrestigeTalentLayout.CanLearn(tree, "EP1") && tree["EP1"].State == UpgradeButton.UnlockState.Revealed
        && tree["SYU1"].CurrentLevel == 0 && !manager.UGM.ShipyardUnlocked && manager.ExpandSpaceLevel == 2
        && PrestigeTalentLayout.CanLearn(tree, "HSK1"),
        "Reaching the third tier learns none of it and claims no Shipyard or Expand Space");
      ulong points = manager.CurrentPrestigePoints;
      manager.Upgrade(tree["EP1"]);
      Check(tree["EP1"].CurrentLevel == 0 && manager.CurrentPrestigePoints == points, "A held-back talent can't be bought");

      // All fourteen talents of the open tiers: still no further.
      manager = Extracted(Talents(PrestigeTalentLayout.Tiers.Take(2).SelectMany(tier => tier.Talents).ToArray()));
      Check(!manager.UGM.ShipyardUnlocked && !manager.UGM.SignalsUnlocked && manager.ExpandSpaceLevel == 2
        && Math.Abs(manager.GemLoreMultiplier - Math.Pow(CoreExtraction.GemLore[0], 7) * Math.Pow(CoreExtraction.GemLore[1], 7)) < 1e-9,
        "Filling both tiers unlocks nothing past them");
      manager.RespecPrestigeTalents();
      // Two first-tier talents: the second tier is still closed, however many points wait.
      manager = Extracted(Talents("TR1", "PRL1"));
      Check(!PrestigeTalentLayout.DemoComplete(tree, DemoLimits.PrestigePoints),
        "The demo doesn't end before a run with the second tier open");
      manager.RespecPrestigeTalents();

      // The ladder pays its last point at the cap; a full-game ladder goes on.
      var state = new GameState();
      state.RestorePrestige(DemoLimits.PrestigePoints - 1, 0, 0, 0);
      state.EarnRedGems(ulong.MaxValue / 4);
      Check(state.UpdatePrestigeProgress() == 1 && state.PrestigePointsEarned == DemoLimits.PrestigePoints
        && state.PrestigePointsExhausted && state.PrestigeProgress == 0 && state.UpdatePrestigeProgress() == 0,
        "The demo's ladder stops at its last point, whatever the income");
      DemoLimits.Override = false;
      Check(!state.PrestigePointsExhausted && state.UpdatePrestigeProgress() > 0, "The full game's ladder goes on");
      DemoLimits.Override = true;

      // Held-back nodes: the late fleets, the warp drive and the Railgun.
      string[] flagged = regular.Values.Where(button => button.Data.LockedInDemo)
        .Select(button => button.Data.ShortName).Order().ToArray();
      Check(flagged.SequenceEqual(new[] { "EHU1", "UHU1", "UHWD1", "WDR1" }),
        "The demo flags the Prospector, the Trove Hunter and both warp drive nodes");
      manager = new UpgradeManager();
      // Seeker Count is what reveals the Prospector.
      manager.RestoreProgress(new GameSave { RedGems = ulong.MaxValue / 2, Upgrades = new() { ["HB"] = 1, ["HU1"] = 1, ["HC1"] = regular["HC1"].Data.NumLevels, ["AHU1"] = 1, ["AHC1"] = 1 } });
      Check(regular["EHU1"].State == UpgradeButton.UnlockState.DemoLocked, "The Prospector shows as held back once revealed");
      manager.Upgrade(regular["EHU1"]);
      Check(regular["EHU1"].CurrentLevel == 0, "A held-back upgrade can't be bought");
      Check(!Weaponry.IsAvailable(MainShipWeapon.Railgun) && Weaponry.IsUnavailableNode("RG1") && Weaponry.IsUnavailableNode("RGP1")
        && Weaponry.UnavailableNote("RG1") == DemoLimits.FullGameNote && Weaponry.IsAvailable(MainShipWeapon.Rockets)
        && !manager.IsWeaponBlueprintUnlocked(MainShipWeapon.Railgun),
        "The demo holds the Railgun back and keeps the other weapons");

      DemoLimits.Override = false;
      manager = Extracted(Talents("TR1", "PRL1", "DEY1", "LR1", "BR1"));
      Check(PrestigeTalentLayout.CanLearn(tree, "EP1") && manager.UGM.ShipyardUnlocked && manager.ExpandSpaceLevel == 3
        && !PrestigeTalentLayout.DemoComplete(tree, 100) && Weaponry.IsAvailable(MainShipWeapon.Railgun),
        "Without the limits the third tier opens as usual");
      manager.RespecPrestigeTalents();
    }
    finally
    {
      DemoLimits.Override = before;
      Demo.IsDemo = wasDemo; Demo.IsDev = wasDev;
    }
    Console.WriteLine("Demo limit checks passed: two talent tiers with a preview, the point cap, held-back upgrades and the Railgun.");
  }
}
