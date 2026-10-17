using System;
using AsyncContent;
using Gum;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  private static Color SignalAccent => OrbitSkin.PanelAccent;
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
    if (!UpgradeManager.Instance.UGM.SignalsUnlocked || !GameInput.Mouse.WasButtonPressed(MouseButton.Left)) return;
    var cursor = GameInput.UiCursor;
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
        signalRevealStarted = BaseGame.Time.TotalGameTime.TotalSeconds;
      return;
    }
    if (!signalChoicesVisible) return;
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
      OrbitSkin.TechCard(batch, tile, hovered ? GuiPalette.Base(new Color(14, 34, 58, 244)) : GuiPalette.Base(new Color(8, 22, 42, 238)),
        hovered ? Color.White : SignalAccent, hovered, hovered, cornerGraphic: true, hexWatermark: false);
      OrbitSkin.DrawHexSocket(batch, new Rectangle(tile.Center.X - 70, tile.Y + 40, 140, 140), SignalAccent, active: hovered);
      OrbitSkin.TechDivider(batch, new Rectangle(tile.X + 36, tile.Bottom - 122, tile.Width - 72, 3), SignalAccent * 0.60f);
      batch.End();
      QueueSignalIcon(id, new Vector2(tile.Center.X, tile.Y + 110), 96);
      SignalLabel(Loc.T(SignalPreviews[id].Name), tile.Center.X, tile.Bottom - 108, 26, OrbitSkin.ButtonTextColor);
      SignalLabel($"x{Signals.StackCount(id)}", tile.Center.X, tile.Bottom - 62, 32, SignalAccent);
      if (hovered) { hoveredId = id; hoveredTile = tile; }
    }
    if (visible == 0)
    {
      SignalLabel(Loc.T("No signals discovered yet"), SignalPanel.Center.X, SignalPanel.Center.Y - 30, 36, OrbitSkin.ButtonTextColor);
      SignalLabel(Loc.T("Scan deep space and choose your first enhancement."), SignalPanel.Center.X, SignalPanel.Center.Y + 36, 26, OrbitSkin.MutedTextColor);
    }
    if (SignalCollectionPages > 1)
    {
      var cursor = GameInput.UiCursor;
      DrawHudButton(batch, SignalPreviousPage, Loc.T("Previous"), SignalAccent, false,
        signalCollectionPage > 0 && SignalPreviousPage.Contains(cursor.X, cursor.Y), 0);
      DrawHudButton(batch, SignalNextPage, Loc.T("Next"), SignalAccent, false,
        signalCollectionPage + 1 < SignalCollectionPages && SignalNextPage.Contains(cursor.X, cursor.Y), 0);
      SignalLabel($"{signalCollectionPage + 1} / {SignalCollectionPages}", SignalPanel.Center.X,
        SignalPreviousPage.Y + 12, 24, OrbitSkin.MutedTextColor);
    }
    FlushIcons(batch, SamplerState.LinearClamp);
    if (hoveredId < 0) return;
    int x = Math.Clamp(hoveredTile.Center.X - 480, SignalPanel.Left + 24, SignalPanel.Right - 984);
    int y = Math.Min(hoveredTile.Bottom + 20, SignalScanButton.Top - 270);
    var tooltip = new Rectangle(x, y, 960, 250);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Tooltip(batch, tooltip);
    OrbitSkin.TechDivider(batch, new Rectangle(tooltip.X + 48, tooltip.Y + 72, tooltip.Width - 96, 3), SignalAccent * 0.65f);
    batch.End();
    SignalLabel(Loc.T(SignalPreviews[hoveredId].Name), tooltip.Center.X, y + 22, 32, SignalAccent);
    SignalLabel(SignalPreviews[hoveredId].Description, tooltip.Center.X, y + 84, 24, OrbitSkin.ButtonTextColor);
    SignalLabel(SignalTotal(hoveredId), tooltip.Center.X, y + 132, 26, OrbitSkin.StatValueColor);
    SignalLabel(Loc.P(Signals.StackCount(hoveredId), "{0} discovery combined", "{0} discoveries combined"),
      tooltip.Center.X, y + 190, 22, OrbitSkin.MutedTextColor);
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

  private static Vector2 SignalChoiceIconCenter(Rectangle card) => new(card.Center.X, card.Y + 260);

  private void DrawSignalRevealBurst(SpriteBatch batch, Rectangle card, int rarity, float age)
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
    for (int i = 0; i < count; i++)
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

  private void DrawSignalCardFrame(SpriteBatch batch, Rectangle card, Color rarity, bool hovered)
  {
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    Color fill = hovered ? GuiPalette.Base(new Color(14, 34, 58, 246)) : GuiPalette.Base(new Color(7, 20, 38, 242));
    OrbitSkin.TechCard(batch, card, fill, rarity, hovered, hovered, cornerGraphic: true, hexWatermark: false);

    var iconCenter = new Point(card.Center.X, card.Y + 260);
    OrbitSkin.DrawScannerReticle(batch, iconCenter, 244, rarity, hovered ? 0.50f : 0.35f);
    OrbitSkin.DrawHexSocket(batch, new Rectangle(iconCenter.X - 92, iconCenter.Y - 92, 184, 184), rarity, active: hovered);

    OrbitSkin.TechDivider(batch, new Rectangle(card.X + 36, card.Y + 94, card.Width - 72, 4), rarity * 0.85f);
    foreach (int y in new[] { card.Y + 474, card.Bottom - 164 })
      OrbitSkin.TechDivider(batch, new Rectangle(card.X + 48, y, card.Width - 96, 3), rarity * 0.68f);
    batch.End();
  }

  private void DrawSignals(SpriteBatch batch)
  {
    var panel = SignalPanel;
    if (!signalChoicesVisible)
    {
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      OrbitSkin.Panel(batch, panel, header: true, SignalAccent);
      var gridArea = new Rectangle(panel.X + 24, panel.Y + OrbitSkin.PanelHeaderHeight + 16,
        panel.Width - 48, panel.Height - OrbitSkin.PanelHeaderHeight - 176);
      OrbitSkin.DrawTiled(batch, "synty_hud_crosses", gridArea, 192, 192, 0, 0, SignalAccent * 0.12f);
      OrbitSkin.DrawScannerReticle(batch, new Point(panel.Center.X, panel.Center.Y + 24), 640, SignalAccent, 0.38f);
      OrbitSkin.TechDivider(batch, new Rectangle(panel.Center.X - 460, panel.Y + 194, 920, 4), SignalAccent * 0.65f);
      batch.End();
      string heading = Loc.T("Discovered signals");
      SignalLabel(heading, panel.Center.X,
        panel.Y + (OrbitSkin.PanelHeaderHeight - Measure2(heading, Vector2.Zero, 40).Y) / 2,
        40, SignalAccent);
      SignalLabel(Loc.T("Repeated discoveries add stacks to the same signal. Hover to inspect."),
        panel.Center.X, panel.Y + 146, 26, OrbitSkin.MutedTextColor);
    }

    if (signalChoicesVisible)
    {
      for (int i = 0; i < 3; i++)
      {
        int id = Signals.PendingChoices[i].Signal;
        var card = SignalCard(i);
        float age = SignalRevealAge(i);
        int rarityIndex = Signals.PendingChoices[i].Rarity;
        var rarity = SignalRarity(rarityIndex);
        bool revealed = age >= 0.3f;
        bool hovered = age >= 0.65f && card.Contains(GameInput.UiCursor.X, GameInput.UiCursor.Y);
        DrawSignalCardFrame(batch, card, revealed ? rarity.Color : OrbitSkin.BorderColor, hovered);
        if (!revealed)
        {
          SignalLabel(Loc.T("DECODING SIGNAL..."), card.Center.X, card.Center.Y, 28, OrbitSkin.MutedTextColor);
          continue;
        }
        DrawSignalRevealBurst(batch, card, rarityIndex, age);
        SignalCardLabel(Loc.T(SignalPreviews[id].Name), card, card.Y + 24, 42, OrbitSkin.StatHeadingColor);
        SignalCardLabel(Loc.T(SignalPreviews[id].Category), card, card.Y + 110, 24, OrbitSkin.MutedTextColor);
        SignalCardLabel(SignalRarityLabel(rarityIndex), card, card.Bottom - 60, 28, rarity.Color);
        QueueSignalIcon(id, SignalChoiceIconCenter(card), 160);
        SignalCardLabel($"{(SignalPreviews[id].Reduction ? "-" : "+")}{SignalProgression.BonusForRarity(id, rarityIndex):0.##}%", card, card.Y + 364, 64, OrbitSkin.StatValueColor);
        float descriptionY = card.Y + 546;
        string descriptionLine = "";
        foreach (var (word, join) in Loc.WrapPieces(SignalPreviews[id].Description))
        {
          string next = descriptionLine.Length == 0 ? word : descriptionLine + join + word;
          if (descriptionLine.Length > 0 && Measure2(next, Vector2.Zero, 34).X > card.Width - 80)
          {
            SignalCardLabel(descriptionLine, card, descriptionY, 34, OrbitSkin.StatHeadingColor);
            descriptionY += 48;
            descriptionLine = word;
          }
          else descriptionLine = next;
        }
        SignalCardLabel(descriptionLine, card, descriptionY, 34, OrbitSkin.StatHeadingColor);
        if (SignalPreviews[id].Reduction)
          SignalCardLabel(Loc.T("Applied to the remaining cooldown"), card, descriptionY + 56, 24, OrbitSkin.MutedTextColor);
        double currentBonus = Signals.BonusPercent(id);
        double addedBonus = SignalProgression.BonusForRarity(id, rarityIndex);
        double nextBonus = SignalPreviews[id].Reduction
          ? 100 - (100 - currentBonus) * (1 - addedBonus / 100)
          : currentBonus + addedBonus;
        string sign = SignalPreviews[id].Reduction ? "-" : "+";
        SignalCardLabel(Loc.F("Total effect: {0}{1:0.##}% -> {0}{2:0.##}%", sign, currentBonus, nextBonus),
          card, card.Bottom - 132, 26, OrbitSkin.StatHeadingColor);
        SignalCardLabel(Loc.F("Discoveries: {0} -> {1}", Signals.StackCount(id), Signals.StackCount(id) + 1),
          card, card.Bottom - 94, 22, OrbitSkin.MutedTextColor);
      }
      FlushIcons(batch, SamplerState.LinearClamp);
    }
    else DrawSignalCollection(batch);

    if (signalChoicesVisible) return;

    var scan = SignalScanButton;
    ulong? cost = Signals.ScanCost;
    bool canScan = !signalChoicesVisible && cost is ulong price
      && UntitledGemGameGameScreen.Instance.State.CurrentRedGemCount >= price;
    Color scanColor = signalChoicesVisible ? OrbitSkin.MutedTextColor
      : canScan ? OrbitSkin.ButtonTextColor : OrbitSkin.LockedTextColor;
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
        panel.Center.X, panel.Bottom - 50, 22, OrbitSkin.MutedTextColor);
  }
}
