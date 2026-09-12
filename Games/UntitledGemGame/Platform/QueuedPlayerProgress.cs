using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace UntitledGemGame.Platform
{
  internal sealed class QueuedPlayerProgress : IStatsService, IAchievementService, IDisposable
  {
    private sealed record Work(double EnqueuedAt, Action Run, Action<PlatformResult> Fail);
    private readonly object _gate = new();
    private readonly Queue<Work> _queue = new();
    private readonly IPlayerProgressBackend _backend;
    private readonly Func<double> _clock;
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private const double TimeoutSeconds = 30;
    private bool _disposed;
    private string _fault = "";
    private volatile bool _ready;
    private bool _dirty;
    private TaskCompletionSource<PlatformResult> _store;
    private double _storeStarted;

    internal QueuedPlayerProgress(IPlayerProgressBackend backend, Func<double> clock = null)
    {
      _backend = backend;
      _clock = clock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
      _backend.StoreCompleted += OnStored;
    }

    public bool IsReady => _ready;
    private static PlatformResult Invalid => new(PlatformStatus.InvalidArgument, "Use a non-empty API name without NUL characters and a finite numeric value.");
    private static bool Valid(string name) => !string.IsNullOrWhiteSpace(name) && !name.Contains('\0');
    private static PlatformResult Rejected(string name) => new(PlatformStatus.Rejected,
      $"Steam rejected '{name}'. Check the published API name, stat type and constraints.");
    private static TaskCompletionSource<T> Completion<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Enqueue(Action run, Action<PlatformResult> fail)
    {
      lock (_gate)
      {
        if (_disposed) fail(new(PlatformStatus.Disposed, "Platform services have shut down."));
        else if (_fault.Length != 0) fail(new(PlatformStatus.Unavailable, _fault));
        else if (_queue.Count >= 4096) fail(new(PlatformStatus.Busy, "Platform request queue is full."));
        else _queue.Enqueue(new Work(_clock(), run, fail));
      }
    }

    private Task<PlatformResult> Change(string name, bool valueValid, Func<PlatformResult> apply)
    {
      if (!Valid(name) || !valueValid) return Task.FromResult(Invalid);
      var completion = Completion<PlatformResult>();
      Enqueue(() =>
      {
        var result = apply();
        if (result.Success) _dirty = true;
        else Console.WriteLine($"[Steam progress] {result.Message}");
        completion.TrySetResult(result);
      }, result => completion.TrySetResult(result));
      return completion.Task;
    }

    private Task<PlatformResult<T>> Read<T>(string name, Func<PlatformResult<T>> read)
    {
      if (!Valid(name)) return Task.FromResult(new PlatformResult<T>(Invalid.Status, default, Invalid.Message));
      var completion = Completion<PlatformResult<T>>();
      Enqueue(() => completion.TrySetResult(read()),
        result => completion.TrySetResult(new(result.Status, default, result.Message)));
      return completion.Task;
    }

    public Task<PlatformResult<int>> GetIntAsync(string name) => Read(name, () =>
      _backend.GetInt(name, out int value) ? new PlatformResult<int>(PlatformStatus.Success, value)
        : new(PlatformStatus.Rejected, default, Rejected(name).Message));
    public Task<PlatformResult<float>> GetFloatAsync(string name) => Read(name, () =>
      _backend.GetFloat(name, out float value) ? new PlatformResult<float>(PlatformStatus.Success, value)
        : new(PlatformStatus.Rejected, default, Rejected(name).Message));
    public Task<PlatformResult<bool>> IsUnlockedAsync(string name) => Read(name, () =>
      _backend.GetAchievement(name, out bool value) ? new PlatformResult<bool>(PlatformStatus.Success, value)
        : new(PlatformStatus.Rejected, default, Rejected(name).Message));
    public Task<PlatformResult> Set(string name, int value) => Change(name, true, () =>
      _backend.SetInt(name, value) ? PlatformResult.Ok : Rejected(name));
    public Task<PlatformResult> Set(string name, float value) => Change(name, float.IsFinite(value), () =>
      _backend.SetFloat(name, value) ? PlatformResult.Ok : Rejected(name));
    public Task<PlatformResult> Unlock(string name) => Change(name, true, () =>
      _backend.Unlock(name) ? PlatformResult.Ok : Rejected(name));

    public Task<PlatformResult> Increment(string name, int amount = 1) => Change(name, true, () =>
    {
      if (!_backend.GetInt(name, out int current)) return Rejected(name);
      long next = (long)current + amount;
      if (next < int.MinValue || next > int.MaxValue)
        return new(PlatformStatus.InvalidArgument, $"Increment would overflow stat '{name}'.");
      return _backend.SetInt(name, (int)next) ? PlatformResult.Ok : Rejected(name);
    });

    public Task<PlatformResult> Increment(string name, float amount) => Change(name, float.IsFinite(amount), () =>
    {
      if (!_backend.GetFloat(name, out float current)) return Rejected(name);
      float next = current + amount;
      if (!float.IsFinite(next)) return new(PlatformStatus.InvalidArgument, $"Increment would overflow stat '{name}'.");
      return _backend.SetFloat(name, next) ? PlatformResult.Ok : Rejected(name);
    });

    public Task<PlatformResult> FlushAsync()
    {
      var completion = Completion<PlatformResult>();
      Enqueue(() =>
      {
        if (!_dirty)
        {
          completion.TrySetResult(PlatformResult.Ok);
          return;
        }
        _store = completion;
        _storeStarted = _clock();
        if (!_backend.Store())
          OnStored(new(PlatformStatus.Rejected, "Steam did not accept the save request. Changes remain pending; retry FlushAsync later."));
      }, result => completion.TrySetResult(result));
      return completion.Task;
    }

    private void OnStored(PlatformResult result)
    {
      AssertThread();
      if (_store == null) return;
      // Steam replaces invalid stats with server values. Do not blindly re-save them.
      if (result.Success || result.Status == PlatformStatus.Conflict) _dirty = false;
      if (!result.Success) Console.WriteLine($"[Steam progress] Save failed: {result.Message}");
      _store.TrySetResult(result);
      _store = null;
    }

    internal void Update()
    {
      AssertThread();
      if (_disposed) return;
      if (_backend.InitializationError.Length != 0) Fault(_backend.InitializationError);
      if (_store != null && _clock() - _storeStarted >= TimeoutSeconds)
      {
        var result = new PlatformResult(PlatformStatus.TimedOut,
          "Steam save confirmation timed out; its outcome is unknown. Restart the game before issuing more progress requests.");
        _store.TrySetResult(result);
        _store = null;
        Fault(result.Message); // A late callback has no request ID; don't let it confirm a newer save.
      }
      _ready = _fault.Length == 0 && _backend.IsReady;
      for (int processed = 0; processed < 256; processed++)
      {
        Work work;
        PlatformResult? failure = null;
        lock (_gate)
        {
          if (_queue.Count == 0) break;
          work = _queue.Peek();
          if (_fault.Length != 0) failure = new(PlatformStatus.Unavailable, _fault);
          else if (_clock() - work.EnqueuedAt >= TimeoutSeconds)
            failure = new(PlatformStatus.TimedOut, "Platform request timed out waiting for readiness or an earlier save.");
          else if (!_ready || _store != null) break;
          _queue.Dequeue();
        }
        if (failure.HasValue) work.Fail(failure.Value);
        else
        {
          try { work.Run(); }
          catch (Exception error)
          {
            work.Fail(new(PlatformStatus.Rejected, error.Message));
            Fault($"Platform backend failed: {error.Message}");
          }
        }
      }
    }

    private void Fault(string message)
    {
      lock (_gate)
      {
        if (_fault.Length == 0) Console.WriteLine($"[Steam progress] {message}");
        _fault = message;
      }
      _ready = false;
    }

    private void AssertThread()
    {
      if (Environment.CurrentManagedThreadId != _threadId)
        throw new InvalidOperationException("Pump and dispose platform services on the initializing game thread.");
    }

    public void Dispose()
    {
      AssertThread();
      lock (_gate)
      {
        if (_disposed) return;
        _disposed = true;
        _ready = false;
        var result = new PlatformResult(PlatformStatus.Disposed, "Platform services shut down before the request completed.");
        while (_queue.TryDequeue(out var work)) work.Fail(result);
        _store?.TrySetResult(result);
        _store = null;
      }
      _backend.StoreCompleted -= OnStored;
      if (_dirty) Console.WriteLine("[Steam progress] Shutting down with changes not confirmed saved. Await FlushAsync while the game loop is running.");
    }
  }
}
