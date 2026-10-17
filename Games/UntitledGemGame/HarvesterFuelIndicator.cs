using System;
using Apos.Shapes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

// Geometry is shared by drawing, hover feedback and refuel clicks.
internal static class HarvesterFuelIndicator
{
  public static readonly Color Amber = new(255, 190, 65);

  public static bool IsWaiting(Harvester ship) => !ship.MarkedForDestroy
    && ship.CurrentState is Harvester.HarvesterState.OutOfFuel or Harvester.HarvesterState.RequestingFuel;

  public static void Layout(Harvester ship, OrthographicCamera camera,
    out float unit, out float radius, out Vector2 badge)
  {
    // Keep the badge readable as the camera widens, on both render resolutions.
    unit = RenderQuality.WorldScale / Math.Max(0.01f, camera.Zoom);
    float hullRadius = BaseStats.GetHarvesterBaseCollectionRange(ship)
      * (ship.VisualCollectionRangeMultiplier ?? 1f);
    radius = Math.Max(hullRadius + 8f * unit, 26f * unit);
    badge = ship.BoundingCircle.Center - new Vector2(0, radius + 30f * unit);
    var area = PlayAreaBounds.ForCamera(camera);
    if (badge.Y - 20f * unit < area.Minimum.Y)
      badge.Y = ship.BoundingCircle.Center.Y + radius + 30f * unit;
    badge = area.Inset(new Vector2(30f, 20f) * unit).Clamp(badge);
  }

  public static bool Contains(Harvester ship, OrthographicCamera camera, Vector2 point)
  {
    if (!IsWaiting(ship)) return false;
    Layout(ship, camera, out float unit, out float radius, out var badge);
    var offset = point - badge;
    return Vector2.DistanceSquared(point, ship.BoundingCircle.Center) <= radius * radius
      || (Math.Abs(offset.X) <= 30f * unit && Math.Abs(offset.Y) <= 20f * unit);
  }

  public static void Draw(ShapeBatch batch, Harvester ship, OrthographicCamera camera, float time, bool hovered)
  {
    Layout(ship, camera, out float unit, out float radius, out var badge);
    var center = ship.BoundingCircle.Center;
    float pulse = 0.5f + 0.5f * MathF.Sin(time * 4f);
    var color = hovered ? Color.White : Amber;
    // A dark rim keeps the warning legible against gems and bright engines.
    batch.BorderCircle(center, radius, Color.Black * 0.85f, 7f * unit, unit);
    batch.BorderCircle(center, radius, color * (0.75f + 0.25f * pulse), 3f * unit, unit);
    batch.BorderCircle(center, radius + (4f + 5f * pulse) * unit,
      Amber * (0.12f + 0.18f * pulse), 2f * unit, unit);

    var size = new Vector2(60, 40) * unit;
    batch.DrawRectangle(badge - size / 2, size, new Color(7, 14, 24), color,
      (hovered ? 3f : 2f) * unit, 8f * unit, aaSize: unit);
    // Fuel pump, with an empty dark gauge.
    var pump = badge + new Vector2(-21, -12) * unit;
    batch.FillRectangle(pump, new Vector2(19, 24) * unit, color, 2f * unit, aaSize: unit);
    batch.FillRectangle(pump + new Vector2(4, 4) * unit, new Vector2(11, 7) * unit,
      new Color(7, 14, 24), aaSize: unit);
    batch.FillRectangle(pump + new Vector2(-3, 24) * unit, new Vector2(25, 3) * unit, color, aaSize: unit);
    batch.FillLine(pump + new Vector2(19, 12) * unit, pump + new Vector2(25, 18) * unit, 1.5f * unit, color, unit);
    batch.FillLine(pump + new Vector2(25, 18) * unit, pump + new Vector2(25, 5) * unit, 1.5f * unit, color, unit);
    batch.FillLine(pump + new Vector2(25, 5) * unit, pump + new Vector2(21, 1) * unit, 1.5f * unit, color, unit);
    // A white mouse with its left button highlighted makes the action explicit.
    var mouse = badge + new Vector2(12, -12) * unit;
    batch.BorderRectangle(mouse, new Vector2(13, 24) * unit, Color.White, 2f * unit, 5f * unit, aaSize: unit);
    batch.FillRectangle(mouse + new Vector2(2, 2) * unit, new Vector2(4, 8) * unit,
      Amber, 1f * unit, aaSize: unit);
    batch.FillLine(mouse + new Vector2(1, 11) * unit, mouse + new Vector2(12, 11) * unit,
      0.7f * unit, Color.White, unit);
  }
}
