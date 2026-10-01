using Microsoft.Xna.Framework;
using JapeFramework;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private float reserveBurstFlash;
  private ulong reserveBurstLastPayout;
  private void DrawGemReserve()
  {
    if (!m_upgradeManager.UG.GemReserveUnlocked || m_prestiging || m_postPrestige) return;
    var reserve = m_entityFactory.GemReserve;
    int capacity = SignalStats.ReserveCapacity;
    var panel = HudLayout.GemReservePanel;
    bool full = reserve.Count >= capacity;
    m_spriteBatch.Begin();
    OrbitSkin.Button(m_spriteBatch, panel, false);
    OrbitSkin.Progress(m_spriteBatch, new Rectangle(panel.X + 14, panel.Y + 85, panel.Width - 28, 8),
      capacity > 0 ? reserve.Count / (float)capacity : 0);
    m_spriteBatch.End();
    DrawFittedHudText(reserveBurstFlash > 0 ? "RESERVE BURST!" : full ? "GEM RESERVE · SLOTS FULL" : "GEM RESERVE",
      new Vector2(panel.X + 14, panel.Y + 7), panel.Width - 28, 26,
      full || reserveBurstFlash > 0 ? Color.Gold : Color.Aquamarine);
    DrawFittedHudText($"{reserve.Count:N0} / {capacity:N0} slots",
      new Vector2(panel.X + 14, panel.Y + 43), panel.Width - 28, 32, HudLayout.MutedTextColor);
    DrawFittedHudText($"Value · {reserve.StoredGemCount:N0} gems",
      new Vector2(panel.X + 14, panel.Y + 103), panel.Width - 28, 24, HudLayout.MutedTextColor);
    DrawFittedHudText(NumberFormatter.AbbreviateBigNumber(reserve.StoredValue),
      new Vector2(panel.X + 14, panel.Y + 132), panel.Width - 28, 40, Color.Aquamarine);
    DrawFittedHudText(reserveBurstFlash > 0 ? $"+{NumberFormatter.AbbreviateBigNumber(reserveBurstLastPayout)} gems"
      : $"Burst: {NumberFormatter.AbbreviateBigNumber(reserve.BurstValue(ManualAbilities.ReserveBurstMultiplier))}",
      new Vector2(panel.X + 14, panel.Y + 181), panel.Width - 28, 26,
      reserveBurstFlash > 0 ? Color.Gold : HudLayout.MutedTextColor);
  }
}
