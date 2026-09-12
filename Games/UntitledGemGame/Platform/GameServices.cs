using System.Threading;

namespace UntitledGemGame.Platform
{
  // Safe to access before startup, after shutdown, and from worker threads.
  public static class GameServices
  {
    private static IPlatformServices _platform = LocalPlatformServices.Instance;
    public static IPlatformServices Platform => Volatile.Read(ref _platform);
    public static IStatsService Stats => Platform.Stats;
    public static IAchievementService Achievements => Platform.Achievements;

    internal static void Attach(IPlatformServices platform) => Volatile.Write(ref _platform, platform);
    internal static void Detach(IPlatformServices platform) =>
      Interlocked.CompareExchange(ref _platform, LocalPlatformServices.Instance, platform);
  }
}
