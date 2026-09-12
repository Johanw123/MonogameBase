using System;

namespace UntitledGemGame.Platform
{
  // All backend calls and events run on the initializing game thread.
  internal interface IPlayerProgressBackend
  {
    bool IsReady { get; }
    string InitializationError { get; }
    event Action<PlatformResult> StoreCompleted;
    bool GetInt(string name, out int value);
    bool GetFloat(string name, out float value);
    bool GetAchievement(string name, out bool unlocked);
    bool SetInt(string name, int value);
    bool SetFloat(string name, float value);
    bool Unlock(string name);
    bool Store();
  }
}
