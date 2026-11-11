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
  CargoPod, IonBooster, LongHaulLedger, WidebandArray, OverdriveCoil, CrystalRefinery,
  EchoChamber, SingularityEngine, SupernovaCore, PhaseAnchor,
  AuxiliaryTank, SoloRig, HomewardJets, LaunchCapacitor, CargoScanner, LightFrame, DeepHold, FuelInjector,
  VacuumNozzle, GemPolisher, SalvageCell, FleetUplink, MomentumDrive, PulseHarvester, TwinTractor, PrismFilter,
  OverflowVault, CourierSeal, StormCoil, MidasTouch, ChronoDrive, RiftSiphon, TowWinch, EchoVault, EventHorizon,
  PhoenixReactor, QuantumForge, StellarEngine, InfinityHold, AstralRelay,
  CascadeCapacitor, OverflowDrive, KineticRefinery, ArcEmitter, GravityBuoy, TuningKit, ThunderGod, TimeHeist, WorldEater,
  GunPod, RocketRack, SiegeCannon, DetonatorCharge
}

public enum ModuleRarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

public static class ModuleCatalog
{
  public const int BaseSlotsPerType = 2;
  public const int MaxSlotsPerType = 4;
  public static int UnlockedSlots => Math.Clamp(UpgradeManager.Instance.UGM.ModuleSlots, BaseSlotsPerType, MaxSlotsPerType);
  public const float TractorChance = 0.3f;
  public const float TractorRadius = 60f;
  public const float JackpotChance = 0.1f;
  public const ulong JackpotMultiplier = 8;
  public const float ProspectorRadius = 400f;
  public const float WakeRadius = 30f;
  public const float CargoBonus = 0.5f;
  public const float IonSpeedBonus = 0.25f;
  public const float WidebandRangeBonus = 0.2f;
  public const float VacuumRangeBonus = 0.3f;
  public const float OverdriveDuration = 3f;
  public const float OverdriveSpeedMultiplier = 1.75f;
  public const double RefineryValueMultiplier = 1.5;
  public const float ScannerValueBonus = 0.12f;
  public const int EchoInterval = 6;
  public const int EchoGems = 2;
  public const int SingularityInterval = 8;
  public const int SingularityGemLimit = 8;
  public const float SingularityRadius = 90f;
  public const int SupernovaGemLimit = 32;
  public const float SupernovaRadius = 120f;
  public const double SupernovaPressure = 2.5;
  public const float CourierDeadlineSeconds = 6f;
  public const double CourierBonus = 0.6;
  public const double LedgerBonusPerSecond = 0.08;
  public const double LedgerMaxBonus = 0.6;
  public const double SoloRigBonus = 0.4;
  public const double PrismBonusPerModule = 0.15;
  public const double UplinkBonusPerType = 0.07;
  public const float CascadeChance = 0.15f;
  public const int CascadeGems = 3;
  public const int CascadeMaxGems = 30;
  public const double PhoenixMultiplier = 2.5;
  public const double HeistCopyMultiplier = 2;
  public const double WorldEaterMultiplier = 3;
  public const float PulseDuration = 0.6f;
  // Weapon modules (effects in GameScreen.TalentCombos.cs).
  public const int GunPodInterval = 40;
  public const int RocketRackGemsPerRocket = 3;
  public const int RocketRackMaxRockets = 60;
  public const int SiegeGemsPerPower = 10;
  public const int SiegeMaxPower = 40;
  public static readonly Harvester.HarvesterType[] Types =
    [Harvester.HarvesterType.Harvester, Harvester.HarvesterType.AdvancedHarvester,
     Harvester.HarvesterType.ExpertHarvester, Harvester.HarvesterType.UltimateHarvester,
     Harvester.HarvesterType.PerimeterHarvester];
  // Names and Descriptions hold English marked for the string table (names also identify
  // modules in capture scripts and sort the inventory); translate them where shown.
  public static readonly string[] Names =
    [Loc.N("Empty slot"), Loc.N("Final Sweep"), Loc.N("Tractor Link"), Loc.N("Jackpot Core"), Loc.N("Return Beacon"), Loc.N("Prospector Lens"), Loc.N("Wake Collector"),
     Loc.N("Cargo Pod"), Loc.N("Ion Booster"), Loc.N("Long Haul Ledger"), Loc.N("Wideband Array"), Loc.N("Overdrive Coil"), Loc.N("Crystal Refinery"),
     Loc.N("Echo Chamber"), Loc.N("Singularity Engine"), Loc.N("Supernova Core"), Loc.N("Phase Anchor"),
     Loc.N("Auxiliary Tank"), Loc.N("Solo Rig"), Loc.N("Homeward Jets"), Loc.N("Launch Capacitor"),
     Loc.N("Cargo Scanner"), Loc.N("Light Frame"), Loc.N("Deep Hold"), Loc.N("Fuel Injector"),
     Loc.N("Vacuum Nozzle"), Loc.N("Gem Polisher"), Loc.N("Salvage Cell"), Loc.N("Fleet Uplink"),
     Loc.N("Momentum Drive"), Loc.N("Pulse Harvester"), Loc.N("Twin Tractor"), Loc.N("Prism Filter"),
     Loc.N("Overflow Vault"), Loc.N("Courier Seal"), Loc.N("Storm Coil"), Loc.N("Midas Touch"),
     Loc.N("Chrono Drive"), Loc.N("Rift Siphon"), Loc.N("Tow Winch"), Loc.N("Echo Vault"),
     Loc.N("Event Horizon"), Loc.N("Phoenix Reactor"), Loc.N("Quantum Forge"), Loc.N("Stellar Engine"),
     Loc.N("Infinity Hold"), Loc.N("Astral Relay"),
     Loc.N("Cascade Capacitor"), Loc.N("Overflow Drive"), Loc.N("Kinetic Refinery"), Loc.N("Arc Emitter"), Loc.N("Gravity Buoy"), Loc.N("Tuning Kit"), Loc.N("Thunder God"), Loc.N("Time Heist"), Loc.N("World Eater"),
     Loc.N("Gun Pod"), Loc.N("Rocket Rack"), Loc.N("Siege Cannon"), Loc.N("Detonator Charge")];
  public static readonly ModuleRarity[] Rarities =
    [ModuleRarity.Common,
     ModuleRarity.Rare, // FinalSweep
     ModuleRarity.Common, // TractorLink
     ModuleRarity.Epic, // JackpotCore
     ModuleRarity.Rare, // ReturnBeacon
     ModuleRarity.Uncommon, // ProspectorLens
     ModuleRarity.Uncommon, // WakeCollector
     ModuleRarity.Common, // CargoPod
     ModuleRarity.Common, // IonBooster
     ModuleRarity.Uncommon, // LongHaulLedger
     ModuleRarity.Common, // WidebandArray
     ModuleRarity.Rare, // OverdriveCoil
     ModuleRarity.Rare, // CrystalRefinery
     ModuleRarity.Epic, // EchoChamber
     ModuleRarity.Epic, // SingularityEngine
     ModuleRarity.Legendary, // SupernovaCore
     ModuleRarity.Rare, // PhaseAnchor
     ModuleRarity.Common, // AuxiliaryTank
     ModuleRarity.Uncommon, // SoloRig
     ModuleRarity.Common, // HomewardJets
     ModuleRarity.Common, // LaunchCapacitor
     ModuleRarity.Common, // CargoScanner
     ModuleRarity.Common, // LightFrame
     ModuleRarity.Uncommon, // DeepHold
     ModuleRarity.Uncommon, // FuelInjector
     ModuleRarity.Uncommon, // VacuumNozzle
     ModuleRarity.Uncommon, // GemPolisher
     ModuleRarity.Common, // SalvageCell
     ModuleRarity.Uncommon, // FleetUplink
     ModuleRarity.Rare, // MomentumDrive
     ModuleRarity.Common, // PulseHarvester
     ModuleRarity.Rare, // TwinTractor
     ModuleRarity.Rare, // PrismFilter
     ModuleRarity.Rare, // OverflowVault
     ModuleRarity.Rare, // CourierSeal
     ModuleRarity.Epic, // StormCoil
     ModuleRarity.Epic, // MidasTouch
     ModuleRarity.Rare, // ChronoDrive
     ModuleRarity.Epic, // RiftSiphon
     ModuleRarity.Epic, // TowWinch
     ModuleRarity.Epic, // EchoVault
     ModuleRarity.Legendary, // EventHorizon
     ModuleRarity.Legendary, // PhoenixReactor
     ModuleRarity.Legendary, // QuantumForge
     ModuleRarity.Epic, // StellarEngine
     ModuleRarity.Legendary, // InfinityHold
     ModuleRarity.Legendary, // AstralRelay
     ModuleRarity.Epic, // CascadeCapacitor
     ModuleRarity.Rare, // OverflowDrive
     ModuleRarity.Epic, // KineticRefinery
     ModuleRarity.Rare, // ArcEmitter
     ModuleRarity.Epic, // GravityBuoy
     ModuleRarity.Rare, // TuningKit
     ModuleRarity.Mythic, // ThunderGod
     ModuleRarity.Mythic, // TimeHeist
     ModuleRarity.Mythic, // WorldEater
     ModuleRarity.Rare, // GunPod
     ModuleRarity.Epic, // RocketRack
     ModuleRarity.Legendary, // SiegeCannon
     ModuleRarity.Uncommon // DetonatorCharge
    ];
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
    ["",
     Loc.N("Once per trip, sweep at double pickup radius before heading home, even past full cargo."),
     Loc.N("30% chance per pickup to pull in one more gem within 60 units."),
     Loc.N("10% chance for a delivery worth 8x its normal value."),
     Loc.N("Warp back to the last collection endpoint after unloading."),
     Loc.N("Seek the most valuable available gem within 400 units."),
     Loc.N("Collect gems along a wide return trail, even with full cargo."),
     Loc.N("+50% cargo capacity."),
     Loc.N("+25% movement speed, including the return trip."),
     Loc.N("Deliveries gain +8% value per second the trip lasted, up to +60%."),
     Loc.N("+20% pickup radius. Module radius bonuses add together."),
     Loc.N("Each pickup grants +75% speed for 3 seconds. Further pickups refresh the boost."),
     Loc.N("All gems delivered by this ship are worth 50% more."),
     Loc.N("Gems collected by module effects are worth triple. Every 6 pickups pull in 2 gems nearby."),
     Loc.N("Every 8 pickups pull in up to 8 more gems nearby, worth double, even past full cargo."),
     Loc.N("Gems gain up to +250% value as the hold fills. Heading home, a blast collects up to 32 nearby gems."),
     Loc.N("Warp home instantly when heading back, then warp back out after unloading."),
     Loc.N("+100% maximum fuel and +60% refueling speed."),
     Loc.N("+40% delivery value while this is the ship's only module."),
     Loc.N("+60% speed on the return trip."),
     Loc.N("+60% movement speed during the first 4 seconds of each trip."),
     Loc.N("Deliveries are worth 12% more."),
     Loc.N("+40% movement speed, but 25% less cargo capacity."),
     Loc.N("Double cargo capacity, but 20% slower movement."),
     Loc.N("+35% movement speed while fuel is above half."),
     Loc.N("+30% pickup radius. Module pulls, pulses and lightning collect one more gem."),
     Loc.N("Each pickup adds 25% bonus value."),
     Loc.N("Each pickup restores 12 fuel, up to maximum fuel."),
     Loc.N("+7% delivery value for each ship type with a module fitted."),
     Loc.N("Gain +5% speed per cargo pickup, up to +100%. Resets each trip."),
     Loc.N("Every 6 pickups grab up to 4 more gems nearby."),
     Loc.N("Each pickup pulls in one more gem nearby, worth 50% more, even past full cargo."),
     Loc.N("+15% delivery value for each module fitted to this ship."),
     Loc.N("Carry 25% past full cargo. Gems past full cargo are worth double."),
     Loc.N("Deliver within 6 seconds of starting a trip for +60% delivery value."),
     Loc.N("Every 8 pickups arc lightning through up to 6 more gems, worth double."),
     Loc.N("Each cargo pickup grants +75% base value."),
     Loc.N("Triple movement speed during the first 5 seconds of each trip."),
     Loc.N("The first pickup each trip opens a rift that collects a third of cargo capacity, worth triple."),
     Loc.N("Tow 25% of cargo capacity home behind the ship at 4x value. With Tractor Tails, tow three times as many."),
     Loc.N("Every third delivery is worth triple."),
     Loc.N("Once per trip, heading home collapses a field that collects half of cargo capacity at +150% value."),
     Loc.N("Once per trip, a full hold ignites: it is beamed home at 2.5x value and the ship refuels and keeps collecting."),
     Loc.N("Every fourth pickup adds five times its value as a bonus."),
     Loc.N("Double speed and +25% pickup radius, but twice the fuel use."),
     Loc.N("Double cargo capacity. Deliveries gain +1% value per cargo gem, up to +75%."),
     Loc.N("At half a hold, and again at a full one, beam a copy of the cargo's value home."),
     Loc.N("15% chance per pickup to pull in 3 nearby gems worth double. Each of those can cascade again."),
     Loc.N("Each gem delivered past full cargo speeds up the next trip, up to +200%."),
     Loc.N("+15% movement speed. Pickups gain value equal to this ship's module speed bonus, up to +150%."),
     Loc.N("Every 90 units flown, zap up to 5 nearby gems into cargo, worth triple."),
     Loc.N("Every 150 units flown, drop a gravity buoy that pulls up to 24 nearby gems into cargo, worth triple."),
     Loc.N("Common modules on this ship are twice as strong."),
     Loc.N("Every 60 units flown, unleash a branching lightning storm that collects up to 40 gems, worth double."),
     Loc.N("Each delivery sends a ghost along the last route: it collects gems, then delivers the cargo again at double value."),
     Loc.N("A black hole trails the ship, swallowing one gem for each gem the ship picks up and sending it home at triple value."),
     Loc.N("Every 40 pickups, fire a cannon shell at the planet."),
     Loc.N("Each delivery fires rockets at the planet: one per 3 gems delivered, up to 60."),
     Loc.N("Each delivery fires a siege shell at the planet. Bigger loads hit harder."),
     Loc.N("Each delivery detonates every molten scar and crater on the planet.")];
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
