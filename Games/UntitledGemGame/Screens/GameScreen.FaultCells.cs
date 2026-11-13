using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

// Fault Cells (PrestigeTalentEffects.FractureCells): every core fracture also throws out
// power cells. Like the fracture's Core Shard they are earned the moment it starts, so a
// save mid-event counts them. They burst out of the crack with the shard, hang beside it
// for a moment, then fly into the home base, and each one is in the wallet as it lands.
public partial class UntitledGemGameGameScreen
{
  private const float CellRiseSeconds = 0.8f;
  private const float CellHoverSeconds = 0.6f;
  private const float CellFlightSeconds = 0.7f;
  private const float CellStaggerSeconds = 0.18f;
  private const float CellPopupSeconds = 2f;

  private sealed class FaultCellFlight
  {
    // World positions: out of the crack, round the control point, to a spot beside the shard.
    public Vector2 From, Control, Hover;
    public float Age;
  }

  private readonly List<FaultCellFlight> faultCellFlights = new();
  // Cells earned at fractures but not in the wallet yet; saves count them as owned.
  private int cellsOwed;
  // Cells the fracture under way throws out with its shard.
  private int fractureCells;
  private float cellPopupTime = -1f;
  private int cellPopupCells;

  private ulong OwedPowerCells => (ulong)Math.Max(0, cellsOwed);

  private void OweFaultCells()
  {
    fractureCells = PrestigeTalentEffects.FractureCells;
    cellsOwed += fractureCells;
  }

  // The cells fan out to either side of where the shard will hover, a little below it.
  private void ThrowFaultCells(Vector2 crack, Vector2 direction, Vector2 shardLanding)
  {
    for (int i = 0; i < fractureCells; i++)
    {
      float side = i % 2 == 0 ? -1f : 1f;
      int pair = i / 2;
      faultCellFlights.Add(new FaultCellFlight
      {
        From = crack,
        Control = crack + direction * PlanetRadius * (1.6f + 0.3f * pair) + new Vector2(side * 60f, 0f),
        Hover = shardLanding + new Vector2(side * (110f + 70f * pair), 40f),
        Age = -CellStaggerSeconds * i,
      });
    }
    fractureCells = 0;
  }

  private void UpdateFaultCells(float dt)
  {
    if (cellPopupTime >= 0f && (cellPopupTime += dt) >= CellPopupSeconds) cellPopupTime = -1f;
    for (int i = faultCellFlights.Count - 1; i >= 0; i--)
    {
      var flight = faultCellFlights[i];
      flight.Age += dt;
      if (flight.Age < CellRiseSeconds + CellHoverSeconds + CellFlightSeconds) continue;
      faultCellFlights.RemoveAt(i);
      LandFaultCell();
    }
  }

  private void LandFaultCell()
  {
    if (cellsOwed <= 0) return;
    cellsOwed--;
    m_gameState.TryRefundAbilityPoints(1);
    // The caption above the ship counts up as each cell lands.
    cellPopupCells = cellPopupTime >= 0f ? cellPopupCells + 1 : 1;
    cellPopupTime = 0f;
    SpawnerEffects.Add(null, HomeBasePos, GuiSkin.AbilityAccent, 8f, 80f, 0.45f);
    AudioManager.Instance.PlaySound(SoundEvent.PowerCellLanded);
    m_upgradeManager.UpdateTooltipContent();
  }

  private void ClearFaultCells()
  {
    faultCellFlights.Clear();
    cellsOwed = fractureCells = 0;
    cellPopupTime = -1f;
  }

  // Out of the crack with a pop, a moment's hover beside the shard, then an accelerating
  // arc into the home base.
  private (Vector2 Position, float Scale, float Glow)? FaultCellPose(FaultCellFlight flight)
  {
    if (flight.Age < 0f) return null;
    if (flight.Age < CellRiseSeconds)
    {
      float t = flight.Age / CellRiseSeconds;
      float eased = 1f - (1f - t) * (1f - t);
      float u = 1f - eased;
      var rise = u * u * flight.From + 2f * u * eased * flight.Control + eased * eased * flight.Hover;
      return (rise, EaseOutBack(Math.Min(1f, t * 2f)), 1f + 0.6f * (1f - t));
    }
    float held = flight.Age - CellRiseSeconds;
    if (held < CellHoverSeconds)
      return (flight.Hover + new Vector2(0f, MathF.Sin(held * 5f) * 4f), 1f, 1f);
    float f = Math.Clamp((held - CellHoverSeconds) / CellFlightSeconds, 0f, 1f);
    float e = f * f;
    var control = Vector2.Lerp(flight.Hover, HomeBasePos, 0.3f) - new Vector2(0f, 120f);
    float v = 1f - e;
    var home = v * v * flight.Hover + 2f * v * e * control + e * e * HomeBasePos;
    return (home, MathHelper.Lerp(1f, 0.5f, e), 1f);
  }

  // World pass, beside the Core Shards: a glow and the battery glyph used for power cells.
  private void DrawFaultCells()
  {
    if (faultCellFlights.Count == 0) return;
    float feather = 1.25f / Math.Max(0.1f, m_camera.Zoom);
    float height = ShardVisualSize * 0.6f;
    var color = GuiSkin.AbilityAccent;
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    foreach (var flight in faultCellFlights)
      if (FaultCellPose(flight) is { } pose)
        m_shapeBatch.FillCircle(pose.Position, height * 0.75f * pose.Scale, color * (0.22f * pose.Glow),
          Math.Max(feather, height * 0.5f * pose.Scale));
    m_shapeBatch.End();

    var icon = PowerCells.IconTexture(GraphicsDevice);
    var origin = new Vector2(icon.Width * 0.5f, icon.Height * 0.5f);
    m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
      transformMatrix: m_camera.GetViewMatrix());
    foreach (var flight in faultCellFlights)
      if (FaultCellPose(flight) is { } pose)
        m_spriteBatch.Draw(icon, pose.Position, null, Color.White, 0f, origin,
          height * pose.Scale / icon.Height, SpriteEffects.None, 0f);
    m_spriteBatch.End();
  }

  // HUD: the caption above the ship while the cells land.
  private void DrawFaultCellPopup()
  {
    if (cellPopupTime < 0f || GameMain.IsPaused || RenderGuiSystem.Instance.DrawingPopout
      || RenderGuiSystem.Instance.IsOverlayVisible) return;
    var ship = m_camera.WorldToScreen(HomeBasePos);
#if KNI_WEB
    ship *= 2f; // World target pixels to logical HUD coordinates.
#endif
    float alpha = Math.Min(Math.Clamp(cellPopupTime / 0.15f, 0f, 1f),
      Math.Clamp((CellPopupSeconds - cellPopupTime) / 0.4f, 0f, 1f));
    float y = ship.Y - 170f - 30f * Math.Clamp(cellPopupTime / CellPopupSeconds, 0f, 1f);
    DrawCenteredNotification(Loc.P(cellPopupCells, "+{0} POWER CELL", "+{0} POWER CELLS"),
      ship.X, y, 40f, GuiSkin.AbilityAccent * alpha, Color.Black * alpha);
  }
}
