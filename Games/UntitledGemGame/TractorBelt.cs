using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// A gem the belt carries, identified so a reused pool slot is never mistaken for it.
public readonly record struct BeltRider(int GridIndex, int EntityId, ulong Lifetime);

// What the belt needs from the game. Tests stand in a plain spatial index.
public interface ITractorBeltCargo
{
  // Takes a loose gem off the field: out of spatial queries, positioned by the belt.
  bool TryBoard(int gridIndex, out BeltRider rider, out Vector2 position);
  // False once something else owns the gem (a swallow, prestige, the pool).
  bool Ride(in BeltRider rider, Vector2 position, float scale);
  void Deliver(in BeltRider rider, double valueMultiplier);
  // The belt was torn down with the gem aboard.
  void Drop(in BeltRider rider);
}

// The belt's effective stats: run upgrades with signals and talents applied.
public readonly record struct TractorBeltStats(bool Enabled, float Radius, float Speed, int Capacity,
  float Duration, double ValueMultiplier, bool FullFreight, bool Sweep, int MaxIntakes)
{
  public bool Permanent => float.IsPositiveInfinity(Duration);

  // Belt Network (a prestige talent) doubles how many intakes run at once.
  public const int NetworkIntakeMultiplier = 2;
  // Express Line (a Core Shard upgrade): the belt runs this much faster with pods this much bigger.
  public const int ExpressMultiplier = 3;

  public static TractorBeltStats From(UpgradesGeneratorUpgrades upgrades, float clickRadius,
    SignalProgression signals = null, UpgradesGeneratorUpgrades_meta talents = null) => new(
    upgrades.TractorBeltEnabled,
    clickRadius * SignalStats.Scale(SignalKind.TractorBeltRadius, upgrades.TractorBeltRadiusMultiplier, signals),
    TractorBelt.BaseSpeed * SignalStats.Scale(SignalKind.TractorBeltSpeed, upgrades.TractorBeltSpeedMultiplier, signals)
      * (upgrades.ExpressLine ? ExpressMultiplier : 1),
    Math.Max(1, signals?.ScaleCount(SignalKind.TractorBeltCapacity, upgrades.TractorBeltCapacity)
      ?? upgrades.TractorBeltCapacity) * (upgrades.ExpressLine ? ExpressMultiplier : 1),
    upgrades.TractorBeltPermanent ? float.PositiveInfinity
      : SignalStats.Scale(SignalKind.TractorBeltDuration, upgrades.TractorBeltDuration, signals),
    1 + Math.Max(0, upgrades.TractorBeltValue),
    upgrades.TractorBeltFullFreight, upgrades.TractorBeltSweep,
    (upgrades.TractorBeltBranch ? 2 : 1) * (talents?.BeltNetwork == true ? NetworkIntakeMultiplier : 1));
}

// One stretch of the belt: a line from A to B, or an arc around Center starting at angle
// StartAngle and turning Sweep radians (positive with increasing angle). Start is the
// distance along the belt where it begins.
public readonly record struct BeltPiece(bool IsArc, Vector2 A, Vector2 B, Vector2 Center, float Radius,
  float StartAngle, float Sweep, float Start, float Length)
{
  public static BeltPiece Segment(Vector2 from, Vector2 to, float start)
    => new(false, from, to, Vector2.Zero, 0, 0, 0, start, Vector2.Distance(from, to));

  public static BeltPiece Arc(Vector2 center, float radius, float startAngle, float sweep, float start)
    => new(true, center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius,
      center + new Vector2(MathF.Cos(startAngle + sweep), MathF.Sin(startAngle + sweep)) * radius,
      center, radius, startAngle, sweep, start, Math.Abs(sweep) * radius);

  public Vector2 PointAt(float turn)
    => Center + new Vector2(MathF.Cos(StartAngle + turn), MathF.Sin(StartAngle + turn)) * Radius;
}

// The right-click power: an intake placed on the field and a conveyor of pods running from
// it to the home base, around the planet. Each pod leaves the intake loaded with the nearest
// loose gems, up to its capacity, and delivers them on arrival. A placement runs for a while
// (Long Haul extends it, Fixed Installation makes it permanent); pods already on the belt
// still finish their trip after it shuts off. Never claims gems a harvester has reserved.
public sealed class TractorBelt
{
  public const float BaseSpeed = 160f;
  public const float PodSpacing = 44f;
  public const float BoardSeconds = 0.35f;
  // Gems are delivered this close to the home base, shrinking into it over ShrinkDistance.
  public const float EndReach = 14f;
  public const float ShrinkDistance = 44f;
  public const float SweepRadius = 16f;
  // The most gems one search looks at: each departing pod has its own, and a belt's sweeping
  // pods share one a frame. Searches run nearest first (GemSpatialIndex.QueryNearest), so in
  // a field too dense to look through they still end with the gems closest to the intake,
  // and one intake's pile never costs another its turn.
  public const int ScanBudget = 4096;
  public const float WarningSeconds = 3f;
  public const float PlaceGlowSeconds = 0.45f;
  // A shut-off belt reels in toward the home base, pods and all, speeding up as it goes; its
  // end never passes a pod that still carries gems.
  public const float RetractStartSpeed = 400f;
  public const float RetractAcceleration = 2400f;
  public const float MaxRetractSpeed = 2400f;
  public const float RetractGap = 10f;
  // The intake's catch ring fades in fast when wanted and lingers a moment after.
  public const float HighlightInSeconds = 0.1f;
  public const float HighlightOutSeconds = 0.35f;
  // The belt's circle around the planet, as a share of its radius: clear of the surface.
  public const float PlanetClearance = 1.15f;
  // Gameplay samples the arc this finely (radians); pods follow it without visible facets.
  private const float RouteArcStep = 0.04f;

  public static float WrapAngle(float angle) => angle - MathHelper.TwoPi * MathF.Floor(angle / MathHelper.TwoPi);

  public struct Rider
  {
    public BeltRider Gem;
    public Vector2 From;
    public float Age;
    public int Slot;
  }

  public sealed class Pod
  {
    public float DistanceToEnd;
    public int Capacity;
    public readonly List<Rider> Riders = new(2);
    public int Count => Riders.Count;
    public bool Full => Riders.Count >= Capacity;
  }

  public sealed class Line
  {
    public Vector2 Intake;
    // From the intake to the home base, around the planet.
    public readonly List<Vector2> Route = new();
    // The same route as exact lines and an arc, each starting where the last ends.
    public readonly List<BeltPiece> Pieces = new(3);
    private readonly List<float> distances = new();
    public float RouteLength { get; private set; }
    public float Duration, Remaining;
    // Shut off: no more pods leave the intake, the rest still run home.
    public bool Expired;
    // Total distance the belt has run, for the drawn flow.
    public float Travel;
    // Distance to the home base of the newest pod: pods sit at this phase every PodSpacing,
    // so the drawn chevrons can fall between them.
    public float PodPhase;
    public float SpawnProgress = PodSpacing;
    public float PlaceGlow;
    // How much of the belt, from the intake end, a shut-off belt has reeled in.
    public float Retracted;
    internal float RetractSpeed = RetractStartSpeed;
    // 0..1: how much of the intake's catch ring shows (UpdateHighlights).
    public float Highlight;
    internal long Order;
    // Pods in departure order: the first is nearest the home base.
    public readonly List<Pod> Pods = new();

    public float LifeFraction => Expired ? 0f : float.IsPositiveInfinity(Remaining) ? 1f
      : Duration > 0 ? Math.Clamp(Remaining / Duration, 0f, 1f) : 0f;
    public bool Warning => !Expired && Remaining <= WarningSeconds;

    // The belt runs like a drive belt around a pulley: straight to the planet's clearance
    // circle, along it, and straight on to the home base, taking the shorter way round. The
    // pieces are exact (BeltPiece, drawn by TractorBelt.fx); Route samples them for gameplay.
    internal void BuildRoute(Vector2 home, Vector2? planet, float clearance)
    {
      Pieces.Clear();
      if (planet is { } center && clearance > 0 && Vector2.Distance(home, center) > clearance * 1.001f
        && PlanetObstacle.SegmentHitsCircle(Intake, home, center, clearance))
      {
        float fromIntake = Math.Max(clearance, Vector2.Distance(Intake, center));
        float fromHome = Vector2.Distance(home, center);
        float intakeAngle = MathF.Atan2(Intake.Y - center.Y, Intake.X - center.X);
        float homeAngle = MathF.Atan2(home.Y - center.Y, home.X - center.X);
        float intakeTangent = MathF.Acos(Math.Clamp(clearance / fromIntake, -1f, 1f));
        float homeTangent = MathF.Acos(Math.Clamp(clearance / fromHome, -1f, 1f));
        // Leaving with increasing angle touches the circle past the intake's angle, and
        // arrives short of the home's; the other way round mirrors both.
        float startUp = intakeAngle + intakeTangent, sweepUp = WrapAngle(homeAngle - homeTangent - startUp);
        float startDown = intakeAngle - intakeTangent, sweepDown = -WrapAngle(startDown - homeAngle - homeTangent);
        bool up = sweepUp <= -sweepDown;
        float start = up ? startUp : startDown, sweep = up ? sweepUp : sweepDown;
        var departure = center + new Vector2(MathF.Cos(start), MathF.Sin(start)) * clearance;
        var arrival = center + new Vector2(MathF.Cos(start + sweep), MathF.Sin(start + sweep)) * clearance;
        AddSegment(Intake, departure);
        Pieces.Add(BeltPiece.Arc(center, clearance, start, sweep, LengthOf(Pieces)));
        AddSegment(arrival, home);
      }
      else
        AddSegment(Intake, home);
      RouteLength = LengthOf(Pieces);

      Route.Clear();
      distances.Clear();
      Route.Add(Intake);
      foreach (var piece in Pieces)
      {
        if (!piece.IsArc)
        {
          Route.Add(piece.B);
          continue;
        }
        int steps = Math.Max(1, (int)MathF.Ceiling(Math.Abs(piece.Sweep) / RouteArcStep));
        for (int i = 1; i <= steps; i++)
          Route.Add(piece.PointAt(piece.Sweep * i / steps));
      }
      float total = 0;
      distances.Add(0);
      for (int i = 1; i < Route.Count; i++)
      {
        total += Vector2.Distance(Route[i - 1], Route[i]);
        distances.Add(total);
      }
      // Sampled chords run a hair shorter than the arc; travel uses the sampled length.
      RouteLength = total;
    }

    private void AddSegment(Vector2 from, Vector2 to)
    {
      if (Vector2.DistanceSquared(from, to) > 1e-4f) Pieces.Add(BeltPiece.Segment(from, to, LengthOf(Pieces)));
    }

    private static float LengthOf(List<BeltPiece> pieces) => pieces.Count == 0 ? 0 : pieces[^1].Start + pieces[^1].Length;

    // Distance from the intake along the route.
    public Vector2 PointAlong(float distance)
    {
      if (Route.Count == 0) return Intake;
      if (distance <= 0) return Route[0];
      for (int i = 1; i < Route.Count; i++)
        if (distance <= distances[i])
        {
          float span = distances[i] - distances[i - 1];
          return span > 0 ? Vector2.Lerp(Route[i - 1], Route[i], (distance - distances[i - 1]) / span) : Route[i];
        }
      return Route[^1];
    }

    public Vector2 DirectionAlong(float distance)
    {
      for (int i = 1; i < Route.Count; i++)
        if (distance <= distances[i] || i == Route.Count - 1)
        {
          var segment = Route[i] - Route[i - 1];
          return segment.LengthSquared() > 1e-4f ? Vector2.Normalize(segment) : Vector2.UnitX;
        }
      return Vector2.UnitX;
    }

    public Vector2 PodPosition(Pod pod) => PointAlong(RouteLength - pod.DistanceToEnd);

    // Over the intake's catch area, or within reach of the belt itself.
    public bool IsNear(Vector2 point, float intakeRadius, float beltReach)
    {
      if (Vector2.DistanceSquared(point, Intake) <= intakeRadius * intakeRadius) return true;
      for (int i = 1; i < Route.Count; i++)
      {
        var segment = Route[i] - Route[i - 1];
        float lengthSquared = segment.LengthSquared();
        float t = lengthSquared > 1e-4f ? Math.Clamp(Vector2.Dot(point - Route[i - 1], segment) / lengthSquared, 0f, 1f) : 0f;
        if (Vector2.DistanceSquared(point, Route[i - 1] + segment * t) <= beltReach * beltReach) return true;
      }
      return false;
    }
  }

  private readonly List<Line> lines = new();
  private Line grabbed;
  private long placements;
  private bool expiredPending;
  private int[] nearest = new int[8];
  private float[] nearestDistance = new float[8];

  public IReadOnlyList<Line> Lines => lines;
  public Line Grabbed => grabbed;
  public int ActiveCount
  {
    get
    {
      int count = 0;
      foreach (var line in lines) if (!line.Expired) count++;
      return count;
    }
  }

  // Once per timed-out placement, for its power-down cue.
  public bool TakeExpiredNotification()
  {
    bool pending = expiredPending;
    expiredPending = false;
    return pending;
  }

  public static Vector2 SlotOffset(int slot)
  {
    if (slot <= 0) return Vector2.Zero;
    float angle = slot * 2.3999632f;
    float radius = 3.5f * MathF.Sqrt(slot);
    return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
  }

  // Right press places an intake (or grabs one under the pointer); holding drags it.
  // Returns true when a placement starts, which also restarts its run time.
  public bool HandleInput(bool rightPressed, bool rightHeld, bool inputEnabled, Vector2 pointer,
    in TractorBeltStats stats)
  {
    if (!stats.Enabled || !inputEnabled || !rightHeld)
    {
      grabbed = null;
      return false;
    }
    if (rightPressed)
    {
      grabbed = ActiveLineNear(pointer, stats.Radius) ?? Place(stats);
      grabbed.Intake = pointer;
      grabbed.Duration = grabbed.Remaining = stats.Duration;
      grabbed.PlaceGlow = PlaceGlowSeconds;
      grabbed.Highlight = 1f;
      return true;
    }
    if (grabbed is { Expired: false }) grabbed.Intake = pointer;
    return false;
  }

  // The catch ring is only wanted while placing an intake or pointing at a belt; otherwise
  // it would cover a large share of the field. pointer is null when the field has no pointer.
  public void UpdateHighlights(float dt, Vector2? pointer, float intakeRadius, float beltReach)
  {
    dt = Math.Max(0, dt);
    foreach (var line in lines)
    {
      bool wanted = !line.Expired && (line == grabbed
        || pointer is { } point && line.IsNear(point, intakeRadius, beltReach));
      line.Highlight = wanted ? Math.Min(1f, line.Highlight + dt / HighlightInSeconds)
        : Math.Max(0f, line.Highlight - dt / HighlightOutSeconds);
    }
  }

  private Line ActiveLineNear(Vector2 pointer, float radius)
  {
    Line best = null;
    float bestDistance = radius * radius;
    foreach (var line in lines)
    {
      if (line.Expired) continue;
      float distance = Vector2.DistanceSquared(line.Intake, pointer);
      if (distance <= bestDistance) { best = line; bestDistance = distance; }
    }
    return best;
  }

  private Line Place(in TractorBeltStats stats)
  {
    // Over the limit the oldest intake shuts off and reels in, taking its loaded pods home.
    Line replaced = null;
    while (ActiveCount >= Math.Max(1, stats.MaxIntakes))
    {
      Line oldest = null;
      foreach (var line in lines)
        if (!line.Expired && (oldest == null || line.Order < oldest.Order)) oldest = line;
      oldest.Expired = true;
      replaced = oldest;
    }
    // A new intake keeps the replaced one's pod rhythm: re-placing never sends extra pods.
    var placed = new Line { Order = ++placements, SpawnProgress = replaced?.SpawnProgress ?? PodSpacing };
    lines.Add(placed);
    return placed;
  }

  public void Update(float dt, GemSpatialIndex grid, ITractorBeltCargo cargo, in TractorBeltStats stats,
    Vector2 home, Vector2? planet = null, float planetRadius = 0)
  {
    dt = Math.Max(0, dt);
    float clearance = planetRadius * PlanetClearance;
    for (int l = lines.Count - 1; l >= 0; l--)
    {
      var line = lines[l];
      if (!stats.Enabled) line.Expired = true;
      if (planet is { } center && clearance > 0)
        line.Intake = PlanetObstacle.PushOut(line.Intake, center, clearance);
      line.BuildRoute(home, planet, clearance);
      line.PlaceGlow = Math.Max(0, line.PlaceGlow - dt);
      if (!line.Expired)
      {
        if (stats.Permanent) line.Duration = line.Remaining = float.PositiveInfinity;
        else if (float.IsPositiveInfinity(line.Remaining)) line.Duration = line.Remaining = stats.Duration;
        line.Remaining -= dt;
        if (line.Remaining <= 0)
        {
          line.Remaining = 0;
          line.Expired = true;
          expiredPending = true;
          if (grabbed == line) grabbed = null;
        }
      }

      // A shut-off belt reels in faster and faster, its pods with it.
      if (line.Expired) line.RetractSpeed = Math.Min(MaxRetractSpeed, line.RetractSpeed + RetractAcceleration * dt);
      float advance = (line.Expired ? Math.Max(stats.Speed, line.RetractSpeed) : stats.Speed) * dt;
      line.Travel += advance;
      MovePods(line, advance, cargo, stats);
      if (!line.Expired)
      {
        line.SpawnProgress += advance;
        while (line.SpawnProgress >= PodSpacing)
        {
          line.SpawnProgress -= PodSpacing;
          var pod = new Pod { DistanceToEnd = line.RouteLength - line.SpawnProgress, Capacity = stats.Capacity };
          if (pod.DistanceToEnd <= EndReach) continue;
          int search = ScanBudget;
          Load(pod, line.Intake, stats.Radius, grid, cargo, ref search);
          line.Pods.Add(pod);
        }
      }
      line.PodPhase = line.Pods.Count > 0 ? line.Pods[^1].DistanceToEnd : line.PodPhase - advance;
      // Only a running belt picks gems up on the way; a shut-off one reels in.
      if (stats.Sweep && !line.Expired)
      {
        int search = ScanBudget;
        foreach (var pod in line.Pods)
          if (!pod.Full) Load(pod, line.PodPosition(pod), SweepRadius, grid, cargo, ref search);
      }
      PlaceRiders(line, dt, cargo);

      if (line.Expired && Retract(line, advance))
      {
        if (grabbed == line) grabbed = null;
        lines.RemoveAt(l);
      }
    }
  }

  // Reels a shut-off belt in from its intake end, as far as the pods moved this frame.
  // Empty pods are taken in as the end passes them; a pod with gems keeps the end behind it
  // until it delivers. True once fully in.
  private static bool Retract(Line line, float advance)
  {
    float limit = line.RouteLength;
    for (int i = line.Pods.Count - 1; i >= 0; i--)
      if (line.Pods[i].Count > 0)
      {
        limit = Math.Max(0f, line.RouteLength - line.Pods[i].DistanceToEnd - RetractGap);
        break;
      }
    float reeled = Math.Min(line.Retracted + advance, limit);
    line.Retracted = Math.Min(Math.Max(line.Retracted, reeled), line.RouteLength);
    line.Pods.RemoveAll(pod => pod.Count == 0 && line.RouteLength - pod.DistanceToEnd <= line.Retracted);
    return line.Pods.Count == 0 && line.Retracted >= line.RouteLength - 0.01f;
  }

  private static void MovePods(Line line, float advance, ITractorBeltCargo cargo, in TractorBeltStats stats)
  {
    int arrived = 0;
    foreach (var pod in line.Pods)
    {
      // A shorter route (the intake moved closer) carries the pods with it.
      pod.DistanceToEnd = Math.Min(pod.DistanceToEnd, line.RouteLength) - advance;
      if (pod.DistanceToEnd > EndReach) continue;
      double multiplier = stats.ValueMultiplier * (stats.FullFreight && pod.Full ? 2 : 1);
      foreach (var rider in pod.Riders) cargo?.Deliver(rider.Gem, multiplier);
      pod.Riders.Clear();
      arrived++;
    }
    // Pods arrive in departure order.
    line.Pods.RemoveRange(0, arrived);
  }

  // Loads the loose gems nearest a point into the pod's free room, looking at no more gems
  // than budget has left.
  private void Load(Pod pod, Vector2 point, float radius, GemSpatialIndex grid, ITractorBeltCargo cargo,
    ref int budget)
  {
    int room = pod.Capacity - pod.Count;
    if (room <= 0 || grid == null || cargo == null || budget <= 0) return;
    if (nearest.Length < room)
    {
      nearest = new int[room];
      nearestDistance = new float[room];
    }
    // Boarding unlinks a gem from the index, so it waits until the search is over.
    int found = grid.QueryNearest(point.X, point.Y, radius, nearest, nearestDistance, room, ref budget);
    for (int i = 0; i < found; i++)
      if (cargo.TryBoard(nearest[i], out var rider, out var position))
        pod.Riders.Add(new Rider { Gem = rider, From = position, Slot = pod.Count });
  }

  private static void PlaceRiders(Line line, float dt, ITractorBeltCargo cargo)
  {
    foreach (var pod in line.Pods)
    {
      var center = line.PodPosition(pod);
      float scale = MathHelper.Lerp(0.35f, 1f,
        Math.Clamp((pod.DistanceToEnd - EndReach) / (ShrinkDistance - EndReach), 0f, 1f));
      for (int i = pod.Riders.Count - 1; i >= 0; i--)
      {
        var rider = pod.Riders[i];
        rider.Age += dt;
        float progress = Math.Clamp(rider.Age / BoardSeconds, 0f, 1f);
        float eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
        var position = Vector2.Lerp(rider.From, center + SlotOffset(rider.Slot), eased);
        if (cargo == null || !cargo.Ride(rider.Gem, position, scale))
        {
          pod.Riders.RemoveAt(i);
          continue;
        }
        pod.Riders[i] = rider;
      }
    }
  }

  public void Reset(ITractorBeltCargo cargo = null)
  {
    foreach (var line in lines)
      foreach (var pod in line.Pods)
        foreach (var rider in pod.Riders) cargo?.Drop(rider.Gem);
    lines.Clear();
    grabbed = null;
    expiredPending = false;
  }
}
