#if !KNI_WEB
using System;
using System.Diagnostics;
using System.Threading;
using Steamworks;

namespace UntitledGemGame.Platform
{
  internal static class SteamDiagnostics
  {
    // Runs on the initializing thread, without constructing a window or renderer.
    public static bool Run(IPlatformServices platform)
    {
      if (!platform.IsAvailable)
      {
        Console.WriteLine("[Steam check] FAIL: Steam initialization is required.");
        return false;
      }

      Console.WriteLine("[Steam check] Read-only diagnostics; no achievements, stats or files will be written.");
      var user = SteamUser.GetSteamID();
      var app = SteamUtils.GetAppID();
      bool loggedOn = SteamUser.BLoggedOn();
      bool subscribed = SteamApps.BIsSubscribedApp(app);
      Console.WriteLine($"[Steam check] User: {platform.PlayerName}; Steam ID: {user}; logged on: {loggedOn}");
      Console.WriteLine($"[Steam check] App: {app}; subscribed: {subscribed}; language: {SteamApps.GetCurrentGameLanguage()}");
      Console.WriteLine($"[Steam check] Friends count: {SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate)}");
      bool quotaRead = SteamRemoteStorage.GetQuota(out ulong totalBytes, out ulong availableBytes);
      Console.WriteLine($"[Steam check] Cloud enabled: account={SteamRemoteStorage.IsCloudEnabledForAccount()}, app={SteamRemoteStorage.IsCloudEnabledForApp()}; quota read={quotaRead}, available={availableBytes}/{totalBytes} bytes");

      bool statsReceived = false;
      bool statsOk = false;
      bool playersReceived = false;
      bool playersOk = false;
      using var stats = CallResult<UserStatsReceived_t>.Create((result, ioFailure) =>
      {
        statsReceived = true;
        statsOk = !ioFailure && result.m_eResult == EResult.k_EResultOK &&
                  result.m_steamIDUser == user && result.m_nGameID == app.m_AppId;
        Console.WriteLine($"[Steam check] Stats callback: {result.m_eResult}; I/O failure: {ioFailure}; matched request: {statsOk}");
      });
      using var players = CallResult<NumberOfCurrentPlayers_t>.Create((result, ioFailure) =>
      {
        playersReceived = true;
        playersOk = !ioFailure && result.m_bSuccess != 0;
        Console.WriteLine($"[Steam check] Player-count callback: success={playersOk}, players={result.m_cPlayers}, I/O failure={ioFailure}");
      });

      var statsRequest = SteamUserStats.RequestUserStats(user);
      var playersRequest = SteamUserStats.GetNumberOfCurrentPlayers();
      if (statsRequest != SteamAPICall_t.Invalid)
        stats.Set(statsRequest);
      else
      {
        statsReceived = true;
        Console.WriteLine("[Steam check] FAIL: Stats request returned an invalid handle.");
      }
      if (playersRequest != SteamAPICall_t.Invalid)
        players.Set(playersRequest);
      else
      {
        playersReceived = true;
        Console.WriteLine("[Steam check] FAIL: Player-count request returned an invalid handle.");
      }

      var timer = Stopwatch.StartNew();
      while ((!statsReceived || !playersReceived) && timer.Elapsed < TimeSpan.FromSeconds(15))
      {
        platform.Update();
        Thread.Sleep(10);
      }
      if (!statsReceived || !playersReceived)
        Console.WriteLine($"[Steam check] FAIL: Timed out waiting for callbacks (stats={statsReceived}, players={playersReceived}).");

      bool achievementsOk = statsOk;
      if (statsOk)
      {
        uint count = SteamUserStats.GetNumAchievements();
        Console.WriteLine($"[Steam check] Achievement definitions: {count} (showing at most 5)");
        for (uint index = 0; index < Math.Min(count, 5u); index++)
        {
          string name = SteamUserStats.GetAchievementName(index);
          bool read = SteamUserStats.GetUserAchievement(user, name, out bool unlocked);
          achievementsOk &= read;
          Console.WriteLine($"[Steam check] Achievement {name}: read={read}, unlocked={unlocked}");
        }
        if (count == 0)
          Console.WriteLine("[Steam check] SKIP: No achievement definitions available to verify achievement reads.");
      }

      bool passed = loggedOn && subscribed && quotaRead && statsOk && playersOk && achievementsOk;
      if (statsOk && SteamUserStats.GetNumAchievements() > 0)
      {
        string name = SteamUserStats.GetAchievementName(0);
        var read = platform.Achievements.IsUnlockedAsync(name);
        timer.Restart();
        while (!read.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(15))
        {
          platform.Update();
          Thread.Sleep(10);
        }
        bool readOk = read.IsCompletedSuccessfully && read.Result.Success;
        Console.WriteLine($"[Steam check] Queued achievement API: success={readOk}");
        passed &= readOk;
      }
      Console.WriteLine(passed
        ? "[Steam check] PASS: Identity, ownership, cloud quota, stats and async call results verified."
        : "[Steam check] FAIL: One or more checks failed; see results above.");
      Console.WriteLine("[Steam check] Overlay, writes and Windows behavior require separate testing.");
      return passed;
    }
  }
}
#endif
