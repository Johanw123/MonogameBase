using System.Collections.Generic;

public class IncomeTracker
{
    private readonly float _windowDuration;
    private readonly Queue<Sample> _samples = new();
    private float _elapsedTime;

    public float GemsPerSecond { get; private set; }
    public float GemsPerMinute => GemsPerSecond * 60f;

    private readonly struct Sample
    {
        public readonly float Timestamp;
        public readonly ulong DeliveredCount;

        public Sample(float timestamp, ulong deliveredCount)
        {
            Timestamp = timestamp;
            DeliveredCount = deliveredCount;
        }
    }

    /// <param name="windowDuration">Time in seconds to average rate over (3.0f to 5.0f recommended).</param>
    public IncomeTracker(float windowDuration = 3.0f)
    {
        _windowDuration = windowDuration;
    }

    public void Update(float deltaTime, ulong currentDelivered)
    {
        if (_samples.Count > 0 && currentDelivered < _samples.Peek().DeliveredCount)
            Reset();

        _elapsedTime += deltaTime;

        // 1. Record current snapshot
        _samples.Enqueue(new Sample(_elapsedTime, currentDelivered));

        // 2. Remove snapshots older than the window duration
        while (_samples.Count > 0 && (_elapsedTime - _samples.Peek().Timestamp) > _windowDuration)
        {
            _samples.Dequeue();
        }

        // 3. Calculate rate across the window
        if (_samples.Count > 1)
        {
            Sample oldest = _samples.Peek();
            float timeSpan = _elapsedTime - oldest.Timestamp;
            ulong gemsGained = currentDelivered - oldest.DeliveredCount;

            GemsPerSecond = timeSpan > 0.1f ? (float)gemsGained / timeSpan : 0f;
        }
        else
        {
            GemsPerSecond = 0f;
        }
    }

    public void Reset()
    {
        _samples.Clear();
        _elapsedTime = 0;
        GemsPerSecond = 0;
    }
}
