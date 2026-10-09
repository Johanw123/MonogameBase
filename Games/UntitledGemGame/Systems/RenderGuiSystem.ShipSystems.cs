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
    var camera = SystemManagers.Default.Renderer.Camera;
    targetZoom = camera.Zoom = 1f;
    camera.Position = new System.Numerics.Vector2(m_systemTab * ShipSystems.TabStride, 0);
    camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
    Renderer.UseBasicEffectRendering = false;
  }

  // The bar shows ShipSystems.VisibleTabs; index is the slot in it.
  private static Rectangle SystemTabBounds(int index)
  {
    var panel = ShipSystems.Panel;
    int count = ShipSystems.VisibleTabs.Length;
    int width = Math.Min(520, (panel.Width - 80 - (count - 1) * 16) / count);
    return new Rectangle(panel.X + 40 + index * (width + 16), panel.Y + 24, width, 104);
  }

  private static Rectangle SystemsRefundBounds
    => new(ShipSystems.Readout.X + 40, ShipSystems.Readout.Bottom - 124, 400, 84);

  // Power cells are bought here, just above Refund all.
  private static Rectangle SystemsBuyCellBounds
    => new(ShipSystems.Readout.X + 40, SystemsRefundBounds.Y - 24 - HudLayout.ProgressPanelHeight,
      ShipSystems.Readout.Width - 80, HudLayout.ProgressPanelHeight);

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
    if (m_upgradeWindowType != UpgradeTypes.Abilities
      || !GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    var cursor = SystemsCursor;
    var visible = ShipSystems.VisibleTabs;
    for (int slot = 0; slot < visible.Length; slot++)
      if (SystemTabBounds(slot).Contains(cursor) && visible[slot] != m_systemTab)
      {
        SelectSystemTab(visible[slot]);
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      }
    if (SystemsBuyCellBounds.Contains(cursor) && CanBuyPowerCell && SystemsWallet.TryBuyAbilityPoint())
    {
      m_animateBuyCell = 0.001f;
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance.SaveProgress();
    }
    if (SystemsRefundBounds.Contains(cursor) && UpgradeManager.Instance.CanRefundAllSystems)
    {
      UpgradeManager.Instance.RefundAllSystems();
      m_animateSystemsRefund = 0.001f;
    }
  }

  private void DrawShipSystemsPanel(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities;
    // A talent bought since the panel was opened can swap the selected system out.
    m_systemTab = ShipSystems.Resolve(m_systemTab);
    var tab = ShipSystems.Tabs[m_systemTab];
    var panel = ShipSystems.Panel;
    var cursor = SystemsCursor;
    var visible = ShipSystems.VisibleTabs;

    batch.Begin();
    OrbitSkin.Panel(batch, panel);
    for (int row = 0; row < tab.Rows.Length; row++)
    {
      bool open = ShipSystems.IsRowOpen(buttons, m_systemTab, row);
      float y = ShipSystems.RowCenterY(row);
      var band = new Rectangle(panel.X + 40, (int)y - 66, ShipSystems.Readout.X - panel.X - 80, 182);
      batch.Draw(AssetManager.DefaultTexture, band, open ? new Color(16, 30, 38, 205) : new Color(8, 12, 18, 205));
      OrbitSkin.NineSlice(batch, "modal_info_complete", band, 8, open ? 0.6f : 0.25f);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(band.X, band.Y, HudLayout.ButtonBorderThickness, band.Height),
        open ? tab.Accent * 0.8f : OrbitSkin.BorderColor * 0.45f);
    }
    OrbitSkin.NineSlice(batch, "modal_info_complete", ShipSystems.Readout, 8, 0.75f);
    for (int slot = 0; slot < visible.Length; slot++)
    {
      int i = visible[slot];
      var bounds = SystemTabBounds(slot);
      bool selected = i == m_systemTab, hovered = bounds.Contains(cursor);
      OrbitSkin.Button(batch, bounds, selected || hovered, 0, true);
      int thickness = HudLayout.ButtonBorderThickness;
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(bounds.X, bounds.Bottom - thickness * 3, bounds.Width, thickness * 3), OrbitSkin.PanelColor);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness),
        selected ? ShipSystems.Tabs[i].Accent : hovered ? ShipSystems.Tabs[i].Accent * 0.6f : OrbitSkin.BorderColor);
      if (SystemIcon(i) is { } icon)
        batch.Draw(icon, new Rectangle(bounds.X + 20, bounds.Y + 20, 64, 64),
          selected ? ShipSystems.Tabs[i].Accent : OrbitSkin.MutedTextColor);
    }
    batch.End();

    for (int slot = 0; slot < visible.Length; slot++)
    {
      int i = visible[slot];
      var bounds = SystemTabBounds(slot);
      var system = ShipSystems.Tabs[i];
      bool selected = i == m_systemTab;
      ulong spent = ShipSystems.Spent(buttons, i);
      DrawFittedSystemText(Loc.T(system.Name), new Vector2(bounds.X + 104, bounds.Y + 14), bounds.Width - 124, 32,
        selected ? system.Accent : OrbitSkin.ButtonTextColor);
      DrawFittedSystemText(spent == 0 ? Loc.T("Offline") : Loc.F("Online  •  {0}", CellCount(spent)),
        new Vector2(bounds.X + 104, bounds.Y + 58), bounds.Width - 124, 22,
        spent == 0 ? OrbitSkin.MutedTextColor : OrbitSkin.ConfirmAccent);
    }

    var gameState = UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance?.State;
    ulong available = gameState?.CurrentBlueGemCount ?? 0;
    DrawFittedSystemText(Loc.F("{0} available  •  {1} spent in {2}", CellCount(available),
        ShipSystems.Spent(buttons, m_systemTab), Loc.T(tab.Name)),
      new Vector2(panel.X + 44, panel.Y + 152), ShipSystems.Readout.X - panel.X - 84, 30, OrbitSkin.AbilityAccent);

    for (int row = 0; row < tab.Rows.Length; row++)
    {
      float y = ShipSystems.RowCenterY(row);
      ulong spent = ShipSystems.Spent(buttons, m_systemTab, row);
      int required = ShipSystems.RowRequirement(row);
      bool open = spent >= (ulong)required;
      DrawFittedSystemText(row == 0 ? Loc.T("CORE") : Loc.F("TIER {0}", row), new Vector2(ShipSystems.RowLabelX, y - 40), 260, 28,
        open ? tab.Accent : OrbitSkin.MutedTextColor);
      DrawFittedSystemText(row == 0 ? Loc.T("Bring online") : open ? Loc.T("Open") : Loc.F("Spend {0} above  •  {1}/{0}", required, spent),
        new Vector2(ShipSystems.RowLabelX, y + 2), 300, 20, open ? OrbitSkin.ButtonTextColor : OrbitSkin.LockedTextColor);
    }

    DrawSystemReadout(batch, tab, buttons);
    DrawBuyPowerCell(batch, cursor);

    var refund = SystemsRefundBounds;
    bool canRefund = UpgradeManager.Instance.CanRefundAllSystems;
    DrawHudButton(batch, refund, canRefund ? Loc.T("Refund all") : Loc.T("Nothing to refund"),
      OrbitSkin.AbilityAccent * (canRefund ? 1f : 0.45f), false, canRefund && refund.Contains(cursor), m_animateSystemsRefund);
    DrawWrappedSystemText(Loc.T("Right-click a talent to refund one rank. Cells and talents reset when you extract the core."),
      new Vector2(refund.Right + 32, refund.Y + 4), ShipSystems.Readout.Right - refund.Right - 72, 22, OrbitSkin.MutedTextColor);
  }

  // Laid out like the HUD's progress panels: a title, the way to the next cell's price, its status.
  private void DrawBuyPowerCell(SpriteBatch batch, Point cursor)
  {
    var wallet = SystemsWallet;
    if (wallet == null) return;
    var panel = SystemsBuyCellBounds;
    ulong? price = wallet.NextAbilityPointPrice;
    bool available = CanBuyPowerCell;
    float progress = price is ulong target ? (float)Math.Min(1d, (double)wallet.CurrentRedGemCount / target) : 1f;
    float pulse = m_animateBuyCell > 0 ? MathF.Sin(Math.Clamp(m_animateBuyCell, 0f, 1f) * MathHelper.Pi) : 0f;
    int padding = HudLayout.ProgressPanelPadding;
    int contentWidth = panel.Width - padding * 2;

    batch.Begin();
    OrbitSkin.Button(batch, panel, available && panel.Contains(cursor), pulse, confirm: available);
    OrbitSkin.Progress(batch, new Rectangle(panel.X + padding, panel.Y + HudLayout.ProgressBarTop, contentWidth, 8), progress);
    batch.End();

    DrawFittedSystemText(Loc.T("Buy +1 power cell"), new Vector2(panel.X + padding, panel.Y + HudLayout.ProgressTitleTop),
      contentWidth, 36, available ? Color.White : OrbitSkin.AbilityAccent);
    string status = price is ulong next
      ? available
        ? Loc.F("Ready · {0} gems", NumberFormatter.AbbreviateBigNumber(next))
        : Loc.F("Cost: {0} gems", NumberFormatter.AbbreviateBigNumber(next))
      : Loc.T("Maximum reached");
    DrawFittedSystemText(status, new Vector2(panel.X + padding, panel.Y + HudLayout.ProgressStatusTop),
      contentWidth, 32, available ? Color.White : OrbitSkin.MutedTextColor);
  }

  private void DrawSystemReadout(SpriteBatch batch, ShipSystems.Tab tab, Dictionary<string, UpgradeButton> buttons)
  {
    var box = ShipSystems.Readout;
    float x = box.X + 40, width = box.Width - 80, y = box.Y + 32;
    DrawFittedSystemText(Loc.Upper(Loc.T(tab.Name)), new Vector2(x, y), width, 44, tab.Accent);
    y += 70;
    var ability = HomeBase.Instance?.Abilities.FirstOrDefault(a => HomeBase.GetAbilityUpgradeId(a) == tab.Root);
    bool equipped = ability != null && HomeBase.Instance.ActiveAbilities.Contains(ability);
    string status = ability == null ? Loc.F("OFFLINE  •  learn {0} to bring it online", Loc.T(tab.Name))
      : equipped ? Loc.T("ONLINE  •  EQUIPPED") : Loc.T("ONLINE  •  NOT EQUIPPED");
    DrawFittedSystemText(status, new Vector2(x, y), width, 24, ability == null ? OrbitSkin.LockedTextColor : OrbitSkin.ConfirmAccent);
    y += 52;
    y = DrawWrappedSystemText(Loc.T(tab.Description), new Vector2(x, y), width, 28, OrbitSkin.ButtonTextColor) + 24;
    batch.Begin();
    batch.Draw(AssetManager.DefaultTexture,
      new Rectangle((int)x, (int)y, (int)width, HudLayout.ButtonBorderThickness), tab.Accent * 0.45f);
    batch.End();
    y += 28;
    if (ability == null)
    {
      DrawWrappedSystemText(Loc.T("Spend power cells on the core talent, then work down the tiers. You can't afford every capstone, so pick a path."),
        new Vector2(x, y), width, 24, OrbitSkin.MutedTextColor);
      return;
    }
    string readout = RichTextMarkup.Replace(HomeBase.Instance.GetAbilityDescription(ability), "");
    foreach (string line in readout.Split('\n'))
    {
      if (y > SystemsBuyCellBounds.Y - 60) break;
      if (line.Length == 0) { y += 14; continue; }
      y = DrawWrappedSystemText(line, new Vector2(x, y), width, 24, OrbitSkin.StatHeadingColor) + 6;
    }
  }

  private void DrawShipSystemLabels(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities;
    var tab = ShipSystems.Tabs[m_systemTab];
    int offset = m_systemTab * ShipSystems.TabStride;
    var nodes = buttons.Values.Where(b => ShipSystems.TabOf(b.Data.ShortName) == m_systemTab).ToList();

    batch.Begin();
    foreach (var button in nodes)
      if (button.State == UpgradeButton.UnlockState.Revealed)
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(button.Data.PosX - offset, button.Data.PosY,
          (int)button.Button.Width, (int)button.Button.Height), new Color(3, 7, 12, 175));
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
        locked ? OrbitSkin.MutedTextColor : capstone ? tab.Accent : OrbitSkin.ButtonTextColor);
      ulong cost = button.GetNextLevelCost();
      string rank = button.IsMaxLevel ? Loc.F("MAX  {0}/{1}", button.CurrentLevel, button.Data.NumLevels)
        : Loc.P((long)cost, "{0} cell", "{0} cells") + $"  •  {button.CurrentLevel}/{button.Data.NumLevels}";
      LayoutFittedSystemText(rank, new Vector2(left, middle + 2), width, 19, locked ? OrbitSkin.LockedTextColor : tab.Accent);
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
}
