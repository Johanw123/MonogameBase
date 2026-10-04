namespace UntitledGemGame.Screens;

// Hooks for Capture/CaptureSession: staged sessions and scripted manual abilities.
public partial class UntitledGemGameGameScreen
{
  // Replacing a staged session must not save it over the prepared capture save.
  internal void DiscardProgressOnUnload() => progressReady = false;

  internal float CaptureGemsPerMinute => _incomeTracker.GemsPerMinute;

  // A gem event right now, through the same spawn code as its timer.
  internal void CaptureGemEvent(string kind)
  {
    var bounds = PlayAreaBounds.ForCamera(m_camera);
    if (kind == "shower") SpawnGemShower(bounds.Minimum, bounds.Maximum);
    else SpawnGemComet(bounds.Minimum, bounds.Maximum);
  }

  // After a prestige: what the permanent-upgrade tree's Apply button does (start the next run).
  internal void CaptureStartNewRun()
  {
    RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
    m_prestiging = false;
    m_postPrestige = false;
    m_prestigeTime = 0.0f;
    SaveProgress();
  }

  // Same as pressing the ability's key, including its button sound.
  internal bool CaptureActivateManual(int slot)
  {
    if (!ManualAbilities.TryActivate(slot, ActivateManualEffect, m_entityFactory.GemReserve)) return false;
    AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
    return true;
  }
}
