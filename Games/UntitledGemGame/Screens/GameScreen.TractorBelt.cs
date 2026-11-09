using System;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private TractorBeltRenderer tractorBeltRenderer;

  // Drawn additively after the world, so the belt reads through dense gem fields: the belt
  // and its intake marks from TractorBelt.fx, then the pods over them.
  private void DrawTractorBelt()
  {
    if (TractorBelt.Lines.Count == 0) return;
    var stats = TractorBeltStats.From(UpgradeManager.Instance.UG, GemClickRadius, UpgradeManager.Instance.Signals);
    float pixel = 1f / Math.Max(0.1f, m_camera.Zoom);
    float clock = (float)Entities.Gem.FlightClock;
    var effect = EffectCache.TractorBeltFx;
    if (effect?.IsLoaded == true && !effect.IsFailed)
    {
      tractorBeltRenderer ??= new TractorBeltRenderer(GraphicsDevice, effect.Value);
      var widths = TractorBeltWidths.For(pixel);
      tractorBeltRenderer.Begin(m_camera.ViewProjection(), widths);
      foreach (var line in TractorBelt.Lines)
        TractorBeltVisual.AddBelt(tractorBeltRenderer, line, stats.Radius, widths, clock);
      tractorBeltRenderer.End();
    }
    m_shapeBatch.Begin(UntitledGemGame.ClickUtility.RenderView(m_camera.GetViewMatrix(),
        BaseGame.BoxingViewportAdapter.GetScaleMatrix()),
      Matrix.CreateOrthographicOffCenter(0, BaseGame.BoxingViewportAdapter.VirtualWidth,
        BaseGame.BoxingViewportAdapter.VirtualHeight, 0, 0, 1), blendState: BlendState.Additive);
    foreach (var line in TractorBelt.Lines)
      TractorBeltVisual.DrawPods(m_shapeBatch, line, pixel, clock);
    m_shapeBatch.End();
  }
}
