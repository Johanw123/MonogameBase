using ImGuiNET;
using JapeFramework;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private readonly WorldPolish worldPolish = new();

  private void AddMiningImpact(Vector2 position, Color color, float energy = 1, bool debris = true)
  {
    if (!PlanetMiningEnabled || planetConsumed || m_prestiging || m_postPrestige) return;
    worldPolish.AddImpact(position, WorldToPlanetNormal(position), position - PlanetPos, color, energy, debris);
  }

  private void DrawPlanetAtmosphere()
  {
    if (EffectCache.PlanetAtmosphereFx?.IsLoaded != true || EffectCache.PlanetAtmosphereFx.IsFailed) return;
    // The same scale and shake as the shell/sprite keep the rim attached during hits.
    // Fade away during extraction so the black hole doesn't inherit a planet atmosphere.
    float visibility = MathHelper.Clamp(PlanetLoopScale(), 0, 1);
    worldPolish.DrawPlanet(m_spriteBatch, EffectCache.PlanetAtmosphereFx.Value, m_camera.ViewProjection(),
      PlanetPos + PlanetShakeOffset(), 31 * PlanetSpriteScale(), PlanetRotation, VisualEffects.Time, visibility);
  }

  private void DrawWorldDust()
  {
    if (EffectCache.WorldParticlesFx?.IsLoaded != true || EffectCache.WorldParticlesFx.IsFailed) return;
    WorldPolish.DrawDust(m_spriteBatch, EffectCache.WorldParticlesFx.Value,
      BaseGame.BoxingViewportAdapter.VirtualWidth, BaseGame.BoxingViewportAdapter.VirtualHeight,
      m_camera.Position, m_camera.Zoom, VisualEffects.Time);
  }

  private void DrawMiningFragments()
  {
    if (EffectCache.WorldParticlesFx?.IsLoaded != true || EffectCache.WorldParticlesFx.IsFailed) return;
    worldPolish.DrawFragments(m_spriteBatch, EffectCache.WorldParticlesFx.Value, m_camera.ViewProjection());
  }

  private void DrawWorldPolishPreview()
  {
    if (!ImGui.CollapsingHeader("Mining impact preview")) return;
    ImGui.TextWrapped("Visual-only hits: no damage, rewards or progress changes. Tune these under Visual effects / space atmosphere.");
    ImGui.Text($"Surface lights: {worldPolish.ActiveImpacts} / {WorldPolish.MaxImpacts}, rock fragments: {worldPolish.ActiveFragments} / {WorldPolish.MaxFragments}");
    ImGui.BeginDisabled(!GameStarted || GameMain.IsPaused || !PlanetMiningEnabled || planetConsumed || m_prestiging || m_postPrestige);
    if (ImGui.Button("Preview cannon hit")) PreviewMiningImpact(false);
    ImGui.SameLine();
    if (ImGui.Button("Preview heavy hit")) PreviewMiningImpact(true);
    ImGui.EndDisabled();
    if (ImGui.Button("Clear impact previews")) worldPolish.Clear();
  }

  private void PreviewMiningImpact(bool heavy)
  {
    var position = PlanetPos + new Vector2(-.78f, -.25f) * PlanetRadius;
    EmitWeaponImpactParticles(new PlanetShot { Kind = heavy ? PlanetShotKind.Rail : PlanetShotKind.Cannon,
      End = position, Critical = heavy });
  }
}
