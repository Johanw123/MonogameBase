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
  private static Rectangle ModuleRevealPanel => ShipyardPanel;
  private static Rectangle InspectModuleButton => new(ShipyardPanel.Center.X - 240, ShipyardPanel.Bottom - 160, 480, 80);
  private static Rectangle RevealContinue => new(ModuleRevealPanel.Center.X - 420, ModuleRevealPanel.Bottom - 120, 380, 72);
  private static Rectangle RevealShipyard => new(ModuleRevealPanel.Center.X + 40, ModuleRevealPanel.Bottom - 120, 380, 72);
  private static Rectangle RevealSkip => new(ModuleRevealPanel.Center.X - 190, ModuleRevealPanel.Bottom - 120, 380, 72);
  private static Vector2 SalvageCursor
  {
    get
    {
      var viewport = BaseGame.BoxingViewportAdapterGui.Viewport;
      return Vector2.Transform(MouseExtended.GetState().Position.ToVector2() - new Vector2(viewport.X, viewport.Y),
        Matrix.Invert(BaseGame.BoxingViewportAdapterGui.GetScaleMatrix()));
    }
  }

  private float ModuleRevealDuration => ModuleCatalog.Rarities[(int)revealingModule] switch
  {
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
      || UntitledGemGameGameScreen.Instance.m_postPrestige) return false;
    if (revealingModule != ShipModule.None && !ModuleInventory.PendingReveals.Contains(revealingModule))
      revealingModule = ShipModule.None;
    var mouse = MouseExtended.GetState();
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
      audio?.PlaySound(rarity == ModuleRarity.Legendary ? audio.ImpactSoundEffect
        : rarity == ModuleRarity.Epic ? audio.RefuelCompleteEffect ?? audio.UpgradeDoneEffect : audio.UpgradeDoneEffect,
        pitch: rarity == ModuleRarity.Legendary ? -0.35f : rarity == ModuleRarity.Epic ? 0.2f : 0f);
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
    var panel = ShipyardPanel;
    var accent = HudLayout.UpgradeAccent;
    int pending = ModuleInventory.PendingReveals.Count;
    bool complete = ModuleInventory.CollectionComplete;
    float progress = ModuleInventory.DiscoveryThresholdSeconds > 0
      ? (float)Math.Clamp(ModuleInventory.DiscoveryProgressSeconds / ModuleInventory.DiscoveryThresholdSeconds, 0, 1) : 0;
    RevealLabel("DISCOVERY", panel.Y + (OrbitSkin.PanelHeaderHeight - Measure2("DISCOVERY", Vector2.Zero, 40).Y) / 2, 40, accent);
    RevealLabel("Your fleet searches for traces of unfamiliar technology.", panel.Y + 120, 26, OrbitSkin.MutedTextColor);
    var artifact = new Rectangle(panel.Center.X - 110, panel.Y + 240, 220, 220);
    DrawModulePanel(batch, artifact, pending > 0 ? accent : OrbitSkin.BorderColor);
    RevealLabel(complete && pending == 0 ? "—" : "?", artifact.Y + 52, 80, pending > 0 ? accent : OrbitSkin.MutedTextColor);
    RevealLabel(pending > 0 ? (pending == 1 ? "A sealed module awaits inspection" : $"{pending} sealed modules await inspection")
      : complete ? "Every module has been discovered" : "Something is out there...", panel.Y + 520, 34, accent);
    if (!complete)
    {
      var rarity = ModuleInventory.DiscoveryRarity;
      var discoveryAccent = rarity.HasValue ? ModuleRarityColor(rarity.Value) : accent;
      RevealLabel(rarity.HasValue ? $"{rarity.Value.ToString().ToUpperInvariant()} MODULE TRACE" : "SEARCHING FOR A TRACE",
        panel.Y + 592, 28, discoveryAccent);
      string stage = progress < 0.2f ? "Searching the debris" : progress < 0.5f ? "A faint trace emerges"
        : progress < 0.8f ? "Isolating an unknown signature" : "Closing in on the source";
      RevealLabel(stage, panel.Y + 644, 28, OrbitSkin.MutedTextColor);
      var track = new Rectangle(panel.Center.X - 460, panel.Y + 708, 920, 20);
      batch.Begin();
      batch.Draw(AssetManager.DefaultTexture, track, OrbitSkin.ButtonBorderColor);
      if (progress > 0)
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(track.X, track.Y, Math.Max(1, (int)(track.Width * progress)), track.Height), discoveryAccent * 0.7f);
      batch.End();
      RevealLabel("Harvest gems or scan for signals to strengthen the trace.", panel.Y + 772, 24, OrbitSkin.MutedTextColor);
    }
    RevealLabel($"Collection: {ModuleInventory.RevealedCount} / {ModuleCatalog.Names.Length - 1}", panel.Bottom - 240, 26, OrbitSkin.MutedTextColor);
    if (pending > 0)
      DrawHudButton(batch, InspectModuleButton, "Inspect module", accent, true, InspectModuleButton.Contains(SalvageCursor), 0);
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
    DrawModulePanel(batch, panel, accent);
    RevealLabel(ready ? "MODULE DISCOVERED" : "UNKNOWN MODULE RECOVERED", panel.Y + 52, 30, accent);
    var center = new Vector2(panel.Center.X, panel.Y + 330);
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
      RevealLabel(progress < 0.55f ? "Opening sealed module..." : "Decoding module signature...", panel.Y + 548, 28, accent);
      DrawHudButton(batch, RevealSkip, "Skip reveal", accent, false, RevealSkip.Contains(SalvageCursor), 0);
      return;
    }
    float settle = Math.Clamp((moduleRevealAge - ModuleRevealDuration) / .45f, 0, 1);
    int iconSize = (int)(256 * (1 + .18f * MathF.Sin(settle * MathF.PI) * (1 - settle)));
    QueueModuleIcon(revealingModule, new Rectangle((int)center.X - iconSize / 2, (int)center.Y - iconSize / 2, iconSize, iconSize));
    FlushIcons(batch, Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp);
    RevealLabel(ModuleRarityLabel(revealingModule), panel.Y + 498, 26, accent);
    RevealLabel(ModuleCatalog.Names[(int)revealingModule], panel.Y + 546, 48, accent);
    float y = panel.Y + 634;
    string line = "";
    foreach (string word in ModuleCatalog.Descriptions[(int)revealingModule].Split(' '))
    {
      string next = line.Length == 0 ? word : line + " " + word;
      if (line.Length > 0 && Measure2(next, Vector2.Zero, 30).X > panel.Width - 160)
      {
        RevealLabel(line, y, 30, OrbitSkin.ButtonTextColor);
        y += 42;
        line = word;
      }
      else line = next;
    }
    if (line.Length > 0) RevealLabel(line, y, 30, OrbitSkin.ButtonTextColor);
    RevealLabel("Permanently added to your collection", panel.Bottom - 195, 26, OrbitSkin.MutedTextColor);
    DrawHudButton(batch, RevealContinue, "Discovery", accent, false, RevealContinue.Contains(SalvageCursor), 0);
    DrawHudButton(batch, RevealShipyard, "View modules", accent, false, RevealShipyard.Contains(SalvageCursor), 0);
  }
}
