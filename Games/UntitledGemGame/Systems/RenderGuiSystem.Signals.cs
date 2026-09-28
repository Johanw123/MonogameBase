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
  private static readonly Color SignalAccent = new(125, 235, 210);
  private readonly Random signalRandom = new();
  private SignalProgression Signals => UpgradeManager.Instance.Signals;
  private bool signalChoicesVisible => Signals.PendingChoices.Count == 3;
  private double signalRevealStarted;
  private static readonly (string Name, Color Color)[] SignalRarities =
  [
    ("COMMON", OrbitSkin.CommonRarity),
    ("UNCOMMON", OrbitSkin.UncommonRarity),
    ("RARE", OrbitSkin.RareRarity),
    ("EPIC", OrbitSkin.EpicRarity),
    ("LEGENDARY", OrbitSkin.LegendaryRarity)
  ];
  private readonly AsyncAsset<Texture2D>[] signalIcons = new AsyncAsset<Texture2D>[SignalProgression.SignalCount];
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

  private static Rectangle SignalPanel => new(120, 196, HudLayout.Width - 240, HudLayout.Top - 240);
  private static Rectangle SignalScanButton => new(HudLayout.Width / 2 - 190, SignalPanel.Bottom - 132, 380, 72);
  private static Rectangle SignalCard(int index)
  {
    var panel = SignalPanel;
    int width = (panel.Width - 192) / 3;
    return new Rectangle(panel.X + 64 + index * (width + 32), panel.Y + 240, width, Math.Min(1040, panel.Height - 460));
  }

  private void UpdateSignalsInput()
  {
    if (!UpgradeManager.Instance.UGM.SignalsUnlocked || !MouseExtended.GetState().WasButtonPressed(MouseButton.Left)) return;
    var cursor = GumService.Default.Cursor;
    if (HudLayout.NavigationButton(3).Contains(cursor.X, cursor.Y))
    {
      SetUpgradeType(m_upgradeWindowType == UpgradeTypes.Signals ? UpgradeTypes.None : UpgradeTypes.Signals);
      return;
    }
    if (m_upgradeWindowType != UpgradeTypes.Signals) return;
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
    var bounds = HudLayout.NavigationButton(3);
    bool selected = m_upgradeWindowType == UpgradeTypes.Signals;
    DrawHudButton(batch, bounds, selected ? "Hide" : "Signals", SignalAccent,
      selected, bounds.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y), 0, tab: true);
  }

  private void SignalLabel(string text, float centerX, float y, float size, Color color)
  {
    var measure = Measure2(text, Vector2.Zero, size);
    FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
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
    => $"Total: {(SignalPreviews[id].Reduction ? "-" : "+")}{Signals.BonusPercent(id):0.##}% {SignalPreviews[id].Target}";

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
      bool hovered = tile.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
      DrawHudButton(batch, tile, "", SignalAccent, false, hovered, 0);
      DrawSignalIcon(batch, id, new Vector2(tile.Center.X, tile.Y + 110), 96);
      SignalLabel(SignalPreviews[id].Name, tile.Center.X, tile.Bottom - 108, 26, OrbitSkin.ButtonTextColor);
      SignalLabel($"x{Signals.StackCount(id)}", tile.Center.X, tile.Bottom - 62, 32, SignalAccent);
      if (hovered) { hoveredId = id; hoveredTile = tile; }
    }
    if (visible == 0)
    {
      SignalLabel("No signals discovered yet", SignalPanel.Center.X, SignalPanel.Center.Y - 30, 36, OrbitSkin.ButtonTextColor);
      SignalLabel("Scan deep space and choose your first enhancement.", SignalPanel.Center.X, SignalPanel.Center.Y + 36, 26, OrbitSkin.MutedTextColor);
    }
    if (SignalCollectionPages > 1)
    {
      var cursor = GumService.Default.Cursor;
      DrawHudButton(batch, SignalPreviousPage, "Previous", SignalAccent, false,
        signalCollectionPage > 0 && SignalPreviousPage.Contains(cursor.X, cursor.Y), 0);
      DrawHudButton(batch, SignalNextPage, "Next", SignalAccent, false,
        signalCollectionPage + 1 < SignalCollectionPages && SignalNextPage.Contains(cursor.X, cursor.Y), 0);
      SignalLabel($"{signalCollectionPage + 1} / {SignalCollectionPages}", SignalPanel.Center.X,
        SignalPreviousPage.Y + 12, 24, OrbitSkin.MutedTextColor);
    }
    if (hoveredId < 0) return;
    int x = Math.Clamp(hoveredTile.Center.X - 480, SignalPanel.Left + 24, SignalPanel.Right - 984);
    int y = Math.Min(hoveredTile.Bottom + 20, SignalScanButton.Top - 270);
    var tooltip = new Rectangle(x, y, 960, 250);
    batch.Begin();
    batch.Draw(AssetManager.DefaultTexture, tooltip, SignalAccent);
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(x + 3, y + 3, 954, 244), OrbitSkin.PanelColor);
    batch.End();
    SignalLabel(SignalPreviews[hoveredId].Name, tooltip.Center.X, y + 26, 32, SignalAccent);
    SignalLabel(SignalPreviews[hoveredId].Description, tooltip.Center.X, y + 84, 24, OrbitSkin.ButtonTextColor);
    SignalLabel(SignalTotal(hoveredId), tooltip.Center.X, y + 132, 26, SignalAccent);
    SignalLabel($"{Signals.StackCount(hoveredId)} discoveries combined",
      tooltip.Center.X, y + 190, 22, OrbitSkin.MutedTextColor);
  }

  private void DrawSignalIcon(SpriteBatch batch, int id, Vector2 center, float size)
  {
    var asset = signalIcons[id] ??= AssetManager.LoadAsync<Texture2D>(SignalPreviews[id].Icon);
    if (!asset.IsLoaded) return;
    var texture = asset.Value;
    float scale = size / Math.Max(texture.Width, texture.Height);
    batch.Begin(samplerState: SamplerState.LinearClamp);
    batch.Draw(texture, center, null, Color.White, 0,
      new Vector2(texture.Width, texture.Height) / 2, scale, SpriteEffects.None, 0);
    batch.End();
  }

  private float SignalRevealAge(int index)
    => (float)(BaseGame.Time.TotalGameTime.TotalSeconds - signalRevealStarted) - index * 0.22f;

  private void DrawSignalRarityEffect(SpriteBatch batch, Rectangle card, int rarity, float age)
  {
    var color = SignalRarities[rarity].Color;
    float time = (float)BaseGame.Time.TotalGameTime.TotalSeconds;
    float reveal = Math.Clamp(age / 0.65f, 0, 1);
    float pulse = 0.5f + 0.5f * MathF.Sin(time * 2.4f);
    batch.Begin(blendState: Microsoft.Xna.Framework.Graphics.BlendState.Additive);
    // Layered edge light, with stronger bloom for the higher tiers.
    for (int layer = 5; layer >= 1; layer--)
    {
      int width = layer * (2 + rarity);
      float opacity = (0.025f + rarity * 0.012f + pulse * 0.015f) * reveal;
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.X, card.Y, card.Width, width), color * opacity);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.X, card.Bottom - width, card.Width, width), color * opacity);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.X, card.Y, width, card.Height), color * opacity);
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.Right - width, card.Y, width, card.Height), color * opacity);
    }
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.X + 4, card.Y + 4, card.Width - 8, 8), color * reveal);
    if (age >= 0 && age < 0.9f)
    {
      int sweepY = card.Y + (int)((card.Height - 80) * Math.Clamp(age / 0.9f, 0, 1));
      batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.X + 6, sweepY + 6, card.Width - 12, 68),
        color * (0.22f * (1 - age / 0.9f)));
    }
    if (rarity >= 2)
    {
      // Orbiting motes and a brief outward burst frame the rare discovery.
      var center = new Vector2(card.Center.X, card.Y + card.Height * 0.32f);
      int count = 8 + rarity * 4;
      for (int i = 0; i < count; i++)
      {
        float angle = MathF.Tau * i / count + time * 0.18f;
        float burst = age < 1.4f ? Math.Clamp(age, 0, 1.4f) * 75 : 105;
        var pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Math.Min(65 + burst, Math.Min(card.Width * 0.18f, card.Height * 0.12f));
        float sparkle = 0.35f + 0.65f * MathF.Abs(MathF.Sin(time * 2 + i * 1.7f));
        int size = rarity == 4 ? 7 : 4;
        batch.Draw(AssetManager.DefaultTexture, new Rectangle((int)pos.X, (int)pos.Y, size, size), color * (sparkle * reveal));
      }
    }
    batch.End();
  }

  private void DrawSignalCardFrame(SpriteBatch batch, Rectangle card, Color rarity, bool hovered)
  {
    batch.Begin();
    void Line(Vector2 from, Vector2 to, Color color, float thickness = 2)
    {
      var delta = to - from;
      batch.Draw(AssetManager.DefaultTexture, from, null, color,
        MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(delta.Length(), thickness), SpriteEffects.None, 0);
    }
    var edge = hovered ? rarity : rarity * .45f;
    // Rectangles share an outer edge so stroke direction cannot offset the brackets.
    const int inset = 14, topInset = 30, bottomInset = 18, bracketLength = 54;
    foreach (int side in new[] { -1, 1 })
    {
      int outerX = side < 0 ? card.Left + inset : card.Right - inset;
      int top = card.Top + topInset, bottom = card.Bottom - bottomInset;
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(side < 0 ? outerX : outerX - 2, top, 2, bottom - top), edge);
      foreach (int end in new[] { -1, 1 })
      {
        int horizontalY = end < 0 ? top : bottom - 4;
        int verticalY = end < 0 ? top : bottom - bracketLength;
        batch.Draw(AssetManager.DefaultTexture,
          new Rectangle(side < 0 ? outerX : outerX - bracketLength, horizontalY, bracketLength, 4), rarity);
        batch.Draw(AssetManager.DefaultTexture,
          new Rectangle(side < 0 ? outerX : outerX - 4, verticalY, 4, bracketLength), rarity);
      }
    }
    // A technical diamond housing around the icon, with circuit traces to the rails.
    var center = new Vector2(card.Center.X, card.Y + card.Height * .32f);
    const float radius = 112;
    batch.Draw(AssetManager.DefaultTexture, center, null, new Color(8, 18, 28), MathHelper.PiOver4,
      new Vector2(.5f), new Vector2(radius * 1.4142f), SpriteEffects.None, 0);
    for (int i = 0; i < 4; i++)
    {
      float angle = i * MathHelper.PiOver2;
      var a = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
      var b = center + new Vector2(MathF.Cos(angle + MathHelper.PiOver2), MathF.Sin(angle + MathHelper.PiOver2)) * radius;
      Line(a, b, rarity * (hovered ? 1 : .65f), 3);
    }
    foreach (int side in new[] { -1, 1 })
    {
      var a = center + new Vector2(side * 122, 0);
      var b = center + new Vector2(side * 174, 38);
      Line(a, b, edge);
      Line(b, new Vector2(card.Center.X + side * (card.Width / 2 - 36), b.Y), edge);
      for (int i = 0; i < 3; i++)
        batch.Draw(AssetManager.DefaultTexture, new Rectangle((int)b.X + side * i * 16, (int)b.Y + 12, 6, 6), edge);
    }
    Line(new Vector2(card.X + 48, card.Bottom - 214), new Vector2(card.Right - 48, card.Bottom - 214), edge);
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(card.X + 28, card.Bottom - 104, card.Width - 56, 76),
      rarity * (hovered ? .16f : .05f));
    batch.End();
  }

  private void DrawSignals(SpriteBatch batch)
  {
    var panel = SignalPanel;
    batch.Begin();
    OrbitSkin.Panel(batch, panel, header: true);
    batch.End();

    string heading = signalChoicesVisible ? "Choose a discovery" : "Discovered signals";
    SignalLabel(heading, panel.Center.X,
      panel.Y + (OrbitSkin.PanelHeaderHeight - Measure2(heading, Vector2.Zero, 40).Y) / 2,
      40, OrbitSkin.ButtonTextColor);
    SignalLabel(signalChoicesVisible ? "Choose one enhancement to add to your collection." : "Repeated discoveries add stacks to the same signal. Hover to inspect.",
      panel.Center.X, panel.Y + 146, 26, OrbitSkin.MutedTextColor);

    if (signalChoicesVisible)
    {
      for (int i = 0; i < 3; i++)
      {
        int id = Signals.PendingChoices[i].Signal;
        var card = SignalCard(i);
        float age = SignalRevealAge(i);
        int rarityIndex = Signals.PendingChoices[i].Rarity;
        var rarity = SignalRarities[rarityIndex];
        bool revealed = age >= 0.3f;
        bool hovered = age >= 0.65f && card.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
        DrawHudButton(batch, card, "", revealed ? rarity.Color : OrbitSkin.ButtonBorderColor, false, hovered, 0);
        if (!revealed)
        {
          SignalLabel("DECODING SIGNAL...", card.Center.X, card.Center.Y, 28, OrbitSkin.MutedTextColor);
          continue;
        }
        DrawSignalRarityEffect(batch, card, rarityIndex, age);
        DrawSignalCardFrame(batch, card, rarity.Color, hovered);
        SignalLabel(SignalPreviews[id].Name, card.Center.X, card.Y + 38, 48, OrbitSkin.StatHeadingColor);
        SignalLabel(SignalPreviews[id].Category + "  /  " + rarity.Name,
          card.Center.X, card.Y + 108, 28, rarity.Color);
        DrawSignalIcon(batch, id, new Vector2(card.Center.X, card.Y + card.Height * 0.32f), 120);
        SignalLabel($"{(SignalPreviews[id].Reduction ? "-" : "+")}{SignalProgression.BonusForRarity(rarityIndex):0.##}%", card.Center.X, card.Y + card.Height * 0.47f, 80, rarity.Color);
        float descriptionY = card.Y + card.Height * 0.59f;
        string descriptionLine = "";
        foreach (string word in SignalPreviews[id].Description.Split(' '))
        {
          string next = descriptionLine.Length == 0 ? word : descriptionLine + " " + word;
          if (descriptionLine.Length > 0 && Measure2(next, Vector2.Zero, 38).X > card.Width - 96)
          {
            SignalLabel(descriptionLine, card.Center.X, descriptionY, 38, OrbitSkin.StatHeadingColor);
            descriptionY += 48;
            descriptionLine = word;
          }
          else descriptionLine = next;
        }
        SignalLabel(descriptionLine, card.Center.X, descriptionY, 38, OrbitSkin.StatHeadingColor);
        if (SignalPreviews[id].Reduction)
          SignalLabel("Applied to the remaining cooldown", card.Center.X, descriptionY + 56, 26, OrbitSkin.MutedTextColor);
        double currentBonus = Signals.BonusPercent(id);
        double addedBonus = SignalProgression.BonusForRarity(rarityIndex);
        double nextBonus = SignalPreviews[id].Reduction
          ? 100 - (100 - currentBonus) * (1 - addedBonus / 100)
          : currentBonus + addedBonus;
        string sign = SignalPreviews[id].Reduction ? "-" : "+";
        SignalLabel($"Total effect: {sign}{currentBonus:0.##}% -> {sign}{nextBonus:0.##}%",
          card.Center.X, card.Bottom - 196, 32, OrbitSkin.StatHeadingColor);
        SignalLabel($"Discoveries: {Signals.StackCount(id)} -> {Signals.StackCount(id) + 1}", card.Center.X, card.Bottom - 148, 26, rarity.Color);
        SignalLabel(hovered ? "Click to choose this discovery" : "Choose this discovery", card.Center.X,
          card.Bottom - 86, 32, hovered ? Color.White : OrbitSkin.ButtonTextColor);
      }
    }
    else DrawSignalCollection(batch);

    var scan = SignalScanButton;
    ulong? cost = Signals.ScanCost;
    bool canScan = !signalChoicesVisible && cost is ulong price
      && UntitledGemGameGameScreen.Instance.State.CurrentRedGemCount >= price;
    Color scanColor = signalChoicesVisible ? OrbitSkin.MutedTextColor
      : canScan ? SignalAccent : new Color(235, 125, 135);
    string scanLabel = signalChoicesVisible ? "Choose a discovery"
      : canScan ? "Scan" : cost.HasValue ? "Not enough gems" : "Scan unavailable";
    DrawHudButton(batch, scan, scanLabel, scanColor,
      !signalChoicesVisible, canScan && scan.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y), 0);
    if (!signalChoicesVisible && cost is ulong amount)
    {
      string label = NumberFormatter.AbbreviateBigNumber(amount);
      const float fontSize = 22, iconSpace = 44;
      var measure = Measure2(label, Vector2.Zero, fontSize);
      float left = panel.Center.X - (iconSpace + measure.X) / 2;
      float top = panel.Bottom - 42;
      batch.Begin();
      UntitledGemGameGameScreen.Instance.DrawHudRedGem(batch, new Vector2(left + 16, top + measure.Y / 2));
      batch.End();
      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
        label, new Vector2(left + iconSpace, top), scanColor, Color.Black, fontSize);
    }
    else
      SignalLabel(signalChoicesVisible ? "Scan paid - choose one permanent enhancement" : "Scan cost exceeds the currency limit",
        panel.Center.X, panel.Bottom - 42, 22, OrbitSkin.MutedTextColor);
  }
}
