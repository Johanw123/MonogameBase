using System;
using AsyncContent;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  public bool SalvageInputCaptured { get; private set; }
  private ShipModule revealingModule;
  private float moduleRevealAge;
  private bool moduleRevealSoundPlayed;
  private static Rectangle ModuleRevealPanel
  {
    get
    {
      var area = ShipyardPanel;
      int width = Math.Min(1500, area.Width - 160), height = Math.Min(1120, area.Height - 120);
      return new Rectangle(area.Center.X - width / 2, area.Center.Y - height / 2, width, height);
    }
  }
  private static Rectangle InspectModuleButton => new(ModuleRevealPanel.Center.X - 240, ModuleRevealPanel.Bottom - 140, 480, 80);
  private static Rectangle RevealContinue => new(ModuleRevealPanel.Center.X - 420, ModuleRevealPanel.Bottom - 140, 380, 72);
  private static Rectangle RevealShipyard => new(ModuleRevealPanel.Center.X + 40, ModuleRevealPanel.Bottom - 140, 380, 72);
  private static Rectangle RevealSkip => new(ModuleRevealPanel.Center.X - 190, ModuleRevealPanel.Bottom - 140, 380, 72);
  private Vector2 SalvageCursor
  {
    get
    {
#if !KNI_WEB
      if (IsDetached)
        return Vector2.Transform(GameInput.Mouse.Position.ToVector2(), popout.InputTransform());
#endif
      var viewport = BaseGame.BoxingViewportAdapterGui.Viewport;
      return Vector2.Transform(GameInput.Mouse.Position.ToVector2() - new Vector2(viewport.X, viewport.Y),
        Matrix.Invert(BaseGame.BoxingViewportAdapterGui.GetScaleMatrix()));
    }
  }

  private float ModuleRevealDuration => ModuleCatalog.Rarities[(int)revealingModule] switch
  {
    ModuleRarity.Mythic => 4.5f,
    ModuleRarity.Legendary => 3.6f,
    ModuleRarity.Epic => 2.5f,
    ModuleRarity.Rare => 1.7f,
    _ => 1.1f
  };

  private void BeginModuleReveal()
  {
    revealingModule = ModuleInventory.PendingReveals.Count > 0 ? ModuleInventory.PendingReveals[0] : ShipModule.None;
    moduleRevealAge = 0;
    moduleRevealSoundPlayed = false;
    AudioManager.Instance?.PlaySound(AudioManager.Instance.UpgradeStartEffect);
  }

  private bool UpdateModuleSalvageInput(float dt)
  {
    if (GameMain.IsPaused || UntitledGemGameGameScreen.Instance.m_prestiging
      || UntitledGemGameGameScreen.Instance.m_postPrestige
      || (IsDetached && !IsPopoutFocused)) return false;
    if (revealingModule != ShipModule.None && !ModuleInventory.PendingReveals.Contains(revealingModule))
      revealingModule = ShipModule.None;
    var mouse = GameInput.Mouse;
    bool clicked = mouse.WasButtonPressed(MouseButton.Left);
    if (m_upgradeWindowType != UpgradeTypes.Shipyard || !shipyardDiscoverySelected) return false;
    // Keep navigation usable while a reveal is playing. Leaving never consumes a module.
    if (clicked && !ShipyardPanel.Contains(SalvageCursor))
      return false;
    if (revealingModule == ShipModule.None)
    {
      if (ModuleInventory.PendingReveals.Count == 0 || !clicked || !InspectModuleButton.Contains(SalvageCursor)) return false;
      BeginModuleReveal();
      SalvageInputCaptured = true;
      return true;
    }
    SalvageInputCaptured = true;
    moduleRevealAge += dt;
    bool ready = moduleRevealAge >= ModuleRevealDuration;
    if (!ready && ((clicked && RevealSkip.Contains(SalvageCursor))
      || KeyboardExtended.GetState().WasKeyPressed(Keys.Space)))
      moduleRevealAge = ModuleRevealDuration;
    if (moduleRevealAge >= ModuleRevealDuration && !moduleRevealSoundPlayed)
    {
      moduleRevealSoundPlayed = true;
      var rarity = ModuleCatalog.Rarities[(int)revealingModule];
      var audio = AudioManager.Instance;
      audio?.PlaySound(rarity >= ModuleRarity.Legendary ? audio.ImpactSoundEffect
        : rarity == ModuleRarity.Epic ? audio.RefuelCompleteEffect ?? audio.UpgradeDoneEffect : audio.UpgradeDoneEffect,
        pitch: rarity >= ModuleRarity.Legendary ? -0.35f : rarity == ModuleRarity.Epic ? 0.2f : 0f);
    }
    // A skip press reveals the card; a separate press acknowledges it.
    if (ready && clicked && (RevealContinue.Contains(SalvageCursor) || RevealShipyard.Contains(SalvageCursor)))
    {
      if (UntitledGemGameGameScreen.Instance.TryAcknowledgeModule(revealingModule))
      {
        shipyardDiscoverySelected = !RevealShipyard.Contains(SalvageCursor);
        revealingModule = ShipModule.None;
      }
    }
    return true;
  }

  private void DrawShipyardDiscovery(SpriteBatch batch)
  {
    var panel = ModuleRevealPanel;
    var accent = HudLayout.UpgradeAccent;
    int pending = ModuleInventory.PendingReveals.Count;
    bool complete = ModuleInventory.CollectionComplete;
    float progress = ModuleInventory.DiscoveryThresholdSeconds > 0
      ? (float)Math.Clamp(ModuleInventory.DiscoveryProgressSeconds / ModuleInventory.DiscoveryThresholdSeconds, 0, 1) : 0;
    var artifact = new Rectangle(panel.Center.X - 110, panel.Y + 150, 220, 220);
    Color artColor = pending > 0 ? accent : OrbitSkin.BorderColor;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Panel(batch, panel, header: true, accent);
    OrbitSkin.DrawScannerReticle(batch, artifact.Center, 340, artColor, 0.80f);
    OrbitSkin.BlueprintBay(batch, artifact, new Color(6, 18, 34, 242), artColor);
    OrbitSkin.DrawHexSocket(batch, new Rectangle(artifact.X + 22, artifact.Y + 22, artifact.Width - 44, artifact.Height - 44),
      artColor, active: pending > 0);
    batch.End();
    RevealLabel(Loc.T("DISCOVERY"), panel.Y + 28, 38, OrbitSkin.StatHeadingColor);
    RevealLabel(complete && pending == 0 ? "—" : "?", artifact.Y + 52, 80, pending > 0 ? accent : OrbitSkin.MutedTextColor);
    RevealLabel(pending > 0 ? (pending == 1 ? Loc.T("A sealed module awaits inspection")
        : Loc.P(pending, "{0} sealed module awaits inspection", "{0} sealed modules await inspection"))
      : complete ? Loc.T("Every module has been discovered") : Loc.T("Something is out there..."), panel.Y + 398, 34, accent);
    if (!complete)
    {
      var rarity = ModuleInventory.DiscoveryRarity;
      var discoveryAccent = rarity.HasValue ? ModuleRarityColor(rarity.Value) : accent;
      RevealLabel(rarity.HasValue ? Loc.F("{0} MODULE TRACE", ModuleRarityLabel(rarity.Value)) : Loc.T("SEARCHING FOR A TRACE"),
        panel.Y + 502, 28, discoveryAccent);
      string stage = progress < 0.2f ? Loc.T("Searching the debris") : progress < 0.5f ? Loc.T("A faint trace emerges")
        : progress < 0.8f ? Loc.T("Isolating an unknown signature") : Loc.T("Closing in on the source");
      RevealLabel(stage, panel.Y + 552, 28, OrbitSkin.MutedTextColor);
      var track = new Rectangle(panel.Center.X - 460, panel.Y + 616, 920, 16);
      batch.Begin(transformMatrix: HudLayout.RenderTransform);
      OrbitSkin.ProgressTrack(batch, track);
      if (progress > 0)
        OrbitSkin.ProgressFill(batch, track, 0f, progress, discoveryAccent);
      batch.End();
      RevealLabel(Loc.T("Harvest gems or scan for signals to strengthen the trace."), panel.Y + 674, 24, OrbitSkin.MutedTextColor);
    }
    RevealLabel(Loc.F("Collection: {0} / {1}", ModuleInventory.RevealedCount, ModuleCatalog.Names.Length - 1), panel.Bottom - 216, 26, OrbitSkin.MutedTextColor);
    if (pending > 0)
      DrawHudButton(batch, InspectModuleButton, Loc.T("Inspect module"), accent, true, InspectModuleButton.Contains(SalvageCursor), 0);
  }

  private void RevealLabel(string text, float y, float size, Color color)
    => ShipyardLabel(text, new Vector2(ModuleRevealPanel.Center.X - Measure2(text, Vector2.Zero, size).X / 2, y), size, color);

  private void DrawModuleReveal(SpriteBatch batch)
  {
    if (revealingModule == ShipModule.None) return;
    var panel = ModuleRevealPanel;
    float progress = Math.Clamp(moduleRevealAge / ModuleRevealDuration, 0f, 1f);
    bool ready = progress >= 1;
    var rarity = ModuleCatalog.Rarities[(int)revealingModule];
    var accent = progress < 0.55f ? OrbitSkin.MutedTextColor : ModuleColor(revealingModule);
    var center = new Vector2(panel.Center.X, panel.Y + 300);
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Panel(batch, panel, header: true, accent);
    OrbitSkin.DrawScannerReticle(batch, new Point((int)center.X, (int)center.Y), 380, accent, 0.85f);
    batch.End();
    RevealLabel(ready ? Loc.T("MODULE DISCOVERED") : Loc.T("UNKNOWN MODULE RECOVERED"), panel.Y + 32, 30, accent);
    DrawModuleRevealEffects(batch, center, rarity, progress);
    if (!ready)
    {
      float release = Math.Clamp((progress - .72f) / .28f, 0, 1);
      int spread = (int)(release * release * (150 + (int)rarity * 25));
      center.X += MathF.Sin(moduleRevealAge * 65) * release * (2 + (int)rarity);
      var left = new Rectangle((int)center.X - 128 - spread, (int)center.Y - 128, 124, 256);
      var right = new Rectangle((int)center.X + 4 + spread, (int)center.Y - 128, 124, 256);
      DrawModulePanel(batch, left, accent);
      DrawModulePanel(batch, right, accent);
      RevealLabel("?", center.Y - 48, 80, accent);
      RevealLabel(progress < 0.55f ? Loc.T("Opening sealed module...") : Loc.T("Decoding module signature..."), panel.Y + 490, 28, accent);
      DrawHudButton(batch, RevealSkip, Loc.T("Skip reveal"), accent, false, RevealSkip.Contains(SalvageCursor), 0);
      return;
    }
    float settle = Math.Clamp((moduleRevealAge - ModuleRevealDuration) / .45f, 0, 1);
    int iconSize = (int)(256 * (1 + .18f * MathF.Sin(settle * MathF.PI) * (1 - settle)));
    QueueModuleIcon(revealingModule, new Rectangle((int)center.X - iconSize / 2, (int)center.Y - iconSize / 2, iconSize, iconSize));
    FlushIcons(batch, Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp);
    RevealLabel(ModuleRarityLabel(revealingModule), panel.Y + 478, 28, accent);
    RevealLabel(Loc.T(ModuleCatalog.Names[(int)revealingModule]), panel.Y + 532, 44, OrbitSkin.StatHeadingColor);
    float y = panel.Y + 620;
    string line = "";
    foreach (var (word, join) in Loc.WrapPieces(Loc.T(ModuleCatalog.Descriptions[(int)revealingModule])))
    {
      string next = line.Length == 0 ? word : line + join + word;
      if (line.Length > 0 && Measure2(next, Vector2.Zero, 30).X > panel.Width - 160)
      {
        RevealLabel(line, y, 30, OrbitSkin.ButtonTextColor);
        y += 42;
        line = word;
      }
      else line = next;
    }
    if (line.Length > 0) RevealLabel(line, y, 30, OrbitSkin.ButtonTextColor);
    RevealLabel(Loc.T("Added to your collection for this run"), panel.Bottom - 210, 24, OrbitSkin.MutedTextColor);
    DrawHudButton(batch, RevealContinue, Loc.T("Discovery"), accent, false, RevealContinue.Contains(SalvageCursor), 0);
    DrawHudButton(batch, RevealShipyard, Loc.T("View modules"), accent, false, RevealShipyard.Contains(SalvageCursor), 0);
  }
}
