using System;
using Microsoft.Xna.Framework;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Entities;

// Mothership Protocol's fleet roles. A shuttle is an ordinary ship whose home is its carrier;
// a carrier is one more ship of the type that never scoops gems itself and flies the hold
// back to the home base. Behaviour lives in Systems/HarvesterSystem.Carriers.cs.
public partial class Harvester
{
  public bool IsCarrier;
  public int ShuttleCount;
  public float StationSeconds;
  public bool ShuttleUnloading;
  public Harvester Carrier;

  public bool IsShuttle => Carrier != null;

  // Empty shuttles escort a carrier that is heading home; loaded ones bring their load aboard first.
  public bool ShuttleRecalled => Carrier?.ReturningToHomebase == true;

  // How far a carrier's shuttles range from it.
  public float ShuttleLeash => MathF.Max(PrestigeTalentEffects.MinShuttleLeash,
    BaseStats.GetHarvesterSpeed(Carrier ?? this) * PrestigeTalentEffects.ShuttleLeashCarrierSeconds);

  // Where this ship unloads.
  public Vector2 DockPosition => Carrier?.BoundingCircle.Center ?? UntitledGemGameGameScreen.HomeBasePos;

  // A swarm of black holes would swallow the field; the group has one, on the carrier.
  private ulong RoleExcludedModules => IsShuttle ? 1UL << (int)ShipModule.WorldEater : 0UL;

  // How a role changes the drawn hull; gameplay keeps the ship's own size.
  public float HullScale => IsCarrier ? PrestigeTalentEffects.CarrierHullScale
    : IsShuttle ? PrestigeTalentEffects.ShuttleHullScale : 1f;

  public float RoleSpeedMultiplier => IsCarrier ? PrestigeTalentEffects.CarrierSpeedMultiplier : 1f;

  public double RoleCapacityMultiplier => IsCarrier ? Math.Max(1, ShuttleCount)
    : IsShuttle ? 1.0 / PrestigeTalentEffects.ShuttlesPerShip : 1.0;

  // How full a carrier's hold is, for its gauge.
  public float HoldFill => Math.Min(1f, CarryingGemCount / (float)Math.Max(1, ModuleReturnCapacity));

  // Reasons to head home before the hold is full.
  private bool CalledHomeEarly => CarryingGemCount > 0
    && (IsCarrier ? ModuleTripAge >= PrestigeTalentEffects.CarrierMaxTripSeconds : ShuttleRecalled || ShuttleUnloading);

  public void JoinCarrier(Harvester carrier)
  {
    if (Carrier == carrier) return;
    Carrier = carrier;
    DepartingHomeBase = false;
    TargetScreenPosition = null;
    BeginModuleTrip();
    // Shuttles never burn fuel, so a ship stranded before joining would never recover.
    if (CurrentState != HarvesterState.Collecting) EmergencyRefuel();
  }

  public void LeaveCarrier()
  {
    Carrier = null;
    TargetScreenPosition = null;
    BeginModuleTrip();
  }

  public void AddToHold(uint gems, ulong baseValue, CargoCredits credits = null)
  {
    CarryingGemCount = (uint)Math.Min(uint.MaxValue, (ulong)CarryingGemCount + gems);
    CarryingGemBaseValue = PrestigeProgression.AddSaturating(CarryingGemBaseValue, baseValue);
    credits?.MoveTo(Credits);
  }
}
