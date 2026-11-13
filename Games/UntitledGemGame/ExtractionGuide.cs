namespace UntitledGemGame;

// When ending the run is the smart move. New players keep playing because nothing tells them
// a run is meant to end. Three points is about where a first run should stop: going further
// without talents turns into a grind. Any run is also worth ending once it hits a wall, so
// this watches the extraction bar rather than the clock: a slow player whose bar still
// climbs is left alone, and only a flat one counts.
public sealed class ExtractionGuide
{
  public const ulong FirstRunPoints = 3;
  // As long as Autoplay's stall_minutes, the pacing playthroughs' idea of a finished run.
  public const float WallSeconds = 5 * 60;
  // The share of the bar a run must gain for it to count as still climbing.
  public const double WallClimb = 0.05;

  private double climbMark;
  private float flatSeconds;
  private ulong lastPending;

  public bool AtWall => flatSeconds >= WallSeconds;

  // bestProgress is the run's best progress toward the next point (GameState.BestPrestigeProgress).
  public void Update(float seconds, ulong pending, double bestProgress)
  {
    if (pending != lastPending || bestProgress >= climbMark + WallClimb)
    {
      lastPending = pending;
      climbMark = bestProgress;
      flatSeconds = 0;
    }
    else
      flatSeconds += seconds;
  }

  public bool Recommended(ulong pending, ulong extractions)
    => pending > 0 && (AtWall || (extractions == 0 && pending >= FirstRunPoints));
}
