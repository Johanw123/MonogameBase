using System;
using System.Collections.Generic;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

// Prestige points (tuning in PrestigeProgression.cs): sustained gem income charges the
// extraction panel's bar, and each time it fills, the home base surges and a prestige
// point condenses out of the delivery stream. It hangs above the ship for a moment,
// then flies into the panel, whose reward counts up as it lands. Core fractures are
// the damage half of the same idea: the planet cracks and drops a Core Shard that lasts
// the run; the ship glows and keeps a point for good.
public partial class UntitledGemGameGameScreen
{
  private const float PointRiseSeconds = 0.8f;
  private const float PointFlightSeconds = 0.75f;
  private const float PointStaggerSeconds = 0.3f;
  private const int MaxPointFlights = 8;
  private const float PointFlashSeconds = 0.6f;
  private const float PointPopupSeconds = 2f;
  private static readonly Color PrestigePointColor = new(210, 170, 255);

  private sealed class PrestigePointFlight
  {
    public float Age;
    public float Drift;
  }

  private readonly List<PrestigePointFlight> prestigePointFlights = new();
  private float prestigePanelFlash;
  private float prestigePopupTime = -1f;
  private int prestigePopupPoints;

  // Points still on their way to the panel are not in its reward yet.
  private ulong ShownPrestigePoints
  {
    get
    {
      ulong pending = m_gameState.PendingPrestigePoints, flying = (ulong)prestigePointFlights.Count;
      return pending > flying ? pending - flying : 0;
    }
  }

  private void UpdatePrestigePoints(float dt)
  {
    m_gameState.Income.Update(dt);
    prestigePanelFlash = Math.Max(0f, prestigePanelFlash - dt / PointFlashSeconds);
    if (prestigePopupTime >= 0f && (prestigePopupTime += dt) >= PointPopupSeconds)
      prestigePopupTime = -1f;
    for (int i = prestigePointFlights.Count - 1; i >= 0; i--)
    {
      var flight = prestigePointFlights[i];
      flight.Age += dt;
      if (flight.Age < PointRiseSeconds + PointFlightSeconds) continue;
      prestigePointFlights.RemoveAt(i);
      prestigePanelFlash = 1f;
      AudioManager.Instance.PlaySound(AudioManager.Instance.GemClickSoundEffect, pitch: 0.5f);
    }
    if (!progressReady || !GameStarted || m_prestiging || m_postPrestige) return;
    int earned = m_gameState.UpdatePrestigeProgress();
    if (earned > 0) EarnPrestigePoints(earned);
  }

  private void EarnPrestigePoints(int count)
  {
    // Several at once (a big jump in income) leave one after another, fanned out.
    int first = prestigePointFlights.Count;
    for (int i = 0; i < Math.Min(count, MaxPointFlights); i++)
    {
      int slot = first + i;
      prestigePointFlights.Add(new PrestigePointFlight
      {
        Age = -PointStaggerSeconds * i,
        Drift = (slot % 2 == 0 ? 1f : -1f) * 46f * ((slot + 1) / 2),
      });
    }
    prestigePopupPoints = prestigePopupTime >= 0f ? prestigePopupPoints + count : count;
    prestigePopupTime = 0f;
    SpawnerEffects.Add(null, HomeBasePos, PrestigePointColor, 12f, 220f, 0.9f);
    SpawnerEffects.Add(null, HomeBasePos, Color.White, 8f, 120f, 0.5f);
    AudioManager.Instance.PlaySound(AudioManager.Instance.UpgradeDoneEffect, pitch: 0.5f);
    SaveProgress();
  }

  private void ClearPrestigePoints()
  {
    prestigePointFlights.Clear();
    prestigePanelFlash = 0f;
    prestigePopupTime = -1f;
  }

  // HUD: the points in flight and the caption above the ship. With a menu open over the
  // field only the last stretch into the panel shows.
  private void DrawPrestigePoints()
  {
    if (GameMain.IsPaused || RenderGuiSystem.Instance.DrawingPopout) return;
    bool overlay = RenderGuiSystem.Instance.IsOverlayVisible;
    var ship = m_camera.WorldToScreen(HomeBasePos);
    if (prestigePopupTime >= 0f && !overlay)
    {
      float alpha = Math.Min(Math.Clamp(prestigePopupTime / 0.15f, 0f, 1f),
        Math.Clamp((PointPopupSeconds - prestigePopupTime) / 0.4f, 0f, 1f));
      float y = ship.Y - 250f - 30f * Math.Clamp(prestigePopupTime / PointPopupSeconds, 0f, 1f);
      DrawCenteredNotification(Loc.P(prestigePopupPoints, "+{0} PRESTIGE POINT", "+{0} PRESTIGE POINTS"),
        ship.X, y, 40f, PrestigePointColor * alpha, Color.Black * alpha);
    }
    if (prestigePointFlights.Count == 0) return;

    var panel = HudLayout.PrestigePanel;
    var target = new Vector2(panel.X + panel.Width * 0.8f, panel.Y + 28f);
    var viewport = GraphicsDevice.Viewport;
    m_shapeBatch.Begin(Matrix.Identity, Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1),
      blendState: BlendState.Additive);
    foreach (var flight in prestigePointFlights)
      if (PointFlightPose(flight, ship, target, overlay) is { } pose)
        m_shapeBatch.FillCircle(pose.Position, 18f * pose.Scale, PrestigePointColor * (0.45f * pose.Glow), 14f * pose.Scale);
    m_shapeBatch.End();

    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
    foreach (var flight in prestigePointFlights)
      if (PointFlightPose(flight, ship, target, overlay) is { } pose)
        gemSpritePurpleHud?.Draw(m_spriteBatch, pose.Position, 0, Vector2.One * pose.Scale);
    m_spriteBatch.End();
  }

  // Up out of the ship with a pop, a moment's hover, then an accelerating arc into the panel.
  private (Vector2 Position, float Scale, float Glow)? PointFlightPose(PrestigePointFlight flight, Vector2 ship,
    Vector2 target, bool overlay)
  {
    if (flight.Age < 0f) return null;
    var hover = ship + new Vector2(flight.Drift, -160f);
    if (flight.Age < PointRiseSeconds)
    {
      if (overlay) return null;
      float t = flight.Age / PointRiseSeconds;
      float rise = 1f - (1f - t) * (1f - t) * (1f - t);
      var position = Vector2.Lerp(ship + new Vector2(0f, -40f), hover, rise)
        + new Vector2(0f, MathF.Sin(flight.Age * 9f) * 4f * t);
      return (position, 4.2f * EaseOutBack(Math.Min(1f, t * 2f)), 1f + 0.5f * (1f - t));
    }
    float f = Math.Clamp((flight.Age - PointRiseSeconds) / PointFlightSeconds, 0f, 1f);
    if (overlay && f < 0.6f) return null;
    float eased = f * f;
    var control = new Vector2(MathHelper.Lerp(hover.X, target.X, 0.15f), hover.Y - 140f);
    float u = 1f - eased;
    var arc = u * u * hover + 2f * u * eased * control + eased * eased * target;
    return (arc, MathHelper.Lerp(4.2f, 1.8f, eased), 1f);
  }
}
