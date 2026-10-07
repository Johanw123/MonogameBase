using System;
using System.Numerics;
using AsyncContent;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace UntitledGemGame.Screens;

// The HUD's Damage panel: the planet's damage by source (PlanetDamageMeter), over the
// last minute and over the run, strongest first. Closed, its header still shows the
// total per minute; clicking the header opens or closes it, and the choice is kept in
// the settings. Sources show up once they have dealt damage this run.
public partial class UntitledGemGameGameScreen
{
  private const int DamagePanelPadding = 24;
  private const int DamageColumnsTop = 12;
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
    && !m_prestiging && !m_postPrestige && !_renderGuiSystem.IsOverlayVisible;
#endif

  private bool DamagePanelOpen => GameMain.DamagePanelOpen;

  // The panel blocks world clicks under it, so opening it never fires the cannon.
  private bool DamagePanelUnderCursor => DamagePanelVisible && DamagePanelBounds().Contains(GameInput.UiCursor);

  private Rectangle DamagePanelBounds()
  {
    var header = HudLayout.DamagePanelHeader;
    if (!DamagePanelOpen) return header;
    int rows = Math.Max(1, CountDamageRows());
    return new Rectangle(header.X, header.Y, header.Width,
      header.Height + DamageColumnsTop + DamageColumnsHeight + rows * HudLayout.DamageRowHeight + DamageTotalHeight);
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
    if (!HudLayout.DamagePanelHeader.Contains(GameInput.UiCursor)
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
    var damage = m_gameState.Damage;
    var header = HudLayout.DamagePanelHeader;
    bool open = DamagePanelOpen;
    bool hovered = GameplayInputEnabled && header.Contains(GameInput.UiCursor);
    if (open) SortDamageRows();
    var bounds = DamagePanelBounds();
    int left = header.X + DamagePanelPadding, right = header.Right - DamagePanelPadding;
    int top = header.Bottom + DamageColumnsTop + DamageColumnsHeight;
    double minuteTotal = damage.TotalPerMinute;

    m_spriteBatch.Begin();
    if (open)
      OrbitSkin.Panel(m_spriteBatch, new Rectangle(bounds.X, header.Bottom - 8, bounds.Width, bounds.Height - header.Height + 8));
    OrbitSkin.Button(m_spriteBatch, header, hovered);
    DrawDamageChevron(new Vector2(right - 12, header.Center.Y), open, hovered ? Color.White : OrbitSkin.ButtonTextColor);
    if (open)
    {
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
    }
    m_spriteBatch.End();

    DrawFittedHudText(Loc.T("DAMAGE"), new Vector2(left, header.Y + 14), 200, 36f,
      hovered ? Color.White : OrbitSkin.StatHeadingColor);
    DrawRightHudText(Loc.F("{0} / min", DamageText(minuteTotal)), right - 44, header.Y + 16, 32f, OrbitSkin.StatValueColor);
    if (!open) return;

    float columns = header.Bottom + DamageColumnsTop + 6;
    DrawFittedHudText(Loc.T("SOURCE"), new Vector2(left, columns), 200, 24f, OrbitSkin.MutedTextColor);
    DrawRightHudText(Loc.T("PER MIN"), header.X + DamageMinuteRight, columns, 24f, OrbitSkin.MutedTextColor);
    DrawRightHudText(Loc.T("THIS RUN"), right, columns, 24f, OrbitSkin.MutedTextColor);
    if (damageRowCount == 0)
      DrawFittedHudText(Loc.T("No damage yet"), new Vector2(left, top + 6), right - left, 28f, OrbitSkin.MutedTextColor);
    for (int i = 0; i < damageRowCount; i++)
    {
      var source = damageRows[i];
      float y = top + i * HudLayout.DamageRowHeight + 4;
      double minute = damage.PerMinute(source);
      DrawFittedHudText(Loc.T(PlanetDamageMeter.Name(source)), new Vector2(left, y), DamageMinuteRight - DamagePanelPadding - 130,
        28f, minute > 0 ? OrbitSkin.ButtonTextColor : OrbitSkin.MutedTextColor);
      DrawRightHudText(DamageText(minute), header.X + DamageMinuteRight, y, 28f,
        minute > 0 ? OrbitSkin.StatValueColor : OrbitSkin.MutedTextColor);
      DrawRightHudText(DamageText(damage.ThisRun(source)), right, y, 28f, OrbitSkin.StatHeadingColor);
    }
    float total = top + Math.Max(1, damageRowCount) * HudLayout.DamageRowHeight + 16;
    DrawFittedHudText(Loc.T("Total"), new Vector2(left, total), 200, 30f, OrbitSkin.StatHeadingColor);
    DrawRightHudText(DamageText(minuteTotal), header.X + DamageMinuteRight, total, 30f, OrbitSkin.StatValueColor);
    DrawRightHudText(DamageText(damage.TotalThisRun), right, total, 30f, OrbitSkin.StatHeadingColor);
  }

  // Pointing down when open, right when closed. Called inside a SpriteBatch pass.
  private void DrawDamageChevron(Vector2 center, bool open, Color color)
  {
    const float arm = 15f, thickness = 4f;
    float tip = open ? MathHelper.PiOver2 : 0f;
    var pointAt = center + new Vector2(MathF.Cos(tip), MathF.Sin(tip)) * arm * 0.4f;
    for (int side = -1; side <= 1; side += 2)
    {
      float angle = tip + MathHelper.Pi + side * MathHelper.PiOver4;
      m_spriteBatch.Draw(AssetManager.DefaultTexture, pointAt, null, color, angle, new Vector2(0f, 0.5f),
        new Vector2(arm, thickness), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
    }
  }
}
