using System;
using AsyncContent;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  private static Color SignalAccent => GuiSkin.PanelAccent;
  private readonly Random signalRandom = new();
  private SignalProgression Signals => UpgradeManager.Instance.Signals;
  private bool signalChoicesVisible => Signals.PendingChoices.Count == 3;
  private double signalRevealStarted;
  // English rarity names, shown upper-cased in the current language (SignalRarityLabel).
  private static readonly (string Name, GuiColor Role)[] SignalRarityRoles =
  [
    (Loc.N("Common"), GuiColor.Text),
    (Loc.N("Uncommon"), GuiColor.Uncommon),
    (Loc.N("Rare"), GuiColor.Ability),
    (Loc.N("Epic"), GuiColor.Prestige),
    (Loc.N("Legendary"), GuiColor.Upgrade)
  ];
  private static (string Name, Color Color) SignalRarity(int index)
    => (SignalRarityRoles[index].Name, GuiPalette.Get(SignalRarityRoles[index].Role));
  private static string SignalRarityLabel(int rarity) => Loc.Upper(Loc.T(SignalRarity(rarity).Name));
  private static SignalDefinition[] SignalPreviews => SignalCatalog.Definitions;
  private int signalCollectionPage;
  private static int SignalCollectionColumns => Math.Max(1, (SignalPanel.Width - 96) / 352);
  private static int SignalCollectionPageSize => SignalCollectionColumns * Math.Max(1, (SignalPanel.Height - 540) / 352);
  private int SignalCollectionPages
  {
    get
    {
      int count = 0;
      for (int i = 0; i < SignalProgression.SignalCount; i++) if (Signals.StackCount(i) > 0) count++;
      return Math.Max(1, (count + SignalCollectionPageSize - 1) / SignalCollectionPageSize);
    }
  }
  private static Rectangle SignalPreviousPage => new(SignalPanel.Center.X - 310, SignalPanel.Bottom - 215, 180, 52);
  private static Rectangle SignalNextPage => new(SignalPanel.Center.X + 130, SignalPanel.Bottom - 215, 180, 52);

  private static Rectangle SignalPanel => new(120, 196, HudLayout.Width - 240, HudLayout.ContentBottom - 240);
  private static Rectangle SignalScanButton => new(HudLayout.Width / 2 - 190, SignalPanel.Bottom - 140, 380, 72);
  private static Rectangle SignalCard(int index)
  {
    var panel = SignalPanel;
    const int gap = 40;
    int width = Math.Min(780, (panel.Width - 128 - gap * 2) / 3);
    int height = Math.Min(1000, panel.Height - 160);
    int left = panel.Center.X - (width * 3 + gap * 2) / 2;
    return new Rectangle(left + index * (width + gap), panel.Center.Y - height / 2, width, height);
  }

  private void UpdateSignalsInput()
  {
    if (!UpgradeManager.Instance.UGM.SignalsUnlocked) return;
    var cursor = GameInput.UiCursor;
    if (m_upgradeWindowType == UpgradeTypes.Signals && signalChoicesVisible)
    {
      float dt = Math.Clamp((float)BaseGame.Time.ElapsedGameTime.TotalSeconds, 0.001f, 0.1f);
      for (int i = 0; i < 3; i++)
      {
        bool hovered = SignalRevealAge(i) >= 0.65f && (IsCardHoverPinned(i) || SignalCard(i).Contains(cursor.X, cursor.Y));
        signalHoverAmounts[i] = hovered
          ? Math.Min(1f, signalHoverAmounts[i] + dt * 10f)
          : Math.Max(0f, signalHoverAmounts[i] - dt * 8f);
      }
    }
    if (!GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    if (HudLayout.NavigationButton(HudLayout.SignalsTab).Contains(cursor.X, cursor.Y))
    {
      SetUpgradeType(m_upgradeWindowType == UpgradeTypes.Signals ? UpgradeTypes.None : UpgradeTypes.Signals);
      m_animateButtonClickSignals = 0.001f;
      return;
    }
    if (m_upgradeWindowType != UpgradeTypes.Signals || (IsDetached && !IsPopoutFocused)) return;
    if (!signalChoicesVisible && SignalCollectionPages > 1)
    {
      if (SignalPreviousPage.Contains(cursor.X, cursor.Y))
      {
        signalCollectionPage = Math.Max(0, signalCollectionPage - 1);
        return;
      }
      if (SignalNextPage.Contains(cursor.X, cursor.Y))
      {
        signalCollectionPage = Math.Min(SignalCollectionPages - 1, signalCollectionPage + 1);
        return;
      }
    }
    if (!signalChoicesVisible && SignalScanButton.Contains(cursor.X, cursor.Y))
    {
      if (UntitledGemGameGameScreen.Instance.TryScanSignals(signalRandom))
      {
        ClearSignalParticles();
        signalRevealStarted = BaseGame.Time.TotalGameTime.TotalSeconds;
      }
      return;
    }
    if (!signalChoicesVisible || PinSignalHoverMode != 0) return;
    for (int i = 0; i < 3; i++)
      if (SignalRevealAge(i) >= 0.65f && SignalCard(i).Contains(cursor.X, cursor.Y))
      {
        UntitledGemGameGameScreen.Instance.TryChooseSignal(i);
        return;
      }
  }

  private void DrawSignalsNavigation(SpriteBatch batch)
  {
    if (!UpgradeManager.Instance.UGM.SignalsUnlocked) return;
    var bounds = HudLayout.NavigationButton(HudLayout.SignalsTab);
    bool selected = m_upgradeWindowType == UpgradeTypes.Signals;
    DrawHudButton(batch, bounds, selected ? Loc.T("Hide") : Loc.T("Signals"), SignalAccent,
      selected, bounds.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y), m_animateButtonClickSignals, tab: true);
  }

  private void SignalLabel(string text, float centerX, float y, float size, Color color)
  {
    var measure = Measure2(text, Vector2.Zero, size);
    FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
      text, new Vector2(centerX - measure.X / 2, y), color, Color.Black, size);
  }

  private static Rectangle SignalCollectionTile(int index)
  {
    var panel = SignalPanel;
    const int width = 320, gap = 32;
    int columns = Math.Max(1, (panel.Width - 128 + gap) / (width + gap));
    return new Rectangle(panel.X + 64 + index % columns * (width + gap),
      panel.Y + 290 + index / columns * (width + gap), width, width);
  }

  private string SignalTotal(int id)
    => Loc.F("Total: {0}{1:0.##}% {2}", SignalPreviews[id].Reduction ? "-" : "+", Signals.BonusPercent(id),
      Loc.T(SignalPreviews[id].Target));

  private void DrawSignalCollection(SpriteBatch batch)
  {
    signalCollectionPage = Math.Clamp(signalCollectionPage, 0, SignalCollectionPages - 1);
    int visible = 0, hoveredId = -1;
    Rectangle hoveredTile = default;
    for (int id = 0; id < SignalProgression.SignalCount; id++)
    {
      if (Signals.StackCount(id) == 0) continue;
      int index = visible++ - signalCollectionPage * SignalCollectionPageSize;
      if (index < 0 || index >= SignalCollectionPageSize) continue;
      var tile = SignalCollectionTile(index);
      bool hovered = tile.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.TechCard(batch, tile, hovered ? GuiPalette.Base(new Color(14, 34, 58, 244)) : GuiPalette.Base(new Color(8, 22, 42, 238)),
        hovered ? Color.White : SignalAccent, hovered, hovered, cornerGraphic: true, hexWatermark: false);
      GuiSkin.DrawHexSocket(batch, new Rectangle(tile.Center.X - 72, tile.Y + 34, 144, 144), SignalAccent, active: hovered);
      GuiSkin.TechDivider(batch, new Rectangle(tile.X + 36, tile.Bottom - 122, tile.Width - 72, 3), SignalAccent * 0.60f);
      batch.End();
      QueueSignalIcon(id, new Vector2(tile.Center.X, tile.Y + 106), 76);
      SignalLabel(Loc.T(SignalPreviews[id].Name), tile.Center.X, tile.Bottom - 108, 26, GuiSkin.ButtonTextColor);
      SignalLabel($"x{Signals.StackCount(id)}", tile.Center.X, tile.Bottom - 62, 32, SignalAccent);
      if (hovered) { hoveredId = id; hoveredTile = tile; }
    }
    if (visible == 0)
    {
      SignalLabel(Loc.T("No signals discovered yet"), SignalPanel.Center.X, SignalPanel.Center.Y - 30, 36, GuiSkin.ButtonTextColor);
      SignalLabel(Loc.T("Scan deep space and choose your first enhancement."), SignalPanel.Center.X, SignalPanel.Center.Y + 36, 26, GuiSkin.MutedTextColor);
    }
    if (SignalCollectionPages > 1)
    {
      var cursor = GameInput.UiCursor;
      DrawHudButton(batch, SignalPreviousPage, Loc.T("Previous"), SignalAccent, false,
        signalCollectionPage > 0 && SignalPreviousPage.Contains(cursor.X, cursor.Y), 0);
      DrawHudButton(batch, SignalNextPage, Loc.T("Next"), SignalAccent, false,
        signalCollectionPage + 1 < SignalCollectionPages && SignalNextPage.Contains(cursor.X, cursor.Y), 0);
      SignalLabel($"{signalCollectionPage + 1} / {SignalCollectionPages}", SignalPanel.Center.X,
        SignalPreviousPage.Y + 12, 24, GuiSkin.MutedTextColor);
    }
    FlushIcons(batch, SamplerState.LinearClamp);
    if (hoveredId < 0) return;
    int x = Math.Clamp(hoveredTile.Center.X - 480, SignalPanel.Left + 24, SignalPanel.Right - 984);
    int y = Math.Min(hoveredTile.Bottom + 20, SignalScanButton.Top - 270);
    var tooltip = new Rectangle(x, y, 960, 250);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.Tooltip(batch, tooltip);
    GuiSkin.TechDivider(batch, new Rectangle(tooltip.X + 48, tooltip.Y + 72, tooltip.Width - 96, 3), SignalAccent * 0.65f);
    batch.End();
    SignalLabel(Loc.T(SignalPreviews[hoveredId].Name), tooltip.Center.X, y + 22, 32, SignalAccent);
    SignalLabel(SignalPreviews[hoveredId].Description, tooltip.Center.X, y + 84, 24, GuiSkin.ButtonTextColor);
    SignalLabel(SignalTotal(hoveredId), tooltip.Center.X, y + 132, 26, GuiSkin.StatValueColor);
    SignalLabel(Loc.P(Signals.StackCount(hoveredId), "{0} discovery combined", "{0} discoveries combined"),
      tooltip.Center.X, y + 190, 22, GuiSkin.MutedTextColor);
  }

  private void QueueSignalIcon(int id, Vector2 center, float size)
  {
    var region = TextureCache.SignalIcons[id];
    QueueIcon(region, center, size / Math.Max(1, Math.Max(region.Width, region.Height)), Color.White);
  }

  private float SignalRevealAge(int index)
    => (float)(BaseGame.Time.TotalGameTime.TotalSeconds - signalRevealStarted) - index * 0.22f;

  private void SignalCardLabel(string text, Rectangle card, float y, float size, Color color)
  {
    float measuredWidth = Measure2(text, Vector2.Zero, size).X;
    if (measuredWidth > card.Width - 80) size *= (card.Width - 80) / measuredWidth;
    SignalLabel(text, card.Center.X, y, size, color);
  }

  private static Vector2 SignalChoiceIconCenter(Rectangle card) => new(card.Center.X, card.Y + 244);

  private void DrawSignalRevealBurst(SpriteBatch batch, int index, Rectangle card, int rarity, float age)
  {
    // Begin with the visible reveal, not the preceding decoding stage.
    float elapsed = age - .3f;
    float duration = .7f + rarity * .3f;
    if (rarity == 0 || elapsed < 0 || elapsed >= duration) return;
    float progress = elapsed / duration;
    var color = SignalRarity(rarity).Color;
    var center = SignalChoiceIconCenter(card);
    var interior = new Rectangle(card.X + 8, card.Y + 8, card.Width - 16, card.Height - 16);
    float radius = Math.Min(card.Width * .32f, card.Height * .22f);
    batch.Begin(blendState: Microsoft.Xna.Framework.Graphics.BlendState.Additive, transformMatrix: HudLayout.RenderTransform);
    void Spark(Vector2 position, int size, Color tint)
    {
      var bounds = Rectangle.Intersect(interior,
        new Rectangle((int)position.X - size / 2, (int)position.Y - size / 2, size, size));
      if (bounds.Width > 0 && bounds.Height > 0)
        batch.Draw(AssetManager.DefaultTexture, bounds, tint);
    }
    // One short illumination pulse; no repeated flashes while choosing.
    float flash = MathF.Pow(Math.Max(0, 1 - elapsed / .35f), 2);
    if (rarity >= 2)
      batch.Draw(AssetManager.DefaultTexture, interior, color * (flash * (.035f + rarity * .015f)));
    int count = 10 + rarity * 10;
    // Desktop motes live in a small GPU pool per card. Keep the analytic browser fallback.
    for (int i = 0; signalParticlePools[index] == null && i < count; i++)
    {
      // Stable per-particle variation makes the burst independent of frame rate.
      float seed = (i * .618034f) % 1;
      float angle = i * 2.399963f;
      var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
      float distance = 70 + radius * (.45f + seed * .55f) * (1 - MathF.Pow(1 - progress, 3));
      var position = center + direction * distance;
      float opacity = (1 - progress) * (.45f + seed * .55f);
      int size = 3 + rarity + (int)(seed * 3);
      Spark(position, size * 3, color * (opacity * .08f));
      Spark(position, size, Color.Lerp(color, Color.White, .45f) * opacity);
      if (rarity >= 2)
        for (int trail = 1; trail <= rarity * 2; trail++)
          Spark(position - direction * (trail * 7), Math.Max(2, size - trail / 2),
            color * (opacity * .3f * (1 - trail / (rarity * 2f + 1))));
    }
    // Segmented shock rings read as a scanner discharge around the icon housing.
    if (rarity >= 3)
    {
      int rings = rarity == 4 ? 2 : 1;
      for (int ring = 0; ring < rings; ring++)
      {
        float phase = Math.Clamp((elapsed - ring * .18f) / (duration - ring * .18f), 0, 1);
        if (elapsed < ring * .18f) continue;
        float ringRadius = 90 + radius * phase;
        for (int segment = 0; segment < 96; segment++)
        {
          if (segment % 12 >= 9) continue;
          float angle = MathF.Tau * segment / 96 + ring * .1f;
          Spark(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius,
            rarity == 4 ? 5 : 3, color * ((1 - phase) * .65f));
        }
      }
    }
    batch.End();
  }

  private void DrawSignalGpuParticles(SpriteBatch batch, int index, Rectangle card)
  {
    var particles = signalParticlePools[index];
    if (particles == null || !particles.HasParticles) return;
    var center = SignalChoiceIconCenter(card);
    var viewport = batch.GraphicsDevice.Viewport;
    particles.Draw(Matrix.CreateTranslation(center.X, center.Y, 0) * HudLayout.RenderTransform
      * Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1));
  }

  private static void DrawSignalHexGem(SpriteBatch batch, Point center, int size, Color accent, float pulse = 0f)
  {
    var glow = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
    var underlay = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_hud_hex_underlay");
    var ring = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_hud_ring_hex_med");

    int glowSize = (int)MathF.Round(size * (1.85f + pulse * 0.25f));
    batch.Draw(glow, new Rectangle(center.X - glowSize / 2, center.Y - glowSize / 2, glowSize, glowSize),
      accent * (0.34f + pulse * 0.14f));

    int outerSize = size + 10;
    batch.Draw(underlay, new Rectangle(center.X - outerSize / 2, center.Y - outerSize / 2, outerSize, outerSize),
      GuiPalette.Base(new Color(4, 11, 22, 252)));
    batch.Draw(ring, new Rectangle(center.X - outerSize / 2, center.Y - outerSize / 2, outerSize, outerSize),
      Color.Lerp(accent, Color.White, 0.28f) * 0.94f);

    int innerSize = Math.Max(8, size - 2);
    batch.Draw(underlay, new Rectangle(center.X - innerSize / 2, center.Y - innerSize / 2, innerSize, innerSize),
      Color.Lerp(accent, Color.White, 0.24f + pulse * 0.12f) * 0.96f);

    int coreSize = Math.Max(4, size - 8);
    batch.Draw(underlay, new Rectangle(center.X - coreSize / 2, center.Y - coreSize / 2, coreSize, coreSize),
      Color.Lerp(accent, Color.White, 0.68f + pulse * 0.18f));
  }

  private static void DrawSignalInsetPlaque(SpriteBatch batch, Rectangle bounds, Color fill, Color accent, float borderOpacity, bool doubleBorder)
  {
    int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 3, 10, 16);
    Color borderCol = Color.Lerp(accent, Color.White, 0.22f) * borderOpacity;
    GuiSkin.DrawChamferedBox(batch, bounds, corner, fill, borderCol, glassAlpha: 0.12f * borderOpacity, vignetteAlpha: 0.45f);
    if (doubleBorder)
    {
      var inner = new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
      GuiSkin.NineSliceScaled(batch, "synty_hud_frame_06", inner, 44, Math.Max(6, corner - 3),
        Color.Lerp(accent, Color.White, 0.45f) * 0.52f);
    }
  }

  private readonly float[] signalHoverAmounts = new float[3];

  private void BeginSignalCardBatch(SpriteBatch batch, Rectangle uvBounds, int rarityIndex, Color rarity, float hover, bool revealed, float effectMode)
  {
    var fxAsset = EffectCache.SignalCardFx;
    if (revealed && fxAsset != null && fxAsset.IsLoaded && fxAsset.Value != null)
    {
      var fx = fxAsset.Value;
      var viewport = batch.GraphicsDevice.Viewport;
      var projection = HudLayout.RenderTransform
        * Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);
      float time = (float)BaseGame.Time.TotalGameTime.TotalSeconds + uvBounds.X * 0.0013f;

      fx.Parameters["view_projection"]?.SetValue(projection);
      fx.Parameters["CardRect"]?.SetValue(new Vector4(uvBounds.X, uvBounds.Y, uvBounds.Width, uvBounds.Height));
      fx.Parameters["RarityColor"]?.SetValue(rarity.ToVector4());
      fx.Parameters["Time"]?.SetValue(time);
      fx.Parameters["Rarity"]?.SetValue((float)rarityIndex);
      fx.Parameters["Hovered"]?.SetValue(hover);
      fx.Parameters["EffectMode"]?.SetValue(effectMode);
      fx.Parameters["HoverOutlineParams"]?.SetValue(HoverOutlineShaderParams);
      fx.Parameters["HoverOutlineWaveSpeed"]?.SetValue(OutlineWaveSpeed);
      fx.Parameters["GlassTexture"]?.SetValue(GuiSkin.GetTexture(batch.GraphicsDevice, "synty_hud_glass_02"));

      batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
        null, null, fx, null);
    }
    else
    {
      batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
        null, null, null, HudLayout.RenderTransform);
    }
  }

  private void DrawSignalCardFrame(SpriteBatch batch, Rectangle card, int rarityIndex, Color rarity, bool revealed, float hover)
  {
    int tier = revealed ? Math.Clamp(rarityIndex, 0, 4) : 0;
    float time = (float)BaseGame.Time.TotalGameTime.TotalSeconds;
    float pulse = 0.5f + 0.5f * MathF.Sin(time * (1.8f + tier * 0.35f) + card.X * 0.005f);
    var iconCenter = new Point(card.Center.X, card.Y + 244);
    var iconCenterVec = new Vector2(iconCenter.X, iconCenter.Y);
    var upperBay = new Rectangle(card.X + 38, card.Y + 104, card.Width - 76, 344);
    var descBox = new Rectangle(card.X + 38, card.Y + 500, card.Width - 76, 382);
    int bridgeY = (upperBay.Bottom + descBox.Y) / 2;
    string railAsset = tier >= 2 ? "synty_hud_frame_horiz" : "synty_hud_frame_horiz_02";

    Color baseFill = Color.Lerp(GuiPalette.Base(new Color(6, 17, 32, 246)), rarity, tier * 0.010f);
    Color hoverFill = Color.Lerp(GuiPalette.Base(new Color(14, 34, 58, 250)), rarity, 0.06f + tier * 0.012f);
    Color fill = Color.Lerp(baseFill, hoverFill, hover);

    Color baseArmor = Color.Lerp(new Color(18, 36, 62), rarity, 0.34f + tier * 0.11f) * (0.82f + tier * 0.04f);
    Color armorColor = Color.Lerp(baseArmor, Color.Lerp(rarity, Color.White, 0.18f), hover * 0.28f);
    Color baseRim = Color.Lerp(rarity, Color.White, 0.12f + tier * 0.05f);
    Color rimColor = Color.Lerp(baseRim, Color.White, hover * 0.42f);
    Color railArmorColor = armorColor * 0.86f;
    Color railRimColor = rimColor * (0.82f + tier * 0.04f + hover * 0.14f);

    // Pass 0: Crisp 1:1 SDF geometric contour line + parallel tech-trace + orbiting comets on hover
    if (revealed && hover > 0.001f)
    {
      const int auraPad = 32;
      var auraRect = new Rectangle(card.X - auraPad, card.Y - auraPad, card.Width + auraPad * 2, card.Height + auraPad * 2);
      BeginSignalCardBatch(batch, auraRect, tier, rarity, hover, revealed, effectMode: 2f);
      var auraTex = GuiSkin.GetClamshellHoverAuraTexture(batch.GraphicsDevice);
      batch.Draw(auraTex, auraRect, Color.White);
      batch.End();
    }

    // Pass 1: Structural base, framed compartments, and undistorted outer clamshell frame
    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
      null, null, null, HudLayout.RenderTransform);

    var glowTex = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_fx_glow_01");
    if (tier >= 2)
    {
      int pad = 26 + tier * 10;
      float ambientAlpha = 0.05f + tier * 0.025f + pulse * (0.015f * tier);
      var glowRect = new Rectangle(card.X - pad, card.Y - pad, card.Width + pad * 2, card.Height + pad * 2);
      batch.Draw(glowTex, glowRect, rarity * ambientAlpha);
    }

    // Solid dark interior card body tucked strictly inside the outer clamshell armor perimeter
    var cardBody = new Rectangle(card.X + 20, card.Y + 12, card.Width - 40, card.Height - 54);
    GuiSkin.DrawChamferedBoxBase(batch, cardBody, 28, fill, vignetteAlpha: 0.50f - hover * 0.14f);

    if (revealed)
    {
      // Upper Reactor Viewport Base (chamfer-clipped fill and vignette, zero exterior bleed)
      Color bayFill = Color.Lerp(GuiPalette.Base(new Color(4, 12, 24, 236)), rarity, 0.02f + tier * 0.015f + hover * 0.04f);
      GuiSkin.DrawChamferedBoxBase(batch, upperBay, 18, bayFill, vignetteAlpha: 0.48f - hover * 0.10f);

      // Lower Description Readout Box (pixel-matched 45° chamfered box, zero exterior bleed)
      Color descFill = Color.Lerp(GuiPalette.Base(new Color(4, 11, 22, 242)), rarity, hover * 0.025f);
      Color descBorder = Color.Lerp(rarity, Color.White, 0.14f + hover * 0.28f) * Math.Min(1f, 0.52f + tier * 0.10f + hover * 0.32f);
      GuiSkin.DrawChamferedBox(batch, descBox, 18, descFill, descBorder,
        glassAlpha: 0.04f + tier * 0.015f + hover * 0.06f, vignetteAlpha: 0.48f);

      if (tier >= 3)
      {
        var descInner = new Rectangle(descBox.X + 6, descBox.Y + 6, descBox.Width - 12, descBox.Height - 12);
        GuiSkin.NineSliceScaled(batch, "synty_hud_frame_06", descInner, 44, 14,
          Color.Lerp(rarity, Color.White, 0.25f + hover * 0.22f) * Math.Min(0.95f, (tier == 4 ? 0.42f : 0.26f) + hover * 0.24f));
      }

      // Sub-divider separating description from total stack readout inside descBox
      GuiSkin.TechDivider(batch, new Rectangle(descBox.X + 28, card.Bottom - 206, descBox.Width - 56, 3),
        Color.Lerp(rarity, Color.White, hover * 0.30f) * (0.48f + hover * 0.28f));
    }
    else
    {
      var decodeArea = new Rectangle(card.X + 38, card.Y + 104, card.Width - 76, card.Height - 160);
      GuiSkin.DrawTiled(batch, "synty_hud_crosses", decodeArea, 160, 160, 0, 0, rarity * 0.06f);
    }

    batch.End();

    // Pass 2: Shader Mode 0 — Edge-faded interior background patterns inside upperBay (zero hard cuts)
    if (revealed)
    {
      BeginSignalCardBatch(batch, upperBay, tier, rarity, hover, revealed, effectMode: 0f);

      var bayInterior = new Rectangle(upperBay.X + 8, upperBay.Y + 8, upperBay.Width - 16, upperBay.Height - 16);
      GuiSkin.DrawTiled(batch, "synty_hud_crosses", bayInterior, 160, 160, 0, 0,
        rarity * (0.06f + tier * 0.02f + hover * 0.05f));

      if (tier >= 1 || hover > 0.01f)
      {
        float hexAlpha = (tier >= 1 ? (0.10f + tier * 0.035f) : 0f) + hover * 0.10f;
        var gradHex = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_fx_grad_hex");
        batch.Draw(gradHex, bayInterior, null, rarity * hexAlpha, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
      }

      if (tier >= 2)
      {
        float patchAlpha = 0.14f + (tier - 2) * 0.045f + hover * 0.10f;
        var hexPatch = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_fx_hexpatch");
        int patchSize = 220 + (tier - 2) * 12;
        batch.Draw(hexPatch, new Rectangle(iconCenter.X - patchSize / 2, iconCenter.Y - patchSize / 2, patchSize, patchSize),
          rarity * patchAlpha);
      }

      batch.End();

      // Pass 3: Shader Mode 1 — Smooth holographic sheen on upperBay glass bevel & concentric scanner rings
      BeginSignalCardBatch(batch, upperBay, tier, rarity, hover, revealed, effectMode: 1f);

      GuiSkin.DrawChamferedBoxGlass(batch, upperBay, 18,
        Color.Lerp(rarity, Color.White, 0.32f + hover * 0.22f) * (0.07f + tier * 0.025f + hover * 0.10f));

      if (tier >= 2)
      {
        GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_large_01", iconCenterVec, 192f,
          -time * 0.20f, Color.Lerp(rarity, Color.White, 0.25f + hover * 0.25f) * (0.40f + (tier - 2) * 0.11f + hover * 0.22f));

        var ticksTex = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_hud_calibration_ticks");
        Color tickColor = Color.Lerp(rarity, Color.White, 0.22f + hover * 0.25f) * (0.48f + (tier - 2) * 0.12f + hover * 0.24f);
        batch.Draw(ticksTex, new Rectangle(iconCenter.X - 172, iconCenter.Y - 56, 24, 112), null,
          tickColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
        batch.Draw(ticksTex, new Rectangle(iconCenter.X + 148, iconCenter.Y - 56, 24, 112), null,
          tickColor, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
      }

      if (tier >= 1)
      {
        float calAlpha = 0.38f + tier * 0.08f + hover * 0.24f;
        GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", iconCenterVec, 166f,
          time * 0.30f, Color.Lerp(rarity, Color.White, hover * 0.28f) * Math.Min(0.95f, calAlpha));
      }

      batch.End();
    }

    // Pass 4: Crisp foreground framing, winged collars, plaques, and rarity gems
    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
      null, null, null, HudLayout.RenderTransform);

    if (revealed)
    {
      // Winged collar sits cleanly along the bottom inside of upperBay below the value plaque
      if (tier >= 2)
      {
        Color wingCollarColor = Color.Lerp(rarity, Color.White, 0.22f + hover * 0.24f)
          * Math.Min(0.98f, 0.68f + (tier - 2) * 0.10f + hover * 0.22f);
        GuiSkin.DrawWingedDetail07(batch, card.Center.X, upperBay.Bottom - 52, upperBay.Width - 16, 46,
          wingCollarColor, flipVertical: false, gap: 0);
      }

      // Crisp chamfered border around upperBay enclosing all interior grids, rings, and wing collar
      Color bayBorder = Color.Lerp(rarity, Color.White, 0.16f + hover * 0.28f)
        * Math.Min(1f, 0.54f + tier * 0.10f + hover * 0.32f);
      GuiSkin.NineSliceScaled(batch, "synty_hud_frame_06", upperBay, 44, 18, bayBorder);
    }

    // Header divider rail separating card title from upperBay
    int headerRailH = tier >= 2 ? 22 : 18;
    GuiSkin.DrawSymmetricHorizRail(batch, railAsset, card.Center.X, card.Y + 76, card.Width - 72, headerRailH,
      railArmorColor, railRimColor, flipVertical: false, drawArmor: true, drawRim: true);

    // Outer Clamshell Frame (1:1 aspect ratio corners and side brackets)
    GuiSkin.DrawLargeClamshellFrame(batch, card, armorColor, rimColor, gap: 0, drawArmor: true, drawRim: true);

    // Central Hex Icon Socket — consistent baseline across rarities and consistent illumination on hover
    int glowRadius = 108 + tier * 14 + (int)MathF.Round(hover * 28f);
    batch.Draw(glowTex, new Rectangle(iconCenter.X - glowRadius, iconCenter.Y - glowRadius, glowRadius * 2, glowRadius * 2),
      rarity * (0.10f + tier * 0.03f + hover * 0.26f));

    var hexBounds = new Rectangle(iconCenter.X - 72, iconCenter.Y - 72, 144, 144);
    var hexUnderlay = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_hud_hex_underlay");
    var hexRing = GuiSkin.GetTexture(batch.GraphicsDevice, "synty_hud_ring_hex_med");
    Color hexFill = Color.Lerp(
      Color.Lerp(GuiPalette.Base(new Color(8, 24, 42, 228)), rarity, 0.08f + tier * 0.04f),
      Color.Lerp(GuiPalette.Base(new Color(16, 46, 74, 244)), rarity, 0.34f),
      hover);
    Color hexRim = Color.Lerp(
      Color.Lerp(rarity, Color.White, 0.16f) * (0.82f + tier * 0.04f),
      Color.Lerp(rarity, Color.White, 0.54f),
      hover);
    batch.Draw(hexUnderlay, hexBounds, hexFill);
    batch.Draw(hexRing, hexBounds, hexRim);

    if (revealed && tier >= 2)
    {
      float innerHexAlpha = 0.45f + (tier - 2) * 0.15f + hover * 0.25f;
      batch.Draw(hexRing, new Rectangle(iconCenter.X - 58, iconCenter.Y - 58, 116, 116),
        Color.Lerp(rarity, Color.White, 0.35f + hover * 0.25f) * Math.Min(0.96f, innerHexAlpha));
    }

    if (revealed && tier >= 3)
    {
      ReadOnlySpan<float> sideAngles = tier == 4 ? [-0.38f, 0f, 0.38f] : [0f];
      for (int side = -1; side <= 1; side += 2)
      {
        for (int k = 0; k < sideAngles.Length; k++)
        {
          float baseAngle = (side < 0 ? MathF.PI : 0f) + sideAngles[k] * side;
          float bob = MathF.Sin(time * 1.8f + k * 1.2f + side) * 0.02f;
          float radius = sideAngles.Length == 1 || k == 1 ? 122f : 118f;
          var pos = iconCenterVec + new Vector2(MathF.Cos(baseAngle + bob), MathF.Sin(baseAngle + bob)) * radius;
          int gemSz = sideAngles.Length == 1 || k == 1 ? 16 : 13;
          DrawSignalHexGem(batch, new Point((int)MathF.Round(pos.X), (int)MathF.Round(pos.Y)), gemSz, rarity, pulse + hover * 0.35f);
        }
      }
    }

    if (revealed)
    {
      // Framed Value Plaque on ALL rarities (sits cleanly above detail_07's bottom horizontal rail)
      Color plaqueFill = Color.Lerp(GuiPalette.Base(new Color(6, 16, 30, 246)), rarity, 0.04f + tier * 0.025f + hover * 0.08f);
      float plaqueBorderOpacity = Math.Min(1f, 0.62f + tier * 0.08f + hover * 0.32f);
      Color plaqueAccent = Color.Lerp(rarity, Color.White, hover * 0.28f);
      var valPlaque = new Rectangle(card.Center.X - 128, card.Y + 362, 256, 62);
      if (hover > 0.01f)
      {
        batch.Draw(glowTex, new Rectangle(valPlaque.X - 24, valPlaque.Y - 14, valPlaque.Width + 48, valPlaque.Height + 28),
          rarity * (hover * 0.28f));
      }
      DrawSignalInsetPlaque(batch, valPlaque, plaqueFill, plaqueAccent, plaqueBorderOpacity, doubleBorder: tier == 4);

      // Framed Footer Rarity Plaque on ALL rarities
      DrawSignalInsetPlaque(batch, new Rectangle(card.Center.X - 132, card.Bottom - 104, 264, 38),
        plaqueFill, plaqueAccent, Math.Min(1f, 0.56f + tier * 0.095f + hover * 0.32f), doubleBorder: tier == 4);

      // Mid-card Rarity Bridge (sits cleanly in the channel between upperBay.Bottom and descBox.Top)
      int gemCount = tier + 1;
      const int gemSpacing = 38;
      int startGemX = card.Center.X - (gemCount - 1) * gemSpacing / 2;
      int endGemX = startGemX + (gemCount - 1) * gemSpacing;
      int leftLineW = Math.Max(0, (startGemX - 24) - (descBox.X + 12));
      if (leftLineW > 0)
      {
        GuiSkin.TechDivider(batch, new Rectangle(descBox.X + 12, bridgeY - 1, leftLineW, 3), rimColor * (0.72f + hover * 0.24f));
        GuiSkin.TechDivider(batch, new Rectangle(endGemX + 24, bridgeY - 1, leftLineW, 3), rimColor * (0.72f + hover * 0.24f));
      }
      for (int g = 0; g < gemCount; g++)
      {
        DrawSignalHexGem(batch, new Point(startGemX + g * gemSpacing, bridgeY), 24, rarity, pulse + hover * 0.40f);
      }
    }

    batch.End();
  }

  private void DrawSignals(SpriteBatch batch)
  {
    var panel = SignalPanel;
    if (!signalChoicesVisible)
    {
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      GuiSkin.Panel(batch, panel, header: true, SignalAccent);
      var gridArea = new Rectangle(panel.X + 24, panel.Y + GuiSkin.PanelHeaderHeight + 16,
        panel.Width - 48, panel.Height - GuiSkin.PanelHeaderHeight - 176);
      GuiSkin.DrawTiled(batch, "synty_hud_crosses", gridArea, 192, 192, 0, 0, SignalAccent * 0.12f);
      GuiSkin.DrawScannerReticle(batch, new Point(panel.Center.X, panel.Center.Y + 24), 640, SignalAccent, 0.38f);
      GuiSkin.TechDivider(batch, new Rectangle(panel.Center.X - 460, panel.Y + 194, 920, 4), SignalAccent * 0.65f);
      batch.End();
      string heading = Loc.T("Discovered signals");
      SignalLabel(heading, panel.Center.X,
        panel.Y + (GuiSkin.PanelHeaderHeight - Measure2(heading, Vector2.Zero, 40).Y) / 2,
        40, SignalAccent);
      SignalLabel(Loc.T("Repeated discoveries add stacks to the same signal. Hover to inspect."),
        panel.Center.X, panel.Y + 146, 26, GuiSkin.MutedTextColor);
    }

    if (signalChoicesVisible)
    {
      float dt = Math.Clamp((float)BaseGame.Time.ElapsedGameTime.TotalSeconds, 0.001f, 0.1f);
      for (int i = 0; i < 3; i++)
      {
        int id = Signals.PendingChoices[i].Signal;
        var baseCard = SignalCard(i);
        float age = SignalRevealAge(i);
        int rarityIndex = Signals.PendingChoices[i].Rarity;
        var rarity = SignalRarity(rarityIndex);
        bool revealed = age >= 0.3f;
        bool hovered = age >= 0.65f && baseCard.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y);
        signalHoverAmounts[i] = hovered
          ? Math.Min(1f, signalHoverAmounts[i] + dt * 10f)
          : Math.Max(0f, signalHoverAmounts[i] - dt * 8f);
        float hover = signalHoverAmounts[i];
        int liftY = (int)MathF.Round(hover * 10f);
        var card = new Rectangle(baseCard.X, baseCard.Y - liftY, baseCard.Width, baseCard.Height);

        DrawSignalCardFrame(batch, card, rarityIndex, revealed ? rarity.Color : GuiSkin.BorderColor, revealed, hover);
        if (!revealed)
        {
          SignalLabel(Loc.T("DECODING SIGNAL..."), card.Center.X, card.Center.Y, 28, GuiSkin.MutedTextColor);
          continue;
        }
        DrawSignalRevealBurst(batch, i, card, rarityIndex, age);
        DrawSignalGpuParticles(batch, i, card);
        Color baseTitle = rarityIndex == 4
          ? Color.Lerp(GuiSkin.StatHeadingColor, rarity.Color, 0.35f)
          : GuiSkin.StatHeadingColor;
        Color titleColor = Color.Lerp(baseTitle, Color.White, hover * 0.45f);
        Color categoryColor = rarityIndex >= 2
          ? Color.Lerp(GuiSkin.MutedTextColor, rarity.Color, 0.35f + hover * 0.25f)
          : Color.Lerp(GuiSkin.MutedTextColor, Color.White, hover * 0.35f);
        Color rarityLabelColor = rarityIndex >= 3
          ? Color.Lerp(rarity.Color, Color.White, 0.32f + hover * 0.25f)
          : Color.Lerp(rarity.Color, Color.White, hover * 0.35f);
        Color valueColor = Color.Lerp(GuiSkin.StatValueColor, Color.White, hover * 0.35f);

        SignalCardLabel(Loc.T(SignalPreviews[id].Name), card, card.Y + 28, 38, titleColor);
        SignalCardLabel(Loc.T(SignalPreviews[id].Category), card, card.Y + 116, 22, categoryColor);
        SignalCardLabel(SignalRarityLabel(rarityIndex), card, card.Bottom - 96, 22, rarityLabelColor);
        QueueSignalIcon(id, SignalChoiceIconCenter(card), 84);
        SignalCardLabel($"{(SignalPreviews[id].Reduction ? "-" : "+")}{SignalProgression.BonusForRarity(id, rarityIndex):0.##}%",
          card, card.Y + 363, 52, valueColor);
        float descriptionY = card.Y + 534;
        string descriptionLine = "";
        int descMaxWidth = card.Width - 136;
        foreach (var (word, join) in Loc.WrapPieces(SignalPreviews[id].Description))
        {
          string next = descriptionLine.Length == 0 ? word : descriptionLine + join + word;
          if (descriptionLine.Length > 0 && Measure2(next, Vector2.Zero, 34).X > descMaxWidth)
          {
            SignalCardLabel(descriptionLine, card, descriptionY, 34, GuiSkin.StatHeadingColor);
            descriptionY += 46;
            descriptionLine = word;
          }
          else descriptionLine = next;
        }
        SignalCardLabel(descriptionLine, card, descriptionY, 34, GuiSkin.StatHeadingColor);
        if (SignalPreviews[id].Reduction)
          SignalCardLabel(Loc.T("Applied to the remaining cooldown"), card, descriptionY + 52, 24, GuiSkin.MutedTextColor);
        double currentBonus = Signals.BonusPercent(id);
        double addedBonus = SignalProgression.BonusForRarity(id, rarityIndex);
        double nextBonus = SignalPreviews[id].Reduction
          ? 100 - (100 - currentBonus) * (1 - addedBonus / 100)
          : currentBonus + addedBonus;
        string sign = SignalPreviews[id].Reduction ? "-" : "+";
        SignalCardLabel(Loc.F("Total effect: {0}{1:0.##}% -> {0}{2:0.##}%", sign, currentBonus, nextBonus),
          card, card.Bottom - 188, 26, GuiSkin.StatHeadingColor);
        SignalCardLabel(Loc.F("Discoveries: {0} -> {1}", Signals.StackCount(id), Signals.StackCount(id) + 1),
          card, card.Bottom - 152, 22, GuiSkin.MutedTextColor);
      }
      FlushIcons(batch, SamplerState.LinearClamp);
    }
    else DrawSignalCollection(batch);

    if (signalChoicesVisible) return;

    var scan = SignalScanButton;
    ulong? cost = Signals.ScanCost;
    bool canScan = !signalChoicesVisible && cost is ulong price
      && UntitledGemGameGameScreen.Instance.State.CurrentRedGemCount >= price;
    Color scanColor = signalChoicesVisible ? GuiSkin.MutedTextColor
      : canScan ? GuiSkin.ButtonTextColor : GuiSkin.LockedTextColor;
    string scanLabel = signalChoicesVisible ? Loc.T("Choose a discovery")
      : canScan ? Loc.T("Scan") : cost.HasValue ? Loc.T("Not enough gems") : Loc.T("Scan unavailable");
    DrawHudButton(batch, scan, scanLabel, scanColor,
      !signalChoicesVisible, canScan && scan.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y), 0);
    if (!signalChoicesVisible && cost is ulong amount)
    {
      string label = NumberFormatter.AbbreviateBigNumber(amount);
      const float fontSize = 22, iconSpace = 44;
      var measure = Measure2(label, Vector2.Zero, fontSize);
      float left = panel.Center.X - (iconSpace + measure.X) / 2;
      float top = panel.Bottom - 50;
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      UntitledGemGameGameScreen.Instance.DrawHudRedGem(batch, new Vector2(left + 16, top + measure.Y / 2));
      batch.End();
      FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
        label, new Vector2(left + iconSpace, top), scanColor, Color.Black, fontSize);
    }
    else
      SignalLabel(signalChoicesVisible ? Loc.T("Scan paid - choose one permanent enhancement") : Loc.T("Scan cost exceeds the currency limit"),
        panel.Center.X, panel.Bottom - 50, 22, GuiSkin.MutedTextColor);
  }
}
