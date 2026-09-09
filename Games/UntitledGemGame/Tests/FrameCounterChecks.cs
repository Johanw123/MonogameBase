using System;
using JapeFramework.Helpers;

internal static class FrameCounterChecks
{
  public static void Run()
  {
    var c = new FrameCounter();
    void Check(bool ok) { if (!ok) throw new Exception("Frame counter check failed"); }
    c.Update(0); c.Update(-1); c.Update(float.NaN); c.Update(float.PositiveInfinity);
    Check(c.SampleCount == 0);
    c.Update(.01f); c.Update(.03f);
    Check(Math.Abs(c.AverageFramesPerSecond - 50) < .001);
    Check(Math.Abs(c.MeanMilliseconds - 20) < .001);
    c.Reset();
    for (int i = 0; i < 650; i++) c.Update(.01f);
    c.Update(.3f);
    Check(c.SampleCount == 600 && c.TotalFrames == 651);
    Check(Math.Abs(c.GetFrameMilliseconds(599) - 300) < .001);
    Check(Math.Abs(c.GetFrameMilliseconds(0) - 10) < .001);
    Check(Math.Abs(c.P99Milliseconds - 10) < .001 && Math.Abs(c.MaxMilliseconds - 300) < .001);
    Check(c.OverBudgetFrames == 1);
    c.Reset();
    Check(c.SampleCount == 0 && c.TotalFrames == 0 && c.MaxMilliseconds == 0);
    c.Update(.02f);
    Check(Math.Abs(c.P95Milliseconds - 20) < .001);
    Console.WriteLine("Frame counter checks passed.");
  }
}
