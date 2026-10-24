using System;
using Apos.Shapes;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  private readonly ShapeBatch panelNavigationShapes;
  private bool panelNavigationGestureCaptured;
  private int panelNavigationHovered = -1;
  private int lastNavigationTab = HudLayout.UpgradesTab;

  private void DrawNavigationPanelBackground(SpriteBatch batch, Color accent)
  {
    var panel = PanelNavigationLayout.Panel;
    GuiSkin.Panel(batch, panel, header: false, accent);
    JoinPanelTabToFrame(batch);
    var watermark = PanelNavigationLayout.Watermark;
    GuiSkin.DrawScannerReticle(batch, watermark.Center, watermark.Width, accent, 0.2f);
    var icon = PanelNavigationIcon(batch.GraphicsDevice, CurrentNavigationTab);
    batch.Draw(icon, watermark.Center.ToVector2(), null, accent * 0.045f, 0,
      new Vector2(icon.Width, icon.Height) / 2, 240f / Math.Max(icon.Width, icon.Height), SpriteEffects.None, 0);
  }

  private void JoinPanelTabToFrame(SpriteBatch batch)
  {
    if (!PanelNavigationShown) return;
    var active = PanelNavigationLayout.Tab(CurrentNavigationTab);
    // Clear the entire textured top bevel before content is drawn. A small mask
    // after content left a strip of the frame underneath the selected tab.
    batch.Draw(GuiSkin.Pixel(batch.GraphicsDevice),
      new Rectangle(active.X + 2, PanelNavigationLayout.Panel.Top - 6, active.Width - 4, 34), GuiSkin.PanelColor);
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
    var device = batch.GraphicsDevice;
    var previousScissor = device.ScissorRectangle;
    var viewport = device.Viewport;
    device.ScissorRectangle = ToScissorRectangle(device, new Rectangle(0, 0, HudLayout.Width, frame.Top));
    panelNavigationShapes.Begin(HudLayout.RenderTransform,
      Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1),
      blendState: BlendState.AlphaBlend, rasterizerState: ScissorRasterizer);
    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
    {
      var bounds = PanelNavigationLayout.Tab(tab);
      bool enabled = CanOpenNavigation(tab);
      bool selected = tab == CurrentNavigationTab;
      bool hovered = enabled && bounds.Contains(cursor);
      DrawRaisedPanelTab(bounds, selected, hovered, enabled);
    }
    panelNavigationShapes.End();
    device.ScissorRectangle = previousScissor;

    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    for (int tab = 0; tab < PanelNavigationLayout.TabCount; tab++)
    {
      var bounds = PanelNavigationLayout.Tab(tab);
      bool enabled = CanOpenNavigation(tab);
      bool selected = tab == CurrentNavigationTab;

      var icon = enabled ? PanelNavigationIcon(batch.GraphicsDevice, tab)
        : AssetManager.Load<Texture2D>("Textures/icons_set/icons_128/lock.png");
      float size = enabled ? 38 : 48;
      var position = enabled ? new Vector2(bounds.X + 36, bounds.Center.Y) : bounds.Center.ToVector2();
      batch.Draw(icon, position, null, Color.White * (enabled ? 1f : 0.42f), 0,
        new Vector2(icon.Width, icon.Height) / 2, size / Math.Max(icon.Width, icon.Height), SpriteEffects.None, 0);
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
      DrawFittedSystemText(PanelNavigationLabel(tab), new Vector2(bounds.X + 70, bounds.Center.Y - 18), bounds.Width - 88, 30,
        selected || bounds.Contains(cursor) ? Color.White : GuiSkin.ButtonTextColor);
    }
  }

  private void DrawRaisedPanelTab(Rectangle bounds, bool selected, bool hovered, bool enabled)
  {
    if (!selected) bounds.Y += 8;
    Color fill = selected ? GuiSkin.PanelColor : hovered ? GuiSkin.ButtonHoverColor
      : GuiPalette.Base(new Color(5, 12, 20));
    Color border = selected ? GuiSkin.Accent : hovered ? GuiSkin.BorderColor : GuiSkin.BorderColor * (enabled ? 0.65f : 0.4f);
    // Clip at the panel's top: only the smooth upper corners and straight sides
    // remain, with no lower bevels or protruding feet at the join.
    panelNavigationShapes.DrawRectangle(new Vector2(bounds.X, bounds.Y),
      new Vector2(bounds.Width, PanelNavigationLayout.Panel.Top - bounds.Y + 24),
      fill, border, selected ? 2.5f : 2, rounded: 10);
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
