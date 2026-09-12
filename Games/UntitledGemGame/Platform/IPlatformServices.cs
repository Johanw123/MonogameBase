using System;

namespace UntitledGemGame.Platform
{
  public interface IPlatformServices : IDisposable
  {
    bool IsAvailable { get; }
    string PlayerName { get; }
    IStatsService Stats { get; }
    IAchievementService Achievements { get; }
    void Update();
  }

  public sealed class LocalPlatformServices : IPlatformServices
  {
    public static readonly LocalPlatformServices Instance = new();
    public bool IsAvailable => false;
    public string PlayerName => string.Empty;
    public IStatsService Stats => UnavailableProgress.Instance;
    public IAchievementService Achievements => UnavailableProgress.Instance;
    public void Update() { }
    public void Dispose() { }
  }
}
