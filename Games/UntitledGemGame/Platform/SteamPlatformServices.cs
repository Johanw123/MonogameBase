#if !KNI_WEB
using System;
using Steamworks;

namespace UntitledGemGame.Platform
{
  public sealed class SteamPlatformServices : IPlatformServices
  {
    // The game's Steam App ID.
    // During development, SteamAppId can be supplied through the environment.
    private const uint AppId = 5084070;

    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private volatile bool _initialized;
    private SteamProgressBackend _progressBackend;
    private QueuedPlayerProgress _progress;
    public bool IsAvailable => _initialized;
    public string PlayerName { get; private set; } = string.Empty;
    public IStatsService Stats => _progress;
    public IAchievementService Achievements => _progress;

    private SteamPlatformServices() { }

    // Call before constructing GameMain so the overlay can hook the renderer.
    public static IPlatformServices Start(out bool restartRequested)
    {
      restartRequested = false;
      var environmentAppId = Environment.GetEnvironmentVariable("SteamAppId");
      var appId = AppId;
      if (!string.IsNullOrWhiteSpace(environmentAppId) &&
          (!uint.TryParse(environmentAppId, out appId) || appId == 0))
      {
        Console.WriteLine("[Steam] Invalid SteamAppId; continuing without Steam.");
        return LocalPlatformServices.Instance;
      }

      if (appId == 0)
      {
        Console.WriteLine("[Steam] No App ID configured; continuing without Steam.");
        return LocalPlatformServices.Instance;
      }

      var service = new SteamPlatformServices();
      try
      {
        // Steam supplies SteamAppId when launching the game. A developer may
        // also set it explicitly to run locally without a Steam relaunch.
        if (string.IsNullOrWhiteSpace(environmentAppId) &&
            SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
        {
          restartRequested = true;
          Console.WriteLine("[Steam] Relaunching through Steam.");
          return LocalPlatformServices.Instance;
        }

        var result = SteamAPI.InitEx(out var error);
        if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
        {
          Console.WriteLine($"[Steam] Initialization failed ({result}): {error}. Continuing without Steam.");
          return LocalPlatformServices.Instance;
        }

        service._initialized = true;
        service._progressBackend = new SteamProgressBackend();
        service._progress = new QueuedPlayerProgress(service._progressBackend);
        service.PlayerName = SteamFriends.GetPersonaName();
        Console.WriteLine($"[Steam] Connected as {service.PlayerName} (App ID {SteamUtils.GetAppID()}).");
        return service;
      }
      catch (Exception error) when (error is DllNotFoundException ||
                                    error is EntryPointNotFoundException ||
                                    error is BadImageFormatException)
      {
        service.Dispose();
        Console.WriteLine($"[Steam] Could not load the Steam runtime: {error.Message}. Continuing without Steam.");
        return LocalPlatformServices.Instance;
      }
      catch
      {
        service.Dispose();
        throw;
      }
    }

    public void Update()
    {
      AssertThread();
      if (_initialized)
      {
        SteamAPI.RunCallbacks();
        _progress.Update();
      }
    }

    public void Dispose()
    {
      AssertThread();
      GameServices.Detach(this);
      if (!_initialized)
        return;

      _progress?.Dispose();
      _progressBackend?.Dispose();
      _initialized = false;
      PlayerName = string.Empty;
      SteamAPI.Shutdown();
    }

    private void AssertThread()
    {
      if (Environment.CurrentManagedThreadId != _threadId)
        throw new InvalidOperationException("Pump and dispose Steam on the initializing game thread. Stats and achievement requests may be queued from any thread.");
    }
  }
}
#endif
