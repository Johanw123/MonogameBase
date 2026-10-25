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

  private bool DamagePanelVisible => HudLayout.BottomBarVisible && GameStarted && PlanetMiningEnabled && !GameMain.IsPaused
    && !m_prestiging && !m_postPrestige && !_renderGuiSystem.IsOverlayVisible
    && !_renderGuiSystem.BulkUpgradeButtonsShown;


  private bool DamagePanelOpen => GameMain.DamagePanelOpen;
  private bool damagePanelGestureCaptured;

  // The open panel blocks world clicks under it, so reading it never fires the cannon.
  private bool DamagePanelUnderCursor => damagePanelGestureCaptured
    || (DamagePanelVisible && DamagePanelOpen && DamagePanelBounds().Contains(GameInput.UiCursor));

  // Right-aligned with the button, bottom just above the HUD bar, growing up as sources appear.
  private Rectangle DamagePanelBounds()
  {
    int rows = Math.Max(1, CountDamageRows());
    int height = DamageTitleHeight + DamageColumnsHeight + rows * HudLayout.DamageRowHeight + DamageTotalHeight;
    int bottom = HudLayout.Top - DamagePanelGap;
    return new Rectangle(HudLayout.DamageButton.Right - HudLayout.DamagePanelWidth, bottom - height,
      HudLayout.DamagePanelWidth, height);
  }

  private Rectangle DamagePanelCloseButton
    => GuiSkin.PanelCloseButtonBounds(DamagePanelBounds(), size: 38, marginX: 20, marginY: 16);

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
    if (damagePanelGestureCaptured && !GameInput.Mouse.IsButtonDown(MouseButton.Left))
      damagePanelGestureCaptured = false;
    if (!DamagePanelVisible || !GameplayInputEnabled) return;
    if (!GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    if (DamagePanelOpen && DamagePanelCloseButton.Contains(GameInput.UiCursor))
    {
      damagePanelGestureCaptured = true;
      GameMain.DamagePanelOpen = false;
      AudioManager.Instance.PlaySound(SoundEvent.WindowClose);
      return;
    }
    if (!HudLayout.DamageButton.Contains(GameInput.UiCursor)) return;
    GameMain.DamagePanelOpen = !GameMain.DamagePanelOpen;
    AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
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
    var closeBtn = DamagePanelCloseButton;
    int left = bounds.X + DamagePanelPadding, right = bounds.Right - DamagePanelPadding;
    int columns = bounds.Y + DamageTitleHeight;
    int top = columns + DamageColumnsHeight;
    double minuteTotal = damage.TotalPerMinute;

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.TechCard(m_spriteBatch, bounds, GuiPalette.Base(new Color(6, 20, 38, 224)), GuiSkin.Accent, cornerGraphic: true, hexWatermark: false);
    GuiSkin.TechDivider(m_spriteBatch, new Rectangle(left, columns - 4, right - left, 3), GuiSkin.Accent * 0.65f);
    GuiSkin.CloseButton(m_spriteBatch, closeBtn, GuiSkin.Accent, GameplayInputEnabled && closeBtn.Contains(GameInput.UiCursor));
    // Each row's share of the last minute's damage, as a bar under its name.
    for (int i = 0; i < damageRowCount; i++)
    {
      int y = top + i * HudLayout.DamageRowHeight;
      float share = minuteTotal > 0 ? (float)(damage.PerMinute(damageRows[i]) / minuteTotal) : 0f;
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(left, y + HudLayout.DamageRowHeight - 8, right - left, 3), GuiSkin.ButtonColor);
      if (share > 0f)
        m_spriteBatch.Draw(AssetManager.DefaultTexture,
          new Rectangle(left, y + HudLayout.DamageRowHeight - 8, Math.Max(2, (int)((right - left) * share)), 3),
          GuiSkin.Accent * 0.8f);
    }
    int separator = top + Math.Max(1, damageRowCount) * HudLayout.DamageRowHeight + 6;
    GuiSkin.TechDivider(m_spriteBatch, new Rectangle(left, separator, right - left, 3), GuiSkin.Accent * 0.65f);
    m_spriteBatch.End();

    DrawFittedHudText(Loc.T("DAMAGE"), new Vector2(left, bounds.Y + 18), Math.Max(80, closeBtn.Left - left - 12), 36f, GuiSkin.StatHeadingColor);
    DrawFittedHudText(Loc.T("SOURCE"), new Vector2(left, columns + 6), 200, 24f, GuiSkin.MutedTextColor);
    DrawRightHudText(Loc.T("PER MIN"), bounds.X + DamageMinuteRight, columns + 6, 24f, GuiSkin.MutedTextColor);
    DrawRightHudText(Loc.T("THIS RUN"), right, columns + 6, 24f, GuiSkin.MutedTextColor);
    if (damageRowCount == 0)
      DrawFittedHudText(Loc.T("No damage yet"), new Vector2(left, top + 6), right - left, 28f, GuiSkin.MutedTextColor);
    for (int i = 0; i < damageRowCount; i++)
    {
      var source = damageRows[i];
      float y = top + i * HudLayout.DamageRowHeight + 4;
      double minute = damage.PerMinute(source);
      DrawFittedHudText(Loc.T(PlanetDamageMeter.Name(source)), new Vector2(left, y), DamageMinuteRight - DamagePanelPadding - 130,
        28f, minute > 0 ? GuiSkin.ButtonTextColor : GuiSkin.MutedTextColor);
      DrawRightHudText(DamageText(minute), bounds.X + DamageMinuteRight, y, 28f,
        minute > 0 ? GuiSkin.StatValueColor : GuiSkin.MutedTextColor);
      DrawRightHudText(DamageText(damage.ThisRun(source)), right, y, 28f, GuiSkin.StatHeadingColor);
    }
    float total = top + Math.Max(1, damageRowCount) * HudLayout.DamageRowHeight + 16;
    DrawFittedHudText(Loc.T("Total"), new Vector2(left, total), 200, 30f, GuiSkin.StatHeadingColor);
    DrawRightHudText(DamageText(minuteTotal), bounds.X + DamageMinuteRight, total, 30f, GuiSkin.StatValueColor);
    DrawRightHudText(DamageText(damage.TotalThisRun), right, total, 30f, GuiSkin.StatHeadingColor);
  }

  // Laid out like the power cell panel: a title over a status line.
  private void DrawDamageButton()
  {
    var button = HudLayout.DamageButton;
    bool open = DamagePanelOpen;
    bool hovered = GameplayInputEnabled && button.Contains(GameInput.UiCursor);
    int padding = HudLayout.ProgressPanelPadding;
    int contentWidth = button.Width - padding * 2;

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Button(m_spriteBatch, button, open || hovered);
    DrawDamageChevron(new Vector2(button.Right - padding - 12, button.Y + 26), open,
      hovered ? Color.White : GuiSkin.ButtonTextColor);
    m_spriteBatch.End();

    DrawFittedHudText(Loc.T("DAMAGE"), new Vector2(button.X + padding, button.Y + 12),
      contentWidth - 40, 26f, hovered ? Color.White : GuiSkin.StatHeadingColor);
    DrawFittedHudText(Loc.F("{0} / min", DamageText(m_gameState.Damage.TotalPerMinute)),
      new Vector2(button.X + padding, button.Y + 46), contentWidth, 26f, GuiSkin.StatValueColor);
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
