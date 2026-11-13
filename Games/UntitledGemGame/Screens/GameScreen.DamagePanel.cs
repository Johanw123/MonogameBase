using System;
using System.Numerics;
using AsyncContent;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace UntitledGemGame.Screens;

// The HUD's Collection and Damage panels: gem collection (GemCollectionMeter) and planet
// damage (PlanetDamageMeter) by source, over the last minute and over the run. Each is one
// table: the total on top, then every row that has contributed this run, strongest first,
// as many as fit on screen. Collection rows are ships, clicks and the Tractor Belt alongside
// the talents, upgrades and modules that pulled gems in, so they add up to the total. Two
// minimal chevron toggle buttons sit on the left edge in level with the home base ship and
// expand or collapse the panels; both choices are kept in the settings.
public partial class UntitledGemGameGameScreen
{
  private const int DamagePanelPadding = 24;
  private const int DamagePanelGap = 12;
  private const int DamageTitleHeight = 72;
  private const int DamageColumnsHeight = 40;
  private const int DamageTotalHeight = 64;
  private const int MeterBottomPadding = 12;
  // Right edges of the per-minute and run columns, from the panel's left.
  private const int DamageMinuteRight = 470;

  // The table being drawn, sorted; both panels fill it in turn (FillCollectionRows, FillDamageRows).
  private static readonly int MeterRowCapacity = Math.Max(GemCollectionMeter.RowCount, PlanetDamageMeter.SourceCount);
  private readonly string[] meterRowNames = new string[MeterRowCapacity];
  private readonly double[] meterRowMinute = new double[MeterRowCapacity];
  private readonly double[] meterRowRun = new double[MeterRowCapacity];
  private int meterRowCount;

  private bool MeterPanelsVisible => !SideResourcesSuppressed && !GameMain.IsPaused
    && !_renderGuiSystem.BulkUpgradeButtonsShown;

  private bool CollectionPanelVisible => MeterPanelsVisible;
  private bool DamagePanelVisible => MeterPanelsVisible && PlanetMiningEnabled;

  private bool CollectionPanelOpen => GameMain.CollectionPanelOpen;
  private bool DamagePanelOpen => GameMain.DamagePanelOpen;
  private bool damagePanelGestureCaptured;

  private Rectangle CollectionButtonBounds => HudLayout.SideCollectionButton();
  private Rectangle DamageButtonBounds => HudLayout.SideDamageButton();

  // The buttons and open panels block world clicks under them, so toggling or reading them never fires the cannon.
  private bool DamagePanelUnderCursor => damagePanelGestureCaptured
    || (CollectionPanelVisible && (CollectionButtonBounds.Contains(GameInput.UiCursor)
      || (CollectionPanelOpen && CollectionPanelBounds().Contains(GameInput.UiCursor))))
    || (DamagePanelVisible && (DamageButtonBounds.Contains(GameInput.UiCursor)
      || (DamagePanelOpen && DamagePanelBounds().Contains(GameInput.UiCursor))));

  private const int MeterChromeHeight = DamageTitleHeight + DamageColumnsHeight + DamageTotalHeight + MeterBottomPadding;

  private static int MeterPanelHeight(int rows)
    => MeterChromeHeight + Math.Max(1, rows) * HudLayout.DamageRowHeight;

  // Rows a panel can show without running off screen; the quietest ones go first.
  private static int MaxMeterRows
    => Math.Max(1, (HudLayout.ContentBottom - 32 - MeterChromeHeight) / HudLayout.DamageRowHeight);

  private static int ClampMeterPanelTop(int y, int height)
    => Math.Clamp(y, 16, Math.Max(16, HudLayout.ContentBottom - 16 - height));

  private int MeterPanelsLeft => HudLayout.SideMeterButtonWidth + DamagePanelGap;

  private Rectangle CollectionPanelBounds()
  {
    int height = MeterPanelHeight(CountCollectionRows());
    int centerY = HudLayout.SideMeterCenterY;
    if (DamagePanelVisible && DamagePanelOpen)
    {
      int damageHeight = MeterPanelHeight(CountDamageRows());
      int combined = height + DamagePanelGap + damageHeight;
      if (combined <= HudLayout.ContentBottom - 32)
      {
        int top = ClampMeterPanelTop(centerY - combined / 2, combined);
        return new Rectangle(MeterPanelsLeft, top, HudLayout.DamagePanelWidth, height);
      }
    }
    return new Rectangle(MeterPanelsLeft, ClampMeterPanelTop(centerY - height / 2, height),
      HudLayout.DamagePanelWidth, height);
  }

  private Rectangle DamagePanelBounds()
  {
    int height = MeterPanelHeight(CountDamageRows());
    int centerY = HudLayout.SideMeterCenterY;
    if (CollectionPanelVisible && CollectionPanelOpen)
    {
      var collection = CollectionPanelBounds();
      int stackedTop = collection.Bottom + DamagePanelGap;
      if (stackedTop + height <= HudLayout.ContentBottom - 16)
        return new Rectangle(MeterPanelsLeft, stackedTop, HudLayout.DamagePanelWidth, height);
      return new Rectangle(collection.Right + DamagePanelGap,
        ClampMeterPanelTop(centerY - height / 2, height), HudLayout.DamagePanelWidth, height);
    }
    return new Rectangle(MeterPanelsLeft, ClampMeterPanelTop(centerY - height / 2, height),
      HudLayout.DamagePanelWidth, height);
  }

  private Rectangle CollectionPanelCloseButton
    => GuiSkin.PanelCloseButtonBounds(CollectionPanelBounds(), size: 38, marginX: 20, marginY: 16);

  private Rectangle DamagePanelCloseButton
    => GuiSkin.PanelCloseButtonBounds(DamagePanelBounds(), size: 38, marginX: 20, marginY: 16);

  private int CountCollectionRows()
  {
    var collection = m_gameState.Collection;
    int count = 0;
    for (int row = 0; row < GemCollectionMeter.RowCount; row++)
      if (GemCollectionMeter.IsRow(row) && collection.ThisRun(row) > 0) count++;
    return Math.Min(count, MaxMeterRows);
  }

  private int CountDamageRows()
  {
    var damage = m_gameState.Damage;
    int count = 0;
    for (int i = 0; i < PlanetDamageMeter.SourceCount; i++)
      if (damage.ThisRun((PlanetDamageSource)i) > 0) count++;
    return Math.Min(count, MaxMeterRows);
  }

  private void UpdateDamagePanel()
  {
    if (damagePanelGestureCaptured && !GameInput.Mouse.IsButtonDown(MouseButton.Left))
      damagePanelGestureCaptured = false;
    if (!MeterPanelsVisible || !GameplayInputEnabled) return;
    if (!GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    var cursor = GameInput.UiCursor;

    if (CollectionPanelVisible && CollectionPanelOpen && CollectionPanelCloseButton.Contains(cursor))
    {
      damagePanelGestureCaptured = true;
      GameMain.CollectionPanelOpen = false;
      AudioManager.Instance.PlaySound(SoundEvent.WindowClose);
      return;
    }
    if (DamagePanelVisible && DamagePanelOpen && DamagePanelCloseButton.Contains(cursor))
    {
      damagePanelGestureCaptured = true;
      GameMain.DamagePanelOpen = false;
      AudioManager.Instance.PlaySound(SoundEvent.WindowClose);
      return;
    }
    if (CollectionPanelVisible && CollectionButtonBounds.Contains(cursor))
    {
      damagePanelGestureCaptured = true;
      GameMain.CollectionPanelOpen = !GameMain.CollectionPanelOpen;
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      return;
    }
    if (DamagePanelVisible && DamageButtonBounds.Contains(cursor))
    {
      damagePanelGestureCaptured = true;
      GameMain.DamagePanelOpen = !GameMain.DamagePanelOpen;
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
    }
  }

  private void FillCollectionRows()
  {
    var collection = m_gameState.Collection;
    meterRowCount = 0;
    for (int row = 0; row < GemCollectionMeter.RowCount; row++)
      if (GemCollectionMeter.IsRow(row))
        AddMeterRow(GemCollectionMeter.RowName(row), collection.PerMinute(row), collection.ThisRun(row));
    meterRowCount = Math.Min(meterRowCount, MaxMeterRows);
  }

  private void FillDamageRows()
  {
    var damage = m_gameState.Damage;
    meterRowCount = 0;
    for (int i = 0; i < PlanetDamageMeter.SourceCount; i++)
    {
      var source = (PlanetDamageSource)i;
      AddMeterRow(PlanetDamageMeter.Name(source), damage.PerMinute(source), damage.ThisRun(source));
    }
    meterRowCount = Math.Min(meterRowCount, MaxMeterRows);
  }

  // Inserted in order, busiest first: by the last minute, then by the run. Rows that have
  // contributed nothing this run stay off the table.
  private void AddMeterRow(string name, double minute, double run)
  {
    if (!(run > 0)) return;
    int i = meterRowCount++;
    while (i > 0 && (meterRowMinute[i - 1] != minute ? meterRowMinute[i - 1] < minute : meterRowRun[i - 1] < run))
    {
      meterRowNames[i] = meterRowNames[i - 1];
      meterRowMinute[i] = meterRowMinute[i - 1];
      meterRowRun[i] = meterRowRun[i - 1];
      i--;
    }
    meterRowNames[i] = name;
    meterRowMinute[i] = minute;
    meterRowRun[i] = run;
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
    if (!MeterPanelsVisible || _renderGuiSystem.DrawingPopout) return;
    if (CollectionPanelVisible)
    {
      DrawMeterExpanderButton(CollectionButtonBounds, CollectionPanelOpen);
      if (CollectionPanelOpen)
        DrawCollectionPanelCard();
    }
    if (DamagePanelVisible)
    {
      DrawMeterExpanderButton(DamageButtonBounds, DamagePanelOpen);
      if (DamagePanelOpen)
        DrawDamagePanelCard();
    }
  }

  private void DrawMeterRowProgressBar(Rectangle track, float share, bool activeThisMinute)
  {
    share = Math.Clamp(share, 0f, 1f);
    var pixel = GuiSkin.Pixel(GraphicsDevice);
    m_spriteBatch.Draw(pixel, track, Color.Black * 0.65f);

    if (share > 0f)
    {
      int fillWidth = Math.Max(4, (int)MathF.Round(track.Width * share));
      var fillRect = new Rectangle(track.X, track.Y, Math.Min(track.Width, fillWidth), track.Height);
      Color fillColor = activeThisMinute ? GuiSkin.ProgressAccent * 0.85f : GuiSkin.ProgressAccent * 0.45f;
      m_spriteBatch.Draw(pixel, fillRect, fillColor);
    }

    m_spriteBatch.Draw(pixel, new Rectangle(track.X, track.Y, track.Width, 1), GuiSkin.BorderColor * 0.35f);
    m_spriteBatch.Draw(pixel, new Rectangle(track.X, track.Bottom - 1, track.Width, 1), GuiSkin.BorderColor * 0.35f);
    m_spriteBatch.Draw(pixel, new Rectangle(track.X, track.Y, 1, track.Height), GuiSkin.BorderColor * 0.35f);
    m_spriteBatch.Draw(pixel, new Rectangle(track.Right - 1, track.Y, 1, track.Height), GuiSkin.BorderColor * 0.35f);
  }

  private void DrawCollectionPanelCard()
  {
    var collection = m_gameState.Collection;
    FillCollectionRows();
    DrawMeterTable(CollectionPanelBounds(), CollectionPanelCloseButton, Loc.T("COLLECTION"),
      Loc.T("No gems collected yet"), collection.TotalPerMinute, collection.TotalThisRun);
  }

  private void DrawDamagePanelCard()
  {
    var damage = m_gameState.Damage;
    FillDamageRows();
    DrawMeterTable(DamagePanelBounds(), DamagePanelCloseButton, Loc.T("DAMAGE"),
      Loc.T("No damage yet"), damage.TotalPerMinute, damage.TotalThisRun);
  }

  // Title, column headings, the total, then the filled rows, each with a bar for its share
  // of the last minute (or of the run when the last minute was idle).
  private void DrawMeterTable(Rectangle bounds, Rectangle closeBtn, string title, string emptyText,
    double minuteTotal, double runTotal)
  {
    int left = bounds.X + DamagePanelPadding, right = bounds.Right - DamagePanelPadding;
    int minuteRight = bounds.X + DamageMinuteRight;
    int columns = bounds.Y + DamageTitleHeight;
    int totalTop = columns + DamageColumnsHeight;
    int top = totalTop + DamageTotalHeight;

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.TechCard(m_spriteBatch, bounds, GuiPalette.Base(new Color(6, 20, 38, 224)), GuiSkin.Accent, cornerGraphic: true, hexWatermark: false);
    GuiSkin.TechDivider(m_spriteBatch, new Rectangle(left, columns - 4, right - left, 3), GuiSkin.Accent * 0.65f);
    GuiSkin.TechDivider(m_spriteBatch, new Rectangle(left, top - 10, right - left, 3), GuiSkin.Accent * 0.65f);
    GuiSkin.CloseButton(m_spriteBatch, closeBtn, GuiSkin.Accent, GameplayInputEnabled && closeBtn.Contains(GameInput.UiCursor));
    for (int i = 0; i < meterRowCount; i++)
    {
      int y = top + i * HudLayout.DamageRowHeight;
      float share = minuteTotal > 0
        ? (float)(meterRowMinute[i] / minuteTotal)
        : runTotal > 0 ? (float)(meterRowRun[i] / runTotal) : 0f;
      DrawMeterRowProgressBar(new Rectangle(left, y + HudLayout.DamageRowHeight - 12, right - left, 8), share,
        meterRowMinute[i] > 0);
    }
    m_spriteBatch.End();

    DrawFittedHudText(title, new Vector2(left, bounds.Y + 18), Math.Max(80, closeBtn.Left - left - 12), 36f, GuiSkin.StatHeadingColor);
    DrawFittedHudText(Loc.T("SOURCE"), new Vector2(left, columns + 6), 200, 24f, GuiSkin.MutedTextColor);
    DrawRightHudText(Loc.T("PER MIN"), minuteRight, columns + 6, 24f, GuiSkin.MutedTextColor);
    DrawRightHudText(Loc.T("THIS RUN"), right, columns + 6, 24f, GuiSkin.MutedTextColor);
    float total = totalTop + 10;
    DrawFittedHudText(Loc.T("Total"), new Vector2(left, total), 200, 30f, GuiSkin.StatHeadingColor);
    DrawRightHudText(DamageText(minuteTotal), minuteRight, total, 30f, GuiSkin.StatValueColor);
    DrawRightHudText(DamageText(runTotal), right, total, 30f, GuiSkin.StatHeadingColor);
    if (meterRowCount == 0)
      DrawFittedHudText(emptyText, new Vector2(left, top + 6), right - left, 28f, GuiSkin.MutedTextColor);
    for (int i = 0; i < meterRowCount; i++)
    {
      float y = top + i * HudLayout.DamageRowHeight + 2;
      bool active = meterRowMinute[i] > 0;
      DrawFittedHudText(Loc.T(meterRowNames[i]), new Vector2(left, y), DamageMinuteRight - DamagePanelPadding - 130,
        28f, active ? GuiSkin.ButtonTextColor : GuiSkin.MutedTextColor);
      DrawRightHudText(DamageText(meterRowMinute[i]), minuteRight, y, 28f,
        active ? GuiSkin.StatValueColor : GuiSkin.MutedTextColor);
      DrawRightHudText(DamageText(meterRowRun[i]), right, y, 28f, GuiSkin.StatHeadingColor);
    }
  }

  // Minimal chevron-only toggle tab on the left edge at ship level; no text until expanded.
  private void DrawMeterExpanderButton(Rectangle button, bool open)
  {
    bool hovered = GameplayInputEnabled && button.Contains(GameInput.UiCursor);
    Color pillFill = hovered
      ? GuiPalette.Base(new Color(14, 38, 60, 238))
      : open
        ? GuiPalette.Base(new Color(9, 28, 46, 224))
        : GuiPalette.Base(new Color(5, 16, 26, 190));

    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    var pixel = GuiSkin.Pixel(GraphicsDevice);
    m_spriteBatch.Draw(pixel, button, pillFill);
    DrawDamageChevron(new Vector2(button.Center.X, button.Center.Y), open,
      hovered ? Color.White : open ? GuiSkin.ProgressAccent : GuiSkin.ButtonTextColor * 0.85f);
    m_spriteBatch.End();
  }

  // Pointing right (the way the panel opens) when closed, left when open. Called inside a
  // SpriteBatch pass.
  private void DrawDamageChevron(Vector2 center, bool open, Color color)
  {
    const float arm = 16f, thickness = 3.5f;
    float tip = open ? MathHelper.Pi : 0f;
    var pointAt = center + new Vector2(MathF.Cos(tip), MathF.Sin(tip)) * arm * 0.35f;
    for (int side = -1; side <= 1; side += 2)
    {
      float angle = tip + MathHelper.Pi + side * 0.55f;
      m_spriteBatch.Draw(AssetManager.DefaultTexture, pointAt, null, color, angle, new Vector2(0f, 0.5f),
        new Vector2(arm, thickness), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
    }
  }
}
