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

  public void OpenShipyard(int tab = -1)
  {
    if (tab >= 0 && tab < ShipyardNames.Length)
    {
      selectedShipyardTab = tab;
      selectedModuleSlot = 0;
    }
    shipyardDiscoverySelected = false;
    SetUpgradeType(UpgradeTypes.Shipyard);
  }

  private int selectedModuleSlot;
  private ShipModule draggedModule;
  private int draggedModuleSource = -1;
  private Vector2 modulePressPosition;
  private bool moduleDragging;
  private int moduleHoverTab = -1;
  private float moduleHoverTabTime;

  private static ShipyardModules ModuleInventory => UpgradeManager.Instance.Modules;
  private static readonly UpgradesGeneratorUpgrades ShipyardBaseUpgrades = new();

  // Outer bounds of the Shipyard screen (used by ModuleSalvage modal positioning and hit-testing).
  private static Rectangle ShipyardPanel => new(84, 184, HudLayout.Width - 168, HudLayout.ContentBottom - 208);

  private static readonly string[] ShipyardNames =
    [Loc.N("Drifter"), Loc.N("Seeker"), Loc.N("Prospector"), Loc.N("Trove Hunter"), Loc.N("Rimrunner")];

  private static readonly string[] ShipyardRoles =
    ["Class-I Patrol Scout", "Class-II Gem Seeker", "Class-III Cluster Miner", "Class-IV Apex Excavator", "Perimeter Interceptor"];

  // Visual positions for each ship type; equipment indices stay tied to their type.
  private static readonly int[] ShipyardTabPositions = [0, 1, 3, 4, 2];

  // Top-left row of 5 hexagonal ship medallions above the 3D ship showcase.
  private static Rectangle ShipyardTab(int index)
  {
    int pos = ShipyardTabPositions[index];
    return new Rectangle(240 + pos * 168, 224, 136, 136);
  }

  // Vertical column of 4 hexagonal module hardpoint slots directly to the right of the 3D ship.
  private static Rectangle ModuleSlot(int slot)
    => new(1440, 524 + slot * 172, 148, 148);

  // Discovery button above the "Available Modules" header on the right side.
  private static Rectangle DiscoveryTab => new(AvailableLeft, 224, 280, 100);

  // Right-hand available modules region and hexagonal grid layout (9x7 fits all 59 modules with room to spare).
  private const int AvailableLeft = 1860;
  private const int AvailableWidth = 1764;
  private const int AvailableGridTop = 464;
  private const int AvailableCols = 9;
  private const int AvailableTileSize = 156;
  private const int AvailableGapX = 45;
  private const int AvailableGapY = 22;

  private static Rectangle ModuleInventoryBounds => new(AvailableLeft - 40, 360, AvailableWidth + 80, 1400);

  private static Rectangle ModuleTile(int index)
  {
    int col = index % AvailableCols;
    int row = index / AvailableCols;
    return new Rectangle(
      AvailableLeft + col * (AvailableTileSize + AvailableGapX),
      AvailableGridTop + row * (AvailableTileSize + AvailableGapY),
      AvailableTileSize,
      AvailableTileSize);
  }

  private static MonoGame.Extended.Graphics.Texture2DRegion ShipyardTexture(int index) => index switch
  {
    0 => TextureCache.HarvesterShip,
    1 => TextureCache.AdvancedHarvesterShip,
    2 => TextureCache.ExpertHarvesterShip,
    3 => TextureCache.UltimateHarvesterShip,
    _ => TextureCache.PerimeterHarvesterShip
  };

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
        bool droppedOnSlot = false;
        for (int slot = 0; slot < ModuleCatalog.UnlockedSlots; slot++)
        {
          if (!ModuleSlot(slot).Contains(position)) continue;
          droppedOnSlot = true;
          changed = draggedModuleSource < 0
            ? ModuleInventory.TryEquip(selectedShipyardTab, slot, draggedModule)
            : ModuleInventory.TryMoveSlot(draggedModuleSource, selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + slot);
          selectedModuleSlot = slot;
          break;
        }
        if (draggedModuleSource >= 0 && !droppedOnSlot)
        {
          changed = ModuleInventory.TryEquip(draggedModuleSource / ModuleCatalog.MaxSlotsPerType,
            draggedModuleSource % ModuleCatalog.MaxSlotsPerType, ShipModule.None);
          if (changed) AudioManager.Instance.PlaySound(SoundEvent.ModuleUnequip);
        }
        else if (changed)
        {
          AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
        }
      }
      else if (validSource && draggedModuleSource < 0)
      {
        // A short click still equips into the selected slot.
        changed = ModuleInventory.TryEquip(selectedShipyardTab, selectedModuleSlot, draggedModule);
        if (changed)
        {
          AudioManager.Instance.PlaySound(SoundEvent.ModuleEquip);
          for (int offset = 1; offset < ModuleCatalog.UnlockedSlots; offset++)
          {
            int next = (selectedModuleSlot + offset) % ModuleCatalog.UnlockedSlots;
            if (ModuleInventory.Slots[selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + next] != ShipModule.None) continue;
            selectedModuleSlot = next;
            break;
          }
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
      if (mouse.WasButtonPressed(MouseButton.Right))
      {
        if (ModuleInventory.TryEquip(selectedShipyardTab, slot, ShipModule.None))
        {
          AudioManager.Instance.PlaySound(SoundEvent.ModuleUnequip);
          SaveModuleLoadout();
        }
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
    DrawHudButton(batch, bounds, selected ? Loc.T("Hide") : Loc.T("Shipyard"), GuiSkin.PanelAccent,
      selected, bounds.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y), m_animateButtonClickShipyard, tab: true, badge: !selected && pending);
  }

  private static void ShipyardLabel(string text, Vector2 position, float size, Color color)
    => FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
      text, position, color, Color.Black, size);

  private void DrawShipyardLabelCentered(string text, Vector2 center, float size, Color color)
  {
    var measured = Measure2(text, Vector2.Zero, size);
    ShipyardLabel(text, new Vector2(center.X - measured.X / 2f, center.Y - measured.Y / 2f), size, color);
  }

  private static Color ModuleColor(ShipModule module) => module == ShipModule.None
    ? GuiSkin.ButtonBorderColor : ModuleRarityColor(ModuleCatalog.Rarities[(int)module]);

  private static Color ModuleRarityColor(ModuleRarity rarity) => rarity switch
  {
    ModuleRarity.Common => GuiSkin.CommonRarity,
    ModuleRarity.Uncommon => GuiSkin.UncommonRarity,
    ModuleRarity.Rare => GuiSkin.RareRarity,
    ModuleRarity.Epic => GuiSkin.EpicRarity,
    ModuleRarity.Legendary => GuiSkin.LegendaryRarity,
    ModuleRarity.Mythic => GuiSkin.MythicRarity,
    _ => GuiSkin.ButtonBorderColor
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
    // The in-game prerendered sprite, nose up; the pixel-art hull when those are off.
    bool prerendered = PrerenderedShips.TryGet(ModuleCatalog.Types[index], 0f, out var region, out _, out _, out _);
    if (!prerendered) region = ShipyardTexture(index);
    if (region == null) return;
    float scale = Math.Min(bounds.Width / (float)region.Width, bounds.Height / (float)region.Height);
    batch.Begin(samplerState: prerendered ? SamplerState.LinearClamp : SamplerState.PointClamp,
      transformMatrix: HudLayout.RenderTransform);
    batch.Draw(region.Texture, new Vector2(bounds.Center.X, bounds.Center.Y), region.Bounds, Color.White,
      0f, new Vector2(region.Width, region.Height) / 2, scale, SpriteEffects.None, 0f);
    batch.End();
  }

  // Showroom turntable in the shipyard preview bay: a slow idle spin, plus a quick
  // whip-around that eases out whenever another ship is picked.
  private const float ShowcaseSpinDegrees = 24f;
  private const float ShowcaseIntroDegrees = 200f;
  private const float ShowcaseIntroRate = 3.5f;
  // Heading the whip-around settles on: three-quarter front view (180 = nose to camera).
  private const float ShowcaseSettleHeading = 145f;
  private int showcaseShip = -1;
  private double showcaseStart;
  private double showcaseLastDrawn = double.NegativeInfinity;

  private RenderTarget2D showcaseTarget;
  private SpriteBatch showcaseBatch;
  private int showcasePreparedShip = -1;

  private static float ShowcaseHeading(float t)
  {
    // Counter-clockwise seen from above, so the near side sweeps left to right.
    float turned = ShowcaseSpinDegrees * t + ShowcaseIntroDegrees * (1f - MathF.Exp(-ShowcaseIntroRate * t));
    float heading = (ShowcaseSettleHeading + ShowcaseIntroDegrees - turned) % 360f;
    return heading < 0 ? heading + 360f : heading;
  }

  /// <summary>
  /// Renders the selected ship's 3D showcase mesh at this frame's exact turntable
  /// heading into a supersampled render target. Called before anything is drawn
  /// into the HUD target because binding another target mid-HUD discards it.
  /// </summary>
  public void PrepareShipShowcase(GraphicsDevice device)
  {
    double now = BaseGame.Time.TotalGameTime.TotalSeconds;
    showcasePreparedShip = -1;
    if (!PrerenderedShips.Enabled || m_upgradeWindowType != UpgradeTypes.Shipyard || shipyardDiscoverySelected) return;
    if (UpgradeManager.Instance?.UGM == null || !UpgradeManager.Instance.UGM.ShipyardUnlocked) return;
    if (!UpgradeManager.Instance.IsHarvesterDiscovered(selectedShipyardTab)) return;
    var bindings = device.GetRenderTargets();
    if (bindings.Length == 0) return;
    var mesh = ShipShowcase.Get(device, ModuleCatalog.Types[selectedShipyardTab]);
    if (mesh == null) return;

    if (showcaseShip != selectedShipyardTab || now - showcaseLastDrawn > 0.25)
    {
      showcaseShip = selectedShipyardTab;
      showcaseStart = now;
    }
    showcaseLastDrawn = now;
    float heading = ShowcaseHeading((float)(now - showcaseStart));
    ShipShowcase.Render(device, mesh, heading, ref showcaseTarget, ref showcaseBatch);
    device.SetRenderTargets(bindings);
    device.Clear(Color.Transparent);
    showcasePreparedShip = showcaseShip;
  }

  /// <summary>Draws the selected ship spinning above a holo pad; false if it has no showcase mesh.</summary>
  private bool DrawShipShowcase(SpriteBatch batch, Rectangle bay, float sizeScale = 1f)
  {
    if (showcasePreparedShip != selectedShipyardTab || showcaseTarget == null || showcaseTarget.IsDisposed)
      return false;

    double now = BaseGame.Time.TotalGameTime.TotalSeconds;
    float t = (float)(now - showcaseStart);
    float heading = ShowcaseHeading(t);

    float ratio = Math.Min(bay.Width / 440f, bay.Height / 368f) * sizeScale;
    float squash = MathF.Sin(MathHelper.ToRadians(ShipShowcase.Elevation));
    var pad = new Vector2(bay.Center.X, bay.Center.Y + 98f * ratio);
    var device = batch.GraphicsDevice;
    var glow = GuiSkin.GetTexture(device, "synty_fx_glow_01");

    // Pad glow and the ship's soft shadow lie flat on the floor.
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(glow, Ellipse(pad, 430f * ratio, 430f * ratio * squash * 1.4f), GuiSkin.PanelAccent * 0.24f);
    batch.Draw(glow, Ellipse(pad, 256f * ratio, 256f * ratio * squash), Color.Black * 0.58f);
    batch.End();

    // Rings turn with the ship, like the platform it stands on.
    var floor = Matrix.CreateRotationZ(MathHelper.ToRadians(heading)) * Matrix.CreateScale(1f, squash, 1f)
      * Matrix.CreateTranslation(pad.X, pad.Y, 0f) * HudLayout.RenderTransform;
    int outerR = (int)MathF.Round(186f * ratio);
    int innerR = (int)MathF.Round(130f * ratio);
    int calR = (int)MathF.Round(218f * ratio);
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: floor);
    batch.Draw(GuiSkin.GetTexture(device, "synty_hud_ring_calibration"), new Rectangle(-calR, -calR, calR * 2, calR * 2),
      GuiSkin.PanelAccent * 0.26f);
    batch.Draw(GuiSkin.GetTexture(device, "synty_hud_ring_large_02"), new Rectangle(-outerR, -outerR, outerR * 2, outerR * 2),
      GuiSkin.PanelAccent * 0.58f);
    batch.Draw(GuiSkin.GetTexture(device, "synty_hud_ring_large_01"), new Rectangle(-innerR, -innerR, innerR * 2, innerR * 2),
      Color.Lerp(GuiSkin.PanelAccent, Color.White, 0.35f) * 0.44f);
    batch.End();

    float scale = (392f * ratio) / showcaseTarget.Width;
    var center = new Vector2(bay.Center.X, bay.Center.Y - 40f * ratio + MathF.Sin(t * 1.7f) * 4f * ratio);
    var origin = new Vector2(showcaseTarget.Width, showcaseTarget.Height) / 2f;
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    batch.Draw(showcaseTarget, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
    batch.End();
    return true;

    static Rectangle Ellipse(Vector2 c, float width, float height)
      => new((int)(c.X - width / 2), (int)(c.Y - height / 2), (int)width, (int)height);
  }

  private void DrawShowcaseOrFallback(SpriteBatch batch, Rectangle bay, float sizeScale = 1f)
  {
    if (!DrawShipShowcase(batch, bay, sizeScale))
    {
      int reticleD = (int)(Math.Min(bay.Width, bay.Height) * 0.85f * sizeScale);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.DrawScannerReticle(batch, bay.Center, reticleD, GuiSkin.PanelAccent, 0.38f);
      batch.End();
      int shipD = (int)(Math.Min(bay.Width, bay.Height) * 0.78f * sizeScale);
      DrawShipyardShip(batch, selectedShipyardTab,
        new Rectangle(bay.Center.X - shipD / 2, bay.Center.Y - shipD / 2, shipD, shipD));
    }
  }

  // Used by ModuleSalvage.cs during module reveal animations.
  private static void DrawModulePanel(SpriteBatch batch, Rectangle bounds, Color border, bool subdued = false)
  {
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    Color fill = subdued ? GuiPalette.Base(new Color(6, 16, 28, 224)) : GuiPalette.Base(new Color(6, 20, 38, 236));
    GuiSkin.TechCard(batch, bounds, fill, border * (subdued ? 0.55f : 0.9f), cornerGraphic: !subdued, hexWatermark: false);
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
    Color modColor = ModuleColor(module);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Tooltip(batch, new Rectangle(x, y, width, height));
    GuiSkin.TechDivider(batch, new Rectangle(x + 24, y + 96, width - 48, 3), modColor * 0.65f);
    batch.End();
    ShipyardLabel(Loc.T(ModuleCatalog.Names[(int)module]), new Vector2(x + 24, y + 20), 32, modColor);
    ShipyardLabel(ModuleRarityLabel(module), new Vector2(x + 24, y + 62), 22, modColor);
    for (int i = 0; i < lines.Count; i++)
      ShipyardLabel(lines[i], new Vector2(x + 24, y + 108 + i * 36), 26, GuiSkin.ButtonTextColor);
    ShipyardLabel(equipped ? Loc.T("Drag to a slot or back to inventory. Right-click to remove.")
      : Loc.T("Drag into a slot, or click to equip in the selected slot."),
      new Vector2(x + 24, y + height - 48), 22, GuiSkin.MutedTextColor);
  }

  private void DrawLockedSlotTooltip(SpriteBatch batch, int slot, Vector2 cursor)
  {
    const int width = 620;
    const int height = 176;
    int x = Math.Clamp((int)cursor.X + 28, 24, HudLayout.Width - width - 24);
    int y = (int)cursor.Y + 32;
    if (y + height > ShipyardPanel.Bottom) y = (int)cursor.Y - height - 24;
    y = Math.Max(ShipyardPanel.Top, y);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Tooltip(batch, new Rectangle(x, y, width, height));
    GuiSkin.TechDivider(batch, new Rectangle(x + 24, y + 72, width - 48, 3), GuiSkin.LockedTextColor * 0.65f);
    batch.End();
    ShipyardLabel(Loc.F("SLOT {0} / LOCKED", slot + 1), new Vector2(x + 24, y + 22), 28, GuiSkin.LockedTextColor);
    ShipyardLabel(Loc.F("Module Bays rank {0}", slot - ModuleCatalog.BaseSlotsPerType + 1),
      new Vector2(x + 24, y + 86), 26, GuiSkin.StatHeadingColor);
    ShipyardLabel(Loc.T("Unlock in the meta upgrade tree"), new Vector2(x + 24, y + 128), 22, GuiSkin.MutedTextColor);
  }

  // Draws the 4 mini hexagonal rarity pips for a ship's currently equipped modules.
  private static void DrawShipEquippedPips(SpriteBatch batch, int shipIndex, Point start, int pipSize, int spacing)
  {
    bool unlocked = UpgradeManager.Instance.IsHarvesterDiscovered(shipIndex);
    for (int s = 0; s < ModuleCatalog.MaxSlotsPerType; s++)
    {
      var rect = new Rectangle(start.X + s * (pipSize + spacing), start.Y, pipSize, pipSize);
      if (!unlocked || s >= ModuleCatalog.UnlockedSlots)
      {
        GuiSkin.DrawHexPip(batch, rect, GuiSkin.BorderColor * 0.35f, false);
        continue;
      }
      var mod = ModuleInventory.Slots[shipIndex * ModuleCatalog.MaxSlotsPerType + s];
      Color pipColor = mod == ShipModule.None ? GuiSkin.PanelAccent * 0.55f : ModuleColor(mod);
      GuiSkin.DrawHexPip(batch, rect, pipColor, mod != ShipModule.None);
    }
  }

  private (float Speed, float Capacity, float Fuel, string Strategy) GetHarvesterTelemetry(int shipTab)
  {
    var defaults = ShipyardBaseUpgrades;
    var (speed, capacity, strategy) = ModuleCatalog.Types[shipTab] switch
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
        Loc.T("Flies toward random locations, collecting gems along its route."))
    };
    float fuel = Harvester.BaseMaxFuel * defaults.FleetMaxFuel;
    return (speed, capacity, fuel, strategy);
  }

  private void DrawWrappedText(string text, Vector2 pos, float width, float fontSize, float lineStep, Color color, int maxLines = 4)
  {
    string line = "";
    int drawn = 0;
    foreach (var (word, join) in Loc.WrapPieces(text))
    {
      string next = line.Length == 0 ? word : line + join + word;
      if (line.Length > 0 && Measure2(next, Vector2.Zero, fontSize).X > width)
      {
        ShipyardLabel(line, pos, fontSize, color);
        pos.Y += lineStep;
        drawn++;
        if (drawn >= maxLines) return;
        line = word;
      }
      else line = next;
    }
    if (line.Length > 0 && drawn < maxLines)
      ShipyardLabel(line, pos, fontSize, color);
  }

  // Draws a clean Elden-Ring style horizontal divider line with an optional leading diamond/hex pip.
  private static void DrawCleanHorizDivider(SpriteBatch batch, int x, int y, int width, Color color, bool leadingPip = false)
  {
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    int lineStart = leadingPip ? x + 22 : x;
    int lineW = Math.Max(0, width - (leadingPip ? 22 : 0));
    if (leadingPip)
      GuiSkin.DrawHexPip(batch, new Rectangle(x, y - 6, 14, 14), color, true);
    if (lineW > 0)
      batch.Draw(pixel, new Rectangle(lineStart, y, lineW, 2), color * 0.65f);
  }

  // Draws a clean vertical divider line separating bottom/side unboxed sections.
  private static void DrawCleanVertDivider(SpriteBatch batch, int x, int topY, int height, Color color)
  {
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    GuiSkin.DrawHexPip(batch, new Rectangle(x - 6, topY - 4, 14, 14), color * 0.85f, false);
    batch.Draw(pixel, new Rectangle(x, topY + 14, 2, Math.Max(0, height - 14)), color * 0.45f);
  }

  // Draws a clean unboxed stat row with a small leading pip, left label, right-aligned value, and a thin bottom divider line.
  private void DrawCleanStatRow(SpriteBatch batch, Rectangle rowRect, string label, string valueText, string unitText, Color pipColor)
  {
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.DrawHexPip(batch, new Rectangle(rowRect.X, rowRect.Y + 10, 16, 16), pipColor, true);
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    batch.Draw(pixel, new Rectangle(rowRect.X, rowRect.Bottom - 2, rowRect.Width, 2), GuiSkin.BorderColor * 0.28f);
    batch.End();

    ShipyardLabel(label, new Vector2(rowRect.X + 28, rowRect.Y + 4), 23, GuiSkin.MutedTextColor);
    string fullVal = string.IsNullOrEmpty(unitText) ? valueText : $"{valueText} {unitText}";
    float valW = Measure2(fullVal, Vector2.Zero, 28).X;
    ShipyardLabel(fullVal, new Vector2(rowRect.Right - valW, rowRect.Y), 28, GuiSkin.StatHeadingColor);
  }

  private void DrawShipyard(SpriteBatch batch)
  {
    var outer = ShipyardPanel;
    var cursor = GameInput.UiCursor;
    var position = new Vector2(cursor.X, cursor.Y);

    // Single clean dark backdrop over gameplay — no inner boxes or sub-panels.
    if (!shipyardDiscoverySelected)
    {
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.Panel(batch, outer, header: false, GuiSkin.PanelAccent);
      batch.End();
    }

    // 1. Top-Left: 5 Hexagonal Ship Class Medallions + Equipped Module Pips
    for (int i = 0; i < ShipyardNames.Length; i++)
    {
      var tab = ShipyardTab(i);
      bool selected = !shipyardDiscoverySelected && selectedShipyardTab == i;
      bool over = tab.Contains(position);
      bool unlocked = UpgradeManager.Instance.IsHarvesterDiscovered(i);
      Color accent = !unlocked ? GuiSkin.BorderColor * 0.42f : selected ? GuiSkin.PanelAccent : GuiSkin.ButtonBorderColor;

      int medD = selected ? 136 : 122;
      var medRect = new Rectangle(tab.Center.X - medD / 2, tab.Center.Y - medD / 2, medD, medD);

      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      if (selected)
      {
        float spin = (float)BaseGame.Time.TotalGameTime.TotalSeconds * 0.65f;
        GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", new Vector2(medRect.Center.X, medRect.Center.Y),
          medD + 22, spin, GuiSkin.PanelAccent * 0.72f);
      }
      GuiSkin.DrawHexSocket(batch, medRect, over ? Color.White : accent, active: selected || over);
      DrawShipEquippedPips(batch, i, new Point(tab.Center.X - 41, tab.Bottom + 8), 16, 6);
      batch.End();

      if (unlocked)
      {
        int inset = medD * 22 / 100;
        DrawShipyardShip(batch, i, new Rectangle(medRect.X + inset, medRect.Y + inset, medRect.Width - inset * 2, medRect.Height - inset * 2));
      }
      else
      {
        DrawShipyardLabelCentered("?", new Vector2(medRect.Center.X, medRect.Center.Y), 38, GuiSkin.LockedTextColor);
      }
    }

    // 2. Top-Right (above Available Modules): Discovery Button
    var discTab = DiscoveryTab;
    bool pending = ModuleInventory.PendingReveals.Count > 0;
    DrawHudButton(batch, discTab, pending ? Loc.T("Discovery") + " !" : Loc.T("Discovery"),
      GuiSkin.PanelAccent, shipyardDiscoverySelected || pending, discTab.Contains(position), 0, tab: true, badge: pending);

    if (shipyardDiscoverySelected)
    {
      if (revealingModule != ShipModule.None) DrawModuleReveal(batch);
      else DrawShipyardDiscovery(batch);
      return;
    }

    string shipName = Loc.T(ShipyardNames[selectedShipyardTab]);

    // Clean Elden-Ring style Ship Title + Role subtitle with trailing divider line
    ShipyardLabel(Loc.Upper(shipName), new Vector2(240, 412), 40, GuiSkin.StatHeadingColor);
    string roleText = UpgradeManager.Instance.IsHarvesterDiscovered(selectedShipyardTab)
      ? ShipyardRoles[selectedShipyardTab] : Loc.T("HARVESTER LOCKED");
    ShipyardLabel(roleText, new Vector2(240, 466), 23,
      UpgradeManager.Instance.IsHarvesterDiscovered(selectedShipyardTab) ? GuiSkin.MutedTextColor : GuiSkin.LockedTextColor);
    int roleW = (int)Measure2(roleText, Vector2.Zero, 23).X;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, 240 + roleW + 24, 480, Math.Max(120, 680 - roleW), GuiSkin.PanelAccent, leadingPip: true);
    batch.End();

    if (!UpgradeManager.Instance.IsHarvesterDiscovered(selectedShipyardTab))
    {
      DrawShipyardLabelCentered(Loc.T("HARVESTER LOCKED"), new Vector2(outer.Center.X, outer.Y + 520), 40, GuiSkin.LockedTextColor);
      DrawShipyardLabelCentered(Loc.F("Unlock {0} in the upgrade tree to access its modules.", shipName),
        new Vector2(outer.Center.X, outer.Y + 600), 30, GuiSkin.ButtonTextColor);
      DrawShipyardLabelCentered(Loc.T("Once discovered, this ship remains available here after prestige."),
        new Vector2(outer.Center.X, outer.Y + 664), 26, GuiSkin.MutedTextColor);
      return;
    }

    // 3. Center-Left: Large Unboxed 3D Rotating Ship on Holo Turntable
    var shipTurntable = new Rectangle(180, 484, 1240, 756);
    DrawShowcaseOrFallback(batch, shipTurntable, sizeScale: 1.14f);

    // 4. Vertical Column of 4 Hexagonal Hardpoint Slots on the Right Edge of the Ship Showcase
    ShipModule hovered = ShipModule.None;
    bool hoveredEquipped = false;
    int hoveredLockedSlot = -1;

    for (int slot = 0; slot < ModuleCatalog.MaxSlotsPerType; slot++)
    {
      var bounds = ModuleSlot(slot);
      bool over = bounds.Contains(position);

      if (slot >= ModuleCatalog.UnlockedSlots)
      {
        batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
        GuiSkin.DrawHexSocket(batch, bounds, GuiSkin.BorderColor * (over ? 0.52f : 0.32f), active: false);
        if (TextureCache.LockIcon?.IsLoaded == true && !TextureCache.LockIcon.IsFailed)
        {
          int lockSize = 54;
          var lockRect = new Rectangle(bounds.Center.X - lockSize / 2, bounds.Center.Y - lockSize / 2, lockSize, lockSize);
          batch.Draw(TextureCache.LockIcon.Value, lockRect,
            (over ? GuiSkin.LockedTextColor : GuiSkin.MutedTextColor) * (over ? 0.85f : 0.45f));
        }
        batch.End();
        if (TextureCache.LockIcon?.IsLoaded != true || TextureCache.LockIcon.IsFailed)
          DrawShipyardLabelCentered("—", new Vector2(bounds.Center.X, bounds.Center.Y), 34, GuiSkin.LockedTextColor * 0.65f);
        if (over) hoveredLockedSlot = slot;
        continue;
      }

      int slotIndex = selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + slot;
      var module = ModuleInventory.Slots[slotIndex];
      bool slotSelected = selectedModuleSlot == slot;
      Color slotAccent = moduleDragging && over ? GuiSkin.ConfirmAccent
        : module != ShipModule.None ? ModuleColor(module)
        : slotSelected ? GuiSkin.PanelAccent
        : GuiSkin.ButtonBorderColor * 0.78f;

      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      if (slotSelected)
      {
        float spin = (float)BaseGame.Time.TotalGameTime.TotalSeconds * 0.6f;
        GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", new Vector2(bounds.Center.X, bounds.Center.Y),
          bounds.Width + 18, spin, slotAccent * 0.55f);
      }
      GuiSkin.DrawHexSocket(batch, bounds, over ? Color.Lerp(slotAccent, Color.White, 0.35f) : slotAccent,
        active: module != ShipModule.None || over || slotSelected);
      batch.End();

      if (module == ShipModule.None)
      {
        DrawShipyardLabelCentered("+", new Vector2(bounds.Center.X, bounds.Center.Y), 44,
          slotSelected || over ? GuiSkin.PanelAccent : GuiSkin.MutedTextColor);
      }
      else
      {
        int iconD = bounds.Width * 52 / 100;
        QueueModuleIcon(module, new Rectangle(bounds.Center.X - iconD / 2, bounds.Center.Y - iconD / 2, iconD, iconD),
          moduleDragging && draggedModuleSource == slotIndex ? 0.25f : 1f);
        if (over) { hovered = module; hoveredEquipped = true; }
      }
    }

    // 5. Right Side: "Available Modules" Heading + Clean Horizontal Divider Line + Unboxed Hex Module Grid
    ShipyardLabel(Loc.T("Available Modules"), new Vector2(AvailableLeft, 360), 46, GuiSkin.StatHeadingColor);
    string collCount = ModuleInventory.CollectionComplete && ModuleInventory.PendingReveals.Count == 0
      ? Loc.T("Module collection complete")
      : Loc.F("Module collection ({0}/{1})", ModuleInventory.RevealedCount, ModuleCatalog.Names.Length - 1);
    float collW = Measure2(collCount, Vector2.Zero, 24).X;
    ShipyardLabel(collCount, new Vector2(AvailableLeft + AvailableWidth - collW, 380), 24, GuiSkin.MutedTextColor);

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    var pixel = GuiSkin.Pixel(batch.GraphicsDevice);
    Color headerLineColor = moduleDragging && draggedModuleSource >= 0 && ModuleInventoryBounds.Contains(position)
      ? GuiSkin.ConfirmAccent : Color.Lerp(GuiSkin.StatHeadingColor, GuiSkin.PanelAccent, 0.3f) * 0.72f;
    batch.Draw(pixel, new Rectangle(AvailableLeft, 434, AvailableWidth, 2), headerLineColor);
    batch.End();

    var available = ModuleInventory.GetAvailableModules().ToArray();
    for (int index = 0; index < available.Length; index++)
    {
      var bounds = ModuleTile(index);
      var module = available[index];
      bool over = bounds.Contains(position);
      Color modColor = ModuleColor(module);

      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      if (over)
      {
        float spin = (float)BaseGame.Time.TotalGameTime.TotalSeconds * 0.7f;
        GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", new Vector2(bounds.Center.X, bounds.Center.Y),
          bounds.Width + 18, spin, modColor * 0.65f);
      }
      GuiSkin.DrawHexSocket(batch, bounds, over ? Color.Lerp(modColor, Color.White, 0.35f) : modColor, active: over);
      batch.End();

      int iconSize = bounds.Width * 52 / 100;
      QueueModuleIcon(module, new Rectangle(bounds.Center.X - iconSize / 2, bounds.Center.Y - iconSize / 2, iconSize, iconSize),
        moduleDragging && draggedModuleSource < 0 && draggedModule == module ? 0.25f : 1f);
      if (over) { hovered = module; hoveredEquipped = false; }
    }

    if (available.Length == 0)
    {
      ShipyardLabel(ModuleInventory.RevealedCount == 0 ? Loc.T("No modules discovered yet") : Loc.T("All discovered modules are equipped"),
        new Vector2(AvailableLeft, AvailableGridTop + 16), 28, GuiSkin.ButtonTextColor);
      ShipyardLabel(Loc.T("Recover more modules by harvesting, or return an equipped module here."),
        new Vector2(AvailableLeft, AvailableGridTop + 64), 23, GuiSkin.MutedTextColor);
    }

    // 6. Bottom-Left Row: Base Stats + Vertical Divider Line + Gem Targeting Protocol
    const int bottomTop = 1316;
    const int bottomHeight = 396;
    var (speed, capacity, fuel, strategy) = GetHarvesterTelemetry(selectedShipyardTab);

    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanVertDivider(batch, 780, bottomTop, bottomHeight, GuiSkin.PanelAccent);
    batch.End();

    // Bottom Column 1 (X = 240 .. 716): Base Stats with Horizontal Divider Lines
    ShipyardLabel(Loc.Upper(Loc.T("Base stats")), new Vector2(240, bottomTop), 24, GuiSkin.PanelAccent);
    ShipyardLabel(Loc.T("Before upgrades and modules"), new Vector2(240, bottomTop + 36), 20, GuiSkin.MutedTextColor);

    DrawCleanStatRow(batch, new Rectangle(240, bottomTop + 96, 476, 54),
      Loc.Upper(Loc.T("Speed:")), $"{speed:0}", Loc.T("units/s"), GuiSkin.PanelAccent);
    DrawCleanStatRow(batch, new Rectangle(240, bottomTop + 174, 476, 54),
      Loc.Upper(Loc.T("Cargo:")), $"{capacity:0}", Loc.T("gems"), GuiSkin.ConfirmAccent);
    DrawCleanStatRow(batch, new Rectangle(240, bottomTop + 252, 476, 54),
      Loc.Upper(Loc.T("Fuel:")), $"{fuel:0}", "", GuiSkin.UpgradeAccent);

    // Bottom Column 2 (X = 844 .. 1760): Gem Targeting Protocol + Active Hardpoints
    ShipyardLabel(Loc.Upper(Loc.T("Gem targeting")), new Vector2(844, bottomTop), 24, GuiSkin.PanelAccent);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    DrawCleanHorizDivider(batch, 844, bottomTop + 44, 520, GuiSkin.PanelAccent, leadingPip: true);
    batch.End();

    DrawWrappedText(strategy, new Vector2(844, bottomTop + 68), 900, 26, 38, GuiSkin.ButtonTextColor, 3);
    ShipyardLabel($"{Loc.T("Modules apply to every ship of this type")}. {Loc.T("Changes apply on the next trip.")}",
      new Vector2(844, bottomTop + 198), 22, GuiSkin.MutedTextColor);

    int equippedCount = 0;
    for (int s = 0; s < ModuleCatalog.UnlockedSlots; s++)
      if (ModuleInventory.Slots[selectedShipyardTab * ModuleCatalog.MaxSlotsPerType + s] != ShipModule.None)
        equippedCount++;
    ShipyardLabel(Loc.F("ACTIVE HARDPOINTS: {0} / {1} EQUIPPED", equippedCount, ModuleCatalog.UnlockedSlots),
      new Vector2(844, bottomTop + 340), 22, GuiSkin.ConfirmAccent);

    FlushIcons(batch, SamplerState.PointClamp);
    if (moduleDragging)
    {
      var ghost = new Rectangle((int)position.X + 16, (int)position.Y + 16, 112, 112);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.DrawHexSocket(batch, ghost, ModuleColor(draggedModule), active: true);
      batch.End();
      QueueModuleIcon(draggedModule, new Rectangle(ghost.X + 24, ghost.Y + 24, 64, 64));
      FlushIcons(batch, SamplerState.PointClamp);
    }
    else if (hovered != ShipModule.None)
    {
      DrawModuleTooltip(batch, hovered, position, hoveredEquipped);
    }
    else if (hoveredLockedSlot >= 0)
    {
      DrawLockedSlotTooltip(batch, hoveredLockedSlot, position);
    }
  }
}
