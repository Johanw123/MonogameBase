using JapeFramework;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  private readonly SignalAudio signalAudio = new();

  private void BeginSignalReveal()
  {
    ClearSignalParticles();
    signalRevealStarted = BaseGame.Time.TotalGameTime.TotalSeconds;
    signalAudio.BeginReveal();
  }

  internal void UpdateSignalAudio()
  {
    bool visible = drawUpgradesGui && m_upgradeWindowType == UpgradeTypes.Signals
      && !GameMain.IsPaused && !PlaytestFeedback.IsOpen && !IsSelectingSignal;
    int hover = -1;
    if (visible && (!IsDetached || IsPopoutFocused) && HasInputFocus)
    {
      var cursor = GameInput.UiCursor;
      for (int i = 0; i < 3; i++)
        if (SignalCard(i).Contains(cursor.X, cursor.Y)) { hover = i; break; }
    }
    signalAudio.Update((float)(BaseGame.Time.TotalGameTime.TotalSeconds - signalRevealStarted),
      Signals.PendingChoices, visible, hover, soundEvent => AudioManager.Instance.PlaySound(soundEvent));
  }
}
