using System;
using System.Numerics;
using AsyncContent;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace UntitledGemGame.Screens;

// The HUD's Damage panel: the planet's damage by source (PlanetDamageMeter), over the
// last minute and over the run, strongest first. The Damage button at the right end of
// the HUD bar shows the total per minute and opens or closes the panel, which grows up
// from the bar over the play area; the choice is kept in the settings. Sources show up
// once they have dealt damage this run.
public partial class UntitledGemGameGameScreen
{
  private const int DamagePanelPadding = 24;
  private const int DamagePanelGap = 12;
  private const int DamageTitleHeight = 72;
  private const int DamageColumnsHeight = 40;
  private const int DamageTotalHeight = 58;
  // Right edges of the per-minute and run columns, from the panel's left.
  private const int DamageMinuteRight = 470;

  private readonly PlanetDamageSource[] damageRows = new PlanetDamageSource[PlanetDamageMeter.SourceCount];
  private int damageRowCount;

#if KNI_WEB
  // The web build skips the HUD text it is drawn with (GameScreen.cs), so it has no panel to click.
  private bool DamagePanelVisible => false;
#else
  private bool DamagePanelVisible => GameStarted && PlanetMiningEnabled && !GameMain.IsPaused
    && !m_prestiging && !m_postPrestige && !_renderGuiSystem.IsOverlayVisible
    && !_renderGuiSystem.BulkUpgradeButtonsShown;
#endif

  private bool DamagePanelOpen => GameMain.DamagePanelOpen;

  // The open panel blocks world clicks under it, so reading it never fires the cannon.
  private bool DamagePanelUnderCursor => DamagePanelVisible && DamagePanelOpen
    && DamagePanelBounds().Contains(GameInput.UiCursor);

  // Right-aligned with the button, bottom just above the HUD bar, growing up as sources appear.
  private Rectangle DamagePanelBounds()
  {
    int rows = Math.Max(1, CountDamageRows());
    int height = DamageTitleHeight + DamageColumnsHeight + rows * HudLayout.DamageRowHeight + DamageTotalHeight;
    int bottom = HudLayout.Top - DamagePanelGap;
    return new Rectangle(HudLayout.DamageButton.Right - HudLayout.DamagePanelWidth, bottom - height,
      HudLayout.DamagePanelWidth, height);
  }

  private int CountDamageRows()
  {
    var damage = m_gameState.Damage;
    int count = 0;
    for (int i = 0; i < PlanetDamageMeter.SourceCount; i++)
      if (damage.ThisRun((PlanetDamageSource)i) > 0) count++;
    return count;
  }

  private void UpdateDamagePanel()
  {
    if (!DamagePanelVisible || !GameplayInputEnabled) return;
    if (!HudLayout.DamageButton.Contains(GameInput.UiCursor)
      || !GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    GameMain.DamagePanelOpen = !GameMain.DamagePanelOpen;
    AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
  }

  // Busiest sources first: by damage over the last minute, then over the run.
  private void SortDamageRows()
  {
    var damage = m_gameState.Damage;
    damageRowCount = 0;
    for (int i = 0; i < PlanetDamageMeter.SourceCount; i++)
      if (damage.ThisRun((PlanetDamageSource)i) > 0) damageRows[damageRowCount++] = (PlanetDamageSource)i;
    // Insertion sort: a handful of rows, and no allocation every frame.
    for (int i = 1; i < damageRowCount; i++)
    {
      var row = damageRows[i];
      int j = i - 1;
      while (j >= 0 && DamageRowBefore(row, damageRows[j]))
      {
        damageRows[j + 1] = damageRows[j];
        j--;
      }
      damageRows[j + 1] = row;
    }
  }

  private bool DamageRowBefore(PlanetDamageSource a, PlanetDamageSource b)
  {
    var damage = m_gameState.Damage;
    double minuteA = damage.PerMinute(a), minuteB = damage.PerMinute(b);
    return minuteA != minuteB ? minuteA > minuteB : damage.ThisRun(a) > damage.ThisRun(b);
  }

  private static string DamageText(double damage)
    => NumberFormatter.AbbreviateBigNumber(new BigInteger(Math.Floor(Math.Max(0, damage))));

  private void DrawRightHudText(string text, float right, float y, float fontSize, Color color)
  {
    float width = Measure2(text, Vector2.Zero, fontSize).X;
    DrawFittedHudText(text, new Vector2(right - width, y), width + 1, fontSize, color);
  }

  private void DrawDamagePanel()
  {
    if (!DamagePanelVisible || _renderGuiSystem.DrawingPopout) return;
    DrawDamageButton();
    if (!DamagePanelOpen) return;

    var damage = m_gameState.Damage;
    SortDamageRows();
    var bounds = DamagePanelBounds();
    int left = bounds.X + DamagePanelPadding, right = bounds.Right - DamagePanelPadding;
    int columns = bounds.Y + DamageTitleHeight;
    int top = columns + DamageColumnsHeight;
    double minuteTotal = damage.TotalPerMinute;

    m_spriteBatch.Begin();
    OrbitSkin.Panel(m_spriteBatch, bounds);
    // Each row's share of the last minute's damage, as a bar under its name.
    for (int i = 0; i < damageRowCount; i++)
    {
      int y = top + i * HudLayout.DamageRowHeight;
      float share = minuteTotal > 0 ? (float)(damage.PerMinute(damageRows[i]) / minuteTotal) : 0f;
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(left, y + HudLayout.DamageRowHeight - 8, right - left, 3), OrbitSkin.ButtonColor);
      if (share > 0f)
        m_spriteBatch.Draw(AssetManager.DefaultTexture,
          new Rectangle(left, y + HudLayout.DamageRowHeight - 8, Math.Max(2, (int)((right - left) * share)), 3),
          OrbitSkin.Accent * 0.8f);
    }
    int separator = top + Math.Max(1, damageRowCount) * HudLayout.DamageRowHeight + 6;
    m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(left, separator, right - left, 2), OrbitSkin.BorderColor);
    m_spriteBatch.End();

    DrawFittedHudText(Loc.T("DAMAGE"), new Vector2(left, bounds.Y + 18), right - left, 36f, OrbitSkin.StatHeadingColor);
    DrawFittedHudText(Loc.T("SOURCE"), new Vector2(left, columns + 6), 200, 24f, OrbitSkin.MutedTextColor);
    DrawRightHudText(Loc.T("PER MIN"), bounds.X + DamageMinuteRight, columns + 6, 24f, OrbitSkin.MutedTextColor);
    DrawRightHudText(Loc.T("THIS RUN"), right, columns + 6, 24f, OrbitSkin.MutedTextColor);
    if (damageRowCount == 0)
      DrawFittedHudText(Loc.T("No damage yet"), new Vector2(left, top + 6), right - left, 28f, OrbitSkin.MutedTextColor);
    for (int i = 0; i < damageRowCount; i++)
    {
      var source = damageRows[i];
      float y = top + i * HudLayout.DamageRowHeight + 4;
      double minute = damage.PerMinute(source);
      DrawFittedHudText(Loc.T(PlanetDamageMeter.Name(source)), new Vector2(left, y), DamageMinuteRight - DamagePanelPadding - 130,
        28f, minute > 0 ? OrbitSkin.ButtonTextColor : OrbitSkin.MutedTextColor);
      DrawRightHudText(DamageText(minute), bounds.X + DamageMinuteRight, y, 28f,
        minute > 0 ? OrbitSkin.StatValueColor : OrbitSkin.MutedTextColor);
      DrawRightHudText(DamageText(damage.ThisRun(source)), right, y, 28f, OrbitSkin.StatHeadingColor);
    }
    float total = top + Math.Max(1, damageRowCount) * HudLayout.DamageRowHeight + 16;
    DrawFittedHudText(Loc.T("Total"), new Vector2(left, total), 200, 30f, OrbitSkin.StatHeadingColor);
    DrawRightHudText(DamageText(minuteTotal), bounds.X + DamageMinuteRight, total, 30f, OrbitSkin.StatValueColor);
    DrawRightHudText(DamageText(damage.TotalThisRun), right, total, 30f, OrbitSkin.StatHeadingColor);
  }

  // Laid out like the power cell panel: a title over a status line.
  private void DrawDamageButton()
  {
    var button = HudLayout.DamageButton;
    bool open = DamagePanelOpen;
    bool hovered = GameplayInputEnabled && button.Contains(GameInput.UiCursor);
    int padding = HudLayout.ProgressPanelPadding;
    int contentWidth = button.Width - padding * 2;

    m_spriteBatch.Begin();
    OrbitSkin.Button(m_spriteBatch, button, open || hovered);
    DrawDamageChevron(new Vector2(button.Right - padding - 12, button.Y + 30), open,
      hovered ? Color.White : OrbitSkin.ButtonTextColor);
    m_spriteBatch.End();

    DrawFittedHudText(Loc.T("DAMAGE"), new Vector2(button.X + padding, button.Y + HudLayout.ProgressTitleTop),
      contentWidth - 40, 36f, hovered ? Color.White : OrbitSkin.StatHeadingColor);
    DrawFittedHudText(Loc.F("{0} / min", DamageText(m_gameState.Damage.TotalPerMinute)),
      new Vector2(button.X + padding, button.Y + HudLayout.ProgressStatusTop), contentWidth, 32f, OrbitSkin.StatValueColor);
  }

  // Pointing up (the way the panel opens) when closed, down when open. Called inside a
  // SpriteBatch pass.
  private void DrawDamageChevron(Vector2 center, bool open, Color color)
  {
    const float arm = 15f, thickness = 4f;
    float tip = open ? MathHelper.PiOver2 : -MathHelper.PiOver2;
    var pointAt = center + new Vector2(MathF.Cos(tip), MathF.Sin(tip)) * arm * 0.4f;
    for (int side = -1; side <= 1; side += 2)
    {
      float angle = tip + MathHelper.Pi + side * MathHelper.PiOver4;
      m_spriteBatch.Draw(AssetManager.DefaultTexture, pointAt, null, color, angle, new Vector2(0f, 0.5f),
        new Vector2(arm, thickness), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
    }
  }
}
