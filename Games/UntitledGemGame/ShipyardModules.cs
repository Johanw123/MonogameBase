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
  CascadeCapacitor, OverflowDrive, KineticRefinery
}

public enum ModuleRarity { Common, Uncommon, Rare, Epic, Legendary }

public static class ModuleCatalog
{
  public const int BaseSlotsPerType = 2;
  public const int MaxSlotsPerType = 4;
  public static int UnlockedSlots => Math.Clamp(UpgradeManager.Instance.UGM.ModuleSlots, BaseSlotsPerType, MaxSlotsPerType);
  public const float TractorChance = 0.2f;
  public const float TractorRadius = 100f;
  public const float JackpotChance = 0.05f;
  public const ulong JackpotMultiplier = 5;
  public const float ProspectorRadius = 400f;
  public const float WakeRadius = 20f;
  public const float CargoMultiplier = 1.5f;
  public const float IonSpeedMultiplier = 1.25f;
  public const float FuelEfficiencyMultiplier = 2f;
  public const float WidebandRangeMultiplier = 1.5f;
  public const float OverdriveDuration = 3f;
  public const float OverdriveSpeedMultiplier = 1.75f;
  public const double RefineryValueMultiplier = 1.5;
  public const int EchoInterval = 5;
  public const int SingularityInterval = 8;
  public const int SingularityGemLimit = 8;
  public const float SingularityRadius = 180f;
  public const int SupernovaGemLimit = 32;
  public const float SupernovaRadius = 240f;
  public const float PulseDuration = 0.6f;
  public static readonly Harvester.HarvesterType[] Types =
    [Harvester.HarvesterType.Harvester, Harvester.HarvesterType.AdvancedHarvester,
     Harvester.HarvesterType.ExpertHarvester, Harvester.HarvesterType.UltimateHarvester,
     Harvester.HarvesterType.PerimeterHarvester];
  public static readonly string[] Names =
    ["Empty slot", "Final Sweep", "Tractor Link", "Jackpot Core", "Return Beacon", "Prospector Lens", "Wake Collector",
     "Cargo Pod", "Ion Booster", "Fuel Recycler", "Wideband Array", "Overdrive Coil", "Crystal Refinery",
     "Echo Chamber", "Singularity Engine", "Supernova Core", "Phase Anchor",
     "Auxiliary Tank", "Quick Coupler", "Homeward Jets", "Launch Capacitor",
     "Cargo Scanner", "Light Frame", "Deep Hold", "Reserve Burn",
     "Vacuum Nozzle", "Gem Polisher", "Salvage Cell", "Dock Battery",
     "Momentum Drive", "Pulse Harvester", "Twin Tractor", "Prism Filter",
     "Overflow Vault", "Courier Seal", "Storm Coil", "Midas Touch",
     "Chrono Drive", "Rift Siphon", "Reactor Bloom", "Echo Vault",
     "Event Horizon", "Phoenix Reactor", "Quantum Forge", "Stellar Engine",
     "Infinity Hold", "Astral Relay",
     "Cascade Capacitor", "Overflow Drive", "Kinetic Refinery"];
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
     ModuleRarity.Legendary, ModuleRarity.Legendary, ModuleRarity.Epic, ModuleRarity.Epic, ModuleRarity.Epic];
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
     "Textures/craftpix_icons/craftpix-net-415479-artifact-32x32-icons-pixel-art-for-cyberpunk/1 Icons/Icon22_18.png"];
  public static readonly string[] Descriptions =
    ["", "Sweep at 3x pickup radius when cargo is full.",
     "20% chance per pickup to collect one extra gem within 100 units.",
     "5% chance for a delivery worth 5x its normal value.",
     "Warp back to the last collection endpoint after unloading.",
     "Seek the most valuable available gem within 400 units.",
     "Collect gems along a narrow return trail, even with full cargo.",
     "+50% cargo capacity.",
     "+25% movement speed, including the return trip.",
     "Use 50% less fuel while moving.",
     "+50% pickup radius.",
     "Each pickup grants +75% speed for 3 seconds. Further pickups refresh the boost.",
     "All gems delivered by this ship are worth 50% more.",
     "Every fifth cargo pickup echoes its base value twice as bonus cargo value, without using extra space. Resets each trip.",
     "Every 8 direct pickups pull up to 8 extra gems within 180 units, even beyond capacity. Bonus pulls do not charge it. Resets each trip.",
     "Once per trip, filling cargo detonates a 240-unit sweep that collects up to 32 extra gems beyond capacity.",
     "Warp home instantly when cargo fills, after collection sweeps.",
     "+75% maximum fuel.",
     "+60% refueling speed.",
     "+60% speed while returning with full cargo.",
     "+60% movement speed during the first 4 seconds of each trip.",
     "+75% pickup radius once cargo is at least half full.",
     "+40% movement speed, but 25% less cargo capacity.",
     "Double cargo capacity, but 20% slower movement.",
     "+80% movement speed while fuel is below 25% of maximum.",
     "Double pickup radius, but 15% slower movement.",
     "Add 25% of each cargo pickup's base value as bonus cargo value, rounded up.",
     "Each pickup restores 12 fuel, up to maximum fuel.",
     "Each nonempty delivery restores 35% of maximum fuel.",
     "Gain +5% speed per cargo pickup, up to +100%. Resets each trip.",
     "Every 6 direct pickups collect up to 3 extra gems within 120 units. Resets each trip.",
     "Each direct pickup pulls one extra gem within 60 units, even beyond cargo capacity.",
     "The first cargo gem and each new base-value record this trip grant twice their base value as bonus cargo value.",
     "Cargo pickups beyond capacity grant +100% base value.",
     "Full-cargo deliveries are worth 75% more.",
     "Every 8 direct pickups arc through up to 6 extra gems, jumping up to 100 units per hop. Resets each trip.",
     "Each cargo pickup grants +100% base value.",
     "Triple movement speed during the first 5 seconds of each trip.",
     "The first direct pickup each trip opens a rift, collecting up to 10 extra gems within 220 units.",
     "Once per trip, full cargo restores maximum fuel and collects up to 6 extra gems within 100 units.",
     "Every third nonempty delivery by this ship is worth triple. Unequipping resets the sequence.",
     "Once per trip, full cargo collapses a 400-unit field, collecting up to 48 extra gems beyond capacity.",
     "Once per trip, running out of fuel restores maximum fuel instantly.",
     "Every fourth cargo pickup grants five times its base value as bonus cargo value without using extra space. Resets each trip.",
     "Double movement speed and pickup radius, but use twice as much fuel per unit traveled.",
     "Triple cargo capacity. Full deliveries gain +1% value per cargo gem, up to +200%.",
     "Every 5 direct pickups transmit a copy of current cargo value as income, keeping the cargo. Resets each trip.",
     "Every 6 bonus-pull pickups charge a cascade: your next direct pickup pulls up to 6 gems within 180 units. Stores up to 24 charges.",
     "Each gem delivered beyond capacity grants +10% speed and +5% pickup radius for the next trip, up to +200% speed and +100% radius.",
     "Cargo pickups grant +100% base value during timed module speed boosts, plus +5% per momentum stack. Bonus value uses no cargo space."];
  // Keep the shared inventory grouped by rarity as the roster grows.
  public static readonly ShipModule[] InventoryOrder = Enum.GetValues<ShipModule>()
    .Where(module => module != ShipModule.None)
    .OrderBy(module => Rarities[(int)module])
    .ThenBy(module => Names[(int)module], StringComparer.Ordinal)
    .ToArray();
}

public sealed class ShipyardModules
{
  // Each module is unique and can be assigned to one harvester type at a time.
  [JsonRequired] public ShipModule[] Slots { get; set; } = new ShipModule[ModuleCatalog.Types.Length * ModuleCatalog.MaxSlotsPerType];

  public bool IsAvailable(ShipModule module)
    => module != ShipModule.None && Enum.IsDefined(module) && !Slots.Contains(module);

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
    var equipped = Slots.Where(module => module != ShipModule.None).ToArray();
    if (equipped.Distinct().Count() != equipped.Length)
      throw new InvalidDataException("A module cannot be equipped more than once.");
  }
}
