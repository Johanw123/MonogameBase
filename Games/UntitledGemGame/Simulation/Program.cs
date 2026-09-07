using System.Globalization;
using System.Text.Json;
using UntitledGemGame;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
try
{
    var options = Options.Parse(args);
    if (options.Help)
    {
        Console.WriteLine("dotnet run --project Simulation -- [--hours 100] [--step 2] [--efficiency 0.65] [--clicks 0] [--distance 200] [--prestige 10] [--purchase-seconds 0] [--grind 300] [--output Simulation/results] [--data PATH] [--self-test]");
        return;
    }
    if (options.SelfTest) { Checks.Run(); return; }
    var sim = new Simulator(options);
    sim.Run();
    sim.WriteReport();
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
}

sealed record Options
{
    public double Hours = 100, Step = 2, Efficiency = .65, Clicks = 0, Distance = 200, Grind = 300;
    public double PurchaseSeconds;
    public ulong Prestige = 10;
    public string Output = "Simulation/results", Data = Path.Combine(AppContext.BaseDirectory, "Data");
    public bool Help, SelfTest;
    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (key == "--help") { o.Help = true; continue; }
            if (key == "--self-test") { o.SelfTest = true; continue; }
            if (++i == args.Length) throw new ArgumentException($"Missing value for {key}");
            string value = args[i];
            switch (key)
            {
                case "--hours": o.Hours = double.Parse(value); break;
                case "--step": o.Step = double.Parse(value); break;
                case "--efficiency": o.Efficiency = double.Parse(value); break;
                case "--clicks": o.Clicks = double.Parse(value); break;
                case "--distance": o.Distance = double.Parse(value); break;
                case "--grind": o.Grind = double.Parse(value); break;
                case "--prestige": o.Prestige = ulong.Parse(value); break;
                case "--purchase-seconds": o.PurchaseSeconds = double.Parse(value); break;
                case "--output": o.Output = value; break;
                case "--data": o.Data = value; break;
                default: throw new ArgumentException($"Unknown option {key}");
            }
        }
        if (new[] { o.Hours, o.Step, o.Efficiency, o.Distance, o.Grind }.Any(x => !double.IsFinite(x) || x <= 0)
            || !double.IsFinite(o.Clicks) || o.Clicks < 0 || !double.IsFinite(o.PurchaseSeconds)
            || o.PurchaseSeconds < 0 || o.Efficiency > 1 || o.Prestige == 0)
            throw new ArgumentException("Times/distance must be finite and positive, efficiency in (0, 1], clicks >= 0, prestige >= 1.");
        return o;
    }
}

sealed class Node(string tree, UpgradeButton button)
{
    public string Tree = tree;
    public UpgradeButton Button = button;
    public string Id => Button.Data.ShortName;
    public string Key => Tree + ":" + Id;
    public string Property => Button.Data.UpgradeDefinition.PropertyName;
    public string Currency => Button.Data.UpgradeDefinition.Currency;
    public int Level { get => Button.CurrentLevel; set => Button.CurrentLevel = value; }
    public int Ever;
    public bool Action => Id is "P1" or "ResetAbilities1";
    public bool Maxed => Level >= Button.Data.NumLevels;
    public UpgradeDataLevel Next => Button.Data.LevelInfo[Level];
}

sealed record Entry(double Seconds, int Run, string Event, string Upgrade, int Level,
    double Cost, string Currency, double GapSeconds, double RedPerSecond);
sealed record Rates(double Spawn, double Value, double Collection, double DeliveryMultiplier, double Passive, double Cap);

sealed class Simulator
{
    readonly Options options;
    public readonly List<Node> Nodes = [];
    public readonly List<string> Warnings = [];
    readonly Dictionary<string, double> balances = new() { ["red"] = 0, ["blue"] = 0, ["purple"] = 0 };
    public readonly List<Entry> Timeline = [];
    UpgradesGeneratorUpgrades ug = new();
    UpgradesGeneratorUpgrades_abilities ua = new();
    UpgradesGeneratorUpgrades_meta um = new();
    public double Seconds, Earned, Loose, LooseValue;
    double lastEvent, lastNovel, income;
    public int RunNumber = 1;
    public string Status = "Time limit reached";
    public double? EverCompleted;
    readonly List<(double Gap, double At, string Upgrade)> noveltyGaps = [];
    public Simulator(Options options)
    {
        this.options = options;
        foreach (string tree in new[] { "regular", "abilities", "meta" })
        {
            string suffix = tree == "regular" ? "" : "_" + tree;
            var upgrades = new Upgrades();
            var buttons = new Dictionary<string, UpgradeButton>();
            var definitions = new Dictionary<string, JsonUpgrade>();
            upgrades.LoadJson(File.ReadAllText(Path.Combine(options.Data, $"upgrades{suffix}.json")),
                File.ReadAllText(Path.Combine(options.Data, $"upgrades{suffix}_buttons.json")), buttons, definitions);
            foreach (var button in buttons.Values)
            {
                if (!definitions.ContainsKey(button.Data.UpgradeDefinition.ShortName))
                    Warnings.Add($"Excluded {tree}:{button.Data.ShortName}: missing upgrade definition (editor placeholder).");
                else Nodes.Add(new Node(tree, button));
            }
        }
        foreach (var n in Nodes)
        {
            if (n.Button.Data.LevelInfo.Count != n.Button.Data.NumLevels)
                throw new InvalidDataException($"Incomplete levels: {n.Key}");
            foreach (string dependency in new[] { n.Button.Data.HiddenBy, n.Button.Data.LockedBy, n.Button.Data.BlockedBy })
                if (!string.IsNullOrEmpty(dependency) && !Nodes.Any(p => p.Tree == n.Tree && p.Id == dependency))
                    throw new InvalidDataException($"Missing dependency {dependency}: {n.Key}");
        }
        ResetWorld();
    }
    public bool Available(Node n)
    {
        if (n.Maxed) return false;
        var d = n.Button.Data;
        bool root = string.IsNullOrEmpty(d.BlockedBy) && string.IsNullOrEmpty(d.LockedBy) && string.IsNullOrEmpty(d.HiddenBy);
        bool unlocked = root || n.Level > 0 || Nodes.Any(p => p.Tree == n.Tree && p.Id == d.BlockedBy && p.Level > 0);
        // Space retains its level but is hidden until the home base is rebuilt.
        if (n.Id == "CZS1" && !ug.HomeBase) return false;
        return unlocked && (n.Currency != "purple" || Nodes.Single(x => x.Id == "CZS1").Level >= n.Next.RequiredExpandSpaceLevel);
    }
    void Apply(Node n, UpgradeDataLevel level)
    {
        string id = n.Button.Data.UpgradeDefinition.ShortName;
        switch (n.Button.Data.UpgradeDefinition.Type)
        {
            case "float": ug.Increment(id, level.m_upgradeAmountFloat); ua.Increment(id, level.m_upgradeAmountFloat); um.Increment(id, level.m_upgradeAmountFloat); break;
            case "int": ug.Increment(id, level.m_upgradeAmountInt); ua.Increment(id, level.m_upgradeAmountInt); um.Increment(id, level.m_upgradeAmountInt); break;
            case "bool": ug.Set(id, level.m_upgradesToBool); ua.Set(id, level.m_upgradesToBool); um.Set(id, level.m_upgradesToBool); break;
        }
    }
    void RebuildStats()
    {
        ug = new(); ua = new(); um = new();
        foreach (var n in Nodes)
            for (int i = 0; i < n.Level; i++) Apply(n, n.Button.Data.LevelInfo[i]);
        if (ug.HomeBase) ug.HarvesterCount++;
    }
    public void Buy(Node n)
    {
        var next = n.Next;
        balances[n.Currency] -= next.Cost;
        if (n.Button.Data.UpgradeDefinition.ShortName == "AP") balances["blue"] += next.m_upgradeAmountInt;
        n.Level++;
        if (n.Level > n.Ever && !n.Action)
        {
            noveltyGaps.Add((Seconds - lastNovel, Seconds, n.Key)); lastNovel = Seconds;
            n.Ever = n.Level;
        }
        Timeline.Add(new(Seconds, RunNumber, "purchase", n.Key, n.Level, next.Cost, n.Currency, Seconds - lastEvent, income));
        lastEvent = Seconds;
        RebuildStats();
        if (n.Id is "CZS1" or "P1") Prestige();
    }
    void Prestige()
    {
        ulong reward = PrestigeProgression.GetReward((ulong)Math.Clamp(Earned + LooseValue, 0, ulong.MaxValue));
        balances["purple"] += reward;
        Timeline.Add(new(Seconds, RunNumber, "prestige", "", 0, reward, "purple", 0, income));
        foreach (var n in Nodes.Where(n => n.Tree == "regular" && n.Button.Data.UpgradeDefinition.ShortName != "CZS")) n.Level = 0;
        balances["red"] = Earned = 0;
        RunNumber++;
        RebuildStats();
        ResetWorld();
    }
    void ResetWorld()
    {
        Loose = Math.Min(um.StartingGemCount, ug.MaxGemCount);
        LooseValue = Loose * Economy().Value;
    }
    public void Run()
    {
        var rates = Economy();
        while (Seconds < options.Hours * 3600)
        {
            bool persistentRemaining = Nodes.Any(n => n.Tree != "regular" && !n.Action && !n.Maxed);
            bool expanded = Nodes.Single(n => n.Id == "CZS1").Maxed;
            var candidates = Nodes.Where(n => Available(n) && n.Id != "ResetAbilities1"
                && (n.Id != "P1" || expanded && persistentRemaining
                    && PrestigeProgression.GetReward((ulong)Math.Clamp(Earned + LooseValue, 0, ulong.MaxValue)) >= options.Prestige)
                && balances[n.Currency] >= n.Next.Cost)
                .OrderBy(n => n.Next.Cost).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
            if (candidates.Count > 0)
            {
                // Optional paused menu time; no income is earned during this action.
                if (Seconds + options.PurchaseSeconds > options.Hours * 3600)
                {
                    Seconds = options.Hours * 3600;
                    break;
                }
                Seconds += options.PurchaseSeconds;
                Buy(candidates[0]); rates = Economy();
                if (EverCompleted == null && Nodes.Where(n => !n.Action).All(n => n.Ever == n.Button.Data.NumLevels)) EverCompleted = Seconds;
                if (Nodes.Where(n => !n.Action).All(n => n.Maxed)) { Status = "All upgrade levels currently maxed"; break; }
                continue;
            }
            double dt = Math.Min(options.Step, options.Hours * 3600 - Seconds);
            Advance(rates, dt);
            Seconds += dt;
        }
    }
    public void Advance(Rates r, double dt)
    {
        double spawned = Math.Min(r.Spawn * dt, Math.Max(0, r.Cap - Loose));
        Loose += spawned; LooseValue += spawned * r.Value;
        double collected = Math.Min(Loose, r.Collection * dt);
        double value = Loose > 0 ? LooseValue * collected / Loose : 0;
        Loose -= collected; LooseValue = Math.Max(0, LooseValue - value);
        double earned = value * r.DeliveryMultiplier + r.Passive * dt;
        balances["red"] += earned; Earned += earned; income = earned / dt;
    }
    public Rates Economy()
    {
        // Expected quality and luck, using the game's actual quality table and unlocks.
        bool[] unlocked = [true, ug.LightGreenGemUnlocked, ug.BlueGemUnlocked, ug.TealGemUnlocked,
            ug.LilacGemUnlocked, ug.PurpleGemUnlocked, ug.GoldGemUnlocked, ug.DarkBlueGemUnlocked];
        double value = (uint)((ug.GemValue + um.GemValue) * um.GemValueMultiplier);
        var row = GemQualityTable.Levels[Math.Clamp(ug.GemSpawnQuality - 1, 0, GemQualityTable.Levels.Length - 1)];
        string[] colors = ["Red", "LightGreen", "Blue", "Teal", "Lilac", "Purple", "Gold", "DarkBlue"];
        value *= row.Sum(e => e.ChancePercent / 100.0 * (unlocked[Array.IndexOf(colors, e.Type.ToString())] ? e.ValueMultiplier : 1));
        if (ug.LuckyGems) value *= 1 + Math.Clamp(ug.LuckyGemChance, 0, 1) * (ug.LuckyGemValue - 1);
        double clusterChance = ug.ClusterGems ? Math.Clamp(ug.ClusterGemsChance, 0, 1) : 0;
        double clusters = ug.Supercluster ? 1 + BaseStats.SuperclusterChance * (BaseStats.SuperclusterCount - 1) : 1;
        double size = Math.Max(1, ug.ClusterSize) * (ug.Motherlode ? 1 + BaseStats.MotherlodeChance * (BaseStats.MotherlodeSizeMultiplier - 1) : 1);
        double burst = 1 - clusterChance + clusterChance * clusters * size;
        double ambient = ug.GemSpawnRate * ug.GemSpawnCooldown / BaseStats.GemSpawnCooldownSeconds;
        double spawn = ambient * burst;
        double coreExtra = ug.ClusterCore ? ambient * clusterChance * clusters * (BaseStats.ClusterCoreValueMultiplier - 1) : 0;
        double cosmicSpawns = 0;
        if (ug.GemShower) cosmicSpawns += ug.GemShowerGemCount * ug.GemShowerCooldown / BaseStats.GemShowerCooldownSeconds;
        if (ug.GemComet) cosmicSpawns += ug.GemCometGemCount * ug.GemCometCooldown / BaseStats.GemCometCooldownSeconds;
        spawn += cosmicSpawns * (ug.CosmicClusters ? burst : 1);
        if (ug.CosmicClusters && ug.ClusterCore)
            coreExtra += cosmicSpawns * clusterChance * clusters * (BaseStats.ClusterCoreValueMultiplier - 1);
        // Equip Gem Spawner first; other active abilities are omitted from this baseline.
        if (ua.AbilitySlot > 0 && ua.GemSpawner > 0 && ug.HomeBase)
        {
            int gems = ua.GemSpawnerNrGems, rings = 0;
            for (int i = 0; i < ua.GemSpawnerNumberOfRings; i++) { rings += gems; gems /= 2; }
            double extra = (ua.GemSpawnerHarvesters ? ug.HarvesterCount : 0)
                + (ua.GemSpawnerAdvancedHarvesters ? ug.AdvancedHarvesterCount : 0)
                + (ua.GemSpawnerExpertHarvesters ? ug.ExpertHarvesterCount : 0)
                + (ua.GemSpawnerUltimateHarvesters ? ug.UltimateHarvesterCount : 0);
            spawn += (rings + extra * Math.Ceiling(ua.GemSpawnerNrGems * .3)) * ua.GemSpawnerCooldown / (BaseStats.GemSpawnerCooldownMilliseconds / 1000.0);
        }
        if (spawn > 0) value *= 1 + coreExtra / spawn;
        double fleet = 0;
        // Average travel/cargo cycle; distance and encounter efficiency are calibration inputs.
        void Ship(int count, double speed, double range, double capacity, double fuel, double efficiency, double refuel)
        {
            speed *= um.AllHarvesterSpeed;
            capacity = Math.Ceiling(capacity * um.AllHarvesterCapacity);
            double distance = options.Distance * 3.5 / Math.Max(.1, ug.CameraZoomScale);
            double cycle = distance / speed + distance / (speed * um.AllHarvesterReturnSpeed)
                + capacity / Math.Max(.01, options.Efficiency * range * um.AllHarvesterCollectionRange);
            double fuelSeconds = 2500 * fuel * um.AllHarvesterMaxFuel * efficiency * um.AllHarvesterFuelEfficiency / (1.5 * speed);
            double uptime = fuelSeconds / (fuelSeconds + 2 / refuel);
            fleet += count * capacity / cycle * uptime;
        }
        if (ug.HomeBase)
        {
            Ship(ug.HarvesterCount, BaseStats.HarvesterSpeed * ug.HarvesterSpeed, ug.HarvesterCollectionRange, ug.HarvesterCapacity, ug.HarvesterMaxFuel, ug.FuelEfficiency, ug.HarvesterRefuelSpeed);
            Ship(ug.AdvancedHarvesterCount, BaseStats.AdvancedHarvesterSpeed * ug.AdvancedHarvesterSpeed, ug.AdvancedHarvesterCollectionRange, ug.AdvancedHarvesterCapacity, ug.AdvancedHarvesterMaxFuel, ug.AdvancedFuelEfficiency, ug.AdvancedHarvesterRefuelSpeed);
            Ship(ug.ExpertHarvesterCount, BaseStats.ExpertHarvesterSpeed * ug.ExpertHarvesterSpeed, ug.ExpertHarvesterCollectionRange, ug.ExpertHarvesterCapacity, ug.ExpertHarvesterMaxFuel, ug.ExpertFuelEfficiency, ug.ExpertHarvesterRefuelSpeed);
            Ship(ug.UltimateHarvesterCount, BaseStats.UltimateHarvesterSpeed * ug.UltimateHarvesterSpeed, ug.UltimateHarvesterCollectionRange, ug.UltimateHarvesterCapacity, ug.UltimateHarvesterMaxFuel, ug.UltimateFuelEfficiency, ug.UltimateHarvesterRefuelSpeed);
        }
        double direct = options.Clicks + (ug.HomeBaseCollector ? options.Efficiency * ug.HomebaseCollectionRange : 0);
        double multiplier = um.AllHarvesterValueMultiplier;
        if (um.JackpotHaul) multiplier *= 1 + BaseStats.JackpotHaulChance * ((1 - BaseStats.JackpotHaulMegaChance) * BaseStats.JackpotHaulMultiplier + BaseStats.JackpotHaulMegaChance * BaseStats.JackpotHaulMegaMultiplier - 1);
        double collection = fleet + direct;
        return new(spawn, value, collection, collection > 0 ? (fleet * multiplier + direct) / collection : 1,
            ug.PassiveIncome / BaseStats.PassiveIncomeInterval, ug.MaxGemCount);
    }
    public void WriteReport()
    {
        Directory.CreateDirectory(options.Output);
        var pending = Nodes.Where(n => !n.Action && !n.Maxed).Select(n => new
        {
            n.Key, n.Level, Maximum = n.Button.Data.NumLevels, Ever = n.Ever,
            Available = Available(n), Cost = n.Next.Cost, n.Currency,
            Prerequisite = n.Button.Data.BlockedBy, SpaceRequired = n.Next.RequiredExpandSpaceLevel
        }).ToArray();
        File.WriteAllText(Path.Combine(options.Output, "summary.json"), JsonSerializer.Serialize(new
        {
            Model = "Expected-value economy approximation, not the live ECS", Status, Seconds, RunNumber,
            EverCompleted, Options = options, Warnings, Balances = balances, Pending = pending,
            LongestNoveltyGaps = noveltyGaps.OrderByDescending(g => g.Gap).Take(20).Select(g => new { g.Gap, g.At, g.Upgrade }),
            UnfinishedNoveltyGap = Seconds - lastNovel
        }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        string Csv(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
        File.WriteAllLines(Path.Combine(options.Output, "timeline.csv"), new[] { "seconds,run,event,upgrade,level,cost,currency,gap_seconds,red_per_second" }
            .Concat(Timeline.Select(e => $"{e.Seconds:F2},{e.Run},{e.Event},{Csv(e.Upgrade)},{e.Level},{e.Cost},{e.Currency},{e.GapSeconds:F2},{e.RedPerSecond:F4}")));
        var lines = new List<string>
        {
            "# Progression simulation", "", "Expected-value estimate; collection/abilities are approximations. See Simulation/README.md.", "",
            $"- Result: {Status}", $"- Simulated playtime: {Seconds / 3600:F2} hours; runs: {RunNumber}",
            $"- Ever purchased: {Nodes.Where(n => !n.Action).Sum(n => n.Ever)} / {Nodes.Where(n => !n.Action).Sum(n => n.Button.Data.NumLevels)} levels",
            $"- Currently maxed: {Nodes.Count(n => !n.Action && n.Maxed)} / {Nodes.Count(n => !n.Action)} nodes",
            $"- First all-levels-ever milestone: {(EverCompleted is double t ? $"{t / 3600:F2} hours" : "not reached")}",
            $"- Unfinished wait since last purchase: {(Seconds - lastEvent) / 60:F2} minutes",
            $"- Unfinished wait since new progression: {(Seconds - lastNovel) / 60:F2} minutes", "",
            "## Longest waits between purchases", "", "| At (hours) | Run | Next upgrade | Wait (minutes) | Income/sec |", "|---:|---:|---|---:|---:|"
        };
        foreach (var e in Timeline.Where(e => e.Event == "purchase").OrderByDescending(e => e.GapSeconds).Take(20))
            lines.Add($"| {e.Seconds / 3600:F2} | {e.Run} | {e.Upgrade} level {e.Level} | {e.GapSeconds / 60:F2}{(e.GapSeconds >= options.Grind ? " GRIND" : "")} | {e.RedPerSecond:F2} |");
        lines.AddRange(["", "## Longest waits for a previously unpurchased level", "", "Includes time spent rebuilding after prestige.", "", "| At (hours) | Upgrade | Wait (minutes) |", "|---:|---|---:|"]);
        foreach (var g in noveltyGaps.OrderByDescending(g => g.Gap).Take(20)) lines.Add($"| {g.At / 3600:F2} | {g.Upgrade} | {g.Gap / 60:F2}{(g.Gap >= options.Grind ? " GRIND" : "")} |");
        lines.AddRange(["", "## Data warnings", ""]);
        lines.AddRange(Warnings);
        lines.AddRange(["", "## Remaining upgrades", "", "| Upgrade | Level | Ever | Next cost | Available | Space required |", "|---|---:|---:|---|---|---:|"]);
        foreach (var n in pending) lines.Add($"| {n.Key} | {n.Level}/{n.Maximum} | {n.Ever} | {n.Cost} {n.Currency} | {n.Available} | {n.SpaceRequired} |");
        File.WriteAllLines(Path.Combine(options.Output, "report.md"), lines);
        Console.WriteLine(string.Join(Environment.NewLine, lines.Take(12)));
        Console.WriteLine($"Reports: {Path.GetFullPath(options.Output)}");
    }
}
