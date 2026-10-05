using UntitledGemGame.Screens;

namespace UntitledGemGame.Entities;

// Launches a Core Drill pod; the game screen flies, lands and runs it (GameScreen.CoreDrill.cs).
public class CoreDrillAbility : IHomeBaseAbility
{
  public override string IconPath => "Textures/scifi_icons/icon_accuracy/6_accuracy.png";
  public override int Level => UpgradeManager.Instance.UGA.CoreDrill;
  public override int DurationTimeMax => 0;
  protected override int BaseCooldownMilliseconds => CoreDrill.CooldownMilliseconds;
  protected override float CooldownMultiplier => UpgradeManager.Instance.UGA.CoreDrillCooldown;

  public override void Activate() => UntitledGemGameGameScreen.Instance?.LaunchCoreDrill();

  public override void Deactivate()
  {
    // A launched pod finishes its own drilling after the activation.
  }

  public override void Cancel()
  {
    UntitledGemGameGameScreen.Instance?.ClearCoreDrills();
    base.Cancel();
  }
}
