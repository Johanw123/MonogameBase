using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Entities;

public partial class Harvester
{
  public float ThunderTravelCharge, ThunderFlashRemaining, EaterCooldown;
  public readonly List<(Vector2 Start, Vector2 End)> ThunderArcs = new();
  public int WorldEaterPickups;
  // Gems the black hole may still swallow: one for each gem the ship picked up itself.
  public int WorldEaterHunger;
  public float WorldEaterRadius => Math.Min(240f, 80f + WorldEaterPickups * 2f);
  // Bounded routes contain separate segments so a warp never becomes a collection trail.
  private readonly List<(Vector2 Start, Vector2 End)> heistRoute = new();
  public (Vector2 Start, Vector2 End)[] GhostRoute;
  public int GhostSegment;
  public float GhostSegmentDistance;
  public Vector2 GhostPosition;
  public Vector2 GhostPreviousPosition;
  // What the ghost delivers when it reaches the end of the route.
  public ulong GhostPayout;

  public void RecordHeistMovement(Vector2 start, Vector2 end)
  {
    if (!HasModule(ShipModule.TimeHeist) || MarkedForDestroy || start == end) return;
    if (heistRoute.Count > 0)
    {
      var last = heistRoute[^1];
      var oldDirection = last.End - last.Start;
      var newDirection = end - start;
      if (last.End == start && oldDirection.LengthSquared() > 0f && newDirection.LengthSquared() > 0f
        && Vector2.Dot(Vector2.Normalize(oldDirection), Vector2.Normalize(newDirection)) > 0.995f)
      {
        heistRoute[^1] = (last.Start, end);
        return;
      }
    }
    if (heistRoute.Count < 256) heistRoute.Add((start, end));
  }

  public void StartHeistReplay(ulong payout)
  {
    if (!HasModule(ShipModule.TimeHeist) || CarryingGemCount == 0 || heistRoute.Count == 0) return;
    GhostPayout = payout;
    GhostRoute = heistRoute.ToArray();
    GhostSegment = 0;
    GhostSegmentDistance = 0;
    GhostPosition = GhostPreviousPosition = GhostRoute[0].Start;
  }

  private void ResetMythicTrip(ulong previousLoadout)
  {
    ThunderTravelCharge = ThunderFlashRemaining = EaterCooldown = 0f;
    WorldEaterPickups = WorldEaterHunger = 0;
    ThunderArcs.Clear();
    heistRoute.Clear();
    if (!HasModule(ShipModule.TimeHeist))
    {
      GhostRoute = null;
      GhostPayout = 0;
    }
  }
}
