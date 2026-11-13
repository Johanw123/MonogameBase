using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Entities;

public partial class Harvester
{
  public float ModuleTripAge { get; private set; }
  public ulong DirectModulePickups { get; private set; }
  public int MomentumPickups { get; private set; }
  public int OverflowDriveStacks { get; private set; }
  private int nextOverflowDriveStacks;
  public Vector2[] StormArcPoints;
  public int StormArcCount;
  public float StormArcRemaining, RelayFlashRemaining;
  // Picks the shape of this strike's bolts (ModuleEffects.fx).
  public float StormArcSeed;
  public Vector2 RelayOrigin;
  public float ArcTravelCharge, BuoyTravelCharge;
  public float BuoyRemaining;
  public Vector2 BuoyPosition;

  public void ChargeTravelModules(float distance)
  {
    if (MarkedForDestroy || distance <= 0f) return;
    if (IsShuttle) distance /= PrestigeTalentEffects.ShuttlesPerShip;
    if (HasModule(ShipModule.ThunderGod)) ThunderTravelCharge += distance;
    if (HasModule(ShipModule.ArcEmitter)) ArcTravelCharge += distance;
    if (HasModule(ShipModule.GravityBuoy)) BuoyTravelCharge += distance;
  }

  private int forgePickups;
  private int echoVaultDeliveries;
  private int relayCopies;
  private bool eventHorizonConsumed, phoenixConsumed;

  public float ModuleMaxFuel => BaseMaxFuel * BaseStats.GetHarvesterMaxFuelMultiplier(this);
  public int FittedModules => System.Numerics.BitOperations.PopCount(moduleLoadout);

  // Overflow Vault lets the hold run past full before the ship turns home.
  public int ModuleReturnCapacity
  {
    get
    {
      int capacity = BaseStats.GetHarvesterCapacity(this);
      return HasModule(ShipModule.OverflowVault)
        ? (int)Math.Min(int.MaxValue, capacity + Math.Max(1L, (capacity + 3L) / 4)) : capacity;
    }
  }

  // Tuning Kit doubles the bonus of every Common module on the ship.
  public float Tuned(float bonus) => HasModule(ShipModule.TuningKit) ? bonus * 2f : bonus;

  private void ResetAdditionalModuleTrip(ulong previousLoadout)
  {
    ResetMythicTrip(previousLoadout);
    ModuleTripAge = 0f;
    ArcTravelCharge = BuoyTravelCharge = 0f;
    // A buoy already dropped still fires after the ship has unloaded.
    if (!HasModule(ShipModule.GravityBuoy)) BuoyRemaining = 0f;
    DirectModulePickups = 0;
    MomentumPickups = 0;
    ulong overflowMask = 1UL << (int)ShipModule.OverflowDrive;
    OverflowDriveStacks = (previousLoadout & moduleLoadout & overflowMask) != 0 ? nextOverflowDriveStacks : 0;
    nextOverflowDriveStacks = 0;
    forgePickups = 0;
    relayCopies = 0;
    eventHorizonConsumed = phoenixConsumed = dropPodLaunched = false;
    ulong vaultMask = 1UL << (int)ShipModule.EchoVault;
    if ((previousLoadout & moduleLoadout & vaultMask) == 0) echoVaultDeliveries = 0;
  }

  public double AdditionalModuleCapacityMultiplier()
  {
    double multiplier = 1;
    if (HasModule(ShipModule.LightFrame)) multiplier *= 0.75;
    if (HasModule(ShipModule.DeepHold)) multiplier *= 2;
    if (HasModule(ShipModule.InfinityHold)) multiplier *= 2;
    return multiplier;
  }

  public float AdditionalModuleRangeMultiplier()
  {
    float multiplier = 1f;
    // Equipment contributes to one additive radius bonus rather than multiplying other modules.
    if (HasModule(ShipModule.WidebandArray)) multiplier += Tuned(ModuleCatalog.WidebandRangeBonus);
    if (HasModule(ShipModule.VacuumNozzle)) multiplier += ModuleCatalog.VacuumRangeBonus;
    if (HasModule(ShipModule.StellarEngine)) multiplier += 0.25f;
    if (HasModule(ShipModule.OverflowDrive)) multiplier += OverflowDriveStacks * 0.00625f;
    return multiplier;
  }

  // Every module's speed bonus multiplied together (Kinetic Refinery pays out on it).
  public float ModuleSpeedMultiplier()
  {
    float multiplier = 1f;
    if (HasModule(ShipModule.IonBooster)) multiplier *= 1f + Tuned(ModuleCatalog.IonSpeedBonus);
    if (HasModule(ShipModule.OverdriveCoil) && OverdriveTimeRemaining > 0f)
      multiplier *= ModuleCatalog.OverdriveSpeedMultiplier;
    if (HasModule(ShipModule.HomewardJets) && ReturningToHomebase) multiplier *= 1f + Tuned(0.6f);
    if (HasModule(ShipModule.LaunchCapacitor) && ModuleTripAge < 4f) multiplier *= 1f + Tuned(0.6f);
    if (HasModule(ShipModule.LightFrame)) multiplier *= 1f + Tuned(0.4f);
    if (HasModule(ShipModule.DeepHold)) multiplier *= 0.8f;
    if (HasModule(ShipModule.FuelInjector) && Fuel >= ModuleMaxFuel * 0.5f) multiplier *= 1.35f;
    if (HasModule(ShipModule.MomentumDrive)) multiplier *= 1f + MomentumPickups * 0.05f;
    if (HasModule(ShipModule.ChronoDrive) && ModuleTripAge < 5f) multiplier *= 3f;
    if (HasModule(ShipModule.StellarEngine)) multiplier *= 2f;
    if (HasModule(ShipModule.OverflowDrive)) multiplier *= 1f + OverflowDriveStacks * 0.1f;
    if (HasModule(ShipModule.KineticRefinery)) multiplier *= 1.15f;
    return multiplier;
  }

  // Extra value on a gem that a module effect collected, as a share of its base value.
  // Echo Chamber adds to a module's own bonus instead of multiplying it.
  public double ModuleGemBonus(GemCollectionMethod method)
  {
    double bonus = method switch
    {
      GemCollectionMethod.TwinTractor => 0.5,
      GemCollectionMethod.SingularityEngine or GemCollectionMethod.StormCoil or GemCollectionMethod.ThunderGod
        or GemCollectionMethod.CascadeCapacitor => 1,
      GemCollectionMethod.EventHorizon => 1.5,
      GemCollectionMethod.ArcEmitter or GemCollectionMethod.RiftSiphon or GemCollectionMethod.GravityBuoy => 2,
      GemCollectionMethod.WorldEater => ModuleCatalog.WorldEaterMultiplier - 1,
      GemCollectionMethod.TractorTails => HasModule(ShipModule.TowWinch) ? 3 : 0,
      _ => 0,
    };
    if (method >= GemCollectionMethod.FinalSweep && HasModule(ShipModule.EchoChamber)) bonus += 2;
    return bonus;
  }

  public ulong AddModuleGemBonus(GemCollectionMethod method, uint baseValue)
  {
    double share = ModuleGemBonus(method);
    if (share <= 0) return 0;
    ulong bonus = (ulong)Math.Ceiling(baseValue * share);
    CarryingGemBaseValue = PrestigeProgression.AddSaturating(CarryingGemBaseValue, bonus);
    return bonus;
  }

  private void ApplyAdditionalCargoModules(uint baseValue)
  {
    double share = 0;
    int capacity = BaseStats.GetHarvesterCapacity(this);
    if (HasModule(ShipModule.GemPolisher)) share += 0.25;
    if (HasModule(ShipModule.MidasTouch)) share += 0.75;
    if (HasModule(ShipModule.KineticRefinery)) share += Math.Min(1.5, ModuleSpeedMultiplier() - 1.0);
    if (HasModule(ShipModule.OverflowVault) && CarryingGemCount >= capacity) share += 1;
    if (HasModule(ShipModule.SupernovaCore))
      share += ModuleCatalog.SupernovaPressure * Math.Min(1.0, (CarryingGemCount + 1.0) / capacity);
    if (HasModule(ShipModule.QuantumForge) && ++forgePickups >= 4)
    {
      forgePickups = 0;
      share += 5;
      ShowModulePulse(BoundingCircle.Center, 70f, Color.Gold);
    }
    if (share > 0)
      CarryingGemBaseValue = PrestigeProgression.AddSaturating(CarryingGemBaseValue, (ulong)Math.Ceiling(baseValue * share));
    if (HasModule(ShipModule.MomentumDrive)) MomentumPickups = Math.Min(20, MomentumPickups + 1);
  }

  public void RestoreModuleFuel(float amount)
  {
    // Ordinary refueling can roll above nominal maximum; a bonus never removes that fuel.
    Fuel = Math.Max(Fuel, Math.Min(ModuleMaxFuel, Fuel + amount));
    if (Fuel > 0f && CurrentState == HarvesterState.OutOfFuel) CurrentState = HarvesterState.Collecting;
  }

  public void RegisterModulePickup(bool direct)
  {
    if (direct)
    {
      ++DirectModulePickups;
      if (HasModule(ShipModule.WorldEater)) WorldEaterHunger = Math.Min(256, WorldEaterHunger + 1);
    }
    if (HasModule(ShipModule.SalvageCell)) RestoreModuleFuel(Tuned(12f));
  }

  // Astral Relay beams a copy of the cargo home at half a hold and again at a full one.
  public bool TryBeginRelayCopy()
  {
    if (relayCopies >= 2 || !HasModule(ShipModule.AstralRelay)) return false;
    long capacity = ModuleReturnCapacity;
    if (CarryingGemCount * 2L < capacity * (relayCopies + 1L)) return false;
    ++relayCopies;
    return true;
  }

  // Phoenix Reactor: the first full hold of a trip is sent home and the ship keeps going.
  public bool TryIgnitePhoenix()
  {
    if (phoenixConsumed || MarkedForDestroy || IsShuttle || !HasModule(ShipModule.PhoenixReactor)
      || CarryingGemCount == 0 || CarryingGemCount < ModuleReturnCapacity) return false;
    phoenixConsumed = true;
    return true;
  }

  public void FinishPhoenix()
  {
    CarryingGemCount = 0;
    CarryingGemBaseValue = 0;
    Credits.Clear();
    relayCopies = 0;
    forgePickups = 0;
    // The trip goes on: where it ends is decided by the second hold.
    CollectionEndpoint = null;
    RestoreModuleFuel(ModuleMaxFuel);
    ShowModulePulse(BoundingCircle.Center, 110f, Color.OrangeRed);
  }

  public bool TryBeginEventHorizon()
  {
    if (eventHorizonConsumed || MarkedForDestroy || !HasModule(ShipModule.EventHorizon) || !ReturningToHomebase) return false;
    eventHorizonConsumed = true;
    return true;
  }

  public ulong ApplyAdditionalDeliveryModules(ulong value)
  {
    if (CarryingGemCount == 0) return value;
    if (HasModule(ShipModule.OverflowDrive))
      nextOverflowDriveStacks = (int)Math.Min(20L, Math.Max(0L, (long)CarryingGemCount - BaseStats.GetHarvesterCapacity(this)));
    double multiplier = 1;
    if (HasModule(ShipModule.CourierSeal) && ModuleTripAge <= ModuleCatalog.CourierDeadlineSeconds)
      multiplier *= 1 + ModuleCatalog.CourierBonus;
    if (HasModule(ShipModule.LongHaulLedger))
      multiplier *= 1 + Math.Min(ModuleCatalog.LedgerMaxBonus, ModuleTripAge * ModuleCatalog.LedgerBonusPerSecond);
    if (HasModule(ShipModule.InfinityHold)) multiplier *= 1 + Math.Min(0.75, CarryingGemCount * 0.01);
    if (HasModule(ShipModule.CargoScanner)) multiplier *= 1 + Tuned(ModuleCatalog.ScannerValueBonus);
    if (HasModule(ShipModule.SoloRig) && FittedModules == 1) multiplier *= 1 + ModuleCatalog.SoloRigBonus;
    if (HasModule(ShipModule.PrismFilter)) multiplier *= 1 + ModuleCatalog.PrismBonusPerModule * FittedModules;
    if (HasModule(ShipModule.FleetUplink))
    {
      int types = 0;
      foreach (var type in ModuleCatalog.Types)
        if (UpgradeManager.Instance.Modules.GetLoadout(type) != 0) ++types;
      multiplier *= 1 + ModuleCatalog.UplinkBonusPerType * types;
    }
    if (HasModule(ShipModule.EchoVault) && ++echoVaultDeliveries >= 3)
    {
      echoVaultDeliveries = 0;
      multiplier *= 3;
      ShowModulePulse(BoundingCircle.Center, 90f, Color.MediumPurple);
    }
    if (multiplier == 1) return value;
    double result = Math.Ceiling(value * multiplier);
    return result >= ulong.MaxValue ? ulong.MaxValue : (ulong)result;
  }
}
