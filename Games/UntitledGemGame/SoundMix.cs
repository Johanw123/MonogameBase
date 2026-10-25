using System;

// Gameplay importance belongs to the event, independently of the editable sound asset/category.
public enum SoundBus { Player, Automation, Pickup, Milestone }

public readonly record struct SoundMixRule(SoundBus Bus, float Cooldown);

public static class SoundMix
{
  public static SoundMixRule Rule(SoundEvent soundEvent, bool playerInitiated = false)
  {
    if (playerInitiated) return new(SoundBus.Player, 0.025f);
    return soundEvent switch
    {
      SoundEvent.MenuHover or SoundEvent.TooltipShow or SoundEvent.SignalHover => new(SoundBus.Player, 0.09f),
      SoundEvent.MenuClick or SoundEvent.WindowOpen or SoundEvent.WindowClose
        or SoundEvent.UpgradeStart or SoundEvent.UpgradeError or SoundEvent.UpgradeRefund
        or SoundEvent.ManualAbilityActivate or SoundEvent.SignalScan or SoundEvent.SignalChoose
        or SoundEvent.SignalDecodeFirst or SoundEvent.SignalDecodeSecond or SoundEvent.SignalDecodeThird
        or SoundEvent.SignalRevealCommon or SoundEvent.SignalRevealUncommon or SoundEvent.SignalRevealRare
        or SoundEvent.SignalRevealEpic or SoundEvent.SignalRevealLegendary
        or SoundEvent.GemClick or SoundEvent.CursorGravityCollapse or SoundEvent.RefuelStart
        or SoundEvent.ModuleRevealStart or SoundEvent.ModuleRevealCommon or SoundEvent.ModuleRevealEpic
        or SoundEvent.ModuleRevealLegendary or SoundEvent.ModuleEquip or SoundEvent.ModuleUnequip
        or SoundEvent.CannonFireManual or SoundEvent.CoreShardClick => new(SoundBus.Player, 0.025f),

      SoundEvent.GemPickup => new(SoundBus.Pickup, 0.14f),
      SoundEvent.GemMerge or SoundEvent.HarvesterDeliverCargo => new(SoundBus.Pickup, 0.4f),

      SoundEvent.ShipEngineDying or SoundEvent.ShipCrashLand
        or SoundEvent.PlanetCrackerStart or SoundEvent.PlanetShellShatterStart or SoundEvent.PlanetShellBurst
        or SoundEvent.CoreFractureStart or SoundEvent.CoreFractureHeartbeat or SoundEvent.CoreFractureSwallow
        or SoundEvent.CoreFractureEruption or SoundEvent.CoreShardRelease or SoundEvent.CoreShardCollect
        or SoundEvent.CoreExtractionStart or SoundEvent.TimeLoopCollapseBuildup or SoundEvent.TimeLoopCollapseImplode
        or SoundEvent.TimeLoopPlanetSwallowed or SoundEvent.TimeLoopRestart => new(SoundBus.Milestone, 0.12f),

      SoundEvent.RefuelRequest or SoundEvent.RefuelComplete => new(SoundBus.Automation, 0.8f),
      SoundEvent.UpgradeComplete or SoundEvent.ModuleSalvageReady or SoundEvent.CursorGravityReady
        or SoundEvent.JackpotHaul or SoundEvent.WeaponJackpot or SoundEvent.PrestigePointEarned
        or SoundEvent.PrestigePointLanded => new(SoundBus.Automation, 0.65f),
      SoundEvent.CannonFireAuto or SoundEvent.CannonImpact or SoundEvent.RocketMiniImpact
        or SoundEvent.KamikazeBombletImpact or SoundEvent.ArcHarpoonPulse => new(SoundBus.Automation, 0.18f),
      // New unclassified events default to the bounded background bus.
      _ => new(SoundBus.Automation, 0.3f)
    };
  }
}
