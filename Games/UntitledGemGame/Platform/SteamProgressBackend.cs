#if !KNI_WEB
using System;
using Steamworks;

namespace UntitledGemGame.Platform
{
  internal sealed class SteamProgressBackend : IPlayerProgressBackend, IDisposable
  {
    private readonly CSteamID _user = SteamUser.GetSteamID();
    private readonly AppId_t _app = SteamUtils.GetAppID();
    private readonly CallResult<UserStatsReceived_t> _load;
    private readonly Callback<UserStatsStored_t> _stored;
    private readonly Callback<UserStatsUnloaded_t> _unloaded;
    public bool IsReady { get; private set; }
    public string InitializationError { get; private set; } = "";
    public event Action<PlatformResult> StoreCompleted;

    internal SteamProgressBackend()
    {
      _stored = Callback<UserStatsStored_t>.Create(result =>
      {
        if (result.m_nGameID != _app.m_AppId) return;
        var status = result.m_eResult switch
        {
          EResult.k_EResultOK => PlatformStatus.Success,
          EResult.k_EResultInvalidParam => PlatformStatus.Conflict,
          _ => PlatformStatus.Rejected
        };
        string message = status switch
        {
          PlatformStatus.Success => "",
          PlatformStatus.Conflict => "Steam rejected invalid stats and supplied server values. Re-read the stats and correct the values or definitions before retrying.",
          _ => $"Steam returned {result.m_eResult}. Changes remain pending; retry FlushAsync later."
        };
        StoreCompleted?.Invoke(new(status, message));
      });
      _unloaded = Callback<UserStatsUnloaded_t>.Create(result =>
      {
        if (result.m_steamIDUser != _user) return;
        IsReady = false;
        InitializationError = "Steam unloaded the current user's stats. Restart the game to reload them.";
      });
      _load = CallResult<UserStatsReceived_t>.Create((result, ioFailure) =>
      {
        IsReady = !ioFailure && result.m_eResult == EResult.k_EResultOK &&
                  result.m_steamIDUser == _user && result.m_nGameID == _app.m_AppId;
        if (!IsReady) InitializationError = $"Could not load Steam stats ({result.m_eResult}, I/O failure={ioFailure}).";
      });
      // Request an explicit read to gate queued operations, including direct developer launches.
      // RequestCurrentStats is deprecated in the SDK shipped with this wrapper.
      var request = SteamUserStats.RequestUserStats(_user);
      if (request == SteamAPICall_t.Invalid) InitializationError = "Steam rejected the initial stats request.";
      else _load.Set(request);
    }

    public bool GetInt(string name, out int value) => SteamUserStats.GetStat(name, out value);
    public bool GetFloat(string name, out float value) => SteamUserStats.GetStat(name, out value);
    public bool GetAchievement(string name, out bool unlocked) => SteamUserStats.GetAchievement(name, out unlocked);
    public bool SetInt(string name, int value) => SteamUserStats.SetStat(name, value);
    public bool SetFloat(string name, float value) => SteamUserStats.SetStat(name, value);
    public bool Unlock(string name) => SteamUserStats.SetAchievement(name);
    public bool Store() => SteamUserStats.StoreStats();

    public void Dispose()
    {
      IsReady = false;
      _load.Dispose();
      _stored.Dispose();
      _unloaded.Dispose();
    }
  }
}
#endif
