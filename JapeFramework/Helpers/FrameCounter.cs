using System;

namespace JapeFramework.Helpers
{
  // Fixed storage; sorting/statistics are refreshed only four times per second.
  public class FrameCounter
  {
    public const int MaximumSamples = 600;
    private readonly float[] _samples = new float[MaximumSamples];
    private readonly float[] _sorted = new float[MaximumSamples];
    private int _next;
    private double _sum;
    private float _refresh;
    public int SampleCount { get; private set; }
    public long TotalFrames { get; private set; }
    public float TotalSeconds { get; private set; }
    public float AverageFramesPerSecond { get; private set; }
    public float CurrentFramesPerSecond { get; private set; }
    public float MeanMilliseconds { get; private set; }
    public float P95Milliseconds { get; private set; }
    public float P99Milliseconds { get; private set; }
    public float MaxMilliseconds { get; private set; }
    public float HistorySeconds => (float)_sum;
    public int OverBudgetFrames { get; private set; }
    public float TargetMilliseconds { get; set; } = 1000f / 60f;

    public float GetFrameMilliseconds(int chronologicalIndex)
    {
      if ((uint)chronologicalIndex >= (uint)SampleCount)
        throw new ArgumentOutOfRangeException(nameof(chronologicalIndex));
      return _samples[(_next - SampleCount + MaximumSamples + chronologicalIndex) % MaximumSamples] * 1000f;
    }

    public void Update(float deltaTime)
    {
      if (!float.IsFinite(deltaTime) || deltaTime <= 0) return;
      CurrentFramesPerSecond = 1f / deltaTime;
      if (SampleCount == MaximumSamples) _sum -= _samples[_next];
      else ++SampleCount;
      _samples[_next] = deltaTime;
      _next = (_next + 1) % MaximumSamples;
      _sum += deltaTime;
      AverageFramesPerSecond = (float)(SampleCount / _sum);
      MeanMilliseconds = (float)(_sum * 1000 / SampleCount);
      ++TotalFrames;
      TotalSeconds += deltaTime;
      _refresh -= deltaTime;
      if (_refresh > 0) return;
      _refresh = 0.25f;
      OverBudgetFrames = 0;
      for (int i = 0; i < SampleCount; ++i)
      {
        _sorted[i] = GetFrameMilliseconds(i);
        if (_sorted[i] > TargetMilliseconds) ++OverBudgetFrames;
      }
      Array.Sort(_sorted, 0, SampleCount);
      P95Milliseconds = _sorted[(int)Math.Ceiling(SampleCount * 0.95) - 1];
      P99Milliseconds = _sorted[(int)Math.Ceiling(SampleCount * 0.99) - 1];
      MaxMilliseconds = _sorted[SampleCount - 1];
    }

    public void Reset()
    {
      _next = SampleCount = OverBudgetFrames = 0;
      _sum = _refresh = 0;
      TotalFrames = 0;
      TotalSeconds = AverageFramesPerSecond = CurrentFramesPerSecond = 0;
      MeanMilliseconds = P95Milliseconds = P99Milliseconds = MaxMilliseconds = 0;
    }
  }
}
