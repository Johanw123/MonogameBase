using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Systems;

public partial class RenderSystem
{
  private void DrawFleetTalentEffects(Harvester ship, Transform2 transform)
  {
    if (ship.MarkedForDestroy) return;
    foreach (var delivery in ship.DeliveredTails)
    {
      var anchor = delivery.Anchor;
      foreach (var gem in delivery.Gems)
      {
        _shapeBatch.FillLine(anchor, gem.Position, 0.1f, Color.Cyan * 0.18f, 5f);
        _shapeBatch.FillLine(anchor, gem.Position, 0.1f, Color.LightCyan * 0.65f, 1.3f);
        anchor = gem.Position;
      }
    }
    if (ship.TailGems is { Count: > 0 } tail)
    {
      var anchor = transform.Position;
      foreach (var gem in tail)
      {
        _shapeBatch.FillLine(anchor, gem.Position, 0.1f, Color.Cyan * 0.18f, 5f);
        _shapeBatch.FillLine(anchor, gem.Position, 0.1f, Color.LightCyan * 0.65f, 1.3f);
        anchor = gem.Position;
      }
    }
  }

  private static System.Collections.Generic.IEnumerable<Harvester.TailGem> EnumerateTailGems(Harvester ship)
  {
    if (ship.TailGems != null)
      foreach (var gem in ship.TailGems) yield return gem;
    foreach (var delivery in ship.DeliveredTails)
      foreach (var gem in delivery.Gems) yield return gem;
  }

  // Decode the existing packed gem art with the gem shader's SpriteBatch technique.
  private void DrawTractorTailGems()
  {
    if (EffectCache.GemEffect?.IsLoaded != true) return;
    var effect = EffectCache.GemEffect.Value;
    var previousTechnique = effect.CurrentTechnique;
    bool begun = false;
    foreach (int id in ActiveEntities)
    {
      var ship = _harvesterMapper.Has(id) ? _harvesterMapper.Get(id) : null;
      if (ship == null || ship.MarkedForDestroy || (ship.TailGems is not { Count: > 0 } && ship.DeliveredTails.Count == 0)) continue;
      if (!begun)
      {
        effect.CurrentTechnique = effect.Techniques["TractorTailDrawing"];
        effect.Parameters["view_projection"]?.SetValue(m_camera.ViewProjection());
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
          effect: effect, transformMatrix: m_camera.GetViewMatrix());
        begun = true;
      }
      foreach (var gem in EnumerateTailGems(ship))
      {
        if (gem.Region == null) continue;
        var region = gem.Region;
        _spriteBatch.Draw(region.Texture, gem.Position, region.Bounds, gem.Color, gem.Rotation,
          new Vector2(region.Width, region.Height) * 0.5f, Gem.TexelSize * 0.8f, SpriteEffects.None, 0f);
      }
    }
    if (begun)
    {
      _spriteBatch.End();
      effect.CurrentTechnique = previousTechnique;
    }
  }
}
