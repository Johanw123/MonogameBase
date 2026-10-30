using System;
using System.Collections.Generic;
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

// Icon inventory, ship hardpoints and a compact selected-weapon tree.
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

  public static Rectangle WeaponBlueprintCardBounds(int index)
  {
    var col = Weaponry.BlueprintsColumn;
    return new Rectangle(col.X + (index % 2) * 192, col.Y + 100 + (index / 2) * 192, 172, 172);
  }

  private static Rectangle WeaponModuleInventoryBounds(int index)
  {
    var col = Weaponry.BlueprintsColumn;
    return new Rectangle(col.X + index % 2 * 192, col.Y + 870 + index / 2 * 192, 172, 172);
  }

  private static Rectangle WeaponPrimaryMountCardBounds
    => new(Weaponry.ShowcaseColumn.Center.X - 200, Weaponry.ShowcaseColumn.Y + 120, 400, 248);

  public static Rectangle WeaponHardpointCardBounds(int slotIndex)
  {
    var col = Weaponry.ShowcaseColumn;
    return new Rectangle(slotIndex % 2 == 0 ? col.X + 16 : col.Right - 416,
      col.Y + 400 + slotIndex / 2 * 620, 400, 248);
  }

  private static Rectangle WeaponSupportBounds(int index)
  {
    int mount = index / 3;
    var card = mount == 0 ? WeaponPrimaryMountCardBounds : WeaponHardpointCardBounds(mount - 1);
    return new Rectangle(card.X + 58 + index % 3 * 100, card.Y + 150, 84, 84);
  }

  private static Vector2 ShowcaseShipPivot => new(Weaponry.ShowcaseColumn.Center.X, Weaponry.ShowcaseColumn.Y + 830);
  private const float ShowcaseTexelScale = 2.1f;
  private static float ShowcaseHullUnitScale => ShowcaseTexelScale / PrerenderedShips.HomeBaseScale;
  private static Vector2 HullToShowcaseScreen(Vector2 hullMount) => ShowcaseShipPivot + hullMount * ShowcaseHullUnitScale;
  private static Rectangle ShowcaseHullMarkerBounds(int slotIndex)
  {
    var pos = HullToShowcaseScreen(Weaponry.HardpointSlots[slotIndex].HullMount);
    return new Rectangle((int)pos.X - 24, (int)pos.Y - 24, 48, 48);
  }

  private static string FormatWeaponStat(double value, int decimals = 1)
    => value >= 1000
      ? NumberFormatter.AbbreviateBigNumber((ulong)Math.Round(value))
      : value.ToString($"F{decimals}", System.Globalization.CultureInfo.InvariantCulture);

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
    if (weapon == MainShipWeapon.Cannon) return false;
    string rootKey = Weaponry.ForWeapon(weapon).Root;
    if (!UpgradeManager.CurrentUpgrades.UpgradeButtons.TryGetValue(rootKey, out var rootButton))
      return false;
    if (rootButton.CurrentLevel > 0 || !CanAffordUpgradeButton(rootButton))
      return false;

    UpgradeManager.Instance.Upgrade(rootButton);
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
        if (draggedSupport != WeaponSupportModule.None)
        {
          for (int i = 0; i < WeaponLoadout.Supports.Length; i++)
            if (WeaponSupportBounds(i).Contains(point))
            {
              changed = WeaponLoadout.EquipSupport(draggedSupport, i, draggedSupportSource);
              break;
            }
          if (!changed && Weaponry.BlueprintsColumn.Contains(point) && draggedSupportSource >= 0)
            changed = WeaponLoadout.ReturnSupport(draggedSupportSource);
        }
        else if (draggedWeapon is { } weapon && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon))
        {
          for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
            if (WeaponHardpointCardBounds(slot).Contains(point) || ShowcaseHullMarkerBounds(slot).Contains(point))
            {
              changed = WeaponLoadout.TryEquip(weapon, slot);
              break;
            }
          if (!changed && Weaponry.BlueprintsColumn.Contains(point) && draggedWeaponSourceSlot >= 0)
            changed = WeaponLoadout.TryUnequipSlot(draggedWeaponSourceSlot);
        }
      }
      CancelWeaponDrag();
      if (changed) EquipmentChanged();
      return;
    }
    if (!pressed && !right) return;
    // Supports have priority over their enclosing mount card.
    for (int i = 0; i < WeaponLoadout.Supports.Length; i++)
    {
      if (!WeaponSupportBounds(i).Contains(point)) continue;
      if (!WeaponLoadout.IsSupportUnlocked(i)) return;
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
      if (pressed && WeaponLoadout.SupportInventory[(int)module] > 0)
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
      if (!UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon))
        TryUnlockWeaponBlueprint(weapon);
      else if (weapon != MainShipWeapon.Cannon)
        BeginWeaponDrag(weapon, -1, position);
      return;
    }
    if (WeaponPrimaryMountCardBounds.Contains(point))
    {
      if (pressed) SelectWeaponTab(0);
      return;
    }
    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
    {
      if (!WeaponHardpointCardBounds(slot).Contains(point) && !ShowcaseHullMarkerBounds(slot).Contains(point)) continue;
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

  private void DrawWeaponryPanel(SpriteBatch batch, float dt)
  {
    weaponHoverTitle = null;
    var cursor = new Point((int)GameInput.UiCursor.X, (int)GameInput.UiCursor.Y);
    var tab = Weaponry.Tabs[selectedWeaponTab];
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawNavigationPanelBackground(batch, tab.Accent);
    if (!IsDetached) GuiSkin.CloseButton(batch, WeaponryCloseButton, tab.Accent, WeaponryCloseButton.Contains(cursor));
    DrawCleanVertDivider(batch, Weaponry.BlueprintsColumn.Right + 20, 330, Weaponry.BlueprintsColumn.Height - 20, GuiSkin.PanelAccent * 0.4f);
    DrawCleanVertDivider(batch, Weaponry.ShowcaseColumn.Right + 20, 330, Weaponry.ShowcaseColumn.Height - 20, GuiSkin.PanelAccent * 0.4f);
    batch.End();
    DrawWeaponInventory(batch, cursor);
    var col = Weaponry.ShowcaseColumn;
    DrawFittedSystemText(Loc.T("LOADOUT"), new Vector2(col.X + 16, col.Y + 12), 700, 34, GuiSkin.StatHeadingColor);
    DrawHomebaseHardpointDiagram(batch, cursor);
    DrawWeaponMount(batch, -1, cursor);
    for (int i = 0; i < Weaponry.MaxCustomSlots; i++) DrawWeaponMount(batch, i, cursor);
    FlushIcons(batch, SamplerState.PointClamp);
    DrawWeaponryTreeHeader(batch, tab, cursor);
  }

  private void DrawWeaponInventory(SpriteBatch batch, Point cursor)
  {
    var col = Weaponry.BlueprintsColumn;
    DrawFittedSystemText(Loc.T("WEAPONS"), new Vector2(col.X, col.Y + 12), col.Width, 34, GuiSkin.StatHeadingColor);
    for (int i = 0; i < Weaponry.Tabs.Length; i++)
    {
      var tab = Weaponry.Tabs[i];
      var card = WeaponBlueprintCardBounds(i);
      bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(tab.Weapon);
      bool hovered = card.Contains(cursor);
      DrawEquipmentSocket(batch, card, tab.Accent, selectedWeaponTab == i || hovered, !unlocked);
      DrawWeaponTurretPreview(batch, tab.Weapon, card.Center.ToVector2(), 0.9f, unlocked ? 1 : 0.3f);
      if (!unlocked) DrawWeaponLock(batch, new Rectangle(card.Right - 46, card.Bottom - 46, 32, 32));
      else if (WeaponLoadout.IsFitted(tab.Weapon))
        DrawFittedSystemText(Loc.T("FITTED"), new Vector2(card.X + 12, card.Bottom - 32), card.Width - 24, 19, GuiSkin.ConfirmAccent);
      if (hovered) HoverWeapon(tab.Weapon);
    }
    DrawFittedSystemText(Loc.T("Click to unlock · Drag to fit"), new Vector2(col.X, col.Y + 692), col.Width, 23, GuiSkin.MutedTextColor);
    DrawFittedSystemText(Loc.T("MODULES"), new Vector2(col.X, col.Y + 782), col.Width, 34, GuiSkin.StatHeadingColor);
    for (int i = 0; i < WeaponSupportCatalog.All.Length; i++)
    {
      var module = WeaponSupportCatalog.All[i];
      var card = WeaponModuleInventoryBounds(i);
      int count = WeaponLoadout.SupportInventory[(int)module];
      DrawEquipmentSocket(batch, card, GuiSkin.PanelAccent, card.Contains(cursor), count == 0);
      QueueModuleIcon(WeaponSupportCatalog.Icon(module), new Rectangle(card.X + 40, card.Y + 30, 92, 92), count > 0 ? 1 : 0.2f);
      DrawFittedSystemText($"×{count}", new Vector2(card.X + 16, card.Bottom - 36), card.Width - 32, 25, GuiSkin.StatValueColor);
      if (card.Contains(cursor)) HoverSupport(module, false);
    }
    DrawFittedSystemText(Loc.T("Found while firing on the planet"), new Vector2(col.X, col.Y + 1270), col.Width, 23, GuiSkin.MutedTextColor);
  }

  private void DrawEquipmentSocket(SpriteBatch batch, Rectangle bounds, Color color, bool active, bool locked = false)
  {
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.TechCard(batch, bounds, GuiPalette.Base(new Color(7, 19, 33, 235)),
      active ? color : color * (locked ? 0.22f : 0.48f), cornerGraphic: active, hexWatermark: false);
    batch.End();
  }

  private void DrawWeaponLock(SpriteBatch batch, Rectangle bounds)
  {
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    batch.Draw(TextureCache.LockIcon.Value, bounds, GuiSkin.LockedTextColor);
    batch.End();
  }

  private void DrawWeaponMount(SpriteBatch batch, int slot, Point cursor)
  {
    var card = slot < 0 ? WeaponPrimaryMountCardBounds : WeaponHardpointCardBounds(slot);
    MainShipWeapon? weapon = slot < 0 ? MainShipWeapon.Cannon : WeaponLoadout.Slots[slot];
    bool locked = slot >= WeaponLoadout.UnlockedHardpoints;
    bool hovered = card.Contains(cursor);
    Color color = weapon is { } w ? Weaponry.ForWeapon(w).Accent : GuiSkin.PanelAccent;
    bool selected = weapon != null && (int)weapon.Value == selectedWeaponTab;
    DrawEquipmentSocket(batch, card, color, !locked && (selected || hovered), locked);
    string title = slot < 0 ? Loc.T("BOW / PRIMARY") : Loc.F("HARDPOINT {0}", HardpointNumerals[slot]);
    DrawFittedSystemText(title, new Vector2(card.X + 18, card.Y + 14), card.Width - 36, 23, locked ? GuiSkin.LockedTextColor : color);
    if (locked)
    {
      DrawWeaponLock(batch, new Rectangle(card.X + 30, card.Y + 66, 50, 50));
      DrawFittedSystemText(Loc.T("Combined Arms"), new Vector2(card.X + 100, card.Y + 80), card.Width - 120, 25, GuiSkin.LockedTextColor);
      if (hovered) SetWeaponHover(title, Loc.T("Unlock the Combined Arms talent to use hardpoints III and IV and their support sockets."), Loc.T("Available in Talents"), GuiSkin.LockedTextColor);
    }
    else if (weapon is { } fitted)
    {
      DrawWeaponTurretPreview(batch, fitted, new Vector2(card.X + 70, card.Y + 94), 0.65f, 1);
      DrawFittedSystemText(Loc.T(Weaponry.ForWeapon(fitted).Name), new Vector2(card.X + 132, card.Y + 78), card.Width - 150, 27, GuiSkin.StatHeadingColor);
      if (hovered) HoverWeapon(fitted);
    }
    else
    {
      DrawFittedSystemText("+", new Vector2(card.X + 54, card.Y + 65), 80, 48, GuiSkin.MutedTextColor);
      DrawFittedSystemText(Loc.T("Drop weapon"), new Vector2(card.X + 132, card.Y + 80), card.Width - 150, 26, GuiSkin.MutedTextColor);
    }
    for (int support = 0; support < 3; support++)
    {
      int index = (slot + 1) * 3 + support;
      var bounds = WeaponSupportBounds(index);
      var module = WeaponLoadout.Supports[index];
      DrawEquipmentSocket(batch, bounds, color, !locked && bounds.Contains(cursor), locked);
      if (module != WeaponSupportModule.None)
        QueueModuleIcon(WeaponSupportCatalog.Icon(module), new Rectangle(bounds.X + 12, bounds.Y + 12, 60, 60));
      else if (!locked)
        DrawFittedSystemText("+", new Vector2(bounds.X + 28, bounds.Y + 22), 30, 32, GuiSkin.MutedTextColor * 0.5f);
      if (!locked && bounds.Contains(cursor)) HoverSupport(module, true);
    }
  }

  private void DrawHomebaseHardpointDiagram(SpriteBatch batch, Point cursor)
  {
    var shipCenter = ShowcaseShipPivot;
    var device = batch.GraphicsDevice;
    var glow = GuiSkin.GetTexture(device, "synty_fx_glow_01");
    float time = (float)(BaseGame.Time?.TotalGameTime.TotalSeconds ?? 0.0);

    // Scanner reticle and soft holo glow behind the cruiser.
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(glow, new Rectangle((int)(shipCenter.X - 270), (int)(shipCenter.Y - 360), 540, 720),
      GuiSkin.PanelAccent * 0.16f);
    GuiSkin.DrawScannerReticle(batch, shipCenter.ToPoint(), 500, GuiSkin.PanelAccent, 0.34f);

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

    // Draw mounted turrets at their live hull coordinates.
    DrawWeaponTurretOnHull(batch, MainShipWeapon.Cannon, HullToShowcaseScreen(Weaponry.PrimaryBowMountLeft), -MathHelper.PiOver2);
    DrawWeaponTurretOnHull(batch, MainShipWeapon.Cannon, HullToShowcaseScreen(Weaponry.PrimaryBowMountRight), -MathHelper.PiOver2);

    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
    {
      if (WeaponLoadout.Slots[slot] is { } fitted && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(fitted))
      {
        var mountPos = HullToShowcaseScreen(Weaponry.HardpointSlots[slot].HullMount);
        DrawWeaponTurretOnHull(batch, fitted, mountPos, -MathHelper.PiOver4 * 0.5f);
      }
    }

    // Draw leader lines and callout markers connecting the hull hardpoints to the right-side socket cards.
    var selectedWeapon = Weaponry.Tabs[selectedWeaponTab].Weapon;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);

    // Bow Mount leader line.
    var bowHullPos = HullToShowcaseScreen(Weaponry.PrimaryHullMarker);
    var bowCard = WeaponPrimaryMountCardBounds;
    var bowColor = selectedWeaponTab == 0 ? Weaponry.Tabs[0].Accent : GuiSkin.PanelAccent * 0.65f;
    DrawHardpointLeaderLine(batch, bowHullPos, new Vector2(bowCard.Center.X, bowCard.Bottom), bowColor);

    // 4 Customizable Hardpoint leader lines & hull socket rings.
    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
    {
      var slotDef = Weaponry.HardpointSlots[slot];
      var hullPos = HullToShowcaseScreen(slotDef.HullMount);
      var card = WeaponHardpointCardBounds(slot);
      var fitted = WeaponLoadout.Slots[slot];
      var markerBounds = ShowcaseHullMarkerBounds(slot);
      bool hovered = markerBounds.Contains(cursor) || card.Contains(cursor);
      bool isSelectedWeaponSlot = fitted != null && fitted == selectedWeapon;

      Color slotColor = fitted is { } w
        ? Weaponry.ForWeapon(w).Accent
        : hovered && selectedWeapon != MainShipWeapon.Cannon
          ? Weaponry.ForWeapon(selectedWeapon).Accent
          : GuiSkin.PanelAccent * 0.7f;

      DrawHardpointLeaderLine(batch, hullPos, new Vector2(slot % 2 == 0 ? card.Right : card.Left, card.Center.Y),
        slotColor * (hovered || isSelectedWeaponSlot ? 0.95f : 0.55f));

      // Marker badge on the hull.
      GuiSkin.DrawHexPip(batch, markerBounds, slotColor, active: fitted != null || hovered);
    }
    batch.End();

    // Draw numeral labels inside each hull marker.
    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
    {
      var markerBounds = ShowcaseHullMarkerBounds(slot);
      string numeral = HardpointNumerals[slot];
      var measured = Measure2(numeral, Vector2.Zero, 19);
      FontManager.RenderFieldFont(SystemFont, numeral,
        new Vector2(markerBounds.Center.X - measured.X / 2f, markerBounds.Center.Y - measured.Y / 2f),
        Color.White, Color.Black, 19);
    }

    // Caption below cruiser diagram.
    var col = Weaponry.ShowcaseColumn;
    string diagramCaption = Loc.T("Drag to equip or swap · Right-click to remove");
    DrawFittedSystemText(diagramCaption, new Vector2(col.Center.X - 400, col.Bottom - 44), 800, 24, GuiSkin.MutedTextColor);
  }

  private static void DrawHardpointLeaderLine(SpriteBatch batch, Vector2 hullPoint, Vector2 cardLeftCenter, Color color)
  {
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    float elbowX = MathHelper.Lerp(hullPoint.X, cardLeftCenter.X, 0.52f);

    // Horizontal segment from hull point to elbowX, vertical to cardLeftCenter.Y, horizontal into card edge.
    int x0 = (int)MathF.Round(Math.Min(hullPoint.X, elbowX));
    int w0 = (int)MathF.Round(Math.Abs(elbowX - hullPoint.X));
    batch.Draw(pixel, new Rectangle(x0, (int)MathF.Round(hullPoint.Y) - 1, Math.Max(1, w0), 2), color);

    int y0 = (int)MathF.Round(Math.Min(hullPoint.Y, cardLeftCenter.Y));
    int h0 = (int)MathF.Round(Math.Abs(cardLeftCenter.Y - hullPoint.Y));
    batch.Draw(pixel, new Rectangle((int)MathF.Round(elbowX) - 1, y0, 2, Math.Max(1, h0)), color);

    int x1 = (int)MathF.Round(Math.Min(elbowX, cardLeftCenter.X));
    int w1 = (int)MathF.Round(Math.Abs(cardLeftCenter.X - elbowX));
    if (w1 > 0)
      batch.Draw(pixel, new Rectangle(x1, (int)MathF.Round(cardLeftCenter.Y) - 1, w1, 2), color);
  }


  private void DrawWeaponryTreeHeader(SpriteBatch batch, Weaponry.Tab tab, Point cursor)
  {
    var tree = Weaponry.TreeColumn;
    float x = tree.X + 24, width = tree.Width - 48;
    DrawFittedSystemText(Loc.T("SELECTED WEAPON"), new Vector2(x, tree.Y + 12), width, 24, GuiSkin.MutedTextColor);
    DrawFittedSystemText(Loc.T(tab.Name), new Vector2(x, tree.Y + 58), width, 42, tab.Accent);
    DrawFittedSystemText(Loc.T(tab.Role), new Vector2(x, tree.Y + 114), width, 25, GuiSkin.MutedTextColor);
    bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(tab.Weapon);
    string status = !unlocked ? Loc.T("LOCKED · Click its inventory icon to unlock")
      : WeaponLoadout.IsFitted(tab.Weapon) ? Loc.T("FITTED") : Loc.T("READY · Drag onto a ship hardpoint");
    DrawFittedSystemText(status, new Vector2(x, tree.Y + 162), width, 25, unlocked ? GuiSkin.ConfirmAccent : GuiSkin.LockedTextColor);
    if (unlocked)
    {
      float rate = SignalStats.FireRate(tab.Weapon);
      int power = SignalStats.FirePower(tab.Weapon);
      double output = MainShipWeapons.GemsPerSecond(UpgradeManager.Instance.UG, tab.Weapon, rate, power);
      DrawFittedSystemText(Loc.F("Power {0}    Rate {1}×", power, FormatWeaponStat(rate, 2)),
        new Vector2(x, tree.Y + 214), width, 28, GuiSkin.StatValueColor);
      string yield = tab.Weapon == MainShipWeapon.Cannon && !UpgradeManager.Instance.UG.AutoCannon
        ? Loc.T("Manual fire · Upgrade to automate") : Loc.F("{0} gems/s", FormatWeaponStat(output));
      DrawFittedSystemText(yield, new Vector2(x, tree.Y + 258), width, 25, GuiSkin.MutedTextColor);
    }
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, (int)x, tree.Y + 316, (int)width, tab.Accent * 0.5f);
    batch.End();
  }

  private void DrawWeaponryTreeLabels(SpriteBatch batch)
  {
    var tab = Weaponry.Tabs[selectedWeaponTab];
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtons;
    int offset = selectedWeaponTab * Weaponry.TabStride;
    // Mask links underneath labels so vertical branches never run through the text.
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    foreach (var row in tab.Rows)
      foreach (var id in row)
        if (id != "" && buttons.TryGetValue(id, out var node) && node.State != UpgradeButton.UnlockState.Hidden)
        {
          int center = node.Data.PosX - offset + (int)node.Button.Width / 2;
          batch.Draw(AssetManager.DefaultTexture,
            new Rectangle(center - 132, node.Data.PosY + (int)node.Button.Height + 10, 264, 66),
            GuiPalette.Base(new Color(7, 19, 33, 245)));
        }
    batch.End();
    FontManager.BeginFieldFonts(SystemFont);
    foreach (var row in tab.Rows)
      foreach (var id in row)
      {
        if (id == "" || !buttons.TryGetValue(id, out var button) || button.State == UpgradeButton.UnlockState.Hidden) continue;
        float center = button.Data.PosX - offset + button.Button.Width / 2;
        float y = button.Data.PosY + button.Button.Height + 14;
        bool locked = button.State == UpgradeButton.UnlockState.Revealed;
        string name = Loc.T(button.Data.UpgradeDefinition.Name);
        const int width = Weaponry.ColumnSpacing - 24;
        float size = Math.Min(23, 23 * width / Math.Max(width, Measure2(name, Vector2.Zero, 23).X));
        var measure = Measure2(name, Vector2.Zero, size);
        LayoutFittedSystemText(name, new Vector2(center - measure.X / 2, y), width, size,
          locked ? GuiSkin.MutedTextColor : GuiSkin.ButtonTextColor);
        string rank = button.IsMaxLevel ? Loc.T("MAX") : $"{button.CurrentLevel}/{button.Data.NumLevels}";
        if (!button.IsMaxLevel)
          rank += " · " + NumberFormatter.AbbreviateBigNumber(button.GetNextLevelCost())
            + (button.Data.UpgradeDefinition.Currency == CoreShards.Currency ? " " + Loc.T("shards") : "");
        var rm = Measure2(rank, Vector2.Zero, 20);
        LayoutFittedSystemText(rank, new Vector2(center - rm.X / 2, y + 30), width, 20,
          locked ? GuiSkin.MutedTextColor : tab.Accent);
      }
    FontManager.EndFieldFonts(SystemFont);
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
      : Loc.T("Drag to a hardpoint · Right-click a fitted weapon to remove");
    if (!unlocked && UpgradeManager.CurrentUpgrades.UpgradeButtons.TryGetValue(tab.Root, out var root))
      hint = Loc.F("{0} gems · Click to unlock", NumberFormatter.AbbreviateBigNumber(root.GetNextLevelCost()))
        + (CanAffordUpgradeButton(root) ? "" : " · " + Loc.T("Not enough gems"));
    SetWeaponHover(Loc.T(tab.Name), Loc.T(tab.Description), hint, tab.Accent);
  }

  private void HoverSupport(WeaponSupportModule module, bool fitted)
    => SetWeaponHover(WeaponSupportCatalog.Name(module), WeaponSupportCatalog.Description(module),
      module == WeaponSupportModule.None ? Loc.T("Modules drop while firing on the planet")
        : fitted ? Loc.T("Drag to move · Right-click to return to inventory") : Loc.T("Drag into a support socket"), GuiSkin.PanelAccent);

  // Draw after the Gum upgrade tree so tooltips and dragged icons remain on top.
  private void DrawWeaponryDragOverlay(SpriteBatch batch)
  {
    var pos = new Vector2(GameInput.UiCursor.X, GameInput.UiCursor.Y);
    if (weaponDragging)
    {
      var rect = new Rectangle((int)pos.X - 60, (int)pos.Y - 60, 120, 120);
      DrawEquipmentSocket(batch, rect, GuiSkin.PanelAccent, true);
      if (draggedWeapon is { } weapon) DrawWeaponTurretPreview(batch, weapon, pos, 0.85f, 1);
      if (draggedSupport != WeaponSupportModule.None)
      {
        QueueModuleIcon(WeaponSupportCatalog.Icon(draggedSupport), rect);
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
