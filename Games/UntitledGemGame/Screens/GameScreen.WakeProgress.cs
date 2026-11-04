using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private void DrawWakeProgress()
  {
    if (!GameStarted || m_prestiging || m_postPrestige || GameMain.IsPaused
      || RenderGuiSystem.Instance.IsOverlayVisible) return;
    var ship = m_camera.WorldToScreen(HomeBasePos);
#if KNI_WEB
    ship *= 2f;
#endif
    bool shell = !PlanetShell.Broken(m_gameState.ShellDamage);
    double core = fractureActive ? 1 : shell ? PlanetShell.Wear(m_gameState.ShellDamage)
      : DamagePerMinute / CoreFracture.Threshold(m_gameState.CoreFractures);
    double prestige = m_gameState.PrestigeEcho + m_gameState.PrestigeProgress;
    DrawWakeBar(ship + new Vector2(-260, -28), core,
      shell ? Loc.T("SHELL") : Loc.T("CORE SHATTER"), FractureColor);
    DrawWakeBar(ship + new Vector2(-260, 28), prestige,
      Loc.T("PRESTIGE"), PrestigePointColor);
  }

  private void DrawWakeBar(Vector2 center, double progress, string label, Color color)
  {
    float fill = (float)Math.Clamp(progress, 0, 1);
    var viewport = GraphicsDevice.Viewport;
    m_shapeBatch.Begin(HudLayout.RenderTransform,
      Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1),
      blendState: BlendState.AlphaBlend);
    const float width = 180f;
    const float height = 7f;
    var start = center - new Vector2(width * 0.5f, height * 0.5f);
    m_shapeBatch.FillRectangle(start - new Vector2(2), new Vector2(width + 4, height + 4),
      Color.Black * 0.5f);
    m_shapeBatch.FillRectangle(start, new Vector2(width, height), color * 0.16f);
    if (fill > 0)
      m_shapeBatch.FillRectangle(start, new Vector2(width * fill, height), color * 0.85f);
    m_shapeBatch.End();
    DrawCenteredNotification(label, center.X, center.Y - 23, 14f,
      color * 0.8f, Color.Black);
  }
}
