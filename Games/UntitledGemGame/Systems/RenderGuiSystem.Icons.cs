using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  private readonly IconDrawBatch icons = new();

  private void QueueIcon(Rectangle region, Vector2 center, float scale, Color color)
  {
    if (TextureCache.IconAtlas == null || !TextureCache.IconAtlas.IsLoaded || TextureCache.IconAtlas.IsFailed) return;
    icons.Add(region, center, scale, color);
  }

  // Flush before overlapping UI (tooltips, drag ghosts). Modules retain point
  // filtering and signals retain linear filtering, each in its own UI pass.
  private void FlushIcons(SpriteBatch batch, SamplerState sampler)
    => icons.Draw(batch, TextureCache.IconAtlas?.Value, sampler);
}
