static class Checks
{
    public static void Run()
    {
        int count = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            count++;
        }
        var defaults = Options.Parse([]);
        Check(defaults.ManualCollectionRate(0) == 3 && defaults.ManualCollectionRate(1) == 3
            && defaults.ManualCollectionRate(10) < 3 && defaults.ManualCollectionRate(10) > .25
            && defaults.ManualCollectionRate(20) == .25 && defaults.ManualCollectionRate(100) == .25,
            "Default manual collection must taper with the fleet and retain occasional late collection");
        Check(Options.Parse(["--clicks", "0"]).ManualCollectionRate(0) == 0
            && Options.Parse(["--clicks", "2"]).ManualCollectionRate(100) == 2,
            "Explicit click rates must override the default profile, including idle runs");
        var sim = new Simulator(defaults);
        // Until Auto Cannon, half the clicks fire the cannon at the planet and half collect.
        Check(sim.Economy().Collection == 1.5, "Fresh runs must include manual collection before the first ship");
        Node Node(string id) => sim.Nodes.Single(n => n.Tree == "regular" && n.Id == id);
        Check(sim.Available(Node("HB")) && !sim.Available(Node("HS1")), "Dependency must block speed before home base");
        sim.Buy(Node("HB"));
        Check(sim.Available(Node("HS1")), "One parent level must unlock speed");
        Check(sim.Economy().Collection > 0, "Home base must grant the initial harvester");
        Check(Math.Abs(sim.Economy().Spawn - 1.5) < .00001, "Clicked cannon shots must knock gems loose");
        sim.Buy(Node("AC1"));
        Check(Math.Abs(sim.Economy().Spawn - 1 / (double)UntitledGemGame.MainShipWeapons.CannonInterval) < .00001,
            "Auto Cannon must fire once per interval");
        Check(sim.Economy().Value == 1, "Initial gem value must be one");
        sim.Loose = sim.LooseValue = 0;
        sim.Advance(new Rates(10, 2, 0, 1, 3, 5), 1);
        Check(sim.Loose == 5 && sim.LooseValue == 10 && sim.Earned == 3, "Cap must reject excess spawn value; passive earnings remain");
        sim.Advance(new Rates(0, 2, 2, 2, 0, 5), 1);
        Check(sim.Loose == 3 && sim.LooseValue == 6 && sim.Earned == 11, "Collection must conserve loose value and apply delivery multiplier once");
        sim.Earned = 100_000;
        var ability = sim.Nodes.First(n => n.Tree == "abilities" && n.Id == "GS1");
        Check(!sim.Available(ability), "Ship systems must wait for Auxiliary Power");
        var power = sim.Nodes.First(n => n.Tree == "meta" && n.Id == UntitledGemGame.ShipSystems.UnlockTalent);
        sim.Buy(power);
        Check(sim.Available(ability) && !sim.Available(sim.Nodes.First(n => n.Tree == "abilities" && n.Id == "GSNG1")),
            "Auxiliary Power must open the system cores, with their talents behind them");
        sim.Buy(ability);
        sim.Prestige();
        Check(Node("HB").Level == 0 && ability.Level == 0 && power.Level == 1,
            "Extraction must reset regular upgrades and ship systems, but keep prestige talents");
        Check(sim.ExpandSpaceLevel == 1, "The first extraction must reach talent tier one and its free Expand Space");
        Check(sim.Earned == 0 && sim.RunNumber == 2 && sim.Timeline.Last().Cost == 1, "Prestige must grant correct reward and clear earnings");
        Check(sim.Economy().Collection == 3, "Prestige must restore early manual collection while rebuilding the fleet");
        Check(Node("HB").Ever == 1, "Novel progression must survive reset");
        sim.Advance(new Rates(0, 0, 0, 1, 0, 0, UntitledGemGame.CoreFracture.Threshold(1) / 60), 1);
        Check(sim.CoreFractures == 2 && sim.Timeline.Count(e => e.Event == "fracture" && e.Run == sim.RunNumber) == 2,
            "Damage per minute past two thresholds must fracture the core twice, one Core Shard each");
        sim.Advance(new Rates(0, 0, 0, 1, 0, 0, UntitledGemGame.CoreFracture.Threshold(1) / 60), 1);
        Check(sim.CoreFractures == 2, "The same damage must not fracture the core again");
        sim.BuyAbilityPoint(AbilityPointProgression.GetPrice(0)!.Value);
        sim.Prestige();
        Check(sim.AbilityPointsPurchased == 0 && sim.Timeline.Any(e => e.Event == "ability-point"),
            "Power cells and their price curve must reset with each extraction");
        Check(sim.CoreFractures == 0, "Extraction must reset core fractures, so the next run earns its shards again");
        var noPrestige = new Simulator(new Options { Hours = 2, NoPrestige = true });
        noPrestige.Run();
        Check(noPrestige.Timeline.Any(e => e.Event == "purchase" && e.Currency == UntitledGemGame.CoreShards.Currency),
            "Core fracture shards must fund a powerful upgrade during a run");
        Check(noPrestige.RunNumber == 1 && noPrestige.ExpandSpaceLevel == 0
            && noPrestige.AbilityPointsPurchased == 0,
            "No-prestige comparisons never extract, so Auxiliary Power and power cells stay locked");
        var gated = sim.Nodes.First(n => n.Currency == "purple" && !n.Maxed && n.Next.RequiredExpandSpaceLevel > 1);
        foreach (var parent in sim.Nodes.Where(n => n.Tree == gated.Tree && n.Id == gated.Button.Data.BlockedBy)) parent.Level = 1;
        Check(!sim.Available(gated), "Purple levels must respect the next level's space requirement even with a purchased parent");
        var limited = new Simulator(new Options { Hours = .001, PurchaseSeconds = 10 });
        limited.Run();
        Check(limited.Seconds == 3.6 && limited.Timeline.Count == 0, "Menu delay must respect the time limit without purchasing early");
        foreach (var args in new[] { new[] { "--step", "0" }, new[] { "--hours", "NaN" }, new[] { "--clicks", "-1" } })
        {
            bool rejected = false;
            try { Options.Parse(args); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid simulation settings must be rejected");
        }
        Check(PrestigeProgression.GetReward(99_999) == 0 && PrestigeProgression.GetReward(100_000) == 1, "Prestige boundary mismatch");
        foreach (ulong reward in new ulong[] { 2, 5, 10, 25, 100 })
        {
            ulong required = PrestigeProgression.GetRequiredEarnings(reward)!.Value;
            Check(PrestigeProgression.GetReward(required) == reward
                && PrestigeProgression.GetReward(required - 1) < reward,
                "Prestige reward previews must agree with payouts at the revised curve's boundaries");
        }
        Console.WriteLine($"Passed {count} simulation checks.");
    }
}
