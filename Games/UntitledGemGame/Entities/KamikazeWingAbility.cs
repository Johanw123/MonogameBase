using UntitledGemGame.Screens;

namespace UntitledGemGame.Entities;

// Launches a Kamikaze Wing (KamikazeWing.cs); the game screen flies and detonates the
// bombers (GameScreen.KamikazeWing.cs).
public class KamikazeWingAbility : IHomeBaseAbility
{
  public override string IconPath => "Textures/scifi_icons/icon_skull/9_skull.png";
  public override int Level => UpgradeManager.Instance.UGA.KamikazeWing;
  public override int DurationTimeMax => 0;
  protected override int BaseCooldownMilliseconds => KamikazeWing.CooldownMilliseconds;
  protected override float CooldownMultiplier => UpgradeManager.Instance.UGA.KamikazeWingCooldown;
  // Drone cooldown signals recharge the wing too.
  protected override SignalKind? CooldownSignal => SignalKind.DroneCooldown;

  public override void Activate() => UntitledGemGameGameScreen.Instance?.LaunchKamikazeWing();

  public override void Deactivate()
  {
    // Bombers in flight finish their dive after the activation.
  }
}
