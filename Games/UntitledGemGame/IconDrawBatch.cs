using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

/// <summary>Collects non-overlapping UI icons for one atlas draw pass.</summary>
public sealed class IconDrawBatch
{
  private readonly List<(Rectangle Region, Vector2 Center, float Scale, Color Color)> draws = new();

  public void Add(Rectangle region, Vector2 center, float scale, Color color)
    => draws.Add((region, center, scale, color));

  public void Draw(SpriteBatch batch, Texture2D atlas, SamplerState sampler)
  {
    if (draws.Count == 0) return;
    try
    {
      if (atlas == null) return;
      batch.Begin(samplerState: sampler);
      foreach (var draw in draws)
        batch.Draw(atlas, draw.Center, draw.Region, draw.Color, 0,
          new Vector2(draw.Region.Width, draw.Region.Height) / 2,
          draw.Scale, SpriteEffects.None, 0);
      batch.End();
    }
    finally
    {
      draws.Clear();
    }
  }
}
