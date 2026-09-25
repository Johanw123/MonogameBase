using UntitledGemGame;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  public bool TryAcknowledgeModule(ShipModule module)
  {
    if (!progressReady || GameMain.IsPaused || m_prestiging || m_postPrestige
      || !m_gameState.Modules.TryAcknowledgeReveal(module)) return false;
    SaveProgress();
    return true;
  }
}
