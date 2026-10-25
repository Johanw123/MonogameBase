using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AsyncContent;
using Gum;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using UntitledGemGame;
using UntitledGemGame.Entities;

// The Ship Systems window: one talent tab per system on a fixed panel. Every tab's nodes
// live side by side in tree space (ShipSystems.TabStride apart) and the camera frames
// the selected one, so the Gum buttons, links and borders need no per-tab visibility.
public partial class RenderGuiSystem
{
  private static readonly Regex RichTextMarkup = new(@"\[fill #[0-9A-Fa-f]{6}\]", RegexOptions.Compiled);
  private static readonly Regex TelemetryValueRegex = new(
    @"[+-]?\d+(?:[.,]\d+)?(?:\s*->\s*[+-]?\d+(?:[.,]\d+)?)?(?:%|s\b|x\b|/s\b)?",
    RegexOptions.Compiled);
  private int m_systemTab;
  private float m_animateSystemsRefund;
  private float m_animateBuyCell;

  public int SelectedSystemTab => m_systemTab;

  public void OpenShipSystems(int tab)
  {
    if (!ShipSystems.Online) return;
    m_systemTab = ShipSystems.Resolve(tab);
    if (m_upgradeWindowType == UpgradeTypes.Abilities) FrameSystemTab();
    else SetUpgradeType(UpgradeTypes.Abilities);
  }

  public void SelectSystemTab(int tab)
  {
    m_systemTab = ShipSystems.Resolve(tab);
    UpgradeManager.Instance.HideTooltip();
    if (m_upgradeWindowType == UpgradeTypes.Abilities) FrameSystemTab();
  }

  private void FrameSystemTab()
  {
    m_systemTab = ShipSystems.Resolve(m_systemTab);
    var camera = SystemManagers.Default.Renderer.Camera;
    targetZoom = camera.Zoom = 1f;
    camera.Position = new System.Numerics.Vector2(m_systemTab * ShipSystems.TabStride, 0);
    camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
    Renderer.UseBasicEffectRendering = false;
  }

  public static Rectangle ShipSystemsCloseButton
    => GuiSkin.PanelCloseButtonBounds(PanelNavigationLayout.Panel, size: 56, marginX: 36, marginY: 24);

  // The bar shows ShipSystems.VisibleTabs; index is the slot in it.
  private static Rectangle SystemTabBounds(int index) => PanelNavigationLayout.SelectionButton(index);

  private static Rectangle SystemsRefundBounds
    => new(ShipSystems.Readout.X + 40, ShipSystems.Readout.Bottom - 124, 400, 84);

  // Power cells are bought here, just above Refund all.
  private static Rectangle SystemsBuyCellBounds
    => new(ShipSystems.Readout.X + 40, SystemsRefundBounds.Y - 24 - PurchaseButtonHeight,
      400, PurchaseButtonHeight);

  private static GameState SystemsWallet => UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance?.State;

  // Also flags the HUD's Systems button.
  public bool CanBuyPowerCell => SystemsWallet?.CanBuyAbilityPoint == true
    && !UpgradeManager.Instance.UpdatingButtons && !UpgradeManager.Instance.UpgradeGuiEditMode;

  // GameInput, so capture scenes can click the window too.
  private static Point SystemsCursor => new((int)GameInput.UiCursor.X, (int)GameInput.UiCursor.Y);

  private void UpdateShipSystemsInput(float dt)
  {
    AdvanceButtonAnimation(ref m_animateSystemsRefund, dt);
    AdvanceButtonAnimation(ref m_animateBuyCell, dt);
    if (m_upgradeWindowType != UpgradeTypes.Abilities) return;
    // Resolve replacements before drawing captures the camera's tree transform.
    if (m_systemTab != ShipSystems.Resolve(m_systemTab))
    {
      UpgradeManager.Instance.HideTooltip();
      FrameSystemTab();
    }
    if (!GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    var cursor = SystemsCursor;
    var visible = ShipSystems.VisibleTabs;
    for (int slot = 0; slot < visible.Length; slot++)
      if (SystemTabBounds(slot).Contains(cursor) && visible[slot] != m_systemTab)
      {
        SelectSystemTab(visible[slot]);
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      }
    if (SystemsBuyCellBounds.Contains(cursor) && CanBuyPowerCell && SystemsWallet.TryBuyAbilityPoint())
    {
      m_animateBuyCell = 0.001f;
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance.SaveProgress();
    }
    if (SystemsRefundBounds.Contains(cursor) && UpgradeManager.Instance.CanRefundAllSystems)
    {
      UpgradeManager.Instance.RefundAllSystems();
      AudioManager.Instance.PlaySound(SoundEvent.UpgradeRefund);
      m_animateSystemsRefund = 0.001f;
    }
  }

  private void DrawShipSystemsPanel(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities;
    var tab = ShipSystems.Tabs[m_systemTab];
    var panel = ShipSystems.Panel;
    var cursor = SystemsCursor;
    var visible = ShipSystems.VisibleTabs;
    float time = (float)(JapeFramework.BaseGame.Time?.TotalGameTime.TotalSeconds ?? 0.0);
    int spineX = ShipSystems.RowLabelX + 8;

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawNavigationPanelBackground(batch, tab.Accent);
    if (!IsDetached)
    {
      var closeBtn = ShipSystemsCloseButton;
      GuiSkin.CloseButton(batch, closeBtn, tab.Accent, closeBtn.Contains(cursor));
    }

    int tabStripBottom = SystemTabBounds(0).Bottom + 8;
    // Clean vertical divider separating the tree workspace from the right-hand system readout
    DrawCleanVertDivider(batch, ShipSystems.Readout.X, ShipSystems.Readout.Y + 108, panel.Bottom - ShipSystems.Readout.Y - 140, tab.Accent);

    // Left vertical tier spine & subtle inter-tier rules (no TierBand boxes)
    for (int row = 0; row < tab.Rows.Length; row++)
    {
      bool open = ShipSystems.IsRowOpen(buttons, m_systemTab, row);
      int y = (int)ShipSystems.RowCenterY(row);
      var rowColor = open ? tab.Accent : GuiSkin.BorderColor * 0.55f;

      if (row > 0)
      {
        int prevY = (int)ShipSystems.RowCenterY(row - 1);
        batch.Draw(AssetManager.DefaultTexture,
          new Rectangle(spineX - 1, prevY + 12, 2, y - prevY - 24),
          open ? tab.Accent * 0.48f : GuiSkin.BorderColor * 0.24f);

        int midY = (prevY + y) / 2;
        batch.Draw(AssetManager.DefaultTexture,
          new Rectangle(490, midY, ShipSystems.Readout.X - 538, 1),
          open ? tab.Accent * 0.10f : GuiSkin.BorderColor * 0.07f);
      }

      // Horizontal tick from the spine toward the row label
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(spineX + 12, y - 1, 14, 2), rowColor * 0.55f);
      GuiSkin.DrawHexPip(batch, new Rectangle(spineX - 11, y - 11, 22, 22), rowColor, open);
    }

    // The same medallion, selected ring and compact spacing as the Shipyard selector.
    for (int slot = 0; slot < visible.Length; slot++)
    {
      int i = visible[slot];
      var bounds = SystemTabBounds(slot);
      var medallion = GuiSkin.SelectionMedallion(batch, bounds, i == m_systemTab, bounds.Contains(cursor));
      if (SystemIcon(i) is { } icon)
        batch.Draw(icon, new Rectangle(medallion.Center.X - 32, medallion.Center.Y - 32, 64, 64), Color.White);
    }

    // Power cell icon in the bottom-left footer, below the horizontal dividing line and left of the vertical divider
    int footerIconY = panel.Bottom - 61;
    PowerCells.DrawIcon(batch, new Rectangle(panel.X + 44, footerIconY, 24, 30));

    // Clean horizontal divider above the bottom footer
    DrawCleanHorizDivider(batch, panel.X + 44, panel.Bottom - 82, ShipSystems.Readout.X - panel.X - 92, tab.Accent * 0.5f);
    batch.End();

    var gameState = UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance?.State;
    ulong available = gameState?.CurrentBlueGemCount ?? 0;
    string cellsSummary = Loc.F("{0} available  •  {1} spent", CellCount(available), ShipSystems.Spent(buttons, m_systemTab));
    float cellsX = panel.X + 78;
    float footerTextY = panel.Bottom - 60;
    DrawFittedSystemText(cellsSummary, new Vector2(cellsX, footerTextY), 640, 26, GuiSkin.PanelAccent);

    for (int row = 0; row < tab.Rows.Length; row++)
    {
      float y = ShipSystems.RowCenterY(row);
      ulong spent = ShipSystems.Spent(buttons, m_systemTab, row);
      int required = ShipSystems.RowRequirement(row);
      bool open = spent >= (ulong)required;
      DrawFittedSystemText(row == 0 ? Loc.T("CORE") : Loc.F("TIER {0}", row), new Vector2(spineX + 30, y - 28), 240, 26,
        open ? tab.Accent : GuiSkin.MutedTextColor);
      DrawFittedSystemText(row == 0 ? Loc.T("Bring online") : open ? Loc.T("Open") : Loc.F("Spend {0} above  •  {1}/{0}", required, spent),
        new Vector2(spineX + 30, y + 4), 280, 19, open ? GuiSkin.ConfirmAccent : GuiSkin.MutedTextColor);
    }

    DrawSystemReadout(batch, tab, buttons);
    DrawBuyPowerCell(batch, cursor);

    var refund = SystemsRefundBounds;
    bool canRefund = UpgradeManager.Instance.CanRefundAllSystems;
    DrawHudButton(batch, refund, canRefund ? Loc.T("Refund all") : Loc.T("Nothing to refund"),
      GuiSkin.PanelAccent * (canRefund ? 1f : 0.45f), false, canRefund && refund.Contains(cursor), m_animateSystemsRefund);
    // Under the tree rather than in every talent's tooltip, right-aligned in the left-pane footer opposite the power cells summary.
    float cellsMeasuredW = Math.Min(640f, Measure2(cellsSummary, Vector2.Zero, 26).X);
    float hintLeft = cellsX + cellsMeasuredW + 40;
    float hintRight = ShipSystems.Readout.X - 48;
    float hintMaxW = Math.Max(320f, hintRight - hintLeft);
    string hintText = Loc.T("Right-click a talent to refund one rank. Cells and talents reset when you extract the core.");
    float hintSize = 22f;
    var hintMeasure = Measure2(hintText, Vector2.Zero, hintSize);
    if (hintMeasure.X > hintMaxW)
    {
      hintSize *= hintMaxW / hintMeasure.X;
      hintMeasure = Measure2(hintText, Vector2.Zero, hintSize);
    }
    FontManager.RenderFieldFont(SystemFont, hintText,
      new Vector2(hintRight - hintMeasure.X, footerTextY + 3), GuiSkin.MutedTextColor, Color.Black, hintSize);
  }

  private void DrawBuyPowerCell(SpriteBatch batch, Point cursor)
  {
    var wallet = SystemsWallet;
    if (wallet == null) return;
    var button = SystemsBuyCellBounds;
    bool available = CanBuyPowerCell;
    DrawPurchaseButton(batch, button, Loc.T("Buy +1 power cell"), wallet.NextAbilityPointPrice,
      available, button.Contains(cursor), m_animateBuyCell, Loc.T("Maximum reached"), PowerCells.IconTexture());
  }

  private void DrawSystemReadout(SpriteBatch batch, ShipSystems.Tab tab, Dictionary<string, UpgradeButton> buttons)
  {
    var box = ShipSystems.Readout;
    float x = box.X + 44, width = box.Width - 84, y = box.Y + 8;
    DrawFittedSystemText(Loc.Upper(Loc.T(tab.Name)), new Vector2(x, y), width, 42, tab.Accent);
    y += 58;
    var ability = HomeBase.Instance?.Abilities.FirstOrDefault(a => HomeBase.GetAbilityUpgradeId(a) == tab.Root);
    bool equipped = ability != null && HomeBase.Instance.ActiveAbilities.Contains(ability);
    string status = ability == null ? Loc.F("OFFLINE  •  learn {0} to bring it online", Loc.T(tab.Name))
      : equipped ? Loc.T("ONLINE  •  EQUIPPED") : Loc.T("ONLINE  •  NOT EQUIPPED");
    var statusColor = ability == null ? GuiSkin.LockedTextColor : GuiSkin.ConfirmAccent;

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.DrawHexPip(batch, new Rectangle((int)x, (int)y + 5, 16, 16), statusColor, active: ability != null);
    batch.End();
    DrawFittedSystemText(status, new Vector2(x + 24, y), width - 24, 22, statusColor);
    y += 42;

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, (int)x, (int)y, (int)width, tab.Accent);
    batch.End();
    y += 22;

    y = DrawWrappedSystemText(Loc.T(tab.Description), new Vector2(x, y), width, 26, GuiSkin.ButtonTextColor) + 28;

    DrawFittedSystemText(Loc.Upper(Loc.T("System Telemetry")), new Vector2(x, y), width, 20, tab.Accent * 0.9f);
    y += 30;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, (int)x, (int)y, (int)width, tab.Accent * 0.72f);
    DrawCleanHorizDivider(batch, (int)x, SystemsBuyCellBounds.Y - 24, (int)width, tab.Accent * 0.55f);
    batch.End();
    y += 22;

    if (ability == null)
    {
      DrawWrappedSystemText(Loc.T("Spend power cells on the core talent, then work down the tiers. You can't afford every capstone, so pick a path."),
        new Vector2(x, y), width, 24, GuiSkin.MutedTextColor);
      return;
    }
    string readout = RichTextMarkup.Replace(HomeBase.Instance.GetAbilityDescription(ability), "");
    foreach (string line in readout.Split('\n'))
    {
      if (y > SystemsBuyCellBounds.Y - 64) break;
      string trimmed = line.Trim();
      if (trimmed.Length == 0) continue;

      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.DrawHexPip(batch, new Rectangle((int)x, (int)y + 6, 14, 14), tab.Accent * 0.85f, active: true);
      batch.End();
      float nextY = DrawTelemetryLine(trimmed, new Vector2(x + 24, y), width - 24, 23);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle((int)x, (int)nextY + 4, (int)width, 1), tab.Accent * 0.12f);
      batch.End();
      y = nextY + 13;
    }
  }

  private void DrawShipSystemLabels(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities;
    var tab = ShipSystems.Tabs[m_systemTab];
    int offset = m_systemTab * ShipSystems.TabStride;
    var nodes = buttons.Values.Where(b => ShipSystems.TabOf(b.Data.ShortName) == m_systemTab).ToList();

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    foreach (var button in nodes)
      if (button.State == UpgradeButton.UnlockState.Revealed)
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(button.Data.PosX - offset, button.Data.PosY,
          (int)button.Button.Width, (int)button.Button.Height), GuiPalette.Base(new Color(3, 7, 12, 175)));
    batch.End();

    // Labels sit beside the nodes: links run vertically through the node centers. They never
    // overlap, so all of them share one stroke and one fill pass.
    FontManager.BeginFieldFonts(SystemFont);
    foreach (var button in nodes)
    {
      bool locked = button.State == UpgradeButton.UnlockState.Revealed;
      bool capstone = ShipSystems.IsCapstone(button);
      float left = button.Data.PosX - offset + button.Button.Width + 16;
      float middle = button.Data.PosY + button.Button.Height / 2f;
      float width = ShipSystems.ColumnSpacing - button.Button.Width - 36;
      LayoutFittedSystemText(Loc.T(button.Data.UpgradeDefinition.Name), new Vector2(left, middle - 30), width, capstone ? 24 : 22,
        locked ? GuiSkin.MutedTextColor : capstone ? tab.Accent : GuiSkin.ButtonTextColor);
      ulong cost = button.GetNextLevelCost();
      string rank = button.IsMaxLevel ? Loc.F("MAX  {0}/{1}", button.CurrentLevel, button.Data.NumLevels)
        : Loc.P((long)cost, "{0} cell", "{0} cells") + $"  •  {button.CurrentLevel}/{button.Data.NumLevels}";
      LayoutFittedSystemText(rank, new Vector2(left, middle + 2), width, 19, locked ? GuiSkin.MutedTextColor : button.IsMaxLevel ? GuiSkin.ConfirmAccent : tab.Accent);
    }
    FontManager.EndFieldFonts(SystemFont);
  }

  // Drawn every frame, so look each icon up once.
  private readonly Dictionary<string, Texture2D> systemIcons = new();

  private Texture2D SystemIcon(int tab)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities;
    string path = buttons.TryGetValue(ShipSystems.Tabs[tab].Root, out var root) ? root.Data.UpgradeDefinition.Icon : null;
    if (string.IsNullOrEmpty(path)) return null;
    if (!systemIcons.TryGetValue(path, out var icon))
      systemIcons[path] = icon = AssetManager.Load<Texture2D>(path);
    return icon;
  }

  // Below 1000 the abbreviation is the plain number, so Loc.P picks the plural form from it.
  // Larger counts show abbreviated ("1.50K"), which languages count like decimals: their
  // own key, translated with the form for fractional amounts.
  private static string CellCount(ulong cells)
    => cells < 1000 ? Loc.P((long)cells, "{0} power cell", "{0} power cells")
      : Loc.F("{0} power cells", NumberFormatter.AbbreviateBigNumber(cells));

  private const string SystemFont = nameof(ContentDirectory.Fonts.Roboto_Regular_ttf);

  private void DrawFittedSystemText(string text, Vector2 position, float width, float size, Color color)
  {
    var measured = Measure2(text, Vector2.Zero, size);
    if (measured.X > width) size *= width / measured.X;
    FontManager.RenderFieldFont(SystemFont, text, position, color, Color.Black, size);
  }

  // DrawFittedSystemText inside FontManager.BeginFieldFonts/EndFieldFonts.
  private void LayoutFittedSystemText(string text, Vector2 position, float width, float size, Color color)
  {
    var measured = Measure2(text, Vector2.Zero, size);
    if (measured.X > width) size *= width / measured.X;
    FontManager.LayoutFieldFont(SystemFont, text, position, color, Color.Black, size);
  }

  // Returns the y below the last line.
  private float DrawWrappedSystemText(string text, Vector2 position, float width, float size, Color color)
  {
    float lineHeight = size * 1.3f;
    string line = "";
    foreach (var (word, join) in Loc.WrapPieces(text))
    {
      string candidate = line.Length == 0 ? word : line + join + word;
      if (line.Length > 0 && Measure2(candidate, Vector2.Zero, size).X > width)
      {
        FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf), line, position, color, Color.Black, size);
        position.Y += lineHeight;
        line = word;
      }
      else line = candidate;
    }
    if (line.Length > 0)
    {
      FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf), line, position, color, Color.Black, size);
      position.Y += lineHeight;
    }
    return position.Y;
  }

  private float DrawTelemetryLine(string text, Vector2 position, float width, float size)
  {
    int colon = text.IndexOf(':');
    int labelEnd = colon > 0 && colon < 40 && !text[..colon].Contains('(') ? colon + 1 : 0;

    float lineHeight = size * 1.3f;
    string line = "";
    int lineOffset = 0;
    int currentOffset = 0;
    foreach (var (word, join) in Loc.WrapPieces(text))
    {
      string candidate = line.Length == 0 ? word : line + join + word;
      if (line.Length > 0 && Measure2(candidate, Vector2.Zero, size).X > width)
      {
        RenderColorizedTelemetryLine(line, lineOffset, labelEnd, position, size);
        position.Y += lineHeight;
        lineOffset = currentOffset + join.Length;
        line = word;
      }
      else
      {
        line = candidate;
      }
      currentOffset += (currentOffset == 0 ? 0 : join.Length) + word.Length;
    }
    if (line.Length > 0)
    {
      RenderColorizedTelemetryLine(line, lineOffset, labelEnd, position, size);
      position.Y += lineHeight;
    }
    return position.Y;
  }

  private void RenderColorizedTelemetryLine(string line, int lineOffset, int labelEnd, Vector2 position, float size)
  {
    if (string.IsNullOrWhiteSpace(line)) return;

    Color labelColor = GuiSkin.StatHeadingColor;
    Color bodyColor = Color.Lerp(GuiSkin.MutedTextColor, GuiSkin.ButtonTextColor, 0.2f);
    Color valueColor = GuiSkin.Accent;

    int localLabelEnd = Math.Clamp(labelEnd - lineOffset, 0, line.Length);
    FontManager.BeginFieldFonts(SystemFont);

    void EmitSpan(int start, int end, Color color)
    {
      while (start < end && char.IsWhiteSpace(line[start])) start++;
      while (end > start && char.IsWhiteSpace(line[end - 1])) end--;
      if (start >= end) return;

      string spanText = line[start..end];
      float spanX = start == 0
        ? position.X
        : position.X + Measure2(line[..end], Vector2.Zero, size).X - Measure2(spanText, Vector2.Zero, size).X;
      FontManager.LayoutFieldFont(SystemFont, spanText, new Vector2(spanX, position.Y), color, Color.Black, size);
    }

    if (localLabelEnd > 0)
      EmitSpan(0, localLabelEnd, labelColor);

    int cursor = localLabelEnd;
    foreach (Match match in TelemetryValueRegex.Matches(line, localLabelEnd))
    {
      if (match.Length == 0) continue;
      if (match.Index > 0 && char.IsLetter(line[match.Index - 1])) continue;
      if (match.Index > cursor)
        EmitSpan(cursor, match.Index, bodyColor);
      EmitSpan(match.Index, match.Index + match.Length, valueColor);
      cursor = match.Index + match.Length;
    }
    if (cursor < line.Length)
      EmitSpan(cursor, line.Length, bodyColor);

    FontManager.EndFieldFonts(SystemFont);
  }
}
