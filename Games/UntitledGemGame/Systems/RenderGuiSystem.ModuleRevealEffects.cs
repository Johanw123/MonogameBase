using System;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  private void DrawModuleRevealEffects(SpriteBatch batch, Vector2 center, ModuleRarity rarity, float progress)
  {
    int tier = (int)rarity;
    float elapsed = moduleRevealAge - ModuleRevealDuration;
    bool opening = elapsed < 0;
    float duration = 0.9f + tier * 0.35f;
    float fade = opening ? 1f : Math.Clamp(1f - elapsed / duration, 0f, 1f);
    var color = opening ? GetEscalatingRevealColor(rarity, progress) : ModuleRarityColor(rarity);

    var panel = ModuleRevealPanel;
    var clip = new Rectangle(panel.X + 24, panel.Y + 92, panel.Width - 48, 360);

    // 1. Multi-layer counter-rotating Synty holographic rings & breach shockwave
    batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: HudLayout.RenderTransform);
    if (opening)
    {
      float spin = moduleRevealAge * (0.75f + progress * 1.85f);
      int outerD = (int)(360 - progress * 48);
      int midD = (int)(296 - progress * 34);
      int innerD = (int)(236 + MathF.Sin(moduleRevealAge * 5f) * 10f);
      GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_calibration", center, outerD, spin, color * (0.28f + progress * 0.38f));
      GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_large_02", center, midD, -spin * 1.25f, color * (0.35f + progress * 0.42f));
      GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_large_01", center, innerD, spin * 0.8f,
        Color.Lerp(color, Color.White, 0.35f) * (0.30f + progress * 0.45f));
    }
    else
    {
      float idleSpin = (float)BaseGame.Time.TotalGameTime.TotalSeconds * 0.35f;
      GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_large_02", center, 336, -idleSpin, color * 0.36f);
      GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_large_01", center, 286, idleSpin * 1.3f, color * 0.48f);

      if (fade > 0f)
      {
        int shockD = (int)(230 + (1f - fade * fade) * (310 + tier * 36));
        GuiSkin.DrawRotatedCentered(batch, "synty_hud_ring_large_01", center, shockD, elapsed * 1.5f,
          Color.Lerp(color, Color.White, 0.4f) * (fade * 0.75f));
      }
    }
    batch.End();

    if (fade <= 0f) return;

    // 2. Additive particle convergence, orbital scanner arcs, and breach burst
    batch.Begin(blendState: BlendState.Additive, transformMatrix: HudLayout.RenderTransform);
    void Spark(Vector2 position, int size, Color tint)
    {
      var rect = Rectangle.Intersect(clip, new Rectangle((int)position.X - size / 2,
        (int)position.Y - size / 2, size, size));
      if (rect.Width > 0 && rect.Height > 0) batch.Draw(AssetManager.DefaultTexture, rect, tint);
    }
    void Ring(float radius, float rotation, float opacity, int size)
    {
      for (int i = 0; i < 120; i++)
      {
        if (i % 20 > 15) continue;
        float angle = i * MathF.Tau / 120 + rotation;
        Spark(center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.58f), size, color * opacity);
      }
    }
    if (opening)
    {
      for (int ring = 0; ring < 1 + tier; ring++)
        Ring(168 + ring * 30 + (1 - progress) * 76,
          moduleRevealAge * (ring % 2 == 0 ? 1 : -1) * (0.45f + progress), 0.16f + progress * 0.36f, 3);
    }
    else
    {
      int rings = tier >= 4 ? 3 : tier >= 2 ? 2 : 1;
      for (int ring = 0; ring < rings; ring++)
      {
        float age = elapsed - ring * 0.14f;
        if (age < 0) continue;
        Ring(136 + age * (190 + tier * 42), ring * 0.3f, fade * 0.72f, 3 + tier);
      }
      float flash = MathF.Pow(Math.Max(0, 1 - elapsed / 0.30f), 2);
      batch.Draw(AssetManager.DefaultTexture, clip, color * (flash * (0.04f + tier * 0.022f)));
    }

    int particles = 20 + tier * 18;
    for (int i = 0; i < particles; i++)
    {
      float seed = (i * 0.618034f) % 1;
      float angle = i * 2.399963f + (opening ? moduleRevealAge * (0.35f + seed) : 0);
      var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.56f);
      float radius;
      float opacity;
      if (opening)
      {
        float phase = (moduleRevealAge * (0.45f + progress) + seed) % 1;
        radius = 124 + (1 - phase) * (136 + tier * 32);
        opacity = MathF.Sin(phase * MathF.PI) * (0.28f + progress * 0.52f);
      }
      else
      {
        radius = 108 + (96 + seed * 300) * (1 - MathF.Pow(fade, 3));
        opacity = fade * (0.5f + seed * 0.5f);
      }
      var position = center + direction * radius;
      int size = 3 + tier;
      Spark(position, size * 3, color * (opacity * 0.09f));
      Spark(position, size, Color.Lerp(color, Color.White, 0.5f) * opacity);
      if (!opening && tier >= 2)
        for (int trail = 1; trail <= tier * 2; trail++)
          Spark(position - direction * trail * 9, Math.Max(2, size - trail / 2), color * (opacity * 0.32f / trail));
      if (!opening && tier >= 4 && i % 5 == 0)
      {
        for (int arm = -2; arm <= 2; arm++)
        {
          Spark(position + new Vector2(arm * 5, 0), 3, color * opacity);
          Spark(position + new Vector2(0, arm * 5), 3, color * opacity);
        }
      }
    }
    batch.End();
  }
}
