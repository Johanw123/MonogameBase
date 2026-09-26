using System;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Entities;

public partial class Harvester
{
  public float ModuleTripAge { get; private set; }
  public ulong DirectModulePickups { get; private set; }
  public int MomentumPickups { get; private set; }
  public int CascadeCharges { get; private set; }
  public int OverflowDriveStacks { get; private set; }
  private int nextOverflowDriveStacks;
  public Vector2[] StormArcPoints;
  public int StormArcCount;
  public float StormArcRemaining, RelayFlashRemaining;
  public Vector2 RelayOrigin;
  private int forgePickups;
  private uint prismRecord;
  private int echoVaultDeliveries;
  private bool reactorBloomConsumed, eventHorizonConsumed, phoenixConsumed;

  public float ModuleMaxFuel => BaseMaxFuel * BaseStats.GetHarvesterMaxFuelMultiplier(this);

  private void ResetAdditionalModuleTrip(ulong previousLoadout)
  {
    ModuleTripAge = 0f;
    DirectModulePickups = 0;
    MomentumPickups = 0;
    CascadeCharges = 0;
    ulong overflowMask = 1UL << (int)ShipModule.OverflowDrive;
    OverflowDriveStacks = (previousLoadout & moduleLoadout & overflowMask) != 0 ? nextOverflowDriveStacks : 0;
    nextOverflowDriveStacks = 0;
    forgePickups = 0;
    prismRecord = 0;
    reactorBloomConsumed = eventHorizonConsumed = phoenixConsumed = false;
    ulong vaultMask = 1UL << (int)ShipModule.EchoVault;
    if ((previousLoadout & moduleLoadout & vaultMask) == 0) echoVaultDeliveries = 0;
  }

  public double AdditionalModuleCapacityMultiplier()
  {
    double multiplier = 1;
    if (HasModule(ShipModule.LightFrame)) multiplier *= 0.75;
    if (HasModule(ShipModule.DeepHold)) multiplier *= 2;
    if (HasModule(ShipModule.InfinityHold)) multiplier *= 3;
    return multiplier;
  }

  public float AdditionalModuleRangeMultiplier()
  {
    float multiplier = 1f;
    if (HasModule(ShipModule.CargoScanner) && (ulong)CarryingGemCount * 2 >= (ulong)BaseStats.GetHarvesterCapacity(this))
      multiplier *= 1.75f;
    if (HasModule(ShipModule.VacuumNozzle)) multiplier *= 2f;
    if (HasModule(ShipModule.StellarEngine)) multiplier *= 2f;
    if (HasModule(ShipModule.OverflowDrive)) multiplier *= 1f + OverflowDriveStacks * 0.05f;
    return multiplier;
  }

  public float AdditionalModuleSpeedMultiplier()
  {
    float multiplier = 1f;
    if (HasModule(ShipModule.HomewardJets) && ReturningToHomebase) multiplier *= 1.6f;
    if (HasModule(ShipModule.LaunchCapacitor) && ModuleTripAge < 4f) multiplier *= 1.6f;
    if (HasModule(ShipModule.LightFrame)) multiplier *= 1.4f;
    if (HasModule(ShipModule.DeepHold)) multiplier *= 0.8f;
    if (HasModule(ShipModule.ReserveBurn) && Fuel < ModuleMaxFuel * 0.25f) multiplier *= 1.8f;
    if (HasModule(ShipModule.VacuumNozzle)) multiplier *= 0.85f;
    if (HasModule(ShipModule.MomentumDrive)) multiplier *= 1f + MomentumPickups * 0.05f;
    if (HasModule(ShipModule.ChronoDrive) && ModuleTripAge < 5f) multiplier *= 3f;
    if (HasModule(ShipModule.StellarEngine)) multiplier *= 2f;
    if (HasModule(ShipModule.OverflowDrive)) multiplier *= 1f + OverflowDriveStacks * 0.1f;
    return multiplier;
  }

  private void ApplyAdditionalCargoModules(uint baseValue)
  {
    ulong bonus = 0;
    if (HasModule(ShipModule.GemPolisher)) bonus += ((ulong)baseValue + 3) / 4;
    if (HasModule(ShipModule.MidasTouch)) bonus += baseValue;
    if (HasModule(ShipModule.KineticRefinery))
    {
      bool boosted = (HasModule(ShipModule.LaunchCapacitor) && ModuleTripAge < 4f)
        || (HasModule(ShipModule.ChronoDrive) && ModuleTripAge < 5f)
        || (HasModule(ShipModule.OverdriveCoil) && OverdriveTimeRemaining > 0f);
      if (boosted) bonus += baseValue;
      if (HasModule(ShipModule.MomentumDrive)) bonus += ((ulong)baseValue * (uint)MomentumPickups + 19) / 20;
    }
    if (HasModule(ShipModule.PrismFilter) && baseValue > prismRecord)
    {
      prismRecord = baseValue;
      bonus += (ulong)baseValue * 2;
      ShowModulePulse(BoundingCircle.Center, 50f, Color.LimeGreen);
    }
    if (HasModule(ShipModule.OverflowVault) && CarryingGemCount >= BaseStats.GetHarvesterCapacity(this))
      bonus += baseValue;
    if (HasModule(ShipModule.QuantumForge) && ++forgePickups >= 4)
    {
      forgePickups = 0;
      bonus += (ulong)baseValue * 5;
      ShowModulePulse(BoundingCircle.Center, 70f, Color.Gold);
    }
    CarryingGemBaseValue = PrestigeProgression.AddSaturating(CarryingGemBaseValue, bonus);
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
    if (direct) ++DirectModulePickups;
    else if (HasModule(ShipModule.CascadeCapacitor)) CascadeCharges = Math.Min(24, CascadeCharges + 1);
    if (HasModule(ShipModule.SalvageCell)) RestoreModuleFuel(12f);
  }

  public bool TryConsumeCascade()
  {
    if (!HasModule(ShipModule.CascadeCapacitor) || CascadeCharges < 6) return false;
    CascadeCharges -= 6;
    return true;
  }

  public bool TryRestorePhoenixFuel(float requiredFuel)
  {
    if (phoenixConsumed || !HasModule(ShipModule.PhoenixReactor) || Fuel > requiredFuel) return false;
    phoenixConsumed = true;
    RestoreModuleFuel(ModuleMaxFuel);
    ShowModulePulse(BoundingCircle.Center, 100f, Color.OrangeRed);
    return true;
  }

  public bool TryBeginReactorBloom()
  {
    if (reactorBloomConsumed || MarkedForDestroy || !HasModule(ShipModule.ReactorBloom) || !ReturningToHomebase) return false;
    reactorBloomConsumed = true;
    RestoreModuleFuel(ModuleMaxFuel);
    return true;
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
      multiplier *= 1.75;
    if (HasModule(ShipModule.InfinityHold)) multiplier *= 1 + Math.Min(2.0, CarryingGemCount * 0.01);
    if (HasModule(ShipModule.EchoVault) && ++echoVaultDeliveries >= 3)
    {
      echoVaultDeliveries = 0;
      multiplier *= 3;
      ShowModulePulse(BoundingCircle.Center, 90f, Color.MediumPurple);
    }
    if (HasModule(ShipModule.DockBattery)) RestoreModuleFuel(ModuleMaxFuel * 0.35f);
    if (multiplier == 1) return value;
    double result = Math.Ceiling(value * multiplier);
    return result >= ulong.MaxValue ? ulong.MaxValue : (ulong)result;
  }
}
