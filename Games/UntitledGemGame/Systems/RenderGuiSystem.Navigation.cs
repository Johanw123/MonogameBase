using System;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  private bool panelNavigationGestureCaptured;
  private int panelNavigationHovered = -1;
  private int lastNavigationTab = HudLayout.UpgradesTab;

  private void DrawNavigationPanelBackground(SpriteBatch batch, Color accent)
  {
    var panel = PanelNavigationLayout.Panel;
    GuiSkin.Panel(batch, panel, header: false, accent);
    var watermark = PanelNavigationLayout.Watermark;
    GuiSkin.DrawScannerReticle(batch, watermark.Center, watermark.Width, accent, 0.2f);
    var icon = PanelNavigationIcon(batch.GraphicsDevice, CurrentNavigationTab);
    batch.Draw(icon, watermark.Center.ToVector2(), null, accent * 0.045f, 0,
      new Vector2(icon.Width, icon.Height) / 2, 240f / Math.Max(icon.Width, icon.Height), SpriteEffects.None, 0);
  }

  private bool PanelNavigationShown
  {
    get
    {
      if (!drawUpgradesGui || ChoosingTalents || GameMain.IsPaused
        || (IsDetached && !DrawingPopout && !IsPopoutFocused)) return false;
      return m_upgradeWindowType switch
      {
        UpgradeTypes.Upgrades or UpgradeTypes.Abilities => true,
        UpgradeTypes.Meta => TalentsReadOnly,
        UpgradeTypes.Shipyard => revealingModule == ShipModule.None,
        UpgradeTypes.Signals => !signalChoicesVisible && !IsSelectingSignal,
        _ => false
      };
    }
  }

  private int CurrentNavigationTab => m_upgradeWindowType switch
  {
    UpgradeTypes.Upgrades => HudLayout.UpgradesTab,
    UpgradeTypes.Meta => HudLayout.TalentsTab,
    UpgradeTypes.Abilities => HudLayout.SystemsTab,
    UpgradeTypes.Shipyard => HudLayout.ShipyardTab,
    _ => HudLayout.SignalsTab
  };

  private static string PanelNavigationLabel(int tab) => tab switch
  {
    HudLayout.UpgradesTab => Loc.T("Upgrades"),
    HudLayout.TalentsTab => Loc.T("Talents"),
    HudLayout.SystemsTab => ShipSystems.NavigationLabel,
    HudLayout.ShipyardTab => Loc.T("Shipyard"),
    _ => Loc.T("Signals")
  };

  private Texture2D PanelNavigationIcon(GraphicsDevice graphics, int tab) => tab switch
  {
    HudLayout.UpgradesTab => AssetManager.Load<Texture2D>("Textures/scifi_icons/icon_arrow/14_arrow.png"),
    HudLayout.TalentsTab => GemSurfaceTexture.PrestigeOrbIcon(),
    HudLayout.SystemsTab => PowerCells.IconTexture(),
    HudLayout.ShipyardTab => GuiSkin.GetTexture(graphics, "synty_map_ship"),
    _ => AssetManager.Load<Texture2D>("Textures/scifi_icons/icon_accuracy/14_accuracy.png")
  };

  private bool UpdatePanelNavigation()
  {
    var mouse = GameInput.Mouse;
    if (panelNavigationGestureCaptured)
    {
      if (mouse.IsButtonDown(MouseButton.Left)) return true;
      panelNavigationGestureCaptured = false;
    }
    if (!PanelNavigationShown)
    {
      panelNavigationHovered = -1;
      return false;
    }
    var cursor = SalvageCursor;
    int hovered = -1;
    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
      if (PanelNavigationLayout.Tab(tab).Contains(cursor)) { hovered = tab; break; }
    if (hovered != panelNavigationHovered && hovered >= 0 && CanOpenNavigation(hovered))
      AudioManager.Instance.PlaySound(SoundEvent.MenuHover);
    panelNavigationHovered = hovered;
    if (hovered < 0 || !mouse.WasButtonPressed(MouseButton.Left)) return false;

    // The whole gesture belongs to the tab strip, including disabled placeholders.
    panelNavigationGestureCaptured = true;
    PanelDismissInputCaptured = true;
    ClearGumCursorInput();
    if (hovered != CurrentNavigationTab && CanOpenNavigation(hovered))
      OpenNavigation(hovered);
    return true;
  }

  private void DrawPanelNavigation(SpriteBatch batch)
  {
    if (!PanelNavigationShown) return;
    var frame = PanelNavigationLayout.Panel;
    var accent = GuiSkin.Accent;
    var cursor = SalvageCursor;
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
    {
      var bounds = PanelNavigationLayout.Tab(tab);
      bool enabled = CanOpenNavigation(tab);
      bool selected = tab == CurrentNavigationTab;
      bool hovered = enabled && bounds.Contains(cursor);
      GuiSkin.NavigationTab(batch, bounds, accent, selected, hovered, enabled);
    }
    if (!IsDetached)
    {
      var close = GuiSkin.PanelCloseButtonBounds(frame, size: 56, marginX: 36, marginY: 24);
      GuiSkin.CloseButton(batch, close, accent, close.Contains(cursor));
    }
    batch.End();
    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
    {
      if (!CanOpenNavigation(tab)) continue; // Neither names nor feature icons reveal a locked panel.
      var bounds = PanelNavigationLayout.Tab(tab);
      bool selected = tab == CurrentNavigationTab;
      bool hovered = bounds.Contains(cursor);
      int topInset = selected ? 4 : hovered ? 8 : 12;
      var tabRect = new Rectangle(bounds.X, bounds.Y + topInset, bounds.Width, bounds.Height - topInset);
      string label = PanelNavigationLabel(tab);
      float fontSize = 28f;
      var measured = Measure2(label, Vector2.Zero, fontSize);
      if (measured.X > tabRect.Width - 36)
      {
        fontSize *= (tabRect.Width - 36) / measured.X;
        measured = Measure2(label, Vector2.Zero, fontSize);
      }
      Color textColor = selected ? Color.Lerp(accent, Color.White, 0.25f)
        : hovered ? GuiSkin.StatHeadingColor
        : GuiSkin.MutedTextColor;
      FontManager.RenderFieldFont(SystemFont, label,
        new Vector2(tabRect.Center.X - measured.X / 2f, tabRect.Center.Y - measured.Y / 2f + 2f),
        textColor, Color.Black, fontSize);
    }
  }

  public void ToggleNavigationPanel()
  {
    if (drawUpgradesGui)
    {
      lastNavigationTab = CurrentNavigationTab;
      SetUpgradeType(UpgradeTypes.None);
    }
    else
      OpenNavigation(CanOpenNavigation(lastNavigationTab) ? lastNavigationTab : HudLayout.UpgradesTab);
  }

  public bool CanOpenNavigation(int tab) => !ChoosingTalents && tab switch
  {
    HudLayout.UpgradesTab => true,
    HudLayout.TalentsTab => TalentsTabShown,
    HudLayout.SystemsTab => ShipSystems.Online,
    HudLayout.ShipyardTab => UpgradeManager.Instance.UGM.ShipyardUnlocked,
    HudLayout.SignalsTab => UpgradeManager.Instance.UGM.SignalsUnlocked,
    _ => false
  };

  // Both navigation locations open the same windows, including the read-only Talents view.
  public bool OpenNavigation(int tab)
  {
    if (!CanOpenNavigation(tab)) return false;
    lastNavigationTab = tab;
    switch (tab)
    {
      case HudLayout.UpgradesTab:
        SetUpgradeType(UpgradeTypes.Upgrades);
        m_animateButtonClickUpgrades = 0.001f;
        break;
      case HudLayout.TalentsTab:
        SetUpgradeType(UpgradeTypes.Meta);
        m_talentsReadOnly = true;
        m_animateButtonClickTalents = 0.001f;
        break;
      case HudLayout.SystemsTab:
        SetUpgradeType(UpgradeTypes.Abilities);
        m_animateButtonClickAbilities = 0.001f;
        break;
      case HudLayout.ShipyardTab:
        SetUpgradeType(UpgradeTypes.Shipyard);
        m_animateButtonClickShipyard = 0.001f;
        break;
      case HudLayout.SignalsTab:
        SetUpgradeType(UpgradeTypes.Signals);
        m_animateButtonClickSignals = 0.001f;
        break;
    }
    return true;
  }
}
