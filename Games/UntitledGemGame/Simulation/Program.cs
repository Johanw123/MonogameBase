using System.Globalization;
using System.Text.Json;
using UntitledGemGame;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
try
{
    var options = Options.Parse(args);
    if (options.Help)
    {
        Console.WriteLine("dotnet run --project Simulation -- [--hours 100] [--step 2] [--efficiency 0.65] [--clicks GEMS_PER_SECOND] [--distance 200] [--prestige 10] [--no-prestige] [--purchase-seconds 0] [--grind 300] [--output Simulation/results] [--data PATH] [--self-test]\nDefault manual collection: 3 gems/sec with up to one ship, tapering to 0.25 at 20 ships. --clicks overrides this with a constant rate; 0 disables it.");
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
    public double Hours = 100, Step = 2, Efficiency = .65, Distance = 200, Grind = 300;
    public double? Clicks;
    public double ManualCollectionRate(int fleetCount) => Clicks
        ?? 3 - 2.75 * Math.Clamp((fleetCount - 1) / 19.0, 0, 1);
    public double PurchaseSeconds;
    public ulong Prestige = 10;
    public string Output = "Simulation/results", Data = Path.Combine(AppContext.BaseDirectory, "Data");
    public bool Help, SelfTest, NoPrestige;
    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (key == "--help") { o.Help = true; continue; }
            if (key == "--self-test") { o.SelfTest = true; continue; }
            if (key == "--no-prestige") { o.NoPrestige = true; continue; }
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
            || (o.Clicks is double clicks && (!double.IsFinite(clicks) || clicks < 0)) || !double.IsFinite(o.PurchaseSeconds)
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
    public bool Action => Id == "ResetAbilities1";
    // Core Shard upgrades are a per-run build choice: no run can afford them all.
    public bool Choice => Currency == CoreShards.Currency;
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
    readonly Dictionary<string, double> balances = new() { ["red"] = 0, ["blue"] = 0, ["purple"] = 0, [CoreShards.Currency] = 0 };
    readonly HashSet<string> completedObjectives = [];
    readonly Dictionary<string, UpgradeButton> talents;
    double peakPerMinute;
    public readonly List<Entry> Timeline = [];
    UpgradesGeneratorUpgrades ug = new();
    UpgradesGeneratorUpgrades_abilities ua = new();
    UpgradesGeneratorUpgrades_meta um = new();
    public double Seconds, Earned, Loose, LooseValue;
    double lastEvent, lastNovel, income;
    public int RunNumber = 1;
    public string Status = "Time limit reached";
    public double? EverCompleted;
    public double? RegularCompleted;
    public ulong AbilityPointsPurchased;
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
        talents = Nodes.Where(n => n.Tree == "meta").ToDictionary(n => n.Id, n => n.Button);
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
        return unlocked && (n.Currency != "purple" || ExpandSpaceLevel >= n.Next.RequiredExpandSpaceLevel);
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
        ug.HomeBaseCollector = ug.HomeBase;
        if (ug.HarvesterUnlocked) ug.HarvesterCount++;
        if (ug.AdvancedHarvesterUnlocked) ug.AdvancedHarvesterCount++;
        if (ug.PerimeterHarvesterUnlocked) ug.PerimeterHarvesterCount++;
        if (ug.ExpertHarvesterUnlocked) ug.ExpertHarvesterCount++;
        if (ug.UltimateHarvesterUnlocked) ug.UltimateHarvesterCount++;
        // Every extraction so far is one finished run; the talent tiers reached set Expand Space.
        ExpandSpaceLevel = CoreExtraction.ExpandSpaceLevel(talents, (ulong)(RunNumber - 1));
        CoreExtraction.ApplyExpandSpace(ug, ExpandSpaceLevel);
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
        CompleteObjectives();
    }
    public int ExpandSpaceLevel { get; private set; }
    // Same objectives and rewards as the game, measured on the simulated run.
    void CompleteObjectives()
    {
        var stats = CoreShards.Measure(ug, (ulong)Math.Clamp(Earned, 0, ulong.MaxValue), peakPerMinute);
        foreach (var objective in CoreShards.Objectives)
            if (CoreShards.IsComplete(objective, stats) && completedObjectives.Add(objective.Id))
            {
                balances[CoreShards.Currency] += objective.Reward;
                Timeline.Add(new(Seconds, RunNumber, "objective", objective.Id, 0, objective.Reward, CoreShards.Currency, 0, income));
            }
    }
    // Extracting the core: the run's reward, then a reset of the regular tree.
    public void Prestige()
    {
        ulong reward = PrestigeProgression.GetReward((ulong)Math.Clamp(Earned + LooseValue, 0, ulong.MaxValue));
        balances["purple"] += reward;
        Timeline.Add(new(Seconds, RunNumber, "prestige", "", 0, reward, "purple", 0, income));
        foreach (var n in Nodes.Where(n => n.Tree == "regular")) n.Level = 0;
        balances["red"] = Earned = 0;
        balances[CoreShards.Currency] = peakPerMinute = 0;
        completedObjectives.Clear();
        RunNumber++;
        RebuildStats();
        ResetWorld();
    }
    void ResetWorld()
    {
        Loose = Math.Min(BaseStats.StartingGemCount, ug.MaxGemCount);
        LooseValue = Loose * Economy().Value;
    }
    public void Run()
    {
        var rates = Economy();
        while (Seconds < options.Hours * 3600)
        {
            bool persistentRemaining = Nodes.Any(n => n.Tree == "meta" && !n.Action && !n.Maxed);
            if (!options.NoPrestige && persistentRemaining
                && PrestigeProgression.GetReward((ulong)Math.Clamp(Earned + LooseValue, 0, ulong.MaxValue)) >= options.Prestige)
            {
                Prestige();
                rates = Economy();
                continue;
            }
            var candidates = Nodes.Where(n => Available(n) && n.Id != "ResetAbilities1"
                && balances[n.Currency] >= n.Next.Cost)
                .OrderBy(n => n.Next.Cost).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
            ulong? pointPrice = AbilityPointProgression.GetPrice(AbilityPointsPurchased);
            bool buyPoint = pointPrice is ulong price && balances["red"] >= price
                && Nodes.Any(n => n.Tree == "abilities" && !n.Action && !n.Maxed)
                && (candidates.Count == 0 || price < candidates[0].Next.Cost);
            if (candidates.Count > 0 || buyPoint)
            {
                // Optional paused menu time; no income is earned during this action.
                if (Seconds + options.PurchaseSeconds > options.Hours * 3600)
                {
                    Seconds = options.Hours * 3600;
                    break;
                }
                Seconds += options.PurchaseSeconds;
                if (buyPoint)
                    BuyAbilityPoint(pointPrice!.Value);
                else
                    Buy(candidates[0]);
                rates = Economy();
                if (RegularCompleted == null && Nodes.Where(n => n.Tree == "regular" && !n.Action && !n.Choice)
                    .All(n => n.Ever == n.Button.Data.NumLevels)) RegularCompleted = Seconds;
                if (EverCompleted == null && Nodes.Where(n => !n.Action && !n.Choice).All(n => n.Ever == n.Button.Data.NumLevels)) EverCompleted = Seconds;
                if (Nodes.Where(n => !n.Action && !n.Choice).All(n => n.Maxed)) { Status = "All upgrade levels currently maxed"; break; }
                continue;
            }
            double dt = Math.Min(options.Step, options.Hours * 3600 - Seconds);
            Advance(rates, dt);
            Seconds += dt;
        }
    }
    public void BuyAbilityPoint(ulong price)
    {
        balances["red"] -= price;
        balances["blue"]++;
        AbilityPointsPurchased++;
        Timeline.Add(new(Seconds, RunNumber, "ability-point", "panel", (int)AbilityPointsPurchased,
            price, "red", Seconds - lastEvent, income));
        lastEvent = Seconds;
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
        peakPerMinute = Math.Max(peakPerMinute, income * 60);
        CompleteObjectives();
    }
    public Rates Economy()
    {
        int fleetCount = ug.HomeBase
            ? ug.HarvesterCount + ug.AdvancedHarvesterCount + ug.PerimeterHarvesterCount + ug.ExpertHarvesterCount + ug.UltimateHarvesterCount
            : 0;
        // Every gem is knocked loose by a main ship weapon, and gets its colors from
        // that weapon's fire power. Until the cannon is automated, half the player's
        // clicks fire it at the planet and half collect gems.
        double clicks = options.ManualCollectionRate(fleetCount);
        double manualShots = ug.AutoCannon ? 0 : clicks * 0.5;
        int cannonPower = MainShipWeapons.FirePower(ug, MainShipWeapon.Cannon);
        double spawn = manualShots * cannonPower, colorValue = spawn * GemQualityTable.ExpectedValueMultiplier(cannonPower);
        foreach (var weapon in MainShipWeapons.All)
        {
            if (!MainShipWeapons.IsAutomatic(ug, weapon)) continue;
            int power = MainShipWeapons.FirePower(ug, weapon);
            float rate = MainShipWeapons.FireRate(ug, weapon);
            double gems = MainShipWeapons.GemsPerSecond(ug, weapon, rate, power);
            double thermal = weapon == MainShipWeapon.Laser && ug.MiningLaserThermalLance ? MainShipWeapons.ThermalLanceValue : 1;
            spawn += gems;
            colorValue += gems * GemQualityTable.ExpectedValueMultiplier(power) * thermal;
        }
        double value = (uint)((ug.GemValue + um.GemValue) * um.GemValueMultiplier)
            * (spawn > 0 ? colorValue / spawn : 1);
        // Equip Gem Spawner first; other active abilities are omitted from this baseline.
        if (ua.AbilitySlot > 0 && ua.GemSpawner > 0 && ug.HomeBase)
        {
            int gems = ua.GemSpawnerNrGems, rings = 0;
            for (int i = 0; i < ua.GemSpawnerNumberOfRings; i++) { rings += gems; gems /= 2; }
            spawn += rings * ua.GemSpawnerCooldown * um.AllAbilityCooldown / (BaseStats.GemSpawnerCooldownMilliseconds / 1000.0);
        }
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
            Ship(ug.PerimeterHarvesterCount, BaseStats.PerimeterHarvesterSpeed * ug.PerimeterHarvesterSpeed, ug.PerimeterHarvesterCollectionRange, ug.PerimeterHarvesterCapacity, ug.PerimeterHarvesterMaxFuel, ug.PerimeterFuelEfficiency, ug.PerimeterHarvesterRefuelSpeed);
            Ship(ug.ExpertHarvesterCount, BaseStats.ExpertHarvesterSpeed * ug.ExpertHarvesterSpeed, ug.ExpertHarvesterCollectionRange, ug.ExpertHarvesterCapacity, ug.ExpertHarvesterMaxFuel, ug.ExpertFuelEfficiency, ug.ExpertHarvesterRefuelSpeed);
            Ship(ug.UltimateHarvesterCount, BaseStats.UltimateHarvesterSpeed * ug.UltimateHarvesterSpeed, ug.UltimateHarvesterCollectionRange, ug.UltimateHarvesterCapacity, ug.UltimateHarvesterMaxFuel, ug.UltimateFuelEfficiency, ug.UltimateHarvesterRefuelSpeed);
        }
        double direct = clicks - manualShots
            + (ug.HomeBaseCollector ? options.Efficiency * ug.HomebaseCollectionRange : 0);
        double multiplier = um.AllHarvesterValueMultiplier * CoreShards.FleetValueMultiplier(ug);
        if (um.JackpotHaul) multiplier *= 1 + BaseStats.JackpotHaulChance * ((1 - BaseStats.JackpotHaulMegaChance) * BaseStats.JackpotHaulMultiplier + BaseStats.JackpotHaulMegaChance * BaseStats.JackpotHaulMegaMultiplier - 1);
        double collection = fleet + direct;
        return new(spawn, value, collection, collection > 0 ? (fleet * multiplier + direct) / collection : 1,
            ug.PassiveIncome / ClickUtility.PassiveInterval(ug), ug.MaxGemCount);
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
            EverCompleted, RegularCompleted, AbilityPointsPurchased, Options = options, Warnings, Balances = balances, Pending = pending,
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
            options.Clicks is double clicks ? $"- Manual collection: constant {clicks} gems/sec"
                : "- Manual collection: 3 gems/sec at 0–1 ships, tapering linearly to 0.25 at 20+ ships; restarts after prestige",
            $"- Ever purchased: {Nodes.Where(n => !n.Action).Sum(n => n.Ever)} / {Nodes.Where(n => !n.Action).Sum(n => n.Button.Data.NumLevels)} levels",
            $"- Currently maxed: {Nodes.Count(n => !n.Action && n.Maxed)} / {Nodes.Count(n => !n.Action)} nodes",
            $"- First all-levels-ever milestone: {(EverCompleted is double t ? $"{t / 3600:F2} hours" : "not reached")}",
            $"- Regular tree purchased (excluding prestige actions): {(RegularCompleted is double rt ? $"{rt / 3600:F2} hours" : "not reached")}",
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
