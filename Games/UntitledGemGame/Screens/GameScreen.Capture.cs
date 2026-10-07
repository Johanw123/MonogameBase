namespace UntitledGemGame.Screens;

// Hooks for Capture/CaptureSession: staged sessions and scripted manual abilities.
public partial class UntitledGemGameGameScreen
{
  // Replacing a staged session must not save it over the prepared capture save.
  internal void DiscardProgressOnUnload() => progressReady = false;

  internal float CaptureGemsPerMinute => _incomeTracker.GemsPerMinute;

  // Fires a main ship weapon right now, as a click or its own timer would.
  internal void CaptureFireWeapon(string weapon)
  {
    switch (weapon)
    {
      case "cannon": FireManualShot(PlanetFacingPoint(0.55f)); break;
      case "rockets": FireRocketSalvo(); break;
      default: CaptureFireRailgun(); break;
    }
  }

  // After a prestige: what the permanent-upgrade tree's Apply button does (start the next run).
  internal void CaptureStartNewRun()
  {
    RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
    m_prestiging = false;
    m_postPrestige = false;
    m_prestigeTime = 0.0f;
    GrantRunStartRewards();
    SaveProgress();
  }

  // Same as pressing the ability's key, including its button sound.
  internal bool CaptureActivateManual(int slot)
  {
    if (!ManualAbilities.TryActivate(slot, ActivateManualEffect)) return false;
    AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
    return true;
  }
}
