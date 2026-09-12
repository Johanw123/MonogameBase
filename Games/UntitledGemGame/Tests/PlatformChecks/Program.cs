using UntitledGemGame.Platform;

int checks = 0;
void Check(bool condition, string message)
{
  if (!condition) throw new Exception(message);
  checks++;
}
void Test(Action<FakeBackend, QueuedPlayerProgress, Action<double>> run)
{
  double time = 0;
  var backend = new FakeBackend();
  using var progress = new QueuedPlayerProgress(backend, () => time);
  run(backend, progress, value => time += value);
}

Check(!GameServices.Stats.IsReady, "Global API must be safe before initialization");
Check(GameServices.Stats.Increment("gems").Result.Status == PlatformStatus.Unavailable,
  "Unavailable API must explicitly report that writes were not applied");
Check(GameServices.Achievements.IsUnlockedAsync("first").Result.Status == PlatformStatus.Unavailable,
  "Unavailable reads must not pretend an achievement is locked");

Test((backend, progress, advance) =>
{
  backend.IsReady = false;
  var increments = new Task<PlatformResult>[200];
  Task.Run(() => Parallel.For(0, increments.Length, index => increments[index] = progress.Increment("gems"))).GetAwaiter().GetResult();
  progress.Update();
  Check(increments.All(t => !t.IsCompleted) && backend.WriteCount == 0, "Worker calls wait for readiness");
  backend.IsReady = true;
  progress.Update();
  Check(increments.All(t => t.Result.Success) && backend.Ints["gems"] == 200,
    "Concurrent increments must execute atomically on the owner thread without losing updates");
  var flush = progress.FlushAsync();
  var duplicateFlush = progress.FlushAsync();
  var later = progress.Increment("gems", 3);
  progress.Update();
  Check(backend.StoreCount == 1 && !flush.IsCompleted && !later.IsCompleted,
    "Flush batches updates and blocks newer mutations until confirmation");
  backend.Complete(PlatformResult.Ok);
  progress.Update();
  Check(flush.Result.Success && duplicateFlush.Result.Success && backend.StoreCount == 1,
    "Repeated flushes must share the confirmed state without redundant saves");
  Check(later.Result.Success && backend.Ints["gems"] == 203, "Later mutation resumes after save");
  var nextFlush = progress.FlushAsync();
  progress.Update();
  Check(backend.StoreCount == 2 && !nextFlush.IsCompleted, "New changes need a new confirmation");
  backend.Complete(PlatformResult.Ok);
});

Test((backend, progress, advance) =>
{
  var set = progress.Set("gems", 42);
  var read = progress.GetIntAsync("gems");
  var setFloat = progress.Set("distance", 1.5f);
  var incrementFloat = progress.Increment("distance", 2.25f);
  var readFloat = progress.GetFloatAsync("distance");
  var unlock = progress.Unlock("first");
  var achievement = progress.IsUnlockedAsync("first");
  progress.Update();
  Check(set.Result.Success && read.Result.Value == 42, "Reads must observe preceding writes");
  Check(setFloat.Result.Success && incrementFloat.Result.Success && readFloat.Result.Value == 3.75f,
    "Float stats support typed reads, writes and increments");
  Check(unlock.Result.Success && achievement.Result.Success && achievement.Result.Value, "Unlock must be observable through achievement reads");
  Check(backend.StoreCount == 0, "Mutations are batched until explicit flush");
});

Test((backend, progress, advance) =>
{
  Check(progress.Set("", 1).Result.Status == PlatformStatus.InvalidArgument, "Empty names must be rejected");
  Check(progress.Unlock("bad\0name").Result.Status == PlatformStatus.InvalidArgument, "Embedded NUL must be rejected");
  Check(progress.Set("distance", float.NaN).Result.Status == PlatformStatus.InvalidArgument, "NaN must be rejected");
  backend.Ints["gems"] = int.MaxValue;
  backend.Floats["distance"] = float.MaxValue;
  var overflow = progress.Increment("gems");
  var floatOverflow = progress.Increment("distance", float.MaxValue);
  var missing = progress.Set("missing", 1);
  var wrongType = progress.GetFloatAsync("gems");
  var flush = progress.FlushAsync();
  progress.Update();
  Check(overflow.Result.Status == PlatformStatus.InvalidArgument && floatOverflow.Result.Status == PlatformStatus.InvalidArgument,
    "Overflow must not wrap or reach the backend");
  Check(missing.Result.Status == PlatformStatus.Rejected && wrongType.Result.Status == PlatformStatus.Rejected,
    "Unknown names and incorrect stat types must fail explicitly");
  Check(flush.Result.Success && backend.WriteCount == 0 && backend.StoreCount == 0,
    "Rejected operations and a clean flush must not write anything");
});

Test((backend, progress, advance) =>
{
  progress.Increment("gems");
  backend.AcceptStore = false;
  var first = progress.FlushAsync();
  progress.Update();
  Check(first.Result.Status == PlatformStatus.Rejected, "Immediate store rejection must complete the task");
  backend.AcceptStore = true;
  var retry = progress.FlushAsync();
  progress.Update();
  backend.Complete(new(PlatformStatus.Rejected, "offline"));
  Check(retry.Result.Status == PlatformStatus.Rejected, "Server failures must reach the caller");
  var successfulRetry = progress.FlushAsync();
  progress.Update();
  backend.Complete(PlatformResult.Ok);
  Check(successfulRetry.Result.Success && backend.StoreCount == 3 && backend.Ints["gems"] == 1,
    "Retrying a save must retain dirty state without applying increments again");
});

Test((backend, progress, advance) =>
{
  progress.Set("gems", 100);
  var flush = progress.FlushAsync();
  progress.Update();
  backend.Ints["gems"] = 7; // Steam supplies the authoritative value after InvalidParam.
  backend.Complete(new(PlatformStatus.Conflict, "invalid parameter"));
  var reread = progress.GetIntAsync("gems");
  var retry = progress.FlushAsync();
  progress.Update();
  Check(flush.Result.Status == PlatformStatus.Conflict && reread.Result.Value == 7,
    "Conflicts must expose Steam's corrected value");
  Check(retry.Result.Success && backend.StoreCount == 1, "Do not automatically re-save rejected values");
});

Test((backend, progress, advance) =>
{
  progress.Increment("gems");
  var flush = progress.FlushAsync();
  progress.Update();
  var queued = progress.Unlock("first");
  advance(31);
  progress.Update();
  Check(flush.Result.Status == PlatformStatus.TimedOut && queued.Result.Status == PlatformStatus.Unavailable,
    "Unknown save outcome must stop the pipeline and complete pending callers");
  backend.Complete(PlatformResult.Ok);
  Check(progress.FlushAsync().Result.Status == PlatformStatus.Unavailable && backend.StoreCount == 1,
    "Late callback must not be mistaken for confirmation of a newer save");
});

Test((backend, progress, advance) =>
{
  backend.IsReady = false;
  var waiting = progress.Unlock("first");
  advance(31);
  progress.Update();
  Check(waiting.Result.Status == PlatformStatus.TimedOut && backend.WriteCount == 0, "Readiness waits are bounded");
  backend.IsReady = true;
  var later = progress.GetIntAsync("gems");
  progress.Update();
  Check(later.Result.Success, "A new request can run after late readiness");
});

Test((backend, progress, advance) =>
{
  var waiting = progress.Set("gems", 1);
  backend.InitializationError = "access denied";
  progress.Update();
  Check(waiting.Result.Status == PlatformStatus.Unavailable && backend.WriteCount == 0,
    "Initialization failures must complete queued tasks without writes");
});

Test((backend, progress, advance) =>
{
  progress.Increment("gems");
  var saving = progress.FlushAsync();
  progress.Update();
  var waiting = progress.Unlock("first");
  progress.Dispose();
  Check(saving.Result.Status == PlatformStatus.Disposed && waiting.Result.Status == PlatformStatus.Disposed,
    "Shutdown must complete both in-flight and queued requests");
  Check(progress.GetIntAsync("gems").Result.Status == PlatformStatus.Disposed, "Cached service references must be safe after shutdown");
});

Test((backend, progress, advance) =>
{
  backend.IsReady = false;
  for (int index = 0; index < 4096; index++) progress.GetIntAsync("gems");
  Check(progress.Unlock("first").Result.Status == PlatformStatus.Busy, "Queue must be bounded while unavailable");
});

Test((backend, progress, advance) =>
{
  var first = new FakePlatform(progress);
  var second = new FakePlatform(progress);
  GameServices.Attach(first);
  var read = Task.Run(() => GameServices.Stats.GetIntAsync("gems"));
  // Task.Run unwraps the read task; pump until its producer has queued the request.
  var deadline = System.Diagnostics.Stopwatch.StartNew();
  while (!read.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5))
  {
    progress.Update();
    Thread.Yield();
  }
  Check(read.IsCompletedSuccessfully && read.Result.Success, "Global facade works from worker threads");
  GameServices.Attach(second);
  GameServices.Detach(first);
  Check(ReferenceEquals(GameServices.Platform, second), "An old service must not detach a newer one");
  GameServices.Detach(second);
  Check(GameServices.Stats.FlushAsync().Result.Status == PlatformStatus.Unavailable, "Global facade falls back safely after shutdown");
});

Console.WriteLine($"Platform checks passed: {checks}. No Steam client or account writes used.");

sealed class FakePlatform(QueuedPlayerProgress progress) : IPlatformServices
{
  public bool IsAvailable => true;
  public string PlayerName => "Test";
  public IStatsService Stats => progress;
  public IAchievementService Achievements => progress;
  public void Update() => progress.Update();
  public void Dispose() => progress.Dispose();
}

sealed class FakeBackend : IPlayerProgressBackend
{
  private readonly int _owner = Environment.CurrentManagedThreadId;
  public bool IsReady { get; set; } = true;
  public string InitializationError { get; set; } = "";
  public event Action<PlatformResult> StoreCompleted;
  public Dictionary<string, int> Ints { get; } = new() { ["gems"] = 0 };
  public Dictionary<string, float> Floats { get; } = new() { ["distance"] = 0 };
  private readonly Dictionary<string, bool> _achievements = new() { ["first"] = false };
  public int WriteCount { get; private set; }
  public int StoreCount { get; private set; }
  public bool AcceptStore { get; set; } = true;
  private void AssertThread()
  {
    if (Environment.CurrentManagedThreadId != _owner) throw new Exception("Backend called from wrong thread");
  }
  public bool GetInt(string name, out int value) { AssertThread(); return Ints.TryGetValue(name, out value); }
  public bool GetFloat(string name, out float value) { AssertThread(); return Floats.TryGetValue(name, out value); }
  public bool GetAchievement(string name, out bool value) { AssertThread(); return _achievements.TryGetValue(name, out value); }
  public bool SetInt(string name, int value)
  {
    AssertThread();
    if (!Ints.ContainsKey(name)) return false;
    Ints[name] = value; WriteCount++; return true;
  }
  public bool SetFloat(string name, float value)
  {
    AssertThread();
    if (!Floats.ContainsKey(name)) return false;
    Floats[name] = value; WriteCount++; return true;
  }
  public bool Unlock(string name)
  {
    AssertThread();
    if (!_achievements.ContainsKey(name)) return false;
    _achievements[name] = true; WriteCount++; return true;
  }
  public bool Store() { AssertThread(); StoreCount++; return AcceptStore; }
  public void Complete(PlatformResult result) { AssertThread(); StoreCompleted?.Invoke(result); }
}
