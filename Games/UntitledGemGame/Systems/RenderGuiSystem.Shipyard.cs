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
using UntitledGemGame;
using UntitledGemGame.Entities;

public partial class RenderGuiSystem
{
  private int selectedShipyardTab;
  private bool shipyardDiscoverySelected;
  private static Rectangle DiscoveryTab => new(64, ShipyardPanel.Bottom - 148, 400, 148);
  private int selectedModuleSlot;
  private ShipModule draggedModule;
  private int draggedModuleSource = -1;
  private Vector2 modulePressPosition;
  private bool moduleDragging;
  private int moduleHoverTab = -1;
  private float moduleHoverTabTime;

  private const int ModuleTileSize = 240;
  private const int ModuleTileSpacing = 20;
  private static ShipyardModules ModuleInventory => UpgradeManager.Instance.Modules;
  private static readonly UpgradesGeneratorUpgrades ShipyardBaseUpgrades = new();
  private static Rectangle ShipyardPanel => new(500, 196, HudLayout.Width - 564, HudLayout.ContentBottom - 240);
  private static int ModulesLeft => ShipyardPanel.X + 530;
  private static int ModulesWidth => ShipyardPanel.Right - ModulesLeft - 48;
  private static Rectangle ModuleSlot(int slot) => new(ModulesLeft + slot % 2 * (ModulesWidth / 2 + 8),
    ShipyardPanel.Y + 210 + slot / 2 * 240, ModulesWidth / 2 - 8, 220);
  private static Rectangle RemoveModuleButton(int slot)
  {
    var bounds = ModuleSlot(slot);
    return new Rectangle(bounds.X + 224, bounds.Bottom - 78, 140, 54);
  }
  private static int ModuleGridWidth => ModulesWidth - 48;
  private static int ModuleGridMaxHeight => ShipyardPanel.Height - 908;
  private static int ModuleColumns
  {
    get
    {
      // Fit the full collection, so equipping a module never resizes the tiles.
      int count = ModuleCatalog.InventoryOrder.Length;
      int bestColumns = 1, bestSize = 0;
      for (int columns = 1; columns <= count; columns++)
      {
        int rows = (count + columns - 1) / columns;
        int width = (ModuleGridWidth - (columns - 1) * ModuleTileSpacing) / columns;
        int height = (ModuleGridMaxHeight - (rows - 1) * ModuleTileSpacing) / rows;
        int size = Math.Min(ModuleTileSize, Math.Min(width, height));
        if (size > bestSize) { bestColumns = columns; bestSize = size; }
      }
      return bestColumns;
    }
  }
  private static int ModuleTileWidth => Math.Min(ModuleTileSize,
    (ModuleGridWidth - (ModuleColumns - 1) * ModuleTileSpacing) / ModuleColumns);
  private static int ModuleTileHeight
  {
    get
    {
      int rows = (ModuleCatalog.InventoryOrder.Length + ModuleColumns - 1) / ModuleColumns;
      return Math.Min(ModuleTileSize, (ModuleGridMaxHeight - (rows - 1) * ModuleTileSpacing) / rows);
    }
  }
  private static Rectangle ModuleInventoryBounds
  {
    get
    {
      int count = ModuleInventory.GetAvailableModules().Count();
      int rows = Math.Max(1, (count + ModuleColumns - 1) / ModuleColumns);
      int height = rows * ModuleTileHeight + (rows - 1) * ModuleTileSpacing + 48;
      return new Rectangle(ModulesLeft, ShipyardPanel.Y + 780, ModulesWidth, height);
    }
  }
  private static Rectangle ModuleGridBounds
  {
    get
    {
      var bounds = ModuleInventoryBounds;
      return new Rectangle(bounds.X + 24, bounds.Y + 24, ModuleGridWidth, bounds.Height - 48);
    }
  }
  private static Rectangle ModuleTile(int index) => new(
    ModuleGridBounds.X + index % ModuleColumns * (ModuleTileWidth + ModuleTileSpacing),
    ModuleGridBounds.Y + index / ModuleColumns * (ModuleTileHeight + ModuleTileSpacing), ModuleTileWidth, ModuleTileHeight);

  private static readonly string[] ShipyardNames =
    [Loc.N("Drifter"), Loc.N("Seeker"), Loc.N("Prospector"), Loc.N("Trove Hunter"), Loc.N("Rimrunner")];
  // Visual positions for each ship type; equipment indices stay tied to their type.
  private static readonly int[] ShipyardTabPositions = [0, 1, 3, 4, 2];

  private static MonoGame.Extended.Graphics.Texture2DRegion ShipyardTexture(int index) => index switch
  {
    0 => TextureCache.HarvesterShip,
    1 => TextureCache.AdvancedHarvesterShip,
    2 => TextureCache.ExpertHarvesterShip,
    3 => TextureCache.UltimateHarvesterShip,
    _ => TextureCache.PerimeterHarvesterShip
  };

  private static Rectangle ShipyardTab(int index)
  {
    int height = Math.Min(160, (ShipyardPanel.Height - DiscoveryTab.Height - 24) / ShipyardNames.Length);
    return new Rectangle(64, 196 + ShipyardTabPositions[index] * height, 400, height - 12);
  }

  private void CancelModuleDrag()
  {
    draggedModule = ShipModule.None;
    draggedModuleSource = -1;
    moduleDragging = false;
    moduleHoverTab = -1;
    moduleHoverTabTime = 0;
  }

  private void StartModuleDrag(ShipModule module, int source, Vector2 position)
  {
    draggedModule = module;
    draggedModuleSource = source;
    modulePressPosition = position;
    moduleDragging = false;
  }

  private void SaveModuleLoadout()
  {
    UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance.SaveProgress();
  }

  private void UpdateShipyardInput(float dt)
  {
    if (!UpgradeManager.Instance.UGM.ShipyardUnlocked) { CancelModuleDrag(); return; }
    var mouse = GameInput.Mouse;
    var cursor = GameInput.UiCursor;
    var position = new Vector2(cursor.X, cursor.Y);
    bool pressed = mouse.WasButtonPressed(MouseButton.Left);
    bool released = mouse.LeftButton == ButtonState.Released;
    if (pressed && HudLayout.NavigationButton(HudLayout.ShipyardTab).Contains(position))
    {
      SetUpgradeType(m_upgradeWindowType == UpgradeTypes.Shipyard ? UpgradeTypes.None : UpgradeTypes.Shipyard);
      m_animateButtonClickShipyard = 0.001f;
      return;
    }
    if (m_upgradeWindowType != UpgradeTypes.Shipyard || (IsDetached && !IsPopoutFocused))
    { CancelModuleDrag(); return; }
    selectedModuleSlot = Math.Clamp(selectedModuleSlot, 0, ModuleCatalog.UnlockedSlots - 1);
    if (KeyboardExtended.GetState().WasKeyPressed(Keys.Escape)) { CancelModuleDrag(); return; }

    if (pressed && DiscoveryTab.Contains(position))
    {
      CancelModuleDrag();
      shipyardDiscoverySelected = true;
      return;
    }
    if (shipyardDiscoverySelected)
    {
      if (pressed)
        for (int i = 0; i < ShipyardNames.Length; i++)
          if (ShipyardTab(i).Contains(position))
          {
            selectedShipyardTab = i;
            selectedModuleSlot = 0;
            shipyardDiscoverySelected = false;
            revealingModule = ShipModule.None;
            return;
          }
      return;
    }

    if (pressed && draggedModule == ShipModule.None)
      for (int i = 0; i < ShipyardNames.Length; i++)
        if (ShipyardTab(i).Contains(position)) { selectedShipyardTab = i; selectedModuleSlot = 0; return; }
    if (!UpgradeManager.Instance.IsHarvesterDiscovered(selectedShipyardTab)) { CancelModuleDrag(); return; }
    var available = ModuleInventory.GetAvailableModules().ToArray();
    if (draggedModule != ShipModule.None)
    {
      moduleDragging |= Vector2.DistanceSquared(position, modulePressPosition) >= 100f;
      // Hovering a ship tab while dragging lets equipment move between ship types.
      if (moduleDragging && !released)
      {
        int tab = -1;
        for (int i = 0; i < ShipyardNames.Length; i++)
          if (ShipyardTab(i).Contains(position) && UpgradeManager.Instance.IsHarvesterDiscovered(i)) tab = i;
        if (tab != moduleHoverTab) { moduleHoverTab = tab; moduleHoverTabTime = 0; }
        if (tab >= 0 && tab != selectedShipyardTab)
        {
          moduleHoverTabTime += dt;
          if (moduleHoverTabTime >= 0.4f) { selectedShipyardTab = tab; selectedModuleSlot = 0; }
        }
      }
      if (!released) return;
      bool changed = false;
      bool validSource = draggedModuleSource < 0 ? ModuleInventory.IsAvailable(draggedModule)
        : ModuleInventory.Slots[draggedModuleSource] == draggedModule;
      if (validSource && moduleDragging)
      {
        for (int slot = 0; slot < ModuleCatalog.UnlockedSlots; slot++)
        {
          if (!ModuleSlot(slot).Contains(position)) continue;
          changed = draggedModuleSource < 0
            ? ModuleInventory.TryEquip(selectedShipyardTab, slot, draggedModule)
            : ModuleInventory.TryMoveSlot(draggedModuleSource, selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + slot);
          selectedModuleSlot = slot;
          break;
        }
        if (draggedModuleSource >= 0 && ModuleInventoryBounds.Contains(position))
          changed = ModuleInventory.TryEquip(draggedModuleSource / ModuleCatalog.MaxSlotsPerType,
            draggedModuleSource % ModuleCatalog.MaxSlotsPerType, ShipModule.None);
      }
      else if (validSource && draggedModuleSource < 0)
      {
        // A short click still equips into the selected slot.
        changed = ModuleInventory.TryEquip(selectedShipyardTab, selectedModuleSlot, draggedModule);
        if (changed)
          for (int offset = 1; offset < ModuleCatalog.UnlockedSlots; offset++)
          {
            int next = (selectedModuleSlot + offset) % ModuleCatalog.UnlockedSlots;
            if (ModuleInventory.Slots[selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + next] != ShipModule.None) continue;
            selectedModuleSlot = next;
            break;
          }
      }
      CancelModuleDrag();
      if (changed) SaveModuleLoadout();
      return;
    }

    if (pressed)
      for (int i = 0; i < ShipyardNames.Length; i++)
        if (ShipyardTab(i).Contains(position)) { selectedShipyardTab = i; selectedModuleSlot = 0; return; }
    for (int slot = 0; slot < ModuleCatalog.UnlockedSlots; slot++)
    {
      if (!ModuleSlot(slot).Contains(position)) continue;
      if (mouse.WasButtonPressed(MouseButton.Right) || (pressed && RemoveModuleButton(slot).Contains(position)))
      {
        if (ModuleInventory.TryEquip(selectedShipyardTab, slot, ShipModule.None)) SaveModuleLoadout();
        return;
      }
      if (!pressed) return;
      selectedModuleSlot = slot;
      int source = selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + slot;
      StartModuleDrag(ModuleInventory.Slots[source], source, position);
      return;
    }
    if (!pressed) return;
    for (int index = 0; index < available.Length; index++)
      if (ModuleTile(index).Contains(position)) { StartModuleDrag(available[index], -1, position); return; }
  }

  private void DrawShipyardNavigation(SpriteBatch batch)
  {
    if (!UpgradeManager.Instance.UGM.ShipyardUnlocked) return;
    var bounds = HudLayout.NavigationButton(HudLayout.ShipyardTab);
    bool selected = m_upgradeWindowType == UpgradeTypes.Shipyard;
    bool pending = ModuleInventory.PendingReveals.Count > 0;
    DrawHudButton(batch, bounds, selected ? Loc.T("Hide") : pending ? Loc.T("Shipyard") + " !" : Loc.T("Shipyard"), OrbitSkin.ShipyardAccent,
      selected, bounds.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y), m_animateButtonClickShipyard, tab: true);
  }

  private static void ShipyardLabel(string text, Vector2 position, float size, Color color)
    => FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
      text, position, color, Color.Black, size);

  private static Color ModuleColor(ShipModule module) => module == ShipModule.None
    ? OrbitSkin.ButtonBorderColor : ModuleRarityColor(ModuleCatalog.Rarities[(int)module]);

  private static Color ModuleRarityColor(ModuleRarity rarity) => rarity switch
    {
      ModuleRarity.Common => OrbitSkin.CommonRarity,
      ModuleRarity.Uncommon => OrbitSkin.UncommonRarity,
      ModuleRarity.Rare => OrbitSkin.RareRarity,
      ModuleRarity.Epic => OrbitSkin.EpicRarity,
      ModuleRarity.Legendary => OrbitSkin.LegendaryRarity,
      ModuleRarity.Mythic => OrbitSkin.MythicRarity,
      _ => OrbitSkin.ButtonBorderColor
    };

  private static string ModuleRarityName(ModuleRarity rarity) => rarity switch
    {
      ModuleRarity.Common => Loc.T("Common"),
      ModuleRarity.Uncommon => Loc.T("Uncommon"),
      ModuleRarity.Rare => Loc.T("Rare"),
      ModuleRarity.Epic => Loc.T("Epic"),
      ModuleRarity.Legendary => Loc.T("Legendary"),
      ModuleRarity.Mythic => Loc.T("Mythic"),
      _ => rarity.ToString()
    };

  private static string ModuleRarityLabel(ModuleRarity rarity) => Loc.Upper(ModuleRarityName(rarity));

  private static string ModuleRarityLabel(ShipModule module) => ModuleRarityLabel(ModuleCatalog.Rarities[(int)module]);

  private static void DrawShipyardShip(SpriteBatch batch, int index, Rectangle bounds)
  {
    var region = ShipyardTexture(index);
    if (region == null) return;
    float scale = Math.Min(bounds.Width / (float)region.Width, bounds.Height / (float)region.Height);
    batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(region.Texture, new Vector2(bounds.Center.X, bounds.Center.Y), region.Bounds, Color.White,
      0f, new Vector2(region.Width, region.Height) / 2, scale, SpriteEffects.None, 0f);
    batch.End();
  }

  private static void DrawModulePanel(SpriteBatch batch, Rectangle bounds, Color border, bool subdued = false)
  {
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    batch.Draw(AssetManager.DefaultTexture, bounds, subdued ? OrbitSkin.LockedSurfaceColor : OrbitSkin.SurfaceColor);
    OrbitSkin.NineSlice(batch, "button_idle_blue", bounds, opacity: subdued ? .45f : 1f);
    // A single frame and a narrow rarity accent match the discovery cards.
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(bounds.X + 12, bounds.Y + 4,
      Math.Max(1, bounds.Width - 24), 5), border);
    batch.End();
  }

  private void QueueModuleIcon(ShipModule module, Rectangle bounds, float opacity = 1f)
  {
    if (module == ShipModule.None) return;
    var region = TextureCache.ModuleIcons[(int)module];
    QueueIcon(region, new Vector2(bounds.Center.X, bounds.Center.Y),
      Math.Min(bounds.Width / (float)Math.Max(1, region.Width), bounds.Height / (float)Math.Max(1, region.Height)),
      Color.White * opacity);
  }

  private void DrawModuleTooltip(SpriteBatch batch, ShipModule module, Vector2 cursor, bool equipped)
  {
    const int width = 780;
    var lines = new List<string>();
    string line = "";
    foreach (var (word, join) in Loc.WrapPieces(Loc.T(ModuleCatalog.Descriptions[(int)module])))
    {
      string next = line.Length == 0 ? word : line + join + word;
      if (line.Length > 0 && Measure2(next, Vector2.Zero, 26).X > width - 48)
      { lines.Add(line); line = word; }
      else line = next;
    }
    if (line.Length > 0) lines.Add(line);
    int height = 188 + lines.Count * 36;
    int x = Math.Clamp((int)cursor.X + 28, 24, HudLayout.Width - width - 24);
    int y = (int)cursor.Y + 32;
    if (y + height > ShipyardPanel.Bottom) y = (int)cursor.Y - height - 24;
    y = Math.Max(ShipyardPanel.Top, y);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Tooltip(batch, new Rectangle(x, y, width, height));
    batch.End();
    ShipyardLabel(Loc.T(ModuleCatalog.Names[(int)module]), new Vector2(x + 24, y + 20), 32, ModuleColor(module));
    ShipyardLabel(ModuleRarityLabel(module), new Vector2(x + 24, y + 66), 22, ModuleColor(module));
    for (int i = 0; i < lines.Count; i++)
      ShipyardLabel(lines[i], new Vector2(x + 24, y + 108 + i * 36), 26, OrbitSkin.ButtonTextColor);
    ShipyardLabel(equipped ? Loc.T("Drag to a slot or back to inventory. Right-click to remove.")
      : Loc.T("Drag into a slot, or click to equip in the selected slot."),
      new Vector2(x + 24, y + height - 48), 22, OrbitSkin.MutedTextColor);
  }

  private void ShipyardStat(string label, string value, string unit, Vector2 position)
  {
    const float size = 26;
    ShipyardLabel(label, position, size, OrbitSkin.ButtonTextColor);
    position.X += Measure2(label, Vector2.Zero, size).X + 8;
    ShipyardLabel(value, position, size, OrbitSkin.StatValueColor);
    if (unit.Length > 0)
    {
      position.X += Measure2(value, Vector2.Zero, size).X + 8;
      ShipyardLabel(unit, position, size, OrbitSkin.MutedTextColor);
    }
  }

  private void DrawHarvesterProfile()
  {
    var defaults = ShipyardBaseUpgrades;
    var (speed, capacity, strategy) = ModuleCatalog.Types[selectedShipyardTab] switch
    {
      Harvester.HarvesterType.AdvancedHarvester => (BaseStats.AdvancedHarvesterSpeed, defaults.AdvancedHarvesterCapacity,
        Loc.T("Flies toward random available gems, collecting along the way.")),
      Harvester.HarvesterType.ExpertHarvester => (BaseStats.ExpertHarvesterSpeed, defaults.ExpertHarvesterCapacity,
        Loc.T("Seeks gem clusters, with denser clusters more likely to be chosen.")),
      Harvester.HarvesterType.UltimateHarvester => (BaseStats.UltimateHarvesterSpeed, defaults.UltimateHarvesterCapacity,
        Loc.T("Seeks dense nearby clusters, balancing gem count against travel distance.")),
      Harvester.HarvesterType.PerimeterHarvester => (BaseStats.PerimeterHarvesterSpeed, defaults.PerimeterHarvesterCapacity,
        Loc.T("Patrols the edges of the play area, collecting gems along its route.")),
      _ => (BaseStats.HarvesterSpeed, defaults.HarvesterCapacity,
        Loc.T("Flies toward random locations, collecting gems along the way."))
    };
    // Fuel Tanks upgrade the whole fleet, so every class starts with the same tank.
    float fuel = defaults.FleetMaxFuel;
    float x = ShipyardPanel.X + 64;
    float y = ShipyardPanel.Y + 600;
    ShipyardLabel(Loc.T("Base stats"), new Vector2(x, y), 28, OrbitSkin.StatHeadingColor);
    ShipyardLabel(Loc.T("Before upgrades and modules"), new Vector2(x, y + 48), 22, OrbitSkin.MutedTextColor);
    // Label, value and unit draw in separate colors, so they stay separate keys.
    ShipyardStat(Loc.T("Speed:"), $"{speed:0}", Loc.T("units/s"), new Vector2(x, y + 100));
    ShipyardStat(Loc.T("Cargo:"), $"{capacity:0}", Loc.T("gems"), new Vector2(x, y + 146));
    ShipyardStat(Loc.T("Fuel:"), $"{Harvester.BaseMaxFuel * fuel:0}", "", new Vector2(x, y + 192));
    ShipyardLabel(Loc.T("Gem targeting"), new Vector2(x, y + 270), 28, OrbitSkin.StatHeadingColor);
    y += 322;
    float width = ModulesLeft - x - 48;
    string line = "";
    foreach (var (word, join) in Loc.WrapPieces(strategy))
    {
      string next = line.Length == 0 ? word : line + join + word;
      if (line.Length > 0 && Measure2(next, Vector2.Zero, 24).X > width)
      {
        ShipyardLabel(line, new Vector2(x, y), 24, OrbitSkin.MutedTextColor);
        y += 36;
        line = word;
      }
      else line = next;
    }
    if (line.Length > 0) ShipyardLabel(line, new Vector2(x, y), 24, OrbitSkin.MutedTextColor);
  }

  private void DrawShipyard(SpriteBatch batch)
  {
    var panel = ShipyardPanel;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    if (!shipyardDiscoverySelected) OrbitSkin.Panel(batch, panel, header: true);
    batch.End();
    var cursor = GameInput.UiCursor;
    var position = new Vector2(cursor.X, cursor.Y);
    for (int i = 0; i < ShipyardNames.Length; i++)
    {
      var tab = ShipyardTab(i);
      bool selected = !shipyardDiscoverySelected && selectedShipyardTab == i;
      bool unlocked = UpgradeManager.Instance.IsHarvesterDiscovered(i);
      DrawHudButton(batch, tab, "", OrbitSkin.ShipyardAccent, selected, tab.Contains(position), 0, tab: true);
      if (unlocked) DrawShipyardShip(batch, i, new Rectangle(tab.X + 16, tab.Y + 8, 100, tab.Height - 16));
      else ShipyardLabel("?", new Vector2(tab.X + 52, tab.Center.Y - 24), 40, OrbitSkin.MutedTextColor);
      string tabName = Loc.T(ShipyardNames[i]);
      ShipyardLabel(tabName, new Vector2(tab.X + 130, tab.Center.Y - Measure2(tabName, Vector2.Zero, 32).Y / 2), 32,
        !unlocked ? OrbitSkin.MutedTextColor : selected ? OrbitSkin.ShipyardAccent : OrbitSkin.ButtonTextColor);
      if (!unlocked) ShipyardLabel(Loc.T("LOCKED"), new Vector2(tab.X + 130, tab.Center.Y + 32), 20, OrbitSkin.LockedTextColor);
    }
    bool pending = ModuleInventory.PendingReveals.Count > 0;
    DrawHudButton(batch, DiscoveryTab, pending ? Loc.T("Discovery") + " !" : Loc.T("Discovery"), OrbitSkin.ShipyardAccent,
      shipyardDiscoverySelected || pending, DiscoveryTab.Contains(position), 0, tab: true);
    if (shipyardDiscoverySelected)
    {
      if (revealingModule != ShipModule.None) DrawModuleReveal(batch);
      else DrawShipyardDiscovery(batch);
      return;
    }
    string shipName = Loc.T(ShipyardNames[selectedShipyardTab]);
    ShipyardLabel(shipName, new Vector2(panel.X + 64,
      panel.Y + (OrbitSkin.PanelHeaderHeight - Measure2(shipName, Vector2.Zero, 44).Y) / 2), 44, OrbitSkin.ShipyardAccent);
    if (!UpgradeManager.Instance.IsHarvesterDiscovered(selectedShipyardTab))
    {
      RevealLabel(Loc.T("HARVESTER LOCKED"), panel.Y + 260, 40, OrbitSkin.LockedTextColor);
      RevealLabel(Loc.F("Unlock {0} in the upgrade tree to access its modules.", shipName), panel.Y + 340, 30, OrbitSkin.ButtonTextColor);
      RevealLabel(Loc.T("Once discovered, this ship remains available here after prestige."), panel.Y + 404, 26, OrbitSkin.MutedTextColor);
      return;
    }
    ShipyardLabel(Loc.T("Modules apply to every ship of this type"), new Vector2(panel.X + 64, panel.Y + 112), 26, OrbitSkin.MutedTextColor);
    DrawModulePanel(batch, new Rectangle(panel.X + 40, panel.Y + 210, 440, 850), OrbitSkin.ShipyardAccent);
    DrawShipyardShip(batch, selectedShipyardTab, new Rectangle(panel.X + 90, panel.Y + 230, 340, 340));
    DrawHarvesterProfile();
    ShipyardLabel(Loc.T("Changes apply on the next trip."), new Vector2(panel.X + 64, panel.Y + 158), 26, OrbitSkin.MutedTextColor);

    ShipModule hovered = ShipModule.None;
    bool hoveredEquipped = false;
    for (int slot = 0; slot < ModuleCatalog.MaxSlotsPerType; slot++)
    {
      var bounds = ModuleSlot(slot);
      if (slot >= ModuleCatalog.UnlockedSlots)
      {
        DrawModulePanel(batch, bounds, OrbitSkin.BorderColor * .65f, subdued: true);
        ShipyardLabel(Loc.F("SLOT {0} / LOCKED", slot + 1), new Vector2(bounds.X + 32, bounds.Y + 40), 24, OrbitSkin.LockedTextColor);
        ShipyardLabel(Loc.F("Module Bays rank {0}", slot - ModuleCatalog.BaseSlotsPerType + 1),
          new Vector2(bounds.X + 32, bounds.Y + 92), 30, OrbitSkin.MutedTextColor);
        ShipyardLabel(Loc.T("Unlock in the meta upgrade tree"), new Vector2(bounds.X + 32, bounds.Y + 148), 24, OrbitSkin.MutedTextColor);
        continue;
      }
      int slotIndex = selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + slot;
      var module = ModuleInventory.Slots[slotIndex];
      bool over = bounds.Contains(position);
      DrawHudButton(batch, bounds, "", OrbitSkin.ShipyardAccent, selectedModuleSlot == slot || moduleDragging, over, 0);
      var iconBounds = new Rectangle(bounds.X + 20, bounds.Y + 20, 180, 180);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(bounds.X + 12, bounds.Y + 6, bounds.Width - 24, 5),
        moduleDragging && over ? OrbitSkin.ConfirmAccent : ModuleColor(module));
      batch.End();
      if (module == ShipModule.None)
        ShipyardLabel("+", new Vector2(iconBounds.Center.X - 16, iconBounds.Center.Y - 28), 48, OrbitSkin.MutedTextColor);
      else
        QueueModuleIcon(module, new Rectangle(iconBounds.X + 24, iconBounds.Y + 24, 132, 132),
          moduleDragging && draggedModuleSource == slotIndex ? 0.25f : 1f);
      ShipyardLabel(module == ShipModule.None ? Loc.F("SLOT {0}", slot + 1) : Loc.F("SLOT {0} / {1}", slot + 1, ModuleRarityLabel(module)),
        new Vector2(bounds.X + 224, bounds.Y + 28), 22, ModuleColor(module));
      ShipyardLabel(module == ShipModule.None ? Loc.T("Drop module here") : Loc.T(ModuleCatalog.Names[(int)module]),
        new Vector2(bounds.X + 224, bounds.Y + 70), 30, OrbitSkin.StatHeadingColor);
      if (module != ShipModule.None)
      {
        DrawHudButton(batch, RemoveModuleButton(slot), Loc.T("Remove"), OrbitSkin.ShipyardAccent,
          false, RemoveModuleButton(slot).Contains(position), 0);
        if (over) { hovered = module; hoveredEquipped = true; }
      }
    }
    ShipyardLabel(ModuleInventory.CollectionComplete && ModuleInventory.PendingReveals.Count == 0
      ? Loc.T("Module collection complete") : Loc.F("Module collection ({0}/{1})", ModuleInventory.RevealedCount, ModuleCatalog.Names.Length - 1), new Vector2(ModulesLeft, panel.Y + 720), 32, OrbitSkin.ButtonTextColor);
    ShipyardLabel(Loc.T("Hover for details. Drag modules into slots or back into the inventory."),
      new Vector2(ModulesLeft, ModuleInventoryBounds.Bottom + 20), 24, OrbitSkin.MutedTextColor);
    DrawModulePanel(batch, ModuleInventoryBounds,
      moduleDragging && draggedModuleSource >= 0 && ModuleInventoryBounds.Contains(position)
        ? OrbitSkin.ConfirmAccent : OrbitSkin.ButtonBorderColor);
    var available = ModuleInventory.GetAvailableModules().ToArray();
    for (int index = 0; index < available.Length; index++)
    {
      var bounds = ModuleTile(index);
      var module = available[index];
      bool over = bounds.Contains(position);
      DrawModulePanel(batch, bounds, over ? Color.White : ModuleColor(module));
      int iconSize = Math.Max(24, Math.Min(112, Math.Min(bounds.Width, bounds.Height) - 24));
      QueueModuleIcon(module, new Rectangle(bounds.Center.X - iconSize / 2, bounds.Center.Y - iconSize / 2, iconSize, iconSize),
        moduleDragging && draggedModuleSource < 0 && draggedModule == module ? 0.25f : 1f);
      if (over) hovered = module;
    }
    if (available.Length == 0)
    {
      ShipyardLabel(ModuleInventory.RevealedCount == 0 ? Loc.T("No modules discovered yet") : Loc.T("All discovered modules are equipped"), new Vector2(ModuleGridBounds.X + 16, ModuleGridBounds.Y + 24), 30, OrbitSkin.ButtonTextColor);
      ShipyardLabel(Loc.T("Recover more modules by harvesting, or return an equipped module here."),
        new Vector2(ModuleGridBounds.X + 16, ModuleGridBounds.Y + 76), 24, OrbitSkin.MutedTextColor);
    }
    FlushIcons(batch, SamplerState.PointClamp);
    if (moduleDragging)
    {
      var ghost = new Rectangle((int)position.X + 16, (int)position.Y + 16, 112, 112);
      DrawModulePanel(batch, ghost, ModuleColor(draggedModule));
      QueueModuleIcon(draggedModule, new Rectangle(ghost.X + 12, ghost.Y + 12, 88, 88));
      FlushIcons(batch, SamplerState.PointClamp);
    }
    else if (hovered != ShipModule.None) DrawModuleTooltip(batch, hovered, position, hoveredEquipped);
  }
}
