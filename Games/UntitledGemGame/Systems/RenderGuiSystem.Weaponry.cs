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

// The Weaponry window: three-column layout inside the main navigation terminal.
// - Left column: Weapon Blueprints (unlock weapon blueprints, select a weapon, or drag to a ship slot).
// - Center column: Homebase Cruiser showcase with 1 fixed bow mount + 4 customizable hull hardpoint slots.
// - Right column: Selected weapon's telemetry readout and 3-column Gum upgrade tree.
public partial class RenderGuiSystem
{
  private int selectedWeaponTab;
  private MainShipWeapon? draggedWeapon;
  private int draggedWeaponSourceSlot = -1;
  private Vector2 weaponPressPosition;
  private bool weaponDragging;

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
    weaponDragging = false;
  }

  private void StartWeaponDrag(MainShipWeapon weapon, int sourceSlot, Vector2 position)
  {
    if (weapon == MainShipWeapon.Cannon) return;
    draggedWeapon = weapon;
    draggedWeaponSourceSlot = sourceSlot;
    weaponPressPosition = position;
    weaponDragging = false;
  }

  // Left-column blueprint cards (5 weapons).
  public static Rectangle WeaponBlueprintCardBounds(int index)
  {
    var col = Weaponry.BlueprintsColumn;
    const int topOffset = 104;
    const int cardHeight = 256;
    const int gap = 22;
    return new Rectangle(col.X, col.Y + topOffset + index * (cardHeight + gap), col.Width, cardHeight);
  }

  private static Rectangle WeaponBlueprintActionBounds(int index)
  {
    var card = WeaponBlueprintCardBounds(index);
    return new Rectangle(card.X + 20, card.Bottom - 68, card.Width - 40, 52);
  }

  // Center-column socket cards: 1 fixed bow card + 4 customizable hardpoint cards on the right half of ShowcaseColumn.
  private static Rectangle WeaponPrimaryMountCardBounds
  {
    get
    {
      var col = Weaponry.ShowcaseColumn;
      int cardWidth = 536;
      return new Rectangle(col.Right - cardWidth - 12, col.Y + 116, cardWidth, 176);
    }
  }

  public static Rectangle WeaponHardpointCardBounds(int slotIndex)
  {
    var col = Weaponry.ShowcaseColumn;
    int cardWidth = 536;
    int firstTop = WeaponPrimaryMountCardBounds.Bottom + 36;
    const int cardHeight = 262;
    const int gap = 24;
    return new Rectangle(col.Right - cardWidth - 12, firstTop + slotIndex * (cardHeight + gap), cardWidth, cardHeight);
  }

  private static Rectangle WeaponHardpointUnequipBounds(int slotIndex)
  {
    var card = WeaponHardpointCardBounds(slotIndex);
    return new Rectangle(card.Right - 54, card.Y + 14, 40, 40);
  }

  private static Rectangle WeaponHardpointActionBounds(int slotIndex)
  {
    var card = WeaponHardpointCardBounds(slotIndex);
    return new Rectangle(card.X + 132, card.Bottom - 56, card.Width - 150, 42);
  }

  private static Rectangle WeaponHeaderFitButtonBounds
  {
    get
    {
      var tree = Weaponry.TreeColumn;
      return new Rectangle(tree.Right - 320, tree.Y + 12, 300, 56);
    }
  }

  // Center-left cruiser diagram transform: maps hull coordinates (like (-17, -30) or (15, -6)) to UI pixels.
  private static Vector2 ShowcaseShipPivot
  {
    get
    {
      var col = Weaponry.ShowcaseColumn;
      return new Vector2(col.X + 286f, col.Y + 820f);
    }
  }

  private const float ShowcaseTexelScale = 1.52f;
  private static float ShowcaseHullUnitScale => ShowcaseTexelScale / PrerenderedShips.HomeBaseScale;

  private static Vector2 HullToShowcaseScreen(Vector2 hullMount)
    => ShowcaseShipPivot + hullMount * ShowcaseHullUnitScale;

  private static Rectangle ShowcaseHullMarkerBounds(int slotIndex)
  {
    var pos = HullToShowcaseScreen(Weaponry.HardpointSlots[slotIndex].HullMount);
    const int size = 44;
    return new Rectangle((int)MathF.Round(pos.X - size / 2f), (int)MathF.Round(pos.Y - size / 2f), size, size);
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
        && WeaponLoadout.Slots.Any(s => s == null))
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
    AdvanceButtonAnimation(ref m_animateButtonClickWeaponry, dt);
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
    var point = new Point((int)cursor.X, (int)cursor.Y);
    bool pressed = mouse.WasButtonPressed(MouseButton.Left);
    bool released = mouse.LeftButton == ButtonState.Released;

    // Handle active weapon drag-and-drop.
    if (draggedWeapon is { } activeDrag)
    {
      weaponDragging |= Vector2.DistanceSquared(position, weaponPressPosition) >= 100f;
      if (!released) return;

      bool changed = false;
      bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(activeDrag);
      if (unlocked && weaponDragging)
      {
        bool droppedOnSlot = false;
        for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
        {
          if (!WeaponHardpointCardBounds(slot).Contains(point) && !ShowcaseHullMarkerBounds(slot).Contains(point))
            continue;
          droppedOnSlot = true;
          changed = WeaponLoadout.TryEquip(activeDrag, slot);
          if (changed)
            AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
          break;
        }

        if (!droppedOnSlot && draggedWeaponSourceSlot >= 0)
        {
          changed = WeaponLoadout.TryUnequipSlot(draggedWeaponSourceSlot);
          if (changed)
            AudioManager.Instance.PlaySound(SoundEvent.ModuleUnequip);
        }
      }

      CancelWeaponDrag();
      if (changed)
        SaveWeaponLoadout();
      return;
    }

    // Right-click on any customizable hardpoint slot unequips its weapon.
    if (mouse.WasButtonPressed(MouseButton.Right))
    {
      for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
      {
        if (WeaponHardpointCardBounds(slot).Contains(point) || ShowcaseHullMarkerBounds(slot).Contains(point))
        {
          if (WeaponLoadout.TryUnequipSlot(slot))
          {
            AudioManager.Instance.PlaySound(SoundEvent.ModuleUnequip);
            SaveWeaponLoadout();
          }
          return;
        }
      }
    }

    if (!pressed) return;

    // 1. Check Right-Column Header "Fit to Ship" button when an unlocked weapon is unequipped.
    var currentTabWeapon = Weaponry.Tabs[selectedWeaponTab].Weapon;
    if (currentTabWeapon != MainShipWeapon.Cannon
      && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(currentTabWeapon)
      && !WeaponLoadout.IsFitted(currentTabWeapon)
      && WeaponHeaderFitButtonBounds.Contains(point))
    {
      if (WeaponLoadout.TryAutoFit(currentTabWeapon))
      {
        AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
        SaveWeaponLoadout();
      }
      return;
    }

    // 2. Check Left-Column Blueprint Cards.
    for (int i = 0; i < Weaponry.Tabs.Length; i++)
    {
      var card = WeaponBlueprintCardBounds(i);
      if (!card.Contains(point)) continue;

      var weapon = Weaponry.Tabs[i].Weapon;
      bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon);
      var actionBounds = WeaponBlueprintActionBounds(i);

      if (!unlocked && weapon != MainShipWeapon.Cannon && actionBounds.Contains(point))
      {
        SelectWeaponTab(i);
        if (!TryUnlockWeaponBlueprint(weapon))
          AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
        return;
      }

      if (unlocked && weapon != MainShipWeapon.Cannon && !WeaponLoadout.IsFitted(weapon) && actionBounds.Contains(point))
      {
        SelectWeaponTab(i);
        if (WeaponLoadout.TryAutoFit(weapon))
        {
          AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
          SaveWeaponLoadout();
        }
        return;
      }

      if (selectedWeaponTab != i)
      {
        SelectWeaponTab(i);
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      }

      if (unlocked && weapon != MainShipWeapon.Cannon)
        StartWeaponDrag(weapon, WeaponLoadout.FindSlot(weapon), position);
      return;
    }

    // 3. Check Primary Bow Mount card.
    if (WeaponPrimaryMountCardBounds.Contains(point))
    {
      if (selectedWeaponTab != 0)
      {
        SelectWeaponTab(0);
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      }
      return;
    }

    // 4. Check 4 Customizable Hardpoint Slot Cards (and hull markers on the ship diagram).
    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
    {
      bool inCard = WeaponHardpointCardBounds(slot).Contains(point);
      bool inMarker = ShowcaseHullMarkerBounds(slot).Contains(point);
      if (!inCard && !inMarker) continue;

      var fitted = WeaponLoadout.Slots[slot];

      // Unequip [X] button in top-right of a fitted slot card.
      if (inCard && fitted != null && WeaponHardpointUnequipBounds(slot).Contains(point))
      {
        if (WeaponLoadout.TryUnequipSlot(slot))
        {
          AudioManager.Instance.PlaySound(SoundEvent.ModuleUnequip);
          SaveWeaponLoadout();
        }
        return;
      }

      // Action button at bottom of slot card ("Fit <Selected Weapon>" when a different unlocked weapon is selected).
      var selectedWeapon = Weaponry.Tabs[selectedWeaponTab].Weapon;
      bool canFitSelected = selectedWeapon != MainShipWeapon.Cannon
        && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(selectedWeapon)
        && fitted != selectedWeapon;

      if (inCard && canFitSelected && (fitted == null || WeaponHardpointActionBounds(slot).Contains(point)))
      {
        if (WeaponLoadout.TryEquip(selectedWeapon, slot))
        {
          AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
          SaveWeaponLoadout();
        }
        return;
      }

      if (inMarker && canFitSelected && fitted == null)
      {
        if (WeaponLoadout.TryEquip(selectedWeapon, slot))
        {
          AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
          SaveWeaponLoadout();
        }
        return;
      }

      if (fitted is { } slotWeapon)
      {
        int tabIndex = (int)slotWeapon;
        if (selectedWeaponTab != tabIndex)
        {
          SelectWeaponTab(tabIndex);
          AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
        }
        StartWeaponDrag(slotWeapon, slot, position);
      }
      return;
    }
  }

  private void DrawWeaponryPanel(SpriteBatch batch, float dt)
  {
    var panel = Weaponry.Panel;
    var tab = Weaponry.Tabs[selectedWeaponTab];
    var cursor = new Point((int)GameInput.UiCursor.X, (int)GameInput.UiCursor.Y);
    var blueprintsCol = Weaponry.BlueprintsColumn;
    var showcaseCol = Weaponry.ShowcaseColumn;
    var treeCol = Weaponry.TreeColumn;

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawNavigationPanelBackground(batch, tab.Accent);
    if (!IsDetached)
    {
      var closeBtn = WeaponryCloseButton;
      GuiSkin.CloseButton(batch, closeBtn, tab.Accent, closeBtn.Contains(cursor));
    }

    // Vertical column dividers separating Blueprints | Ship Showcase & Hardpoints | Weapon Upgrade Tree.
    int divTop = blueprintsCol.Y + 8;
    int divHeight = panel.Bottom - divTop - 36;
    DrawCleanVertDivider(batch, blueprintsCol.Right + 14, divTop, divHeight, GuiSkin.PanelAccent * 0.7f);
    DrawCleanVertDivider(batch, showcaseCol.Right + 14, divTop, divHeight, tab.Accent * 0.75f);
    batch.End();

    DrawWeaponryBlueprintsColumn(batch, cursor);
    DrawWeaponryShowcaseColumn(batch, cursor);
    DrawWeaponryTreeHeader(batch, tab, cursor);
  }

  private void DrawWeaponryBlueprintsColumn(SpriteBatch batch, Point cursor)
  {
    var col = Weaponry.BlueprintsColumn;
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtons;
    var ug = UpgradeManager.Instance.UG;

    DrawFittedSystemText(Loc.Upper(Loc.T("Weapon Blueprints")), new Vector2(col.X, col.Y + 6), col.Width, 34, GuiSkin.StatHeadingColor);
    DrawFittedSystemText(Loc.T("Unlock blueprints & drag or click a hull slot to mount them"),
      new Vector2(col.X, col.Y + 48), col.Width, 21, GuiSkin.MutedTextColor);

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, col.X, col.Y + 84, col.Width, GuiSkin.PanelAccent);
    batch.End();

    for (int i = 0; i < Weaponry.Tabs.Length; i++)
    {
      var tab = Weaponry.Tabs[i];
      var weapon = tab.Weapon;
      var card = WeaponBlueprintCardBounds(i);
      bool selected = selectedWeaponTab == i;
      bool hovered = card.Contains(cursor);
      bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon);
      int fittedSlot = WeaponLoadout.FindSlot(weapon);
      bool isFitted = WeaponLoadout.IsFitted(weapon);

      Color borderColor = selected ? tab.Accent
        : hovered ? Color.Lerp(GuiSkin.ButtonBorderColor, tab.Accent, 0.65f)
        : unlocked ? tab.Accent * 0.55f : GuiSkin.BorderColor * 0.45f;
      Color fillColor = selected
        ? GuiPalette.Base(new Color(14, 34, 58, 242))
        : GuiPalette.Base(new Color(7, 18, 34, unlocked ? 232 : 205));

      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.TechCard(batch, card, fillColor, borderColor, cornerGraphic: selected || hovered, hexWatermark: selected);
      if (selected)
      {
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.X, card.Y + 8, 5, card.Height - 16), tab.Accent);
      }

      // Left turret preview frame inside card.
      var iconBox = new Rectangle(card.X + 20, card.Y + 22, 112, 112);
      GuiSkin.TechCard(batch, iconBox, GuiPalette.Base(new Color(4, 12, 24, 230)), tab.Accent * (unlocked ? 0.75f : 0.35f), cornerGraphic: false, hexWatermark: false);
      batch.End();

      DrawWeaponTurretPreview(batch, weapon, iconBox.Center.ToVector2(), 0.72f, unlocked ? 1f : 0.4f);

      // Title & Role.
      float textLeft = iconBox.Right + 20;
      float textWidth = card.Right - textLeft - 20;
      DrawFittedSystemText(Loc.Upper(Loc.T(tab.Name)), new Vector2(textLeft, card.Y + 20), textWidth - 170, 27,
        unlocked ? tab.Accent : GuiSkin.MutedTextColor);
      DrawFittedSystemText(Loc.T(tab.Role), new Vector2(textLeft, card.Y + 54), textWidth, 20, GuiSkin.MutedTextColor);

      // Status badge in top-right of card.
      var (statusText, statusColor) = weapon == MainShipWeapon.Cannon
        ? (ug.AutoCannon ? Loc.T("BOW  •  AUTO") : Loc.T("BOW  •  MANUAL"), GuiSkin.ConfirmAccent)
        : !unlocked
          ? (Loc.T("LOCKED"), GuiSkin.LockedTextColor)
          : fittedSlot >= 0
            ? (Loc.F("SLOT {0}", HardpointNumerals[fittedSlot]), GuiSkin.ConfirmAccent)
            : (Loc.T("UNEQUIPPED"), new Color(255, 196, 88));

      float statusW = Measure2(statusText, Vector2.Zero, 19).X;
      DrawFittedSystemText(statusText, new Vector2(card.Right - statusW - 22, card.Y + 24), statusW + 8, 19, statusColor);

      // Mid-row summary: DPS & upgrade progress when unlocked, or brief description when locked.
      if (unlocked)
      {
        var (boughtLevels, maxLevels) = CountWeaponUpgradeLevels(buttons, i);
        double dps = MainShipWeapons.GemsPerSecond(ug, weapon,
          MainShipWeapons.FireRate(ug, weapon), MainShipWeapons.FirePower(ug, weapon));
        string dpsSummary = weapon == MainShipWeapon.Cannon && !ug.AutoCannon
          ? Loc.F("Click dmg: {0}  •  Upgrades: {1}/{2}", MainShipWeapons.FirePower(ug, weapon), boughtLevels, maxLevels)
          : isFitted
            ? Loc.F("{0} gems/s  •  Upgrades: {1}/{2}", FormatWeaponStat(dps, 1), boughtLevels, maxLevels)
            : Loc.F("Not fitted  •  Upgrades: {0}/{1}", boughtLevels, maxLevels);
        DrawFittedSystemText(dpsSummary, new Vector2(textLeft, card.Y + 92), textWidth, 21, GuiSkin.StatHeadingColor);
      }
      else
      {
        DrawFittedSystemText(Loc.T(tab.Description), new Vector2(textLeft, card.Y + 92), textWidth, 19, GuiSkin.MutedTextColor);
      }

      // Bottom action bar inside card.
      var actionRect = WeaponBlueprintActionBounds(i);
      if (!unlocked && weapon != MainShipWeapon.Cannon && buttons.TryGetValue(tab.Root, out var rootBtn))
      {
        ulong cost = rootBtn.GetNextLevelCost();
        bool affordable = CanAffordUpgradeButton(rootBtn);
        bool actionHover = actionRect.Contains(cursor);
        DrawPurchaseButton(batch, actionRect, Loc.T("Unlock Blueprint"), cost, affordable, actionHover, 0f, Loc.T("Locked"));
      }
      else if (unlocked && weapon != MainShipWeapon.Cannon && !isFitted)
      {
        bool actionHover = actionRect.Contains(cursor);
        DrawHudButton(batch, actionRect, Loc.T("Fit to Open Hardpoint"), tab.Accent, false, actionHover, 0f);
      }
      else
      {
        batch.Begin(transformMatrix: HudLayout.RenderTransform);
        DrawCleanHorizDivider(batch, actionRect.X, actionRect.Y + 6, actionRect.Width, tab.Accent * 0.28f);
        batch.End();
        string hint = weapon == MainShipWeapon.Cannon
          ? (ug.AutoCannon ? Loc.T("Primary battery automated — click to view upgrades")
            : Loc.T("Unlock Plasma Repeater in the tree to auto-fire"))
          : Loc.F("Mounted in {0} — drag to another hull slot to move",
            Loc.T(Weaponry.HardpointSlots[fittedSlot].Name));
        DrawFittedSystemText(hint, new Vector2(actionRect.X + 4, actionRect.Y + 16), actionRect.Width - 8, 19, GuiSkin.MutedTextColor);
      }
    }
  }

  private void DrawWeaponryShowcaseColumn(SpriteBatch batch, Point cursor)
  {
    var col = Weaponry.ShowcaseColumn;
    var ug = UpgradeManager.Instance.UG;
    double totalDps = MainShipWeapons.GemsPerSecond(ug);

    DrawFittedSystemText(Loc.Upper(Loc.T("Homebase Hardpoints")), new Vector2(col.X, col.Y + 6), col.Width, 34, GuiSkin.StatHeadingColor);
    string subHeader = Loc.F("1 Fixed Bow Mount  •  4 Customizable Slots  •  Auto Output: {0} gems/s",
      FormatWeaponStat(totalDps, 1));
    DrawFittedSystemText(subHeader, new Vector2(col.X, col.Y + 48), col.Width, 21, GuiSkin.MutedTextColor);

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, col.X, col.Y + 84, col.Width, GuiSkin.PanelAccent);
    batch.End();

    // 1. Draw the Homebase Cruiser showcase diagram on the left half of ShowcaseColumn.
    DrawHomebaseHardpointDiagram(batch, cursor);

    // 2. Draw the Primary Bow Mount card at the top-right of ShowcaseColumn.
    DrawPrimaryBowMountCard(batch, cursor);

    // 3. Draw the 4 Customizable Hardpoint Slot cards below it.
    for (int slot = 0; slot < Weaponry.MaxCustomSlots; slot++)
    {
      DrawCustomizableHardpointCard(batch, slot, cursor);
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
    DrawHardpointLeaderLine(batch, bowHullPos, new Vector2(bowCard.X, bowCard.Center.Y), bowColor);

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

      DrawHardpointLeaderLine(batch, hullPos, new Vector2(card.X, card.Center.Y),
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
    string diagramCaption = Loc.T("Drag weapons onto any hardpoint I–IV, or right-click a slot to unequip.");
    DrawFittedSystemText(diagramCaption, new Vector2(col.X + 8, col.Bottom - 44), 520, 20, GuiSkin.MutedTextColor);
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

    int x1 = (int)MathF.Round(elbowX);
    int w1 = (int)MathF.Round(cardLeftCenter.X - elbowX);
    if (w1 > 0)
      batch.Draw(pixel, new Rectangle(x1, (int)MathF.Round(cardLeftCenter.Y) - 1, w1, 2), color);
  }

  private void DrawPrimaryBowMountCard(SpriteBatch batch, Point cursor)
  {
    var card = WeaponPrimaryMountCardBounds;
    var cannonTab = Weaponry.Tabs[0];
    var ug = UpgradeManager.Instance.UG;
    bool selected = selectedWeaponTab == 0;
    bool hovered = card.Contains(cursor);

    Color border = selected ? cannonTab.Accent : hovered ? cannonTab.Accent * 0.85f : cannonTab.Accent * 0.5f;
    Color fill = selected ? GuiPalette.Base(new Color(14, 34, 58, 240)) : GuiPalette.Base(new Color(8, 20, 38, 228));

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.TechCard(batch, card, fill, border, cornerGraphic: selected || hovered, hexWatermark: false);
    var socketRect = new Rectangle(card.X + 18, card.Y + 38, 96, 96);
    GuiSkin.TechCard(batch, socketRect, GuiPalette.Base(new Color(4, 12, 24, 230)), cannonTab.Accent * 0.8f, cornerGraphic: false, hexWatermark: false);
    batch.End();

    DrawWeaponTurretPreview(batch, MainShipWeapon.Cannon, socketRect.Center.ToVector2(), 0.66f, 1f);

    float left = socketRect.Right + 18;
    float width = card.Right - left - 18;
    DrawFittedSystemText(Loc.Upper(Loc.T(Weaponry.PrimarySlotName)), new Vector2(left, card.Y + 16), width - 120, 20, cannonTab.Accent);
    string fixedBadge = Loc.T("DEFAULT SLOT");
    float badgeW = Measure2(fixedBadge, Vector2.Zero, 17).X;
    DrawFittedSystemText(fixedBadge, new Vector2(card.Right - badgeW - 18, card.Y + 16), badgeW + 6, 17, GuiSkin.MutedTextColor);

    DrawFittedSystemText(Loc.Upper(Loc.T(cannonTab.Name)), new Vector2(left, card.Y + 46), width, 26, GuiSkin.StatHeadingColor);

    double dps = MainShipWeapons.GemsPerSecond(ug, MainShipWeapon.Cannon,
      MainShipWeapons.FireRate(ug, MainShipWeapon.Cannon), MainShipWeapons.FirePower(ug, MainShipWeapon.Cannon));
    string modeLine = ug.AutoCannon
      ? Loc.F("Auto + Manual  •  {0} gems/s", FormatWeaponStat(dps, 1))
      : Loc.F("Manual Click ({0} dmg/shot)  •  Upgrade to automate", MainShipWeapons.FirePower(ug, MainShipWeapon.Cannon));
    DrawFittedSystemText(modeLine, new Vector2(left, card.Y + 84), width, 20,
      ug.AutoCannon ? GuiSkin.ConfirmAccent : GuiSkin.MutedTextColor);
    DrawFittedSystemText(Loc.T("Click to inspect & upgrade primary cannon"), new Vector2(left, card.Y + 118), width, 18, GuiSkin.MutedTextColor);
  }

  private void DrawCustomizableHardpointCard(SpriteBatch batch, int slotIndex, Point cursor)
  {
    var slotDef = Weaponry.HardpointSlots[slotIndex];
    var card = WeaponHardpointCardBounds(slotIndex);
    var fitted = WeaponLoadout.Slots[slotIndex];
    var selectedWeapon = Weaponry.Tabs[selectedWeaponTab].Weapon;
    var ug = UpgradeManager.Instance.UG;
    bool hovered = card.Contains(cursor) || ShowcaseHullMarkerBounds(slotIndex).Contains(cursor);
    bool isDragTarget = draggedWeapon != null && weaponDragging && hovered;
    bool isSelectedSlot = fitted != null && (int)fitted.Value == selectedWeaponTab;

    Color accent = fitted is { } fw
      ? Weaponry.ForWeapon(fw).Accent
      : isDragTarget && draggedWeapon is { } dw
        ? Weaponry.ForWeapon(dw).Accent
        : GuiSkin.PanelAccent;

    Color border = isDragTarget || isSelectedSlot
      ? accent
      : hovered ? accent * 0.85f
      : fitted != null ? accent * 0.58f : GuiSkin.BorderColor * 0.48f;
    Color fill = isDragTarget || isSelectedSlot
      ? GuiPalette.Base(new Color(14, 34, 58, 240))
      : GuiPalette.Base(new Color(7, 18, 34, fitted != null ? 232 : 204));

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.TechCard(batch, card, fill, border, cornerGraphic: isSelectedSlot || hovered, hexWatermark: isSelectedSlot);
    var socketRect = new Rectangle(card.X + 18, card.Y + 52, 98, 98);
    GuiSkin.TechCard(batch, socketRect, GuiPalette.Base(new Color(4, 12, 24, 230)),
      accent * (fitted != null || isDragTarget ? 0.85f : 0.4f), cornerGraphic: false, hexWatermark: false);
    batch.End();

    float left = socketRect.Right + 18;
    float width = card.Right - left - 18;
    string slotHeader = Loc.F("{0}  •  {1}", Loc.Upper(Loc.T(slotDef.Name)), Loc.T(slotDef.Subtitle));
    DrawFittedSystemText(slotHeader, new Vector2(card.X + 18, card.Y + 16), card.Width - 84, 20, accent);

    bool canFitSelected = selectedWeapon != MainShipWeapon.Cannon
      && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(selectedWeapon)
      && fitted != selectedWeapon;

    if (fitted is { } weapon && UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon))
    {
      var wTab = Weaponry.ForWeapon(weapon);
      DrawWeaponTurretPreview(batch, weapon, socketRect.Center.ToVector2(), 0.68f, 1f);

      // Unequip [X] button in top-right corner of card.
      var unequipRect = WeaponHardpointUnequipBounds(slotIndex);
      bool unequipHover = unequipRect.Contains(cursor);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.CloseButton(batch, unequipRect, accent, unequipHover);
      batch.End();

      DrawFittedSystemText(Loc.Upper(Loc.T(wTab.Name)), new Vector2(left, card.Y + 54), width, 26, GuiSkin.StatHeadingColor);
      DrawFittedSystemText(Loc.T(wTab.Role), new Vector2(left, card.Y + 86), width, 19, GuiSkin.MutedTextColor);

      double dps = MainShipWeapons.GemsPerSecond(ug, weapon,
        MainShipWeapons.FireRate(ug, weapon), MainShipWeapons.FirePower(ug, weapon));
      string statLine = Loc.F("ACTIVE  •  {0} gems/s  •  Power {1}",
        FormatWeaponStat(dps, 1), MainShipWeapons.FirePower(ug, weapon));
      DrawFittedSystemText(statLine, new Vector2(left, card.Y + 118), width, 20, GuiSkin.ConfirmAccent);

      var actionRect = WeaponHardpointActionBounds(slotIndex);
      if (canFitSelected)
      {
        var selTab = Weaponry.ForWeapon(selectedWeapon);
        string replaceLabel = WeaponLoadout.FindSlot(selectedWeapon) >= 0
          ? Loc.F("Swap with {0}", Loc.T(selTab.Name))
          : Loc.F("Replace with {0}", Loc.T(selTab.Name));
        DrawHudButton(batch, actionRect, replaceLabel, selTab.Accent, false, actionRect.Contains(cursor), 0f);
      }
      else
      {
        DrawFittedSystemText(Loc.T("Drag to another slot to swap  •  Right-click to unequip"),
          new Vector2(left, actionRect.Y + 10), width, 18, GuiSkin.MutedTextColor);
      }
    }
    else
    {
      // Empty socket.
      string numeral = HardpointNumerals[slotIndex];
      var numMeasure = Measure2(numeral, Vector2.Zero, 30);
      FontManager.RenderFieldFont(SystemFont, numeral,
        new Vector2(socketRect.Center.X - numMeasure.X / 2f, socketRect.Center.Y - numMeasure.Y / 2f),
        GuiSkin.MutedTextColor * 0.65f, Color.Black, 30);

      DrawFittedSystemText(Loc.T("EMPTY HARDPOINT"), new Vector2(left, card.Y + 58), width, 24, GuiSkin.MutedTextColor);
      DrawFittedSystemText(Loc.T("No weapon turret fitted in this hull slot."),
        new Vector2(left, card.Y + 92), width, 19, GuiSkin.MutedTextColor * 0.85f);

      var actionRect = WeaponHardpointActionBounds(slotIndex);
      if (canFitSelected)
      {
        var selTab = Weaponry.ForWeapon(selectedWeapon);
        string fitLabel = WeaponLoadout.FindSlot(selectedWeapon) >= 0
          ? Loc.F("Move {0} Here", Loc.T(selTab.Name))
          : Loc.F("Fit {0} Here", Loc.T(selTab.Name));
        DrawHudButton(batch, actionRect, fitLabel, selTab.Accent, false, hovered || actionRect.Contains(cursor), 0f);
      }
      else
      {
        DrawFittedSystemText(Loc.T("Unlock a weapon blueprint on the left to mount it here"),
          new Vector2(left, actionRect.Y + 10), width, 18, GuiSkin.MutedTextColor * 0.8f);
      }
    }
  }

  private void DrawWeaponryTreeHeader(SpriteBatch batch, Weaponry.Tab tab, Point cursor)
  {
    var tree = Weaponry.TreeColumn;
    var ug = UpgradeManager.Instance.UG;
    var weapon = tab.Weapon;
    bool unlocked = UpgradeManager.Instance.IsWeaponBlueprintUnlocked(weapon);
    int fittedSlot = WeaponLoadout.FindSlot(weapon);
    bool isFitted = WeaponLoadout.IsFitted(weapon);

    float x = tree.X + 12;
    float width = tree.Width - 24;
    float y = tree.Y + 6;

    DrawFittedSystemText(Loc.Upper(Loc.T(tab.Name)), new Vector2(x, y), width - 340, 38, tab.Accent);
    DrawFittedSystemText(Loc.T(tab.Role), new Vector2(x, y + 46), width - 340, 21, GuiSkin.MutedTextColor);

    if (weapon != MainShipWeapon.Cannon && unlocked && !isFitted)
    {
      var fitBtn = WeaponHeaderFitButtonBounds;
      DrawHudButton(batch, fitBtn, Loc.T("Auto-Fit to Ship"), tab.Accent, false, fitBtn.Contains(cursor), 0f);
    }
    else
    {
      string mountStatus = weapon == MainShipWeapon.Cannon
        ? Loc.F("MOUNTED: {0}", Loc.Upper(Loc.T(Weaponry.PrimarySlotName)))
        : !unlocked
          ? Loc.T("BLUEPRINT LOCKED")
          : Loc.F("MOUNTED: {0}", Loc.Upper(Loc.T(Weaponry.HardpointSlots[fittedSlot].Name)));
      Color statusColor = !unlocked ? GuiSkin.LockedTextColor : GuiSkin.ConfirmAccent;
      float statusW = Measure2(mountStatus, Vector2.Zero, 21).X;
      DrawFittedSystemText(mountStatus, new Vector2(tree.Right - statusW - 16, y + 14), statusW + 8, 21, statusColor);
    }

    y += 84;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, (int)x, (int)y, (int)width, tab.Accent);
    batch.End();
    y += 16;

    DrawFittedSystemText(Loc.T(tab.Description), new Vector2(x, y), width, 21, GuiSkin.ButtonTextColor);
    y += 36;

    // Telemetry strip across top of the upgrade tree.
    float fireRate = MainShipWeapons.FireRate(ug, weapon);
    int firePower = MainShipWeapons.FirePower(ug, weapon);
    double specialMult = MainShipWeapons.SpecialMultiplier(ug, weapon);
    double dps = MainShipWeapons.GemsPerSecond(ug, weapon, fireRate, firePower);

    string cycleLabel = weapon switch
    {
      MainShipWeapon.Cannon => Loc.F("Cadence: {0}s/shot", FormatWeaponStat(MainShipWeapons.CannonShotInterval(fireRate), 2)),
      MainShipWeapon.Laser => Loc.F("Beams: {0}x  •  Rate: {1}x", MainShipWeapons.LaserBeams(ug), FormatWeaponStat(fireRate, 2)),
      MainShipWeapon.Harpoon => Loc.F("Cycle: {0}s  •  Pulses: {1}", FormatWeaponStat(MainShipWeapons.HarpoonCycleTime(ug, fireRate), 1), MainShipWeapons.HarpoonPulseCount(ug)),
      MainShipWeapon.Rockets => Loc.F("Salvo: {0} rockets / {1}s", MainShipWeapons.RocketsPerSalvo(ug), FormatWeaponStat(MainShipWeapons.RocketSalvoInterval(fireRate), 1)),
      MainShipWeapon.Railgun => Loc.F("Charge Cycle: {0}s  •  Shards: {1}", FormatWeaponStat(MainShipWeapons.RailgunCycleTime(fireRate), 1), Math.Max(1, ug.RailgunFragments)),
      _ => ""
    };

    string telemetryLine = Loc.F("Fire Power: {0}    •    {1}    •    Special Multiplier: {2}x    •    Auto Output: {3} gems/s",
      firePower, cycleLabel, FormatWeaponStat(specialMult, 2), FormatWeaponStat(dps, 1));
    DrawFittedSystemText(telemetryLine, new Vector2(x, y), width, 21, GuiSkin.StatValueColor);

    y += 36;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, (int)x, (int)y, (int)width, tab.Accent * 0.55f);
    batch.End();

    // Subtle row guide lines behind the 5 upgrade rows.
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    for (int row = 1; row < tab.Rows.Length; row++)
    {
      int midY = (int)((Weaponry.RowCenterY(row - 1) + Weaponry.RowCenterY(row)) * 0.5f);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle((int)x, midY, (int)width, 1), tab.Accent * 0.09f);
    }
    batch.End();
  }

  private void DrawWeaponryTreeLabels(SpriteBatch batch)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtons;
    var tab = Weaponry.Tabs[selectedWeaponTab];
    int offset = selectedWeaponTab * Weaponry.TabStride;
    var nodes = buttons.Values
      .Where(b => Weaponry.TabOf(b.Data.ShortName) == selectedWeaponTab && b.State != UpgradeButton.UnlockState.Hidden)
      .ToList();

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    foreach (var button in nodes)
    {
      if (button.State == UpgradeButton.UnlockState.Revealed)
      {
        batch.Draw(AssetManager.DefaultTexture,
          new Rectangle(button.Data.PosX - offset, button.Data.PosY, (int)button.Button.Width, (int)button.Button.Height),
          GuiPalette.Base(new Color(3, 7, 12, 175)));
      }
    }
    batch.End();

    FontManager.BeginFieldFonts(SystemFont);
    foreach (var button in nodes)
    {
      bool locked = button.State == UpgradeButton.UnlockState.Revealed;
      bool isRoot = Weaponry.Locate(button.Data.ShortName)?.Row == 0;
      bool isCoreShard = button.Data.UpgradeDefinition.Currency == CoreShards.Currency;
      ulong cost = button.GetNextLevelCost();
      float left = button.Data.PosX - offset + button.Button.Width + 14;
      float middle = button.Data.PosY + button.Button.Height / 2f;
      float width = Weaponry.ColumnSpacing - button.Button.Width - 28;

      Color titleColor = locked
        ? GuiSkin.MutedTextColor
        : isRoot || isCoreShard
          ? tab.Accent
          : GuiSkin.ButtonTextColor;
      LayoutFittedSystemText(Loc.T(button.Data.UpgradeDefinition.Name), new Vector2(left, middle - 28), width,
        isRoot ? 23 : 21, titleColor);

      string rank;
      if (button.IsMaxLevel)
      {
        rank = button.Data.NumLevels <= 1
          ? Loc.T("INSTALLED")
          : Loc.F("MAX  {0}/{1}", button.CurrentLevel, button.Data.NumLevels);
      }
      else if (isCoreShard)
      {
        rank = Loc.P((long)cost, "{0} core shard", "{0} core shards");
      }
      else
      {
        rank = button.Data.NumLevels <= 1
          ? $"{NumberFormatter.AbbreviateBigNumber(cost)}  •  {Loc.T("Unlock")}"
          : $"{NumberFormatter.AbbreviateBigNumber(cost)}  •  {button.CurrentLevel}/{button.Data.NumLevels}";
      }

      Color rankColor = locked
        ? GuiSkin.MutedTextColor
        : button.IsMaxLevel
          ? GuiSkin.ConfirmAccent
          : tab.Accent;
      LayoutFittedSystemText(rank, new Vector2(left, middle + 2), width, 18, rankColor);
    }
    FontManager.EndFieldFonts(SystemFont);
  }

  private void DrawWeaponryDragOverlay(SpriteBatch batch)
  {
    if (draggedWeapon is not { } weapon || !weaponDragging) return;
    var cursor = GameInput.UiCursor;
    var pos = new Vector2(cursor.X, cursor.Y);
    var tab = Weaponry.ForWeapon(weapon);

    var badgeRect = new Rectangle((int)pos.X - 64, (int)pos.Y - 64, 128, 128);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.TechCard(batch, badgeRect, GuiPalette.Base(new Color(8, 22, 42, 235)), tab.Accent, cornerGraphic: true, hexWatermark: false);
    batch.End();

    DrawWeaponTurretPreview(batch, weapon, pos, 0.85f, 1f);
  }

  private static (int Bought, int Total) CountWeaponUpgradeLevels(Dictionary<string, UpgradeButton> buttons, int tabIndex)
  {
    int bought = 0;
    int total = 0;
    foreach (var row in Weaponry.Tabs[tabIndex].Rows)
    {
      foreach (string id in row)
      {
        if (string.IsNullOrEmpty(id) || !buttons.TryGetValue(id, out var btn)) continue;
        bought += btn.CurrentLevel;
        total += btn.Data.NumLevels;
      }
    }
    return (bought, total);
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

    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(fleet.Texture, center, baseRegion.Bounds, Color.White * alpha, 0f,
      fleet.Pivot(basePath), scale, SpriteEffects.None, 0f);
    batch.Draw(fleet.Texture, center, headRegion.Bounds, Color.White * alpha, rotation,
      headOrigin, scale, SpriteEffects.None, 0f);
    batch.End();
  }
}
