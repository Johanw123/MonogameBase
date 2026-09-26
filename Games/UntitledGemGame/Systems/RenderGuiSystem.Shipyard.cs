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
  private int moduleScrollRow;
  private ShipModule draggedModule;
  private int draggedModuleSource = -1;
  private Vector2 modulePressPosition;
  private bool moduleDragging;
  private int moduleHoverTab = -1;
  private float moduleHoverTabTime;
  private bool moduleScrollDragging;
  private int moduleScrollGrabOffset;
  private readonly AsyncAsset<Texture2D>[] moduleIcons = new AsyncAsset<Texture2D>[ModuleCatalog.Names.Length];

  private const int ModuleTileSize = 180;
  private const int ModuleTileSpacing = 20;
  private static ShipyardModules ModuleInventory => UpgradeManager.Instance.Modules;
  private static readonly UpgradesGeneratorUpgrades ShipyardBaseUpgrades = new();
  private static Rectangle ShipyardPanel => new(500, 196, HudLayout.Width - 564, HudLayout.Top - 240);
  private static int ModulesLeft => ShipyardPanel.X + 530;
  private static int ModulesWidth => ShipyardPanel.Right - ModulesLeft - 48;
  private static Rectangle ModuleSlot(int slot) => new(ModulesLeft + slot % 2 * (ModulesWidth / 2 + 8),
    ShipyardPanel.Y + 210 + slot / 2 * 240, ModulesWidth / 2 - 8, 220);
  private static Rectangle RemoveModuleButton(int slot)
  {
    var bounds = ModuleSlot(slot);
    return new Rectangle(bounds.X + 224, bounds.Bottom - 78, 140, 54);
  }
  private static Rectangle ModuleInventoryBounds => new(ModulesLeft, ShipyardPanel.Y + 780,
    ModulesWidth, ShipyardPanel.Height - 860);
  private static Rectangle ModuleGridBounds
  {
    get
    {
      var bounds = ModuleInventoryBounds;
      return new Rectangle(bounds.X + 24, bounds.Y + 24, bounds.Width - 108, bounds.Height - 48);
    }
  }
  private static int ModuleColumns => Math.Max(1, (ModuleGridBounds.Width + ModuleTileSpacing) / (ModuleTileSize + ModuleTileSpacing));
  private static int ModuleVisibleRows => Math.Max(1, (ModuleGridBounds.Height + ModuleTileSpacing) / (ModuleTileSize + ModuleTileSpacing));
  private static int ModuleMaxScroll(int count) => Math.Max(0, (count + ModuleColumns - 1) / ModuleColumns - ModuleVisibleRows);
  private Rectangle ModuleTile(int index) => new(ModuleGridBounds.X + index % ModuleColumns * (ModuleTileSize + ModuleTileSpacing),
    ModuleGridBounds.Y + (index / ModuleColumns - moduleScrollRow) * (ModuleTileSize + ModuleTileSpacing), ModuleTileSize, ModuleTileSize);
  private static Rectangle ModuleScrollUp => new(ModuleInventoryBounds.Right - 68, ModuleInventoryBounds.Y + 24, 48, 48);
  private static Rectangle ModuleScrollDown => new(ModuleInventoryBounds.Right - 68, ModuleInventoryBounds.Bottom - 72, 48, 48);
  private static Rectangle ModuleScrollTrack => new(ModuleScrollUp.X + 12, ModuleScrollUp.Bottom + 12,
    24, ModuleScrollDown.Y - ModuleScrollUp.Bottom - 24);
  private Rectangle ModuleScrollThumb(int count)
  {
    var track = ModuleScrollTrack;
    int rows = Math.Max(ModuleVisibleRows, (count + ModuleColumns - 1) / ModuleColumns);
    int height = Math.Max(40, track.Height * ModuleVisibleRows / rows);
    int travel = track.Height - height;
    return new Rectangle(track.X, track.Y + travel * moduleScrollRow / Math.Max(1, ModuleMaxScroll(count)), track.Width, height);
  }
  private static readonly string[] ShipyardNames =
    ["Drifter", "Seeker", "Prospector", "Trove Hunter", "Rimrunner"];

  private static AsyncAsset<Texture2D> ShipyardTexture(int index) => index switch
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
    return new Rectangle(64, 196 + index * height, 400, height - 12);
  }

  private void CancelModuleDrag()
  {
    draggedModule = ShipModule.None;
    draggedModuleSource = -1;
    moduleDragging = false;
    moduleHoverTab = -1;
    moduleHoverTabTime = 0;
    moduleScrollDragging = false;
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
    moduleScrollRow = Math.Clamp(moduleScrollRow, 0, ModuleMaxScroll(ModuleInventory.GetAvailableModules().Count()));
    UntitledGemGame.Screens.UntitledGemGameGameScreen.Instance.SaveProgress();
  }

  private void UpdateShipyardInput(float dt)
  {
    if (!UpgradeManager.Instance.UGM.ShipyardUnlocked) { CancelModuleDrag(); return; }
    var mouse = MouseExtended.GetState();
    var cursor = GumService.Default.Cursor;
    var position = new Vector2(cursor.X, cursor.Y);
    bool pressed = mouse.WasButtonPressed(MouseButton.Left);
    bool released = mouse.LeftButton == ButtonState.Released;
    if (pressed && HudLayout.NavigationButton(2).Contains(position))
    {
      SetUpgradeType(m_upgradeWindowType == UpgradeTypes.Shipyard ? UpgradeTypes.None : UpgradeTypes.Shipyard);
      return;
    }
    if (m_upgradeWindowType != UpgradeTypes.Shipyard) { CancelModuleDrag(); return; }
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

    var available = ModuleInventory.GetAvailableModules().ToArray();
    int maxScroll = ModuleMaxScroll(available.Length);
    moduleScrollRow = Math.Clamp(moduleScrollRow, 0, maxScroll);
    if (ModuleInventoryBounds.Contains(position) && mouse.DeltaScrollWheelValue != 0)
      moduleScrollRow = Math.Clamp(moduleScrollRow - Math.Sign(mouse.DeltaScrollWheelValue), 0, maxScroll);

    if (moduleScrollDragging)
    {
      if (released) moduleScrollDragging = false;
      else
      {
        int travel = ModuleScrollTrack.Height - ModuleScrollThumb(available.Length).Height;
        float offset = position.Y - moduleScrollGrabOffset - ModuleScrollTrack.Y;
        moduleScrollRow = Math.Clamp((int)MathF.Round(offset / Math.Max(1, travel) * maxScroll), 0, maxScroll);
      }
      return;
    }

    if (draggedModule != ShipModule.None)
    {
      moduleDragging |= Vector2.DistanceSquared(position, modulePressPosition) >= 100f;
      // Hovering a ship tab while dragging lets equipment move between ship types.
      if (moduleDragging && !released)
      {
        int tab = -1;
        for (int i = 0; i < ShipyardNames.Length; i++)
          if (ShipyardTab(i).Contains(position)) tab = i;
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

    if (pressed && maxScroll > 0)
    {
      if (ModuleScrollUp.Contains(position)) { moduleScrollRow = Math.Max(0, moduleScrollRow - 1); return; }
      if (ModuleScrollDown.Contains(position)) { moduleScrollRow = Math.Min(maxScroll, moduleScrollRow + 1); return; }
      if (ModuleScrollTrack.Contains(position))
      {
        var thumb = ModuleScrollThumb(available.Length);
        if (thumb.Contains(position))
        {
          moduleScrollDragging = true;
          moduleScrollGrabOffset = (int)position.Y - thumb.Y;
        }
        else moduleScrollRow = Math.Clamp(moduleScrollRow + (position.Y < thumb.Y ? -ModuleVisibleRows : ModuleVisibleRows), 0, maxScroll);
        return;
      }
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
    int first = moduleScrollRow * ModuleColumns;
    int last = Math.Min(available.Length, first + ModuleVisibleRows * ModuleColumns);
    for (int index = first; index < last; index++)
      if (ModuleTile(index).Contains(position)) { StartModuleDrag(available[index], -1, position); return; }
  }

  private void DrawShipyardNavigation(SpriteBatch batch)
  {
    if (!UpgradeManager.Instance.UGM.ShipyardUnlocked) return;
    var bounds = HudLayout.NavigationButton(2);
    bool selected = m_upgradeWindowType == UpgradeTypes.Shipyard;
    bool pending = ModuleInventory.PendingReveals.Count > 0;
    DrawHudButton(batch, bounds, pending ? "Shipyard !" : selected ? "Hide" : "Shipyard", HudLayout.UpgradeAccent,
      selected || pending, bounds.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y), 0);
  }

  private static void ShipyardLabel(string text, Vector2 position, float size, Color color)
    => FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
      text, position, color, Color.Black, size);

  private static Color ModuleColor(ShipModule module) => module == ShipModule.None
    ? HudLayout.ButtonBorderColor : ModuleRarityColor(ModuleCatalog.Rarities[(int)module]);

  private static Color ModuleRarityColor(ModuleRarity rarity) => rarity switch
    {
      ModuleRarity.Common => new Color(180, 194, 208),
      ModuleRarity.Uncommon => new Color(100, 220, 140),
      ModuleRarity.Rare => new Color(90, 170, 255),
      ModuleRarity.Epic => new Color(200, 125, 255),
      ModuleRarity.Legendary => new Color(255, 195, 70),
      _ => HudLayout.ButtonBorderColor
    };

  private static string ModuleRarityLabel(ShipModule module)
    => ModuleCatalog.Rarities[(int)module].ToString().ToUpperInvariant();

  private static void DrawShipyardShip(SpriteBatch batch, int index, Rectangle bounds)
  {
    var asset = ShipyardTexture(index);
    if (!asset.IsLoaded) return;
    var texture = asset.Value;
    float scale = Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
    batch.Begin(samplerState: SamplerState.PointClamp);
    batch.Draw(texture, new Vector2(bounds.Center.X, bounds.Center.Y), null, Color.White,
      0f, new Vector2(texture.Width, texture.Height) / 2, scale, SpriteEffects.None, 0f);
    batch.End();
  }

  private static void DrawModulePanel(SpriteBatch batch, Rectangle bounds, Color border)
  {
    int inset = HudLayout.ButtonBorderThickness;
    batch.Begin();
    batch.Draw(AssetManager.DefaultTexture, bounds, border);
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(bounds.X + inset, bounds.Y + inset,
      bounds.Width - inset * 2, bounds.Height - inset * 2), HudLayout.PanelColor);
    batch.End();
  }

  private void DrawModuleIcon(SpriteBatch batch, ShipModule module, Rectangle bounds, float opacity = 1f)
  {
    if (module == ShipModule.None) return;
    var asset = moduleIcons[(int)module] ??= AssetManager.LoadAsync<Texture2D>(ModuleCatalog.Icons[(int)module]);
    if (!asset.IsLoaded) return;
    var texture = asset.Value;
    float scale = Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
    batch.Begin(samplerState: SamplerState.PointClamp);
    batch.Draw(texture, new Vector2(bounds.Center.X, bounds.Center.Y), null, Color.White * opacity,
      0f, new Vector2(texture.Width, texture.Height) / 2, scale, SpriteEffects.None, 0f);
    batch.End();
  }

  private void DrawModuleTooltip(SpriteBatch batch, ShipModule module, Vector2 cursor, bool equipped)
  {
    const int width = 780;
    var lines = new List<string>();
    string line = "";
    foreach (string word in ModuleCatalog.Descriptions[(int)module].Split(' '))
    {
      string next = line.Length == 0 ? word : line + " " + word;
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
    DrawModulePanel(batch, new Rectangle(x, y, width, height), ModuleColor(module));
    ShipyardLabel(ModuleCatalog.Names[(int)module], new Vector2(x + 24, y + 20), 32, ModuleColor(module));
    ShipyardLabel(ModuleRarityLabel(module), new Vector2(x + 24, y + 66), 22, ModuleColor(module));
    for (int i = 0; i < lines.Count; i++)
      ShipyardLabel(lines[i], new Vector2(x + 24, y + 108 + i * 36), 26, HudLayout.ButtonTextColor);
    ShipyardLabel(equipped ? "Drag to a slot or back to inventory. Right-click to remove."
      : "Drag into a slot, or click to equip in the selected slot.",
      new Vector2(x + 24, y + height - 48), 22, HudLayout.MutedTextColor);
  }

  private void DrawHarvesterProfile()
  {
    var defaults = ShipyardBaseUpgrades;
    var (speed, capacity, fuel, strategy) = ModuleCatalog.Types[selectedShipyardTab] switch
    {
      Harvester.HarvesterType.AdvancedHarvester => (BaseStats.AdvancedHarvesterSpeed, defaults.AdvancedHarvesterCapacity,
        defaults.AdvancedHarvesterMaxFuel, "Flies toward random available gems, collecting along the way."),
      Harvester.HarvesterType.ExpertHarvester => (BaseStats.ExpertHarvesterSpeed, defaults.ExpertHarvesterCapacity,
        defaults.ExpertHarvesterMaxFuel, "Seeks gem clusters, with denser clusters more likely to be chosen."),
      Harvester.HarvesterType.UltimateHarvester => (BaseStats.UltimateHarvesterSpeed, defaults.UltimateHarvesterCapacity,
        defaults.UltimateHarvesterMaxFuel, "Seeks dense nearby clusters, balancing gem count against travel distance."),
      Harvester.HarvesterType.PerimeterHarvester => (BaseStats.PerimeterHarvesterSpeed, defaults.PerimeterHarvesterCapacity,
        defaults.PerimeterHarvesterMaxFuel, "Patrols the edges of the play area, collecting gems along its route."),
      _ => (BaseStats.HarvesterSpeed, defaults.HarvesterCapacity, defaults.HarvesterMaxFuel,
        "Flies toward random locations, collecting gems along the way.")
    };
    float x = ShipyardPanel.X + 64;
    float y = ShipyardPanel.Y + 640;
    ShipyardLabel("Base stats", new Vector2(x, y), 28, HudLayout.UpgradeAccent);
    ShipyardLabel("Before upgrades and modules", new Vector2(x, y + 48), 22, HudLayout.MutedTextColor);
    ShipyardLabel($"Speed: {speed:0} units/s", new Vector2(x, y + 100), 26, HudLayout.ButtonTextColor);
    ShipyardLabel($"Cargo: {capacity:0} gems", new Vector2(x, y + 146), 26, HudLayout.ButtonTextColor);
    ShipyardLabel($"Fuel: {Harvester.BaseMaxFuel * fuel:0}", new Vector2(x, y + 192), 26, HudLayout.ButtonTextColor);
    ShipyardLabel("Gem targeting", new Vector2(x, y + 270), 28, HudLayout.UpgradeAccent);
    y += 322;
    float width = ModulesLeft - x - 48;
    string line = "";
    foreach (string word in strategy.Split(' '))
    {
      string next = line.Length == 0 ? word : line + " " + word;
      if (line.Length > 0 && Measure2(next, Vector2.Zero, 24).X > width)
      {
        ShipyardLabel(line, new Vector2(x, y), 24, HudLayout.MutedTextColor);
        y += 36;
        line = word;
      }
      else line = next;
    }
    if (line.Length > 0) ShipyardLabel(line, new Vector2(x, y), 24, HudLayout.MutedTextColor);
  }

  private void DrawShipyard(SpriteBatch batch)
  {
    var panel = ShipyardPanel;
    DrawModulePanel(batch, panel, HudLayout.BorderColor);
    var cursor = GumService.Default.Cursor;
    var position = new Vector2(cursor.X, cursor.Y);
    for (int i = 0; i < ShipyardNames.Length; i++)
    {
      var tab = ShipyardTab(i);
      bool selected = !shipyardDiscoverySelected && selectedShipyardTab == i;
      DrawHudButton(batch, tab, "", HudLayout.UpgradeAccent, selected, tab.Contains(position), 0);
      DrawShipyardShip(batch, i, new Rectangle(tab.X + 16, tab.Y + 8, 100, tab.Height - 16));
      ShipyardLabel(ShipyardNames[i], new Vector2(tab.X + 130, tab.Center.Y - 15), 24,
        selected ? HudLayout.UpgradeAccent : HudLayout.ButtonTextColor);
    }
    bool pending = ModuleInventory.PendingReveals.Count > 0;
    DrawHudButton(batch, DiscoveryTab, pending ? "Discovery !" : "Discovery", HudLayout.UpgradeAccent,
      shipyardDiscoverySelected || pending, DiscoveryTab.Contains(position), 0);
    if (shipyardDiscoverySelected)
    {
      if (revealingModule != ShipModule.None) DrawModuleReveal(batch);
      else DrawShipyardDiscovery(batch);
      return;
    }
    ShipyardLabel(ShipyardNames[selectedShipyardTab], new Vector2(panel.X + 64, panel.Y + 48), 44, HudLayout.UpgradeAccent);
    ShipyardLabel("Modules apply to every ship of this type", new Vector2(panel.X + 64, panel.Y + 112), 26, HudLayout.MutedTextColor);
    DrawShipyardShip(batch, selectedShipyardTab, new Rectangle(panel.X + 64, panel.Y + 210, 360, 360));
    DrawHarvesterProfile();
    ShipyardLabel("Changes apply on the next trip.", new Vector2(ModulesLeft, panel.Y + 158), 24, HudLayout.MutedTextColor);

    ShipModule hovered = ShipModule.None;
    bool hoveredEquipped = false;
    for (int slot = 0; slot < ModuleCatalog.MaxSlotsPerType; slot++)
    {
      var bounds = ModuleSlot(slot);
      if (slot >= ModuleCatalog.UnlockedSlots)
      {
        DrawModulePanel(batch, bounds, HudLayout.ButtonBorderColor);
        ShipyardLabel($"SLOT {slot + 1} / LOCKED", new Vector2(bounds.X + 32, bounds.Y + 40), 24, HudLayout.MutedTextColor);
        ShipyardLabel($"Module Bays rank {slot - ModuleCatalog.BaseSlotsPerType + 1}",
          new Vector2(bounds.X + 32, bounds.Y + 92), 30, HudLayout.MutedTextColor);
        ShipyardLabel("Unlock in the meta upgrade tree", new Vector2(bounds.X + 32, bounds.Y + 148), 24, HudLayout.MutedTextColor);
        continue;
      }
      int slotIndex = selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + slot;
      var module = ModuleInventory.Slots[slotIndex];
      bool over = bounds.Contains(position);
      DrawHudButton(batch, bounds, "", HudLayout.UpgradeAccent, selectedModuleSlot == slot || moduleDragging, over, 0);
      var iconBounds = new Rectangle(bounds.X + 20, bounds.Y + 20, 180, 180);
      DrawModulePanel(batch, iconBounds, moduleDragging && over ? Color.LightGreen : ModuleColor(module));
      if (module == ShipModule.None)
        ShipyardLabel("+", new Vector2(iconBounds.Center.X - 16, iconBounds.Center.Y - 28), 48, HudLayout.MutedTextColor);
      else
        DrawModuleIcon(batch, module, new Rectangle(iconBounds.X + 24, iconBounds.Y + 24, 132, 132),
          moduleDragging && draggedModuleSource == slotIndex ? 0.25f : 1f);
      ShipyardLabel(module == ShipModule.None ? $"SLOT {slot + 1}" : $"SLOT {slot + 1} / {ModuleRarityLabel(module)}",
        new Vector2(bounds.X + 224, bounds.Y + 28), 22, ModuleColor(module));
      ShipyardLabel(module == ShipModule.None ? "Drop module here" : ModuleCatalog.Names[(int)module],
        new Vector2(bounds.X + 224, bounds.Y + 70), 30, module == ShipModule.None ? HudLayout.ButtonTextColor : ModuleColor(module));
      if (module != ShipModule.None)
      {
        DrawHudButton(batch, RemoveModuleButton(slot), "Remove", HudLayout.UpgradeAccent,
          false, RemoveModuleButton(slot).Contains(position), 0);
        if (over) { hovered = module; hoveredEquipped = true; }
      }
    }
    ShipyardLabel(ModuleInventory.CollectionComplete && ModuleInventory.PendingReveals.Count == 0
      ? "Module collection complete" : $"Module collection ({ModuleInventory.RevealedCount}/{ModuleCatalog.Names.Length - 1})", new Vector2(ModulesLeft, panel.Y + 720), 32, HudLayout.ButtonTextColor);
    ShipyardLabel("Hover for details. Drag modules into slots or back into the inventory.",
      new Vector2(ModulesLeft, panel.Bottom - 54), 24, HudLayout.MutedTextColor);
    DrawModulePanel(batch, ModuleInventoryBounds,
      moduleDragging && draggedModuleSource >= 0 && ModuleInventoryBounds.Contains(position)
        ? Color.LightGreen : HudLayout.ButtonBorderColor);
    var available = ModuleInventory.GetAvailableModules().ToArray();
    moduleScrollRow = Math.Clamp(moduleScrollRow, 0, ModuleMaxScroll(available.Length));
    int first = moduleScrollRow * ModuleColumns;
    int last = Math.Min(available.Length, first + ModuleVisibleRows * ModuleColumns);
    for (int index = first; index < last; index++)
    {
      var bounds = ModuleTile(index);
      var module = available[index];
      bool over = bounds.Contains(position);
      DrawModulePanel(batch, bounds, over ? Color.White : ModuleColor(module));
      DrawModuleIcon(batch, module, new Rectangle(bounds.X + 34, bounds.Y + 16, 112, 112),
        moduleDragging && draggedModuleSource < 0 && draggedModule == module ? 0.25f : 1f);
      string rarity = ModuleRarityLabel(module);
      ShipyardLabel(rarity, new Vector2(bounds.Center.X - Measure2(rarity, Vector2.Zero, 20).X / 2,
        bounds.Bottom - 36), 20, ModuleColor(module));
      if (over) hovered = module;
    }
    if (available.Length == 0)
    {
      ShipyardLabel(ModuleInventory.RevealedCount == 0 ? "No modules discovered yet" : "All discovered modules are equipped", new Vector2(ModuleGridBounds.X + 16, ModuleGridBounds.Y + 24), 30, HudLayout.ButtonTextColor);
      ShipyardLabel("Recover more modules by harvesting, or return an equipped module here.",
        new Vector2(ModuleGridBounds.X + 16, ModuleGridBounds.Y + 76), 24, HudLayout.MutedTextColor);
    }
    if (ModuleMaxScroll(available.Length) > 0)
    {
      DrawHudButton(batch, ModuleScrollUp, "^", HudLayout.UpgradeAccent, false, ModuleScrollUp.Contains(position), 0);
      DrawHudButton(batch, ModuleScrollDown, "v", HudLayout.UpgradeAccent, false, ModuleScrollDown.Contains(position), 0);
      batch.Begin();
      batch.Draw(AssetManager.DefaultTexture, ModuleScrollTrack, HudLayout.ButtonBorderColor);
      batch.Draw(AssetManager.DefaultTexture, ModuleScrollThumb(available.Length), HudLayout.UpgradeAccent);
      batch.End();
    }
    if (moduleDragging)
    {
      var ghost = new Rectangle((int)position.X + 16, (int)position.Y + 16, 112, 112);
      DrawModulePanel(batch, ghost, ModuleColor(draggedModule));
      DrawModuleIcon(batch, draggedModule, new Rectangle(ghost.X + 12, ghost.Y + 12, 88, 88));
    }
    else if (hovered != ShipModule.None) DrawModuleTooltip(batch, hovered, position, hoveredEquipped);
  }
}
