using System;
using System.Linq;
using AsyncContent;
using Gum;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using UntitledGemGame;
using UntitledGemGame.Screens;

// Shipyard-style weapon selector, a clean hull diagram whose mounts grow borderless
// callouts as weapons are fitted, and the selected weapon's upgrade tree.
// Mount indices: 0 is the fixed bow, 1-4 are hardpoints I-IV (matching support indices / 3).
public partial class RenderGuiSystem
{
  private int selectedWeaponTab;
  private MainShipWeapon? draggedWeapon;
  private int draggedWeaponSourceSlot = -1;
  private WeaponSupportModule draggedSupport;
  private int draggedSupportSource = -1;
  private Vector2 weaponPressPosition;
  private bool weaponDragging;
  private string weaponHoverTitle, weaponHoverBody, weaponHoverHint;
  private Color weaponHoverColor;
  public int SelectedWeaponTab => selectedWeaponTab;
  private bool SelectedWeaponUnlocked => UpgradeManager.Instance.IsWeaponBlueprintUnlocked(Weaponry.Tabs[selectedWeaponTab].Weapon);
  private static WeaponryLoadout WeaponLoadout => UpgradeManager.Instance.Weapons;
  public static Rectangle WeaponryCloseButton
    => GuiSkin.PanelCloseButtonBounds(PanelNavigationLayout.Panel, size: 56, marginX: 36, marginY: 24);
  private static readonly string[] HardpointNumerals = ["I", "II", "III", "IV"];

  public void OpenWeaponry(int tab = -1)
  {
    if (!UpgradeManager.Instance.UG.HomeBase) return;
    if (tab >= 0 && tab < Weaponry.Tabs.Length)
      selectedWeaponTab = tab;
    if (m_upgradeWindowType == UpgradeTypes.Weaponry) FrameWeaponTab();
    else SetUpgradeType(UpgradeTypes.Weaponry);
  }

  public void SelectWeaponTab(int tab)
  {
    selectedWeaponTab = Math.Clamp(tab, 0, Weaponry.Tabs.Length - 1);
    UpgradeManager.Instance.HideTooltip();
    if (m_upgradeWindowType == UpgradeTypes.Weaponry) FrameWeaponTab();
  }

  private void FrameWeaponTab()
  {
    selectedWeaponTab = Math.Clamp(selectedWeaponTab, 0, Weaponry.Tabs.Length - 1);
    if (UpgradeManager.Instance.m_upgradesWindowWeaponry is { } window)
      window.IsVisible = m_upgradeWindowType == UpgradeTypes.Weaponry && SelectedWeaponUnlocked;
    var camera = SystemManagers.Default.Renderer.Camera;
    targetZoom = camera.Zoom = 1f;
    camera.Position = new System.Numerics.Vector2(selectedWeaponTab * Weaponry.TabStride, 0);
    camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
    Renderer.UseBasicEffectRendering = false;
  }


  private void CancelWeaponDrag()
  {
    draggedWeapon = null;
    draggedWeaponSourceSlot = -1;
    draggedSupport = WeaponSupportModule.None;
    draggedSupportSource = -1;
    weaponDragging = false;
  }

  // ---------------------------------------------------------------- layout

  // The same top-left medallion row as the Shipyard and Systems selectors.
  public static Rectangle WeaponBlueprintCardBounds(int index) => PanelNavigationLayout.SelectionButton(index);

  private const int WeaponBottomTop = 1316;
  private const int WeaponInventorySocket = 136;

  private static Rectangle WeaponModuleInventoryBounds(int index)
    => new(Weaponry.ShowcaseColumn.X + index * (WeaponInventorySocket + 40), WeaponBottomTop + 84,
      WeaponInventorySocket, WeaponInventorySocket);

  private static int WeaponDescriptionY => PanelNavigationLayout.SelectionSubtitleY + 58;

  // Below the four-line description in the left column.
  private static Rectangle WeaponUnlockButtonBounds
    => new((int)PanelNavigationLayout.SelectionHeading.X, WeaponDescriptionY + 4 * 38 + 36, 600, PurchaseButtonHeight);

  private static Vector2 ShowcaseShipPivot => new(Weaponry.ShowcaseColumn.X + 1400, 1190);
  private const float ShowcaseTexelScale = 2.1f;
  private static float ShowcaseHullUnitScale => ShowcaseTexelScale / PrerenderedShips.HomeBaseScale;
  private static Vector2 HullToShowcaseScreen(Vector2 hullMount) => ShowcaseShipPivot + hullMount * ShowcaseHullUnitScale;

  // Callouts: a short stub from the mount, up to an underline that carries the name and sockets.
  private const int MountBoxSize = 80;
  private const int CalloutOffsetX = 440;
  private const int CalloutWidth = 330;
  private const int CalloutRise = 64;
  private const int CalloutNameSize = 30;
  private const int CalloutSocket = 76;
  private const int CalloutSocketGap = 12;

  private static Vector2 MountPoint(int mount) => HullToShowcaseScreen(mount == 0
    ? Weaponry.PrimaryBowMountRight : Weaponry.HardpointSlots[mount - 1].HullMount);

  private static Rectangle MountBox(Vector2 point)
    => new((int)MathF.Round(point.X) - MountBoxSize / 2, (int)MathF.Round(point.Y) - MountBoxSize / 2, MountBoxSize, MountBoxSize);

  private static Rectangle MountBox(int mount) => MountBox(MountPoint(mount));

  // Hardpoints I and III run their callouts to the port side; the bow and II, IV to starboard.
  private static bool CalloutOnLeft(int mount) => mount is 1 or 3;

  private static int CalloutUnderlineY(int mount) => (int)MathF.Round(MountPoint(mount).Y) - CalloutRise;

  private static Rectangle CalloutBounds(int mount)
  {
    int underline = CalloutUnderlineY(mount);
    int x = CalloutOnLeft(mount)
      ? (int)ShowcaseShipPivot.X - CalloutOffsetX - CalloutWidth
      : (int)ShowcaseShipPivot.X + CalloutOffsetX;
    return new Rectangle(x, underline - 48, CalloutWidth, 48 + 14 + CalloutSocket + 6);
  }

  private static bool HardpointLocked(int mount) => mount > 0 && mount - 1 >= WeaponLoadout.UnlockedHardpoints;

  private static MainShipWeapon? MountWeapon(int mount) => mount == 0 ? MainShipWeapon.Cannon : WeaponLoadout.Slots[mount - 1];

  private static bool MountHasSupports(int mount)
  {
    for (int i = 0; i < WeaponSupportCatalog.SlotsPerMount; i++)
      if (WeaponLoadout.Supports[mount * WeaponSupportCatalog.SlotsPerMount + i] != WeaponSupportModule.None) return true;
    return false;
  }

  // A mount shows its callout (name and sockets) only once it carries something.
  private static bool MountCalloutShown(int mount)
    => !HardpointLocked(mount) && (MountWeapon(mount) != null || MountHasSupports(mount));

  private bool DraggingWeapon => weaponDragging && draggedWeapon != null;

  // While a weapon is dragged, empty hardpoints show a ghost callout to drop onto.
  private bool MountGhostShown(int mount) => DraggingWeapon && mount > 0 && !HardpointLocked(mount) && !MountCalloutShown(mount);

  private bool MountContains(int mount, Point point)
  {
    var box = MountBox(mount);
    box.Inflate(16, 16);
    if (box.Contains(point)) return true;
    if (mount == 0)
    {
      var left = MountBox(HullToShowcaseScreen(Weaponry.PrimaryBowMountLeft));
      left.Inflate(16, 16);
      if (left.Contains(point)) return true;
    }
    return (MountCalloutShown(mount) || MountGhostShown(mount)) && CalloutBounds(mount).Contains(point);
  }

  private static Rectangle WeaponSupportBounds(int index)
  {
    int mount = index / WeaponSupportCatalog.SlotsPerMount;
    int i = index % WeaponSupportCatalog.SlotsPerMount;
    var callout = CalloutBounds(mount);
    int stride = CalloutSocket + CalloutSocketGap;
    int x = CalloutOnLeft(mount)
      ? callout.Right - CalloutSocket - (WeaponSupportCatalog.SlotsPerMount - 1 - i) * stride
      : callout.X + i * stride;
    return new Rectangle(x, CalloutUnderlineY(mount) + 14, CalloutSocket, CalloutSocket);
  }

  private static bool SupportShown(int index) => MountCalloutShown(index / WeaponSupportCatalog.SlotsPerMount);

  // Capture target for a hardpoint (slot 0-3).
  public static Rectangle WeaponHardpointBounds(int slotIndex) => MountBox(slotIndex + 1);

  // ---------------------------------------------------------------- actions

  private static bool CanAffordUpgradeButton(UpgradeButton button)
  {
    var state = UntitledGemGameGameScreen.Instance?.State;
    if (state == null || button == null || button.IsMaxLevel) return false;
    ulong cost = button.GetNextLevelCost();
    if (button.Data.UpgradeDefinition.Currency == CoreShards.Currency)
      return state.CurrentCoreShardCount >= cost;
    return state.CurrentRedGemCount >= cost;
  }

  private bool TryUnlockWeaponBlueprint(MainShipWeapon weapon)
  {
    if (weapon == MainShipWeapon.Cannon || !Weaponry.IsAvailable(weapon)) return false;
    string rootKey = Weaponry.ForWeapon(weapon).Root;
    if (!UpgradeManager.CurrentUpgrades.UpgradeButtons.TryGetValue(rootKey, out var rootButton))
      return false;
    if (rootButton.CurrentLevel > 0 || !CanAffordUpgradeButton(rootButton))
      return false;

    UpgradeManager.Instance.Upgrade(rootButton);
    FrameWeaponTab();
    AudioManager.Instance.PlaySound(SoundEvent.UpgradeComplete);
    UntitledGemGameGameScreen.Instance?.SaveProgress();
    return true;
  }

  private void SaveWeaponLoadout()
  {
    UpgradeManager.Instance.SyncWeaponrySlots();
    UntitledGemGameGameScreen.Instance?.SaveProgress();
  }

  private void UpdateButtonWeaponry()
  {
    if (!UpgradeManager.Instance.UG.HomeBase) return;
    if (!HudLayout.BottomBarVisible) return;
    var cursor = GameInput.UiCursor;
    if (GameInput.Mouse.WasButtonPressed(MouseButton.Left)
      && HudLayout.NavigationButton(HudLayout.WeaponryTab).Contains(cursor.X, cursor.Y))
    {
      SetUpgradeType(m_upgradeWindowType == UpgradeTypes.Weaponry ? UpgradeTypes.None : UpgradeTypes.Weaponry);
      m_animateButtonClickWeaponry = 0.001f;
    }
  }

  private void DrawToggleButtonWeaponry(SpriteBatch batch)
  {
    if (!UpgradeManager.Instance.UG.HomeBase) return;
    var bounds = HudLayout.NavigationButton(HudLayout.WeaponryTab);
    bool selected = m_upgradeWindowType == UpgradeTypes.Weaponry;
    bool hasUnfittedUnlocked = false;
    foreach (var weapon in MainShipWeapons.All)
    {
      if (weapon != MainShipWeapon.Cannon
        && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon)
        && !WeaponLoadout.IsFitted(weapon)
        && WeaponLoadout.Slots.Take(WeaponLoadout.UnlockedHardpoints).Any(s => s == null))
      {
        hasUnfittedUnlocked = true;
        break;
      }
    }
    DrawHudButton(batch, bounds, selected ? Loc.T("Hide") : Weaponry.NavigationLabel, GuiSkin.PanelAccent,
      selected, bounds.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y), m_animateButtonClickWeaponry,
      tab: true, badge: !selected && hasUnfittedUnlocked);
  }


  private void UpdateWeaponryInput(float dt)
  {
    if (m_upgradeWindowType != UpgradeTypes.Weaponry || (IsDetached && !IsPopoutFocused))
    {
      CancelWeaponDrag();
      return;
    }
    if (KeyboardExtended.GetState().WasKeyPressed(Keys.Escape))
    {
      CancelWeaponDrag();
      return;
    }
    var mouse = GameInput.Mouse;
    var cursor = GameInput.UiCursor;
    var position = new Vector2(cursor.X, cursor.Y);
    var point = position.ToPoint();
    bool pressed = mouse.WasButtonPressed(MouseButton.Left);
    bool right = mouse.WasButtonPressed(MouseButton.Right);
    if (draggedWeapon != null || draggedSupport != WeaponSupportModule.None)
    {
      weaponDragging |= Vector2.DistanceSquared(position, weaponPressPosition) >= 100;
      if (mouse.LeftButton != ButtonState.Released) return;
      bool changed = false;
      if (weaponDragging)
      {
        // Like the Shipyard: drop on a socket to fit, anywhere else to take it off.
        if (draggedSupport != WeaponSupportModule.None)
        {
          bool dropped = false;
          for (int i = 0; i < WeaponLoadout.Supports.Length; i++)
            if (SupportShown(i) && WeaponSupportBounds(i).Contains(point))
            {
              dropped = true;
              changed = WeaponLoadout.EquipSupport(draggedSupport, i, draggedSupportSource);
              break;
            }
          if (!dropped && draggedSupportSource >= 0)
            changed = WeaponLoadout.ReturnSupport(draggedSupportSource);
        }
        else if (draggedWeapon is { } weapon && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon))
        {
          bool dropped = false;
          for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
            if (MountContains(slot + 1, point))
            {
              dropped = true;
              changed = WeaponLoadout.TryEquip(weapon, slot);
              break;
            }
          if (!dropped && draggedWeaponSourceSlot >= 0)
            changed = WeaponLoadout.TryUnequipSlot(draggedWeaponSourceSlot);
        }
      }
      CancelWeaponDrag();
      if (changed) EquipmentChanged();
      return;
    }
    if (!pressed && !right) return;
    if (pressed && !SelectedWeaponUnlocked && WeaponUnlockButtonBounds.Contains(point))
    {
      TryUnlockWeaponBlueprint(Weaponry.Tabs[selectedWeaponTab].Weapon);
      return;
    }
    // Supports have priority over their enclosing callout.
    for (int i = 0; i < WeaponLoadout.Supports.Length; i++)
    {
      if (!SupportShown(i) || !WeaponSupportBounds(i).Contains(point)) continue;
      if (!WeaponSupportCatalog.Enabled) return;
      var module = WeaponLoadout.Supports[i];
      if (right)
      {
        if (WeaponLoadout.ReturnSupport(i)) EquipmentChanged();
      }
      else if (module != WeaponSupportModule.None)
      {
        draggedSupport = module;
        draggedSupportSource = i;
        weaponPressPosition = position;
      }
      return;
    }
    for (int i = 0; i < WeaponSupportCatalog.All.Length; i++)
    {
      if (!WeaponModuleInventoryBounds(i).Contains(point)) continue;
      var module = WeaponSupportCatalog.All[i];
      if (WeaponSupportCatalog.Enabled && pressed && WeaponLoadout.SupportInventory[(int)module] > 0)
      {
        draggedSupport = module;
        draggedSupportSource = -1;
        weaponPressPosition = position;
      }
      return;
    }
    for (int i = 0; i < Weaponry.Tabs.Length; i++)
    {
      if (!WeaponBlueprintCardBounds(i).Contains(point) || !pressed) continue;
      SelectWeaponTab(i);
      var weapon = Weaponry.Tabs[i].Weapon;
      if (UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon) && weapon != MainShipWeapon.Cannon)
        BeginWeaponDrag(weapon, -1, position);
      return;
    }
    if (MountContains(0, point))
    {
      if (pressed) SelectWeaponTab(0);
      return;
    }
    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
    {
      if (!MountContains(slot + 1, point)) continue;
      if (slot >= WeaponLoadout.UnlockedHardpoints) return;
      if (right)
      {
        if (WeaponLoadout.TryUnequipSlot(slot)) EquipmentChanged();
      }
      else if (WeaponLoadout.Slots[slot] is { } weapon)
      {
        SelectWeaponTab((int)weapon);
        BeginWeaponDrag(weapon, slot, position);
      }
      return;
    }
  }

  private void BeginWeaponDrag(MainShipWeapon weapon, int source, Vector2 position)
  {
    draggedWeapon = weapon;
    draggedWeaponSourceSlot = source;
    weaponPressPosition = position;
    weaponDragging = false;
  }

  private void EquipmentChanged()
  {
    AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
    SaveWeaponLoadout();
  }

  // ---------------------------------------------------------------- drawing

  private void DrawWeaponryPanel(SpriteBatch batch, float dt)
  {
    weaponHoverTitle = null;
    var cursor = new Point((int)GameInput.UiCursor.X, (int)GameInput.UiCursor.Y);
    var tab = Weaponry.Tabs[selectedWeaponTab];
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawNavigationPanelBackground(batch, tab.Accent, showWatermark: false);
    if (!IsDetached) GuiSkin.CloseButton(batch, WeaponryCloseButton, tab.Accent, WeaponryCloseButton.Contains(cursor));
    DrawCleanVertDivider(batch, Weaponry.ShowcaseColumn.Right + 20, 460, Weaponry.ShowcaseColumn.Bottom - 480, GuiSkin.PanelAccent * 0.4f);
    batch.End();
    DrawWeaponSelector(batch, cursor);
    DrawWeaponHeading(batch, tab, cursor);
    DrawHomebaseHardpointDiagram(batch, cursor);
    DrawWeaponModuleInventory(batch, cursor);
    FlushIcons(batch, SamplerState.PointClamp);
    DrawWeaponryTreeHeader(batch, tab, cursor);
  }

  // Top-left hex medallions, one per weapon, with pips for the modules on its mount.
  private void DrawWeaponSelector(SpriteBatch batch, Point cursor)
  {
    for (int i = 0; i < Weaponry.Tabs.Length; i++)
    {
      var tab = Weaponry.Tabs[i];
      var bounds = WeaponBlueprintCardBounds(i);
      bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(tab.Weapon);
      bool hovered = bounds.Contains(cursor);
      int mount = tab.Weapon == MainShipWeapon.Cannon ? 0 : WeaponLoadout.FindSlot(tab.Weapon) + 1;
      bool fitted = tab.Weapon == MainShipWeapon.Cannon || mount > 0;

      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      var medallion = GuiSkin.SelectionMedallion(batch, bounds, selectedWeaponTab == i, hovered, unlocked);
      const int pip = 16, gap = 6;
      int pipsX = bounds.Center.X - (WeaponSupportCatalog.SlotsPerMount * pip + (WeaponSupportCatalog.SlotsPerMount - 1) * gap) / 2;
      for (int s = 0; s < WeaponSupportCatalog.SlotsPerMount; s++)
      {
        var rect = new Rectangle(pipsX + s * (pip + gap), bounds.Bottom + 8, pip, pip);
        if (!WeaponSupportCatalog.Enabled || !unlocked || !fitted)
        {
          GuiSkin.DrawHexPip(batch, rect, GuiSkin.BorderColor * 0.35f, false);
          continue;
        }
        bool filled = WeaponLoadout.Supports[mount * WeaponSupportCatalog.SlotsPerMount + s] != WeaponSupportModule.None;
        GuiSkin.DrawHexPip(batch, rect, filled ? tab.Accent : GuiSkin.PanelAccent * 0.55f, filled);
      }
      batch.End();

      int inset = medallion.Width * 22 / 100;
      DrawWeaponTurretPreview(batch, tab.Weapon, medallion.Center.ToVector2(),
        (medallion.Width - inset * 2) / 140f, unlocked ? 1f : 0.3f);
      if (!unlocked) DrawWeaponLock(batch, new Rectangle(medallion.Center.X - 16, medallion.Center.Y - 16, 32, 32));
      if (hovered) HoverWeapon(tab.Weapon);
    }
  }

  // Name and description under the medallions, with the blueprint unlock button below.
  private void DrawWeaponHeading(SpriteBatch batch, Weaponry.Tab tab, Point cursor)
  {
    var heading = PanelNavigationLayout.SelectionHeading;
    ShipyardLabel(Loc.Upper(Loc.T(tab.Name)), heading, 40, GuiSkin.StatHeadingColor);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, (int)heading.X, PanelNavigationLayout.SelectionSubtitleY + 14, 660, GuiSkin.PanelAccent);
    batch.End();
    DrawWrappedText(Loc.T(tab.Description), new Vector2(heading.X, WeaponDescriptionY), 600, 26, 38, GuiSkin.ButtonTextColor, 4);
    if (!SelectedWeaponUnlocked) DrawWeaponBlueprintUnlock(batch, tab, cursor);
  }

  // Bottom-left module stock, matching the Shipyard's stat block placement.
  private void DrawWeaponModuleInventory(SpriteBatch batch, Point cursor)
  {
    var col = Weaponry.ShowcaseColumn;
    int width = WeaponSupportCatalog.All.Length * (WeaponInventorySocket + 40) - 40;
    ShipyardLabel(Loc.Upper(Loc.T("Weapon modules")), new Vector2(col.X, WeaponBottomTop), 24, GuiSkin.PanelAccent);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    Color line = weaponDragging && draggedSupportSource >= 0 ? GuiSkin.ConfirmAccent : GuiSkin.PanelAccent;
    DrawCleanHorizDivider(batch, col.X, WeaponBottomTop + 44, Math.Max(width, 560), line);
    batch.End();
    if (!WeaponSupportCatalog.Enabled)
    {
      ShipyardLabel(Loc.T("Weapon modules are disabled in this version."),
        new Vector2(col.X, WeaponBottomTop + 80), 24, GuiSkin.MutedTextColor);
      return;
    }
    for (int i = 0; i < WeaponSupportCatalog.All.Length; i++)
    {
      var module = WeaponSupportCatalog.All[i];
      var bounds = WeaponModuleInventoryBounds(i);
      int count = WeaponLoadout.SupportInventory[(int)module];
      bool over = bounds.Contains(cursor);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      if (over && count > 0)
        GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", bounds.Center.ToVector2(), bounds.Width + 18,
          (float)BaseGame.Time.TotalGameTime.TotalSeconds * 0.7f, GuiSkin.PanelAccent * 0.65f);
      var rim = count > 0 ? GuiSkin.PanelAccent : GuiSkin.BorderColor * 0.4f;
      GuiSkin.DrawHexSocket(batch, bounds, over ? Color.Lerp(rim, Color.White, 0.35f) : rim, active: over && count > 0);
      batch.End();
      int icon = bounds.Width * 52 / 100;
      bool lifted = weaponDragging && draggedSupportSource < 0 && draggedSupport == module;
      QueueModuleIcon(WeaponSupportCatalog.Icon(module),
        new Rectangle(bounds.Center.X - icon / 2, bounds.Center.Y - icon / 2, icon, icon), count == 0 ? 0.2f : lifted ? 0.25f : 1f);
      DrawShipyardLabelCentered($"×{count}", new Vector2(bounds.Center.X, bounds.Bottom + 22), 24,
        count > 0 ? GuiSkin.StatValueColor : GuiSkin.MutedTextColor);
      if (over) HoverSupport(module, false);
    }
    ShipyardLabel(Loc.T("Found while firing on the planet"), new Vector2(col.X, WeaponBottomTop + 280), 20, GuiSkin.MutedTextColor);
  }

  private void DrawWeaponLock(SpriteBatch batch, Rectangle bounds, float alpha = 1f)
  {
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    batch.Draw(TextureCache.LockIcon.Value, bounds, GuiSkin.LockedTextColor * alpha);
    batch.End();
  }

  private void DrawHomebaseHardpointDiagram(SpriteBatch batch, Point cursor)
  {
    var shipCenter = ShowcaseShipPivot;
    var device = batch.GraphicsDevice;
    var glow = GuiSkin.GetTexture(device, "synty_fx_glow_01");
    float time = (float)(BaseGame.Time?.TotalGameTime.TotalSeconds ?? 0.0);

    // Soft holo glow behind the cruiser.
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(glow, new Rectangle((int)(shipCenter.X - 270), (int)(shipCenter.Y - 360), 540, 720),
      GuiSkin.PanelAccent * 0.16f);

    var fleet = TextureCache.Fleet;
    if (fleet != null)
    {
      // Engine thruster plume at the stern.
      int frameIdx = (int)(time * 1000f / FleetAtlas.EngineFrameMilliseconds) % FleetAtlas.EngineFrameCount;
      if (frameIdx < 0) frameIdx += FleetAtlas.EngineFrameCount;
      string boosterKey = FleetAtlas.PrerenderBoosterEngine + "#" + frameIdx;
      string boosterCoreKey = FleetAtlas.PrerenderBoosterCore + "#" + frameIdx;
      var booster = fleet.Region(boosterKey);
      var bOrigin = fleet.Pivot(boosterKey);
      var boosterCore = fleet.Region(boosterCoreKey);
      var cOrigin = fleet.Pivot(boosterCoreKey);
      foreach (var nozzle in fleet.Nozzles(FleetAtlas.PrerenderHomeBase))
      {
        var nozzlePos = shipCenter + new Vector2(nozzle.X, nozzle.Y) * ShowcaseHullUnitScale;
        float nozzleScale = nozzle.Width * ShowcaseHullUnitScale / Math.Max(1, booster.Width) * 1.3f;
        batch.Draw(fleet.Texture, nozzlePos, booster.Bounds, new Color(80, 200, 255) * 0.75f,
          MathHelper.Pi, bOrigin, nozzleScale, SpriteEffects.None, 0f);
        batch.Draw(fleet.Texture, nozzlePos, boosterCore.Bounds, Color.White * 0.85f,
          MathHelper.Pi, cOrigin, nozzleScale * 0.8f, SpriteEffects.None, 0f);
      }

      // Main Homebase Cruiser hull.
      var hullRegion = fleet.Region(FleetAtlas.PrerenderHomeBase);
      var hullPivot = fleet.Pivot(FleetAtlas.PrerenderHomeBase);
      batch.Draw(fleet.Texture, shipCenter, hullRegion.Bounds, Color.White, 0f,
        hullPivot, ShowcaseTexelScale, SpriteEffects.None, 0f);

      // Ship terminal on the port deck.
      var termRegion = fleet.Region(FleetAtlas.HullTerminal);
      var termPos = HullToShowcaseScreen(new Vector2(-14.5f, 1.5f));
      batch.Draw(fleet.Texture, termPos, termRegion.Bounds, Color.White, 0f,
        fleet.Pivot(FleetAtlas.HullTerminal), ShowcaseTexelScale, SpriteEffects.None, 0f);
    }
    batch.End();

    // Mounted turrets at their live hull coordinates.
    DrawWeaponTurretOnHull(batch, MainShipWeapon.Cannon, HullToShowcaseScreen(Weaponry.PrimaryBowMountLeft), -MathHelper.PiOver2);
    DrawWeaponTurretOnHull(batch, MainShipWeapon.Cannon, HullToShowcaseScreen(Weaponry.PrimaryBowMountRight), -MathHelper.PiOver2);
    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
      if (WeaponLoadout.Slots[slot] is { } fitted && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(fitted))
        DrawWeaponTurretOnHull(batch, fitted, HullToShowcaseScreen(Weaponry.HardpointSlots[slot].HullMount), -MathHelper.PiOver4 * 0.5f);

    for (int mount = 0; mount <= Weaponry.MaxCustomSlots; mount++)
      DrawWeaponMount(batch, mount, cursor);

    var col = Weaponry.ShowcaseColumn;
    DrawShipyardLabelCentered(Loc.T("Drag to equip or swap · Right-click to remove"),
      new Vector2(shipCenter.X, col.Bottom - 30), 22, GuiSkin.MutedTextColor);
  }

  // A small pin on the hull mount; occupied mounts add a leader to a borderless name + sockets callout.
  private void DrawWeaponMount(SpriteBatch batch, int mount, Point cursor)
  {
    var weapon = MountWeapon(mount);
    bool locked = HardpointLocked(mount);
    bool callout = MountCalloutShown(mount);
    bool ghost = MountGhostShown(mount);
    bool hovered = MountContains(mount, cursor);
    bool dropTarget = DraggingWeapon && mount > 0 && !locked;
    bool selected = weapon is { } w0 && (int)w0 == selectedWeaponTab;
    float pulse = 0.5f + 0.5f * MathF.Sin((float)(BaseGame.Time?.TotalGameTime.TotalSeconds ?? 0.0) * 5f);
    var box = MountBox(mount);
    string mountName = mount == 0 ? Loc.T(Weaponry.PrimarySlotName) : Loc.T(Weaponry.HardpointSlots[mount - 1].Name);

    Color accent = dropTarget && hovered ? GuiSkin.ConfirmAccent
      : weapon is { } w ? Weaponry.ForWeapon(w).Accent : GuiSkin.PanelAccent;
    float strength = locked ? 0.16f
      : dropTarget ? (hovered ? 1f : 0.55f + 0.3f * pulse)
      : weapon != null ? (selected || hovered ? 1f : 0.7f)
      : hovered ? 0.7f : 0.32f;
    Color color = accent * strength;

    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    var pin = MountPin(box.Center);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    if (callout || ghost)
    {
      int underline = CalloutUnderlineY(mount);
      var bounds = CalloutBounds(mount);
      bool left = CalloutOnLeft(mount);
      DrawCalloutLeader(batch, pixel, pin, left, underline, left ? bounds.X : bounds.Right,
        ghost ? accent * (hovered ? 0.9f : 0.35f + 0.25f * pulse) : color);
    }
    if (!locked)
    {
      // A small pin marks roughly where the weapon attaches, without boxing in turrets of any size.
      bool filled = weapon != null || (dropTarget && hovered);
      if (hovered || selected || dropTarget)
        GuiSkin.DrawHexPip(batch, MountPin(box.Center, 40), accent * (dropTarget && !hovered ? 0.25f + 0.2f * pulse : 0.35f), false);
      GuiSkin.DrawHexPip(batch, pin, color, filled);
      if (mount == 0) GuiSkin.DrawHexPip(batch, MountPin(MountBox(HullToShowcaseScreen(Weaponry.PrimaryBowMountLeft)).Center), color, true);
    }
    batch.End();

    if (locked)
    {
      DrawWeaponLock(batch, new Rectangle(box.Center.X - 13, box.Center.Y - 13, 26, 26), hovered ? 0.8f : 0.35f);
      if (hovered) SetWeaponHover(mountName, Loc.T("This hardpoint is unavailable."),
        null, GuiSkin.LockedTextColor);
      return;
    }

    if (ghost)
    {
      DrawCalloutName(mountName, mount, hovered ? GuiSkin.ConfirmAccent : GuiSkin.MutedTextColor);
      return;
    }
    if (!callout)
    {
      if (hovered && !DraggingWeapon)
        SetWeaponHover(mountName, Loc.T("Empty hardpoint."), Loc.T("Drag a weapon here to attach it"), GuiSkin.PanelAccent);
      return;
    }

    string name = weapon is { } fitted ? Loc.T(Weaponry.ForWeapon(fitted).Name) : Loc.T("Empty hardpoint");
    DrawCalloutName(name, mount, dropTarget && hovered ? GuiSkin.ConfirmAccent
      : weapon == null ? GuiSkin.MutedTextColor : selected ? accent : GuiSkin.StatHeadingColor);
    bool supportHovered = false;
    for (int s = 0; s < WeaponSupportCatalog.SlotsPerMount; s++)
    {
      int index = mount * WeaponSupportCatalog.SlotsPerMount + s;
      var bounds = WeaponSupportBounds(index);
      var module = WeaponLoadout.Supports[index];
      bool over = bounds.Contains(cursor);
      if (!WeaponSupportCatalog.Enabled)
      {
        batch.Begin(transformMatrix: HudLayout.RenderTransform);
        GuiSkin.DrawHexSocket(batch, bounds, GuiSkin.BorderColor * 0.35f, active: false);
        batch.End();
        int lockSize = bounds.Width / 3;
        DrawWeaponLock(batch, new Rectangle(bounds.Center.X - lockSize / 2,
          bounds.Center.Y - lockSize / 2, lockSize, lockSize), 0.55f);
        if (over)
        {
          supportHovered = true;
          HoverSupport(WeaponSupportModule.None, true);
        }
        continue;
      }
      bool supportTarget = weaponDragging && draggedSupport != WeaponSupportModule.None;
      Color rim = supportTarget && over ? GuiSkin.ConfirmAccent
        : module != WeaponSupportModule.None ? accent
        : supportTarget ? GuiSkin.PanelAccent * (0.55f + 0.3f * pulse)
        : GuiSkin.ButtonBorderColor * 0.6f;
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.DrawHexSocket(batch, bounds, over ? Color.Lerp(rim, Color.White, 0.35f) : rim,
        active: module != WeaponSupportModule.None || over);
      batch.End();
      if (module != WeaponSupportModule.None)
      {
        int icon = bounds.Width * 56 / 100;
        bool lifted = weaponDragging && draggedSupportSource == index;
        QueueModuleIcon(WeaponSupportCatalog.Icon(module),
          new Rectangle(bounds.Center.X - icon / 2, bounds.Center.Y - icon / 2, icon, icon), lifted ? 0.25f : 1f);
      }
      else
        DrawShipyardLabelCentered("+", bounds.Center.ToVector2(), 30, over ? GuiSkin.PanelAccent : GuiSkin.MutedTextColor * 0.6f);
      if (over)
      {
        supportHovered = true;
        HoverSupport(module, true);
      }
    }
    if (hovered && !supportHovered)
    {
      if (weapon is { } hoveredWeapon) HoverWeapon(hoveredWeapon);
      else SetWeaponHover(mountName, Loc.T("Empty hardpoint."), Loc.T("Drag a weapon here to attach it"), GuiSkin.PanelAccent);
    }
  }

  private void DrawCalloutName(string text, int mount, Color color)
  {
    var bounds = CalloutBounds(mount);
    float size = CalloutNameSize;
    float width = Measure2(text, Vector2.Zero, size).X;
    if (width > bounds.Width) { size *= bounds.Width / width; width = bounds.Width; }
    float x = CalloutOnLeft(mount) ? bounds.Right - width : bounds.X;
    ShipyardLabel(text, new Vector2(x, CalloutUnderlineY(mount) - 44), size, color);
  }

  private const int MountPinSize = 22;

  private static Rectangle MountPin(Point center, int size = MountPinSize)
    => new(center.X - size / 2, center.Y - size / 2, size, size);

  // Short stub out of the pin, a riser to the callout's line, then the underline itself.
  private static void DrawCalloutLeader(SpriteBatch batch, Texture2D pixel, Rectangle pin, bool left, int underlineY, int farX, Color color,
    int stub = 64)
  {
    int y0 = pin.Center.Y;
    int edgeX = left ? pin.Left : pin.Right;
    int elbowX = left ? edgeX - stub : edgeX + stub;
    batch.Draw(pixel, new Rectangle(Math.Min(edgeX, elbowX), y0 - 1, stub, 2), color);
    batch.Draw(pixel, new Rectangle(elbowX - 1, Math.Min(y0, underlineY), 2, Math.Abs(y0 - underlineY) + 1), color);
    batch.Draw(pixel, new Rectangle(Math.Min(elbowX, farX), underlineY - 1, Math.Abs(farX - elbowX), 2), color);
  }


  private void DrawWeaponryTreeHeader(SpriteBatch batch, Weaponry.Tab tab, Point cursor)
  {
    var tree = Weaponry.TreeColumn;
    float x = tree.X + 24, width = tree.Width - 48;
    ShipyardLabel(Loc.T("Upgrade Tree"), new Vector2(x, 360), 46, GuiSkin.StatHeadingColor);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, (int)x, 434, (int)width, Color.Lerp(GuiSkin.StatHeadingColor, GuiSkin.PanelAccent, 0.3f) * 0.72f);
    batch.End();
    if (!SelectedWeaponUnlocked)
      DrawWrappedSystemText(Weaponry.IsAvailable(tab.Weapon)
          ? Loc.F("Unlock the {0} blueprint to attach it and research its upgrades.", Loc.T(tab.Name))
          : Loc.T("Not available in this version"),
        new Vector2(x, 470), width, 26, GuiSkin.MutedTextColor);
  }

  private void DrawWeaponBlueprintUnlock(SpriteBatch batch, Weaponry.Tab tab, Point cursor)
  {
    var bounds = WeaponUnlockButtonBounds;
    // Unreleased weapons get no unlock button outside dev builds, just a note.
    if (!Weaponry.IsAvailable(tab.Weapon))
    {
      DrawWrappedText(Loc.T("Not available in this version"), new Vector2(bounds.X, bounds.Y + 8), bounds.Width,
        26, 38, GuiSkin.MutedTextColor, 2);
      return;
    }
    if (!UpgradeManager.CurrentUpgrades.UpgradeButtons.TryGetValue(tab.Root, out var root)) return;
    bool canUnlock = CanAffordUpgradeButton(root);
    DrawPurchaseButton(batch, bounds, Loc.T("Unlock weapon"), root.GetNextLevelCost(),
      canUnlock, bounds.Contains(cursor), 0f, Loc.T("Locked"));
  }

  private void SetWeaponHover(string title, string body, string hint, Color color)
  {
    weaponHoverTitle = title;
    weaponHoverBody = body;
    weaponHoverHint = hint;
    weaponHoverColor = color;
  }

  private void HoverWeapon(MainShipWeapon weapon)
  {
    var tab = Weaponry.ForWeapon(weapon);
    bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon);
    string hint = weapon == MainShipWeapon.Cannon ? Loc.T("Fixed primary mount · Click to view upgrades")
      : Loc.T("Drag to a hardpoint · Right-click an attached weapon to remove");
    if (!Weaponry.IsAvailable(weapon))
      hint = Loc.T("Not available in this version");
    else if (!unlocked && UpgradeManager.CurrentUpgrades.UpgradeButtons.TryGetValue(tab.Root, out var root))
      hint = Loc.F("{0} gems · Click to inspect blueprint", NumberFormatter.AbbreviateBigNumber(root.GetNextLevelCost()))
        + (CanAffordUpgradeButton(root) ? "" : " · " + Loc.T("Not enough gems"));
    SetWeaponHover(Loc.T(tab.Name), Loc.T(tab.Description), hint, tab.Accent);
  }

  private void HoverSupport(WeaponSupportModule module, bool fitted)
  {
    if (!WeaponSupportCatalog.Enabled)
    {
      SetWeaponHover(Loc.T("Support socket"), Loc.T("Weapon modules are disabled in this version."),
        Loc.T("Unavailable"), GuiSkin.MutedTextColor);
      return;
    }
    SetWeaponHover(WeaponSupportCatalog.Name(module), WeaponSupportCatalog.Description(module),
      module == WeaponSupportModule.None ? Loc.T("Modules drop while firing on the planet")
        : fitted ? Loc.T("Drag to move · Right-click to return to inventory") : Loc.T("Drag into a support socket"), GuiSkin.PanelAccent);
  }

  // Draw after the Gum upgrade tree so tooltips and dragged icons remain on top.
  private void DrawWeaponryDragOverlay(SpriteBatch batch)
  {
    var pos = new Vector2(GameInput.UiCursor.X, GameInput.UiCursor.Y);
    if (weaponDragging)
    {
      // Same hex ghost as a module dragged in the Shipyard.
      var ghost = new Rectangle((int)pos.X + 16, (int)pos.Y + 16, 112, 112);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.DrawHexSocket(batch, ghost, GuiSkin.PanelAccent, active: true);
      batch.End();
      if (draggedWeapon is { } weapon) DrawWeaponTurretPreview(batch, weapon, ghost.Center.ToVector2(), 0.5f, 1);
      if (draggedSupport != WeaponSupportModule.None)
      {
        QueueModuleIcon(WeaponSupportCatalog.Icon(draggedSupport), new Rectangle(ghost.X + 24, ghost.Y + 24, 64, 64));
        FlushIcons(batch, SamplerState.PointClamp);
      }
      return;
    }
    if (weaponHoverTitle == null) return;
    const int width = TooltipLayout.Width;
    var title = WrapTooltipText(weaponHoverTitle, width - TooltipLayout.Padding * 2, TooltipLayout.TitleSize);
    var lines = WrapTooltipText(weaponHoverBody, width - TooltipLayout.Padding * 2, TooltipLayout.BodySize);
    var hints = WrapTooltipText(weaponHoverHint, width - TooltipLayout.Padding * 2, TooltipLayout.DetailSize);
    int extra = (title.Count - 1) * TooltipLayout.TitleLineHeight;
    int height = TooltipLayout.BodyTop + extra + lines.Count * TooltipLayout.BodyLineHeight + 34 + hints.Count * 32;
    int x = Math.Clamp((int)pos.X + 28, 24, HudLayout.Width - width - 24);
    int y = Math.Clamp((int)pos.Y + 32, Weaponry.Panel.Top, Weaponry.Panel.Bottom - height);
    DrawTooltipHeader(batch, new Rectangle(x, y, width, height), title, null, weaponHoverColor);
    for (int i = 0; i < lines.Count; i++)
      ShipyardLabel(lines[i], new Vector2(x + TooltipLayout.Padding, y + TooltipLayout.BodyTop + extra + i * TooltipLayout.BodyLineHeight), TooltipLayout.BodySize, GuiSkin.ButtonTextColor);
    for (int i = 0; i < hints.Count; i++)
      ShipyardLabel(hints[i], new Vector2(x + TooltipLayout.Padding, y + height - hints.Count * 32 - 12 + i * 32), TooltipLayout.DetailSize, GuiSkin.MutedTextColor);
  }

  private static (string Head, string Base) GetTurretSpritePaths(MainShipWeapon weapon) => weapon switch
  {
    MainShipWeapon.Cannon => (FleetAtlas.TurretCannon, FleetAtlas.TurretCannonBase),
    MainShipWeapon.Laser => (FleetAtlas.TurretLaser, FleetAtlas.TurretLaserBase),
    MainShipWeapon.Harpoon => (FleetAtlas.TurretHarpoon, FleetAtlas.TurretHarpoonBase),
    MainShipWeapon.Rockets => (FleetAtlas.TurretRockets, FleetAtlas.TurretRocketsBase),
    MainShipWeapon.Railgun => (FleetAtlas.TurretRailgun, FleetAtlas.TurretRailgunBase),
    MainShipWeapon.Flak => (FleetAtlas.TurretCannon, FleetAtlas.TurretCannonBase),
    _ => (FleetAtlas.TurretCannon, FleetAtlas.TurretCannonBase),
  };

  private static void DrawWeaponTurretOnHull(SpriteBatch batch, MainShipWeapon weapon, Vector2 pivot, float aim)
  {
    var fleet = TextureCache.Fleet;
    if (fleet == null) return;
    var (headPath, basePath) = GetTurretSpritePaths(weapon);
    var baseRegion = fleet.Region(basePath);
    var headRegion = PrerenderedShips.Frame(fleet, headPath, aim + MathHelper.PiOver2, out var headOrigin, out float rotation);

    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(fleet.Texture, pivot, baseRegion.Bounds, Color.White, 0f,
      fleet.Pivot(basePath), ShowcaseTexelScale, SpriteEffects.None, 0f);
    batch.Draw(fleet.Texture, pivot, headRegion.Bounds, Color.White, rotation,
      headOrigin, ShowcaseTexelScale, SpriteEffects.None, 0f);
    batch.End();
  }

  private static void DrawWeaponTurretPreview(SpriteBatch batch, MainShipWeapon weapon, Vector2 center, float scale, float alpha)
  {
    var fleet = TextureCache.Fleet;
    if (fleet == null) return;
    var (headPath, basePath) = GetTurretSpritePaths(weapon);
    var baseRegion = fleet.Region(basePath);
    var headRegion = PrerenderedShips.Frame(fleet, headPath, MathHelper.PiOver4, out var headOrigin, out float rotation);
    // Center the combined head/base bounds and give every blueprint the same icon footprint.
    var baseOrigin = fleet.Pivot(basePath);
    var min = Vector2.Min(-baseOrigin, -headOrigin);
    var max = Vector2.Max(new Vector2(baseRegion.Width, baseRegion.Height) - baseOrigin,
      new Vector2(headRegion.Width, headRegion.Height) - headOrigin);
    scale *= 140f / Math.Max(max.X - min.X, max.Y - min.Y);
    center -= (min + max) * 0.5f * scale;


    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(fleet.Texture, center, baseRegion.Bounds, Color.White * alpha, 0f,
      fleet.Pivot(basePath), scale, SpriteEffects.None, 0f);
    batch.Draw(fleet.Texture, center, headRegion.Bounds, Color.White * alpha, rotation,
      headOrigin, scale, SpriteEffects.None, 0f);
    batch.End();
  }
}
