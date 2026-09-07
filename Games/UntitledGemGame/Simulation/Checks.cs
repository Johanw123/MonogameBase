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
        var sim = new Simulator(new Options());
        Node Node(string id) => sim.Nodes.Single(n => n.Tree == "regular" && n.Id == id);
        Check(sim.Available(Node("HB")) && !sim.Available(Node("HS1")), "Dependency must block speed before home base");
        sim.Buy(Node("HB"));
        Check(sim.Available(Node("HS1")), "One parent level must unlock speed");
        Check(sim.Economy().Collection > 0, "Home base must grant the initial harvester");
        Check(Math.Abs(sim.Economy().Spawn - 1 / (double)BaseStats.GemSpawnCooldownSeconds) < .00001, "Base spawn cooldown mismatch");
        Check(sim.Economy().Value == 1, "Initial gem value must be one");
        sim.Loose = sim.LooseValue = 0;
        sim.Advance(new Rates(10, 2, 0, 1, 3, 5), 1);
        Check(sim.Loose == 5 && sim.LooseValue == 10 && sim.Earned == 3, "Cap must reject excess spawn value; passive earnings remain");
        sim.Advance(new Rates(0, 2, 2, 2, 0, 5), 1);
        Check(sim.Loose == 3 && sim.LooseValue == 6 && sim.Earned == 11, "Collection must conserve loose value and apply delivery multiplier once");
        sim.Earned = 100_000;
        var ability = sim.Nodes.First(n => n.Tree == "abilities" && n.Id == "AS1");
        sim.Buy(ability);
        sim.Buy(Node("CZS1")); // Transaction fixture deliberately bypasses affordability.
        Check(Node("HB").Level == 0 && Node("CZS1").Level == 1 && ability.Level == 1, "Prestige must reset regular upgrades and retain space/abilities");
        Check(sim.Earned == 0 && sim.RunNumber == 2 && sim.Timeline.Last().Cost == 1, "Prestige must grant correct reward and clear earnings");
        Check(!sim.Available(Node("CZS1")), "Retained expansion must require rebuilding home base");
        Check(Node("HB").Ever == 1, "Novel progression must survive reset");
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
        Console.WriteLine($"Passed {count} simulation checks.");
    }
}
