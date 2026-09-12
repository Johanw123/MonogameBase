using System.Threading.Tasks;

namespace UntitledGemGame.Platform
{
  public enum PlatformStatus
  {
    Success, Unavailable, InvalidArgument, Rejected, Conflict, TimedOut, Busy, Disposed
  }

  public readonly record struct PlatformResult(PlatformStatus Status, string Message = "")
  {
    public bool Success => Status == PlatformStatus.Success;
    public static PlatformResult Ok => new(PlatformStatus.Success);
  }

  public readonly record struct PlatformResult<T>(PlatformStatus Status, T Value, string Message = "")
  {
    public bool Success => Status == PlatformStatus.Success;
  }

  public interface IStatsService
  {
    bool IsReady { get; }
    Task<PlatformResult<int>> GetIntAsync(string name);
    Task<PlatformResult<float>> GetFloatAsync(string name);
    // Success means applied to Steam's local state. FlushAsync confirms storage.
    Task<PlatformResult> Set(string name, int value);
    Task<PlatformResult> Set(string name, float value);
    Task<PlatformResult> Increment(string name, int amount = 1);
    Task<PlatformResult> Increment(string name, float amount);
    Task<PlatformResult> FlushAsync();
  }

  public interface IAchievementService
  {
    bool IsReady { get; }
    Task<PlatformResult<bool>> IsUnlockedAsync(string name);
    Task<PlatformResult> Unlock(string name);
  }

  internal sealed class UnavailableProgress : IStatsService, IAchievementService
  {
    internal static readonly UnavailableProgress Instance = new();
    public bool IsReady => false;
    private static Task<PlatformResult> Missing() => Task.FromResult(
      new PlatformResult(PlatformStatus.Unavailable, "Platform services are unavailable."));
    private static Task<PlatformResult<T>> Missing<T>() => Task.FromResult(
      new PlatformResult<T>(PlatformStatus.Unavailable, default, "Platform services are unavailable."));
    public Task<PlatformResult<int>> GetIntAsync(string name) => Missing<int>();
    public Task<PlatformResult<float>> GetFloatAsync(string name) => Missing<float>();
    public Task<PlatformResult<bool>> IsUnlockedAsync(string name) => Missing<bool>();
    public Task<PlatformResult> Set(string name, int value) => Missing();
    public Task<PlatformResult> Set(string name, float value) => Missing();
    public Task<PlatformResult> Increment(string name, int amount = 1) => Missing();
    public Task<PlatformResult> Increment(string name, float amount) => Missing();
    public Task<PlatformResult> Unlock(string name) => Missing();
    public Task<PlatformResult> FlushAsync() => Missing();
  }
}
