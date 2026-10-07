using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

public enum ShipModule
{
  None, FinalSweep, TractorLink, JackpotCore, ReturnBeacon, ProspectorLens, WakeCollector,
  CargoPod, IonBooster, FuelRecycler, WidebandArray, OverdriveCoil, CrystalRefinery,
  EchoChamber, SingularityEngine, SupernovaCore, PhaseAnchor,
  AuxiliaryTank, QuickCoupler, HomewardJets, LaunchCapacitor, CargoScanner, LightFrame, DeepHold, ReserveBurn,
  VacuumNozzle, GemPolisher, SalvageCell, DockBattery, MomentumDrive, PulseHarvester, TwinTractor, PrismFilter,
  OverflowVault, CourierSeal, StormCoil, MidasTouch, ChronoDrive, RiftSiphon, ReactorBloom, EchoVault, EventHorizon,
  PhoenixReactor, QuantumForge, StellarEngine, InfinityHold, AstralRelay,
  CascadeCapacitor, OverflowDrive, KineticRefinery, ArcEmitter, GravityBuoy, RecallTether, ThunderGod, TimeHeist, WorldEater,
  GunPod, RocketRack, LaserUplink, DetonatorCharge
}

public enum ModuleRarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

public static class ModuleCatalog
{
  public const int BaseSlotsPerType = 2;
  public const int MaxSlotsPerType = 4;
  public static int UnlockedSlots => Math.Clamp(UpgradeManager.Instance.UGM.ModuleSlots, BaseSlotsPerType, MaxSlotsPerType);
  public const float TractorChance = 0.2f;
  public const float TractorRadius = 50f;
  public const float JackpotChance = 0.05f;
  public const ulong JackpotMultiplier = 5;
  public const float ProspectorRadius = 400f;
  public const float WakeRadius = 10f;
  public const float CargoMultiplier = 1.5f;
  public const float IonSpeedMultiplier = 1.25f;
  public const float FuelEfficiencyMultiplier = 2f;
  public const float WidebandRangeMultiplier = 1.1f;
  public const float VacuumReachBonus = 20f;
  public const float OverdriveDuration = 3f;
  public const float OverdriveSpeedMultiplier = 1.75f;
  public const double RefineryValueMultiplier = 1.5;
  public const int EchoInterval = 5;
  public const int SingularityInterval = 8;
  public const int SingularityGemLimit = 8;
  public const float SingularityRadius = 90f;
  public const int SupernovaGemLimit = 32;
  public const float SupernovaRadius = 120f;
  public const float CourierDeadlineSeconds = 20f;
  public const float PulseDuration = 0.6f;
  // Weapon modules (effects in GameScreen.TalentCombos.cs).
  public const int GunPodInterval = 10;
  public const int RocketRackGemsPerRocket = 10;
  public const int RocketRackMaxRockets = 8;
  public const float LaserUplinkSecondsPerGem = 0.1f;
  public const float LaserUplinkMaxSeconds = 4f;
  public const float DetonatorRadius = 0.9f; // radians around the side facing the ship
  public static readonly Harvester.HarvesterType[] Types =
    [Harvester.HarvesterType.Harvester, Harvester.HarvesterType.AdvancedHarvester,
     Harvester.HarvesterType.ExpertHarvester, Harvester.HarvesterType.UltimateHarvester,
     Harvester.HarvesterType.PerimeterHarvester];
  // Names and Descriptions hold English marked for the string table (names also identify
  // modules in capture scripts and sort the inventory); translate them where shown.
  public static readonly string[] Names =
    [Loc.N("Empty slot"), Loc.N("Final Sweep"), Loc.N("Tractor Link"), Loc.N("Jackpot Core"), Loc.N("Return Beacon"), Loc.N("Prospector Lens"), Loc.N("Wake Collector"),
     Loc.N("Cargo Pod"), Loc.N("Ion Booster"), Loc.N("Fuel Recycler"), Loc.N("Wideband Array"), Loc.N("Overdrive Coil"), Loc.N("Crystal Refinery"),
     Loc.N("Echo Chamber"), Loc.N("Singularity Engine"), Loc.N("Supernova Core"), Loc.N("Phase Anchor"),
     Loc.N("Auxiliary Tank"), Loc.N("Quick Coupler"), Loc.N("Homeward Jets"), Loc.N("Launch Capacitor"),
     Loc.N("Cargo Scanner"), Loc.N("Light Frame"), Loc.N("Deep Hold"), Loc.N("Reserve Burn"),
     Loc.N("Vacuum Nozzle"), Loc.N("Gem Polisher"), Loc.N("Salvage Cell"), Loc.N("Dock Battery"),
     Loc.N("Momentum Drive"), Loc.N("Pulse Harvester"), Loc.N("Twin Tractor"), Loc.N("Prism Filter"),
     Loc.N("Overflow Vault"), Loc.N("Courier Seal"), Loc.N("Storm Coil"), Loc.N("Midas Touch"),
     Loc.N("Chrono Drive"), Loc.N("Rift Siphon"), Loc.N("Reactor Bloom"), Loc.N("Echo Vault"),
     Loc.N("Event Horizon"), Loc.N("Phoenix Reactor"), Loc.N("Quantum Forge"), Loc.N("Stellar Engine"),
     Loc.N("Infinity Hold"), Loc.N("Astral Relay"),
     Loc.N("Cascade Capacitor"), Loc.N("Overflow Drive"), Loc.N("Kinetic Refinery"), Loc.N("Arc Emitter"), Loc.N("Gravity Buoy"), Loc.N("Recall Tether"), Loc.N("Thunder God"), Loc.N("Time Heist"), Loc.N("World Eater"),
     Loc.N("Gun Pod"), Loc.N("Rocket Rack"), Loc.N("Laser Uplink"), Loc.N("Detonator Charge")];
  public static readonly ModuleRarity[] Rarities =
    [ModuleRarity.Common, ModuleRarity.Rare, ModuleRarity.Uncommon, ModuleRarity.Epic,
     ModuleRarity.Epic, ModuleRarity.Uncommon, ModuleRarity.Rare,
     ModuleRarity.Common, ModuleRarity.Common, ModuleRarity.Uncommon, ModuleRarity.Uncommon,
     ModuleRarity.Rare, ModuleRarity.Rare, ModuleRarity.Epic, ModuleRarity.Epic,
     ModuleRarity.Legendary, ModuleRarity.Legendary,
     ModuleRarity.Common, ModuleRarity.Common, ModuleRarity.Common, ModuleRarity.Common, ModuleRarity.Common,
     ModuleRarity.Common, ModuleRarity.Uncommon, ModuleRarity.Uncommon, ModuleRarity.Uncommon,
     ModuleRarity.Uncommon, ModuleRarity.Uncommon, ModuleRarity.Uncommon, ModuleRarity.Rare, ModuleRarity.Rare,
     ModuleRarity.Rare, ModuleRarity.Rare, ModuleRarity.Rare, ModuleRarity.Rare, ModuleRarity.Epic,
     ModuleRarity.Epic, ModuleRarity.Epic, ModuleRarity.Epic, ModuleRarity.Epic, ModuleRarity.Epic,
     ModuleRarity.Legendary, ModuleRarity.Legendary, ModuleRarity.Legendary, ModuleRarity.Legendary,
     ModuleRarity.Legendary, ModuleRarity.Legendary, ModuleRarity.Epic, ModuleRarity.Epic, ModuleRarity.Epic,
     ModuleRarity.Rare, ModuleRarity.Epic, ModuleRarity.Rare,
     ModuleRarity.Mythic, ModuleRarity.Mythic, ModuleRarity.Mythic,
     ModuleRarity.Uncommon, ModuleRarity.Rare, ModuleRarity.Epic, ModuleRarity.Legendary];
  public static readonly string[] Icons =
    ["", "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_36.png",
     "Textures/craftpix_icons/craftpix-net-960481-genetics-pixel-art-icon-32x32-pack/1 Icons/Icon11_29.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_01.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_12.png",
     "Textures/craftpix_icons/craftpix-net-805026-implants-for-cyberpunk-32x32-pixel-icons/1 Icons/Icon32_10.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_40.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_12.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_04.png",
     "Textures/craftpix_icons/craftpix-net-572384-resources-for-cyberpunk-topic-pixel-art-32x32-icon-pack/1 Icons/Icon6_23.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_07.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_24.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_16.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_06.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_16.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_39.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_05.png",
     "Textures/craftpix_icons/craftpix-net-572384-resources-for-cyberpunk-topic-pixel-art-32x32-icon-pack/1 Icons/Icon6_04.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_07.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_03.png",
     "Textures/craftpix_icons/craftpix-net-572384-resources-for-cyberpunk-topic-pixel-art-32x32-icon-pack/1 Icons/Icon6_06.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_27.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_35.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_13.png",
     "Textures/craftpix_icons/craftpix-net-184808-free-cyberpunk-resource-pixel-art-32x32-icons/1 Icons/Icon14_24.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_01.png",
     "Textures/craftpix_icons/craftpix-net-572384-resources-for-cyberpunk-topic-pixel-art-32x32-icon-pack/1 Icons/Icon6_19.png",
     "Textures/craftpix_icons/craftpix-net-960481-genetics-pixel-art-icon-32x32-pack/1 Icons/Icon11_06.png",
     "Textures/craftpix_icons/craftpix-net-572384-resources-for-cyberpunk-topic-pixel-art-32x32-icon-pack/1 Icons/Icon6_07.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_27.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_01.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_33.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_09.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_11.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_18.png",
     "Textures/craftpix_icons/craftpix-net-184808-free-cyberpunk-resource-pixel-art-32x32-icons/1 Icons/Icon14_26.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_04.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_14.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_09.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_08.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_09.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_17.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_11.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_04.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_10.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_35.png",
     "Textures/craftpix_icons/craftpix-net-434981-cyberpunk-artefact-icons-pixel-art/1 Icons/Icon33_20.png",
     "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_04.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_38.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_18.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_02.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_03.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_05.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_02.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_03.png",
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_05.png",
     "Textures/craftpix_icons/craftpix-net-223231-machine-parts-32x32-pixel-art-icon-pack/1 Icons/Icon13_09.png", "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_29.png", "Textures/craftpix_icons/craftpix-net-101350-drone-32x32-pixel-art-icons/1 Icons/Icon12_16.png", "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_15.png"];
  public static readonly string[] Descriptions =
    ["", Loc.N("Once per trip, sweep at double pickup radius before heading home, even past full cargo."),
     Loc.N("20% chance per pickup to collect one extra gem within 50 units."),
     Loc.N("5% chance for a delivery worth 5x its normal value."),
     Loc.N("Warp back to the last collection endpoint after unloading."),
     Loc.N("Seek the most valuable available gem within 400 units."),
     Loc.N("Collect gems along a narrow return trail, even with full cargo."),
     Loc.N("+50% cargo capacity."),
     Loc.N("+25% movement speed, including the return trip."),
     Loc.N("Use 50% less fuel while moving."),
     Loc.N("+10% pickup radius. Module radius bonuses add together."),
     Loc.N("Each pickup grants +75% speed for 3 seconds. Further pickups refresh the boost."),
     Loc.N("All gems delivered by this ship are worth 50% more."),
     Loc.N("Every fifth pickup adds twice its value as bonus value."),
     Loc.N("Every 8 pickups pull in up to 8 more gems nearby, even past full cargo."),
     Loc.N("Once per trip, heading home sweeps up to 32 nearby gems, even past full cargo."),
     Loc.N("Warp home instantly when heading back."),
     Loc.N("+75% maximum fuel."),
     Loc.N("+60% refueling speed."),
     Loc.N("+60% speed on the return trip."),
     Loc.N("+60% movement speed during the first 4 seconds of each trip."),
     Loc.N("+12.5% pickup radius once cargo is at least half full."),
     Loc.N("+40% movement speed, but 25% less cargo capacity."),
     Loc.N("Double cargo capacity, but 20% slower movement."),
     Loc.N("+80% movement speed while fuel is below 25% of maximum."),
     Loc.N("+20 pickup range, but 15% slower."),
     Loc.N("Each pickup adds 25% bonus value."),
     Loc.N("Each pickup restores 12 fuel, up to maximum fuel."),
     Loc.N("Each nonempty delivery restores 35% of maximum fuel."),
     Loc.N("Gain +5% speed per cargo pickup, up to +100%. Resets each trip."),
     Loc.N("Every 6 pickups grab up to 3 more gems nearby."),
     Loc.N("Each pickup pulls in one more gem nearby, even past full cargo."),
     Loc.N("The first gem of a trip, and each new most valuable one, adds double its value as a bonus."),
     Loc.N("Cargo pickups beyond capacity grant +100% base value."),
     Loc.N("Deliver within 20 seconds of starting a trip for +75% delivery value."),
     Loc.N("Every 8 pickups arc lightning through up to 6 more gems."),
     Loc.N("Each cargo pickup grants +100% base value."),
     Loc.N("Triple movement speed during the first 5 seconds of each trip."),
     Loc.N("The first pickup each trip opens a rift that collects up to 10 more gems."),
     Loc.N("Once per trip, heading home refuels the ship and grabs up to 6 more gems."),
     Loc.N("Every third delivery is worth triple."),
     Loc.N("Once per trip, heading home collapses a field that collects up to 48 more gems, even past full cargo."),
     Loc.N("Once per trip, running out of fuel restores maximum fuel instantly."),
     Loc.N("Every fourth pickup adds five times its value as a bonus."),
     Loc.N("Double speed and +12.5% pickup radius, but twice the fuel use."),
     Loc.N("Triple cargo capacity. Deliveries gain +1% value per cargo gem, up to +200%."),
     Loc.N("Every 5 pickups send a copy of the cargo's value home, keeping the cargo."),
     Loc.N("Every 6 bonus pulls charge a cascade: the next pickup pulls in up to 6 gems."),
     Loc.N("Each gem delivered past full cargo speeds up the next trip, up to +200%."),
     Loc.N("Pickups are worth double during module speed boosts, plus 5% per momentum stack."),
     Loc.N("Every 90 units flown, zap up to 4 nearby gems into cargo, even past full cargo."),
     Loc.N("Every 180 units flown, drop a gravity buoy that pulls up to 8 nearby gems into cargo."),
     Loc.N("Every 120 units flown home, pull up to 3 gems near the last pickup into cargo."),
     Loc.N("Every 60 units flown, unleash a branching lightning storm that collects up to 40 gems."),
     Loc.N("Each delivery sends a ghost along the ship's last route, collecting gems and sending their value home."),
     Loc.N("Become a roaming black hole that swallows nearby gems. Holds 8x cargo, and docking collects up to 96 more."),
     Loc.N("Every 10 pickups, fire a cannon shell at the planet."),
     Loc.N("Each delivery fires rockets at the planet: one per 10 gems delivered, up to 8."),
     Loc.N("Each delivery briefly overcharges the mining laser to triple output."),
     Loc.N("Each delivery detonates the molten scars and craters facing the ship.")];
  // Keep the shared inventory grouped by rarity as the roster grows.
  public static readonly ShipModule[] InventoryOrder = Enum.GetValues<ShipModule>()
    .Where(module => module != ShipModule.None)
    .OrderBy(module => Rarities[(int)module])
    .ThenBy(module => Names[(int)module], StringComparer.Ordinal)
    .ToArray();
}

public sealed partial class ShipyardModules
{
  // Each module is unique and can be assigned to one harvester type at a time.
  [JsonRequired] public ShipModule[] Slots { get; set; } = new ShipModule[ModuleCatalog.Types.Length * ModuleCatalog.MaxSlotsPerType];

  public bool IsAvailable(ShipModule module)
    => module != ShipModule.None && Owned.Contains(module) && !PendingReveals.Contains(module) && !Slots.Contains(module);

  public IEnumerable<ShipModule> GetAvailableModules()
  {
    foreach (var module in ModuleCatalog.InventoryOrder)
      if (IsAvailable(module)) yield return module;
  }

  public bool TryEquip(int typeIndex, int slot, ShipModule module)
  {
    if (typeIndex < 0 || typeIndex >= ModuleCatalog.Types.Length || slot < 0 || slot >= ModuleCatalog.UnlockedSlots
      || !Enum.IsDefined(module)) return false;
    int index = typeIndex * ModuleCatalog.MaxSlotsPerType + slot;
    if (Slots[index] == module) return false;
    if (module != ShipModule.None && !IsAvailable(module)) return false;
    Slots[index] = module;
    return true;
  }

  // Moving equipment swaps occupied slots atomically, preserving unique ownership.
  public bool TryMoveSlot(int source, int destination)
  {
    if (source < 0 || source >= Slots.Length || destination < 0 || destination >= Slots.Length
      || source % ModuleCatalog.MaxSlotsPerType >= ModuleCatalog.UnlockedSlots
      || destination % ModuleCatalog.MaxSlotsPerType >= ModuleCatalog.UnlockedSlots
      || source == destination || Slots[source] == ShipModule.None) return false;
    (Slots[source], Slots[destination]) = (Slots[destination], Slots[source]);
    return true;
  }

  public ulong GetLoadout(Harvester.HarvesterType type)
  {
    int index = Array.IndexOf(ModuleCatalog.Types, type);
    if (index < 0) return 0;
    ulong mask = 0;
    for (int slot = 0; slot < ModuleCatalog.UnlockedSlots; slot++)
      if (Slots[index * ModuleCatalog.MaxSlotsPerType + slot] != ShipModule.None)
        mask |= 1UL << (int)Slots[index * ModuleCatalog.MaxSlotsPerType + slot];
    return mask;
  }

  public bool Has(Harvester.HarvesterType type, ShipModule module)
    => (GetLoadout(type) & (1UL << (int)module)) != 0;

  public void Validate()
  {
    if (Slots == null || Slots.Length != ModuleCatalog.Types.Length * ModuleCatalog.MaxSlotsPerType
      || Slots.Any(module => !Enum.IsDefined(module)))
      throw new InvalidDataException("Invalid shipyard inventory.");
    ValidateSalvage();
    var equipped = Slots.Where(module => module != ShipModule.None).ToArray();
    if (equipped.Any(module => !Owned.Contains(module) || PendingReveals.Contains(module)))
      throw new InvalidDataException("Only revealed, owned modules can be equipped.");
    if (equipped.Distinct().Count() != equipped.Length)
      throw new InvalidDataException("A module cannot be equipped more than once.");
  }
}
