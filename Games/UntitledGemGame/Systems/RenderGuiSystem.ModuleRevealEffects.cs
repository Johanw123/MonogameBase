using System;
using AsyncContent;
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
    float duration = .8f + tier * .35f;
    float fade = opening ? 1 : Math.Clamp(1 - elapsed / duration, 0, 1);
    if (fade <= 0) return;
    var color = Color.Lerp(OrbitSkin.MutedTextColor, ModuleRarityColor(rarity), Math.Clamp(progress * 2, 0, 1));
    // Keep the discharge in the presentation area, clear of labels and controls.
    var panel = ModuleRevealPanel;
    var clip = new Rectangle(panel.X + 24, panel.Y + 112, panel.Width - 48, 370);
    batch.Begin(blendState: Microsoft.Xna.Framework.Graphics.BlendState.Additive);
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
        Spark(center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * .55f), size, color * opacity);
      }
    }
    if (opening)
    {
      // Counter-rotating scanner bands close around the sealed housing.
      for (int ring = 0; ring < 1 + tier; ring++)
        Ring(180 + ring * 34 + (1 - progress) * 90,
          moduleRevealAge * (ring % 2 == 0 ? 1 : -1) * (.4f + progress), .15f + progress * .35f, 3);
    }
    else
    {
      int rings = tier >= 4 ? 3 : tier >= 2 ? 2 : 1;
      for (int ring = 0; ring < rings; ring++)
      {
        float age = elapsed - ring * .14f;
        if (age < 0) continue;
        Ring(145 + age * (200 + tier * 45), ring * .3f, fade * .7f, 3 + tier);
      }
      float flash = MathF.Pow(Math.Max(0, 1 - elapsed / .28f), 2);
      batch.Draw(AssetManager.DefaultTexture, clip, color * (flash * (.035f + tier * .02f)));
    }
    int particles = 16 + tier * 18;
    for (int i = 0; i < particles; i++)
    {
      float seed = (i * .618034f) % 1;
      float angle = i * 2.399963f + (opening ? moduleRevealAge * (.3f + seed) : 0);
      var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .52f);
      float radius;
      float opacity;
      if (opening)
      {
        float phase = (moduleRevealAge * (.4f + progress) + seed) % 1;
        radius = 140 + (1 - phase) * (140 + tier * 35);
        opacity = MathF.Sin(phase * MathF.PI) * (.25f + progress * .5f);
      }
      else
      {
        radius = 115 + (100 + seed * 320) * (1 - MathF.Pow(fade, 3));
        opacity = fade * (.5f + seed * .5f);
      }
      var position = center + direction * radius;
      int size = 3 + tier;
      Spark(position, size * 3, color * (opacity * .08f));
      Spark(position, size, Color.Lerp(color, Color.White, .5f) * opacity);
      if (!opening && tier >= 2)
        for (int trail = 1; trail <= tier * 2; trail++)
          Spark(position - direction * trail * 9, Math.Max(2, size - trail / 2), color * (opacity * .3f / trail));
      if (!opening && tier == 4 && i % 5 == 0)
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
